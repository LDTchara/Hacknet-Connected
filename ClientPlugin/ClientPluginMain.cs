using BepInEx;
using BepInEx.Hacknet;
using HarmonyLib;
using ClientPlugin.Listing;

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

    private static Harmony? _harmony;

    public override bool Load()
    {
        _harmony = new Harmony(ModGUID);
        _harmony.PatchAll(typeof(ClientPluginMain).Assembly);

        // 加载配置
        ClientProfileStorage.Load();

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
        Console.WriteLine("[ClientPlugin.PostLoad] Checking static Random fields...");
        WorldSeedSync.FixAllStaticRandomFields();
    }
}
