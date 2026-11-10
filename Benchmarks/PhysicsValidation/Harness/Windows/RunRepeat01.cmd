@echo off
rem Physics bench repeat-01 runner (M5 H0 reproducibility check, case set r1 = e2c H0 subset, same build M5-H0-win-01). No admin, Python, Git or Unity required.
rem 6 full runs (53 cases, seeds 1,1,2,3,4,5) then 10 single-case runs of E1_P11R_hold (60fps x6, 30fps x2, 120fps x2). Keeps every exit code, then zips the results.
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

if not exist "%~dp0cases\repeat-r1-H0.json" (
  echo missing cases > "%OUT%\missing-cases.txt"
  goto :pack
)
if not exist "%~dp0H0\PhysicsBench.exe" (
  echo missing build > "%OUT%\missing-build.txt"
  goto :pack
)

rem tag attempt seed fps filter(- = none)
call :runone full 1 1 60 -
call :runone full 2 1 60 -
call :runone full 3 2 60 -
call :runone full 4 3 60 -
call :runone full 5 4 60 -
call :runone full 6 5 60 -
call :runone p11r 1 1 60 E1_P11R_hold
call :runone p11r 2 1 60 E1_P11R_hold
call :runone p11r 3 1 60 E1_P11R_hold
call :runone p11r 4 1 60 E1_P11R_hold
call :runone p11r 5 1 60 E1_P11R_hold
call :runone p11r 6 1 60 E1_P11R_hold
call :runone p11r 7 1 30 E1_P11R_hold
call :runone p11r 8 1 30 E1_P11R_hold
call :runone p11r 9 1 120 E1_P11R_hold
call :runone p11r 10 1 120 E1_P11R_hold

:pack
echo Packing results...
tar -a -c -f "%~dp0PhysicsBench-Repeat01-Results-%TS%.zip" -C "%OUT%" .
if errorlevel 1 powershell -NoProfile -Command "Compress-Archive -Path '%OUT%\*' -DestinationPath '%~dp0PhysicsBench-Repeat01-Results-%TS%.zip' -Force"
echo.
echo Done. Please send this file:
echo   %~dp0PhysicsBench-Repeat01-Results-%TS%.zip
echo (If it is missing, send the folder %OUT%)
pause
exit /b 0

:runone
set KIND=%1
set ATT=%2
set SEED=%3
set FPS=%4
set FILTER=%5
set RID=W-H0-r1-%KIND%-a%ATT%-s%SEED%-f%FPS%-%TS%
set FILTERARG=
if not "%FILTER%"=="-" set FILTERARG=-benchCaseFilter %FILTER%
echo [%TIME%] Running %RID% ... do not minimize the window, do not touch the mouse.
echo started %DATE% %TIME% > "%OUT%\%RID%.started.txt"
start "" /wait "%~dp0H0\PhysicsBench.exe" -physicsBench -benchCases "%~dp0cases\repeat-r1-H0.json" %FILTERARG% -benchOut "%OUT%" -benchRunId %RID% -benchSeed %SEED% -benchAttempt %ATT% -benchFps %FPS% -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "%OUT%\%RID%.player.log"
echo %ERRORLEVEL% > "%OUT%\%RID%.exitcode.txt"
echo [%TIME%] %RID% finished, exit code %ERRORLEVEL%
goto :eof
