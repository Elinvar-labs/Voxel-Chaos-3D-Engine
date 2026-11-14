@echo off
setlocal

set "SRC=%~dp0assets"
set "DST=%~dp0bin\Debug\net10.0\assets"

if not exist "%SRC%" (
    echo [ERROR] Source Folder not found: %SRC%
    exit /b 1
)

if not exist "%DST%" (
    echo [INFO] Create folder: %DST%
    mkdir "%DST%"
)

xcopy "%SRC%" "%DST%\" /E /I /Y /H /R

if errorlevel 1 (
    echo [ERROR] Copying with errors!
    exit /b 1
)

echo [OK] Copying is Done: %SRC% -^> %DST%
endlocal