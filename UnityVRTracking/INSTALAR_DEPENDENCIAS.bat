@echo off
title Serena VR - Instalador de Dependencias
cd /d "%~dp0"
echo ========================================================
echo   Instalando dependencias de Python (MediaPipe + OSC)
echo ========================================================
echo.
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
echo.
echo ========================================================
echo   Instalacion finalizada. Ya podes abrir INICIAR_TRACKING.bat!
echo ========================================================
pause

