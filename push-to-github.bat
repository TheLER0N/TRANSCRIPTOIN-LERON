@echo off
chcp 65001 >nul 2>&1
setlocal enabledelayedexpansion
cd /d "%~dp0"
echo.
echo ================================================================
echo LERON-AUDIO · PUSH TO GITHUB
echo ================================================================
echo.
echo Репо: https://github.com/TheLER0N/TRANSCRIPTOIN-LERON
echo.
echo [1] Полная перезапись (force push) — удалит старую историю на GitHub
echo [2] Обновление — подтянуть историю и добавить коммит поверх
echo [3] Отмена
echo.
set /p "CHOICE= Выбери действие [1/2/3] > "
if "%CHOICE%"=="3" goto :end
if "%CHOICE%"=="1" goto :full_rewrite
if "%CHOICE%"=="2" goto :normal_push
goto :end

:full_rewrite
echo.
echo [!] Удаляю старую историю (.git)...
if exist ".git" (
    attrib -h -s -r ".git" /s /d 2>nul
    rmdir /s /q ".git"
)
git init -b main
git config core.autocrlf true
git config core.safecrlf warn
git remote add origin https://github.com/TheLER0N/TRANSCRIPTOIN-LERON.git 2>nul
goto :commit

:normal_push
echo.
echo [i] Обновление существующей истории...
if not exist ".git" (
    git init -b main
    git config core.autocrlf true
    git config core.safecrlf warn
    git remote add origin https://github.com/TheLER0N/TRANSCRIPTOIN-LERON.git 2>nul
)
echo [i] Синхронизация с GitHub (pull --rebase)...
git fetch origin 2>nul
git pull origin main --rebase --allow-unrelated-histories 2>nul
if errorlevel 1 (
    echo [XX] Синхронизация через rebase не удалась.
    echo     Локальная и удалённая истории разошлись.
    echo.
    echo [a] Попробовать merge (создаст merge-коммит)
    echo [f] Сделать force push (перезапишет GitHub — удалит чужие коммиты там)
    echo [c] Отмена
    echo.
    set /p "RECOVER= Выбери [a/f/c] > "
    if /i "!RECOVER!"=="a" goto :merge_fallback
    if /i "!RECOVER!"=="f" goto :force_fallback
    goto :end
)
goto :commit

:merge_fallback
echo [i] Пробую merge...
git merge origin/main --allow-unrelated-histories -m "merge: sync with GitHub" 2>nul
if errorlevel 1 (
    echo [XX] Merge тоже не прошёл — есть конфликты.
    echo     Открой VS Code или другой редактор, разреши конфликты и повтори скрипт.
    goto :end
)
goto :commit

:force_fallback
echo [!] Делаю force push. Старая история на GitHub будет заменена.
git push -u origin main --force
if !errorlevel! equ 0 goto :success
echo [XX] Force push тоже провалился. Проверь доступ к GitHub.
goto :end

:commit
echo [i] Создаю .gitignore...
powershell -NoProfile -ExecutionPolicy Bypass -Command "& { $gc = Get-Content '.gitignore' -Raw -ErrorAction SilentlyContinue; if (-not $gc) { Write-Host 'ERROR: .gitignore missing'; exit 1 } }"
if errorlevel 1 goto :end
echo [i] Вычищаю индекс Git от мусора...
git rm -r --cached . >nul 2>&1
echo [i] Добавляю файлы по .gitignore...
git add -A
echo.
set /p "MSG= Сообщение коммита [LERON-AUDIO update] > "
if "!MSG!"=="" set "MSG=LERON-AUDIO update"
git commit -m "!MSG!" --allow-empty
echo.
echo [i] Push в origin main...
git push -u origin main 2>nul
if !errorlevel! equ 0 goto :success
echo.
echo [XX] Push провалился. Сделай pull --rebase вручную или проверь доступ. Автоматический force push отключен.
goto :end

:success
echo.
echo ✓ Успешно запушено на GitHub!
goto :end

:end
echo.
echo Готово.
pause