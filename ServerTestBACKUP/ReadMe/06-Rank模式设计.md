# Rank 模式设计

## 两种模式

### 共享 Rank

所有玩家共享同一个 Faction rank（`playerVal`）。

```
Alice 做任务 → rank +5 → Bob 也能看到 rank 涨了
Bob 做任务 → rank +10 → 同一份 rank，Faction Action 只触发一次
```

### 独立 Rank

每人各算各的 rank。

```
Alice rank=15 → 触发 ValueRequired=10 的 Action
Bob rank=2 → 还没触发
两人进度独立
```

## 对比

| 维度 | 共享 Rank | 独立 Rank |
|------|----------|----------|
| Faction Action 触发次数 | 1 次（谁先到谁触发） | 每人各触发一次 |
| AddIRCMessage | 不重复 ✅ | 重复（需要 isPublic 过滤）❌ |
| IRC 文件同步 | 无冲突 ✅ | 重复写入，需要去重 |
| 多人化扩展 | 作者不需要管多人逻辑 ✅ | 作者要写 filter/isPublic |
| 单机扩展兼容 | 完美，不改任何逻辑 ✅ | 需要加 isPublic 属性 |
| 旧存档直接联机 | rank 直接复用 ✅ | rank 只记一个人的 |
| 自由度（多支线） | 受限制，所有人走一条线 | 各走各的 |
| 分支任务 | 投票/房主选 | 各接各的，互不干扰 |
| 开发复杂度 | 简单（Faction Action 不加处理也能用） | 复杂（每人状态要独立存） |
| 扩展作者工作量 | 零 | 每条 Faction Action 要考虑是否 public |
| Faction 触发顺序 | 不用管，只触发一次 | 多人同时到阈值→冲突 |
| 掉线/重连恢复 | 简单，同步 rank 即可 | 复杂（要存每人各的 rank） |

## LDT 建议：共享 Rank

除了"自由度"这一个叉之外，其他全是优势。自由度问题可以通过其他手段解决：
- 分支任务通过 Flag 而不是 rank 实现
- 房主选任务/投票机制
- 限制"选线"场景（类似元气骑士 Buff 房主选）

## AC 2026-06-22 回复

看了旧扩展截图后承认旧扩展大量依赖 Faction Action 触发剧情。
如果独立 rank，同样的 IRC 消息、文件操作会触发 N 次。
虽然 isPublic 可以过滤，但旧扩展不写 isPublic = 必须加兼容处理。
共享 rank 在兼容性和开发成本上占优。

目前未拍板，等 AC 进一步讨论。

## 两种模式对应的多人体验

| 模式 | 更像 |
|------|------|
| 共享 Rank | 合作通关（主线一致，剧情共享）|
| 独立 Rank | 开放世界（各玩各的，各自发展）|

## 共享模式下做分支

不依赖 rank 做分支，改用 Flag：

```xml
<Action ValueRequired="5">   ←共享 rank 到这个值触发
    <RunFunction function="addFlags" args="BRANCH_POINT_A" isPublic="true" />
</Action>

<ConditionalActions>
    <ConditionalAction flags="BRANCH_POINT_A">
        <RunFunction function="addFlags" args="PLAYER_CHOSE_PATH_B" />
        <RunFunction function="addRank" args="30" />
    </ConditionalAction>
</ConditionalActions>
```

共享 rank → 所有人都到 5 → 触发分支点 → 选哪条线？
→ 房主选 或 投票。Flag 又是共享的，选了之后所有人都走同一条线。
不完美，但可行。
