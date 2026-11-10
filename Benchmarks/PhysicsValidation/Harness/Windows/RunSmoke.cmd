@echo off
rem Physics bench smoke runner. No admin, Python, Git or Unity required.
rem Runs each build folder listed below in order, keeps every exit code, then zips the results.
setlocal EnableExtensions
cd /d "%~dp0"

for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMddTHHmmss"') do set TS=%%i
if "%TS%"=="" set TS=%RANDOM%%RANDOM%

set OUT=%~dp0Results\%TS%
mkdir "%OUT%" 2>nul
echo test> "%OUT%\_write_test.txt" 2>nul
if not exist "%OUT%\_write_test.txt" (
  set OUT=%LOCALAPPDATA%\PhysicsBenchResults\%TS%
  mkdir "%LOCALAPPDATA%\PhysicsBenchResults\%TS%" 2>nul
)
del "%OUT%\_write_test.txt" 2>nul
echo Results: %OUT%

rem PowerShell writes the file itself as UTF-8 (cmd redirection mixed UTF-16 output in smoke-01).
powershell -NoProfile -Command "& { Get-CimInstance Win32_OperatingSystem | Format-List Caption,Version,OSArchitecture; Get-CimInstance Win32_Processor | Format-List Name,NumberOfCores; Get-CimInstance Win32_VideoController | Format-List Name,DriverVersion,CurrentRefreshRate } | Out-File -Encoding utf8 -FilePath '%OUT%\windows-environment.txt'"

for %%V in (H1 H7) do call :runone %%V

echo Packing results...
rem tar (built into Windows 10 1803+) stores "/" paths. Compress-Archive in PowerShell 5.1 stores "\" paths.
tar -a -c -f "%~dp0PhysicsBench-Results-%TS%.zip" -C "%OUT%" .
if errorlevel 1 powershell -NoProfile -Command "Compress-Archive -Path '%OUT%\*' -DestinationPath '%~dp0PhysicsBench-Results-%TS%.zip' -Force"
echo.
echo Done. Please send this file:
echo   %~dp0PhysicsBench-Results-%TS%.zip
echo (If it is missing, send the folder %OUT%)
pause
exit /b 0

:runone
set V=%1
if not exist "%~dp0%V%\PhysicsBench.exe" (
  echo missing build %V% > "%OUT%\%V%.exitcode.txt"
  goto :eof
)
echo [%TIME%] Running %V% ... do not minimize the window.
echo started %DATE% %TIME% > "%OUT%\%V%.started.txt"
start "" /wait "%~dp0%V%\PhysicsBench.exe" -physicsBench -benchOut "%OUT%" -benchRunId W-%V%-smoke-%TS% -benchSeed 1 -benchCapture end -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "%OUT%\W-%V%-smoke-%TS%.player.log"
echo %ERRORLEVEL% > "%OUT%\%V%.exitcode.txt"
echo [%TIME%] %V% finished, exit code %ERRORLEVEL%
goto :eof
