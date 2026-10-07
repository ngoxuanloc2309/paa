@echo off
chcp 65001 > nul
title SimplePLC MCU Server Daemon (COM5) - Wire Profile V2
echo ================================================================================
echo    SimplePLC MCU Server Daemon (STM32 Simulator with Wire Profile V2)
echo ================================================================================
echo  * Cổng MCU:        COM5 (115200 bps, 8-N-1)
echo  * Giao tiếp Host:  COM10 (Dành cho SimplePLC Studio / Modbus Master)
echo  * Chức năng:       Dedicated Timers/Counters, Diag 0x0A20, Retain, Watchdog Lease
echo --------------------------------------------------------------------------------
echo  [HUONG DAN]:
echo    1. De cua so nay chay lien tuc (Server dang lang nghe tren COM5).
echo    2. Khoi dong ung dung SimplePLC Studio (WPF).
echo    3. Chon cong COM10 o thanh cong cu, nhan Nut "Ket noi" (Connect).
echo    4. Vao tab "Live Watch & Diag" de test cuong buc Tag, Watchdog Lease, Retain!
echo ================================================================================
echo.

dotnet run --project tools/SimplePLC.HardwareTest/SimplePLC.HardwareTest.csproj -- --server-only --mcu COM5 --baud 115200

pause
