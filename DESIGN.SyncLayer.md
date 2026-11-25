# HNC 同步中间层（Sync Layer）设计草案

> 状态：**M0 已编译并通过 55 个单元测试**；**M2 原版 Daemon 适配器已编译，文件夹同步核心有单元测试（66 个通过）**；**M1 传输层已接入 ClientPlugin / ServerPlugin / 终端（已写，未编译、未在游戏里验证）**。
> 面向 mod 作者的使用说明见 `GUIDE.SyncForModAuthors.md`。
> 目标是先定中间层，再把原版 Daemon 作为第一个内置适配器接入。
> 日期：2026-11-23

## 1. 目标与非目标

**目标**
- 给其他 mod 作者一个**公开、稳定、小体积**的 API：声明"这个对象的这些字段要同步"，不用自己碰网络、序列化、广播、重连。
- 典型用户：自定义 Daemon、自定义 Module（含自定义字段）、带状态的 Executable/Faction 等。
- 原版 Daemon 同步**不写特例**，而是作为中间层的第一个内置适配器。

**非目标**
- 不改 `ServerClientEventsMod` 的事件集（AC 已定：保持现状）。
- 不做无头服务器、不做完整的 first-write-wins 冲突系统（见 §8，分期）。
- 不同步"临时层"（终端历史、Exe 实例、Trace 倒计时等，见 `ReadMe/05-状态模型.md`）。

## 2. 现状里已经存在、可以复用的东西

| 现有 | 复用方式 |
|---|---|
| 主机模式：服务端是完整游戏实例，是状态权威 | 中间层的"权威"= 服务端游戏进程 |
| 文本传输 `EVENT:TYPE:p1:p2`，服务端对 `EVENT:` 自动广播（除发送者） | 新增并列的 `SYNC:` 消息族，不污染 `EVENT:` |
| 世界层 / 玩家层 / 临时层 三层模型 | 直接对应 `SyncScope.World / Player / Local` |
| `Scope`（`players`/`group`）已能表示"谁执行" | 玩家层路由复用同一套身份（`SelectPlayerConfig` / `MyPrivateIdentities`） |
| PF 的 `[XMLStorage]` 已标出"哪些字段需要存档" | **默认同步集 = 存档字段**，作者零代码即可同步 |
| `EventForwarder.Suppress` 防自反循环 | 提升为中间层内置的 `ApplyingRemote` 作用域，不再各处手写 |
| `MailServerAddMailPatch` 已经手写过一个 Daemon 同步点 | 变成 `DaemonAdapter` 的一个钩子样例 |

**必须避免重复的坑**：Client/Server 两个插件里有大量逐字复制的文件。中间层核心必须是**单一共享程序集**，不得再复制。

## 3. 项目与程序集划分

```
HNC.Sync.Abstractions   ← 公开。只有 attribute / interface / enum，无依赖，无 Harmony
HNC.Sync.Core           ← 注册表、字段扫描、差异检测、序列化、协议编解码（双端共用）
HNC.Sync.Adapters       ← 内置适配器：Daemon（第二阶段）、Module、Computer 字段…
ClientPlugin            ← 实现 ISyncTransport（LiteNetLib）
ServerPlugin            ← 实现 ISyncTransport（Pipe → HacknetServerTerminal）
```

- 其他 mod **只引用 Abstractions**。运行时未安装 HNC 时，attribute 是惰性的，mod 照常单机运行。
- 用 `[BepInDependency("...", BepInDependency.DependencyFlags.SoftDependency)]` 软依赖。
- `Core` 通过 `ISyncTransport` 与具体网络解耦，Client/Server 只各写一个 transport。

## 4. 公开 API（Abstractions）

### 4.1 作用域

```csharp
public enum SyncScope
{
    World,   // 服务端权威，广播给所有人（节点上的 Daemon、Flag）
    Player,  // 每个玩家一份，只发给对应玩家
    Local,   // 不同步（占位，便于显式声明"我知道这是本地的"）
}
```

### 4.2 方式 A：特性标注（最低成本，覆盖 90% mod）

