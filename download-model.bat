@echo off
chcp 65001 >nul 2>&1
setlocal enabledelayedexpansion
cd /d "%~dp0"

set "MODEL=ggml-large-v3-turbo.bin"
set "URL=https://huggingface.co/ggerganov/whisper.cpp/resolve/main/%MODEL%"

echo.
echo ================================================================
echo   LERON-AUDIO · DOWNLOAD WHISPER MODEL
echo ================================================================
echo.
echo   Файл : %MODEL% (~1.5 ГБ)
echo   Источник: Hugging Face (whisper.cpp)
echo.

if exist "%MODEL%" (
    echo   [i] Модель уже существует в папке проекта.
    echo       Скачивание не требуется.
    pause
    exit /b 0
)

echo   [i] Начинаю загрузку... Это может занять несколько минут.
echo.

:: Используем PowerShell для скачивания, отключаем прогресс-бар для скорости
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
"$ProgressPreference = 'SilentlyContinue';" ^
"$url = '%URL%';" ^
"$out = '%MODEL%';" ^
"try {" ^
"    Invoke-WebRequest -Uri $url -OutFile $out -UseBasicParsing;" ^
"    Write-Host '';" ^
"    Write-Host '  [OK] Модель успешно скачана!';" ^
"} catch {" ^
"    Write-Host '';" ^
"    Write-Host ('  [XX] Ошибка загрузки: ' + $_.Exception.Message);" ^
"    Write-Host '      Проверь интернет-соединение или скачай файл вручную:';" ^
"    Write-Host ('      ' + $url);" ^
"    exit 1;" ^
"}"

if exist "%MODEL%" (
    echo.
    echo   Теперь можно запускать start.bat или собирать Release.
)

echo.
pause