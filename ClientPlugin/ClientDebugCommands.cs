// 本文件已废弃（2026-08-01）：
//
// loadactions / loadmissions 测试命令已移除——
// ClientPlugin 与 ServerPlugin 各自注册同名命令会触发
// CommandManager 的 "Command loadactions has already been registered"，
// 导致插件加载失败。如后续需要，请改为单一插件注册或使用互不冲突的命令名。
//
// 相关代码已从 ClientPluginMain.Load() 中移除（Register 调用）。
