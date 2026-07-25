@echo off
title Nightwatch Unified Updater
echo ============================================================
echo      NIGHTWATCH TEK TUSLA VERITABANI GUNCELLEME ARACI
echo ============================================================
echo.

:: Yonetici haklari veya standart modda PowerShell betigini calistir
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0\run_update_dialog.ps1" -ScriptDir "%~dp0"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [HATA] Guncelleme islemi sirasinda bir sorun olustu.
    pause
    exit /b %ERRORLEVEL%
)
