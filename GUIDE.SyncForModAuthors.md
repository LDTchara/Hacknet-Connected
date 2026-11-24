# HNC.Sync：给 Mod 作者的适配指南

> 状态（2026-11-23）：API 与核心引擎已写好并有单元测试（`HNC.Sync.Tests`，66 个通过）；
> 联机传输层（M1）**已接入 ClientPlugin / ServerPlugin / 终端，但还没有在游戏里验证**。
> 凡是"尚未实现"或"未验证"的地方都会明确标出。

## 1. 它解决什么问题

Hacknet-Connected 里，服务端是一个完整的游戏实例，是世界状态的权威；客户端各自运行自己的游戏。
你的 mod 如果有**运行时会变化、且别的玩家需要看到**的状态（自定义 Daemon 的数据、自定义 Module 的字段、带状态的 Executable……），
就需要把这些变化同步过去。HNC.Sync 让你只声明"这些字段要同步"，不用自己碰网络、序列化、广播、重连。

不在它的范围内：存档里本来就有的初始世界（由存档传输负责）、终端历史/Exe 实例/Trace 倒计时这类纯本地状态、
以及**玩家操作事件**（那是 `ServerClientEventsMod` 的 `PlayerEventAPI`）。

## 2. 引用方式（对单机零影响）

只引用 **`HNC.Sync.Abstractions.dll`**（`netstandard2.0`，不依赖游戏、Harmony、Pathfinder）。
没安装 HNC 时 `HncSync.Runtime` 为 `null`，下面所有 `HncSync.*` 调用都是空操作，`[Sync]` 特性是惰性的，你的 mod 照常单机运行。

不要引用 `HNC.Sync.Core`——它是宿主（ClientPlugin / ServerPlugin）用的实现，接口可能变。

```xml
<Reference Include="HNC.Sync.Abstractions">
  <HintPath>libs\HNC.Sync.Abstractions.dll</HintPath>
  <Private>false</Private>   <!-- 不要打进你的 mod：运行时由 HNC 提供 -->
</Reference>
```

### 加载顺序与软依赖

`HncSync.Runtime` 由 ClientPlugin / ServerPlugin 在它们的 `Load()` 里设置。BepInEx 默认不保证你的 mod 的 `Load()` 排在它们后面，
所以如果你在**自己的 `Load()`** 里就调用 `HncSync.Register`，可能赶在 Runtime 之前，注册被静默丢弃。两种办法，推荐第一种：

1. **把注册放到 `PostLoad()` 或世界加载之后**（所有插件的 `Load()` 都跑完了，Runtime 一定已就绪）。
2. **声明软依赖**，让 BepInEx 先加载 HNC。软依赖在 HNC 没装时不会报错（和 Stuxnet 对 XMOD 的做法一样）：

```csharp
[BepInDependency("com.ClientPlugin.AP",  BepInDependency.DependencyFlags.SoftDependency)]  // HNC 客户端
[BepInDependency("com.ServerPlugin.Tesr", BepInDependency.DependencyFlags.SoftDependency)] // HNC 服务端
public class MyMod : HacknetPlugin { ... }
```

两端各是不同的插件，所以两个 GUID 都要声明（单机 / 没装 HNC 时两个都不存在，照常加载）。

要在代码里区分"装了 HNC 吗"，有两个不同含义的检查：

```csharp
// 1) HNC 插件是否被加载（和 Stuxnet 检测 XMOD 一样）——只说明装了，不说明正在联机
bool hncInstalled = HacknetChainloader.Instance.Plugins.ContainsKey("com.ClientPlugin.AP")
                 || HacknetChainloader.Instance.Plugins.ContainsKey("com.ServerPlugin.Tesr");

// 2) 同步层是否真的在工作（已联机且引擎就绪）——决定要不要走同步逻辑用这个
bool syncing = HncSync.IsActive;
```

注意：第 1 种检查要引用 BepInEx.Hacknet，但**不需要**引用任何 HNC 程序集，所以能在"没装 HNC"的环境里安全运行；
第 2 种只需要 `HNC.Sync.Abstractions`，未联机时返回 `false`。

## 3. 三种接入方式

### 方式 A：特性标注（推荐，覆盖大多数 mod）

