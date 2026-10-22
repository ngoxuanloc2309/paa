using System.IO.Ports;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Enums;
using SimplePLC.Protocol.Models;

namespace SimplePLC.McuEmulator;

internal class Program
{
    private static async Task Main(string[] args)
    {
        string portName = "COM2";
        int baudRate = 115200;
        byte slaveId = 1;
        ushort diCount = 8;
        ushort doCount = 8;
        ushort aiCount = 4;
        ushort wireProfile = 2;
        bool hasCustomProfile = false;
        bool autoSignals = false;
        string capsProfile = "remote-io";
        ushort rs485PortCount = 2;

        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--port" || args[i] == "-p") && i + 1 < args.Length)
            {
                portName = args[++i].ToUpperInvariant();
            }
            else if ((args[i] == "--baud" || args[i] == "-b") && i + 1 < args.Length && int.TryParse(args[i + 1], out int b))
            {
                baudRate = b;
                i++;
            }
            else if ((args[i] == "--slave" || args[i] == "-s") && i + 1 < args.Length && byte.TryParse(args[i + 1], out byte s))
            {
                slaveId = s;
                i++;
            }
            else if ((args[i] == "--profile" || args[i] == "-pr") && i + 1 < args.Length && ushort.TryParse(args[i + 1], out ushort pr))
            {
                wireProfile = pr;
                i++;
            }
            else if (args[i] == "--di" && i + 1 < args.Length && ushort.TryParse(args[i + 1], out ushort di))
            {
                diCount = di;
                hasCustomProfile = true;
                i++;
            }
            else if (args[i] == "--do" && i + 1 < args.Length && ushort.TryParse(args[i + 1], out ushort doVal))
            {
                doCount = doVal;
                hasCustomProfile = true;
                i++;
            }
            else if (args[i] == "--ai" && i + 1 < args.Length && ushort.TryParse(args[i + 1], out ushort ai))
            {
                aiCount = ai;
                hasCustomProfile = true;
                i++;
            }
            else if ((args[i] == "--caps" || args[i] == "-c") && i + 1 < args.Length)
            {
                capsProfile = args[++i].ToLowerInvariant();
            }
            else if ((args[i] == "--rs485-ports" || args[i] == "-rs") && i + 1 < args.Length && ushort.TryParse(args[i + 1], out ushort rsCount))
            {
                rs485PortCount = rsCount;
                i++;
            }
            else if (args[i] == "--auto" || args[i] == "--auto-signals")
            {
                autoSignals = true;
            }
            else if (args[i] == "--help" || args[i] == "-h")
            {
                PrintHelp();
                return;
            }
        }

        Console.Title = $"SimplePLC Virtual MCU Emulator [{portName} @ {baudRate} baud, Slave {slaveId}, WireProfile V{wireProfile}]";
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==================================================================");
        Console.WriteLine("         SimplePLC Virtual STM32 MCU Emulator (Modbus RTU)        ");
        Console.WriteLine("==================================================================");
        Console.ResetColor();
        Console.WriteLine($"Port:         {portName}");
        Console.WriteLine($"Baud:         {baudRate}");
        Console.WriteLine($"Slave ID:     {slaveId}");
        Console.WriteLine($"Wire Profile: V{wireProfile} {(wireProfile == 2 ? "(Dedicated Function Blocks 0x0B00)" : "(Standard V1)")}");
        Console.WriteLine($"Firmware:     v2.0.0 (TinyUSB CDC + NanoModbus)");
        Console.WriteLine($"Profile:      DI={diCount}, DO={doCount}, AI={aiCount}");
        Console.WriteLine($"Capabilities: {capsProfile.ToUpperInvariant()} (0x0A08 Feature Gating)");
        Console.WriteLine($"RS-485 Ports: {rs485PortCount} ports (0x0A0A Auto-Sync)");
        Console.WriteLine("------------------------------------------------------------------");
        Console.WriteLine("Controls:");
        Console.WriteLine($"  [1..{Math.Min(8, (int)diCount)}]  Toggle Discrete Inputs (DI0..DI7)");
        Console.WriteLine("  [+/-]   Increase/Decrease Analog Input AI0 (step 200mV)");
        Console.WriteLine("  [</>]   Increase/Decrease Analog Input AI1 (step 200mV)");
        Console.WriteLine("  [A]     Toggle Auto Waveform Generator (All 23 App Blueprints)");
        Console.WriteLine("  [V]     Toggle VFLAG0 (Production Shift / Security Arm)");
        Console.WriteLine("  [S]     Sync RTC Clock with host computer");
        Console.WriteLine("  [C]     Toggle Capabilities (Remote I/O <-> IoT Gateway)");
        Console.WriteLine("  [R]     Trigger Software Reboot");
        Console.WriteLine("  [F]     Trigger Factory Reset");
        Console.WriteLine("  [Q]     Quit");
        Console.WriteLine("==================================================================");

