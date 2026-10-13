@echo off
chcp 65001 > nul
title SimplePLC MCU Controller Simulator (COM7) - Wire Profile V2
echo ================================================================================
echo    SimplePLC MCU Controller Simulator (Modbus RTU - Auto Signals)
echo ================================================================================
echo  * Cổng MCU Server:    COM7 (115200 bps, 8-N-1, Slave ID 1)
echo  * Giao tiếp Studio:   COM9 (Chọn COM9 trên SimplePLC Studio và nhấn Kết nối)
echo  * Tính năng tự động:  DI0 (chớp 1s), DI1 (chớp 2s), AI0 (sóng sin 0..5V), AI1 (ramp)
echo --------------------------------------------------------------------------------
echo  [HƯỚNG DẪN TEST LIVE DEBUG / ONLINE MONITOR TRỰC TIẾP TRÊN GRAPH]:
echo    1. Để cửa sổ này chạy liên tục (MCU Controller đang lắng nghe trên COM7).
echo    2. Trên SimplePLC Studio: Chọn cổng COM9, Baud 115200, nhấn nút "Kết nối".
echo    3. Chuyển sang tab Logic Graph (FBD Canvas).
echo    4. Nhấn nút [ 👁 ] (Online Monitor / Live Debug) ở thanh dưới cùng (hoặc ấn F8).
echo    5. Dữ liệu từ MCU sẽ tự động lan truyền vào sơ đồ, dây dẫn và đèn trạng thái!
echo ================================================================================
echo.

dotnet run --project tools/SimplePLC.McuEmulator/SimplePLC.McuEmulator.csproj -- --port COM7 --baud 115200 --auto

pause