```csharp
using HNC.Sync;

[SyncType]                                   // 显式模式：只同步带 [Sync] 的成员
public class MyDaemon : BaseDaemon           // 任意类都行，不要求是 Daemon
{
    [Sync] public int Score;                              // 默认 World：服务端权威，广播给所有人
    [Sync(Mode = SyncMode.Manual)] public List<string> Log = new();   // 只在你调用 MarkDirty 时才检查
    [NoSync] public bool HoverState;                      // 显式排除

    public override void loadInit()
    {
        base.loadInit();
        HncSync.Register(this, "mymod:" + comp.idName);   // 见 §4：key 必须稳定
    }
}
```

零代码模式：如果你的字段本来就带 Pathfinder 的 `[XMLStorage]`（也就是"需要存档的字段"），可以直接把它们纳入同步：

```csharp
[SyncType(UseXmlStorage = true)]
public class MyDaemon : BaseDaemon
{
    [XMLStorage] public string Message;       // 自动同步，不需要 [Sync]
    [XMLStorage, NoSync] public string Cache; // 存档要，但不同步
}
```

规则（来自 `ReflectionTarget` 的实际实现）：

- 字段必须**非 readonly**；属性必须同时有 `get` 和 `set`、且不是索引器。否则被静默跳过。
- 沿继承链扫描，基类里的私有成员也会同步。子类同名成员优先。
- 成员类型没有序列化器时会被跳过，并在宿主日志里打 `[Sync] 跳过 类型.成员：类型 X 没有序列化器`。
- 线上按**成员名**寻址，不是按下标。两端成员集合略有出入（版本不一致）时，多出来的成员被忽略，不会错位。

### 方式 B：实现 `ISyncable`（自己控制序列化）

状态放不进字段、或你想手写增量时：

```csharp
public class MyThing : ISyncable
{
    public string SyncId => "mymod:thing:" + id;     // 稳定标识
    public SyncScope Scope => SyncScope.World;

    public void WriteState(ISyncWriter w) { w.WriteInt(Count); w.WriteString(Name); }   // 全量
    public void ReadState(ISyncReader r)  { Count = r.ReadInt(); Name = r.ReadString(); }

    public bool TryWriteDelta(ISyncWriter w)         // 无变化返回 false；每个 Tick 被调用一次
    {
        if (Count == _lastSent) return false;
        _lastSent = Count; w.WriteInt(Count); return true;
    }
    public void ReadDelta(ISyncReader r) { Count = r.ReadInt(); _lastSent = Count; }
}
// 然后 HncSync.Register(thing);  key 取自 SyncId
```

注意 `ReadDelta` 里要把你自己的"上次发送值"一起更新，否则收到的权威值会被下一轮 `TryWriteDelta` 当成本地改动回传。

### 方式 C：自由消息通道（一次性事件 / RPC）

```csharp
var ch = HncSync.Channel("mymod.vote");
ch.Received += (fromPlayerId, payload) => { /* 处理 */ };
ch.Send(payload, SyncScope.World);
```

不保留状态、不重放、不保证送达顺序以外的任何东西。需要"晚加入的玩家也能看到"的就不要用它。

## 4. 注册：时机与 key

```csharp
HncSync.Register(instance, key = null);   // key 为空时依次尝试 ISyncable.SyncId、已注册适配器的 TryGetKey
HncSync.Unregister(instance);
HncSync.MarkDirty(instance, "Log");       // 立刻检查并推送；member 为 null = 全部成员
```

**什么时候注册**：对象已经**完整构造并加载完毕**之后（比如 Daemon 的 `loadInit()`，或 `OSLoadedEvent` 之后）。
注册时当前值会被记为基线——初始世界由存档负责，HNC.Sync 只同步**之后**的变化。
在构造函数里注册会把"还没填好的值"当基线，之后的加载过程会被当成一大堆变化发出去。

**key 必须跨进程稳定**，也就是在服务端和所有客户端上是同一个字符串：

| 可以用 | 不能用 |
|---|---|
| 节点的 `idName`、Daemon 类型名 + 序号、你自己定的常量 | IP（节点 IP 会轮转）、`GetHashCode`、对象在列表里的下标（顺序可能不同）、`Daemon.name`（会被本地化） |

推荐格式：`{种类}:{拥有者}:{实例}`，如 `daemon:myNodeId:MyDaemon:0`。

两个对象用了同一个 key，后注册的会覆盖先注册的。

**线程**：`Register` / `MarkDirty` / 引擎的 `Tick` 都应在游戏主线程调用。引擎内部的"远端写入中"标记是线程静态的。

## 5. 防回环：`HncSync.ApplyingRemote`