```csharp
public class MyDaemon : Daemon   // 或 PF 的 BaseDaemon
{
    [Sync]                          public int Score;            // 默认 World
    [Sync(Scope = SyncScope.Player)] public string Notes;        // 每个玩家一份
    [Sync(Mode = SyncMode.Manual)] public List<string> Log;    // 变化时推送
    [NoSync]                        public bool _uiHover;        // 显式排除
}
```

规则：
- 类上有 `[SyncType]` → 只同步标 `[Sync]` 的字段（显式模式）。
- 类上有 `[SyncType(UseXmlStorage = true)]` → 额外把 PF `[XMLStorage]` 字段纳入（零代码模式）。
- 字段类型走 `ISyncValueSerializer<T>` 注册表，内置：原始类型、string、enum、`List<T>`、`Dictionary<K,V>`、`XElement`。

### 4.3 方式 B：接口（复杂状态、自己控制序列化）

```csharp
public interface ISyncable
{
    string SyncId { get; }                    // 稳定标识，见 §5
    SyncScope Scope { get; }
    void WriteState(ISyncWriter w);           // 全量
    void ReadState(ISyncReader r);
    // 可选：增量
    bool TryWriteDelta(ISyncWriter w);        // 无变化返回 false
    void ReadDelta(ISyncReader r);
}
```

这就是 `ReadMe/03-模组兼容.md` 里留空的 `ISyncableState`（TODO #12）。

### 4.4 方式 C：自由消息通道（状态放不进字段时）

```csharp
HncSync.Channel("mymod.vote").Send(payload, SyncScope.World);
HncSync.Channel("mymod.vote").OnReceived += (sender, payload) => { ... };
```

用于一次性事件/RPC，不保留状态、不重放。

### 4.5 外观类

```csharp
public static class HncSync
{
    public static bool IsActive { get; }           // 联机模式且中间层已就绪
    public static bool IsServer { get; }
    public static string LocalPlayerId { get; }

    public static void Register(object instance);              // 显式注册实例
    public static void Unregister(object instance);
    public static void MarkDirty(object instance, string field = null); // 立即推送
    public static void RegisterAdapter(ISyncAdapter adapter);  // 给第三方类型提供定位/构造
    public static void RegisterSerializer<T>(ISyncValueSerializer<T> s);
    public static bool ApplyingRemote { get; }     // 远端数据正在写入，避免回环
}
```

## 5. 标识：`SyncKey`

状态必须有跨进程稳定的名字。不能用对象引用或枚举下标（节点 IP 每秒轮转，`ReadMe` 已记录）。

```
{kind}:{owner}:{instance}:{field}

daemon:{computerIdName}:{daemonName|index}:{field}
module:{playerId}:{moduleName}:{field}
```

- `computerIdName` 用节点 `idName`（稳定），不用 IP。
- `Player` 作用域的 key 追加 `@{playerId}` 用于服务端路由。
- 找不到目标对象 → 缓存到"待应用队列"，目标对象被注册时补发（处理加载顺序问题）。

## 6. 适配器：把"怎么找到/怎么读写"和"同步逻辑"分开

```csharp
public interface ISyncAdapter
{
    string Kind { get; }                                   // "daemon" / "module"
    IEnumerable<ISyncTarget> Enumerate();                  // 全量同步、重连用
    ISyncTarget Locate(string owner, string instance);     // 按 key 找对象
}
public interface ISyncTarget
{
    string Key { get; }
    SyncScope Scope { get; }
    byte[] Snapshot();                                     // 全量
    void Apply(byte[] data, bool isDelta);                 // 在 ApplyingRemote 作用域内调用
    IEnumerable<FieldChange> DetectChanges();              // 差异检测
}
```

中间层核心只认 `ISyncAdapter`。mod 作者用特性/接口时，`Core` 内置一个"反射适配器"；原版 Daemon 用 Daemon 适配器。

## 7. 变化检测：三条路，同时支持

