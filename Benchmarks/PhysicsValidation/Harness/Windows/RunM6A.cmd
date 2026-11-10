@echo off
rem Physics bench M6-r1 formal runner, bundle A. No admin, Python, Git or Unity required.
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

rem build casefile group dt(- = default) ccd attempt seed profile(none/ctl/game) fps(- = none)
rem P5c: curb fix controlled, build default physics
call :runone BASE m6-curb.json p5c - - 1 1 ctl -
call :runone CURBFIX m6-curb.json p5c - - 1 1 ctl -
call :runone BASE m6-curb.json p5c - - 2 1 ctl -
call :runone CURBFIX m6-curb.json p5c - - 2 1 ctl -
call :runone BASE m6-curb.json p5c - - 3 2 ctl -
call :runone CURBFIX m6-curb.json p5c - - 3 2 ctl -
rem P4: physics settings 2x2 on original scene
call :runone BASE m6-curb.json p4 0.02 Discrete 1 1 ctl -
call :runone BASE m6-curb.json p4 0.02 ContinuousDynamic 1 1 ctl -
call :runone BASE m6-curb.json p4 0.01 Discrete 1 1 ctl -
call :runone BASE m6-curb.json p4 0.01 ContinuousDynamic 1 1 ctl -
call :runone BASE m6-curb.json p4 0.02 Discrete 2 1 ctl -
call :runone BASE m6-curb.json p4 0.02 ContinuousDynamic 2 1 ctl -
call :runone BASE m6-curb.json p4 0.01 Discrete 2 1 ctl -
call :runone BASE m6-curb.json p4 0.01 ContinuousDynamic 2 1 ctl -
call :runone BASE m6-curb.json p4 0.02 Discrete 3 2 ctl -
call :runone BASE m6-curb.json p4 0.02 ContinuousDynamic 3 2 ctl -
call :runone BASE m6-curb.json p4 0.01 Discrete 3 2 ctl -
call :runone BASE m6-curb.json p4 0.01 ContinuousDynamic 3 2 ctl -
rem P5g: curb fix game profile
call :runone BASE m6-curb.json p5g - - 1 1 game -
call :runone CURBFIX m6-curb.json p5g - - 1 1 game -
call :runone BASE m6-curb.json p5g - - 2 2 game -
call :runone CURBFIX m6-curb.json p5g - - 2 2 game -
call :runone BASE m6-curb.json p5g - - 3 3 game -
call :runone CURBFIX m6-curb.json p5g - - 3 3 game -

echo Packing results...
tar -a -c -f "%~dp0PhysicsBench-M6A-Results-%TS%.zip" -C "%OUT%" .
if errorlevel 1 powershell -NoProfile -Command "Compress-Archive -Path '%OUT%\*' -DestinationPath '%~dp0PhysicsBench-M6A-Results-%TS%.zip' -Force"
echo.
echo Done. Please send this file:
echo   %~dp0PhysicsBench-M6A-Results-%TS%.zip
echo (If it is missing, send the folder %OUT%)
pause
exit /b 0

:runone
set V=%1
set CF=%2
set GRP=%3
set DT=%4
set CCD=%5
set ATT=%6
set SEED=%7
set PROF=%8
set FPS=%9
set PHYS=def
set PHYSARG=
if not "%DT%"=="-" set PHYS=dt%DT%-%CCD%
if not "%DT%"=="-" set PHYSARG=-benchDt %DT% -benchCcd %CCD%
set RID=W-M6-%V%-%GRP%-%PHYS%-a%ATT%-s%SEED%-%TS%
set PROFARG=
if "%PROF%"=="ctl" set PROFARG=-benchProfile "%~dp0profiles\profile-H7.json"
if "%PROF%"=="game" set PROFARG=-benchProfile "%~dp0profiles\profile-H7-game.json"
set FPSARG=
if not "%FPS%"=="-" set FPSARG=-benchFps %FPS%
if not exist "%~dp0%V%\PhysicsBench.exe" (
  echo missing build %V% > "%OUT%\%RID%.exitcode.txt"
  goto :eof
)
if not exist "%~dp0cases\%CF%" (
  echo missing cases %CF% > "%OUT%\%RID%.exitcode.txt"
  goto :eof
)
echo [%TIME%] Running %RID% ... do not minimize the window, do not touch the mouse.
echo started %DATE% %TIME% > "%OUT%\%RID%.started.txt"
start "" /wait "%~dp0%V%\PhysicsBench.exe" -physicsBench %PROFARG% -benchCases "%~dp0cases\%CF%" -benchOut "%OUT%" -benchRunId %RID% -benchSeed %SEED% -benchAttempt %ATT% %PHYSARG% %FPSARG% -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "%OUT%\%RID%.player.log"
echo %ERRORLEVEL% > "%OUT%\%RID%.exitcode.txt"
echo [%TIME%] %RID% finished, exit code %ERRORLEVEL%
goto :eof
