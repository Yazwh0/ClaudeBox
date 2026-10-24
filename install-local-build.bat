@echo off
rem Packs cb from source and installs it as the global BitMagic.ClaudeBox.Cli tool, for testing
rem local changes without waiting on a NuGet publish.
setlocal

set SCRIPT_DIR=%~dp0
set PROJ=%SCRIPT_DIR%src\Claudebox\Claudebox.csproj
set OUT=%SCRIPT_DIR%nupkg-local
set VERSION=0.1.0-dev

if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%"

dotnet pack "%PROJ%" -c Release -o "%OUT%" -p:Version=%VERSION%
if errorlevel 1 (
    echo Pack failed.
    exit /b 1
)

rem Not swallowing uninstall's output, so a failure (e.g. a running cb.exe holding the files)
rem is visible rather than install quietly leaving the old build in place.
dotnet tool uninstall -g bitmagic.claudebox.cli
rem --add-source adds to nuget.org rather than replacing it, so pin the version or the
rem published release would win over the local build.
dotnet tool install -g bitmagic.claudebox.cli --add-source "%OUT%" --version %VERSION%
if errorlevel 1 (
    echo.
    echo Install failed - this usually means uninstall above couldn't remove the old files.
    echo Close any running cb sessions ^(tasklist /FI "IMAGENAME eq cb.exe"^) and rerun this script.
    exit /b 1
)

endlocal
exit /b 0
