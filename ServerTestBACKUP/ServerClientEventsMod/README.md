# ServerClientEventsMod

Hacknet Pathfinder 事件框架 —— 一个前置库，为服务端开发提供统一的玩家操作检测事件 API。

## 架构概览

```
OS.Update (每帧)
    │
    ▼
EventDetectorManager —— 唯一 HarmonyPostfix，执行所有 [FrameDetect] 检测器
    │
    ├── 帧对比状态变化 → EventManager<T>.InvokeAll
    │       │
    │       └── GlobalEventBus → PlayerEventAPI.OnXxx += handler
    │
    └── Intercept 拦截链（唯一 HarmonyPrefix）
            │
            ├── InterceptConnect: Computer.connect Prefix
            ├── InterceptDisconnect: Programs.disconnect Prefix
            ├── InterceptLogin: Computer.login Postfix
            └── InterceptCommand: ProgramRunner.ExecuteProgram Prefix
                    │
                    └── 按注册顺序执行拦截器，首个非 PassThrough 决定结果
```

## 快速开始

引用此 dll 后，在 `Load()` 中直接订阅：

```csharp
public override bool Load()
{
    // PlayerEventAPI 已自动初始化，无需手动调用

    // 事后通知
    PlayerEventAPI.OnConnected += (comp, os) =>
    {
        LogServer($"[{os.thisComputer.ip}] CONNECT {comp.name} ({comp.ip})");
    };

    PlayerEventAPI.OnPortChanged += (comp, os, dPort, cPort, open) =>
    {
        string act = open ? "OPEN" : "CLOSE";
        LogServer($"[{os.thisComputer.ip}] PORT {act} {dPort} @ {comp.name}");
    };

    // 事前拦截
    PlayerEventAPI.InterceptCommand((cmd, args, os) =>
    {
        if (cmd == "ls")
        {
            os.write(GetFromServer($"ls {string.Join(" ", args)}"));
            return InterceptResult.Handled;
        }
        return InterceptResult.PassThrough;
    });

    return true;
}
```

## 事件列表

### 1. 连接 / 断连

```csharp
PlayerEventAPI.OnConnected += (Computer, OS) => { };
PlayerEventAPI.OnDisconnected += (IPFrom, OS) => { };
```

| 属性 | 类型 | 说明 |
|---|---|---|
| `Computer` | `Computer` | 连接的目标节点 |
| `IPFrom` | `string` | 断开的节点 IP |
| `CurrentPlayerIP` | `string` | 操作玩家 IP |

### 2. 登录

```csharp
PlayerEventAPI.OnLoggedIn += (Computer, OS, Username, IsAdmin) => { };
```

| 属性 | 说明 |
|---|---|
| `Username` | 登录用户名（"admin" 或普通用户） |
| `IsAdmin` | true=admin 登录, false=普通用户登录 |

### 3. 端口操作

```csharp
PlayerEventAPI.OnPortChanged += (Computer, OS, DisplayPort, CodePort, IsOpen) => { };
```

| 属性 | 说明 |
|---|---|
| `DisplayPort` | 显示端口号（界面看到的） |
| `CodePort` | 内部端口号（匹配 Computer.ports 列表） |
| `IsOpen` | true=开放, false=关闭 |

### 4. 代理

```csharp
PlayerEventAPI.OnProxyChanged += (Computer, OS, ProxyState, remainTicks, startTicks) => { };
```

`ProxyState` 枚举：`Added`, `Activated`, `Overloading`, `Recovering`, `Deactivated`, `Removed`

### 5. 防火墙

```csharp
PlayerEventAPI.OnFirewallChanged += (Computer, OS, FirewallState, Firewall) => { };
```

`FirewallState` 枚举：`Added`, `Removed`, `AnalysisStarted`, `AnalysisCompleted`, `Solved`

### 6. 节点可见性

```csharp
PlayerEventAPI.OnNodeVisibilityChanged += (Computer, OS, Visible) => { };
```

`Visible`：true=变为可见, false=变为不可见

### 7. 节点 IP 变更

```csharp
PlayerEventAPI.OnNodeIPChanged += (Computer, OS, OldIP, NewIP) => { };
```

