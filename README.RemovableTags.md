# 可移除标签：卸载挂载中的条件 / 动作

> 状态：已写，**未编译、未在游戏里验证**。调研和取舍过程见 `RESEARCH.RemovableConditionalActions.md`。

## 用法

给**条件**或**动作**元素加 `RemoveableTag` 属性（多个标签用逗号分隔），再用一个动作按标签把它们移除：

```xml
<!-- 挂载一个条件集：条件不满足时一直挂着 -->
<OnConnect target="bankNode" RemoveableTag="f1">
    <AddIRCMessage ... />
</OnConnect>

<!-- 动作也可以打标签（例如一个带 Delay 的动作） -->
<AddConditionalActions Filepath="Actions/Later.xml" Delay="60" DelayHost="ircNode" RemoveableTag="f1,later" />

<!-- 在别处把它们卸载 -->
<RemoveConditionsOrActions RemoveTag="f1" />
```

`RemoveTag` 可以写多个（`f1,f2`），元素上有任意一个标签匹配就会被移除。比较时忽略大小写。

## 能移除什么

| 位置 | 做法 |
|---|---|
| **挂载中的条件集**（`os.ConditionalActions`，等条件满足的） | 条件上有标签 → 整个"条件 + 动作组"移除；动作上有标签 → 只移除这个动作 |
| **带 `Delay` 的待执行动作**（IRC / DHS 的 `runtime` 文件夹里的加密文件） | **原地换成空动作**，保留剩余时间和文件名；到点后空动作什么都不做，然后和别的一样被消耗掉 |
| **FastActionHost 的待执行动作**（内存对象） | 同样原地换成空动作 |

**替换时的延迟**：空动作**继承原条目剩余的延迟**；原条目没有可用的延迟（读不出来，或已经到点 ≤ 0）就设为 **0**（下一帧触发，什么都不做）。
空动作自己的 XML 不带 `Delay` / `DelayHost`，所以到点后直接执行，不会再被丢回队列里等第二次。

**为什么延时动作是"替换"而不是"删除"**：vanilla 的延时队列 `Update` 是"先触发动作，再 `RemoveAt(i)`"。
如果移除动作被触发时直接删别的条目，下标会错位，循环最后删掉的是**另一个无辜的动作**。原地替换不改变下标。

**为什么挂载列表是"延后删除"**：`RemoveConditionsOrActions` 多半就是某个条件集触发的，此时 `RunnableConditionalActions.Update` 正在遍历同一个列表，
直接删会漏检一项一帧。所以在 `Update` 执行期间只记下请求，`Update` 结束后（Harmony Postfix）再统一删。

## 标签会跟着存档走

标签在读档后不会丢：保存时 `GetSaveString` / `GetSaveElement` 会把 `RemoveableTag` 写回去（延时动作加密文件里的 XML 也包含它），
读档或延时动作到点反序列化时再读回来。做法和 Scope 的 `players` / `group` 完全一致，对原版和 Pathfinder 自定义的条件 / 动作都有效。

## 局限

- **不能移除同一组里的兄弟动作**：正在执行的这一组已经被 vanilla 从挂载列表里摘下来了，找不到它。
- 只覆盖 `os.ConditionalActions` 和上面两种延时队列；其他 mod 自己存的待执行动作不在范围内。
- 多人：这些都是**每个客户端各自的状态**，不在同步范围内（IRC / DHS 的 `runtime` 文件夹被同步层排除了）。
  动作在每个客户端都会执行，要只作用于某些玩家，请用 `players` / `group` 属性（Scope 会门控它）。
- 读不出标签的条目（解密失败、XML 格式错误）原样保留，不会被误删。
- `RemoveableTag` 与任务进度标记（`os.Flags`，`HasFlags` / `AddFlags` 用的）**不是同一个东西**。
- 如果 Pathfinder 自己的 `GetSaveString` 内部又调了 `GetSaveElement`，属性可能写出两遍；字符串版本里已经做了"已有则不再加"的保护。
  Scope 的 `players` / `group` 写回没有这个保护，如果出现重复属性，是同一个原因。
