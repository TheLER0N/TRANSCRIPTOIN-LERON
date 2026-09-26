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
curl.exe -fL --progress-bar -o "%MODEL%" "%URL%"
if !errorlevel! neq 0 (
    echo.
    echo   [XX] Ошибка загрузки. Проверь интернет-соединение или скачай вручную:
    echo       %URL%
    pause
    exit /b 1
)

if exist "%MODEL%" (
    echo.
    echo   [OK] Модель успешно скачана!
    echo       Теперь можно запускать start.bat или собирать Release.
)

echo.
pause