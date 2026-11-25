# HNC 多人适配缺口 TODO

> 对照基准（均为静态源码对比，**未做运行时验证**）：
> - Hacknet 5.069 源码：`ServerTestBACKUP/Hacknet Source Code 5.069/hacknet-main`（359 个 .cs）
> - Pathfinder 源码：`ServerTestBACKUP/Hacknet-Pathfinder-master`
> - 设计文档：`ServerTestBACKUP/ReadMe/01..11`
>
> 生成日期：2026-09-25

---

## 对照：已完成部分（勿重复投入）

现有门控由 `ScopeGatePatches.TargetMethods()` 自动枚举，已拦到四类：

- 原版 26 个 `SA*` Action 的 `Trigger(object)`（含 `SALaunchHackScript`）
- 原版 6 个 `SC*` Condition 的 `Check(object)`
- `DelayablePathfinderAction.Trigger(object)`（`sealed override`，PF delayable action 的共用入口）
- 原版 15 个 `MisisonGoal` 子类的 `isComplete(List<string>)`

Mission 层（客户端 `ClientPlugin/Scope/ScopeMissionPatches.cs`）：根元素、`missionStart`/`missionEnd`/`nextMission`、`sendEmail`、`ComputerLoader.loadMission`。

反序列化侧：原版 Action/Condition + PF `ReadAction`/`ReadCondition`/`LoadGoal`。

**系统性限制（下面多条缺口的根因）**：`EnumerateConcreteMethods<T>()` 遍历的是 `typeof(T).Assembly.GetTypes()`，也就是**只扫 Hacknet.dll**。任何定义在 PathfinderAPI.dll 或第三方程序集里的子类都不会被枚举到。

---

## P0 — 隐蔽且会静默失效

### [~] 1. PF Condition 的 scope 门控缺失（代码已写，待运行时验证）

> **2026-11-23 笔记**：`ScopeGatePatches.PatchExternalConditions(Harmony)` 在 `ClientPluginMain.PostLoad` 里扫描 Hacknet.dll 以外所有程序集，对声明了 `Check(object)` 的 `SerializableCondition` 子类手动打 `ScopeGatePrefix`。不能放进 `TargetMethods()`，因为 PatchAll 时其它插件还没加载。当前环境没有 dotnet，**未编译**。

- **问题**：`PathfinderCondition : SerializableCondition` 定义在 PF 程序集（`PathfinderAPI/Action/PathfinderCondition.cs:8`），不在枚举范围；也没有单独 patch 它的 `Check`。
- **表现**：scope 属性经 `ActionsLoader.ReadCondition` 的 postfix **能注册进去**，但 `Check` 时无人查 → 门控静默失效。作者会误以为是自己的写法有问题。
- **影响**：所有基于 PF 自定义 Condition 的多人分流不生效。
- **建议**：单独 patch `PathfinderCondition` 的具体子类 `Check(object)`，或在 `ConditionManager` 的加载/求值路径上加统一门控。
- **验收**：写一个最小扩展，自定义 condition 带 `players="p2"`，在 p1 客户端确认不触发。

### [~] 2. PF Condition 的序列化丢失（代码已写，未编译）

> **笔记**：已在 Client/Server 两侧补 `PathfinderCondition.GetSaveElement` postfix。局限：若子类重写 `GetSaveElement` 且不调用 base，postfix 不会触发（`PathfinderAction` 现有补丁同样有此局限）。

- **问题**：只 patch 了 `PathfinderAction.GetSaveElement`，**没有** patch `PathfinderCondition.GetSaveElement`。
- **影响**：PF condition 的 `players`/`group` 存档写回时丢失。
- **建议**：对称补一个 postfix，逻辑同 `GetSaveElementPostfix`。
- **验收**：带 scope 的 PF condition 存盘后重新加载，属性仍在。

### [~] 3. 原版 Condition 的序列化丢失（代码已写，未编译）

