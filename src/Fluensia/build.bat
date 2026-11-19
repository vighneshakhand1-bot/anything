@echo off
REM ============================================================================
REM  Fluensia - manual build script (Windows)
REM  Requirements: .NET 8 SDK (https://dotnet.microsoft.com/download)
REM  Usage:   build.bat                 -> framework-dependent, Release
REM           build.bat --portable      -> single self-contained .exe (bigger, no SDK needed)
REM           build.bat --debug         -> Debug build, no publish
REM ============================================================================
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [x] dotnet SDK not found. Install .NET 8 SDK first: https://dotnet.microsoft.com/download
  pause
  exit /b 1
)

if /i "%1"=="--portable" goto portable
if /i "%1"=="--debug"    goto debug

echo [1/2] Restoring packages...
dotnet restore Fluensia.csproj || goto fail

echo [2/2] Publishing release build...
dotnet publish Fluensia.csproj -c Release -r win-x64 --self-contained false -o "..\..\dist\Fluensia" || goto fail

echo.
echo Done.  Run:  dist\Fluensia\Fluensia.exe
goto end

:portable
echo [1/2] Restoring packages...
dotnet restore Fluensia.csproj || goto fail
echo [2/2] Publishing self-contained single file (win-x64)...
dotnet publish Fluensia.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true -o "..\..\dist\Fluensia-portable" || goto fail
echo.
echo Done.  Run:  dist\Fluensia-portable\Fluensia.exe
goto end

:debug
echo Building Debug...
dotnet build Fluensia.csproj -c Debug || goto fail
echo.
echo Done.  Run:  bin\Debug\net8.0\Fluensia.exe
goto end

:fail
echo.
echo [x] Build failed. Scroll up for the compiler output.
pause
exit /b 1

:end
pause
