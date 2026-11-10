@echo off
rem Physics bench fix1 runner (b: curb fix regression, c: cooking 30 restore). No admin, Python, Git or Unity required.
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
powershell -NoProfile -Command "& { Get-CimInstance Win32_OperatingSystem | Format-List Caption,Version,OSArchitecture; Get-CimInstance Win32_Processor | Format-List Name,NumberOfCores; Get-CimInstance Win32_VideoController | Format-List Name,DriverVersion,CurrentRefreshRate; Get-CimInstance Win32_ComputerSystem | Format-List Model } | Out-File -Encoding utf8 -FilePath '%OUT%\windows-environment.txt'"

rem build caseset profile attempt seed
rem b-controlled
call :runone BASE b ctl 1 1
call :runone CURBFIX b ctl 1 1
call :runone BASE b ctl 2 1
call :runone CURBFIX b ctl 2 1
call :runone BASE b ctl 3 2
call :runone CURBFIX b ctl 3 2
rem c-controlled
call :runone BASE c ctl 1 1
call :runone COOK30 c ctl 1 1
call :runone BASE c ctl 2 1
call :runone COOK30 c ctl 2 1
call :runone BASE c ctl 3 2
call :runone COOK30 c ctl 3 2
rem b-game and c-game
call :runone BASE b game 1 1
call :runone CURBFIX b game 1 1
call :runone COOK30 b game 1 1
call :runone BASE b game 2 2
call :runone CURBFIX b game 2 2
call :runone COOK30 b game 2 2
call :runone BASE b game 3 3
call :runone CURBFIX b game 3 3
call :runone COOK30 b game 3 3

echo Packing results...
tar -a -c -f "%~dp0PhysicsBench-Fix1-Results-%TS%.zip" -C "%OUT%" .
if errorlevel 1 powershell -NoProfile -Command "Compress-Archive -Path '%OUT%\*' -DestinationPath '%~dp0PhysicsBench-Fix1-Results-%TS%.zip' -Force"
echo.
echo Done. Please send this file:
echo   %~dp0PhysicsBench-Fix1-Results-%TS%.zip
echo (If it is missing, send the folder %OUT%)
pause
exit /b 0

:runone
set V=%1
set CS=%2
set PR=%3
set ATT=%4
set SEED=%5
set PF=profile-H7.json
if "%PR%"=="game" set PF=profile-H7-game.json
set RID=W-F-%V%-%CS%-%PR%-a%ATT%-s%SEED%-%TS%
if not exist "%~dp0%V%\PhysicsBench.exe" (
  echo missing build %V% > "%OUT%\%RID%.exitcode.txt"
  goto :eof
)
echo [%TIME%] Running %RID% ... do not minimize the window.
echo started %DATE% %TIME% > "%OUT%\%RID%.started.txt"
start "" /wait "%~dp0%V%\PhysicsBench.exe" -physicsBench -benchProfile "%~dp0profiles\%PF%" -benchCases "%~dp0cases\fix1-%CS%.json" -benchOut "%OUT%" -benchRunId %RID% -benchSeed %SEED% -benchAttempt %ATT% -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "%OUT%\%RID%.player.log"
echo %ERRORLEVEL% > "%OUT%\%RID%.exitcode.txt"
echo [%TIME%] %RID% finished, exit code %ERRORLEVEL%
goto :eof
