@echo off
rem Installs (or updates) the published BitMagic.ClaudeBox.Cli .NET tool from NuGet.org.
setlocal

dotnet tool list -g | findstr /I "bitmagic.claudebox.cli" >nul
if %errorlevel%==0 (
    dotnet tool update -g bitmagic.claudebox.cli
) else (
    dotnet tool install -g bitmagic.claudebox.cli
)

endlocal
