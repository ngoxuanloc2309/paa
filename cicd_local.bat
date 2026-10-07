@echo off
setlocal
cd /d "%~dp0"
echo =================================================================
echo  SynaptiX IDE - Local CI/CD Runner
echo =================================================================

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\cicd_local.ps1" %*
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Local CI/CD failed with exit code %ERRORLEVEL%!
    exit /b %ERRORLEVEL%
)

echo.
echo [SUCCESS] Local CI/CD completed successfully!
exit /b 0
