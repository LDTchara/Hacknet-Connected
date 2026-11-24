# 玩家间邮件（sendmail）

> 状态：已写，**未编译、未在游戏里验证**。

## 用法（客户端游戏内终端）

```
sendmail list                          列出在线玩家（编号和名字，标出自己）
sendmail <玩家名|编号> <主题> <正文...>   给在线玩家发邮件
```

- 命令行按空格拆分，所以**主题是一个词**，用下划线代替空格（`Hello_there` 收件人看到 `Hello there`），正文是剩下的所有内容。
- 玩家名忽略大小写；重名时要用编号。
- 限制：主题 ≤ 80 字符，正文 ≤ 2000 字符；不能给自己发。
- 收件人在游戏里会看到终端提示 `[Mail] 收到来自 xxx 的邮件：主题`，邮件出现在他自己 jmail 的收件箱里。
- 发件人会收到 `[Mail] 已发送给 xxx` 或 `[Mail] 发送失败：对方不在线`。

## 服务端玩家（主机）也能收发

服务端游戏不是终端里的一个连接，所以单独走 Pipe。在客户端的 `sendmail list` 里它显示为 **`[0] Server`**，可以按名字或编号 0 给它发信；
服务端游戏里也有 `sendmail` 命令，用法相同，`sendmail list` 列出在线的客户端玩家。

```
客户端 → 服务端：终端收到收件人 0 → Pipe: MAIL_TO_SERVER:{发件人id}:{b64 名}:{b64 主题}:{b64 正文} → 服务端写进自己的 jmail
服务端 → 客户端：sendmail 命令 → Pipe: MAIL_OUT:{收件人id}:{b64 主题}:{b64 正文} → 终端校验在线 → 转给客户端（发件人显示 "Server"）
                                                                  → 回 MAIL_RESULT:{id}:{ok|offline} 给服务端
在线列表：终端在玩家变化时给服务端推 PLAYERS:{id}:{name},…（不含它自己）
```

文件：`ServerPlugin/ServerMail.cs`、`HacknetServerTerminal/NetworkManager.cs`（`HandleServerMail`、`MAIL_SEND` 的 0 号分支、`BroadcastPlayerList`）。
服务端发信时发件人名固定为 `Server`；服务端收到的邮件投递到它自己的 jmail（账户 = 服务端的 `os.defaultUser.name`）。

## 实现

```
发件人 sendmail → MAIL_SEND:{收件人id}:{b64 主题}:{b64 正文}
终端   校验 → MAIL:{发件人id}:{b64 发件人名}:{b64 主题}:{b64 正文}  → 收件人
             → MAIL_SENT:{id} / MAIL_FAIL:{id}:{offline|self|toolong|invalid} → 发件人
收件人 把邮件写进自己的 jmail（netMap.mailServer，账户名 = os.defaultUser.name）
```

- **只定向转发，不广播**：没有走 `EVENT:`（那会发给所有客户端）。
- **发件人由终端盖章**：取自已注册的连接身份，客户端不能冒充别人。
- 收件人的游戏还没加载好 / 账户还没建好时，邮件留在客户端队列里，最多等 120 秒再丢弃。
- 邮件存进收件人自己存档里的 jmail，随存档保存。

文件：`ClientPlugin/PlayerMail.cs`（命令 + 收发 + 投递）、`ClientPlugin/EventReceiver.cs`（消息分发、在线列表）、`HacknetServerTerminal/NetworkManager.cs`（`MAIL_SEND` 转发）。

## 限制

- **没有离线投递**：收件人不在线就失败，邮件不会保存。
- **发件人没有"已发送"副本**。
- **玩家自己的邮箱（jmail）不参与 HNC.Sync 同步**：邮箱是每玩家一份，按 World 范围同步会让所有人的邮件互相可见。NPC 邮件服务器仍然同步。等 `Player` 范围打通后再改。
- 邮件内容只在终端内存里转手，不落盘；终端日志只记发件人和收件人，不记内容。
- 只有 `jmail`（`netMap.mailServer`）一个投递目标；如果扩展改了玩家邮箱节点，需要它仍是 `netMap.mailServer`。
