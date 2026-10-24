@echo off
rem Packs cb from source and installs it as the global BitMagic.ClaudeBox.Cli tool, for testing
rem local changes without waiting on a NuGet publish. install-from-nuget.bat switches back.
setlocal

set SCRIPT_DIR=%~dp0
set PROJ=%SCRIPT_DIR%src\Claudebox\Claudebox.csproj
set OUT=%SCRIPT_DIR%nupkg-local
set PKGCACHE=%USERPROFILE%\.nuget\packages\bitmagic.claudebox.cli
if defined NUGET_PACKAGES set PKGCACHE=%NUGET_PACKAGES%\bitmagic.claudebox.cli

rem Local builds are <next patch after the latest release>-local.<timestamp>: newer than the
rem release they're based on, older than the next real one, and never the same twice. NuGet
rem caches packages by version, so reusing a version can silently install a stale build.
set BASE=v0.0.0
for /f "delims=" %%t in ('git -C "%SCRIPT_DIR%." describe --tags --abbrev^=0 --match "v*" 2^>nul') do set BASE=%%t
for /f "tokens=1-3 delims=v.-" %%a in ("%BASE%") do (
    set MAJOR=%%a
    set MINOR=%%b
    set /a PATCH=%%c+1
)
for /f %%d in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMddHHmmss"') do set STAMP=%%d
set VERSION=%MAJOR%.%MINOR%.%PATCH%-local.%STAMP%

if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%"

echo Packing %VERSION% ...
dotnet pack "%PROJ%" -c Release -o "%OUT%" -p:Version=%VERSION% --nologo -v q
if errorlevel 1 (
    echo Pack failed.
    exit /b 1
)

dotnet tool list -g | findstr /I "bitmagic.claudebox.cli" >nul
if %errorlevel%==0 (
    dotnet tool uninstall -g bitmagic.claudebox.cli
    if errorlevel 1 (
        echo Uninstall failed. Close any cb sessions started before this version and rerun.
        exit /b 1
    )
)

rem Old local builds in the NuGet cache are never used again.
for /d %%p in ("%PKGCACHE%\*-local.*" "%PKGCACHE%\*-dev") do rmdir /s /q "%%p" 2>nul

rem --source (not --add-source) and an exact version, so only this pack can be installed.
dotnet tool install -g bitmagic.claudebox.cli --source "%OUT%" --version %VERSION%
if errorlevel 1 exit /b 1

cb --version

endlocal
exit /b 0
