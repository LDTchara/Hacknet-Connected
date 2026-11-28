# 全局 flag 与动作同步

> 状态：已写完，**未编译、未实测**。

## flag：按"在哪里加的"决定给谁

| flag 是在哪里增删的 | 给谁 |
|---|---|
| 剧情执行中：动作（任何 `SerializableAction` 的 Trigger，包括 Pathfinder 和其它 mod 的动作）、任务函数（`addFlags` / `removeFlags` 等）、任务的起始 / 结束函数 | **所有人**（全局） |
| 上面这些里**带 Scope** 的：动作带 `players` / `group`，或任务根元素 / `missionStart` / `missionEnd` 带私有 scope | **只给执行它的那个玩家** |
| 不在剧情执行中：Hacknet 引擎自己加的（`BootFailure`、`startupBreakinTrap…`、`TutorialComplete`、`startVer:…`、`ExtensionFirstBootComplete`、提示图标、成就……） | **只在本机** |

- **扩展作者想定向加 flag**：在动作或任务函数上带 Scope。例如 `<RunFunction FunctionName="addFlags:got_key" players="p1"/>` 只会加给 p1。
- **不带 Scope 的剧情 flag**：所有人都会加上。
- **任务的起始 / 结束函数只看自己的 scope 标签**：`<missionStart>`、`<missionEnd>` 或任务根元素带 `players`（且不是 `PUBLIC`）就是私有，不受加载它的外层影响。通过 `LoadMission` 动作、`nextMission` 链加载的任务也一样；写了 `suppress="false"`、在读任务文件时就直接运行的起始函数，同样按这个任务文件自己的标签判断。只写 `group` 不写 `players` 视为公开，和 Scope 的规则一致。
- **动作和任务函数会继承外层**：在私有动作里执行的东西（例如带 `players` 的 `RunFunction`）也是私有的。
- **系统 flag 天然是个人的**：x-server 被删导致启动失败、被追踪到后重启、虚拟机攻击等，由引擎加的 flag 控制，只影响那个玩家自己的启动和修复判定。
- **阶段预设**：按服务端的 flag 判定。现在服务端也会收到全局 flag，所以阶段能反映所有人的进度。

### 怎么同步
- 每个人（客户端和服务端主机）把自己产生的全局 flag 增删报给终端（`FLAG_SET`）。
- 终端保存全局 flag 集合，**集合有变化才转发**给其他所有人。每个人本机都跑同一套剧情，同一个 flag 会被报好几次，只转发一次。
- 新加入的客户端、开局后准备完毕的主机，会收到当前所有全局 flag（`FLAG_ALL`），本机只补加缺的。
- 集合按服务器保存到终端的 `Storage/Flags/{服务器名}.txt`。
- 没有了以前服务端发事件的"2 秒内同类只发一次"的去重，不会再丢 flag。

## 动作：不再复制

以前任何人触发的动作都会复制给其他人再执行一遍。可每个人本机本来就在跑同一套剧情，于是**公开动作会被执行多次**（同一封邮件收到好几份）。现在不再复制动作，动作的效果由各自的同步负责：

| 效果 | 怎么同步 |
|---|---|
| 文件增删改 | `EVENT:FILE` |
| 邮件服务器、留言板、IRC 等 Daemon 内容 | HNC.Sync |
| flag | 上面的全局 flag |
| 端口 / 代理 / 防火墙 | 共享节点（`<HNCShared/>`），见 [README.NodeSync.md](README.NodeSync.md) |

**作者要注意**：只在一个玩家那里触发的动作，如果它的效果不属于上面这几类（比如让某台电脑崩溃、加条件动作、播放演出），现在只会发生在那个玩家自己那里。需要所有人都发生的，应写成不依赖单个玩家触发的公开剧情（每人本机各自触发），或者通过全局 flag 让大家的条件都满足。

## 代码位置

| 位置 | 内容 |
|---|---|
| `Shared/Flags/FlagProtocol.cs` | 协议（终端和两个插件共用） |
| `Shared/Flags/FlagSync.cs` | 剧情上下文（动作 Trigger / 任务函数入口打补丁）、Scope 判断、上报与应用 |
| `ClientPlugin/Flags/ClientFlags.cs`、`ServerPlugin/Flags/ServerFlags.cs` | 接上各自的收发和 Scope |
| `HacknetServerTerminal/Lobby/FlagRelay.cs` | 全局 flag 集合、转发、保存、补发 |

## 已知限制
- **只同步增删**：flag 的顺序不保证一致；没有同步的历史顺序。
