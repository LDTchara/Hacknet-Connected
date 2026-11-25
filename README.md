# Hacknet-Connected（HNC）

让多个玩家在**同一个 Hacknet 扩展世界**里一起玩的多人联机层，基于 BepInEx / Pathfinder。

- **服务端**：一个完整的 Hacknet 游戏实例，是世界状态的权威，也是一名玩家（主机）。
- **终端**（HacknetServerTerminal）：一个控制台程序，负责转发消息、管理大厅和暂停、保存玩家存档。
- **客户端**：每个玩家各自运行自己的 Hacknet，世界由同一个种子生成，再由服务端同步变化。

扩展作者可以在扩展里给不同玩家分配角色、分开任务线（谁执行哪个动作 / 条件）、设计中途加入用的阶段预设。

> **状态（2026-11）**：编译通过；**玩家邮件**在游戏里测过两个方向，可用。
> 其余功能（准备大厅、全员暂停、阶段预设、Scope 门控、HNC.Sync 同步、IRC 发言、可移除标签）**都还没在游戏里实测**。
> 各专题文档里都写明了哪些已验证、哪些没有。

---

## 功能一览

| 功能 | 谁用 | 说明 |
|---|---|---|
| 世界同步 | 自动 | 种子与随机状态同步，各端生成同一个世界；玩家操作（文件、端口、防火墙、代理、flag 等）实时转发 |
| 角色与占位符 | 扩展作者 | `#P1NAME#` / `#P1_IP#` 等占位符，按玩家选的角色替换成实际玩家；每个角色有自己的电脑 |
| Scope 门控 | 扩展作者 | 在动作 / 条件 / 任务上加 `players`、`group`，让某段剧情只在指定玩家那里执行 |
| 支线角色选择 | 玩家 | `selectplayer` 命令，私有支线开始前分配角色 |
| 准备大厅 | 玩家 | 开局前等人、准备、移出、满员拒绝、聊天；服务端先进游戏，再放客户端进来 |
| 全员暂停 | 自动 / 主机 | 任何人掉线全员暂停，重连后原地继续；主机可手动暂停 / 强制继续 |
| 阶段预设 | 扩展作者 | 中途加入的玩家按自己的角色选一个阶段预设，等该阶段开始后进入 |
| 玩家邮件 | 玩家 | `sendmail` 给在线玩家（包括主机）发 jmail |
| IRC / DHS 发言 | 玩家 | IRC、DHS 界面里的输入栏和 `sendto` 命令，支持 @ 提及、附带节点和账户 |
| 可移除标签 | 扩展作者 | `RemoveableTag` + `<RemoveConditionsOrActions>`，卸载挂着的条件集和延时动作 |
| HNC.Sync | 其他 mod 作者 | 给自定义 Daemon / Module 等声明"要同步的字段"，不用自己写网络代码 |

---

## 一、玩家：部署

### 需要准备
- Hacknet，装好 Pathfinder（BepInEx）。
- **所有人都装同一个扩展、同一个版本。**
- 终端需要 **.NET 8 运行时**。
- 用 `build.bat` 编译（Windows，需要 .NET SDK）：

```bat
build.bat            :: 默认：每个插件合并成一个 dll（推荐部署用）
build.bat normal     :: 普通的多 dll 构建
```

编译后，`Output\Server` 和 `Output\Client` 下就是要部署的文件，目录结构和游戏里一样：

```
Output\Server\Plugins\          → 服务端游戏的 Extensions\<扩展>\Plugins\
Output\Server\BepInEx\plugins\  → 服务端游戏的 BepInEx\plugins\   （SeedSyncGlobal.dll）
Output\Server\Terminal\         → 终端程序，放哪都行
Output\Client\Plugins\          → 客户端游戏的 Extensions\<扩展>\Plugins\
Output\Client\BepInEx\plugins\  → 客户端游戏的 BepInEx\plugins\   （SeedSyncGlobal.dll）
```

> - **三端必须一起更新**（服务端插件、客户端插件、终端），新旧混用会连不上大厅。
> - 用单 dll 版本时，先删掉 Plugins 里旧的 `ServerClientEventsMod.dll` 和所有 `HNC.*.dll`，否则同一个插件会被加载两次。
> - `SeedSyncGlobal.dll` 要放在 `BepInEx\plugins`，**不要**放进扩展目录。它必须在游戏代码之前加载。

