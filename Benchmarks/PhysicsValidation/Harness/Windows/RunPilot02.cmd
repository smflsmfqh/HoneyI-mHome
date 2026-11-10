@echo off
rem Physics bench pilot-02 runner (M5 exploration, case set e2 per version). No admin, Python, Git or Unity required.
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

call :runone Hm1 base
call :runone Hm1 unfreezeY
call :runone H0 base
call :runone H1 base
call :runone H7 base

echo Packing results...
rem tar (built into Windows 10 1803+) stores "/" paths. Compress-Archive in PowerShell 5.1 stores "\" paths.
tar -a -c -f "%~dp0PhysicsBench-Pilot02-Results-%TS%.zip" -C "%OUT%" .
if errorlevel 1 powershell -NoProfile -Command "Compress-Archive -Path '%OUT%\*' -DestinationPath '%~dp0PhysicsBench-Pilot02-Results-%TS%.zip' -Force"
echo.
echo Done. Please send this file:
echo   %~dp0PhysicsBench-Pilot02-Results-%TS%.zip
echo (If it is missing, send the folder %OUT%)
pause
exit /b 0

:runone
set V=%1
set VAR=%2
set TAG=%V%-%VAR%
set PROFARG=
if not "%VAR%"=="base" set PROFARG=-benchProfile "%~dp0profiles\profile-%V%-%VAR%.json"
rem Each version gets its own e2 case file (start heights from that version's visual mesh probe).
set CASEARG=-benchCases "%~dp0cases\explore-e2-%V%.json"
if not exist "%~dp0cases\explore-e2-%V%.json" (
  echo missing cases %V% > "%OUT%\%TAG%.exitcode.txt"
  goto :eof
)
if not exist "%~dp0%V%\PhysicsBench.exe" (
  echo missing build %V% > "%OUT%\%TAG%.exitcode.txt"
  goto :eof
)
echo [%TIME%] Running %TAG% ... do not minimize the window.
echo started %DATE% %TIME% > "%OUT%\%TAG%.started.txt"
start "" /wait "%~dp0%V%\PhysicsBench.exe" -physicsBench %PROFARG% %CASEARG% -benchOut "%OUT%" -benchRunId W-%TAG%-p02-%TS% -benchSeed 1 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "%OUT%\W-%TAG%-p02-%TS%.player.log"
echo %ERRORLEVEL% > "%OUT%\%TAG%.exitcode.txt"
echo [%TIME%] %TAG% finished, exit code %ERRORLEVEL%
goto :eof
