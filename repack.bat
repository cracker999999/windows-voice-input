@echo off
setlocal

cd /d "%~dp0"

echo [1/4] Stopping running VoiceInput.exe ...
taskkill /F /T /IM VoiceInput.exe >nul 2>&1

echo [2/4] Checking dotnet ...
dotnet --version >nul 2>&1
if errorlevel 1 (
  echo [ERROR] dotnet is not installed or not in PATH.
  exit /b 1
)

echo [3/4] Restoring packages ...
dotnet restore "VoiceInput\VoiceInput.csproj" -r win-x64
if errorlevel 1 (
  echo [ERROR] dotnet restore failed.
  exit /b 1
)

echo [4/4] Publishing single-file executable ...
dotnet publish "VoiceInput\VoiceInput.csproj" -c Release -r win-x64 --self-contained true
if errorlevel 1 (
  echo [ERROR] dotnet publish failed.
  exit /b 1
)

echo.
echo [OK] Repack complete.
echo Output: "%~dp0Release\VoiceInput.exe"
exit /b 0
