# 组队任务（team）

需要多人一起做的支线任务：先到先得锁定任务 → 用命令组队 → 全员确认 → 队长提交 → 全队同时开始任务。

> 状态：命令、组队面板、任务界面里的组队条都已写完，**未编译、未实测**。支持合约板、任务中心、DHS 三种任务界面。

## 三种用法（效果完全一样，都由终端执行）

1. **在任务界面里直接组队（推荐）**：合约板、任务中心、DHS 的任务详情里，组队任务下方会出现一条**组队条**。
   - 第一行是状态：还没人锁定 / 被哪支队伍锁定 / 我的队伍几人、几人确认。
   - 第二行是按钮，按情况变化：**Form Team**（锁定并当队长）、**Join**、**Confirm / Unconfirm**、**Submit**（队长，满员且全员确认后可用）、**Disband**（队长）、**Leave**，以及 **Teams...**（打开组队面板）。
   - 原版的 Accept / Claim Contract 按钮仍然在，点了等同于 Form Team。
2. **组队面板**：输入 `team ui`，或点组队条上的 **Teams...**。
   - 左边是所有队伍，右边是选中队伍的位置、成员和确认情况。
   - 可以加入、选空位置（Take slot）、确认、离开；队长还可以移出成员、提交、解散。
   - 按 Esc 或 Close 关闭。面板打开时游戏照常运行，只是终端暂时不接收按键。
3. **命令**：见下表。


1. **锁定**：在任务板上点组队任务的 **Accept**。任务**不会立即开始**，而是向服务器申请锁定。
   - 你是第一个：任务为你锁定，你成为队长，队伍自动命名为 `team1` 之类。
   - 已经被别人锁定：提示是哪支队伍、队长是谁，可以 `team join <队伍>` 加入。
2. **组队**：其他玩家输入 `team join <队伍名>` 加入。`team list` 查看所有队伍。
3. **确认**：每个人输入 `team confirm`。有人加入、离开、换位置时，所有人的确认都会清空，需要重新确认。
4. **提交**：队长输入 `team submit`，要求满员且全员已确认。服务器通知每个成员，各自在本机接受任务，支线身份和占位符同时设置好。
5. **解散**：提交前，队长 `team disband`（或队长 `team leave`）即可解散，任务解除锁定，任何人都可以重新接取。

| 命令 | 说明 |
|---|---|
| `team` | 查看我的队伍（位置、成员、确认情况） |
| `team ui` | 打开组队面板 |
| `team list` | 列出所有队伍 |
| `team join <队伍>` | 加入组队中的队伍（自动分配空位置） |
| `team leave` | 离开队伍（队长离开 = 解散） |
| `team slot <n>` | 换到第 n 个空位置 |
| `team confirm` / `team unconfirm` | 确认 / 取消确认（当前有任务在进行时不能确认） |
| `team name <名字>` | 队长：给队伍改名（不能有空格） |
| `team kick <玩家>` | 队长：移出成员（玩家名或 `P编号`） |
| `team submit` | 队长：提交，全队开始任务 |
| `team disband` | 队长：解散并解除锁定 |

规则：
- 一个任务同时只能有一支队伍：**先到先得**。已开始的队伍会一直占着这个任务。
- 一个人同时只能在一支"组队中"的队伍里。
- 提交后队伍不能再改。成员提交时掉线了也没关系，他下次加入时会自动补上任务。

## 扩展作者怎么写

不需要新格式，沿用现有的支线角色配置：

1. 任务 XML 带 `group`：根元素，或 `missionStart` / `missionEnd` / `nextMission` 上都可以。
2. `Players-Cli.xml` 的 `<Private>` 里有同名的组：`num` 是需要的人数，`<Player>` 的顺序就是位置 1、2…
3. 任务放进任务板（合约板 / DLC 任务中心），玩家从任务板接取。

