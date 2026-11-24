# IRC / DHS 发消息（输入栏 + sendto）

> 状态：第一、二部分都已写，**未编译、未在游戏里验证**。

## 用法

**输入栏**：在 IRC 或 DHS 界面最下面有一个输入框和 `Send` 按钮。

- **Enter 发送，Shift+Enter 换行**（也可以点 `Send`）。最多 8 行、300 字符，超过 5 行时输入框内部滚动。
- 支持 ← → ↑ ↓、Home / End（当前行）、Delete / Backspace（按住重复），能输入中文（输入法）和 Ctrl+V 粘贴。
- **有焦点时终端被屏蔽**：点进输入框后，打字和 Enter 不会再同时进到下面的终端里。点输入框以外的地方失去焦点，终端恢复。
- 不支持鼠标拖选 / 选区、Ctrl+C 复制。

**命令**（效果相同，发到你正在看的或当前连接的电脑上的 IRC / DHS）：

```
sendto <消息>
```

### @ 语法

| 写法 | 效果 |
|---|---|
| `@everyone` | 提及全体成员，原样留在消息里 |
| `@用户名` | 提及 IRC / DHS 里的某个用户，原样留在消息里 |
| `@1.2.3.4` | 消息里保留 `@1.2.3.4`，并附上这个节点（点附件上的 `+` 会在对方地图上发现该节点） |
| `@1.2.3.4 -p` | 再附上该节点上**你已记录的**账户和密码（最多 5 个，管理员优先） |
| `@1.2.3.4 -p 账户 密码` | 手动指定账户密码。账户不存在或密码不对 → `os.write` 提示错误，**这条消息整条不发送** |

细节：

- 手动账户密码**不会出现在消息正文里**，只在附件里；`-p` 本身也不出现在正文里。
- `-p` 后面如果还有**至少两个词**，会被当作"账户 密码"。如果想自动附带已记录账户、后面还要写正文，请把 `@IP -p` 放在句末。
- 节点必须是你已经发现的（地图上可见），否则提示"你还没有发现节点"。
- 未知的 `@名字` 当作普通文字，不报错。
- 消息最长 300 字符。

### Tab 补全和候选

在输入框里打 `@` 会在输入栏上方弹出候选：`@everyone`、IRC/DHS 的用户名、已发现节点的 IP（旁边显示节点名）。
继续输入会过滤（用户名忽略大小写，IP 按前缀，也可以按节点名前缀）。

- **Tab**：补全。只有一个候选时直接补全并加空格；多个候选时第一次 Tab 填入第一个，再按 Tab 切换下一个。
- **点击候选**：补全并加空格。
- `@IP` 后面打 `-` 会提示 `-p`。

## 实现

| 部分 | 文件 | 说明 |
|---|---|---|
| 解析 / 补全（纯逻辑，有单元测试） | `HNC.Chat.Core/Chat.cs` | `MessageParser`、`Completer`、`Attachments` |
| 游戏适配 | `Shared/IrcChat/GameWorld.cs` | 把节点、账户、用户名提供给解析器 |
| 发送 | `Shared/IrcChat/IrcSender.cs` | 写进 IRC 日志；发送期间临时去掉 `LogAdded`，自己发的消息不再弹"新消息"提示 |
| 输入栏 | `Shared/IrcChat/IrcInputBar.cs` | Harmony 补丁 `IRCSystem.Draw`：Prefix 把记录区缩短一行，Postfix 在腾出的位置画输入栏 |
| 命令 | `Shared/IrcChat/IrcChatFeature.cs` | 注册 `sendto` |

- `Shared/IrcChat` 是**共享源码**，ClientPlugin 和 ServerPlugin 各编译一份（用 `<Compile Include=... LinkBase>`），所以客户端玩家和服务端主机玩家都有这个功能。
- IRC 和 DHS 都通过 `IRCSystem.Draw` 画聊天，所以只补这一个方法就覆盖两者。
- 节点/账户用原版的**附件**格式写进 IRC 日志（`!ATTACHMENT:link#%#名字#%#节点id` / `account#%#名字#%#节点id#%#账户#%#密码`），
  原版界面会把它渲染成带 `+` 按钮的一行，点击效果和原版一致。节点用 `idName` 而不是 IP 定位，因为 IP 可能变化。
- 消息经由**同步层**带给其他玩家：IRC 的聊天记录本来就在同步范围内；这次给 DHS（`HomeBase` 根目录）也加了适配。

## 已知限制 / 没做的