检测 ISP Daemon、SAChangeIP 等所有改 IP 操作。

### 8. 文件系统

```csharp
PlayerEventAPI.OnFileSystemChanged += (Computer, OS, FileSystemAction, Path, SourceIP) => { };
```

`FileSystemAction` 枚举：`FileAdded`, `FileRemoved`, `FolderAdded`, `FolderRemoved`, `FileContentChanged`, `FileMoved`

检测目标：connectedComp + thisComputer 的文件树快照对比 + log 缓存匹配 SourceIP。

### 9. 任务

```csharp
PlayerEventAPI.On<PlayerMissionEvent>(e => { });
```

| 属性 | 说明 |
|---|---|
| `Mission` | `ActiveMission` 完整对象 (internal) |
| `Action` | `Activated` / `Completed` / `BranchAdded` / `Abandoned` |
| `MissionFile` | XML 源文件路径 |
| `StartFunctionName/Value` | 启动回调 |
| `EndFunctionName/Value` | 完成回调 |
| `NextMission` | 后续任务 |
| `EmailSender/Subject` | 任务邮件信息 |

### 10. 动作触发

```csharp
PlayerEventAPI.On<PlayerActionTriggeredEvent>(e => { });
```

| 属性 | 说明 |
|---|---|
| `Action` | `SerializableAction` 实例，可强制转换 |
| `ActionType` | 类名，如 `"SAChangeIP"` |
| `XmlContent` | 完整 XML `<ChangeIP TargetComp="..." NewIP="..." />` |
| `CurrentPlayerIP` | 当前玩家 IP |

覆盖所有 20+ 个 SA* 类型 + mod 自定义类型 + 条件触发/延时触发/直接调用全部路径。

### 11. 函数调用

```csharp
PlayerEventAPI.On<PlayerFunctionEvent>(e => { });
```

Patch `MissionFunctions.runCommand()`，覆盖 `addRank`, `setFaction:xxx`, `loadConditionalActions:xxx` 等所有内置/扩展函数。

### 12. Faction

```csharp
PlayerEventAPI.On<PlayerFactionEvent>(e => { });
```

`FactionAction` 枚举：`Joined`, `Left`, `Changed`, `ValueAdded`, `RankUp`

| 属性 | 说明 |
|---|---|
| `Faction` | `Faction` 对象 (public) |
| `PlayerValue` | 当前贡献值 |
| `OldValue` | 变更前值（仅 ValueAdded） |
| `IsRankUp` | 是否刚突破阈值 |

### 13. Flag

```csharp
PlayerEventAPI.On<PlayerFlagEvent>(e => { });
```

`FlagAction`：`Added` / `Removed`，Patch `ProgressionFlags.AddFlag/RemoveFlag`。

### 14. 邮件

```csharp
PlayerEventAPI.On<PlayerMailEvent>(e => { });
```

| 属性 | 说明 |
|---|---|
| `Receiver` | 收件人 |
| `Sender` / `Subject` / `Body` | 发件人/主题/正文 |
| `Attachments` | 附件列表（link/account 等） |

Patch `MailServer.addMail`（internal 类型，用 `AccessTools.TypeByName` 反射解析）。

### 15. 模块

```csharp
PlayerEventAPI.On<PlayerModuleEvent>(e => { });
```

`ModuleAction`：`Added`, `Removed`, `VisibilityChanged`, `NameChanged`

### 16. 命令检测

```csharp
PlayerEventAPI.On<PlayerCommandEvent>(e => { });     // 事后通知
```

## 拦截器

```csharp
PlayerEventAPI.InterceptConnect(ConnectInterceptor);     // Computer.connect Prefix
PlayerEventAPI.InterceptDisconnect(DisconnectInterceptor); // Programs.disconnect Prefix
PlayerEventAPI.InterceptLogin(LoginInterceptor);           // Computer.login Postfix
PlayerEventAPI.InterceptCommand(CommandInterceptor);       // ProgramRunner.ExecuteProgram Prefix
```

`InterceptResult` 枚举说明：