### 扩展目录里的配置文件
扩展作者通常会随扩展提供以下文件，玩家要确认它们存在：

| 文件 | 放在 | 内容 |
|---|---|---|
| `Players-Ser.xml`（或 `Players.xml`） | 服务端的扩展根目录 | 角色和占位符定义；`<PlayersCount num="N">` = **客户端**名额 |
| `Players-Cli.xml` | 客户端的扩展根目录 | 同上（客户端用） |
| `Config.tmp` | 每台机器的扩展根目录 | 第一行是**本机玩家的角色**（如 `p1`；服务端主机一般是 `p0`），后面是全体玩家的角色分配，见下文 |

### 开服（服务端）
1. 先打开终端 `HacknetServerTerminal.exe`，它会等待游戏连接。
2. 启动 Hacknet，进入扩展详情页，点 **[ Start Server ]**。
3. 回到终端按 **Y** 确认。
4. 第一次需要先建一个服务器配置：`create <名字>` → `set port 9050` → `set hostaccountname <主机账户>` → `set hostaccountpassword <密码>` → `save`。
5. `login <名字>` → `start`：游戏里弹出**准备大厅**，等客户端加入。
6. 所有人都点 **Ready** 后开局：满员直接开局；人数不足时主机会被问是否强制开始。
7. 游戏中：**F8** 全员暂停，**F9** 强制继续。终端也可以用 `pause`、`resume`、`lobby`、`status`、`help`。

> **端口**：终端目前**固定监听 UDP 9050**。配置里的 `Port` 只用来检查端口是否被占用，客户端要连 9050。
> 跨网络联机时，服务端要放行或转发 **UDP 9050**。

终端在**工作目录**下的 `Storage\` 里保存服务器配置（`Storages.ini`）、玩家名单和每个玩家的存档。日志写在工作目录的 `net_log.txt` 和桌面的 `TerminalLog_日期.txt`。

### 加入（客户端）
1. 启动 Hacknet，进入**同一个扩展**的详情页，点 **[ Connect to Server ]**。
2. **Add Server**：填名字、服务器 IP、端口 `9050`。
3. 选中服务器 → **Add Account**：填显示名和密码。这是你在这个服务器上的身份，服务端按这个名字保存你的存档。
4. 点账户的 **Login**，进入准备大厅，点 **Ready** 等待开局。开局后会自动进入游戏。
5. 游戏开始后才加入的人：大厅里直接点 **Start Game**。如果扩展提供了阶段预设，也可以选一个阶段进入。

客户端的服务器列表和账户保存在游戏目录下的 `OnlineSaves\ClientProfiles.ini`。

### 常见问题
- **一直停在 "Connecting to the lobby"**：终端没开或者是旧版本，或者 9050 端口不通。
- **"The server has not opened the lobby yet"**：终端还没 `start`，开放后会自动加入。
- **同一台电脑上同时跑服务端和客户端测试**：两个游戏共用同一个存档目录（`我的文档\My Games\HacknetPathfinder\Accounts\…`），存档可能互相覆盖。存档或世界看起来不对时，先怀疑这一点。

---

## 二、扩展作者：怎么用

### 1. 角色与占位符
一局里有几个**角色**（p1、p2、p3…），每个玩家扮演其中一个。在扩展的 XML 里，写**角色占位符**来代替原版的 `#PLAYERNAME#` / `#PLAYER_IP#`：

