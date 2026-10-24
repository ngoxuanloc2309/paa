@echo off
chcp 65001 > nul
title SimplePLC Full Hardware Test (COM7 ^<-^> COM9)
echo ================================================================================
echo    SimplePLC Full Hardware Test Runner (6 Steps End-to-End)
echo ================================================================================
echo  * Cổng MCU:        COM7
echo  * Cổng Studio:     COM9
echo  * Toc do:          115200 bps
echo --------------------------------------------------------------------------------
echo.

dotnet run --project tools/SimplePLC.HardwareTest/SimplePLC.HardwareTest.csproj -- --full --mcu COM7 --studio COM9 --baud 115200

echo.
pause
