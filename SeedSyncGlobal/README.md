# SeedSyncGlobal - 全局固定种子插件（v2.1.0）

全局 BepInEx 插件：在游戏启动时就把 `Utils.random` 重置为确定性种子，让多个 Hacknet 实例生成相同的世界。

## 为什么必须是全局插件

`Utils.random` 是 static 字段，在 `Utils` 类加载时就被初始化。作为扩展加载的 ClientPlugin / ServerPlugin 生效太晚，拦截不到这次初始化。全局插件早于任何游戏代码加载。

## 种子从哪来

1. 启动时扫描**桌面**（`Environment.SpecialFolder.Desktop`）下的 `*.seed` 文件，取第一个。
2. **文件名（不含扩展名）就是种子**，必须是整数。例如 `42.seed` → 种子 42。
3. 没找到文件、文件名不是整数或读取失败时，使用默认种子 `114514`，并在日志里打印 `ERROR` / `Using default seed`。

> 注意：此版本**不再**使用旧版的"临时种子 0 + 进入扩展后自动 Unload"逻辑。
> 全局模式没有自动卸载；真实世界种子由服务端通过 ClientPlugin / ServerPlugin 的 WorldSeedSync 另行同步。

## 它做了什么

- `Load()`：读取 `.seed` → `PatchAll()` → 对 `Utils.random` 做 `ForceReSeed(FixedSeed)`（反射写 `SeedArray` / `inext` / `inextp`）。
- 维护 `GlobalIpCounter`，用于对齐 `NetworkMap.generateRandomIP()`：两端同种子 + 同计数器 → 同 IP。
- `Unload()`：撤销自己的 Harmony 补丁。

## 安装

```bash
cd SeedSyncGlobal
dotnet build
```

把 `SeedSyncGlobal.dll` 复制到 `Hacknet/BepInEx/plugins/`。**不要**放进扩展目录。

## 验证

日志应出现（路径与种子视实际情况而定）：

```
[SeedSyncGlobal] ✓ Loaded seed from: 42.seed -> 42
[SeedSyncGlobal] Loaded - Fixed Seed = 42
```

如果出现 `No .seed file found on Desktop`，说明桌面没有 `.seed` 文件，正在使用默认种子。

## 注意

- 同一个多人局里，所有客户端和服务端的桌面 `.seed` 文件名必须一致，否则世界不同。
- 桌面上有多个 `.seed` 文件时只取 `Directory.GetFiles` 返回的第一个，顺序不保证，请只保留一个。