> **笔记**：已在 Client/Server 两侧补 `SerializableCondition.GetSaveString` postfix，写法与 Action 版一致。

- **问题**：`SerializableCondition.GetSaveString(string bodyContent)` 存在（`Hacknet/SerializableCondition.cs:14`），但只 patch 了 `SerializableAction.GetSaveString`。
- **影响**：原版 condition 的 scope 只在读取方向和内存中存在，写存档时不带出去。
- **建议**：补 `SerializableCondition.GetSaveString` 的 postfix。
- **验收**：同 2。

---

## P1 — 功能缺口，两侧不对称

### [~] 4. Faction Action 无门控（代码已写，未编译）

> **笔记**：新增 `ClientPlugin/Scope/ScopeFactionPatches.cs`，PostLoad 里手动补丁 internal 类 `CustomFactionAction` 的 Deserialize（先幂等定位到 `<Action>` 再读 players/group）、Trigger（复用 ScopeGatePrefix）、GetSaveString。下面三个附带问题（一次性消费、只能正向 Flag、rank 共享）未处理。

- **问题**：`CustomFactionAction` 是 **`internal class`，不是 `SerializableAction` 子类**（`Hacknet.Factions/CustomFactionAction.cs`），触发路径是 `CustomFaction.CheckForAllCustomActionsToRun()` → `customFactionAction.Trigger(oS)`（`Hacknet.Factions/CustomFaction.cs:27-53`）。HNC 侧无任何 patch 触及。
- **影响**：`<Action ValueRequired="10">` 整组无法用 `players`/`group` 限定。组内单个 action（如 `<AddIRCMessage players="1"/>`）仍可用，因为 `CustomFactionAction.Deserialize` 内部走 `SerializableAction.Deserialize`。
- **建议**：单独 patch `CustomFactionAction.Trigger`，或 hook `CheckForAllCustomActionsToRun`。
- **附带的三个已知问题**（`ReadMe/08-Faction多人化.md`）：
  - `CustomActions.RemoveAt(i)` 是一次性消费
  - `FlagsRequiredForTrigger` 只支持正向检查，"Flag 不存在时触发"做不到
  - rank（`playerValue`）共享还是独立**文档标为未拍板**
- **验收**：Faction Action 带 scope，确认按玩家分流。

### [x] 5. 服务端 mission 门控是空壳（结论：原判断有误，已清理）

> **笔记**：`ServerPlugin/Scope/ScopeMissionPatches.cs:247` 已对 `ActiveMission.finish` 做 missionEnd/nextMission 门控；`ScopePatches.cs` 里的是重复占位，已删除。

- **问题**：`ServerPlugin/Scope/ScopePatches.cs:205-209` 的 `MissionFinishPrefix` 内容为 `return true; // 暂放行`，注释写"后续实现"。
- **对照**：客户端 `ClientPlugin/Scope/ScopeMissionPatches.cs` 已完整实现 `missionEnd`/`nextMission`。
- **建议**：把客户端的实现移植到服务端侧，或明确确认服务端只做中继、无需门控并删除空壳。

### [x] 6. 客户端缺 `RunnableConditionalActions` 门控（结论：无需补）

> **笔记**：`Update` 内部调用的 `Condition.Check` / `Action.Trigger` 均已被门控；不匹配的 Condition 返回 false，该集合在本客户端永不触发。服务端同名空壳补丁已删除。

- **问题**：服务端 patch 了 `RunnableConditionalActions.Update`（`ServerPlugin/Scope/ScopePatches.cs:192`），客户端没有。
- **建议**：确认 DLC ConditionalAction 执行循环是否需要客户端侧拦截；若需要则补齐，若不需要则同步注释说明。

### [~] 7. PF Goal 无门控（代码已写，未编译）

> **笔记**：`PatchExternalConditions` 已泛化为 `PatchExternalGates`，同时扫描 `MisisonGoal.isComplete(List<string>)`，覆盖 `PathfinderGoal` 子类。

