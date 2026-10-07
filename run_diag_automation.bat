@echo off
chcp 65001 > nul
title SimplePLC Automated Diagnostic Scenarios (COM5 ^<-^> COM10)
echo ================================================================================
echo    SimplePLC Automated Diagnostic ^& Commissioning Scenarios Runner
echo ================================================================================
echo  * Cổng MCU:        COM5
echo  * Cổng Studio:     COM10
echo  * Toc do:          115200 bps
echo --------------------------------------------------------------------------------
echo.

dotnet run --project tools/SimplePLC.HardwareTest/SimplePLC.HardwareTest.csproj -- --diag --mcu COM5 --studio COM10 --baud 115200

echo.
pause
