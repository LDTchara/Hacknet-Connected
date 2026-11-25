using BepInEx;
using BepInEx.Hacknet;
using Hacknet;
using Hacknet.Extensions;
using Hacknet.Gui;
using Hacknet.Screens;
using Pathfinder;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hacknet.PlatformAPI.Storage;

namespace ServerPlugin;

[BepInPlugin(ModGUID, ModName, ModVer)]
public class ServerPlugin : HacknetPlugin
{
    public const string ModGUID = "com.ServerPlugin.Tesr";
    public const string ModName = "ServerPlugin";
    public const string ModVer = "1.0.0";

    /// <summary>服务端主机的特殊玩家角色（从扩展根 Config.tmp 读取，如 "p0"）</summary>
    public static string? SelectPlayerConfig = null;

    private static Harmony? _harmony;

    // 必须先于任何 HNC.Sync 类型的加载（PatchAll 会枚举程序集里所有类型，其中 ServerSync 引用了 HNC.Sync）
    static ServerPlugin()
    {
        HncAssemblyResolver.Install(typeof(ServerPlugin).Assembly);
    }

    public override bool Load()
    {
        _harmony = new Harmony(ModGUID);
        // 单 dll 合并构建时，ServerClientEventsMod 的 Load 也会 PatchAll 同一个程序集；这里去重
        ServerClientEventsMod.Events.HarmonyPatchOnce.PatchAll(_harmony, typeof(ServerPlugin).Assembly);
        return true;
    }

    /// <summary>
    /// PostLoad 在所有插件的 Load() 执行完毕后调用。
    /// 此时其他插件的 static readonly Random 字段已经初始化完毕，
    /// 我们可以反射枚举它们并注入确定性子种子。
    /// </summary>
    public override void PostLoad()
    {
        ServerMail.RegisterCommand();
        HNC.IrcChat.IrcChatFeature.Register();
        HNC.RemovableTags.RemovableTagsFeature.Register();

        Console.WriteLine("[ServerPlugin.PostLoad] Scanning for static Random fields...");

        // FixAllStaticRandomFields 已经内部处理未初始化的 pending 标记，
        // 所以直接调用即可，无需提前判断。
        WorldSeedSync.FixAllStaticRandomFields();
    }
}

/// <summary>
/// 日志观察——不拦截，只打印
/// </summary>
[HarmonyPatch(typeof(MainMenu), "CreateNewAccountForExtensionAndStart")]
internal static class CreateAccountPatch
{
    /// <summary>由 HandleLaunchServer 触发时为 true</summary>
    public static bool IsServerLaunch = false;

