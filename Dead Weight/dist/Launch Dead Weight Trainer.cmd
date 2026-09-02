@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Launch Dead Weight Trainer.ps1"
if errorlevel 1 (
    echo.
    echo The trainer could not be launched. See the error above.
    pause
)
endlocal
