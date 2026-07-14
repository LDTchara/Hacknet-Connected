using System;
using System.IO;
using System.Text;
using Hacknet;
using Hacknet.Extensions;
using Hacknet.Localization;
using Hacknet.PlatformAPI.Storage;
using ClientPlugin.Listing;

namespace ClientPlugin;

/// <summary>
/// 启动游戏会话 — 加载扩展 + 用账户存档进入游戏
/// 参考 ServerPlugin 的 CreateAccountPatch 和 TAXCore 的 ForceLoadOverrideStream
/// </summary>
public static class GameLauncher
{
    /// <summary>
    /// 用已有账户进入游戏（有存档则加载，无存档则新建）
    /// </summary>
    public static void LoginAndStart(AvailableServer server, ClientAccountStructure account)
    {
        try
        {
            Console.WriteLine($"[GameLauncher] Login: {account.Name} on {server.HostName}");
            Console.WriteLine($"[GameLauncher] GameUsername: {account.GameUsername}");

            var sman = Game1.getSingleton()?.sman;
            if (sman == null)
            {
                Console.WriteLine("[GameLauncher] ScreenManager not ready.");
                return;
            }

            ClientPluginMain.SelectedServer = server;
            ClientPluginMain.SelectedAccount = account;

            // 进入扩展会话（和 ServerPlugin 的 LAUNCH_SERVER 一样的路径）
            StartExtensionSession(server, account, sman);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameLauncher] Error: {ex}");
        }
    }

    /// <summary>
    /// 初始化扩展会话并进入游戏
    /// 与服务器 CreateAccountPatch 采用相同的原生 Login 流程：
    ///   1. FileExists() 检查存档是否存在
    ///   2. 存在 → WillLoadSave = true → new OS() → OS 构造器跳过 People.init 和默认节点
    ///   3. 不存在 → People.init() → new OS() → 新建游戏
    /// </summary>
    private static void StartExtensionSession(AvailableServer server, ClientAccountStructure account, ScreenManager sman)
    {
        // ════════════════════════════════════════════════════════
        // 种子初始化：在 new OS() 之前注入基础种子
        // ════════════════════════════════════════════════════════
        int seedToApply = 0;
        bool hasSeed = false;

        // 优先级 1：已通过 LiteNetLib 从服务端异步收到的种子
        if (ClientPluginMain.WorldSeed.HasValue)
        {
            seedToApply = ClientPluginMain.WorldSeed.Value;
            hasSeed = true;
            Console.WriteLine($"[GameLauncher] Seed applied from server (async): {seedToApply}");
        }
        // 优先级 2：上次从服务端同步后保存在本地的种子
        else if (server.WorldSeed > 0)
        {
            seedToApply = server.WorldSeed;
            hasSeed = true;
            Console.WriteLine($"[GameLauncher] Seed applied from local cache: {seedToApply}");
        }

        if (hasSeed)
        {
            WorldSeedSync.Initialize(seedToApply);
        }
        else
        {
            Console.WriteLine("[GameLauncher] WARNING: No seed received from server. World may not match!");
        }

        // 扩展已由 Pathfinder 在模组加载时初始化，直接使用
        var eif = ExtensionLoader.ActiveExtensionInfo;
        if (eif == null)
        {
            Console.WriteLine("[GameLauncher] ActiveExtensionInfo is null! Cannot start session.");
            return;
        }

        string extPath = eif.GetFullFolderPath();
        if (!Directory.Exists(extPath))
        {
            Console.WriteLine($"[GameLauncher] Extension not found: {extPath}");
            return;
        }

        // 确保扩展模式已激活
        Settings.IsInExtensionMode = true;
        LocaleActivator.ActivateLocale(eif.Language, Game1.getSingleton().Content);
        SaveFileManager.Init();

        // ════════════════════════════════════════════════════════
        // 账户逻辑：使用 GameUsername 检查账户是否存在
        // ════════════════════════════════════════════════════════
        bool nativeAccountExists = SaveFileManager.Accounts.Any(a =>
            a.Username.Equals(account.GameUsername, StringComparison.OrdinalIgnoreCase));
        string filePath;

        if (nativeAccountExists)
        {
            filePath = SaveFileManager.GetFilePathForLogin(account.GameUsername, account.Password) ?? "";
            Console.WriteLine($"[GameLauncher] Native account exists: {account.GameUsername}, path={filePath}");
        }
        else
        {
            SaveFileManager.AddUser(account.GameUsername, account.Password);
            // AddUser 只写内存 manifest，手动持久化
            try
            {
                var storage = SaveFileManager.StorageMethods?[0];
                if (storage != null)
                    storage.GetSaveManifest().Save(storage);
            }
            catch { }
            filePath = SaveFileManager.GetFilePathForLogin(account.GameUsername, account.Password) ?? "";
            Console.WriteLine($"[GameLauncher] Created new native account: {account.GameUsername}, path={filePath}");
        }

        // 更新存档路径到配置文件
        account.SaveFilePath = filePath;
        ClientProfileStorage.Save();

        // ── 等待服务端存档传输完成（带超时，防止死等） ──
        if (!ClientPluginMain.IsSaveFileReady)
        {
            Console.WriteLine("[GameLauncher] Waiting for server save file...");
            int waited = 0;
            while (!ClientPluginMain.IsSaveFileReady && waited < 100)
            {
                System.Threading.Thread.Sleep(100);
                waited++;
            }
            if (ClientPluginMain.IsSaveFileReady)
                Console.WriteLine($"[GameLauncher] Save file ready after ~{waited * 100}ms wait");
            else
                Console.WriteLine("[GameLauncher] Timeout waiting for save file, continuing without it");
        }

        // ── 写入从服务端接收的存档文件（覆盖本地旧存档） ──
        if (ClientPluginMain.PendingSaveFile != null && !string.IsNullOrEmpty(filePath))
        {
            try
            {
                var storage0 = SaveFileManager.StorageMethods?[0];
                if (storage0 != null)
                {
                    storage0.WriteFileData(filePath, ClientPluginMain.PendingSaveFile);
                    Console.WriteLine($"[GameLauncher] Written server save file: {ClientPluginMain.PendingSaveFile.Length} bytes -> {filePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameLauncher] Failed to write server save file: {ex.Message}");
            }
            ClientPluginMain.PendingSaveFile = null;
        }

        // ════════════════════════════════════════════════════════
        // 遵循原生 Login 按钮流程检查存档
        // ════════════════════════════════════════════════════════
        bool saveExists = !string.IsNullOrEmpty(filePath)
            && SaveFileManager.StorageMethods?[0].FileExists(filePath) == true;

        if (saveExists)
        {
            OS.WillLoadSave = true;
            Console.WriteLine($"[GameLauncher] Found existing save! Loading: {filePath}");
        }
        else
        {
            People.init();
            Console.WriteLine($"[GameLauncher] No save found ({filePath}), starting new game.");
        }

        var oS = new OS();
        oS.SaveGameUserName = filePath;
        oS.SaveUserAccountName = account.GameUsername;

        sman.AddScreen(oS);

        // 无存档时新建游戏（有存档时 OS.Update 自动 loadSaveFile）
        if (!saveExists)
        {
            ExtensionLoader.LoadNewExtensionSession(eif, oS);
        }

        // ════════════════════════════════════════════════════════
        // 恢复服务端的精确随机状态
        // 优先级：内存中的最新数据 > INI 持久化的缓存数据
        // ════════════════════════════════════════════════════════
        string? stateToRestore = ClientPluginMain.RandomState;
        int counterToRestore = ClientPluginMain.ChildSeedCounter;
        if (string.IsNullOrEmpty(stateToRestore))
            stateToRestore = server.RandomState;
        if (counterToRestore <= 0)
            counterToRestore = server.ChildSeedCounter;

        if (!string.IsNullOrEmpty(stateToRestore))
        {
            Utils.random.RestoreState(stateToRestore);
            Console.WriteLine($"[GameLauncher] Utils.random state restored from server (after OS creation)");
        }
        if (counterToRestore > 0)
        {
            WorldSeedSync.ChildSeedCounter = counterToRestore;
            Console.WriteLine($"[GameLauncher] Child seed counter restored: {counterToRestore}");
        }

        // ── IP 计数器 ──
        int ipCounter = ClientPluginMain.IpCounter;
        if (ipCounter > 0)
        {
            try
            {
                var st = Type.GetType("SeedSync.Global.SeedSyncGlobalPlugin");
                if (st != null)
                {
                    var f = st.GetField("GlobalIpCounter",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (f != null)
                    {
                        f.SetValue(null, ipCounter);
                        Console.WriteLine($"[GameLauncher] IP counter set to {ipCounter}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameLauncher] Failed to set IP counter: {ex.Message}");
            }
        }
    }

    /// <summary>从扩展名配置推断扩展目录</summary>
    private static string GetExtensionName(AvailableServer server)
    {
        // 1. 优先使用 INI 中配置的 ExtensionName
        if (!string.IsNullOrEmpty(server.ExtensionName))
        {
            string ep = Path.Combine(ClientPluginMain.EnvPath, "Extensions", server.ExtensionName);
            if (Directory.Exists(ep))
                return server.ExtensionName;
        }

        // 2. 试 HostName 作为扩展名
        string name = server.HostName;
        string extPath = Path.Combine(ClientPluginMain.EnvPath, "Extensions", name);
        if (Directory.Exists(extPath))
            return name;

        // 3. 试 FolderPath 的最后一段
        if (!string.IsNullOrEmpty(server.FolderPath))
        {
            string folderName = Path.GetFileName(server.FolderPath);
            extPath = Path.Combine(ClientPluginMain.EnvPath, "Extensions", folderName);
            if (Directory.Exists(extPath))
                return folderName;
        }

        // 4. 仍找不到，提示用户
        Console.WriteLine("[GameLauncher] WARNING: Cannot find extension directory!");
        Console.WriteLine("[GameLauncher] Set ExtensionName=\"xxx\" in ClientProfiles.ini");
        return ExtensionLoader.ActiveExtensionInfo.Name; // fallback，但会打印上面那个警告
    }
}