        var simulator = new McuReferenceSimulator(wireProfile);
        bool isGateway = capsProfile != "remote-io" && capsProfile != "remoteio" && capsProfile != "no-mqtt";
        var initialCaps = isGateway
            ? SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE | SPLC_HardwareCapabilities.RTC | SPLC_HardwareCapabilities.ETHERNET | SPLC_HardwareCapabilities.WIFI | SPLC_HardwareCapabilities.MQTT_PUB | SPLC_HardwareCapabilities.MQTT_SUB | SPLC_HardwareCapabilities.MODBUS_MASTER
            : SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE | SPLC_HardwareCapabilities.MODBUS_MASTER;
        simulator.SetHardwareCapabilities(initialCaps);
        simulator.SetPhysicalRs485PortCount(rs485PortCount);
        if (hasCustomProfile)
        {
            ushort vflagCount = 32;
            ushort vregCount = 32;
            ushort vregRetainCount = 32;
            ushort counterCount = 8;
            ushort totalTags = (ushort)(diCount + doCount + aiCount + vflagCount + vregCount + vregRetainCount + counterCount);

            simulator.Control.Faults.OverrideResourceInfo = new SimplePLC.Protocol.Dto.DeviceResourceInfoDto
            {
                WireProfile = wireProfile,
                MaxRules = 100,
                RuntimeTagCount = totalTags,
                DigitalInputCount = diCount,
                DigitalOutputCount = doCount,
                AnalogInputCount = aiCount,
                VirtualFlagCount = vflagCount,
                VirtualRegisterCount = vregCount,
                RetentiveRegisterCount = vregRetainCount,
                CounterCount = counterCount
            };
        }

        var layout = simulator.GetTagLayout();

        // Khởi tạo baseline sẵn sàng cho toàn bộ 23 ví dụ (Blueprints):
        // 1. Kích hoạt VFLAG0 = 1 (Ca sản xuất / Arm mode) cho Andon Alert & Night Security
        if (layout.VflagCount > 0)
        {
            simulator.Control.SetTagValue(layout.VflagBase, 1);
        }

        // 2. Khởi tạo VREG_RETAIN1 = 5 (cho CTD Countdown nạp sẵn 5 phôi)
        if (layout.VregRetainCount > 1)
        {
            simulator.Control.SetTagValue((ushort)(layout.VregRetainBase + 1), 5);
        }

        // 3. Khởi tạo RTC clock theo giờ thực tế của máy tính
        var initNow = DateTimeOffset.Now;
        simulator.Control.SyncRtcClock((uint)initNow.ToUnixTimeSeconds(), (short)initNow.Offset.TotalMinutes);

        var server = new McuModbusRtuServer(simulator, slaveId);

