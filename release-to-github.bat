@echo off
chcp 65001 >nul 2>&1
setlocal enabledelayedexpansion
set "ROOT=%~dp0"
set "PROJ=%ROOT%code\Leron.Audio.csproj"
set "BUILD_DIR=%ROOT%release-build"
set "MODEL=%ROOT%ggml-large-v3-turbo.bin"
echo.
echo ================================================================
echo   LERON-AUDIO · RELEASE TO GITHUB
echo ================================================================
echo.
echo   Репозиторий: https://github.com/TheLER0N/TRANSCRIPTOIN-LERON
echo.
:: ── Проверка окружения ──────────────────────────────────────────
where dotnet >nul 2>&1
if !errorlevel! neq 0 (
    echo   [XX] dotnet не найден. Установи .NET 8 SDK.
    pause
    exit /b 1
)
if not exist "%PROJ%" (
    echo   [XX] Leron.Audio.csproj не найден: %PROJ%
    pause
    exit /b 1
)
if not exist "%MODEL%" (
    echo   [XX] Модель Whisper не найдена: %MODEL%
    echo       Скачай ggml-large-v3-turbo.bin и положи в корень проекта.
    pause
    exit /b 1
)
:: ── Ввод данных релиза ──────────────────────────────────────────
set /p "VER=   Версия (например 1.0.0): "
if "!VER!"=="" (
    echo   [XX] Версия не указана.
    pause
    exit /b 1
)
set /p "TITLE=   Название (Enter = LERON-AUDIO v!VER!): "
if "!TITLE!"=="" set "TITLE=LERON-AUDIO v!VER!"
set /p "DESC=    Описание (Enter = дефолт): "
if "!DESC!"=="" set "DESC=LERON-AUDIO v!VER! — локальная PTT-диктовка через Whisper.cpp."
echo.
echo ================================================================
echo   Версия   : !VER!
echo   Название : !TITLE!
echo   Проект   : %PROJ%
echo   Сборка   : %BUILD_DIR%
echo ================================================================
echo.
set /p "CONFIRM=   Продолжить? [Y/n]: "
if /i "!CONFIRM!"=="n" (
    echo   Отменено.
    pause
    exit /b 0
)
:: ── [1/5] Зачистка release-build ────────────────────────────────
echo.
echo   [1/5] Зачищаю release-build от прошлых артефактов...
if exist "%BUILD_DIR%" (
    rd /s /q "%BUILD_DIR%" 2>nul
    if exist "%BUILD_DIR%" (
        echo   [XX] Не удалось удалить %BUILD_DIR% (файл занят?). Закрой приложение и повтори.
        pause
        exit /b 1
    )
)
mkdir "%BUILD_DIR%" >nul 2>&1
echo   [OK] Папка пустая.
:: ── [2/5] Сборка Release ────────────────────────────────────────
echo.
echo   [2/5] dotnet publish (Release, win-x64)...
dotnet publish "%PROJ%" -c Release -r win-x64 --self-contained false -o "%BUILD_DIR%" --nologo -v q
if !errorlevel! neq 0 (
    echo.
    echo   [XX] Сборка не удалась — повтори dotnet publish вручную.
    pause
    exit /b 1
)
echo   [OK] Собрано в %BUILD_DIR%
:: ── [3/5] Удаление пользовательских данных из release-build ─────
:: Релиз должен быть стерильным: никаких settings.json, history.jsonl,
:: логов, temp-папки и .part-файлов. Пользователь создаст свои данные
:: при первом запуске на своей машине.
echo.
echo   [3/5] Вычищаю личные данные из сборки...
for %%F in (settings.json history.jsonl *.log *.part) do (
    if exist "%BUILD_DIR%\%%F" (
        del /q "%BUILD_DIR%\%%F" >nul 2>&1
        echo         удалено: %%F
    )
)
if exist "%BUILD_DIR%\temp" (
    rd /s /q "%BUILD_DIR%\temp" >nul 2>&1
    echo         удалено: temp^/
)
echo   [OK] В сборке только программа и рантаймы.
:: ── [4/5] Добавление модели Whisper и whisper-cli ───────────────
echo.
echo   [4/5] Копирую модель Whisper и whisper-cli в релиз...
copy /y "%MODEL%" "%BUILD_DIR%\ggml-large-v3-turbo.bin" >nul
if !errorlevel! neq 0 (
    echo   [XX] Не удалось скопировать модель.
    pause
    exit /b 1
)
for %%A in ("%MODEL%") do set /a "MODEL_MB=%%~zA / 1048576"
echo         модель: ggml-large-v3-turbo.bin (~!MODEL_MB! MB)
if exist "%ROOT%build\whisper-cli.exe" (
    copy /y "%ROOT%build\whisper-cli.exe" "%BUILD_DIR%\whisper-cli.exe" >nul
    for %%D in (whisper.dll ggml.dll ggml-base.dll ggml-cpu.dll) do (
        if exist "%ROOT%build\%%D" copy /y "%ROOT%build\%%D" "%BUILD_DIR%\%%D" >nul
    )
    echo         whisper-cli.exe и DLL (резервный STT)
) else (
    echo         [!] whisper-cli.exe не найден в build\ — резервный STT не попадёт в релиз.
)
:: ── Итоговый состав релиза ──────────────────────────────────────
echo.
echo   Что попадёт в zip:
echo     - Leron.Audio.exe и зависимые DLL
echo     - runtimes\ (нативные бэкенды Whisper.net: CPU/Vulkan/CUDA)
echo     - ggml-large-v3-turbo.bin (модель распознавания)
echo     - whisper-cli.exe + DLL (если был в build\)
echo   НЕ попадёт:
echo     - settings.json, history.jsonl, temp\, *.log
echo     - папка "для ии\", "Промт\", исходники code\, *.bat-скрипты
echo.
:: ── [5/5] Создание ZIP и открытие страницы релиза ──────────────
echo   [5/5] Создание архива...
set "ZIP_NAME=LERON-AUDIO-!VER!-win-x64.zip"
set "ZIP_PATH=%ROOT%release\!ZIP_NAME!"
if not exist "%ROOT%release" mkdir "%ROOT%release"
if exist "!ZIP_PATH!" del "!ZIP_PATH!"
powershell -NoProfile -Command "Compress-Archive -Path '%BUILD_DIR%\*' -DestinationPath '!ZIP_PATH!' -Force"
if !errorlevel! neq 0 (
    echo   [XX] Не удалось создать архив.
    pause
    exit /b 1
)
for %%A in ("!ZIP_PATH!") do set /a "ZIP_MB=%%~zA / 1048576"
echo   [OK] Архив: !ZIP_PATH! (~!ZIP_MB! MB)
echo.
echo ================================================================
echo   ГОТОВО!
echo ================================================================
echo.
echo   Что сделать в браузере:
echo.
echo   1. В поле "Tag version" введи:   !VER!
echo   2. В поле "Release title" введи: !TITLE!
echo   3. В поле "Describe" вставь:     !DESC!
echo   4. Нажми "attaching binaries"
echo   5. Выбери файл: !ZIP_PATH!
echo   6. Нажми "Publish release"
echo.
echo   Путь к архиву скопирован в буфер обмена —
echo   в поле выбора файла нажми Ctrl+V и Enter.
echo.
:: Копируем путь в буфер
echo|set /p="!ZIP_PATH!" | clip
start "" "https://github.com/TheLER0N/TRANSCRIPTOIN-LERON/releases/new?tag=!VER!&title=!TITLE!"
pause