```xml
<!-- Missions/Heist.xml -->
<mission id="heist" activeCheck="true" group="heist">
  <missionStart val="0" group="heist" players="1">addFlags:heist_p1_started</missionStart>
  …
</mission>

<!-- Players-Cli.xml -->
<Private>
  <PlayerReplacementsGroup name="heist" num="2">
    <Player name="1">
      <PTR Org="#PLAYERNAME#" Curr="#HEIST1NAME#" />
      <PTR Org="#PLAYER_IP#"  Curr="#HEIST1_IP#" />
    </Player>
    <Player name="2">
      <PTR Org="#PLAYERNAME#" Curr="#HEIST2NAME#" />
      <PTR Org="#PLAYER_IP#"  Curr="#HEIST2_IP#" />
    </Player>
  </PlayerReplacementsGroup>
</Private>
```

提交后：
- **Scope**：每个成员的支线身份是 `heist` 组的第几号，`group="heist" players="1"` 的动作 / 条件只在 1 号位置的玩家那里执行。见 [README.ScopeSystem.md](README.ScopeSystem.md)。
- **名字占位符**：`#HEIST1NAME#`、`#HEIST2NAME#` 在**所有玩家**那里都替换成对应成员的游戏内名字。
- **IP 等其他占位符**：只在该位置的玩家自己那里生效，例如 `#HEIST1_IP#` 在 1 号玩家那里是他自己的 IP。别人的 IP 占位符目前不会替换。

## 原理

- **结构化状态**：终端在队伍有任何变化时广播 `TEAM_STATE`（所有队伍、成员、位置、确认、来源），玩家加入时补发一次。组队条和面板都按它画，不解析文字。
- **任务来源**：在任务界面上锁定时，会记下"哪种界面、哪台电脑"（`listing` / `hub` / `dhs`）。提交后，每个成员**走同一个界面的原生接受**：
  - **DHS**：调用它自己的认领，合约会标成被本人认领。DHS 的任务必须认领后才能在 DHS 上点 Complete 完成。
  - **任务中心**：调用它自己的接受，会归档合约。
  - **合约板**，或找不到来源时：走通用的接受步骤。

- **终端是权威**（`HacknetServerTerminal/Lobby/TeamManager.cs`）：管理任务锁、队伍、成员、位置、确认状态。所有 `team` 子命令都由终端执行，客户端只转发参数、显示回复。
- **玩家这一侧**（`Shared/Teams/TeamPlayer.cs`，客户端和服务端主机共用；`ClientTeams` / `ServerTeams` 只接上收发消息、写支线身份和占位符的钩子）。拦截 Accept：
  - 合约板的 Accept 写在 `MissionListingServer.draw` 里。在 draw 执行期间拦截任务的 `sendEmail`（并撤销 `currentMission`），再拦截紧接着的 `ActivateSuppressedStartFunctionIfPresent`。
  - DLC 任务中心直接拦截私有方法 `MissionHubServer.acceptMission`。
  - 剧情自动下发的任务（`loadMission`）不受影响。单机玩扩展（没有联机）时也不拦截。
- **任务 key**：任务文件相对扩展根目录的路径（小写），各端一致。
- **提交后**：终端给成员发 `TEAM_START`，成员在本机执行和 Accept 相同的步骤（设 `currentMission`、发邮件、运行起始函数），然后回 `TEAM_ACK`；给其他人发 `TEAM_APPLY`，只设置占位符。
- **持久化**：已开始的队伍存到终端的 `Storage/Teams/{服务器名}.txt`。玩家加入大厅时，终端补发分配；还没接到任务的成员会补发 `TEAM_START`。客户端在开局后重新应用一次，因为开局时会清空占位符映射。

## 已知限制 / 之后要做

- **服务端主机**：可以入队，算 0 号玩家。主机在服务端游戏里用同样的 `team` 命令，任务板上的 Accept 也同样被拦截、申请锁定，消息经管道和终端通信。
- **只拦截三种任务界面的接受**（合约板、任务中心、DHS）：邮件直接下发的任务没有接受这一步，不走组队流程。
- **和 `selectplayer` 的关系**：旧的 `selectplayer` 命令还在，它写的是同一份支线身份。两者混用会互相覆盖，建议只用 `team`。
- **接受条件**：任务的接受条件（需要的 flag）只在锁定的人本机的任务板上检查过，其他成员不再检查。
- **组队条的位置**：组队条画在任务详情底部按钮的上方，正文很长时会盖住最后几行（合约板的正文可以滚动）。
- **队伍改名**只能用命令 `team name`（面板里有提示），面板里没有文字输入框。