远端数据正在写进你的对象时，`HncSync.ApplyingRemote == true`。如果你的字段有 setter / 钩子 / 事件会触发"再推送"或"再广播"，
请在这个标志为 `true` 时跳过，否则会形成回声：

```csharp
public int Score
{
    get => _score;
    set { _score = value; if (!HncSync.ApplyingRemote) HncSync.MarkDirty(this, nameof(Score)); }
}
```

只用轮询（默认 `SyncMode.Auto`）的话你不需要关心这个：引擎在应用远端值时会把比较基线一起更新，下一轮轮询不会回传。

## 6. 支持的类型

内置（`SerializerRegistry` 的实际实现）：`bool`、`byte`、`short`、`int`、`long`、`float`、`double`、`string`、`byte[]`、
任意 `enum`、**恰好是** `List<T>` 与 `Dictionary<K,V>`（元素类型也必须受支持；声明成 `IList<T>`/`IDictionary` 不行）。

**没有内置**：`XElement`、XNA 的 `Vector2`/`Color`/`Rectangle`、你自己的类和结构体。
注册自己的序列化器：

```csharp
public sealed class ColorSerializer : ISyncValueSerializer
{
    public bool CanHandle(Type t) => t == typeof(Color);
    public void Write(ISyncWriter w, object v, Type t) { var c = (Color)v; w.WriteInt(c.R); w.WriteInt(c.G); w.WriteInt(c.B); w.WriteInt(c.A); }
    public object Read(ISyncReader r, Type t) => new Color(r.ReadInt(), r.ReadInt(), r.ReadInt(), r.ReadInt());
}
HncSync.RegisterSerializer(new ColorSerializer());   // 后注册的优先，可以覆盖内置类型
```

**注意**：变化检测是"序列化成字节再比较"。如果你的序列化器对同一个值可能产生不同字节（比如遍历顺序不稳定），会被误判为一直在变化。

## 7. 范围（Scope）与权威

| `SyncScope` | 含义 | 现状 |
|---|---|---|
| `World`（默认） | 服务端权威，广播给所有人 | ✅ 可用 |
| `Player` | 每个玩家一份，只发给对应玩家 | ⚠ **未打通**：引擎只有"每对象一份"的存储。目前只有服务端的全量快照会发出，增量和客户端提议都被忽略。请先不要用 |
| `Local` | 不同步，仅用于显式声明 | ✅ 被跳过 |

第一期是**服务端权威 + 客户端提议**：

1. 客户端本地改了你的字段 → 引擎检测到差异 → 作为 `PROPOSE` 发给服务端。
2. 服务端**不在"远端写入"作用域内**应用它（让你的游戏逻辑正常响应这次写入）。
3. 服务端下一轮检测到变化 → 以 `DELTA` 广播给所有人（包括提议者）。

没有冲突合并、没有拒绝（`REJECT` 在 M3）：同一成员被两个人同时改，以服务端最终看到的为准。
**服务端不校验提议**——任何客户端都能提议修改任何已注册对象的任何 `World` 成员。需要权限控制的状态不要直接放进字段，用方式 C 的通道自己做校验。

## 8. 原版 Daemon 怎么同步（内置适配器）

原版 Daemon 的运行时状态几乎都存在**所属电脑文件系统的一个子文件夹**里
（`MailServer` → `mail`，`MessageBoardDaemon` → `ImageBoard`，`IRCDaemon` → `IRC`，`UploadServerDaemon` → 它的 `Foldername`）。
所以内置的 `DaemonAdapter`（`HNC.Sync.Adapters`）把"文件夹树"作为同步对象：

- **key**：`daemon:{电脑 idName}:{Daemon 类型名}:{同类型第几个}`。
- **粒度**：以文件 / 文件夹为单位做增量。两个玩家改不同的文件互不覆盖；加入/重连时发全量快照，并删掉本地多出来的条目。
- **原地修改**：Daemon 在 `loadInit` 里缓存了子文件夹和文件对象（`MailServer.accounts`、`IRCSystem.ActiveLogFile`……），
  所以适配器只改已有对象的内容，不替换对象。
- **排除**：`IRCDaemon` 的 `runtime` 子文件夹是延迟动作队列，同步它会让每个客户端都执行一遍，所以被排除。

