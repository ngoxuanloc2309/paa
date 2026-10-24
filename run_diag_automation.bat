@echo off
chcp 65001 > nul
title SimplePLC Automated Diagnostic Scenarios (COM7 ^<-^> COM9)
echo ================================================================================
echo    SimplePLC Automated Diagnostic ^& Commissioning Scenarios Runner
echo ================================================================================
echo  * Cổng MCU:        COM7
echo  * Cổng Studio:     COM9
echo  * Toc do:          115200 bps
echo --------------------------------------------------------------------------------
echo.

dotnet run --project tools/SimplePLC.HardwareTest/SimplePLC.HardwareTest.csproj -- --diag --mcu COM7 --studio COM9 --baud 115200

echo.
pause
