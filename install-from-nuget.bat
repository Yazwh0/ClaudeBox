@echo off
rem Installs the latest published BitMagic.ClaudeBox.Cli from NuGet.org, replacing whatever is
rem installed (including a local build). Uninstalling first matters: 'dotnet tool update' won't
rem move to a lower version, so a local build numbered above the release would otherwise stay.
setlocal

dotnet tool list -g | findstr /I "bitmagic.claudebox.cli" >nul
if %errorlevel%==0 (
    dotnet tool uninstall -g bitmagic.claudebox.cli
    if errorlevel 1 exit /b 1
)

rem --source (not --add-source) so only nuget.org is used, never a local pack.
dotnet tool install -g bitmagic.claudebox.cli --source https://api.nuget.org/v3/index.json
if errorlevel 1 exit /b 1

cb --version

endlocal