| 原版 Daemon | 状态 | 说明 |
|---|---|---|
| `MailServer` | ✅ 已适配（**玩家自己的 jmail 除外**） | 同步 `mail` 文件夹（账户、收件箱、已发送）。**玩家自己的邮箱（`netMap.mailServer` / `jmail`）被跳过**：邮箱是每玩家一份，按 World 范围同步会让所有人的邮件互相可见。NPC 邮件服务器照常同步。玩家之间发邮件见 `README.PlayerMail.md` |
| `MessageBoardDaemon` | ✅ 已适配 | 同步 `ImageBoard`（`Threads` 下的帖子文件） |
| `IRCDaemon` | ✅ 已适配 | 同步 `IRC`（`active.log`、`users.cfg`），排除 `runtime`。`active.log` 是单个文件，每次新消息会重发整个文件 |
| `UploadServerDaemon` | ✅ 已适配 | 同步它的 `Foldername`（含 `Uploads`） |
| `DLCHubServer`（DHS）的聊天记录 | ✅ 已适配（仅 `HomeBase` 根目录） | 只同步聊天记录所在的 `HomeBase` 根目录（`active.log` 等），排除 `contracts` / `archive`（任务相关）和 `runtime`（延迟动作队列）。为 IRC/DHS 发消息功能（`README.IrcChat.md`）加的 |
| `MissionListingServer`、`MissionHubServer`、`DLCHubServer` 的**任务部分** | ⛔ 有意排除 | 内存里有活的 `ActiveMission` 对象列表，被任务完成逻辑直接引用；只同步文件夹会让列表和文件夹脱节。要和 Scope / 私有任务一起设计 |
| `DatabaseDaemon`、`WebServerDaemon`、`OnlineWebServerDaemon`、`Academic/Medical/DeathRow DatabaseDaemon`、`MarkovTextDaemon`、`SongChangerDaemon`、`LogoDaemon`、`CustomConnectDisplayDaemon`、`DLCCreditsDaemon` | ❔ 未评估，预计无需 | 按名字归类：内容在加载时从文件/XML 读入，运行时几乎只读。运行时改它们的文件由文件系统事件覆盖 |
| `PointClickerDaemon`、`HeartMonitorDaemon`、`AircraftDaemon`、`PorthackHeartDaemon`、`ISPDaemon`、`AddEmailDaemon`、`WhitelistConnectionDaemon`、`AuthenticatingDaemon` 子类 | ❔ 未评估 | 状态不在文件夹里（计数、动画进度、会话认证……），没有读过；需要时逐个分析 |

> 上表中 ✅ 和 ⛔ 的结论来自阅读 Hacknet 5.069 源码，**还没有在游戏里验证**；❔ 只是按类名的猜测。

**和现有 `EVENT:FILE` 文件事件的关系**：玩家操作产生的文件变化目前已经通过 `EVENT:FILE` 同步。
适配器接入后，同一次改动可能被两条路径各同步一次。结果是幂等的（最终一致），但会有重复流量；
去重留到 M1 联调时处理。

### 给你的"文件夹型" Daemon 接入（不用写适配器）

如果你的自定义 Daemon 也是把状态存在 `comp.files.root` 下的某个文件夹里，一行就能接入（需要引用 `HNC.Sync.Adapters`，它依赖 Hacknet）：

```csharp
DaemonSync.AddHandler(typeof(MyFolderDaemon), d => "MyDaemonData", "cache");  // 根文件夹名，后面是要排除的子文件夹
```

之后宿主调用 `DaemonSync.RegisterAll(os)` 时会自动注册它。
`AddHandler` 返回 `DaemonHandler`，可以给它的 `Skip` 赋一个 `Func<Daemon, bool>` 来排除某些具体实例（内置的 `MailServer` 就用它跳过玩家自己的 jmail）。

如果你的状态是字段（不在文件夹里），用方式 A，不要用这个。

## 9. 宿主（HNC 自己）做了什么——供你了解它什么时候生效

已接入（**未在游戏里验证**）：