| 方式 | 触发 | 适用 |
|---|---|---|
| 轮询差异 | 每 N 帧（建议 ~10Hz）对已注册对象的 `[Sync]` 字段做值比较/哈希 | 默认；作者什么都不用写 |
| `MarkDirty` | 作者主动调用，立即推送 | 高频或要求即时的字段 |
| Harmony 钩子 | 适配器在已知变更方法上打补丁 | 原版 Daemon（如 `MailServer.addMail`） |

轮询只比较**已注册**对象，开销与"声明的同步字段数"成正比，不遍历整个 NetworkMap。

## 8. 协议（新增 `SYNC:` 消息族）

沿用现有文本管道，但载荷一律 base64，**不再用冒号拼业务字段**（`EVENT:FILE` 已经因为路径里的冒号需要 base64 绕开）。

```
统一格式（已实现，见 HNC.Sync.Core/Protocol/SyncProtocol.cs）：
SYNC:{OP}:{scope 0/1/2}:{playerId|*}:{b64 key}:{ver}:{b64 payload}

OP：HELLO 握手 / FULL 全量 / DELTA 增量 / PROPOSE 客户端提议 / REQ 请求全量 / MSG 自由通道（key=通道名）
头部三段足够终端路由，无需解析 payload；key 与 payload 一律 base64（key 里有冒号）。
REJECT 留到第二期（M3）。
```

**路由**（服务端终端）：现在对 `EVENT:` 是"广播除发送者"。`SYNC:` 需要按 scope 路由：World 广播，Player 只发目标玩家。这是终端侧**唯一必须新增**的代码，README 里"零服务端改动"对 `SYNC:` 不成立。

**权威与冲突（分期）**
- **第一期**：服务端权威、客户端只读。客户端的改动走 `PROPOSE`，由服务端游戏实例在 `ApplyingRemote` 之外正常执行，产生的变化再经轮询/钩子广播。不做冲突合并，服务端怎么算就怎么是。
- **第二期**：每个 key 带版本号，`PROPOSE` 带 `baseVer`，不匹配返回 `REJECT`（即 `ReadMe/02-同步机制.md` 的 first-write-wins）。

**入网/重连**：存档文件传输仍负责"初始世界"。`SYNC:FULL` 补的是存档里没有、或存档后又变化的运行时状态；第二期再用版本号做增量重连。

## 9. 防回环（一次性在中间层解决）

```
Apply(remote data):
    ApplyingRemote = true      // 线程静态
    try { target.Apply(...) } finally { ApplyingRemote = false }
变化检测 / Harmony 钩子：ApplyingRemote 为 true 时不产生推送
```

同时更新轮询的"上次值"快照，避免远端写入被下一轮轮询当成本地变化再发回去。

## 10. 原版 Daemon 接入（第二阶段，建立在上面之上）

`DaemonAdapter : ISyncAdapter`

- **Locate**：`computer.idName` + daemon 名称（`daemon.name`）/ 同类序号。
- **Scope 默认**：非玩家节点上的 Daemon → World；玩家电脑上的 → Player。
- **Snapshot / Apply（保底方案）**：复用 Daemon 自己的存档序列化（`<daemons>` 节点下对应元素）。覆盖所有原版和 `[XMLStorage]` 的 PF Daemon，**不用逐个类写字段**。
- **增量（按热度逐个加）**：对高频变化的 Daemon 在 mutator 上打钩子，产出 `DELTA`。第一批候选：
  - `MailServer`（已有 `MailServerAddMailPatch`，改成适配器钩子）
  - `MissionListingServer`（任务板增删）
  - `WebServer` / `OnlineWebServer`（页面内容，低频，快照足够）
  - IRC / 公告类 Daemon（追加消息，适合 append-delta）
- **Module**：`Module` 是 `internal`，且多数是 UI 状态 → 默认 `Local`。只对**自定义 Module 的数据字段**走 `[Sync]`；位置/可见性不同步（`ModuleDetector` 的事件保持原样）。

## 11. 对现有代码的影响