- **问题**：gate 只枚举 `MisisonGoal` 子类；PF 的 `PathfinderGoal` 是另一个基类，同样在 PF 程序集内。
- **建议**：参照 P0-1 的处理方式。

---

## P2 — 新特性 / 依赖设计拍板

### [ ] 8. Faction rank（`playerValue`）状态同步

- **现状**：HNC 只有**事件检测**——`ServerClientEventsMod/Events/PlayerFactionEvent.cs` 定义 `Joined/Left/Changed/ValueAdded` 并广播，**没有 rank 状态同步逻辑**。检测不等于同步。
- **阻塞**：共享 rank vs 独立 rank 在 `ReadMe/09-待讨论.md` 标为"未拍板"。
- **建议**：先定模式，再实现。

### [ ] 9. `filter` 属性未实现

- **文档要求**：`ReadMe/03-模组兼容.md`、`07-isPublic设计.md` 设计的是 `isPublic` + `filter`（按 rank / flag / 连接状态筛选）。
- **实际实现**：用的是 **`players` + `group`**（`MoveToAttribute("players")`、`TryGetValue("group")`）。`isPublic` 在代码里只是 `ScopeInfo` 的计算属性（`Players == "PUBLIC"`），**不是 XML 属性**。
- **建议**：确认是否仍需要 `filter` 语义；需要则设计属性形态并与现有 `players`/`group` 统一。

### [ ] 10. 索引化占位符未实现

- **文档要求**：`ReadMe/10-玩家标识与占位符.md` 的 `#PLAYER_0#` / `#PLAYER_1#` / `#PLAYER_IP_0#` / `#PLAYER_IP_1#`（按服务端玩家 ID 索引）。
- **实际实现**：`#P1NAME#` / `#P1_IP#` / `#M-P1NAME#`（按角色名）。
- **建议**：确认是否需要"按玩家 ID 而非角色名"寻址；需要则在 `ComputerLoader.filter` 前加预处理层。

### [ ] 11. `#P1_M1_BRANCH#` 局内任务分支身份未实现

- **来源**：`ReadMe/11-联机同步实现计划.md`。

### [ ] 12. `ISyncableState` 扩展点未定义

- **文档要求**：`ReadMe/03-模组兼容.md` 给 KE CustomTrial / PhaseSwift 预留的第三方模组状态同步接口。
- **现状**：工作集内 grep 无定义。

### [ ] 13. Sequencer 未适配

- **文档要求**：`ReadMe/03-模组兼容.md` 明确"共享效果（删节点、崩溃）需广播"。
- **现状**：HNC 侧对 `SequencerExe` / `ExtensionSequencerExe` 零引用。

### [ ] 14. HackScript 内容层隔离

- **已覆盖**：`SALaunchHackScript : SerializableAction` 的 `Trigger(object)` 会被枚举，`Delay > 0` 的分支经 `DelayableActionSystem.AddAction` 回调的仍是同一个 `Trigger`，**门控生效**。
- **缺口**：`HackerScriptExecuter.runScript(Filepath, ...)` 执行的 `.hackscript` 文件内部指令序列不经过任何 scope 机制，无法按玩家区分。HNC 侧对 `HackerScriptExecuter` 的引用数为 0。

### [ ] 15. PF 其余子系统无多人适配

- PF 有 14 个子系统目录：`Action / Administrator / BaseGameFixes / Command / Daemon / Event / Executable / GUI / Meta / Mission / Options / Port / Replacements / Util`。
- HNC 只动了 `Action` 里的一个基类。`Executable`（自定义 exe）、`Daemon`、`Administrator`、`Command`、`Port` 在多玩家下的状态归属都没有适配。
- `ReadMe/01-架构方案.md` 只是把"正在跑的 Exe 实例"划给客户端本地，没有兑现代码。

---

## 附录 A — 配置层已发现缺陷（来自配置文件分析）