    static bool Prefix(string username, string pass, ScreenManager sman, GameScreen currentScreen, ExtensionsMenuScreen extensionsScreen)
    {
        Console.WriteLine($"[CreateAccount] called: user={username} pass={pass}");
        Console.WriteLine($"[CreateAccount]   currentScreen={currentScreen?.GetType().Name}");
        Console.WriteLine($"[CreateAccount]   ActiveExtensionInfo={ExtensionLoader.ActiveExtensionInfo?.Name}");
        Console.WriteLine($"[CreateAccount]   IsServerLaunch={IsServerLaunch}");
        Console.WriteLine($"[CreateAccount]   Settings.IsInExtensionMode={Settings.IsInExtensionMode}");

        if (!IsServerLaunch)
            return true; // 原版流程——不拦截

        IsServerLaunch = false;

        Console.WriteLine("[CreateAccount] SERVER LAUNCH PATH");

        // ════════════════════════════════════════════════════════
        // 种子注入：在 new OS() 之前生成种子并注入 Utils.random
        // ════════════════════════════════════════════════════════

        // 先尝试从持久化文件加载种子（服务端重启后复用）
        int seed = WorldSeedSync.LoadPersistedSeed(username);
        if (seed < 0)
        {
            // 没有持久化种子，生成新种子
            seed = new Random().Next();
            // 持久化到文件，下次服务端重启可用
            WorldSeedSync.PersistSeed(username, seed);
        }
        else
        {
            Console.WriteLine($"[CreateAccount] Loaded persisted seed: {seed}");
        }

        WorldSeedSync.Initialize(seed);
        Console.WriteLine($"[CreateAccount] World seed: {seed}");

        // ── 读取扩展根 Config.tmp：服务端特殊玩家角色 ──
        try
        {
            var eif = ExtensionLoader.ActiveExtensionInfo;
            if (eif != null)
            {
                string cfgPath = Path.Combine(eif.GetFullFolderPath(), "Config.tmp");
                if (File.Exists(cfgPath))
                {
                    string role = File.ReadAllText(cfgPath).Trim()
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(role))
                    {
                        ServerPlugin.SelectPlayerConfig = role;
                        Console.WriteLine($"[CreateAccount] Server role (Config.tmp) = {role}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CreateAccount] ERROR reading Config.tmp for server role: {ex.Message}");
        }

        // ── 加载服务端角色的 PTR 映射（用于 ComputerLoader.filter 替换） ──
        try
        {
            var eif2 = ExtensionLoader.ActiveExtensionInfo;
            if (eif2 != null)
                ServerPtrPatch.LoadFromExtension(eif2.GetFullFolderPath());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CreateAccount] ERROR loading server PTR mappings: {ex.Message}");
        }

        // ── 账户 & 存档检查 ──
        Settings.IsInExtensionMode = true;
        SaveFileManager.Init();

        // 获取存档文件路径（优先 GetFilePathForLogin）
        var filePath = SaveFileManager.GetFilePathForLogin(username, pass);
        if (filePath == null)
        {
            SaveFileManager.AddUser(username, pass);
            // 冗余写入所有 StorageMethod（修复 [0] 指向不确定的问题）
            SaveFileHelper.SaveManifestToAll();
            filePath = SaveFileManager.GetFilePathForLogin(username, pass) ?? "";
        }

        // ════════════════════════════════════════════════════════
        // 调试：遍历所有 StorageMethod + 扫描文件系统目录
        // ════════════════════════════════════════════════════════
        Console.WriteLine($"[CreateAccount] Save file: (from GetFilePathForLogin) = '{filePath}'");
        SaveFileHelper.DebugPrintAllStorages(filePath);

        try
        {
            // 扫描各种可能的存档目录
            string personal = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            string extName = ExtensionLoader.ActiveExtensionInfo?.GetFoldersafeName() ?? "?";

            string[] candidateDirs = new string[]
            {
                personal + "/My Games/Hacknet/Accounts/" + extName + "/",
                personal + "/My Games/Hacknet/Accounts/",
                personal + "/My Games/HacknetPathfinder/Accounts/" + extName + "/",
                personal + "/My Games/HacknetPathfinder/Accounts/" + extName + "/HHBS/",
                personal + "/My Games/Hacknet/",
                personal + "/My Games/HacknetPathfinder/",
            };

            foreach (var dir in candidateDirs)
            {
                if (Directory.Exists(dir))
                {
                    Console.WriteLine($"[CreateAccount] DIR EXISTS: {dir}");
                    foreach (var f in Directory.GetFiles(dir, "*.xml"))
                        Console.WriteLine($"[CreateAccount]   FILE: {f}");
                }
                else
                {
                    Console.WriteLine($"[CreateAccount] DIR MISSING: {dir}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CreateAccount] DEBUG ERROR: {ex.Message}");
        }
        Console.WriteLine("═══════════ END DEBUG ═══════════");

        // ════════════════════════════════════════════════════════
        // 遵循原生 Login 按钮流程
        // ════════════════════════════════════════════════════════
        // 遍历所有 StorageMethod 找有效存档（修复 [0] 指向不确定的问题）
        bool saveExists = !string.IsNullOrEmpty(filePath)
            && SaveFileHelper.SaveExists(filePath);

        if (saveExists)
        {
            OS.WillLoadSave = true;
            Console.WriteLine($"[CreateAccount] Found existing save! Loading: {filePath}");
        }
        else
        {
            People.init();
            Console.WriteLine($"[CreateAccount] No save found ({filePath}), starting new game.");
        }

        if (ExtensionLoader.ActiveExtensionInfo == null)
            Console.WriteLine("[CreateAccount]   WARNING: ActiveExtensionInfo is null!");

        var oS = new OS();
        oS.SaveGameUserName = filePath;
        oS.SaveUserAccountName = username;

        if (currentScreen != null)
            currentScreen.ExitScreen();

        if (!Settings.soundDisabled)
            sman.playAlertSound();

        sman.AddScreen(oS, sman.controllingPlayer);

        Console.WriteLine($"[CreateAccount]   New OS added. currentInstance={OS.currentInstance != null}");
        return false;
    }
}

/// <summary>
/// 扩展详情页添加 [Start Server] 按钮
/// </summary>
[HarmonyPatch(typeof(ExtensionsMenuScreen), "DrawExtensionInfoDetail")]
internal static class CreateServerButtonPatch
{
    static void Postfix(Vector2 drawpos)
    {
        var alreadyActive = ServerManager.IsActive;
        if (Button.doButton(8900001, (int)drawpos.X, (int)drawpos.Y + 30, 450, 25,
            alreadyActive ? "[ Server Mode Active ]" : "[ Start Server ]",
            alreadyActive ? MainMenu.exitButtonColor : MainMenu.buttonColor))
        {
            if (!alreadyActive)
            {
                ServerManager.Activate();
                EventForwarder.Subscribe();
                Task.Run(() => ServerManager.ConnectToConsole());
            }
        }
    }
}

/// <summary>
/// 退出扩展页面时重置服务端状态
/// </summary>
[HarmonyPatch(typeof(ExtensionsMenuScreen), "ExitExtensionsScreen")]
internal static class ResetServerStatePatch
{
    static void Postfix()
    {
        ServerManager.Reset();
        WorldSeedSync.Reset();
        ServerSync.Stop();
        ServerLobby.Reset();
        ServerPause.Reset();
        ServerPhases.Reset();
    }
}

/// <summary>
/// 进入游戏后显示状态提示（驱动消息处理）
/// </summary>
[HarmonyPatch(typeof(Game1), "Update", new Type[] { typeof(GameTime) })]
internal static class GameUpdatePatch
{
    private static bool _shown;
    private static bool _detectorTested;

    static void Postfix()
    {
        // 测试 EventDetectorManager 是否运行
        if (!_detectorTested)
        {
            _detectorTested = true;
            var os1 = OS.currentInstance;
            if (os1 != null)
            {
                Console.WriteLine("[GameUpdate] OS active. EventForwarder.Subscribe() during play.");
            }
        }

        // 注意：全员暂停时原版 Game1.Update 被跳过，但这个后置补丁照常执行（网络、管道、快捷键都靠它）
        ServerManager.ProcessPendingMessages();
        ServerLobby.Update();
        ServerPause.Update();
        ServerPhases.Update();
        ServerSync.Update();
        ServerMail.Update();

        // ── 种子已生成且 Pipe 已连接，发送种子（含随机状态+计数器+IP计数器） ──
        if (WorldSeedSync.IsInitialized && !WorldSeedSync.SeedSent && ServerManager.IsConnected)
        {
            string state = Utils.random.SaveState();
            int childCounter = WorldSeedSync.ChildSeedCounter;
            int ipCounter = 0;
            try
            {
                var st = Type.GetType("SeedSync.Global.SeedSyncGlobalPlugin");
                if (st != null)
                {
                    var f = st.GetField("GlobalIpCounter",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (f != null) ipCounter = (int)f.GetValue(null);
                }
            }
            catch { }
            ServerManager.SendEvent($"EVENT:WORLD_SEED:{WorldSeedSync.MasterSeed}:{childCounter}:{state}:{ipCounter}");
            WorldSeedSync.SeedSent = true;
            Console.WriteLine($"[GameUpdate] WORLD_SEED sent: {WorldSeedSync.MasterSeed} (childCtr={childCounter}, ipCtr={ipCounter})");
        }

        if (!ServerManager.IsActive || _shown) return;

        var os = OS.currentInstance;
        if (os == null) return;

        _shown = true;
        os.write(" ");
        os.write("╔══════════════════════════════╗");
        os.write("║     SERVER MODE ACTIVE      ║");
        os.write("╠══════════════════════════════╣");
        os.write("║  Confirm in ServerConsole   ║");
        os.write("╚══════════════════════════════╝");
    }
}