        server.FrameReceived += frame =>
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] RX: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(Convert.ToHexString(frame));
            Console.ResetColor();
        };

        server.FrameSent += frame =>
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] TX: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(Convert.ToHexString(frame));
            Console.ResetColor();
        };

        server.LogMessage += msg =>
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
            Console.ResetColor();
        };

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // Khởi chạy Serial Port nếu cổng khả dụng
        SerialPort? serialPort = null;
        try
        {
            serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                DtrEnable = true,
                RtsEnable = true,
                ReadTimeout = 500,
                WriteTimeout = 500
            };

            serialPort.Open();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[STATUS] Serial port {portName} opened successfully. Listening for Modbus RTU frames...");
            Console.ResetColor();

            _ = Task.Run(() => server.RunAsync(serialPort.BaseStream, serialPort.BaseStream, cts.Token));
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[WARNING] Could not open serial port {portName}: {ex.Message}");
            Console.WriteLine("[INFO] Running in Interactive In-Memory Simulation mode.");
            Console.ResetColor();
        }

        // Vòng lặp nhận phím điều khiển tương tác & tạo sóng tín hiệu
        int ai0Value = 1000;
        int ai1Value = 2000;
        bool[] diStates = new bool[Math.Max(8, (int)layout.DiCount)];

        while (!cts.IsCancellationRequested)
        {
            if (!Console.IsInputRedirected && Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Q)
                {
                    break;
                }
                else if (key.Key == ConsoleKey.A)
                {
                    autoSignals = !autoSignals;
                    Console.ForegroundColor = autoSignals ? ConsoleColor.Green : ConsoleColor.Yellow;
                    Console.WriteLine($"[MODE] Auto Waveform Generator: {(autoSignals ? "ACTIVE (Tự động giả lập 23 ví dụ)" : "PAUSED (Điều khiển thủ công)")}");
                    Console.ResetColor();
                }
                else if (key.Key == ConsoleKey.V && layout.VflagCount > 0)
                {
                    int curV = simulator.Control.GetTagValue(layout.VflagBase);
                    int newV = curV == 0 ? 1 : 0;
                    simulator.Control.SetTagValue(layout.VflagBase, newV);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[FLAG] VFLAG0 (Shift / Arm) toggled -> {(newV == 1 ? "HIGH (1) [Ca sản xuất / Arm BẬT]" : "LOW (0) [Hết ca / Arm TẮT]")}");
                    Console.ResetColor();
                }
                else if (key.Key == ConsoleKey.C)
                {
                    isGateway = !isGateway;
                    var newCaps = isGateway
                        ? SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE | SPLC_HardwareCapabilities.RTC | SPLC_HardwareCapabilities.ETHERNET | SPLC_HardwareCapabilities.WIFI | SPLC_HardwareCapabilities.MQTT_PUB | SPLC_HardwareCapabilities.MQTT_SUB | SPLC_HardwareCapabilities.MODBUS_MASTER
                        : SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE | SPLC_HardwareCapabilities.MODBUS_MASTER;
                    simulator.SetHardwareCapabilities(newCaps);
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"[CAPS] Toggled Hardware Profile -> {(isGateway ? "IoT Gateway (Ethernet + Wi-Fi + MQTT + Modbus)" : "Remote I/O (NO MQTT, NO Modbus)")}");
                    Console.ResetColor();
                }
                else if (key.Key == ConsoleKey.S)
                {
                    var hostNow = DateTimeOffset.Now;
                    simulator.Control.SyncRtcClock((uint)hostNow.ToUnixTimeSeconds(), (short)hostNow.Offset.TotalMinutes);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[RTC] Synced RTC Clock -> {hostNow:yyyy-MM-dd HH:mm:ss} (UTC+{hostNow.Offset.TotalHours})");
                    Console.ResetColor();
                }

                int maxDiKey = Math.Min(8, (int)layout.DiCount);
                if (maxDiKey > 0 && key.Key >= ConsoleKey.D1 && key.Key < ConsoleKey.D1 + maxDiKey)
                {
                    int index = key.Key - ConsoleKey.D1;
                    diStates[index] = !diStates[index];
                    simulator.Control.SetTagValue((ushort)(layout.DiBase + index), diStates[index] ? 1 : 0);
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"[INPUT] DI{index} toggled -> {(diStates[index] ? "HIGH (1)" : "LOW (0)")}");
                    Console.ResetColor();
                }
                else if (layout.AiCount > 0 && (key.Key == ConsoleKey.OemPlus || key.Key == ConsoleKey.Add))
                {
                    ai0Value = Math.Min(10000, ai0Value + 200);
                    simulator.Control.SetTagValue(layout.AiBase, ai0Value);
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"[INPUT] AI0 changed -> {ai0Value} mV");
                    Console.ResetColor();
                }
                else if (layout.AiCount > 0 && (key.Key == ConsoleKey.OemMinus || key.Key == ConsoleKey.Subtract))
                {
                    ai0Value = Math.Max(0, ai0Value - 200);
                    simulator.Control.SetTagValue(layout.AiBase, ai0Value);
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"[INPUT] AI0 changed -> {ai0Value} mV");
                    Console.ResetColor();
                }
                else if (layout.AiCount > 1 && (key.Key == ConsoleKey.OemPeriod))
                {
                    ai1Value = Math.Min(10000, ai1Value + 200);
                    simulator.Control.SetTagValue((ushort)(layout.AiBase + 1), ai1Value);
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"[INPUT] AI1 changed -> {ai1Value} mV");
                    Console.ResetColor();
                }
                else if (layout.AiCount > 1 && (key.Key == ConsoleKey.OemComma))
                {
                    ai1Value = Math.Max(0, ai1Value - 200);
                    simulator.Control.SetTagValue((ushort)(layout.AiBase + 1), ai1Value);
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"[INPUT] AI1 changed -> {ai1Value} mV");
                    Console.ResetColor();
                }
                else if (key.Key == ConsoleKey.R)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[SYSTEM] Triggering Software Reboot...");
                    simulator.Control.SoftwareReboot();
                    Console.WriteLine("[SYSTEM] Reboot completed. ResetReason: SOFTWARE");
                    Console.ResetColor();
                }
                else if (key.Key == ConsoleKey.F)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[SYSTEM] Triggering Factory Reset...");
                    simulator.Control.FactoryReset();
                    Console.WriteLine("[SYSTEM] Flash cleared. ResetReason: POWER_ON");
                    Console.ResetColor();
                }
            }

            // Nếu bật chế độ auto-signals: Tự động giả lập sóng tín hiệu cho TOÀN BỘ 23 ví dụ (Blueprints)
            if (autoSignals)
            {
                long nowMs = Environment.TickCount64;

                // 1. DI0: Máy chạy (Machine Run) / TON On-Delay / Tower Light / CTD Reload
                // Chu kỳ 16 giây:
                // 0..7000ms: HIGH (1) [7 giây] -> Đủ thời gian cho TON 5,000ms đếm đủ và bật DO0!
                // 7000..16000ms: LOW (0) [9 giây] -> Reset TON về 0. Đồng thời tạo sườn xuống ON_FALL cho Andon Alert!
                if (layout.DiCount > 0)
                {
                    int t16 = (int)(nowMs % 16000);
                    bool autoDi0 = t16 < 7000;
                    if (autoDi0 != diStates[0])
                    {
                        diStates[0] = autoDi0;
                        simulator.Control.SetTagValue(layout.DiBase, autoDi0 ? 1 : 0);
                    }
                }

                // 2. DI1: Cấp liệu (Feed Button) / TOF Off-Delay / CTD Countdown
                // Chu kỳ 20 giây:
                // 0..5000ms: HIGH (1) [5 giây] -> Bật quạt DO1.
                // 5000..20000ms: LOW (0) [15 giây] -> Tắt tín hiệu; người dùng quan sát TOF 10s duy trì DO1 đến 15s rồi ngắt!
                if (layout.DiCount > 1)
                {
                    int t20 = (int)(nowMs % 20000);
                    bool autoDi1 = t20 < 5000;
                    if (autoDi1 != diStates[1])
                    {
                        diStates[1] = autoDi1;
                        simulator.Control.SetTagValue((ushort)(layout.DiBase + 1), autoDi1 ? 1 : 0);
                    }
                }

                // 3. DI2: Cảm biến sản lượng Line 1 & CTU Batch Counter (Mỗi 2.0 giây phát 1 xung)
                // Xung nhịp: HIGH 350ms, LOW 1650ms -> Cứ 2s tăng 1 đơn vị vào VREG_RETAIN0 và CTU!
                if (layout.DiCount > 2)
                {
                    int t2 = (int)(nowMs % 2000);
                    bool autoDi2 = t2 < 350;
                    if (autoDi2 != diStates[2])
                    {
                        diStates[2] = autoDi2;
                        simulator.Control.SetTagValue((ushort)(layout.DiBase + 2), autoDi2 ? 1 : 0);
                    }
                }

                // 4. DI3: Cảm biến phôi TP Pulse & CTU Reset (Chu kỳ 24 giây)
                // Tại 20.0s..20.6s: Phát xung HIGH 600ms -> Kích hoạt TP Pulse 1.5s và Reset CTU về 0!
                if (layout.DiCount > 3)
                {
                    int t24 = (int)(nowMs % 24000);
                    bool autoDi3 = t24 >= 20000 && t24 < 20600;
                    if (autoDi3 != diStates[3])
                    {
                        diStates[3] = autoDi3;
                        simulator.Control.SetTagValue((ushort)(layout.DiBase + 3), autoDi3 ? 1 : 0);
                    }
                }

                // 5. DI4: Dừng khẩn E-Stop & Cảm biến chuyển động ca đêm (Chu kỳ 30 giây)
                // Tại 26.0s..28.5s: Phát xung HIGH 2.5 giây -> Hú còi E-Stop DO3 và bật đèn an ninh DO7!
                if (layout.DiCount > 4)
                {
                    int t30 = (int)(nowMs % 30000);
                    bool autoDi4 = t30 >= 26000 && t30 < 28500;
                    if (autoDi4 != diStates[4])
                    {
                        diStates[4] = autoDi4;
                        simulator.Control.SetTagValue((ushort)(layout.DiBase + 4), autoDi4 ? 1 : 0);
                    }
                }

                // 6. DI5: Cảm biến phao mức cao Sump Pump (Chu kỳ 15 giây)
                // 0..4000ms: HIGH (1) [Nước dâng cao] -> Bơm thoát nước DO4 chạy
                // 4000..15000ms: LOW (0) [Nước rút] -> Bơm ngắt
                if (layout.DiCount > 5)
                {
                    int t15 = (int)(nowMs % 15000);
                    bool autoDi5 = t15 < 4000;
                    if (autoDi5 != diStates[5])
                    {
                        diStates[5] = autoDi5;
                        simulator.Control.SetTagValue((ushort)(layout.DiBase + 5), autoDi5 ? 1 : 0);
                    }
                }

                // 7. DI6: Cửa an toàn Safety Door (Bình thường 0 = ĐÓNG để Guard cho phép chạy)
                // Mỗi 25 giây, cửa mở ra (HIGH 1) trong 3.5 giây (20s đến 23.5s) -> Safety Interlock ngắt DO7!
                if (layout.DiCount > 6)
                {
                    int t25 = (int)(nowMs % 25000);
                    bool autoDi6 = t25 >= 20000 && t25 < 23500;
                    if (autoDi6 != diStates[6])
                    {
                        diStates[6] = autoDi6;
                        simulator.Control.SetTagValue((ushort)(layout.DiBase + 6), autoDi6 ? 1 : 0);
                    }

                    // 8. DI7: Nút khởi động băng tải khi cửa đóng (Phát xung lúc 6.0s..6.8s khi DI6 đang đóng = 0)
                    if (layout.DiCount > 7)
                    {
                        bool autoDi7 = t25 >= 6000 && t25 < 6800;
                        if (autoDi7 != diStates[7])
                        {
                            diStates[7] = autoDi7;
                            simulator.Control.SetTagValue((ushort)(layout.DiBase + 7), autoDi7 ? 1 : 0);
                        }
                    }
                }

                // 9. AI0: Cảm biến Áp suất khí nén / Mức nước / Báo động khẩn (Chu kỳ 24 giây)
                // Quét mượt mà hình sin dải toàn phần 1,200 mV đến 9,500 mV:
                // - < 2,000 mV: Kích hoạt Alarm Code #101
                // - < 4,000 mV: Hú còi áp suất thấp DO3
                // - 4,000 .. 8,000 mV: Nằm trong Cửa sổ áp suất danh định [40..80 bar] -> Van nạp DO6 đóng
                // - Toàn dải 0..10V: Khối SCALE đổi sang 0..16 bar và 0..5.0m nước
                if (layout.AiCount > 0)
                {
                    double rad0 = (nowMs % 24000) / 24000.0 * 2.0 * Math.PI;
                    int waveAi0 = (int)(5350 + 4150 * Math.Sin(rad0)); // Dải: [1200 .. 9500] mV
                    simulator.Control.SetTagValue(layout.AiBase, waveAi0);
                }

                // 10. AI1: Cảm biến Nhiệt độ bể dầu 4..20mA (Chu kỳ 18 giây)
                // Quét dải 2,200 mV đến 9,200 mV:
                // - > 8,500 mV (> 85°C): Kích hoạt quạt làm mát quá nhiệt DO2!
                // - Toàn dải: Khối SCALE đổi sang -20°C .. 100°C trên VREG1
                if (layout.AiCount > 1)
                {
                    double rad1 = (nowMs % 18000) / 18000.0 * 2.0 * Math.PI;
                    int waveAi1 = (int)(5700 + 3500 * Math.Sin(rad1)); // Dải: [2200 .. 9200] mV
                    simulator.Control.SetTagValue((ushort)(layout.AiBase + 1), waveAi1);
                }

                // 11. AI2 & AI3: Kênh tương tự phụ
                if (layout.AiCount > 2)
                {
                    int waveAi2 = (int)((nowMs % 10000) * 10000 / 10000);
                    simulator.Control.SetTagValue((ushort)(layout.AiBase + 2), waveAi2);
                }
                if (layout.AiCount > 3)
                {
                    int waveAi3 = (int)(5000 + 3000 * Math.Cos((nowMs % 12000) / 12000.0 * 2.0 * Math.PI));
                    simulator.Control.SetTagValue((ushort)(layout.AiBase + 3), waveAi3);
                }
            }

            simulator.ExecuteScanPass(50);
            await Task.Delay(50, cts.Token).ConfigureAwait(false);
        }

        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
        }

        Console.WriteLine("MCU Emulator stopped.");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: SimplePLC.McuEmulator [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --port,  -p <PORT>   Serial COM port name (default: COM2)");
        Console.WriteLine("  --baud,  -b <BAUD>   Baud rate (default: 115200)");
        Console.WriteLine("  --slave, -s <ID>     Modbus Slave ID (default: 1)");
        Console.WriteLine("  --rs485-ports, -rs <N> Physical RS-485 port count: 0, 1, 2, 4 (default: 2)");
        Console.WriteLine("  --help,  -h          Show this help message");
    }
}