| 位置 | 变化 |
|---|---|
| `ServerPlugin` / `ClientPlugin` | 各加一个 `ISyncTransport` 实现；`EventReceiver` 增加 `SYNC:` 分发 |
| `HacknetServerTerminal/NetworkManager` | 新增 `SYNC:` 的按 scope 路由 |
| `ServerPlugin/Patches/MailServerAddMailPatch.cs` | 迁入 `DaemonAdapter` 钩子 |
| 重复文件（`SaveFileHelper` 等） | 不在本次范围，但 `Core` 必须单份 |
| `ServerClientEventsMod` | 不改 |

## 11.5 M0 已实现内容与已知限制

已写（`HNC.Sync.Abstractions`、`HNC.Sync.Core`，netstandard2.0，不依赖游戏/Harmony）：特性、`ISyncable`、适配器/传输接口、`HncSync` 外观（无运行时则空操作）、二进制序列化 + `List`/`Dictionary`/enum、`ReflectionTarget`（含 `[XMLStorage]` 按类型名识别）、协议编解码、`SyncEngine`（轮询、DELTA 版本缺口检测 → REQ 全量、PROPOSE、待应用队列、通道）。

**限制**
- **未编译、未运行**：当前环境没有 dotnet。
- **Player 范围未打通**：服务端对单个对象只有一份字段值，没有“每玩家一份”的存储；目前 Player 范围的 DELTA/PROPOSE 都被忽略，仅 FULL 快照会发出。需要在第二期决定存储方式。
- 没有 transport 实现、终端路由、`Tick` 调用点，也没有任何适配器。
- 无单元测试。

## 11.6 M2 实现记录：原版 Daemon 适配（2026-11-23）

**结论先行**：§10 里设想的"复用 Daemon 自己的存档序列化（`getSaveString` / `<daemons>` 节点）做快照"**不可行，已放弃**。
原因（读 Hacknet 5.069 源码确认）：

- 原版只有 `Computer.load()` 里一个巨大的 if-else 能**新建**一个 Daemon；没有"把一段 XML 应用到已存在的 Daemon 实例"的入口。
- `getSaveString()` 只写 Daemon 的**配置**（名字、颜色、文件夹名），不含运行时状态。
- 运行时状态实际在电脑文件系统里：`MailServer`→`mail`、`MessageBoardDaemon`→`ImageBoard`、`IRCDaemon`→`IRC`、`UploadServerDaemon`→`Foldername`。
  这些 Daemon 在 `loadInit()` 里把子 `Folder` / `FileEntry` 缓存成字段，所以同步必须"原地改对象"。

**实际做法**：把"文件夹树"作为同步对象。
- `HNC.Sync.Core/Targets/FolderSync.cs`：`IFolderModel`（树的抽象）+ `FolderSyncTarget`（基线差异、文件/文件夹粒度增量、全量合并、文件顺序）。**不依赖游戏**，在 `HNC.Sync.Tests/FolderSyncTests.cs` 里用内存树测试。
- `HNC.Sync.Adapters`：`HacknetFolderModel`（把 `Folder`/`FileEntry` 适配成 `IFolderModel`，所有修改原地进行）、`DaemonAdapter`、`DaemonSync`（`AddHandler` / `Install` / `RegisterAll`）。
- 已适配：`MailServer`、`MessageBoardDaemon`、`IRCDaemon`（排除 `runtime`）、`UploadServerDaemon`。
- 有意排除：`MissionListingServer` / `MissionHubServer` / `DLCHubServer`（持有活的 `ActiveMission` 列表，需与任务 / Scope 一起设计）。

**与 §10 的其他差异**
- 没有给 `MailServer` 单独写 `DELTA` 钩子：文件级增量已经足够，`MailServerAddMailPatch`（修复 NRE 的补丁）保持原样，不迁移。
- Scope 默认一律 `World`；"玩家电脑上的 Daemon 用 Player 范围"依赖 Player 范围打通，推迟。

**未验证**：适配器项目依赖游戏程序集，当前环境无法编译；`searchForFolder`、`FileEntry.size` 等成员名按源码核对过，仍需编译确认。

