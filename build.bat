@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"

rem ==========================================================================
rem  build.bat - build the Hacknet-Connected plugins and export them
rem              into separate Server / Client folders.
rem
rem  Usage:   build.bat [mode] [target]
rem
rem    mode    single   (default) merge everything into ONE dll per plugin
rem            normal   the regular multi-dll build
rem    target  all      (default) server + client + terminal
rem            server   ServerPlugin (+ SeedSyncGlobal) only
rem            client   ClientPlugin (+ SeedSyncGlobal) only
rem            terminal HacknetServerTerminal only
rem
rem  Examples:
rem    build.bat                  single-dll server + client, plus the terminal
rem    build.bat single server    only the single-dll ServerPlugin
rem    build.bat normal           regular multi-dll build of everything
rem
rem  Export layout (ready to copy into the game):
rem    Output\Server\Plugins\          -> Extensions\<ext>\Plugins   (server game)
rem    Output\Server\BepInEx\plugins\  -> BepInEx\plugins            (server game)
rem    Output\Server\Terminal\         -> HacknetServerTerminal (relay program)
rem    Output\Client\Plugins\          -> Extensions\<ext>\Plugins   (client game)
rem    Output\Client\BepInEx\plugins\  -> BepInEx\plugins            (client game)
rem
rem  Raw build output stays in Output\ServerPlugin(.Single)\, Output\ClientPlugin(.Single)\,
rem  Output\SeedSyncGlobal\ and Output\Terminal\.
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

set "DO_SERVER="
set "DO_CLIENT="
set "DO_TERMINAL="
if /i "%TARGET%"=="all"      set "DO_SERVER=1"
if /i "%TARGET%"=="all"      set "DO_CLIENT=1"
if /i "%TARGET%"=="all"      set "DO_TERMINAL=1"
if /i "%TARGET%"=="server"   set "DO_SERVER=1"
if /i "%TARGET%"=="client"   set "DO_CLIENT=1"
if /i "%TARGET%"=="terminal" set "DO_TERMINAL=1"

echo Mode: %MODE%    Target: %TARGET%

set "FAILED="

if defined DO_SERVER   call :build "ServerPlugin\ServerPlugin.csproj" "%EXTRA%"
if defined DO_CLIENT   call :build "ClientPlugin\ClientPlugin.csproj" "%EXTRA%"
rem SeedSyncGlobal is a global BepInEx plugin both games need; it has no single-dll mode
set "DO_SEED="
if defined DO_SERVER set "DO_SEED=1"
if defined DO_CLIENT set "DO_SEED=1"
if defined DO_SEED     call :build "SeedSyncGlobal\SeedSyncGlobal.csproj" ""
rem the terminal is not a game plugin and has no single-dll mode
if defined DO_TERMINAL call :build "HacknetServerTerminal\HacknetServerTerminal.csproj" ""

echo.
if defined FAILED (
    echo ===== BUILD FAILED =====
    exit /b 1
)
echo ===== BUILD OK =====

rem ---------------------------------------------------------------- export
echo.
echo ===== Exporting =====
set "EXPORT_FAILED="
if defined DO_SERVER   call :export_side Server ServerPlugin
if defined DO_CLIENT   call :export_side Client ClientPlugin
if defined DO_TERMINAL call :export_terminal

echo.
if defined EXPORT_FAILED (
    echo ===== EXPORT FAILED =====
    exit /b 1
)
echo ===== EXPORT OK =====
echo.
if defined DO_SERVER (
    echo Output\Server\Plugins\:
    dir /b "Output\Server\Plugins"
    echo Output\Server\BepInEx\plugins\:
    dir /b "Output\Server\BepInEx\plugins"
)
if defined DO_TERMINAL echo Output\Server\Terminal\  [HacknetServerTerminal]
if defined DO_CLIENT (
    echo Output\Client\Plugins\:
    dir /b "Output\Client\Plugins"
    echo Output\Client\BepInEx\plugins\:
    dir /b "Output\Client\BepInEx\plugins"
)
echo.
echo Install notes:
echo   - Copy ^<Side^>\Plugins\* into Extensions\^<ext^>\Plugins of that game.
echo   - Copy ^<Side^>\BepInEx\plugins\SeedSyncGlobal.dll into BepInEx\plugins (NOT the extension).
if /i "%MODE%"=="single" (
    echo   - Single-dll: delete ServerClientEventsMod.dll and every HNC.*.dll from the
    echo     game's Plugins folder first, otherwise the same plugin GUID is loaded twice.
)
exit /b 0

rem ======================================================================
:build
echo.
echo ===== dotnet build %~1 %~2 =====
dotnet build "%~1" %~2 --nologo -v:minimal -m:1
if errorlevel 1 (
    echo [FAILED] %~1
    set "FAILED=1"
)
exit /b 0

rem ----------------------------------------------------------------------
rem  :export_side <Server|Client> <ServerPlugin|ClientPlugin>
:export_side
set "SIDE=%~1"
set "PLUGIN=%~2"
set "DST=Output\%SIDE%\Plugins"
set "DSTG=Output\%SIDE%\BepInEx\plugins"
if exist "%DST%"  rmdir /s /q "%DST%"
if exist "%DSTG%" rmdir /s /q "%DSTG%"
mkdir "%DST%"
mkdir "%DSTG%"

if /i "%MODE%"=="single" (
    call :copy1 "Output\%PLUGIN%.Single\%PLUGIN%.dll" "%DST%"
) else (
    call :copy1 "Output\%PLUGIN%\%PLUGIN%.dll"               "%DST%"
    rem Costura embeds ServerClientEventsMod in each plugin.
    call :copy1 "Output\%PLUGIN%\HNC.Sync.Abstractions.dll"   "%DST%"
    call :copy1 "Output\%PLUGIN%\HNC.Sync.Core.dll"           "%DST%"
    call :copy1 "Output\%PLUGIN%\HNC.Sync.Adapters.dll"       "%DST%"
    call :copy1 "Output\%PLUGIN%\HNC.Chat.Core.dll"           "%DST%"
)
call :copy1 "Output\SeedSyncGlobal\SeedSyncGlobal.dll" "%DSTG%"
exit /b 0

rem ----------------------------------------------------------------------
:export_terminal
set "DST=Output\Server\Terminal"
if exist "%DST%" rmdir /s /q "%DST%"
mkdir "%DST%"
xcopy "Output\Terminal\*" "%DST%\" /e /i /y /q >nul
if errorlevel 1 (
    echo [EXPORT FAILED] Output\Terminal
    set "EXPORT_FAILED=1"
)
exit /b 0

rem ----------------------------------------------------------------------
rem  :copy1 <file> <dir>
:copy1
if not exist "%~1" (
    echo [MISSING] %~1
    set "EXPORT_FAILED=1"
    exit /b 0
)
copy /y "%~1" "%~2\" >nul
if errorlevel 1 (
    echo [EXPORT FAILED] %~1
    set "EXPORT_FAILED=1"
)
exit /b 0
