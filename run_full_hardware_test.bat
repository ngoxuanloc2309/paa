@echo off
chcp 65001 > nul
title SimplePLC Full Hardware Test (COM5 ^<-^> COM10)
echo ================================================================================
echo    SimplePLC Full Hardware Test Runner (6 Steps End-to-End)
echo ================================================================================
echo  * Cổng MCU:        COM5
echo  * Cổng Studio:     COM10
echo  * Toc do:          115200 bps
echo --------------------------------------------------------------------------------
echo.

dotnet run --project tools/SimplePLC.HardwareTest/SimplePLC.HardwareTest.csproj -- --full --mcu COM5 --studio COM10 --baud 115200

echo.
pause