- 提及（`@用户名` / `@everyone`）**不会**给被提及的真人玩家弹通知（原版的提醒图标靠 `TagsToAlertFor` 配置，没有改动）。
- 自己发的消息不弹提示；别的玩家收到的是同步过来的日志，同样不会触发"新消息"提示（同步写入不走 `LogAdded`）。
- 候选里的用户名只来自频道的用户配置（`HighlightKeywords`），不包括只发过言但没配置的人。
- 手动账户密码在你自己的终端里会明文显示（因为是你输入的），只是不会进入聊天正文。
- 输入栏用 `TextBox.doTextBox`，**中文输入法能否在里面输入未验证**。

## @ 提及的红色高亮

提到**你自己**（`@你的账户名`）或 `@everyone` 的那一行会画红色底，和原版公告（`!ANNOUNCEMENT!`）用的是**同一个**红色（`Color.Red * 0.22`，同样的位置和大小）——
原版聊天里唯一的红色高亮就是公告那一种，所以沿用它。
- `@Bob` 不会误中 `@Bobby`；邮箱里的 `a@Bob` 不算；中文名（`@张三你好`）后面直接接文字也能识别。忽略大小写。
- 高亮是按**自动换行之后的每一"行"**画的：一条跨多行的消息，只有包含 `@` 的那一行是红的。
- 原版自己产生的消息也会生效，例如 DHS 的 `CONTRACT CLAIMED: @你的名字 claimed …`。

## 节点 / 账户附件的格式

附件用原版 `AttachmentRenderer` 的格式写进聊天记录，所以显示出来和原版一样：一行带名字，右边一个 `+` 按钮，点击后效果也和原版一致
（`link`：在你的地图上发现该节点；`account`：发现节点并把该账户标记为已知）。

```
!ATTACHMENT:link#%#节点名#%#节点id
!ATTACHMENT:account#%#节点名#%#节点id#%#账户#%#密码
```

单元测试里按原版的拆分方式（去掉前缀，按 `#%#` 拆并丢弃空项）核对了每个字段的下标：`array[0]` 是类型，`array[2]` 是节点 id（原版用它 `getComputer`），
account 的 `array[3]` / `array[4]` 是账户和密码。原版拆分会丢弃空字段、让后面的下标错位甚至越界，所以名字、账户、密码**从不留空**（空的会换成 id 或 `?`）。
**这只证明格式和原版的读取方式一致，还没有在游戏里看过 `+` 按钮的实际效果。**

---

# 第二部分：中间层、禁言动作、发言条件 / 目标

## 中间层 `ChatBus`（`HNC.Chat.Core`，公开 API）

其他代码按 **computerId**（电脑的 `idName`）读取玩家每次从输入框发出的消息：

```csharp
using HNC.Chat;

// 订阅：每发出一条就触发一次
ChatBus.MessageSent += m =>
{
    if (m.ComputerId == "dhsNode" && m.Player == "alice")
        Console.WriteLine($"{m.Player} 说: {m.Text}  (来源 {m.Source}, 附件 {m.Attachments.Count} 个)");
};

// 查询：某台电脑上（可选：某个玩家）发出的全部消息，按发送顺序
IReadOnlyList<ChatMessage> all   = ChatBus.GetMessages("dhsNode");
IReadOnlyList<ChatMessage> alice = ChatBus.GetMessages("dhsNode", "alice");
ChatMessage last                 = ChatBus.Last("dhsNode", "alice");     // 没有则为 null

// 直接判断
bool said = ChatBus.HasSent("dhsNode", "alice", "password", ChatMatchMode.Fragment, ignoreCase: true);

// 禁言（玩家名写 "*" = 该频道所有人）
ChatBus.SetMuted("dhsNode", "alice", true);
bool muted = ChatBus.IsMuted("dhsNode", "alice");
```

`ChatMessage`：`Sequence`（递增序号）、`ComputerId`、`Player`（游戏账户名）、`Text`、`Source`、`Attachments`（`!ATTACHMENT:…` 原文）、`Time`。

**消息来源 `ChatSource`**
- `InputBox`：输入栏或 `sendto` 发出的。`Text` 是正文，手动写的账户密码不在里面；节点 / 账户在 `Attachments` 里。
- `MissionResponse`：**提交 DHS 任务时的"附加内容"**。vanilla 会在任务完成后把它们以 `Channel` 的名义写进聊天记录；
  这里在它清空之前取一份，任务**确实完成**后当作该玩家发出的消息发布（任务没通过则不发布）。

**范围和寿命**
- 只记录**本机玩家**发出的消息（每个客户端各自一份，服务端主机玩家在服务端那份）。别的玩家的消息只在同步过来的 IRC 日志里。
- 历史在内存里，最多 2000 条；**换存档 / 重启后清空**。
- 订阅者抛出的异常会被吞掉，不影响发送。

**给其他 mod 用**：引用 `HNC.Chat.Core.dll`，把它和 `HNC.Sync.*.dll` 一样放在扩展的 `Plugins` 目录里，插件里的解析器会让所有人拿到同一个实例。
（单 dll 合并构建时它被编进了插件，第三方 mod 绑不到，见下面"打包"。）

