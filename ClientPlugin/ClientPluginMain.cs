using BepInEx;
using BepInEx.Hacknet;
using ClientPlugin.Listing;
using Hacknet;
using HarmonyLib;

namespace ClientPlugin;

[BepInPlugin(ModGUID, ModName, ModVer)]
public class ClientPluginMain : HacknetPlugin
{
    public const string ModGUID = "com.ClientPlugin.AP";
    public const string ModName = "ClientPlugin";
    public const string ModVer = "1.0.0";

    /// <summary>游戏根目录</summary>
    public static string EnvPath => Environment.CurrentDirectory;

    // ── 选择状态（GUI 层传递） ──
    public static AvailableServer? SelectedServer;
    public static ClientAccountStructure? SelectedAccount;
    
    // ── 角色选择配置 ──
    /// <summary>
    /// 客户端当前选择的角色标识（如 "p1"、"p2"、"p3"）。
    /// 对应 Players-Cli.xml 中 Globle SelectedName 的值，
    /// 由角色选择界面设定，并通过 PlayerDataManager 持久化。
    /// </summary>
    public static string? SelectPlayerConfig = null;

    // ── 世界种子 ──
    /// <summary>从服务端接收的世界种子（由 EventReceiver 设置）</summary>
    public static int? WorldSeed = null;

    /// <summary>服务端 Utils.random 的完整内部状态，用于精确恢复而非 ReSeed</summary>
    public static string? RandomState = null;

    /// <summary>服务端的 _childSeedCounter，确保 new Random() 子种子分配一致</summary>
    public static int ChildSeedCounter = 0;

    /// <summary>服务端的 GlobalIpCounter，对齐 NetworkMap.generateRandomIP()</summary>
    public static int IpCounter = 0;

    /// <summary>从服务端接收的存档文件原始字节，GameLauncher 会用到</summary>
    public static byte[]? PendingSaveFile = null;

    /// <summary>存档文件分块接收：{seq → base64部分}</summary>
    public static Dictionary<int, string>? PendingSaveChunks = null;
    public static int PendingSaveTotal = 0;
    public static string? PendingSaveFileB64 = null;

    /// <summary>存档文件是否已完整接收，GameLauncher 等待此标志</summary>
    public static volatile bool IsSaveFileReady = false;

    /// <summary>服务端是否有该玩家的存档（HELLO 握手时由 HAS_SAVE/NO_SAVE 设置）</summary>
    public static volatile bool HasServerSave = false;

    /// <summary>服务端在握手时明确回复了 NO_SAVE（区别于"还没收到回复"）</summary>
    public static volatile bool ServerReportedNoSave = false;

    private static Harmony? _harmony;

    // 必须先于任何 HNC.Sync 类型的加载（PatchAll 会枚举程序集里所有类型，其中 ClientSync 引用了 HNC.Sync）
    static ClientPluginMain()
    {
        HncAssemblyResolver.Install(typeof(ClientPluginMain).Assembly);
    }

    public override bool Load()
    {
        _harmony = new Harmony(ModGUID);
        // 单 dll 合并构建时，ServerClientEventsMod 的 Load 也会 PatchAll 同一个程序集；这里去重
        ServerClientEventsMod.Events.HarmonyPatchOnce.PatchAll(_harmony, typeof(ClientPluginMain).Assembly);

        // 加载配置
        ClientProfileStorage.Load();

        // HNC.Sync：尽早创建引擎，其他 mod 在 PostLoad 里就能看到 HncSync.Runtime
        ClientSync.Init();

        Console.WriteLine($"[ClientPlugin] Loaded. EnvPath={EnvPath}");
        return true;
    }

    /// <summary>
    /// PostLoad 在所有插件的 Load() 执行完毕后调用。
    /// 尝试修复已存在的 static Random 字段。
    /// 注意：此时种子可能还没从服务端收到，
    /// FixAllStaticRandomFields 会判断 IsInitialized，未就绪则延迟。
    /// </summary>
    public override void PostLoad()
    {
        // 注册 selectplayer 命令
        SelectPlayerCommand.Register();
        SendMailCommand.Register();
        HNC.IrcChat.IrcChatFeature.Register();
        HNC.RemovableTags.RemovableTagsFeature.Register();

        // P0-1：此时所有插件已加载，给 PF/第三方程序集里的 Condition 补 scope 门控
        if (_harmony != null)
        {
            ScopeGatePatches.PatchExternalGates(_harmony);
            ScopeFactionPatches.Apply(_harmony);
        }

        Console.WriteLine("[ClientPlugin.PostLoad] Checking static Random fields...");
        WorldSeedSync.FixAllStaticRandomFields();
    }
}