```xml
<!-- Players-Cli.xml / Players-Ser.xml -->
<PlayersConfig>
  <PlayersCount num="2" />                  <!-- 客户端名额（准备大厅按它判断满员） -->
  <PlayerToReplacements>
    <Globle>
      <PlayerReplacementsGroup name="p1" playerid="1">
        <PTR Org="#PLAYERNAME#" Curr="#P1NAME#" />
        <PTR Org="#PLAYER_IP#"  Curr="#P1_IP#" />
      </PlayerReplacementsGroup>
      <PlayerReplacementsGroup name="p2" playerid="2">
        <PTR Org="#PLAYERNAME#" Curr="#P2NAME#" />
        <PTR Org="#PLAYER_IP#"  Curr="#P2_IP#" />
      </PlayerReplacementsGroup>
    </Globle>
    <Private> … 支线任务的局部角色，见 README.ScopeSystem.md … </Private>
  </PlayerToReplacements>
  <PlayerNodes>                             <!-- 每个角色自己的电脑 -->
    <PlayerNode id="playerCompP1" players="p1" />
    <PlayerNode id="playerCompP2" players="p2" />
  </PlayerNodes>
</PlayersConfig>
```

```
Config.tmp（每台机器的扩展根目录）
p1                ← 第一行：本机玩家的角色
@G {
  Alice:p1;       ← 全体玩家的角色分配：玩家名:角色
  Bob:p2;
}
```

- 在节点、邮件、文件内容里写 `#P2NAME#`，加载时会变成"扮演 p2 的那个玩家的名字"。
- `#P1_IP#` 在扮演 p1 的玩家那里会变成他自己电脑的 IP。
- `PlayerNodes` 指定的电脑会成为对应角色玩家的"我的电脑"，各端的 IP 和位置一致。
- 例子：[`Examples/Nodes/ChatHub.xml`](Examples/Nodes/ChatHub.xml)，一个所有玩家都在里面的 IRC + DHS 节点。

### 2. 谁执行哪段剧情：Scope
在动作、条件、任务目标、`missionStart` / `missionEnd` / `nextMission` 上加属性：

```xml
<HasFlags flag="got_key" group="heist" players="1" />   <!-- 只在 heist 支线里 1 号角色的玩家那里检查 -->
<RunFunction FunctionName="..." players="PUBLIC" />      <!-- 所有人都执行（不写属性也一样） -->
```

私有支线开始前，玩家用 `selectplayer list` / `selectplayer <序号>` 分配支线角色。详见 [README.ScopeSystem.md](README.ScopeSystem.md)。

### 3. 中途加入：阶段预设
把流程切成 A → B → C… 多个阶段，每个阶段按角色准备一份预设（起始任务、起始动作、flag）。游戏开始后才加入的新玩家可以选一个阶段，等它开始后进入。详见 [README.Phases.md](README.Phases.md)。

### 4. 其他可用的东西
| 功能 | 文档 |
|---|---|
| 卸载挂着的条件集 / 延时动作（`RemoveableTag`） | [README.RemovableTags.md](README.RemovableTags.md) |
| IRC / DHS 输入栏、`sendto`、@ 提及 | [README.IrcChat.md](README.IrcChat.md) |
| 玩家邮件 `sendmail` | [README.PlayerMail.md](README.PlayerMail.md) |
| 准备大厅、全员暂停（作者需要知道：`PlayersCount` = 客户端名额） | [README.Lobby.md](README.Lobby.md) |
| 给其他 mod 适配同步（HNC.Sync） | [GUIDE.SyncForModAuthors.md](GUIDE.SyncForModAuthors.md) |

### 写多人扩展时的注意事项
- **服务端是权威**：一次性的世界事件（删节点、改共享文件）最好由服务端触发，或者用 Scope 限定只在一个玩家那里执行，否则每个客户端都会执行一遍。
- **只想给一个人的东西**（邮件、任务、程序）一定要加 Scope 或用角色占位符。
- **开局人数**：人数不足时主机可以强制开始，但扩展最好能应付角色空缺，或者在说明里写清楚必须满员。

---

## 三、开发者：原理

### 进程与通信

```
 客户端游戏 ──┐
 客户端游戏 ──┼── LiteNetLib（UDP 9050，文本消息）── 终端 ── 命名管道 HacknetServerPipe（逐行文本）── 服务端游戏
 客户端游戏 ──┘                                       │
                                                      └─ Storage\：服务器配置、玩家名单、每个玩家的存档
```

- **服务端游戏**（ServerPlugin）：世界的权威。处理客户端转来的事件，判定阶段，发种子。
- **终端**：消息路由，按 playerId 定向或广播。它还持有**大厅**（LobbyManager）和**全员暂停**（PauseManager）的权威状态，并保存玩家存档。玩家身份由终端按连接盖章，客户端无法冒充别人。
- **客户端游戏**（ClientPlugin）：应用服务端的事件和同步，转发本机玩家的操作。

