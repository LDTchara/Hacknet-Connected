# SeedSyncGlobal - 全局种子同步插件

## 📝 说明

这是一个全局 BepInEx 插件，用于在游戏启动时就初始化 `Utils.random` 为确定性种子。

## 🎯 为什么需要全局插件？

`Utils.random` 是一个 static 字段，在 `Utils` 类加载时就被初始化了。如果 ClientPlugin/ServerPlugin 作为扩展加载，它们在 `Utils` 类已经初始化后才生效，无法拦截 `Utils.random` 的创建。

全局插件在游戏启动时就加载，早于任何游戏代码，可以成功拦截 `Utils.random`。

## 🔄 全局/局部切换逻辑

### 全局模式（默认）
- 安装在 `Hacknet/BepInEx/plugins/SeedSyncGlobal.dll`
- 只初始化 `Utils.random` 种子（临时种子 0）
- 不启用其他功能（UI、网络等）
- 等待扩展内的 ClientPlugin/ServerPlugin 接管

### 局部模式（自动切换）
- 当进入包含 ClientPlugin/ServerPlugin 的扩展时
- 全局插件检测到局部插件
- 自动 Unload 自己
- 局部插件接管所有功能

## 📦 安装

### 编译
```bash
cd SeedSyncGlobal
dotnet build
```

### 部署
将编译后的 `SeedSyncGlobal.dll` 复制到：
```
Hacknet/BepInEx/plugins/SeedSyncGlobal.dll
```

**不要**将其放在扩展目录中！

## ✅ 验证

启动游戏后，日志应该显示：
```
[SeedSyncGlobal] ═══════════════════════════════════════════
[SeedSyncGlobal] Loading in GLOBAL mode
[SeedSyncGlobal] ═══════════════════════════════════════════
[SeedSyncGlobal] Patched Utils.random initialization
[SeedSyncGlobal] ═══════════════════════════════════════════
[SeedSyncGlobal] Utils.random re-seeded with temporary seed: 0
[SeedSyncGlobal] Ensures deterministic behavior until real seed arrives
[SeedSyncGlobal] ═══════════════════════════════════════════
```

进入扩展后，应该显示：
```
[SeedSyncGlobal] ═══════════════════════════════════════════
[SeedSyncGlobal] Detected local ClientPlugin/ServerPlugin
[SeedSyncGlobal] Switching to local mode, unloading global plugin
[SeedSyncGlobal] ═══════════════════════════════════════════
```

## 🔧 工作原理

1. **游戏启动** → BepInEx 加载 SeedSyncGlobal
2. **检查安装位置** → 确认是全局安装
3. **Patch Utils 静态构造函数** → 等待 Utils 类加载
4. **Utils 类加载** → Utils.random = new Random()
5. **Patch 立即执行** → Utils.random.ReSeed(0)
6. **进入扩展** → ExtensionLoader.LoadExtension()
7. **检测局部插件** → 发现 ClientPlugin/ServerPlugin
8. **Unload 全局插件** → 局部插件接管

## 🎮 与 ClientPlugin/ServerPlugin 的关系

| 功能 | SeedSyncGlobal | ClientPlugin/ServerPlugin |
|------|----------------|---------------------------|
| Utils.random 初始化 | ✅ (临时种子 0) | ✅ (真正种子) |
| 网络通信 | ❌ | ✅ |
| UI 界面 | ❌ | ✅ |
| 种子同步 | ❌ | ✅ |
| 配置管理 | ❌ | ✅ |

SeedSyncGlobal 只负责最早期的 `Utils.random` 初始化，其他所有功能由 ClientPlugin/ServerPlugin 处理。

## 📋 依赖

- BepInEx.Core
- BepInEx.Hacknet
- HacknetPathfinder
- 0Harmony
- RandomSeedHelper.cs（来自 ClientPlugin）

## ⚠️ 注意事项

1. **必须全局安装**：不要放在扩展目录中
2. **与局部插件协同**：需要同时安装 ClientPlugin/ServerPlugin
3. **版本兼容**：确保与 ClientPlugin/ServerPlugin 版本一致

## 🐛 故障排查

### 问题：没有看到 `[SeedSyncGlobal]` 日志
**原因**：插件没有被加载
**解决**：
- 检查 DLL 是否在 `Hacknet/BepInEx/plugins/` 目录
- 检查 BepInEx 日志中是否有加载错误

### 问题：显示 "Loading in LOCAL mode"
**原因**：插件被放在了扩展目录中
**解决**：移动到全局 plugins 目录

### 问题：进入扩展后没有 Unload
**原因**：没有检测到局部 ClientPlugin/ServerPlugin
**解决**：
- 确认扩展中有 ClientPlugin.dll 或 ServerPlugin.dll
- 检查 GUID 是否匹配（`com.ClientPlugin.AP` 或 `com.ServerPlugin.Tesr`）