| 环节 | 位置 | 行为 |
|---|---|---|
| 引擎与 `HncSync.Runtime` | `ClientSync.Init()`（插件 `Load` 时）/ `ServerSync`（Pipe 连上后的第一个 `Update`） | 创建 `SyncEngine` 并赋给 `HncSync.Runtime`。客户端在 `Load` 阶段就创建，所以你的 mod 在 `PostLoad` 里就能看到它 |
| 驱动 | 两端的 `Game1.Update` 补丁 | 每 ~100ms `Tick()`；每 ~3s 扫描一次世界，把原版 Daemon 注册进来 |
| 客户端 → 服务端 | `ClientNetworkManager.Send` → 终端 → Pipe（`SYNC_FROM:{playerId}:{原消息}`）→ `ServerSync` | 只转给服务端游戏，**不广播给其他客户端** |
| 服务端 → 客户端 | `ServerManager.SendToConsole` → 终端 `RouteSync` | 按消息头里的 playerId 单发，`*` 广播。注意不走 `SendEvent`（它会把 2 秒内同类消息折叠掉） |
| 玩家连入 | 终端 HELLO 握手后发 `SYNC_JOIN:{playerId}` → 服务端 `OnPlayerConnected` | 服务端发 HELLO + 全部已注册对象的全量快照 |

关键设计点：客户端引擎在世界加载**之前**就存在。服务端在客户端世界没加载完时发来的全量/增量，会被引擎按 key 暂存（每个 key 最多 64 条），
等对应对象注册（`Register`）时按顺序重放。所以 mod 不需要关心"连接早于加载"的时序。

**部署**：`HNC.Sync.Abstractions.dll`、`HNC.Sync.Core.dll`、`HNC.Sync.Adapters.dll` **不嵌入**插件，是 `Plugins` 目录里的独立 dll
（`FodyWeavers.xml` 里用 `ExcludeAssemblies` 排除）。原因：`HncSync.Runtime` 是静态字段，插件和第三方 mod 必须引用**同一个**程序集实例。
所以你的 mod 不要自带 `HNC.Sync.Abstractions.dll`（`<Private>false</Private>`），让它从同目录加载 HNC 提供的那份。

**为什么需要解析器**：BepInEx 解析依赖只搜 BepInEx 自己的目录和 `BepInEx/plugins`，**不搜** `Extensions/<名>/Plugins`。
实测：把 `HNC.Sync.*.dll` 放在扩展的 `Plugins` 里，`ServerPlugin` 加载时报 `Could not load file or assembly 'HNC.Sync.Abstractions'`。
所以 ClientPlugin / ServerPlugin 在静态构造函数里装了一个 `AssemblyResolve` 处理器（`HncAssemblyResolver`），
去自己所在目录找 `HNC.Sync.*.dll`，加载一次后缓存，之后任何 mod 来要都返回同一个实例。
你的 mod 不需要做任何事——只要 HNC.Sync.*.dll 和 ClientPlugin/ServerPlugin 在同一目录。
把三个 dll 放进全局的 `BepInEx/plugins` 同样可行（那里在 BepInEx 的搜索路径里），但**不要两处都放**。

还没做：
- 终端对 `SYNC:` 不按 scope 区分（只看消息头里的 playerId）：`Player` 范围的单播靠 playerId，World 靠 `*`，够用但没有别的过滤。
- 终端没有对单条超大消息分块。大型 MailServer 的全量快照会变成一条很长的消息，LiteNetLib 会自动分片，但没测过上限。
- 客户端 `PROPOSE` 没有速率限制。

## 10. 线格式（调试用）

```
SYNC:{OP}:{scope 0/1/2}:{playerId|*}:{b64 key}:{version}:{b64 payload}
OP = HELLO / FULL / DELTA / PROPOSE / REQ / MSG
```

- DELTA 带版本号；客户端发现版本缺口（收到的不是 当前+1）会发 `REQ`，服务端回全量。过期/重复的 DELTA 被丢弃。
- 目标还没注册时（加载顺序），收到的消息每个 key 暂存最多 64 条，等 `Register` 时重放。
- 文件夹同步的载荷是一串 `[kind, path, data]`：`0` 建文件夹 / `1` 写文件 / `2` 删文件夹 / `3` 删文件；路径用 `/` 分隔，名字里的 `/` 和 `%` 分别转义成 `%2F`、`%25`。

## 11. 已知限制

- 传输层已接入但**未在游戏里验证**；终端对超大单条消息没有分块，没有速率限制。
- `Player` 范围未打通。
- 无冲突检测与拒绝；服务端不校验客户端提议。
- 变化检测是每个 Tick 把所有已注册成员序列化后比较，成员很多或列表很长时会有分配开销。大列表请用 `SyncMode.Manual` + `MarkDirty`。
- 没有 `XElement` 等内置序列化器（早期设计稿里写了，代码里没有）。
- 文件夹同步里文件的**顺序**只在全量快照时保证；增量新增的文件追加在末尾。
- 版本号只存在内存里，没有持久化。
