# 阶段预设（给扩展作者）

把任务流程切成 A → B → C … 多个阶段，按角色给每个阶段准备一份"预设"。
玩家在游戏已经开始后才加入时，可以在准备大厅里从**自己角色**的预设中挑一个：
- **正在进行的阶段**：点 Join now，立即用这个预设开新游戏。
- **还没开始的阶段**：点 Wait for it，等这个阶段开始后自动进入。
- **已经过去的阶段**：不能选。
- 也可以点 **Start without preset**，从扩展默认的起点开新游戏。

> 状态：已写完，**未实测**。思路参考 StuxnetHN 的 Gamemode：预设是"新游戏的另一个起点"，不是存档快照。
> 存档快照里记的是作者那一局的世界；HNC 每个服务器的世界种子不同，节点和 IP 会对不上。

## 什么时候会显示

三个条件同时满足：
1. 游戏已经开始（大厅阶段为"游戏中"）。
2. 服务端没有这个玩家的存档：他是这个世界里的新玩家。有存档的老玩家照常读自己的存档。
3. 扩展里有阶段定义，并且其中有这个玩家角色的预设。

角色取自扩展根目录 `Config.tmp` 的第一行（如 `p1`），和 HNC 现有的角色机制一致。之后做了局内身份选择，会改成读选择结果。

## 阶段定义：`HNC_Phases.xml`

放在扩展根目录（也可以放 `HNC/Phases.xml`）：

```xml
<HNCPhases>
  <Phase id="A" title="Prologue">
    <Description>Everyone starts here.</Description>
    <!-- 没有 Conditions：开局即开始 -->
    <Set p="p1" pth="/Sets/p1_A.xml"/>
    <Set p="p2" pth="/Sets/p2_A.xml"/>
  </Phase>

  <Phase id="B" title="Chapter 1">
    <Description>The heist begins.</Description>
    <Conditions>
      <HasFlags>prologue_done,prologue_skipped</HasFlags>   <!-- 任意一个存在即可 -->
      <RequireFlags>met_bob</RequireFlags>                   <!-- 必须全部存在 -->
    </Conditions>
    <Set p="p1" pth="/Sets/p1_B.xml"/>
    <Set p="p2" pth="/Sets/p2_B.xml"/>
  </Phase>
</HNCPhases>
```

- **阶段元素名**：随意，比如 `<Ph1>`。没写 `id` 时用元素名当 id。`title` 或 `name` 是显示名。
- **阶段顺序**：就是文件里的书写顺序。"正在进行的阶段" = 按书写顺序最后一个已开始的。
- **条件**：按**服务端游戏**的 flag 判断，每秒检查一次。所有条件都满足时阶段开始，之后不会再变回未开始。
  - `HasFlags`：列出的 flag **任意一个**存在即可。可以写多个 `HasFlags`，每个都要满足。
  - `RequireFlags`：列出的 flag **必须全部**存在。
  - flag 之间用逗号或分号分隔。
  - 其他条件还没定义，写了会在日志里警告并忽略。
- **`Set`**：`p` 是角色名，`pth`（或 `path`）是预设文件，路径相对扩展根目录，开头的 `/` 可写可不写。

## 预设文件

```xml
<HNCSet title="Chapter 1 - Alice">
  <Description>You already know Bob. Your crew is waiting.</Description>
  <StartingMission>Missions/ch1_p1_start.xml</StartingMission>
  <StartingActions>Actions/ch1_p1_start.xml</StartingActions>
  <Flags>prologue_done,met_bob</Flags>
</HNCSet>
```

| 元素 | 不写 | 写 `NONE` | 写路径 |
|---|---|---|---|
| `StartingMission` | 用扩展默认的起始任务 | 没有起始任务 | 用这个任务开局 |
| `StartingActions` | 用扩展默认的起始动作 | 没有起始动作 | 开局时运行这个动作文件 |

- **`Flags`**：进入游戏后加到玩家身上。
- **程序、文件、邮件**：玩家在这个阶段"应该已经有"的东西，用起始动作给（例如 AddAsset、AddEmail）。
- **玩家电脑**：由 HNC 的角色机制决定，预设**不替换**。这一点和 Stuxnet 的 `playerCompID` 不同，是为了不和 HNC 冲突。
- **共享世界的状态**：别人在前面阶段改过的节点，不用写在预设里，加入后由服务端同步（HNC.Sync 全量同步，加上 `refresh fs`）。预设只管**属于玩家自己**的部分。

## 注意

- 扩展有开场动画（`HasIntroStartup`）时，起始任务在动画结束后才读取。预设对 `ExtensionInfo` 的改动会保留到下一次开局前，所以不受影响。
- 等待期间，如果服务端一下跳过了好几个阶段，玩家仍会用他选的那个阶段的预设进入。
- 阶段进度不会单独存档：服务端每次开局后按 flag 重新判断。只要 flag 还在存档里，结果就一样。

## 代码位置

| 位置 | 内容 |
|---|---|
| `Shared/Phases/PhaseConfig.cs` | 阶段定义、预设文件的解析与条件判断 |
| `ServerPlugin/Phases/ServerPhases.cs` | 服务端每秒判断阶段，报告给终端（`HNC_PHASES`） |
| `HacknetServerTerminal/Lobby/LobbyManager.cs` | 记录阶段进度，转给大厅成员，新成员加入时补发 |
| `ClientPlugin/Phases/ClientPhases.cs` | 列出本角色的预设、玩家的选择、开新游戏时应用预设 |
| `ClientPlugin/Lobby/ClientLobbyScreen.cs` | 大厅里的预设选择列表 |
