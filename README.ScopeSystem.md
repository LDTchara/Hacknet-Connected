# Hacknet-Connected — Scope 系统与支线角色分配

## 概述

本版本引入了 **Scope（执行范围）系统**，允许在 Hacknet 的 Action、Condition、Mission、Goal 上标记 `players` 和 `group` 属性，实现**多玩家协作时各客户端只执行自己的那部分逻辑**。

同时引入了 **selectplayer 命令系统**，用于私有支线任务开始前的角色分配。

---

## 一、Scope 系统

### 1.1 基本概念

每个 Action/Condition/Mission 元素默认对所有玩家可见（PUBLIC）。通过添加 `players` 和 `group` 属性，可以限定其只对特定玩家执行。

```
无属性 → 默认 PUBLIC，所有客户端都执行
players="PUBLIC" → 显式全局，所有客户端执行
players="1" group="mission1-private" → 仅被分配到该组的该角色的客户端执行
```

### 1.2 支持的 XML 元素

| 元素类型 | 示例 | 门控含义 |
|----------|------|----------|
| Action | `<RunFunction FunctionName="..." players="PUBLIC" />` | 谁执行这个动作 |
| Condition | `<HasFlags flag="X" group="m1" players="1" />` | 谁检查这个条件 |
| Goal | `<goal type="hasflag" target="X" group="m1" players="1" />` | 谁检查这个目标 |
| missionStart | `<missionStart val="0" name="addFlags:X" group="m1" players="1" />` | 谁的启动函数 |
| missionEnd | `<missionEnd val="5" name="addRank" players="PUBLIC" />` | 谁的完成函数 |
| nextMission | `<nextMission group="m1" players="1">Missions/Private/Next.xml</nextMission>` | 谁加载下一任务 |

### 1.3 核心架构

```
客户端本地过滤（非服务端路由）:

XML 文件（含 players/group 属性）
  ↓
ScopePatches 反序列化 Patch
  → 读取 players/group 存入 ScopeManager
  ↓
游戏运行中 Trigger/Check 触发
  ↓
ScopeGatePrefix（Harmony Prefix，自动枚举所有具体类）
  → 无 scope / PUBLIC → 放行
  → PRIVATE → 检查当前客户端身份 → 匹配则放行，否则跳过
```

### 1.4 存储结构

```csharp
ScopeManager
├── Global: List<object>                             // PUBLIC 对象
├── Private: Dictionary<string, List<object>>         // 玩家 → 私有对象列表
└── _meta: ConditionalWeakTable<object, ScopeInfo>    // 对象 → ScopeInfo
```

### 1.5 覆盖的类

| 体系 | 类型 | 数量 | 门控方式 |
|------|------|:----:|----------|
| 原版 Action | `SA* : SerializableAction` | 26 | Harmony TargetMethods 自动枚举 Trigger |
| 原版 Condition | `SC* : SerializableCondition` | 6 | Harmony TargetMethods 自动枚举 Check |
| PF Action | `DelayablePathfinderAction` | 1 | sealed override Trigger |
| Goal | `MisisonGoal` 子类 | 15 | Harmony TargetMethods 自动枚举 isComplete |

---

## 二、PTR 替换系统

### 2.1 Globle 替换（主线角色）

`Players-Cli.xml` 的 `<Globle>` 节定义角色对应的替换符映射：

```xml
<Globle SelectedName="p1">
  <PlayerReplacementsGroup name="p1">
    <PTR Org="#PLAYERNAME#" Curr="#P1NAME#" />
    <PTR Org="#PLAYER_IP#" Curr="#P1_IP" />
  </PlayerReplacementsGroup>
</Globle>
```

当玩家选择角色 `p1` 后：
- `#P1NAME#` → 在 `ComputerLoader.filter()` 中被替换为 `#PLAYERNAME#` → 最终解析为玩家名
- `#P1_IP#` → 在 filter 中被替换为 `#PLAYER_IP#` → 最终解析为玩家 IP

### 2.2 Private 替换（支线角色）

```xml
<Private>
  <PlayerReplacementsGroup name="mission1-private" num="2">
    <Player name="1">
      <PTR Org="#PLAYERNAME#" Curr="#M-P1NAME#" />
      <PTR Org="#PLAYER_IP#" Curr="#M-P1_IP" />
    </Player>
    <Player name="2">
      <PTR Org="#PLAYERNAME#" Curr="#M-P2NAME#" />
      <PTR Org="#PLAYER_IP#" Curr="#M-P2_IP" />
    </Player>
  </PlayerReplacementsGroup>
</Private>
```

当玩家通过 `selectplayer` 命令分配到 slot 后，对应的 `#M-P1NAME#` → `#PLAYERNAME#` 映射被动态注入。

### 2.3 实现方式

- `PlayerConfigLoader.LoadPrtMappings()` — 从 `Players-Cli.xml` 解析 PTR 映射
- `PlayerPtrFilterPatch`（`ClientPlugin/Patches/Patches.cs`）— `ComputerLoader.filter()` 的 **Prefix** 补丁
- Prefix 做字符串替换：`#P1_IP#` → `#PLAYER_IP#`，再由原版 filter 解析 → `os.thisComputer.ip`
- **不会产生循环调用**（Prefix 只做字符串替换，不调用 filter 自身）

---

## 三、selectplayer 命令系统

### 3.1 命令用法

游戏内终端输入：

```
selectplayer list              — 列出当前支线的可选角色
selectplayer <序号>            — 选择指定序号的角色
```

### 3.2 上下文自动检测

