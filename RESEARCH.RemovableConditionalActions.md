# 研究：用标签把"挂载中"的 Action / Condition 卸载

> 状态：**调研结论已落地为实现，见 `README.RemovableTags.md`。** 下面保留调研过程。你的决定：条件和动作都支持；属性名用 `RemoveableTag`；不做 `RemoveWhenFlag`（等于没加载）；带 `Delay` 的待执行动作要做——用"原地换成空动作"，不删条目。 调研对象是 Hacknet 5.069 源码。
> 需求：`<Instantly RemovableFlag="f1"/>` 这样给条件（或动作）打标签，再用 `<RemoveConditionsOrActions RemoveFlag="f1"/>` 把它们从"挂载状态"移除。

## 1. "挂载状态"在原版里是什么

`OS.ConditionalActions`（`RunnableConditionalActions`）里有一个 `List<SerializableConditionalActionSet> Actions`。每个元素 = **一个条件 + 一组动作**：

```
SerializableConditionalActionSet { SerializableCondition Condition; List<SerializableAction> Actions; }
```

- **挂载**：`SAAddConditionalActions` → `RunnableConditionalActions.LoadIntoOS(文件)` 把 XML 里的条件集反序列化后追加进这个列表。
- **每帧**：`OS.Update` → `ConditionalActions.Update`：逐个 `Condition.Check`，为真就**先把这一项从列表移除（`RemoveAt(i); i--`）**，再依次触发它的动作。所以每个条件集只会触发一次。
- 条件不满足的就一直留在列表里——这就是你说的"处于挂载状态"。
- **存档**：`OS` 保存时调用 `ConditionalActions.GetSaveString()`，读档时从存档的 `<ConditionalActions>` 反序列化回来，所以挂载状态会跨存档保留。

另一个"挂载"：带 `Delay` 的动作会进 `DelayableActionSystem`（电脑 `runtime` 文件夹里的待执行动作文件）。它和上面是两套东西。

## 2. 方案

### 2.1 给条件 / 动作打标签（沿用已有的 Scope 做法）

我们已经用同样的办法给 Action / Condition 加了 `players` / `group` 属性，机制完全可以复用：

| 步骤 | 做法 | 已有的参照 |
|---|---|---|
| 读标签 | 在 `SerializableCondition.Deserialize` / `SerializableAction.Deserialize` 的 Prefix 里读 `RemovableFlag` 属性；PF 的条件 / 动作走 `ActionsLoader.ReadCondition` / `ReadAction` 的 Postfix 从 `ElementInfo` 读 | `ScopePatches` 里的 `ConditionDeserializePrefix` / `ReadConditionPostfix` |
| 存标签 | 存放在 `ConditionalWeakTable<object, string>`（对象 → 标签），不会让对象无法回收 | `ScopeManager._meta` |
| 写回存档 | `SerializableCondition.GetSaveString` / `SerializableAction.GetSaveString` 的 Postfix 把属性写回去，否则**读档后标签丢失** | P0-3 刚补的 `ConditionGetSaveStringPostfix` |

标签**打在条件元素上**（你例子里的 `<Instantly RemovableFlag="f1"/>`）就是标记整个条件集；
打在动作元素上则是标记集合里的那个动作。两种都可以做，建议先做条件集（语义最清楚）。

### 2.2 移除动作

```xml
<RemoveConditionsOrActions RemoveFlag="f1" />
```

继承 `DelayablePathfinderAction`，`Trigger` 里：

```csharp
os.ConditionalActions.Actions.RemoveAll(set => HasTag(set.Condition, "f1"));        // 整个条件集
foreach (var set in os.ConditionalActions.Actions)
    set.Actions.RemoveAll(a => HasTag(a, "f1"));                                     // 集合里被标记的动作
```

支持逗号分隔多个标签（`RemoveFlag="f1,f2"`）。

### 2.3 必须处理的坑

1. **在 `Update` 循环里删除**：这个动作本身通常就是某个条件集触发的，此时 `RunnableConditionalActions.Update` 正在遍历 `Actions`。
   直接 `RemoveAll` 不会越界（循环每次都重新读 `Count`），但会让下标错位、**漏检一项一帧**。
   原版有个 `IsUpdating` 标志（`LoadIntoOS` 用它延后追加）。稳妥做法：`IsUpdating` 为真时先把标签放进待删队列，
   在 `RunnableConditionalActions.Update` 的 Postfix 里统一删除。
2. **标签字符串和 `os.Flags` 不是一回事**。`os.Flags` 是任务进度标记（`HasFlags` / `AddFlags` 用的）。
   把这里的属性也叫 `...Flag` 容易让作者混淆；建议属性名用 `Tag`（`RemovableTag` / `RemoveTag`），或者文档里讲清楚。
3. **嵌套条件集**：条件集的动作里如果又有 `AddConditionalActions`，新挂载的集合会在"加载时"才反序列化并带上它们自己的标签，互不影响。
4. **多人**：`ConditionalActions` 是每个客户端各自的状态，不在同步范围内。动作在每个客户端都会执行，
   要只作用于某些玩家，沿用 `players` / `group` 属性（Scope 系统已经会门控它）。
5. **带 `Delay` 的待执行动作**（`DelayableActionSystem`）是序列化成 `runtime` 文件夹里的文件，要按标签移除得解析这些文件。
   **建议这一期不做**，先只覆盖 `os.ConditionalActions`。

## 3. 另一种更简单的方案（供你取舍）

不加新动作，给条件集加一个 `RemoveWhenFlag="f1"` 属性：每帧（或在 `Update` 的 Postfix 里）检查 `os.Flags.HasFlag("f1")`，
为真就把这一项从列表里卸载。好处：不需要在 `Update` 循环里删除的特殊处理，也直接复用任务脚本已有的 `AddFlags` 动作。
坏处：不能在任意时刻"精确地"卸载，只能靠进度标记。

## 4. 工作量与风险

- 读标签 + 存标签 + 写回存档：和 Scope 的实现同一个模式，约 100 行，风险低（因为已经验证过 Scope 那一套在编译层面可行）。
- 移除动作 + `Update` 后缀的延迟删除：约 80 行。
- **风险**：`GetSaveString` 的 Postfix 靠"在第一个空格后插入属性"实现，对少数自定义序列化的动作可能不适用（Scope 已有同样的限制）；
  `RunnableConditionalActions` 的私有字段 `IsUpdating` 需要用反射或 `AccessTools` 读取。

## 5. 需要你决定的

1. 标签打在**条件**上、**动作**上，还是两者都支持？（建议先做条件。）
2. 属性名：`RemovableFlag` / `RemoveFlag`（你的写法）还是改成 `Tag`，避免和 `os.Flags` 混淆？
3. 要不要同时支持上面第 3 节的 `RemoveWhenFlag` 简单方案？
4. 带 `Delay` 的待执行动作要不要纳入？（建议先不做。）