### 世界为什么一致
- **SeedSyncGlobal**（全局插件，最早加载）：在 `Utils.random` 初始化时就换成确定性种子，并维护 IP 计数器。
- **WorldSeedSync**：服务端开局时生成主种子（或用固定种子），把 `Utils.random` 的完整内部状态、子种子计数器、IP 计数器发给客户端（`WORLD_SEED`）。客户端开局前恢复这些状态，再加上其他插件里静态 `Random` 字段的子种子，生成同一个世界。
- **玩家节点**：IP 和位置由"主种子 + 节点 id"派生，不依赖随机数流。

### 运行时同步
- **玩家操作事件**：ServerClientEventsMod 的检测器（文件、端口、防火墙、代理、flag、任务、连接…）产生 `EVENT:…`，经终端广播；EventReceiver 应用到本地。应用时用 `Suppress` 标记防止事件被再次转发形成回环。
- **状态同步**（HNC.Sync，`SYNC:…`）：引擎检测已注册对象的字段变化，发增量或全量。原版 Daemon（邮件服务器、留言板、IRC、上传服务器）按文件夹树同步。玩家加入时（`SYNC_JOIN`）服务端发全量。
- **存档**：终端按玩家名保存存档。客户端每次存档时自动上传（30 秒冷却）。加入大厅时，终端告诉客户端有没有它的存档（`HAS_SAVE` + 分块传输，或 `NO_SAVE`）。

### 准备大厅
`start` 只开放大厅，不开局。客户端连接（`HELLO`）时只做临时登记并请求种子。点 Login 加入大厅（`LOBBY_JOIN`）时，终端才用真实账户名确定 PlayerId、处理存档、加入玩家列表。

全员准备后，终端发 `LAUNCH_SERVER`。服务端游戏加载完、种子发出后再等 1.5 秒，回 `LOBBY_GAME_READY`；终端随后给客户端发 `LOBBY_START`。所以客户端一定先拿到种子，再开局。

### 全员暂停
闸门取自 Freezer：`Game1.Update`、`Game1.Draw`、`TextInputHook.OnTextInput` 各有一个最先执行的前置补丁，暂停时跳过原方法。改成了按原因暂停，并且只在 `OS.isLoaded` 后生效。

关键在于 Harmony 的规则：**前置补丁跳过原方法时，后置补丁照样执行**。HNC 的网络轮询、管道、心跳都挂在 `Game1.Update` 的后置补丁上，所以暂停期间网络照常工作。

客户端每 2 秒 `PING`，终端回 `PONG`。终端发现游戏中的客户端 6 秒没心跳，就广播 `HNC_PAUSE:1`。掉线的客户端自己冻结、本地存档，重连后用 `LOBBY_JOIN:…:resume` 原地继续。

### 阶段预设
服务端每秒按自己的 flag 判定 `HNC_Phases.xml` 里的阶段，变化时报告 `HNC_PHASES`，终端转给大厅成员。新玩家选好阶段后，开新游戏前把预设的起始任务 / 起始动作写进 `ExtensionInfo`（做法同 StuxnetHN 的 Gamemode），进入游戏后再加 flag。

### Scope 门控
反序列化时读出 `players` / `group`，存进 `ScopeManager`（`ConditionalWeakTable` 挂在对象上）。Harmony 自动枚举原版所有 `SA*` / `SC*` / `MisisonGoal` 子类的 `Trigger` / `Check` / `isComplete` 打前置补丁，不属于本机玩家就跳过。Pathfinder 和第三方程序集里的子类，在 `PostLoad` 时再补。

