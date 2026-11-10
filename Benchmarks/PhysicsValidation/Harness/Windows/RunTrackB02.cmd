@echo off
rem Physics bench track B-02 runner (S3c curb-extended test build, curb set). No admin, Python, Git or Unity required.
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

rem build cases tag attempt seed dt ccd
call :runone S3c trackB-curb-S3.json curb 1 1 0.01 ContinuousDynamic
call :runone S3c trackB-curb-S3.json curb 2 1 0.01 ContinuousDynamic
call :runone S3c trackB-curb-S3.json curb 3 2 0.01 ContinuousDynamic
call :runone S3c trackB-curb-S3.json curb 1 1 0.02 Discrete
call :runone S3c trackB-curb-S3.json curb 2 1 0.02 Discrete
call :runone S3c trackB-curb-S3.json curb 3 2 0.02 Discrete

echo Packing results...
tar -a -c -f "%~dp0PhysicsBench-TrackB02-Results-%TS%.zip" -C "%OUT%" .
if errorlevel 1 powershell -NoProfile -Command "Compress-Archive -Path '%OUT%\*' -DestinationPath '%~dp0PhysicsBench-TrackB02-Results-%TS%.zip' -Force"
echo.
echo Done. Please send this file:
echo   %~dp0PhysicsBench-TrackB02-Results-%TS%.zip
echo (If it is missing, send the folder %OUT%)
pause
exit /b 0

:runone
set V=%1
set CF=%2
set KIND=%3
set ATT=%4
set SEED=%5
set DT=%6
set CCD=%7
set RID=W-B-%V%-%KIND%-dt%DT%-%CCD%-a%ATT%-s%SEED%-%TS%
if not exist "%~dp0%V%\PhysicsBench.exe" (
  echo missing build %V% > "%OUT%\%RID%.exitcode.txt"
  goto :eof
)
echo [%TIME%] Running %RID% ... do not minimize the window.
echo started %DATE% %TIME% > "%OUT%\%RID%.started.txt"
start "" /wait "%~dp0%V%\PhysicsBench.exe" -physicsBench -benchProfile "%~dp0profiles\profile-H7.json" -benchCases "%~dp0cases\%CF%" -benchOut "%OUT%" -benchRunId %RID% -benchSeed %SEED% -benchAttempt %ATT% -benchDt %DT% -benchCcd %CCD% -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "%OUT%\%RID%.player.log"
echo %ERRORLEVEL% > "%OUT%\%RID%.exitcode.txt"
echo [%TIME%] %RID% finished, exit code %ERRORLEVEL%
goto :eof