| 值 | 含义 | 对玩家的效果 |
|---|---|---|
| `PassThrough` | **放行** | 不拦截，交给原版处理 |
| `Handled` | **已处理，阻止原版** | 原版逻辑不执行，终端显示拦截器写入的内容 |
| `Cancelled` | **阻止，无输出** | 原版逻辑不执行，终端无输出（静默阻止） |

### 拦截器示例

```csharp
// 阻止连接特定节点
PlayerEventAPI.InterceptConnect((comp, os) =>
{
    if (comp.name == "blocked") return InterceptResult.Cancelled;
    return InterceptResult.PassThrough;
});

// 用服务端结果替换 ls
PlayerEventAPI.InterceptCommand((cmd, args, os) =>
{
    if (cmd == "ls")
    {
        os.write(GetFromServer($"ls {string.Join(" ", args)}"));
        return InterceptResult.Handled;
    }
    return InterceptResult.PassThrough;
});
```

多个拦截器按注册顺序链式执行，首个非 `PassThrough` 的结果决定最终行为。

## 通用事件入口

对于快捷入口未覆盖的事件：

```csharp
PlayerEventAPI.On<PlayerMissionEvent>(e => { });
PlayerEventAPI.On<PlayerActionTriggeredEvent>(e => { });
// 任意 PathfinderEvent 子类都可用
```

## 标准化字段

所有事件都包含 `CurrentPlayerIP`（`os.thisComputer.ip`），用于多人游戏区分客户端：

```csharp
PlayerEventAPI.OnConnected += (comp, os) =>
{
    // e.Os.thisComputer.ip 等同于 e.CurrentPlayerIP
    // 服务端用此字段识别玩家身份
};
```

## 全局替换字典

```csharp
// #BINARY# → "101010..." 等映射
GlobalReplaceDict.Set("#BINARY#", Computer.generateBinaryString(2000));
GlobalReplaceDict.Set("#PLAYERNAME#", os.defaultUser.name);
```

Pathfinder 的 `TextReplaceEvent` 也会自动写入字典。

## 调试日志

```csharp
// 在 Load() 中添加
EventTestHelper.StartLogging();
```

生成 `event_debug_log.txt`，记录所有 16 种事件的触发详情。

## 文件结构

```
Events/
├── EventDetectorManager.cs          — 唯一 HarmonyPostfix + 静态构造自举
├── FrameDetectAttribute.cs          — 标记检测器方法
├── EventAutoSubscribeAttribute.cs   — 标记事件类
├── GlobalEventBus.cs                — 自动扫描 + 绑定 PlayerEventAPI
├── GlobalInterceptors.cs            — 4 个拦截器入口定义 + HarmonyPrefix
├── PlayerEventAPI.cs                — 唯一公共入口
├── GlobalReplaceDict.cs             — #KEY# → 值 字典
├── EventTestHelper.cs               — 调试日志辅助
│
├── ConnectToComp.cs                 — 连接事件
├── OnPlayerDisconnectFromComputer.cs— 断连事件
├── PlayerLoginComp.cs               — 登录事件
├── PlayerPortEvent.cs               — 端口事件
├── PlayerProxyEvent.cs              — 代理事件
├── PlayerFirewallEvent.cs           — 防火墙事件
├── PlayerNodeVisibilityEvent.cs     — 节点可见性事件
├── PlayerNodeIPChangedEvent.cs      — 节点 IP 变更事件
├── PlayerFileSystemEvent.cs         — 文件系统事件
├── PlayerMissionEvent.cs            — 任务事件
├── PlayerActionTriggeredEvent.cs    — 动作触发事件
├── PlayerFunctionEvent.cs           — 函数调用事件
├── PlayerFactionEvent.cs            — Faction 事件
├── PlayerFlagEvent.cs               — Flag 事件
├── PlayerMailEvent.cs               — 邮件事件
├── PlayerModuleEvent.cs             — 模块事件
├── PlayerCommandEvent.cs            — 命令检测事件
└── NetMapDetector.cs                — 内部节点遍历合并
```

## 依赖

- PathfinderAPI
- BepInEx.Hacknet
- 0Harmony

## 编译

```
dotnet build -c Debug
```

或使用 Visual Studio 打开 `ServerClientEventsMod.sln`。
