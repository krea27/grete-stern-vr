@echo off
title Serena VR - MediaPipe Hand Tracking
cd /d "%~dp0"

echo ========================================================
echo    Iniciando MediaPipe Hand Tracking para Serena VR
echo ========================================================
echo.

:: Liberar la camara si algun proceso previo quedo en segundo plano
powershell -NoProfile -Command "Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*vision_core.py*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"

python vision_core.py
if errorlevel 1 (
    echo.
    echo ========================================================
    echo [!] Hubo un error al ejecutar el programa.
    echo [!] Si es la primera vez que lo abris en esta PC,
    echo     ejecuta primero: INSTALAR_DEPENDENCIAS.bat
    echo ========================================================
    pause
)