- [~] **`ClientProfiles.ini` 的 `ExtensionName` 写出丢失**（已修：`Save()` 现在写出 `ExtensionName`，未编译）：`ClientProfileStorage.Save()` 只拼 `ip`/`Port`/`WorldSeed`/`ChildSeedCounter`，读取时解析但写出遗漏 → 界面保存一次就丢。
- [ ] **`OnlineSaves` 双基准**：`ClientProfiles.ini` 基于 `Environment.CurrentDirectory`，`PlayerData.xml` 基于 `AppDomain.BaseDirectory`；正常游戏目录下相同，换工作目录启动会分裂。
- [ ] **`{ServerName}_Profile.xml` 形同虚设**：`ServerProfile.GetStoragePath` 无调用点，样例为空文件；要么实现要么移除约定。
- [ ] **`Port` 类型不一致**：`Storages.ini` 的 `ServerProfile.Port` 是 `string`，`ClientProfiles.ini` 的 `AvailableServer.Port` 是 `int`。
- [x] **`HacknetServerTerminal/Example/Storages.ini` 样例过期**（已删除两行过期配置）：含程序不再写出的 `ServerProfilesPath`/`ServerSavesPath` 行。
- [x] **`HacknetServerTerminal/Players/PlayerData.cs:75` 过时注释**（已改为 `Storage/Players/{serverName}.xml`）：写"存储在 `Storage/Players.xml`"，实际按服务器分文件 `Storage/Players/{serverName}.xml`。

## 附录 B — 其他已确认的不一致

- [~] **`PrivateMissionManager.TrySelect` 中的 PTR 更新循环为空**（已修：选定后把该 slot 的 Curr→Org 写入 `PlayerConfigLoader.CurrToOrgMappings`，由 `PlayerPtrFilterPatch` 在 filter 前替换；未编译，需实测。注意：只对本人 slot 生效，别人 slot 的占位符仍依赖 `PrivateMappings`）：`ClientPlugin/Missions/PrivateMissionManager.cs` 内 `myPtrs` 的 `foreach` 只有注释、没有实际写入映射 → 支线角色选定后 `#M-P1NAME#` 的动态解析可能未生效，需实测确认。
- [x] **`SeedSyncGlobal/README.md` 与实现不符**（已按 v2.1.0 重写）：README 描述的是"全局模式 + 临时种子 0"的旧逻辑，代码（v2.1.0）实际从桌面 `.seed` 文件读取种子。
- [ ] **属性体系文档与实现分叉**：设计文档统一用 `isPublic`/`filter`，实现统一用 `players`/`group`，需统一措辞以免误导扩展作者。

---

## 建议执行顺序

1. P0-1 / P0-2 / P0-3（PF Condition 门控与序列化、原版 Condition 序列化）—— 成本低、影响隐蔽、都是"静默不生效"
2. P1-4 / P1-5 / P1-6 / P1-7 —— 补齐功能面并消除两侧不对称
3. 附录 A / B 的条目 —— 多为小改动，可随手清理
4. P2 全部 —— 需先与设计文档拍板，属新特性而非补齐

## 验收提示

P0-1 与 P1-4 的表现都是"静默不生效"，静态分析容易漏掉运行时兜底。建议各写一个最小扩展 XML 实测确认，再关闭对应条目。

## 附录处理笔记（2026-11-23）

- **未处理**：`OnlineSaves` 双基准、`ServerProfile.GetStoragePath` 形同虚设、`Port` 类型不一致、属性体系文档措辞分叉。这几项涉及设计取舍（用哪个基准目录、是否删除 `_Profile.xml` 约定、Port 统一成哪种类型），需要先定方案。
- **根目录 `Players*.xml` 格式已修复**（结束标签改成 `</…>`，`#P1_IP` / `#M-P1_IP` 补上结尾 `#`；三个文件均用 XML 解析器验证通过，注释和缩进原样保留）。遗留：`PlayersCount num="2"` 旁的注释仍写"必须为3"，与数值不符。
- **行尾**：仓库里很多文件是 CRLF，git 会警告转换为 LF；本次改动没有刻意改变行尾。