## 禁止 / 解除禁止发言

```xml
<BanChatInput   Computer="dhsNode" Player="p2" />
<UnbanChatInput Computer="dhsNode" Player="p2" />
```

- `Computer`：频道所在电脑（idName / IP / 名字，必填）。
- `Player`：游戏账户名**或角色**（`p1`、`p2`…）；写 `*`、`everyone` 或不写 = 所有人。
- 继承 `Delay` / `DelayHost`，可以延时执行。
- 被禁止的玩家：输入栏和 `sendto` 都发不出去，提示"你被禁止在此频道通过输入框发言"，输入栏的占位提示也变红。**不影响提交 DHS 任务的附加内容。**
- 状态存在**存档标记**里（`HNCChatMute:{computerId}:{玩家名}`），随存档保存；任务脚本里也能用 `HasFlags` 看到。
- 动作在每个客户端都会执行：`Player` 不是本机玩家的客户端什么都不做。所以**不需要**再给动作加 `players` 属性。
- `Player="*"` 的禁止不会被 `Player="alice"` 的解除覆盖，要用同样的写法（`Player="*"`）解除。

## 条件：发送过某条消息

```xml
<ChatSent Computer="dhsNode" Player="p2" Message="xxx" Match="Fragment" />
```

## 目标：发送过某条消息

```xml
<goal type="chatsent" Computer="dhsNode" Player="p2" Message="xxx" Match="Exact" />
```

| 属性 | 说明 |
|---|---|
| `Computer` | 频道所在电脑，必填 |
| `Player` | 账户名或角色；不写 = 本机玩家 |
| `Message` | 要匹配的文字，必填 |
| `Match` | `Fragment` 片段（默认）/ `Exact` 完全 / `NotContains` 不包含。也认中文 `片段` `完全` `不包含` |
| `IgnoreCase` | 默认 `true` |
| `Negate` | `true` = 把整个结果取反 |

**匹配规则**：对玩家发过的**每一条**消息逐条判断，只要有**一条**满足就为真。
- `Fragment`：这条消息包含 `Message`。
- `Exact`：这条消息和 `Message` 完全相同（忽略首尾空格）。
- `NotContains`：这条消息**不**包含 `Message`。所以它的意思是"至少有一条不含 xxx 的消息"，**不是**"从没说过含 xxx 的话"。
  想表达后者，用 `Fragment` + `Negate="true"`。

**数据来源**：内存里的 `ChatBus` 历史 ＋ 该电脑上 IRC / DHS 聊天记录里署名为该玩家的消息。
聊天记录在存档里，所以**输入栏发过的话换存档后仍然查得到**；DHS 任务附加内容在日志里署名是 `Channel`，
所以只在**本次会话**的内存历史里，读档后不再被匹配。

**多人**：只能看到本机玩家说的话，`Player` 不是本机玩家时结果恒为 `false`。
条件 / 目标本身仍然可以用 `players` / `group` 属性按玩家分流（Scope 系统会门控它们）。

条件 / 目标最多每 0.5 秒重新计算一次（读聊天记录不便宜，而它们每帧都会被检查）。

## 打包成一个 dll（测试用）

```bash
dotnet build ServerPlugin/ServerPlugin.csproj -p:SingleDll=true
dotnet build ClientPlugin/ClientPlugin.csproj -p:SingleDll=true
```

输出：`Output\ServerPlugin.Single\ServerPlugin.dll` 和 `Output\ClientPlugin.Single\ClientPlugin.dll`，各自**就是 `Plugins` 目录里需要的全部**。

- 做法：把 `ServerClientEventsMod`、`HNC.Sync.*`、`HNC.Chat.Core` 的**源码**直接编进插件，没有任何外部引用；LiteNetLib（客户端）仍由 Costura 嵌入。普通构建不受影响。
- 用单 dll 时，`Plugins` 里要**删掉** `ServerClientEventsMod.dll` 和所有 `HNC.*.dll`，否则 `ExtraEventsPlugin` 会出现两份（同一个 GUID）。
- 两个插件的 `Load` 都会 `PatchAll` 同一个程序集，已用 `HarmonyPatchOnce` 去重，否则每个补丁会打两遍。
- **限制**：`HNC.*` 的类型成了插件程序集的一部分，第三方 mod 引用独立的 `HNC.Sync.Abstractions.dll` / `HNC.Chat.Core.dll` 时绑不到它们。
- `SeedSyncGlobal.dll` 仍然是单独的（它在全局 `BepInEx\plugins`，不在扩展的 `Plugins` 里）。
- 切换普通构建和单 dll 构建时 `obj` 会被重新还原，第一次会慢一点。
