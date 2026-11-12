@echo off
setlocal EnableDelayedExpansion
rem CyberDefender: Kernel Clash - launches gesture and voice recognition for Unity
rem The window title lets the game see that the bridge is already starting and not launch it twice
title CyberDefender - Setup
cd /d "%~dp0"

if not exist .venv\Scripts\python.exe (
    rem MediaPipe has no wheels for the newest Python, so look for 3.12 / 3.11 / 3.10
    set PYVER=
    for %%v in (3.12 3.11 3.10) do (
        if not defined PYVER (
            py -%%v -c "import sys" >nul 2>&1 && set PYVER=%%v
        )
    )
    if not defined PYVER (
        echo [error] Python 3.10-3.12 is required for MediaPipe. Install it with:
        echo         winget install Python.Python.3.12
        pause
        exit /b 1
    )
    echo [setup] Creating virtual environment with Python !PYVER!...
    py -!PYVER! -m venv .venv
)

call .venv\Scripts\activate.bat

rem Dependencies are installed once; the marker keeps the requirements they were installed from
fc /b requirements.txt .venv\installed-requirements.txt >nul 2>&1
if errorlevel 1 (
    echo [setup] Installing dependencies, the first run takes a few minutes...
    rem --no-cache-dir: the shared pip cache fails when two installs run at once or antivirus locks a wheel
    python -m pip install -r requirements.txt --quiet --disable-pip-version-check --no-cache-dir
    if errorlevel 1 (
        echo [error] pip install failed. See messages above.
        pause
        exit /b 1
    )
    copy /y requirements.txt .venv\installed-requirements.txt >nul
)

if not exist model (
    echo [setup] Downloading Vosk speech model ~40 MB...
    powershell -NoProfile -Command "Invoke-WebRequest https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip -OutFile model.zip; Expand-Archive model.zip -DestinationPath . -Force; Rename-Item vosk-model-small-en-us-0.15 model; Remove-Item model.zip"
)

start "CyberDefender - Gestures" cmd /k python gesture_server.py %*
start "CyberDefender - Voice" cmd /k python voice_server.py
echo [ok] AI bridge started.
