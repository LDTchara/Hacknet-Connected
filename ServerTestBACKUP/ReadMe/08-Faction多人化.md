# Faction 系统多人化问题

## 原版执行机制（CustomFaction.cs）

```
CheckForAllCustomActionsToRun()：
  for each CustomAction:
    if playerValue >= ValueRequiredForTrigger:
      if FlagsRequiredForTrigger 全部满足:
        CustomActions.RemoveAt(i)    ← 触发后从列表删除
        action.Trigger(os)           ← 执行 Action
```

关键：RemoveAt 一次消费，触发后永不再出现。

## 多人下的问题

独立 rank 模式下：
- 每个 OS 有各自的 CustomActions 列表
- 每人 rank 到了就各自触发一次、各自 RemoveAt
- IRC 消息发多次、任务加多遍

共享 rank 模式下：
- 所有人共用一个 CustomActions 列表
- 触发一次 RemoveAt 一次，不再出现 ✅
- IRC 不重复、Flag 不改两遍 ✅

## FlagsRequiredForTrigger 的限制

```csharp
if (!string.IsNullOrWhiteSpace(action.FlagsRequiredForTrigger))
{
    string[] array = action.FlagsRequiredForTrigger.Split(',');
    foreach (var flag in array)
        if (!os.Flags.HasFlag(flag))
            flag = false;  // 只检查 Flag 存在
}
```

- 只能正向检查（"Flag 存在时才触发"）
- 不支持否定（"Flag 不存在时触发"）
- 所以不能用内置 Flags 来实现"每人只触发一次"

## Faction Action 内部指令的归属

同一个 Faction Action 里的指令可能同时有全局副作用和私人副作用：

```xml
<Action ValueRequired="10">
    <AddIRCMessage .../>          <!-- 剧情对话，应该是全局一次 -->
    <AddConditionalActions .../>  <!-- 加新任务，应该是全局一次 -->
    <CopyAsset destComp="playerComp" .../>  <!-- 给自己电脑的文件 -->
</Action>
```

共享 rank 下：全部全局执行一次，CopyAsset 发给所有玩家。
独立 rank 下：每人触发一次，需要 isPublic 区分每条指令。

## Faction 与 ConditionalActions 的区别

| | Faction | ConditionalActions |
|---|---|---|
| 加载时机 | 游戏开始时 | 可随时加载 |
| 触发条件 | rank 值 | Condition（连接、Flag、瞬间等）|
| 一次消费 | 是（RemoveAt） | 否（条件满足就触发）|
| 多人行为 | 共享 rank 下只触发一次 | 取决于 isPublic |


## 当前决策

### rank 模型：共享（推荐，未拍板）

优缺点见 `06-Rank模式设计.md`。AC 看完旧扩展截图后倾向共享。

### isPublic 策略

Faction Action 的 isPublic **默认 false**，与 Condition/ConditionalActions/Mission 不同。
理由：Faction Action 包含大量 AddIRCMessage、AddAsset、AppendToFile 等副作用操作。
默认 false 意味着每人各自触发自己的 Faction Action，不同步给别人。

共享 rank + 默认 false 时：Faction Action 只触发一次，无重复问题。
独立 rank + 默认 false 时：每人各触发一次，IRC/文件会重复写入共享节点。