## 11.7 M1 实现记录：传输层（2026-11-23）

消息流（新增 `SYNC:` 消息族，不碰 `EVENT:`）：

```
客户端引擎 ──LiteNetLib──▶ 终端 ──Pipe: SYNC_FROM:{playerId}:{原消息}──▶ ServerSync ──▶ 服务端引擎
                                                                                │
客户端引擎 ◀──LiteNetLib── 终端.RouteSync ◀──Pipe: SYNC:...（SendToConsole）──────┘
```

- **终端**（`HacknetServerTerminal`）：`NetworkManager.RouteSync` 按头部 `{playerId|*}` 定向/广播；客户端发来的 `SYNC:` 只转给游戏进程（带发送者 id），不广播给其他客户端；HELLO 握手后发 `SYNC_JOIN:{playerId}`；`Log` 对超过 300 字符的消息截断（存档分块、全量快照）。
- **ServerPlugin**：`ServerSync`（传输 + 引擎 + Tick + 扫描 Daemon）。`SYNC_FROM:` / `SYNC_JOIN:` 在 `ServerManager.ProcessPendingMessages` 里**先于**按 `:` 拆分处理。发送必须用 `SendToConsole`，不能用 `SendEvent`（后者对同类消息 2 秒内折叠，会吞掉连续的同步消息）。
- **ClientPlugin**：`ClientSync`（传输 + 引擎 + Tick + 扫描）。引擎在 `Load` 时创建；`EventReceiver` 对 `SYNC:` 先分发再打印。
- **程序集部署**：`HNC.Sync.*` 在两个插件里用 `Costura ExcludeAssemblies` 排除，保持为独立 dll，保证 `HncSync.Runtime`（静态）只有一份。**这是对 Costura 属性名和 BepInEx 依赖解析的假设，需要实测。**
- 修了一个会在引擎重启后让适配器失效的问题：`DaemonSync.Install` 现在按 Runtime 实例判断，换实例就重新安装并清空已注册记录。

**已知风险（未验证）**
- 与 `EVENT:FILE` 并存导致同一改动同步两次：结果幂等，但有重复流量。
- 单条超大全量快照没有分块；终端 `Log` 已截断，但 LiteNetLib / Pipe 的实际上限没测。
- 注册时机靠"世界里已有 `netMap` 之后每 3 秒扫描一次"，没有用 `OSLoadedEvent`；如果世界加载跨多帧，可能在半加载状态下记基线。

## 12. 分阶段计划

1. **M0 骨架**：Abstractions + Core（注册表、`[Sync]` 反射适配器、值序列化、协议编解码、`ApplyingRemote`），单元可测，无网络。
2. **M1 传输**：两端 `ISyncTransport`、终端 scope 路由、`HELLO` 握手；用一个示例 mod（自带 `[Sync]` 字段的最小 Daemon）端到端验证。（传输已写；端到端验证未做。）
3. **M2 原版 Daemon**：`DaemonAdapter` 快照模式 + `MailServer` 增量。
4. **M3 版本与重连**：key 版本号、`PROPOSE/REJECT`、增量重连。
5. **M4 其余适配器**：Module 数据字段、Computer 字段、Faction。

## 13. 待你拍板的决定

1. **程序集归属**：新建 `HNC.Sync.*`（推荐，边界干净，且 Client/Server 共用一份），还是并入 `ServerClientEventsMod`？
2. **客户端写入权限（第一期）**：只读 + `PROPOSE`（推荐），还是允许客户端直接覆盖服务端状态？
3. **载荷格式**：沿用文本 + base64（推荐，改动最小），还是此时就改成二进制帧？
4. **Player 作用域的身份**：用 `playerId`（服务端分配、稳定）还是 `SelectPlayerConfig`（角色名 p1/p2）？推荐 `playerId` 做路由、角色名只做 Scope 门控，两者在握手时建立映射。
5. **原版 Daemon 快照用存档序列化**是否可接受？代价是体积偏大（如大 MailServer），收益是一次覆盖全部原版和 PF Daemon。
