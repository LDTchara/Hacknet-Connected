@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"

rem ==========================================================================
rem  build.bat - build the Hacknet-Connected plugins.
rem
rem  Usage:   build.bat [mode] [target]
rem
rem    mode    single   (default) merge everything into ONE dll per plugin
rem            normal   the regular multi-dll build
rem    target  all      (default) server + client + terminal
rem            server   ServerPlugin only
rem            client   ClientPlugin only
rem            terminal HacknetServerTerminal only
rem
rem  Examples:
rem    build.bat                  single-dll server + client, plus the terminal
rem    build.bat single server    only the single-dll ServerPlugin
rem    build.bat normal           regular multi-dll build of everything
rem
rem  Output (single):  Output\ServerPlugin.Single\ServerPlugin.dll
rem                    Output\ClientPlugin.Single\ClientPlugin.dll
rem  Output (normal):  Output\ServerPlugin\   Output\ClientPlugin\
rem  Output (both):    Output\Terminal\
rem ==========================================================================

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] dotnet was not found on PATH. Install the .NET SDK first.
    exit /b 1
)

set "MODE=%~1"
if "%MODE%"=="" set "MODE=single"
set "TARGET=%~2"
if "%TARGET%"=="" set "TARGET=all"

if /i not "%MODE%"=="single" if /i not "%MODE%"=="normal" (
    echo [ERROR] Unknown mode "%MODE%". Use: single ^| normal
    exit /b 1
)
if /i not "%TARGET%"=="all" if /i not "%TARGET%"=="server" if /i not "%TARGET%"=="client" if /i not "%TARGET%"=="terminal" (
    echo [ERROR] Unknown target "%TARGET%". Use: all ^| server ^| client ^| terminal
    exit /b 1
)

set "EXTRA="
if /i "%MODE%"=="single" set "EXTRA=-p:SingleDll=true"

echo Mode: %MODE%    Target: %TARGET%

set "FAILED="

if /i "%TARGET%"=="all"      call :build "ServerPlugin\ServerPlugin.csproj" "%EXTRA%"
if /i "%TARGET%"=="server"   call :build "ServerPlugin\ServerPlugin.csproj" "%EXTRA%"
if /i "%TARGET%"=="all"      call :build "ClientPlugin\ClientPlugin.csproj" "%EXTRA%"
if /i "%TARGET%"=="client"   call :build "ClientPlugin\ClientPlugin.csproj" "%EXTRA%"
rem the terminal is not a game plugin and has no single-dll mode
if /i "%TARGET%"=="all"      call :build "HacknetServerTerminal\HacknetServerTerminal.csproj" ""
if /i "%TARGET%"=="terminal" call :build "HacknetServerTerminal\HacknetServerTerminal.csproj" ""

echo.
if defined FAILED (
    echo ===== BUILD FAILED =====
    exit /b 1
)

echo ===== BUILD OK =====
echo.
if /i "%MODE%"=="single" (
    if /i "%TARGET%"=="all"    call :show "Output\ServerPlugin.Single\ServerPlugin.dll"
    if /i "%TARGET%"=="server" call :show "Output\ServerPlugin.Single\ServerPlugin.dll"
    if /i "%TARGET%"=="all"    call :show "Output\ClientPlugin.Single\ClientPlugin.dll"
    if /i "%TARGET%"=="client" call :show "Output\ClientPlugin.Single\ClientPlugin.dll"
    echo.
    echo Single-dll install notes:
    echo   - Put ONLY that dll in Extensions\^<ext^>\Plugins.
    echo   - Delete ServerClientEventsMod.dll and every HNC.*.dll from Plugins first,
    echo     otherwise the same plugin GUID is loaded twice.
    echo   - SeedSyncGlobal.dll still goes in BepInEx\plugins ^(it is not part of this^).
) else (
    if /i "%TARGET%"=="all"    call :show "Output\ServerPlugin\ServerPlugin.dll"
    if /i "%TARGET%"=="server" call :show "Output\ServerPlugin\ServerPlugin.dll"
    if /i "%TARGET%"=="all"    call :show "Output\ClientPlugin\ClientPlugin.dll"
    if /i "%TARGET%"=="client" call :show "Output\ClientPlugin\ClientPlugin.dll"
    echo.
    echo Multi-dll install notes: Plugins also needs ServerClientEventsMod.dll and
    echo HNC.Sync.Abstractions / Core / Adapters .dll and HNC.Chat.Core.dll next to the plugin.
)
if /i "%TARGET%"=="all"      call :show "Output\Terminal\HacknetServerTerminal.dll"
if /i "%TARGET%"=="terminal" call :show "Output\Terminal\HacknetServerTerminal.dll"
exit /b 0

:build
echo.
echo ===== dotnet build %~1 %~2 =====
dotnet build "%~1" %~2 --nologo -v:minimal -m:1
if errorlevel 1 (
    echo [FAILED] %~1
    set "FAILED=1"
)
exit /b 0

:show
if exist "%~1" (
    for %%F in ("%~1") do echo   %%~fF   [%%~zF bytes]
) else (
    echo   [missing] %~1
)
exit /b 0
