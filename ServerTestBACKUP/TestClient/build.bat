@echo off
cd /d "%~dp0"
set CSC=%windir%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%windir%\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not exist "bin" mkdir bin

%CSC% -nologo -target:exe -out:bin\TestClient.exe -r:libs\LiteNetLib.dll -recurse:*.cs
if %ERRORLEVEL% EQU 0 (
    echo Build OK: bin\TestClient.exe
) else (
    echo Build FAILED
)
pause
