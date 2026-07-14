@echo off
set ROOT=%~dp0
set OUTPUT=%ROOT%Output

echo ServerTest Build All
echo.

if exist "%OUTPUT%" (
    echo Cleaning old output...
    rmdir /s /q "%OUTPUT%"
)
mkdir "%OUTPUT%"
mkdir "%OUTPUT%\ServerPlugin"
mkdir "%OUTPUT%\ClientPlugin"
mkdir "%OUTPUT%\Terminal"
mkdir "%OUTPUT%\SeedSyncGlobal"

echo [1/4] Building SeedSyncGlobal...
cd /d "%ROOT%SeedSyncGlobal"
dotnet build SeedSyncGlobal.csproj -c Debug --nologo -v q
if %ERRORLEVEL% neq 0 (
    echo SeedSyncGlobal build FAILED
    pause
    exit /b 1
)
copy /y "%ROOT%SeedSyncGlobal\bin\Debug\net472\SeedSyncGlobal.dll" "%OUTPUT%\SeedSyncGlobal\SeedSyncGlobal.dll" >nul
copy /y "%ROOT%SeedSyncGlobal\bin\Debug\net472\SeedSyncGlobal.pdb" "%OUTPUT%\SeedSyncGlobal\SeedSyncGlobal.pdb" >nul 2>nul
echo [OK] SeedSyncGlobal
echo.

echo [2/4] Building ServerPlugin...
cd /d "%ROOT%"
dotnet build ServerPlugin.csproj -c Debug --nologo -v q
if %ERRORLEVEL% neq 0 (
    echo ServerPlugin build FAILED
    pause
    exit /b 1
)
copy /y "%ROOT%bin\Debug\net472\ServerPlugin.dll" "%OUTPUT%\ServerPlugin\ServerPlugin.dll" >nul
copy /y "%ROOT%bin\Debug\net472\ServerPlugin.pdb" "%OUTPUT%\ServerPlugin\ServerPlugin.pdb" >nul 2>nul
echo [OK] ServerPlugin
echo.

echo [3/4] Building ClientPlugin...
cd /d "%ROOT%ClientPlugin"
dotnet build ClientPlugin.csproj -c Debug --nologo -v q
if %ERRORLEVEL% neq 0 (
    echo ClientPlugin build FAILED
    pause
    exit /b 1
)
copy /y "%ROOT%ClientPlugin\bin\Debug\net472\ClientPlugin.dll" "%OUTPUT%\ClientPlugin\ClientPlugin.dll" >nul
echo [OK] ClientPlugin
echo.

echo [4/4] Building Terminal...
cd /d "%ROOT%HacknetServerTerminal\HacknetServerTerminal"
dotnet build HacknetServerTerminal.csproj -c Debug --nologo -v q
if %ERRORLEVEL% neq 0 (
    echo Terminal build FAILED
    pause
    exit /b 1
)
copy /y "%ROOT%HacknetServerTerminal\HacknetServerTerminal\bin\Debug\net9.0\HacknetServerTerminal.exe" "%OUTPUT%\Terminal\HacknetServerTerminal.exe" >nul
copy /y "%ROOT%HacknetServerTerminal\HacknetServerTerminal\bin\Debug\net9.0\LiteNetLib.dll" "%OUTPUT%\Terminal\LiteNetLib.dll" >nul
copy /y "%ROOT%HacknetServerTerminal\HacknetServerTerminal\bin\Debug\net9.0\*.runtimeconfig.json" "%OUTPUT%\Terminal\" >nul 2>nul
copy /y "%ROOT%HacknetServerTerminal\HacknetServerTerminal\bin\Debug\net9.0\*.deps.json" "%OUTPUT%\Terminal\" >nul 2>nul
echo [OK] Terminal
echo.

echo ========================================
echo BUILD COMPLETE
echo Output: %OUTPUT%
echo ========================================
dir /b "%OUTPUT%\SeedSyncGlobal"
dir /b "%OUTPUT%\ServerPlugin"
dir /b "%OUTPUT%\ClientPlugin"
dir /b "%OUTPUT%\Terminal"
echo.
echo Deploy: SeedSyncGlobal.dll -> Hacknet/BepInEx/plugins/ (GLOBAL)
echo Deploy: ServerPlugin.dll -> Extensions/xxx/Plugins/
echo Deploy: ClientPlugin.dll -> Extensions/xxx/Plugins/
echo Deploy: Terminal folder -> anywhere
echo.

cd /d "%ROOT%"
pause
