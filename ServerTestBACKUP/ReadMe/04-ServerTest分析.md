# ServerTest 分析

## 现状架构

```
                    NamedPipe              LiteNetLib UDP
Hacknet 游戏进程 ───────── HacknetServerTerminal ───────── 客户端
├─ ServerPlugin             ├─ Pipe 转发事件                │
│  (BepInEx)               ├─ CommandHandler              TestClient
│  ├─ EventForwarder       ├─ NetworkManager.Broadcast()
│  ├─ ServerManager        └─ ServerStorage (INI)
│  └─ Harmony Patches
└─ ServerClientEventsMod
   (事件捕获)
```

## 4 个组件

### ServerClientEventsMod（事件检测层）
- 独立项目，不依赖 ServerPlugin
- 用 `[EventAutoSubscribe]` + Harmony Prefix/Postfix 捕获游戏操作
- 15+ 种自定义事件：文件系统、登录、连接、端口、防火墙、Flag、任务、邮件、命令、Action、Proxy
- 事件驱动引擎：GlobalEventBus + EventDetectorManager + FrameDetect

### ServerPlugin（BepInEx 模组）
- 跑在游戏进程内，控制台进程的 NamedPipe 客户端
- EventForwarder：订阅 AC 事件，格式化为 `EVENT:TYPE:data` 冒号文本，通过 Pipe 发送
- ServerManager：Pipe 客户端 + 后台 ListenLoop + 主线程 ProcessPendingMessages
- 4 个 Harmony Patch：
  1. `CreateServerButtonPatch` — 扩展详情页加 [Start Server] 按钮
  2. `CreateAccountPatch` — 拦截账户创建，IsServerLaunch=true 时走服务端模式
  3. `ResetServerStatePatch` — 退出扩展时重置
  4. `GameUpdatePatch` — 每帧处理消息队列 + 显示 SERVER MODE 横幅

### HacknetServerTerminal（控制台应用）
- 纯 .NET，零依赖 HN/PF
- NamedPipeServerStream 服务端（"HacknetServerPipe"）
- 握手流程：HELLO → 按 Y 确认 → OK
- PipeListenLoop 后台读 Pipe → NetworkManager.Broadcast()
- NetworkManager：LiteNetLib UDP 9050
- CommandHandler：双态命令系统（normal / create）
- ServerStorage：INI 格式持久化
- ServerProfile：服务端配置数据模型

### TestClient（远程客户端示例）
- 连接 localhost:9050，连接密钥 "HacknetServer"
- 连接后注册 `REGISTER:testTool:ConsoleClient`
- 收发 LiteNetLib 消息

## 与正式方案的差异

| 维度 | 正式方案 | ServerTest 现状 |
|------|---------|----------------|
| 服务端角色 | GameState 权威状态 | 纯事件转发器，无游戏状态 |
| 验证逻辑 | VERT 抽出来的纯函数 | ❌ 零验证 |
| 协议 | XML | 冒号文本（EVENT:FILE:...）|
| 冲突处理 | version++ + first-write-wins | ❌ 无 |
| 入网同步 | NodeManifest → 增量差异 | ❌ 无 |
| 客户端插件 | 发 XML→服务端→收响应→写本地 | 只发了事件的一半 |
| LiteNetLib | 服务端直接连客户端 | 控制台中转（多一层 Pipe）|

## 为什么要用 NamedPipe

ServerPlugin（游戏进程）与 HacknetServerTerminal（控制台进程）之间需要 IPC。
NamedPipe 比 TCP loopback 快（不走网络栈）、不需要管端口冲突、默认只允许本机进程连接。
后期要跨平台可改为 Unix domain socket。

通信方向：
- 游戏 → 控制台：事件（EVENT:FILE / EVENT:FLAG 等）
- 控制台 → 游戏：管理命令（CREATE_ACCOUNT / LAUNCH_SERVER / DELETE_ACCOUNT）

控制台是 Pipe 服务端，因为控制台先启动等待游戏连上来。

## AC 当前思路（2026-06-21 确认）

1. **事件系统不改**：ServerClientEventsMod 保持现状
2. **服务端跑一份完整游戏实例**：不手写加载代码
3. **存储模型修正**：文件系统全部同步，只有三样可能不同（支线任务、Flag、节点破解状态）
4. **做成扩展模组，不是全局模组**
5. **掉线处理**：客户端本地临时存档 + 拉服务器主线进度同步

## 无头服务器的矛盾

ServerTest 当前依赖一台有屏幕有音频的 Windows 机器跑 Hacknet。
正式方案的服务端（GameState + 验证逻辑）是纯 .NET 控制台应用，不需要 GraphicsDevice。
AC 目前不考虑无头机器，走事件中继路线在开发早期更容易跑通。


---

## 后续讨论补充

### NamedPipe 的由来

AC 不愿意手写扩展加载代码（实例化失败、email 为空等问题），
所以选择让游戏进程自己跑，通过 NamedPipe 把事件转发给控制台进程。
手写加载代码效率低且容易出 bug。

### ServerPlugin 的本质

ServerPlugin 不能单独存在——它依赖一份能跑起来的 Hacknet + PF。
对于没有显示设备和音频设备的无头服务器来说不适用。
AC 当前不考虑无头服务器场景。
未来迁移路径：
1. 跑通主机模式端到端流程
2. 抽 HostGameState 层：把游戏当前状态快照序列化
3. 抽验证逻辑：事件处理中校验节点/端口等
4. 替换游戏进程为纯 .NET 控制台

### 当前协议现状

冒号文本 `EVENT:TYPE:data`，不是正式方案的 XML。
没有验证层，没有冲突处理。
AC 的进展属于技术探索阶段，不是正式实现。