### 消息前缀速查
| 前缀 | 方向 | 用途 |
|---|---|---|
| `HELLO` / `WELCOME` / `PLAYER_DATA` / `PLAYER_LIST` | 客户端 ↔ 终端 | 连接与身份 |
| `WORLD_SEED` / `REQUEST_SEED` / `GET_WORLD_SEED` | 三方 | 世界种子 |
| `EVENT:…` | 三方 | 玩家操作事件 |
| `SYNC:…` / `SYNC_FROM` / `SYNC_JOIN` | 三方 | HNC.Sync 状态同步 |
| `HAS_SAVE` / `NO_SAVE` / `S_FILE…` / `SAVE_DATA_CHUNK…` / `SAVE_REQUIRED` | 客户端 ↔ 终端 | 存档下发 / 上传 |
| `LOBBY_…` | 三方 | 准备大厅、聊天 |
| `PING` / `PONG` / `HNC_PAUSE…` | 三方 | 心跳与全员暂停 |
| `HNC_PHASES` | 服务端 → 终端 → 客户端 | 阶段进度 |
| `MAIL…` / `PLAYERS` | 三方 | 玩家邮件 |
| `LAUNCH_SERVER` / `CREATE_ACCOUNT` / `GET_PLAYERS_COUNT` / `SYNC_FILES` … | 终端 → 服务端 | 终端命令 |

### 项目结构
| 项目 | 内容 |
|---|---|
| `ServerPlugin/` | 服务端游戏插件：`Network/`（管道、事件转发、同步）、`Lobby/`（大厅界面、暂停）、`Phases/`、`Scope/`、`Seed/`、`Patches/`、`Mail/`… |
| `ClientPlugin/` | 客户端游戏插件：`Network/`、`Lobby/`（大厅界面、掉线重连）、`Phases/`、`Gui/Listing/`（服务器 / 账户列表）、`Scope/`、`Missions/`、`Players/`、`Seed/`… |
| `HacknetServerTerminal/` | 终端（net8）：`Network/`、`Lobby/`（LobbyManager、PauseManager）、`Commands/`、`Players/`、`Persistence/` |
| `ServerClientEventsMod/` | 玩家事件检测与事件总线（两个插件共用）、运行时替换 |
| `SeedSyncGlobal/` | 全局种子插件 |
| `HNC.Sync.*` | 同步层：`Abstractions`（给 mod 作者引用）、`Core`（引擎、协议）、`Adapters`（原版 Daemon） |
| `HNC.Chat.Core/` | IRC / DHS 发言的纯逻辑 |
| `Shared/` | 两个插件各编译一份的源码：`IrcChat/`、`RemovableTags/`、`Lobby/`、`Pause/`、`Phases/`（终端只编译其中的协议文件） |

### 构建细节
- 插件是 net472。`build.bat` 默认用单 dll 模式（`-p:SingleDll=true`），把 ServerClientEventsMod、HNC.Sync.*、HNC.Chat.Core 的源码直接编进插件。
- 普通模式下，`HNC.Sync.*` 不嵌入（Costura `ExcludeAssemblies`），保持为独立 dll，保证全进程只有一个 `HncSync.Runtime`。`HncAssemblyResolver` 负责从扩展的 Plugins 目录找到它们，因为 BepInEx 不会去扩展目录里找依赖。
- 游戏程序集引用在各项目的 `libs\` 里。

### 文档索引
[README.Lobby.md](README.Lobby.md) · [README.Phases.md](README.Phases.md) · [README.ScopeSystem.md](README.ScopeSystem.md) · [README.IrcChat.md](README.IrcChat.md) · [README.PlayerMail.md](README.PlayerMail.md) · [README.RemovableTags.md](README.RemovableTags.md) · [GUIDE.SyncForModAuthors.md](GUIDE.SyncForModAuthors.md) · [DESIGN.SyncLayer.md](DESIGN.SyncLayer.md) · [TODO.ScopeAdaptation.md](TODO.ScopeAdaptation.md)

### 已知问题（摘要）
- 大部分功能未经游戏内实测，见上方"状态"。
- 终端端口固定 9050，配置里的 `Port` 没有真正生效。
- 暂停期间到期的延时动作（`ActionDelayer` 按真实时间）会在恢复后一起执行。
- 玩家整个游戏进程重启后重新登录，终端那份存档可能比本地旧，会覆盖本地（存档版本戳方案未做）。
- Scope 还有若干缺口（Faction rank 同步、Sequencer、HackScript 内容等），见 [TODO.ScopeAdaptation.md](TODO.ScopeAdaptation.md)。
