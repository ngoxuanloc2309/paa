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
        Console.WriteLine("------------------------------------------------------------------");
        Console.WriteLine("Controls:");
        Console.WriteLine($"  [1..{Math.Min(8, (int)diCount)}]  Toggle Discrete Inputs");
        Console.WriteLine("  [+/-]   Increase/Decrease Analog Input AI0 (step 200mV)");
        Console.WriteLine("  [R]     Trigger Software Reboot");
        Console.WriteLine("  [F]     Trigger Factory Reset");
        Console.WriteLine("  [Q]     Quit");
        Console.WriteLine("==================================================================");

        var simulator = new McuReferenceSimulator(wireProfile);
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

        // Vòng lặp nhận phím điều khiển tương tác
        var layout = simulator.GetTagLayout();
        int ai0Value = 1000;
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

            // Nếu bật chế độ auto-signals: Tự động giả lập sóng tín hiệu công nghiệp
            if (autoSignals)
            {
                long nowMs = Environment.TickCount64;
                if (layout.DiCount > 0)
                {
                    // DI0: Chu kỳ 2 giây (1s ON, 1s OFF)
                    bool autoDi0 = (nowMs % 2000) < 1000;
                    if (autoDi0 != diStates[0])
                    {
                        diStates[0] = autoDi0;
                        simulator.Control.SetTagValue(layout.DiBase, autoDi0 ? 1 : 0);
                    }
                }

                if (layout.DiCount > 1)
                {
                    // DI1: Chu kỳ 4 giây (2s ON, 2s OFF)
                    bool autoDi1 = (nowMs % 4000) < 2000;
                    if (autoDi1 != diStates[1])
                    {
                        diStates[1] = autoDi1;
                        simulator.Control.SetTagValue((ushort)(layout.DiBase + 1), autoDi1 ? 1 : 0);
                    }
                }

                if (layout.AiCount > 0)
                {
                    // AI0: Dải 0..5000 mV tăng giảm hình sin chu kỳ 10 giây
                    double radians = (nowMs % 10000) / 10000.0 * 2.0 * Math.PI;
                    int waveAi0 = (int)(2500 + 2000 * Math.Sin(radians));
                    simulator.Control.SetTagValue(layout.AiBase, waveAi0);
                }

                if (layout.AiCount > 1)
                {
                    // AI1: Dải 0..10000 mV ramp tuyến tính chu kỳ 8 giây
                    int waveAi1 = (int)((nowMs % 8000) * 10000 / 8000);
                    simulator.Control.SetTagValue((ushort)(layout.AiBase + 1), waveAi1);
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
        Console.WriteLine("  --help,  -h          Show this help message");
    }
}
