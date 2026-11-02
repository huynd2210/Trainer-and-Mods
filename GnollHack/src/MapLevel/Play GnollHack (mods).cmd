@echo off
setlocal EnableDelayedExpansion
rem Starts GnollHack with every mod installed in the game folder. A mod is a folder
rem GnollHack\<Mod>\ holding this launcher and <Mod>.dll (or bin\<Mod>.dll when built
rem from source). Every mod ships the same launcher, so any copy starts all of them.
for %%G in ("%~dp0..") do set "GAME=%%~fG"
if not exist "%GAME%\GnollHackM.exe" (
    echo This folder must be inside the GnollHack game folder, next to GnollHackM.exe.
    pause
    exit /b 1
)
rem Mods load only when the game starts. A second start just switches to the open game.
tasklist /FI "IMAGENAME eq GnollHackM.exe" /NH | find /I "GnollHackM.exe" >nul
if not errorlevel 1 (
    echo GnollHack is already running, so the mods cannot be loaded into it.
    echo Save and close GnollHack, then run this launcher again.
    pause
    exit /b 1
)
set "HOOKS="
for /d %%D in ("%GAME%\*") do (
    if exist "%%D\%~nx0" (
        set "DLL="
        if exist "%%D\%%~nxD.dll" set "DLL=%%D\%%~nxD.dll"
        if not defined DLL if exist "%%D\bin\%%~nxD.dll" set "DLL=%%D\bin\%%~nxD.dll"
        if defined DLL (
            if defined HOOKS (set "HOOKS=!HOOKS!;!DLL!") else (set "HOOKS=!DLL!")
        )
    )
)
set "DOTNET_STARTUP_HOOKS=%HOOKS%"
start "" /D "%GAME%" "%GAME%\GnollHackM.exe"
