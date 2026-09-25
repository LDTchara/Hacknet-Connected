# isPublic 设计

## 定位

`isPublic` 是跟 `Delay`、`DelayHost` 同级的可选属性。部分元素支持、部分不支持、不填走默认值。

## 放哪里——两层都支持

Condition 层和 Action 层都可以加，继承 + 覆盖逻辑跟 `Delay` 一样：

```xml
<OnConnect target="targetComp" isPublic="true">          ← Condition 层默认
    <AddIRCMessage .../>                                  ← 继承：public
    <CopyAsset isPublic="false" .../>                     ← 覆盖：私人
    <RunFunction isPublic="true" .../>                    ← 覆盖：public
</OnConnect>
```

| Condition isPublic | Action isPublic | 结果 |
|-------------------|----------------|------|
| 不填 | 不填 | 走 Action 类型的默认值 |
| true | 不填 | 继承 |
| true | false | Action 覆盖 |
| false | 不填 | 继承 |
| false | true | Action 覆盖 |

## 支持 isPublic 的层级

| 层级 | 说明 |
|------|------|
| Condition（Instantly/OnConnect等） | 设定内部 Action 的默认值 |
| Action（AddIRCMessage等） | 覆盖 Condition 的默认值 |
| Faction Action | 本身没有 Condition，直接在 Action 上声明 |
| Mission | 任务级别的全局/私人 |

## 默认值策略

按类型区别对待，不搞"一个默认值走天下"：

| 类型 | 默认值 | 理由 |
|------|--------|------|
| Faction Action | false | 每人各触发各的，不广播全局副作用 |
| ConditionalActions | true | 主动加载的通常是全局剧情推进 |
| Mission | true | 任务完成状态需要所有玩家看到 |
| AddIRCMessage | false | 剧情对话各看各的，不刷屏 |
| AddAsset / AppendToFile | false | 文件操作默认只影响触发者 |
| CopyAsset | false | 同上 |
| RunFunction:addFlags | true | Flag 默认全局共享 |
| RunFunction:addRank | true | 共享 rank 下一人加全涨 |
| ShowNode / HideNode | true | 节点可见性全局 |
| CrashComputer | true | 节点崩溃全局 |
| KillExe | false | 杀进程纯个人 |
| SetLock / ScreenBleed | false | UI 效果各人各管 |
| SaveGame | false | 存档各存各的 |

## 旧扩展兼容

- Faction Action 默认 false → 每人各自触发，不互相干扰 ✅
- 不涉及 isPublic 的旧扩展：IRC 各看各的，不刷屏 ✅
- 涉及全局副作用的旧扩展：有些效果（加任务到任务板）会每人加一次 → 重复但不报错 ✅
- 要在旧扩展上做到完美体验：扩展作者补 isPublic 属性

## 对 KE 的意义

KE 自定义 Action 继承 `DelayablePathfinderAction`，AC 的联机模组只需在 Action 基类加一个 isPublic 属性字段，KE 不需要改代码，只需要在 XML 里写属性。跟现在写 `Delay` 一样简单。

KE 各 Action 的推荐 isPublic：

| KE Action | isPublic | 理由 |
|-----------|----------|------|
| PhaseSwiftInit | true | 启动场景系统，全局 |
| PhaseSwiftScene | true | 切换场景，所有人看到同一个 |
| PhaseSwiftMusic | false | 切歌是自己的事 |
| PhaseSwiftStop | true | 停止场景系统，全局 |
| SetNodeIcon | true | 改节点图标，全部人看到 |
| TerminalWrite | false | 写终端是自己的事 |
| TerminalType | false | 打字效果也是自己的 |
| PlaySound | false | 播放音效是自己的 |
| LaunchVMAttack | true | VM 攻击是全局事件 |
