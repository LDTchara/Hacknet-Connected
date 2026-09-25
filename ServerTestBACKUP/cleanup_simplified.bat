@echo off
echo ========================================
echo 清理简化版多余文件
echo ========================================

REM 删除临时 _simple 文件
del /Q "ServerPlugin_simple.cs" 2>nul
del /Q "UtilsRandomPatch_simple.cs" 2>nul
del /Q "ClientPlugin\ClientPluginMain_simple.cs" 2>nul
del /Q "ClientPlugin\UtilsRandomPatch_simple.cs" 2>nul
del /Q "SeedSyncGlobal\SeedSyncGlobalPlugin_simple.cs" 2>nul

REM 删除 ServerPlugin 不需要的复杂文件
del /Q "WorldSeedSync.cs" 2>nul
del /Q "ServerManager.cs" 2>nul
del /Q "EventForwarder.cs" 2>nul
del /Q "RandomSeedHelper.cs" 2>nul

REM 删除 ClientPlugin 不需要的复杂文件
del /Q "ClientPlugin\WorldSeedSync.cs" 2>nul
del /Q "ClientPlugin\ClientNetworkManager.cs" 2>nul
del /Q "ClientPlugin\EventForwarderClient.cs" 2>nul
del /Q "ClientPlugin\EventReceiver.cs" 2>nul
del /Q "ClientPlugin\RandomSeedHelper.cs" 2>nul
del /Q "ClientPlugin\GameLauncher.cs" 2>nul
del /Q "ClientPlugin\Patches.cs" 2>nul

REM 删除 ClientPlugin 中的 Listing 目录（网络相关）
if exist "ClientPlugin\Listing" (
    rmdir /S /Q "ClientPlugin\Listing"
    echo Deleted ClientPlugin\Listing directory
)

echo ========================================
echo 清理完成！
echo ========================================
echo.
echo 接下来请手动：
echo 1. 编辑 ServerPlugin.csproj，移除 LiteNetLib 包引用
echo 2. 编译项目：dotnet build ServerPlugin.csproj
echo 3. 编译 ClientPlugin：cd ClientPlugin ^&^& dotnet build
echo 4. 编译 SeedSyncGlobal：cd ..\SeedSyncGlobal ^&^& dotnet build
echo.
pause