当加载一个有 `group` 属性的私有任务时，`PrivateMissionContext` 自动检测并设置 `SelectPlayerCommand.CurrentGroup`。

检测路径覆盖：
1. `ComputerLoader.loadMission()` — 脚本加载任务
2. `ActiveMission.ActivateSuppressedStartFunctionIfPresent()` — 任务中心 Accept / DLC Claim

任务完成时自动清除上下文。

### 3.3 选择流程

```
A 接取私有任务 → 自动检测 group，设置 CurrentGroup

A>>selectplayer list
═══ 支线任务 [mission1-private] 角色选择 ═══
总人数: 2
  [1]  角色1  [可选]
  [2]  角色2  [可选]

A>>selectplayer 1
[OK] 你选择了 [角色1]
  → 广播 PLAYER_SELECT 给所有客户端（经服务端转发）

B>>selectplayer list
  [1]  角色1  [已选: A]
  [2]  角色2  [可选]

B>>selectplayer 1
[!] 角色 [角色1] 已被 [A] 选择！

B>>selectplayer 2
[OK] 你选择了 [角色2]
  → 满员 → 广播 MISSION_READY

═══════════════════════════════════
   所有角色已分配完毕！
   任务即将开始...
═══════════════════════════════════
```

### 3.4 网络消息流

```
Client A → LiteNetLib → Server → Broadcast(除 A 外) → Client B
```

服务端 `OnNetworkReceive`（`NetworkManager.cs:379`）自动广播 `EVENT:PLAYER_SELECT` 和 `EVENT:MISSION_READY`，**零服务端代码改动**。

---

## 四、文件清单

### 新增文件

| 文件 | 作用 |
|------|------|
| `ClientPlugin/Scope/ScopeInfo.cs` | Scope 数据模型 |
| `ClientPlugin/Scope/ScopeManager.cs` | Scope 存储（Global + Private + ConditionalWeakTable） |
| `ClientPlugin/Scope/ScopePatches.cs` | 反序列化/序列化/Trigger/Check 门控补丁 |
| `ClientPlugin/Scope/ScopeMissionPatches.cs` | Mission 元素 scope 预扫描 |
| `ClientPlugin/Missions/PrivateMissionManager.cs` | 私有支线角色分配管理 |
| `ClientPlugin/Commands/SelectPlayerCommand.cs` | selectplayer 命令处理器 |
| `ClientPlugin/Missions/PrivateMissionContext.cs` | 私有任务上下文自动检测 |
| `ServerPlugin/Scope/ScopeInfo.cs` | ServerPlugin 侧 Scope 模型（备用） |
| `ServerPlugin/Scope/ScopeManager.cs` | ServerPlugin 侧 Scope 存储（备用） |
| `ServerPlugin/Scope/ScopePatches.cs` | ServerPlugin 侧补丁（门控已改为 no-op，委托给 ClientPlugin） |
| `ServerPlugin/Scope/ScopeMissionPatches.cs` | ServerPlugin 侧 Mission scope（备用） |

### 修改文件

| 文件 | 改动 |
|------|------|
| `ClientPlugin/Patches/Patches.cs` | 新增 `PlayerPtrFilterPatch`（filter Prefix 补丁） |
| `ClientPlugin/Players/PlayerConfigLoader.cs` | 新增 `CurrToOrgMappings` 字典、`LoadPrtMappings()`、`ClearMappings()` |
| `ClientPlugin/Players/PlayerData.cs` | 新增 `SelectPlayerConfig` 属性 |
| `ClientPlugin/Players/PlayerDataManager.cs` | 新增 `UpdateSelectPlayerConfig()`、加载时同步 `SelectPlayerConfig` |
| `ClientPlugin/ClientPluginMain.cs` | 新增 `SelectPlayerConfig` 静态字段、PostLoad 注册命令 |
| `ClientPlugin/Network/EventReceiver.cs` | 新增 `PLAYER_SELECT`、`MISSION_READY` 事件处理 |
| `ClientPlugin/Core/GameLauncher.cs` | `StartExtensionSession()` 中调用 `PlayerConfigLoader.LoadFromExtension()` |
| `HacknetServerTerminal/Persistence/ServerProfile.cs` | 新增 `PlayersCount` 属性 |
| `HacknetServerTerminal/Persistence/ServerStorage.cs` | 新增 `PlayersCount` 解析 |
| `HacknetServerTerminal/Commands/CommandHandler.cs` | `StartServer()` 通过 Pipe 查询 PlayersCount |
| `HacknetServerTerminal/Program.cs` | `PLAYERS_COUNT:` 加入认可回复列表 |
| `ServerPlugin/Network/ServerManager.cs` | 新增 `GET_PLAYERS_COUNT` 处理器 |

---

## 五、网络架构

```
┌─────────────────┐    LiteNetLib    ┌──────────────────┐
│  Client A       │ ◄──────────────► │  Server Terminal │
│  (ClientPlugin)  │                 │  (HacknetServer)  │
│  ScopePatches OK │    NamedPipe     │                   │
│  SelectPlayer OK │ ◄──────────────► │  零改动 YES        │
└─────────────────┘                 └────────┬─────────┘
                                             │ LiteNetLib
┌─────────────────┐                          │
│  Client B       │ ◄────────────────────────┘
│  (ClientPlugin)  │
│  ScopePatches OK │
│  SelectPlayer OK │
└─────────────────┘
```

- 所有 `EVENT:` 消息（含 `PLAYER_SELECT`、`MISSION_READY`）由服务端自动广播
- 服务端只做中继和日志记录，不参与 scope 过滤
- 每个客户端独立判断 scope，匹配则执行，不匹配则跳过
