@echo off
setlocal
chcp 65001 >nul
title StealthSnip - Build and Auto Start Setup

echo ========================================================
echo         STEALTHSNIP - AUTO BUILD AND RUN SCRIPT
echo ========================================================
echo.

REM 1. Tat tien trinh cu neu dang chay de tranh bi lock file khi build
echo [1/3] Kiem tra va dong tien trinh StealthSnip cu neu co...
taskkill /F /IM StealthSnip.exe >nul 2>&1

REM 2. Bien dich ban Release xuat ra thu muc dist
echo [2/3] Dang bien dich ung dung (Release mode)...
dotnet publish "%~dp0screenshot-app.csproj" -c Release -o "%~dp0dist" --nologo
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [LOI] Qua trinh bien dich that bai!
    pause
    exit /b %ERRORLEVEL%
)

REM 3. Dang ky tu khoi dong cung Windows vao Registry (HKCU\Run)
echo [3/3] Dang thiet lap tu dong khoi dong cung Windows...
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "StealthSnip" /t REG_SZ /d "\"%~dp0dist\StealthSnip.exe\"" /f >nul

REM Dong bo file cau hinh config.json neu co
if exist "%~dp0config.json" (
    copy /y "%~dp0config.json" "%~dp0dist\config.json" >nul
)

REM 4. Khoi chay ung dung chay ngam
echo.
echo ========================================================
echo  Bien dich va cai dat thanh cong!
echo  - Da bat tu khoi dong cung Windows (Registry HKCU\Run).
echo  - Phim tat: Alt + A (Chup vung), Alt + S (Toan man hinh).
echo  - Dang khoi dong StealthSnip...
echo ========================================================
start "" "%~dp0dist\StealthSnip.exe"

ping 127.0.0.1 -n 3 >nul
exit /b 0
