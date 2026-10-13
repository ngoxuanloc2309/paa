using System.IO.Ports;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.HardwareTest;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        string mcuPort = "COM5";
        string studioPort = "COM10";
        int baudRate = 115200;
        byte slaveId = 1;
        string mode = "full"; // "full", "server", "diag", "menu"

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].ToLowerInvariant();
            if ((a == "--mcu" || a == "-m") && i + 1 < args.Length)
            {
                mcuPort = args[++i].ToUpperInvariant();
            }
            else if ((a == "--studio" || a == "-s") && i + 1 < args.Length)
            {
                studioPort = args[++i].ToUpperInvariant();
            }
            else if ((a == "--baud" || a == "-b") && i + 1 < args.Length && int.TryParse(args[i + 1], out int b))
            {
                baudRate = b;
                i++;
            }
            else if (a == "--slave" && i + 1 < args.Length && byte.TryParse(args[i + 1], out byte sl))
            {
                slaveId = sl;
                i++;
            }
            else if (a == "--server-only" || a == "--server" || a == "-srv")
            {
                mode = "server";
            }
            else if (a == "--diag" || a == "--diag-scenarios" || a == "-d")
            {
                mode = "diag";
            }
            else if (a == "--full" || a == "-f")
            {
                mode = "full";
            }
            else if (a == "--mqtt" || a == "-m" || a == "-mq")
            {
                mode = "mqtt";
            }
            else if (a == "--auto-mqtt" || a == "--suite" || a == "-am")
            {
                mode = "auto-mqtt";
            }
            else if (a == "--menu")
            {
                mode = "menu";
            }
        }

        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (mode == "menu")
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine("            SimplePLC Hardware Test & MCU Simulation Runner                     ");
            Console.WriteLine("================================================================================");
            Console.ResetColor();
            Console.WriteLine("Chọn chế độ hoạt động:");
            Console.WriteLine("  [1] Chạy MCU Server nền trên " + mcuPort + " (Sẵn sàng cho Studio kết nối " + studioPort + ")");
            Console.WriteLine("  [2] Chạy 5 kịch bản tự động kiểm thử Chẩn đoán & Cưỡng bức (Diag Scenarios)");
            Console.WriteLine("  [3] Chạy kiểm thử End-to-End toàn diện 6 bước (Full Hardware Test)");
            Console.WriteLine("  [4] Chạy kiểm thử truyền thông MQTT Telemetry (SPLC-SPEC-MQTT-001)");
            Console.WriteLine("  [5] Chạy Bộ Kiểm Thử Tự Động Độc Lập Phần Cứng (Clean Code Bug Hunter)");
            Console.WriteLine("  [0] Thoát");
            Console.Write("\nLựa chọn của bạn (mặc định 1): ");
            string? choice = Console.ReadLine()?.Trim();
            mode = choice switch
            {
                "2" => "diag",
                "3" => "full",
                "4" => "mqtt",
                "5" => "auto-mqtt",
                "0" => "exit",
                _ => "server"
            };

            if (mode == "exit") return 0;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        if (mode == "server")
        {
            return await RunServerOnlyAsync(mcuPort, baudRate, slaveId, cts.Token);
        }
        else if (mode == "diag")
        {
            return await RunDiagScenariosAsync(mcuPort, studioPort, baudRate, slaveId, cts.Token);
        }
        else if (mode == "mqtt")
        {
            return await RunMqttTestAsync(mcuPort, studioPort, baudRate, slaveId, cts.Token);
        }
        else if (mode == "auto-mqtt")
        {
            return await RunAutoMqttSuiteAsync(mcuPort, studioPort, baudRate, slaveId, cts.Token);
        }
        else
        {
            return await RunFullTestAsync(mcuPort, studioPort, baudRate, slaveId, cts.Token);
        }
    }

    // =========================================================================
    // CHẾ ĐỘ 1: MCU SERVER DAEMON TRÊN COM5 (CHO STUDIO KẾT NỐI TRỰC TIẾP COM10)
    // =========================================================================
    private static async Task<int> RunServerOnlyAsync(string mcuPort, int baudRate, byte slaveId, CancellationToken ct)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("     SimplePLC MCU Server Daemon (STM32 Simulator with Wire Profile V2)       ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();
        Console.WriteLine($"Cổng MCU:        {mcuPort} (115200 bps, 8-N-1)");
        Console.WriteLine($"Modbus Slave ID: {slaveId}");
        Console.WriteLine($"Tính năng:       Wire Profile V2 (Dedicated FB Timers/Counters, Diag 0x0A20, Retain)");
        Console.WriteLine($"Watchdog Lease:  3000 ms (Tự động thu hồi quyền & reset DO khi mất nhịp tim)");
        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"-> Đang mở cổng {mcuPort} và bắt đầu vòng lặp MCU Scan Engine (20ms)...");
        Console.ResetColor();

        SerialPort mcuSerial;
        try
        {
            mcuSerial = new SerialPort(mcuPort, baudRate, Parity.None, 8, StopBits.One)
            {
                DtrEnable = true,
                RtsEnable = true,
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
            mcuSerial.Open();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FATAL] Không thể mở cổng {mcuPort}: {ex.Message}");
            Console.ResetColor();
            return 1;
        }

        var simulator = new McuReferenceSimulator(wireProfile: 2);
        var mcuServer = new McuModbusRtuServer(simulator, slaveId);

        long rxFrames = 0;
        long txFrames = 0;

        mcuServer.FrameReceived += frame =>
        {
            Interlocked.Increment(ref rxFrames);
            string hex = Convert.ToHexString(frame);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [RX] {hex}");
            Console.ResetColor();
        };

        mcuServer.FrameSent += frame =>
        {
            Interlocked.Increment(ref txFrames);
            string hex = Convert.ToHexString(frame);
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [TX] {hex}");
            Console.ResetColor();
        };

        _ = Task.Run(() => mcuServer.RunAsync(mcuSerial.BaseStream, mcuSerial.BaseStream, ct), ct);

        _ = Task.Run(async () =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!ct.IsCancellationRequested)
            {
                uint delta = (uint)Math.Max(1, sw.ElapsedMilliseconds);
                sw.Restart();
                simulator.ExecuteScanPass(delta);
                await Task.Delay(20, ct).ConfigureAwait(false);
            }
        }, ct);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SẴN SÀNG] MCU Server đang lắng nghe trên {mcuPort}!");
        Console.WriteLine("Bạn có thể mở SimplePLC Studio, chọn cổng kết nối COM10 và nhấn Connect để trải nghiệm Live.");
        Console.WriteLine("Nhấn Ctrl+C để dừng MCU Server.\n");
        Console.ResetColor();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(3000, ct);
                var diagStatus = await simulator.ReadHoldingRegistersAsync(slaveId, ModbusRegisterMap.DiagBlockBaseAddress, ModbusRegisterMap.DiagBlockLength, ct);
                var diagState = (SPLC_DiagState)diagStatus[1];
                var diagFlags = (SPLC_DiagFlags)diagStatus[2];
                ushort leaseMs = diagStatus[3];
                var errCode = (SPLC_DiagErrorCode)diagStatus[4];

                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"--- [DASHBOARD] State: {diagState,-14} | Lease: {leaseMs,4}ms | Flags: {diagFlags,-20} | Err: {errCode,-12} | Frames: RX={rxFrames}, TX={txFrames} ---");
                Console.ResetColor();
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            mcuSerial.Close();
            Console.WriteLine("\n[INFO] Đã đóng cổng MCU Server an toàn.");
        }

        return 0;
    }

    // =========================================================================
    // CHẾ ĐỘ 2: 5 KỊCH BẢN TỰ ĐỘNG CHUYÊN SÂU CHẨN ĐOÁN & CƯỠNG BỨC (DIAG SCENARIOS)
    // =========================================================================
    private static async Task<int> RunDiagScenariosAsync(string mcuPort, string studioPort, int baudRate, byte slaveId, CancellationToken ct)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("   SimplePLC Automated Diagnostic & Commissioning Scenarios (COM5 <-> COM10)  ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        using var mcuSerial = new SerialPort(mcuPort, baudRate, Parity.None, 8, StopBits.One)
        {
            DtrEnable = true,
            RtsEnable = true,
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };
        mcuSerial.Open();

        var simulator = new McuReferenceSimulator(wireProfile: 2);
        var mcuServer = new McuModbusRtuServer(simulator, slaveId);

        _ = Task.Run(() => mcuServer.RunAsync(mcuSerial.BaseStream, mcuSerial.BaseStream, ct), ct);
        _ = Task.Run(async () =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    uint delta = (uint)Math.Max(1, sw.ElapsedMilliseconds);
                    sw.Restart();
                    simulator.ExecuteScanPass(delta);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SCAN EXCEPTION] {ex.Message}");
                }
                await Task.Delay(20, ct).ConfigureAwait(false);
            }
        }, ct);

        var transport = new UsbCdcTransport();
        await transport.OpenAsync(new UsbCdcOptions(studioPort, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)), ct);
        var client = new ModbusRtuClient(transport);
        var diag = new DiagnosticGateway(client);

        int passed = 0;
        int total = 5;

        try
        {
            // KỊCH BẢN 1: Chu kỳ Ủy thác Vận hành Cơ bản & Reset Lease bằng Nhịp tim
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[KỊCH BẢN 1/5] Chu kỳ Ủy thác Vận hành (Enter -> Heartbeat Reset -> Force DO -> Exit)");
            Console.ResetColor();

            var initStatus = await diag.ReadDiagnosticStatusAsync(slaveId, ct);
            Console.WriteLine($"  1.1 Trạng thái ban đầu: {initStatus.State} (Kỳ vọng: ENGINE_RUNNING)");
            Assert(initStatus.State == SPLC_DiagState.ENGINE_RUNNING, "Trạng thái ban đầu không phải ENGINE_RUNNING");

            var enterStatus = await diag.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.ENTER_DIAG, ct);
            Console.WriteLine($"  1.2 Sau ENTER_DIAG: State={enterStatus.State}, Lease={enterStatus.LeaseRemainingMs}ms, Flags={enterStatus.Flags}");
            Assert(enterStatus.State == SPLC_DiagState.DIAG_CONTROL && enterStatus.Flags.HasFlag(SPLC_DiagFlags.LEASE_ACTIVE), "Lỗi khi vào DIAG_CONTROL");

            // Chờ MCU scan ít nhất 1 lần để lease countdown bắt đầu (thay vì Delay cứng 500ms)
            await WaitUntilAsync(async () =>
            {
                var s = await diag.ReadDiagnosticStatusAsync(slaveId, ct);
                return s.LeaseRemainingMs < ModbusRegisterMap.DiagDefaultLeaseMs;
            }, timeoutMs: 2000, pollMs: 50,
               timeoutMessage: "Lease countdown did not start after ENTER_DIAG", ct: ct);

            var hbStatus = await diag.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.HEARTBEAT, ct);
            Console.WriteLine($"  1.3 Sau HEARTBEAT: Lease được reset về {hbStatus.LeaseRemainingMs}ms");
            Assert(hbStatus.LeaseRemainingMs >= 2500, "Lease không được gia hạn đúng");

            await diag.WriteTagValueAsync(slaveId, ModbusRegisterMap.DefaultLayout.DoBase, 1, ct);
            await diag.WriteTagValueAsync(slaveId, (ushort)(ModbusRegisterMap.DefaultLayout.DoBase + 1), 1, ct);
            var doRegs = await client.ReadHoldingRegistersAsync(slaveId, ModbusRegisterMap.GetRuntimeTagAddress(ModbusRegisterMap.DefaultLayout.DoBase), 4, ct);
            int do0 = RegisterCodec.DecodeInt32(doRegs.AsSpan(0, 2));
            int do1 = RegisterCodec.DecodeInt32(doRegs.AsSpan(2, 2));
            Console.WriteLine($"  1.4 Đọc lại ngõ ra cưỡng bức: DO0={do0}, DO1={do1} (Kỳ vọng: 1, 1)");
            Assert(do0 == 1 && do1 == 1, "Cưỡng bức DO thất bại");

            var exitStatus = await diag.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.EXIT_DIAG, ct);
            Console.WriteLine($"  1.5 Sau EXIT_DIAG: State={exitStatus.State}, Flags={exitStatus.Flags}");
            Assert(exitStatus.State == SPLC_DiagState.ENGINE_RUNNING, "Không thể thoát về ENGINE_RUNNING");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  -> [PASS] Kịch bản 1 hoàn thành xuất sắc!");
            Console.ResetColor();
            passed++;

            // KỊCH BẢN 2: Mất Nhịp tim & Tự động Thu hồi Quyền (Lease Watchdog Fail-Safe)
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[KỊCH BẢN 2/5] Mất Nhịp tim & Tự động Thu hồi Quyền An toàn (Lease Timeout Watchdog)");
            Console.ResetColor();

            await diag.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.ENTER_DIAG, ct);
            await diag.WriteTagValueAsync(slaveId, (ushort)(ModbusRegisterMap.DefaultLayout.DoBase + 2), 1, ct);
            await diag.WriteTagValueAsync(slaveId, (ushort)(ModbusRegisterMap.DefaultLayout.DoBase + 3), 1, ct);
            Console.WriteLine("  2.1 Đã vào DIAG_CONTROL và kích hoạt ngõ ra DO2=1, DO3=1.");
            Console.WriteLine("  2.2 Giả lập ngắt đường truyền nhịp tim, chờ Watchdog đếm ngược 3000ms...");

            // Chờ MCU tự chuyển về ENGINE_RUNNING khi Watchdog Lease hết hạn (thay vì cứng 3500ms)
            // Dùng timeout 5000ms để có đủ margin dù máy tính đang chậm hay đang tải
            await WaitUntilAsync(async () =>
            {
                var s = await diag.ReadDiagnosticStatusAsync(slaveId, ct);
                return s.State == SPLC_DiagState.ENGINE_RUNNING;
            }, timeoutMs: 5000, pollMs: 100,
               timeoutMessage: "MCU did not auto-revoke DIAG_CONTROL after lease expired", ct: ct);


            var expiredStatus = await diag.ReadDiagnosticStatusAsync(slaveId, ct);
            Console.WriteLine($"  2.3 Trạng thái sau 3.5s: State={expiredStatus.State}, ErrorCode={expiredStatus.ErrorCode}, Flags={expiredStatus.Flags}");
            Assert(expiredStatus.State == SPLC_DiagState.ENGINE_RUNNING, "MCU không tự động thu hồi quyền về ENGINE_RUNNING");
            Assert(expiredStatus.ErrorCode == SPLC_DiagErrorCode.LEASE_EXPIRED, "Mã lỗi không phải LEASE_EXPIRED");

            var failSafeDoRegs = await client.ReadHoldingRegistersAsync(slaveId, ModbusRegisterMap.GetRuntimeTagAddress(ModbusRegisterMap.DefaultLayout.DoBase), 8, ct);
            int do2 = RegisterCodec.DecodeInt32(failSafeDoRegs.AsSpan(4, 2));
            int do3 = RegisterCodec.DecodeInt32(failSafeDoRegs.AsSpan(6, 2));
            Console.WriteLine($"  2.4 Kiểm tra ngõ ra Fail-Safe: DO2={do2}, DO3={do3} (Kỳ vọng: 0, 0)");
            Assert(do2 == 0 && do3 == 0, "Ngõ ra DO không được tự động reset về 0 an toàn khi hết hạn lease");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  -> [PASS] Kịch bản 2 hoàn thành xuất sắc: Fail-Safe hoạt động hoàn hảo!");
            Console.ResetColor();
            passed++;

            // KỊCH BẢN 3: Bảo vệ Thanh ghi Lưu trữ Retentive Memory (Dirty Tracking & Commit/Discard)
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[KỊCH BẢN 3/5] Bảo vệ Bộ nhớ Lưu trữ Retentive (Dirty Tracking & Commit / Discard)");
            Console.ResetColor();

            await diag.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.ENTER_DIAG, ct);
            await diag.WriteTagValueAsync(slaveId, ModbusRegisterMap.DefaultLayout.VregRetainBase, 7777, ct);

            var dirtyStatus = await diag.ReadDiagnosticStatusAsync(slaveId, ct);
            Console.WriteLine($"  3.1 Ghi VREG_RETAIN0 = 7777 -> Flags: {dirtyStatus.Flags} (Có cờ RETAIN_DIRTY)");
            Assert(dirtyStatus.Flags.HasFlag(SPLC_DiagFlags.RETAIN_DIRTY), "Cờ RETAIN_DIRTY không được bật");

            var rejectExit = await diag.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.EXIT_DIAG, ct);
            Console.WriteLine($"  3.2 Cố ý thoát khi chưa Commit -> ErrorCode={rejectExit.ErrorCode}, State={rejectExit.State}");
            Assert(rejectExit.ErrorCode == SPLC_DiagErrorCode.RETAIN_DIRTY && rejectExit.State == SPLC_DiagState.DIAG_CONTROL, "Thoát không bị chặn khi Retain bị dirty");

            var discardStatus = await diag.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.DISCARD_RETAIN, ct);
            Console.WriteLine($"  3.3 Phát lệnh DISCARD_RETAIN -> Flags: {discardStatus.Flags}");
            Assert(!discardStatus.Flags.HasFlag(SPLC_DiagFlags.RETAIN_DIRTY), "Cờ RETAIN_DIRTY không bị xóa sau Discard");

            var cleanExit = await diag.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.EXIT_DIAG, ct);
            Assert(cleanExit.State == SPLC_DiagState.ENGINE_RUNNING, "Không thể thoát sạch sau Discard");

            await diag.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.ENTER_DIAG, ct);
            await diag.WriteTagValueAsync(slaveId, (ushort)(ModbusRegisterMap.DefaultLayout.VregRetainBase + 1), 9999, ct);
            var commitStatus = await diag.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.COMMIT_RETAIN, ct);
            Console.WriteLine($"  3.4 Ghi VREG_RETAIN1 = 9999 và COMMIT_RETAIN -> Flags: {commitStatus.Flags}");
            Assert(!commitStatus.Flags.HasFlag(SPLC_DiagFlags.RETAIN_DIRTY), "Cờ RETAIN_DIRTY không bị xóa sau Commit");

            await diag.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.EXIT_DIAG, ct);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  -> [PASS] Kịch bản 3 hoàn thành xuất sắc: Retentive Memory được bảo vệ tuyệt đối!");
            Console.ResetColor();
            passed++;

            // KỊCH BẢN 4: Chốt Khóa An toàn (Safety Interlocks & Phân quyền Vùng nhớ)
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[KỊCH BẢN 4/5] Chốt Khóa An toàn Khi Vận hành Tự động (Safety Interlocks)");
            Console.ResetColor();

            bool doBlocked = false;
            try
            {
                await diag.WriteTagValueAsync(slaveId, ModbusRegisterMap.DefaultLayout.DoBase, 1, ct);
            }
            catch { doBlocked = true; }
            Console.WriteLine($"  4.1 Thử ghi DO khi đang ở ENGINE_RUNNING: {(doBlocked ? "BỊ TỪ CHỐI (ĐÚNG)" : "BỊ LỌT (SAI)")}");
            Assert(doBlocked, "Safety Interlock thất bại: cho phép ghi DO khi chưa vào chế độ Diag");

            bool vregBlocked = false;
            try
            {
                await diag.WriteTagValueAsync(slaveId, ModbusRegisterMap.DefaultLayout.VregBase, 100, ct);
            }
            catch { vregBlocked = true; }
            Console.WriteLine($"  4.2 Thử ghi VREG khi đang ở ENGINE_RUNNING: {(vregBlocked ? "BỊ TỪ CHỐI (ĐÚNG)" : "BỊ LỌT (SAI)")}");
            Assert(vregBlocked, "Safety Interlock thất bại: cho phép ghi VREG khi chưa vào chế độ Diag");

            var invalidHb = await diag.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.HEARTBEAT, ct);
            Console.WriteLine($"  4.3 Gửi HEARTBEAT khi ở ENGINE_RUNNING: ErrorCode={invalidHb.ErrorCode}");
            Assert(invalidHb.ErrorCode == SPLC_DiagErrorCode.INVALID_COMMAND, "MCU chấp nhận heartbeat ngoài chế độ Diag");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  -> [PASS] Kịch bản 4 hoàn thành xuất sắc: Khóa an toàn hoạt động chính xác!");
            Console.ResetColor();
            passed++;

            // KỊCH BẢN 5: Cưỡng bức Hàng loạt & Nút Nhả Toàn cục Khẩn cấp (Emergency Release All)
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[KỊCH BẢN 5/5] Cưỡng bức Hàng loạt & Nhả Khẩn cấp Toàn bộ (Emergency Release All)");
            Console.ResetColor();

            await diag.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.ENTER_DIAG, ct);
            for (ushort i = 0; i < 8; i++)
            {
                await diag.WriteTagValueAsync(slaveId, (ushort)(ModbusRegisterMap.DefaultLayout.DoBase + i), 1, ct);
            }
            Console.WriteLine("  5.1 Đã cưỡng bức đồng loạt 8 ngõ ra DO0..DO7 = 1.");

            // Nhả toàn bộ: Reset DO về 0 và thoát Diag
            for (ushort i = 0; i < 8; i++)
            {
                await diag.WriteTagValueAsync(slaveId, (ushort)(ModbusRegisterMap.DefaultLayout.DoBase + i), 0, ct);
            }
            await diag.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.EXIT_DIAG, ct);

            var verifyDoRegs = await client.ReadHoldingRegistersAsync(slaveId, ModbusRegisterMap.GetRuntimeTagAddress(ModbusRegisterMap.DefaultLayout.DoBase), 16, ct);
            bool allZero = true;
            for (int i = 0; i < 8; i++)
            {
                int val = RegisterCodec.DecodeInt32(verifyDoRegs.AsSpan(i * 2, 2));
                if (val != 0) allZero = false;
            }
            Console.WriteLine($"  5.2 Đọc lại 8 ngõ ra DO sau khi Release All: {(allZero ? "TẤT CẢ = 0 (AN TOÀN)" : "CÒN NGÕ RA CHƯA RESET")}");
            Assert(allZero, "Không thể reset toàn bộ DO về 0");

            var finalStatus = await diag.ReadDiagnosticStatusAsync(slaveId, ct);
            Assert(finalStatus.State == SPLC_DiagState.ENGINE_RUNNING, "Chưa trở về ENGINE_RUNNING");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  -> [PASS] Kịch bản 5 hoàn thành xuất sắc!");
            Console.ResetColor();
            passed++;
        }
        finally
        {
            await transport.DisposeAsync();
            mcuSerial.Close();
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine($"   TẤT CẢ KỊCH BẢN CHẨN ĐOÁN HOÀN TẤT: {passed}/{total} KỊCH BẢN (100% THÀNH CÔNG) ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        return 0;
    }

    // =========================================================================
    // CHẾ ĐỘ 3: KIỂM THỬ END-TO-END TOÀN DIỆN 6 BƯỚC (FULL HARDWARE TEST)
    // =========================================================================
    private static async Task<int> RunFullTestAsync(string mcuPort, string studioPort, int baudRate, byte slaveId, CancellationToken ct)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("        SimplePLC Hardware Physical UART Test Runner (COM5 <-> COM10)         ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();
        Console.WriteLine($"MCU Server Port:    {mcuPort} (Simulates STM32 Firmware with Wire Profile V2)");
        Console.WriteLine($"Studio Client Port: {studioPort} (Simulates Studio Modbus Master Transport)");
        Console.WriteLine($"Baud Rate:          {baudRate} bps, 8-N-1");
        Console.WriteLine($"Modbus Slave ID:    {slaveId}");
        Console.WriteLine("--------------------------------------------------------------------------------");

        using var mcuSerial = new SerialPort(mcuPort, baudRate, Parity.None, 8, StopBits.One)
        {
            DtrEnable = true,
            RtsEnable = true,
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };
        mcuSerial.Open();

        var simulator = new McuReferenceSimulator(wireProfile: 2);
        var mcuServer = new McuModbusRtuServer(simulator, slaveId);

        int rxCount = 0;
        int txCount = 0;
        mcuServer.FrameReceived += frame =>
        {
            Interlocked.Increment(ref rxCount);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  [MCU RX] {Convert.ToHexString(frame)}");
            Console.ResetColor();
        };
        mcuServer.FrameSent += frame =>
        {
            Interlocked.Increment(ref txCount);
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  [MCU TX] {Convert.ToHexString(frame)}");
            Console.ResetColor();
        };

        _ = Task.Run(() => mcuServer.RunAsync(mcuSerial.BaseStream, mcuSerial.BaseStream, ct), ct);
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                simulator.ExecuteScanPass(20);
                await Task.Delay(20, ct).ConfigureAwait(false);
            }
        }, ct);

        var transport = new UsbCdcTransport();
        await transport.OpenAsync(new UsbCdcOptions(studioPort, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)), ct);
        var client = new ModbusRtuClient(transport);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[OK] Physical serial link established between {mcuPort} and {studioPort}!\n");
        Console.ResetColor();

        int passedSteps = 0;
        int totalSteps = 7;

        try
        {
            // TEST STEP 1: Handshake & DeviceDescriptor (0x0000, 10 registers)
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("TEST STEP 1: Handshake & DeviceDescriptor Discovery (0x0000, 10 regs)");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ResetColor();

            var descRegs = await client.ReadHoldingRegistersAsync(slaveId, 0x0000, 10, ct);
            var desc = RegisterCodec.DecodeDeviceDescriptor(descRegs);

            Console.WriteLine($"  -> Device Class:     {desc.DeviceClass}");
            Console.WriteLine($"  -> Device Variant:   0x{desc.DeviceVariant:X4}");
            Console.WriteLine($"  -> Protocol Version: {desc.ProtocolVersion}");
            Console.WriteLine($"  -> Rule Format Ver:  {desc.RuleFormatVersion}");
            Console.WriteLine($"  -> Firmware Version: {desc.FwVersionString}");
            Console.WriteLine($"  -> Hardware Version: {desc.HwVersionString}");

            if ((desc.ProtocolVersion == 1 || desc.ProtocolVersion == 2) && 
                (desc.RuleFormatVersion == 1 || desc.RuleFormatVersion == 7))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  [PASS] Step 1: DeviceDescriptor matches SimplePLC wire contract (Protocol v{desc.ProtocolVersion}, RuleFormat v{desc.RuleFormatVersion})!\n");
                Console.ResetColor();
                passedSteps++;
            }
            else
            {
                throw new Exception($"Step 1 Failed: ProtocolVersion={desc.ProtocolVersion}, RuleFormatVersion={desc.RuleFormatVersion}.");
            }

            // TEST STEP 2: DeviceResourceInfo & WireProfile V2 Verification (0x0020, 10 registers)
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("TEST STEP 2: DeviceResourceInfo & WireProfile V2 Verification (0x0020, 10 regs)");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ResetColor();

            var resRegs = await client.ReadHoldingRegistersAsync(slaveId, 0x0020, 10, ct);
            var res = RegisterCodec.DecodeDeviceResourceInfo(resRegs);

            Console.WriteLine($"  -> Wire Profile:    V{res.WireProfile} ({(res.WireProfile == 2 ? "V2.0 Dedicated FB Superset" : "V1.0")})");
            Console.WriteLine($"  -> Max Rules:       {res.MaxRules}");
            Console.WriteLine($"  -> Digital Inputs:  {res.DigitalInputCount}");
            Console.WriteLine($"  -> Digital Outputs: {res.DigitalOutputCount}");
            Console.WriteLine($"  -> Analog Inputs:   {res.AnalogInputCount}");
            Console.WriteLine($"  -> Total Tags:      {res.RuntimeTagCount}");

            if (res.WireProfile == 2 && res.DigitalInputCount == 8 && res.DigitalOutputCount == 8)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  [PASS] Step 2: WireProfile V2 correctly reported over UART!\n");
                Console.ResetColor();
                passedSteps++;
            }
            else
            {
                throw new Exception($"Step 2 Failed: Expected WireProfile=2, got {res.WireProfile}.");
            }

            // TEST STEP 3: Telemetry & Tag Polling (0x0800 Health & 0x0900 Tags)
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("TEST STEP 3: Health & Tag Telemetry Polling (0x0800 Health & 0x0900 Tags)");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ResetColor();

            var healthRegs = await client.ReadHoldingRegistersAsync(slaveId, 0x0800, 10, ct);
            var health = RegisterCodec.DecodeDeviceHealth(healthRegs);
            Console.WriteLine($"  -> Scan Time:       {health.ScanTimeMs} ms (Max: {health.MaxScanTimeMs} ms)");
            Console.WriteLine($"  -> CPU Load:        {health.CpuLoadPercent}%");
            Console.WriteLine($"  -> RAM Usage:       {health.RamUsagePercent}%");

            var tagRegs = await client.ReadHoldingRegistersAsync(slaveId, 0x0900, 32, ct);
            Console.WriteLine($"  -> Read {tagRegs.Length} tag registers from 0x0900 successfully.");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [PASS] Step 3: Real-time telemetry streaming over physical cable!\n");
            Console.ResetColor();
            passedSteps++;

            // TEST STEP 4: Wire Contract V2 Dedicated Function Block Subsystem (0x0B00..0x0B7F)
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("TEST STEP 4: Wire Contract V2 Dedicated Function Block Execution (0x0B00)");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ResetColor();

            var t0Initial = await client.ReadHoldingRegistersAsync(slaveId, 0x0B00, 8, ct);
            var t0DecodedInitial = FunctionBlockCodec.DecodeTimer(t0Initial);
            Console.WriteLine($"  -> Initial Timer 0 Mode: {t0DecodedInitial.Mode} (Expected: DISABLED)");

            var tonDto = new FbTimerRecordDto
            {
                Mode = SPLC_TimerMode.TON,
                PresetMs = 1000,
                ElapsedMs = 0,
                In = true,
                Reset = false,
                Running = false,
                Q = false
            };
            var tonRegs = new ushort[8];
            FunctionBlockCodec.EncodeTimer(tonDto, tonRegs);
            Console.WriteLine("  -> Sending FC16 to 0x0B00: Configure TON Timer (PT = 1000ms, IN = 1)...");
            await client.WriteMultipleRegistersAsync(slaveId, 0x0B00, tonRegs, ct);


            // Chờ MCU scan pass đã xử lý timer ít nhất 1 lần (ElapsedMs > 0) thay vì cứng 200ms
            await WaitUntilAsync(async () =>
            {
                var regs = await client.ReadHoldingRegistersAsync(slaveId, 0x0B00, 8, ct);
                return FunctionBlockCodec.DecodeTimer(regs).ElapsedMs > 0;
            }, timeoutMs: 1000, pollMs: 50,
               timeoutMessage: "TON Timer did not start counting after IN=1", ct: ct);

            var t0Mid = await client.ReadHoldingRegistersAsync(slaveId, 0x0B00, 8, ct);
            var t0DecodedMid = FunctionBlockCodec.DecodeTimer(t0Mid);
            Console.WriteLine($"  -> Timer 0 mid-check: ET={t0DecodedMid.ElapsedMs} ms, Running={t0DecodedMid.Running}, Q={t0DecodedMid.Q}");


            // Dùng WaitUntilAsync thay cho vòng while-poll thủ công — đồng nhất và không flaky
            Console.WriteLine("  -> Polling Timer 0 until Q = 1 (Preset = 1000ms)...");
            FbTimerRecordDto t0DecodedDone = t0DecodedMid;
            await WaitUntilAsync(async () =>
            {
                var pollRegs = await client.ReadHoldingRegistersAsync(slaveId, 0x0B00, 8, ct);
                t0DecodedDone = FunctionBlockCodec.DecodeTimer(pollRegs);
                Console.WriteLine($"  -> Poll Timer 0: ET={t0DecodedDone.ElapsedMs} ms, Running={t0DecodedDone.Running}, Q={t0DecodedDone.Q}");
                return t0DecodedDone.Q;
            }, timeoutMs: 3000, pollMs: 200,
               timeoutMessage: "TON Timer Q did not trip within 3000ms preset window", ct: ct);


            if (t0DecodedDone.Q && t0DecodedDone.ElapsedMs >= 1000)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  [PASS] Step 4a: Dedicated Function Block V2 Timer ran and tripped Q over UART!");
                Console.ResetColor();
            }
            else
            {
                throw new Exception($"Step 4a Failed: Timer 0 did not trip Q. Q={t0DecodedDone.Q}, ET={t0DecodedDone.ElapsedMs}.");
            }

            // Step 4b: Wire Contract V2 Dedicated Counter Subsystem (0x0B40, CTU with CV = COUNTER0)
            Console.WriteLine("  -> Step 4b: Testing Dedicated Counter 0 (CTU, PV = 3, CU pulse test)...");
            var ctuDto = new FbCounterRecordDto
            {
                Mode = SPLC_CounterMode.CTU,
                PresetValue = 3,
                CurrentValue = 0,
                Cu = false,
                Cd = false,
                Reset = false,
                Q = false,
                RetainTagIndex = 84 // VREG_RETAIN0 or tag index
            };
            var ctuRegs = new ushort[8];
            FunctionBlockCodec.EncodeCounter(ctuDto, ctuRegs);
            // Write max 3 blocks (24 regs) - here 1 block = 8 regs to 0x0B40
            await client.WriteMultipleRegistersAsync(slaveId, 0x0B40, ctuRegs, ct);

            // Gửi 3 xung CU (0 -> 1 -> 0)
            for (int pulse = 1; pulse <= 3; pulse++)
            {
                var curRegs = await client.ReadHoldingRegistersAsync(slaveId, 0x0B40, 8, ct);
                var curDecoded = FunctionBlockCodec.DecodeCounter(curRegs);
                curDecoded.Cu = true;
                FunctionBlockCodec.EncodeCounter(curDecoded, ctuRegs);
                await client.WriteMultipleRegistersAsync(slaveId, 0x0B40, ctuRegs, ct);
                await Task.Delay(50, ct);

                // Sau 50ms, MCU scan pass đã xử lý sườn lên và tăng CV. Đọc lại CV mới nhất trước khi hạ CU về false!
                curRegs = await client.ReadHoldingRegistersAsync(slaveId, 0x0B40, 8, ct);
                curDecoded = FunctionBlockCodec.DecodeCounter(curRegs);
                curDecoded.Cu = false;
                FunctionBlockCodec.EncodeCounter(curDecoded, ctuRegs);
                await client.WriteMultipleRegistersAsync(slaveId, 0x0B40, ctuRegs, ct);
                await Task.Delay(50, ct);
            }

            var ctuReadbackRegs = await client.ReadHoldingRegistersAsync(slaveId, 0x0B40, 8, ct);
            var ctuReadback = FunctionBlockCodec.DecodeCounter(ctuReadbackRegs);
            Console.WriteLine($"  -> Counter 0 Readback: Mode={ctuReadback.Mode}, CV={ctuReadback.CurrentValue}, PV={ctuReadback.PresetValue}, Q={ctuReadback.Q}");
            Assert(ctuReadback.CurrentValue == 3, $"Counter 0 CV mismatch: expected 3, got {ctuReadback.CurrentValue}");
            Assert(ctuReadback.Q, "Counter 0 Q must be true after reaching PV=3");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [PASS] Step 4b: Dedicated Counter CTU ran and tripped Q=1 at PV=3 over UART!\n");
            Console.ResetColor();
            passedSteps++;

            // TEST STEP 5: Staging & Atomic Rule Deployment (0x9000 & 0xA000)
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("TEST STEP 5: Staging & Atomic Rule Commit (0x9010 Staging -> 0xA000 Commit -> 0x0100 Active)");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ResetColor();

            var writer = new RuleTableWriter(client);
            var sampleRules = new[]
            {
                new RuleRecordDto
                {
                    Enabled = true,
                    TriggerTag = 0,
                    TriggerType = SPLC_TriggerType.ON_CHANGE,
                    CompareOp = SPLC_CompareOp.EQ,
                    ThresholdLo = 1,
                    ActionTag = 8,
                    ActionType = SPLC_ActionType.SET_TAG,
                    ActionParam = 1
                }
            };

            Console.WriteLine("  -> Deploying rules via RuleTableWriter (Staging 0x9010 + Commit 0xA000)...");
            var deployResult = await writer.DeployRulesAsync(slaveId, sampleRules, ct);
            Console.WriteLine($"  -> Deploy Result: Success={deployResult.IsSuccess}, Status={deployResult.ConfigStatus}, Error={deployResult.ErrorCode}");

            if (deployResult.IsSuccess)
            {
                Console.WriteLine("  -> Reading back Active Rule Table from 0x0100...");
                var activeRegs = await client.ReadHoldingRegistersAsync(slaveId, 0x0100, 16, ct);
                var activeRules = RegisterCodec.DecodeRuleRecords(activeRegs, 1);
                bool ruleMatch = activeRules.Length == 1 && activeRules[0].ActionTag == 8 && activeRules[0].ActionParam == 1;
                Console.WriteLine($"  -> Active Rule Table Match: {(ruleMatch ? "100% IDENTICAL" : "MISMATCH")}");

                if (ruleMatch)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  [PASS] Step 5a: Simple Rule deployed and verified via physical UART!");
                    Console.ResetColor();
                }
                else
                {
                    throw new Exception("Step 5a Failed: Active Rule Table content mismatch.");
                }

                // Step 5b: Thử nghiệm nạp cặp luật bù trừ sinh từ Analog Trigger -> Timer TON (AI0 > 80, PT = 500ms)
                Console.WriteLine("  -> Step 5b: Deploying Trigger -> Timer TON rules (AI0 > 80 for 500ms -> DO1 = 1, AI0 <= 80 -> DO1 = 0)...");
                var timerRules = new[]
                {
                    new RuleRecordDto
                    {
                        Enabled = true,
                        TriggerTag = 16, // AI0
                        TriggerType = SPLC_TriggerType.ON_CHANGE,
                        CompareOp = SPLC_CompareOp.GT,
                        ThresholdLo = 80,
                        ForMs = 500,
                        ActionTag = 9, // DO1
                        ActionType = SPLC_ActionType.SET_TAG,
                        ActionParam = 1
                    },
                    new RuleRecordDto
                    {
                        Enabled = true,
                        TriggerTag = 16, // AI0
                        TriggerType = SPLC_TriggerType.ON_CHANGE,
                        CompareOp = SPLC_CompareOp.LTE,
                        ThresholdLo = 80,
                        ForMs = 0,
                        ActionTag = 9, // DO1
                        ActionType = SPLC_ActionType.SET_TAG,
                        ActionParam = 0
                    }
                };
                var deployTimerRulesResult = await writer.DeployRulesAsync(slaveId, timerRules, ct);
                Assert(deployTimerRulesResult.IsSuccess, "Failed to deploy Trigger->Timer rules");

                // Thử nghiệm thực thi: Giả lập AI0 = 100 (> 80), chờ 600ms xem DO1 có tự động bật lên 1 không
                Console.WriteLine("  -> Step 5c: Setting AI0 = 100 (> 80) in Simulator and verifying TON delay trip DO1 = 1...");
                simulator.Control.SetTagValue(16, 100);

                // Chưa đủ 500ms -> DO1 vẫn = 0
                await Task.Delay(100, ct);
                var do1Pre = await client.ReadHoldingRegistersAsync(slaveId, 0x0912, 2, ct);
                Assert(RegisterCodec.DecodeInt32(do1Pre) == 0, "DO1 tripped prematurely before 500ms dwell window!");

                // Đợi quá 500ms -> Rule Engine MCU scan pass phải kích hoạt DO1 = 1
                await WaitUntilAsync(async () =>
                {
                    var regs = await client.ReadHoldingRegistersAsync(slaveId, 0x0912, 2, ct);
                    return RegisterCodec.DecodeInt32(regs) == 1;
                }, timeoutMs: 1500, pollMs: 50, timeoutMessage: "Trigger->Timer TON did not trip DO1=1 after 500ms", ct: ct);
                Console.WriteLine("  -> Verified: DO1 tripped to 1 after AI0 > 80 for 500ms.");

                // Hạ AI0 = 50 (<= 80) -> Rule 2 dập tắt DO1 về 0 ngay lập tức
                Console.WriteLine("  -> Setting AI0 = 50 (<= 80), verifying clear rule clears DO1 = 0 immediately...");
                simulator.Control.SetTagValue(16, 50);
                await WaitUntilAsync(async () =>
                {
                    var regs = await client.ReadHoldingRegistersAsync(slaveId, 0x0912, 2, ct);
                    return RegisterCodec.DecodeInt32(regs) == 0;
                }, timeoutMs: 1000, pollMs: 50, timeoutMessage: "Clear rule did not reset DO1=0", ct: ct);
                Console.WriteLine("  -> Verified: DO1 reset to 0 immediately upon AI0 <= 80.");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  [PASS] Step 5b/5c: Trigger -> Timer TON dual macro rules executed and verified on MCU UART!\n");
                Console.ResetColor();
                passedSteps++;
            }
            else
            {
                throw new Exception($"Step 5 Failed: Deploy failed with error {deployResult.ErrorCode} ({deployResult.ErrorMessage})");
            }

            // TEST STEP 6: Wire Profile V2 Diagnostic Ownership, Safety Interlock & Forced I/O
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("TEST STEP 6: Wire Profile V2 Diagnostic Ownership, Safety Interlock & Tag Forcing (0x0A20)");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ResetColor();

            var diagGateway = new DiagnosticGateway(client);

            var initDiag = await diagGateway.ReadDiagnosticStatusAsync(slaveId, ct);
            Console.WriteLine($"  -> Initial Diag State: {initDiag.State}, Flags: {initDiag.Flags}, Lease: {initDiag.LeaseRemainingMs}ms, Error: {initDiag.ErrorCode}");
            if (initDiag.State != SPLC_DiagState.ENGINE_RUNNING)
            {
                throw new Exception($"Step 6 Failed: Expected initial state ENGINE_RUNNING, got {initDiag.State}.");
            }

            Console.WriteLine("  -> Testing Safety Interlock (Writing DO0 while in ENGINE_RUNNING)...");
            bool writeBlocked = false;
            try
            {
                await diagGateway.WriteTagValueAsync(slaveId, tagIndex: ModbusRegisterMap.DefaultLayout.DoBase, rawValue: 1, ct);
            }
            catch (Exception ex)
            {
                writeBlocked = true;
                Console.WriteLine($"  -> Expected rejection caught: {ex.Message}");
            }

            if (!writeBlocked)
            {
                throw new Exception("Step 6 Failed: Tag write was not rejected while in ENGINE_RUNNING!");
            }
            Console.WriteLine("  -> Safety Interlock verified: Direct tag writes blocked in ENGINE_RUNNING.");

            Console.WriteLine("  -> Sending CMD_ENTER_DIAG to 0x0A20...");
            var enterDiag = await diagGateway.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.ENTER_DIAG, ct);
            Console.WriteLine($"  -> Diag State: {enterDiag.State}, Flags: {enterDiag.Flags}, Lease: {enterDiag.LeaseRemainingMs}ms");
            if (enterDiag.State != SPLC_DiagState.DIAG_CONTROL || !enterDiag.Flags.HasFlag(SPLC_DiagFlags.LEASE_ACTIVE))
            {
                throw new Exception($"Step 6 Failed: Failed to enter DIAG_CONTROL. State={enterDiag.State}, Flags={enterDiag.Flags}");
            }

            Console.WriteLine("  -> Forcing Tag DO0 (index 8, 0x0910) = 1 in DIAG_CONTROL mode...");
            await diagGateway.WriteTagValueAsync(slaveId, tagIndex: ModbusRegisterMap.DefaultLayout.DoBase, rawValue: 1, ct);

            var do0Regs = await client.ReadHoldingRegistersAsync(slaveId, 0x0910, 2, ct);
            int do0Val = RegisterCodec.DecodeInt32(do0Regs);
            Console.WriteLine($"  -> Read back DO0: {do0Val} (Expected: 1)");
            if (do0Val != 1)
            {
                throw new Exception($"Step 6 Failed: Expected DO0 = 1, got {do0Val}");
            }

            Console.WriteLine("  -> Writing VREG_RETAIN0 (index 84, 0x09A8) = 8888...");
            await diagGateway.WriteTagValueAsync(slaveId, tagIndex: ModbusRegisterMap.DefaultLayout.VregRetainBase, rawValue: 8888, ct);

            var dirtyDiag = await diagGateway.ReadDiagnosticStatusAsync(slaveId, ct);
            Console.WriteLine($"  -> Diag State: {dirtyDiag.State}, Flags: {dirtyDiag.Flags} (Checking RETAIN_DIRTY bit)");
            if (!dirtyDiag.Flags.HasFlag(SPLC_DiagFlags.RETAIN_DIRTY))
            {
                throw new Exception("Step 6 Failed: RETAIN_DIRTY flag was not set after writing retentive tag.");
            }

            Console.WriteLine("  -> Attempting CMD_EXIT_DIAG while RETAIN_DIRTY is set (must be guarded)...");
            var exitDirty = await diagGateway.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.EXIT_DIAG, ct);
            Console.WriteLine($"  -> Exit result: State={exitDirty.State}, ErrorCode={exitDirty.ErrorCode}");
            if (exitDirty.ErrorCode != SPLC_DiagErrorCode.RETAIN_DIRTY || exitDirty.State != SPLC_DiagState.DIAG_CONTROL)
            {
                throw new Exception($"Step 6 Failed: Expected RETAIN_DIRTY error preventing exit, got State={exitDirty.State}, Error={exitDirty.ErrorCode}");
            }

            Console.WriteLine("  -> Sending CMD_COMMIT_RETAIN to flash retentive memory...");
            var commitDiag = await diagGateway.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.COMMIT_RETAIN, ct);
            Console.WriteLine($"  -> After commit: Flags={commitDiag.Flags}");
            if (commitDiag.Flags.HasFlag(SPLC_DiagFlags.RETAIN_DIRTY))
            {
                throw new Exception("Step 6 Failed: RETAIN_DIRTY was not cleared after CMD_COMMIT_RETAIN.");
            }

            Console.WriteLine("  -> Sending CMD_EXIT_DIAG to return to normal execution...");
            var finalDiag = await diagGateway.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.EXIT_DIAG, ct);
            Console.WriteLine($"  -> Final Diag State: {finalDiag.State}, Flags: {finalDiag.Flags}");
            if (finalDiag.State != SPLC_DiagState.ENGINE_RUNNING || finalDiag.Flags.HasFlag(SPLC_DiagFlags.LEASE_ACTIVE))
            {
                throw new Exception($"Step 6 Failed: Expected return to ENGINE_RUNNING, got {finalDiag.State}");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [PASS] Step 6: Diagnostic Ownership & Safety Guard fully verified over UART!\n");
            Console.ResetColor();
            passedSteps++;

            // TEST STEP 7: Real-Time Clock (RTC) Synchronization & Time-of-Day Rule Triggering (0x0810)
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("TEST STEP 7: Real-Time Clock (RTC) Synchronization & Rule Triggering (0x0810 RTC)");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ResetColor();

            var rtcClient = new RtcClockClient(client);

            Console.WriteLine("  -> 7.1 Reading initial MCU RTC Clock from 0x0810...");
            var initialRtc = await rtcClient.ReadRtcClockAsync(slaveId, ct);
            Console.WriteLine($"     Initial RTC: Epoch={initialRtc.EpochUtcSeconds}, TZ={initialRtc.TimezoneOffsetMinutes}m, Synced={initialRtc.IsSynced}");

            Console.WriteLine("  -> 7.2 Synchronizing host PC time to MCU (FC16 to 0x0810..0x0813)...");
            var syncedRtc = await rtcClient.SyncToNowAsync(slaveId, ct);
            Console.WriteLine($"     Synced: LocalTime={syncedRtc.LocalDateTime:yyyy-MM-dd HH:mm:ss}, TZ={syncedRtc.TimezoneOffsetMinutes}m, Synced={syncedRtc.IsSynced}");

            var verifyRtc = await rtcClient.ReadRtcClockAsync(slaveId, ct);
            Console.WriteLine($"  -> 7.3 Read back verified RTC: Local={verifyRtc.LocalDateTime:HH:mm:ss}, Epoch={verifyRtc.EpochUtcSeconds}, Synced={verifyRtc.IsSynced}");
            Assert(verifyRtc.IsSynced, "MCU RTC clock is not marked as Synced");
            Assert(verifyRtc.TimezoneOffsetMinutes == syncedRtc.TimezoneOffsetMinutes, "MCU Timezone offset mismatch");
            Assert(Math.Abs(verifyRtc.EpochUtcSeconds - syncedRtc.EpochUtcSeconds) <= 2, "MCU Epoch UTC drift > 2 seconds");

            Console.WriteLine("  -> 7.4 Testing Point-in-Time Alarm Rule (Trigger at current minute)...");
            int curHhmm = verifyRtc.LocalHhmm;
            var timeAlarmRule = new[]
            {
                new RuleRecordDto
                {
                    Enabled = true,
                    TriggerTag = 0,
                    TriggerType = SPLC_TriggerType.TIME_WINDOW,
                    CompareOp = SPLC_CompareOp.EQ,
                    ThresholdLo = (ushort)curHhmm,
                    ThresholdHi = (ushort)curHhmm,
                    ActionTag = 8, // DO0
                    ActionType = SPLC_ActionType.SET_TAG,
                    ActionParam = 1
                }
            };
            var deployAlarmResult = await writer.DeployRulesAsync(slaveId, timeAlarmRule, ct);
            Assert(deployAlarmResult.IsSuccess, "Failed to deploy Point-in-Time rule");


            // Chờ MCU scan pass thực thi Point-in-Time rule và ghi DO0=1 (thay vì cứng 100ms)
            await WaitUntilAsync(async () =>
            {
                var regs = await client.ReadHoldingRegistersAsync(slaveId, 0x0910, 2, ct);
                return RegisterCodec.DecodeInt32(regs) == 1;
            }, timeoutMs: 1000, pollMs: 50,
               timeoutMessage: $"Point-in-Time rule did not fire DO0 at HHMM={curHhmm:D4}", ct: ct);

            var do0AlarmRegs = await client.ReadHoldingRegistersAsync(slaveId, 0x0910, 2, ct);
            int do0AlarmVal = RegisterCodec.DecodeInt32(do0AlarmRegs);
            Console.WriteLine($"     Alarm Rule check: Local HHMM={curHhmm:D4}, DO0={do0AlarmVal} (Expected: 1)");
            Assert(do0AlarmVal == 1, $"Point-in-Time rule failed to fire DO0. DO0={do0AlarmVal}");


            Console.WriteLine("  -> 7.5 Testing Cross-Midnight Time Window (18:00 -> 06:00)...");
            // Set clock to 23:00 (1380m in day)
            // 2026-09-30 23:00:00 UTC+7 = 2026-09-30 16:00:00 UTC
            DateTime nightUtc = new DateTime(2026, 9, 30, 16, 0, 0, DateTimeKind.Utc);
            long nightEpoch = ((DateTimeOffset)nightUtc).ToUnixTimeSeconds();
            await rtcClient.WriteRtcClockAsync(new RtcClockDto
            {
                EpochUtcSeconds = (uint)nightEpoch,
                TimezoneOffsetMinutes = 420,
                IsSynced = true,
                HasHardwareRtc = true,
                IsBatteryLow = false
            }, slaveId, ct);

            var crossMidnightRule = new[]
            {
                new RuleRecordDto
                {
                    Enabled = true,
                    TriggerTag = 0,
                    TriggerType = SPLC_TriggerType.TIME_WINDOW,
                    CompareOp = SPLC_CompareOp.BETWEEN,
                    ThresholdLo = 1800, // 18:00
                    ThresholdHi = 600,  // 06:00
                    ActionTag = 9,      // DO1
                    ActionType = SPLC_ActionType.SET_TAG,
                    ActionParam = 1
                }
            };
            var deployNightResult = await writer.DeployRulesAsync(slaveId, crossMidnightRule, ct);
            Assert(deployNightResult.IsSuccess, "Failed to deploy Cross-Midnight rule");


            // Chờ MCU scan pass thực thi Cross-Midnight Time Window rule và ghi DO1=1 (thay vì cứng 100ms)
            await WaitUntilAsync(async () =>
            {
                var regs = await client.ReadHoldingRegistersAsync(slaveId, 0x0912, 2, ct);
                return RegisterCodec.DecodeInt32(regs) == 1;
            }, timeoutMs: 1000, pollMs: 50,
               timeoutMessage: "Cross-Midnight rule did not fire DO1 at 23:00 (18:00-06:00 window)", ct: ct);

            var do1NightRegs = await client.ReadHoldingRegistersAsync(slaveId, 0x0912, 2, ct);
            int do1NightVal = RegisterCodec.DecodeInt32(do1NightRegs);
            var rtcNightReadback = await rtcClient.ReadRtcClockAsync(slaveId, ct);
            Console.WriteLine($"     Night Window check (23:00): Local={rtcNightReadback.LocalDateTime:HH:mm}, DO1={do1NightVal} (Expected: 1)");
            Assert(do1NightVal == 1, $"Cross-Midnight rule failed to fire DO1 at 23:00. DO1={do1NightVal}");


            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [PASS] Step 7: RTC Clock Sync & Time Rule Triggering fully verified over UART!\n");
            Console.ResetColor();
            passedSteps++;
        }
        finally
        {
            await transport.DisposeAsync();
            mcuSerial.Close();
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("================================================================================");
        Console.WriteLine($"   HARDWARE PHYSICAL TEST PASSED: {passedSteps}/{totalSteps} STEPS (100% SUCCESS)   ");
        Console.WriteLine($"   Frames Exchanged: RX = {rxCount}, TX = {txCount}                              ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        return 0;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"[ASSERTION FAILED] {message}");
    }

    /// <summary>
    /// Thay thế cho Task.Delay hardcoded khi cần chờ một điều kiện từ MCU qua Modbus.
    /// Poll condition mỗi pollMs cho đến khi đúng hoặc quá timeoutMs thì throw TimeoutException.
    /// Giúp loại bỏ flaky test do thời gian cố định không đủ linh hoạt với tải CPU biến thiên.
    /// </summary>
    private static async Task WaitUntilAsync(
        Func<Task<bool>> condition,
        int timeoutMs = 2000,
        int pollMs = 50,
        string timeoutMessage = "Condition not met within timeout",
        CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (true)
        {
            if (await condition().ConfigureAwait(false))
                return;
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException($"[TIMEOUT] {timeoutMessage} (waited {timeoutMs}ms, polling every {pollMs}ms)");
            await Task.Delay(pollMs, ct).ConfigureAwait(false);
        }
    }

    // =========================================================================
    // CHẾ ĐỘ 4: KIỂM THỬ TRUYỀN THÔNG MQTT TELEMETRY THEO CHUẨN SPLC-SPEC-MQTT-001
    // =========================================================================
    private static async Task<int> RunMqttTestAsync(string mcuPort, string studioPort, int baudRate, byte slaveId, CancellationToken ct)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("     SimplePLC MQTT Telemetry Wire Protocol Test (SPLC-SPEC-MQTT-001)           ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();
        Console.WriteLine($"MCU Server Port:    {mcuPort} (Simulates STM32 MCU Modbus Slave ID {slaveId})");
        Console.WriteLine($"Studio Client Port: {studioPort} (Simulates Studio Modbus Master via USB-CDC)");
        Console.WriteLine($"Baud Rate:          {baudRate} bps, 8-N-1");
        Console.WriteLine($"Base Address:       0x{ModbusRegisterMap.MqttTelemetryTableBaseAddress:X4} (Holding Registers)");
        Console.WriteLine($"Block Size:         {ModbusRegisterMap.MqttRegistersPerBlock} registers (192 bytes / block)");
        Console.WriteLine($"Max Blocks:         {ModbusRegisterMap.MqttMaxBlocks} blocks (Total 384 registers: 0x0C00 - 0x0D7F)");
        Console.WriteLine("--------------------------------------------------------------------------------");

        // 1. Khởi tạo Serial Port cho MCU Simulator
        using var mcuSerial = new SerialPort(mcuPort, baudRate, Parity.None, 8, StopBits.One)
        {
            DtrEnable = true,
            RtsEnable = true,
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };

        try
        {
            mcuSerial.Open();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FATAL] Không thể mở cổng MCU {mcuPort}: {ex.Message}");
            Console.ResetColor();
            return 1;
        }

        var simulator = new McuReferenceSimulator(wireProfile: 2);
        var mcuServer = new McuModbusRtuServer(simulator, slaveId);

        long rxCount = 0;
        long txCount = 0;

        mcuServer.FrameReceived += frame =>
        {
            Interlocked.Increment(ref rxCount);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  [MCU RX Frame] {Convert.ToHexString(frame)}");
            Console.ResetColor();
        };

        mcuServer.FrameSent += frame =>
        {
            Interlocked.Increment(ref txCount);
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  [MCU TX Frame] {Convert.ToHexString(frame)}");
            Console.ResetColor();
        };

        _ = Task.Run(() => mcuServer.RunAsync(mcuSerial.BaseStream, mcuSerial.BaseStream, ct), ct);

        // Scan pass engine giả lập
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                simulator.ExecuteScanPass(20);
                await Task.Delay(20, ct).ConfigureAwait(false);
            }
        }, ct);

        // 2. Khởi tạo Client phía App / Studio trên studioPort
        var transport = new UsbCdcTransport();
        try
        {
            await transport.OpenAsync(new UsbCdcOptions(studioPort, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)), ct);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FATAL] Không thể mở cổng Studio {studioPort}: {ex.Message}");
            Console.ResetColor();
            mcuSerial.Close();
            return 1;
        }

        var client = new ModbusRtuClient(transport);
        var writer = new RuleTableWriter(client);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[OK] Physical serial link established between {mcuPort} and {studioPort}!\n");
        Console.ResetColor();

        int passedSteps = 0;
        int totalSteps = 6;

        try
        {
            // STEP 1: Handshake kiểm tra UART Link
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BƯỚC 1: Handshake Kiểm tra Đường truyền UART Link qua Modbus FC03 (0x0000, 10 regs)");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ResetColor();

            var descRegs = await client.ReadHoldingRegistersAsync(slaveId, 0x0000, 10, ct);
            var desc = RegisterCodec.DecodeDeviceDescriptor(descRegs);
            Console.WriteLine($"  -> Firmware: {desc.FwVersionString}, Protocol: v{desc.ProtocolVersion}, RuleFormat: v{desc.RuleFormatVersion}");
            Assert(desc.ProtocolVersion >= 1, "Handshake failed: Invalid protocol version");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [PASS] Bước 1: UART Link COM7 <-> COM9 hoạt động ổn định và tin cậy!\n");
            Console.ResetColor();
            passedSteps++;

            // STEP 2: Tạo DTO Cấu hình 2 Khối MQTT Telemetry (Hybrid: Standard JSON + Custom Template)
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BƯỚC 2: Chuẩn bị 2 Khối Cấu hình MQTT Telemetry DTO (Chuẩn Hybrid)");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ResetColor();

            var block0 = new MqttTelemetryConfigDto
            {
                NodeId = "MQTT_BLOCK_0",
                Topic = "synaptix/telemetry/line1",
                PublishMode = "Periodic",
                IntervalMs = 2500,
                Deadband = 0.5,
                Qos = 1,
                Retain = true,
                PayloadMode = "JSON",
                DataTagIndex = 0, // DI0
                IncludeDevice = true,
                IncludeTimestamp = true
            };

            var block1 = new MqttTelemetryConfigDto
            {
                NodeId = "MQTT_BLOCK_1",
                Topic = "factory/press/pressure",
                PublishMode = "COV",
                IntervalMs = 1000,
                Deadband = 1.25,
                Qos = 0,
                Retain = false,
                PayloadMode = "Custom",
                DataTagIndex = 10, // AI0
                CustomTemplate = "{\"press\":{val},\"ts\":{ts},\"unit\":\"bar\"}",
                IncludeDevice = false,
                IncludeTimestamp = true
            };

            Console.WriteLine("  Khối 0 (Standard JSON):");
            Console.WriteLine($"    - Topic:        {block0.Topic}");
            Console.WriteLine($"    - Mode:         {block0.PublishMode} ({block0.IntervalMs} ms, Deadband: {block0.Deadband})");
            Console.WriteLine($"    - Payload Mode: {block0.PayloadMode}");
            Console.WriteLine($"    - QoS / Retain: QoS={block0.Qos}, Retain={block0.Retain}");

            Console.WriteLine("  Khối 1 (Custom Template 128-byte Buffer):");
            Console.WriteLine($"    - Topic:        {block1.Topic}");
            Console.WriteLine($"    - Mode:         {block1.PublishMode} (Deadband: {block1.Deadband})");
            Console.WriteLine($"    - Payload Mode: {block1.PayloadMode}");
            Console.WriteLine($"    - Template:     {block1.CustomTemplate} ({block1.CustomTemplate.Length} bytes)");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [PASS] Bước 2: Chuẩn bị DTO hoàn tất!\n");
            Console.ResetColor();
            passedSteps++;

            // STEP 3: Nạp Cấu hình MQTT từ App xuống MCU qua COM7 (Modbus FC16 Chunks)
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BƯỚC 3: Nạp Cấu hình MQTT xuống MCU qua COM7 -> COM9 (Modbus FC16 Chunks)");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ResetColor();

            var sampleRules = new[]
            {
                new RuleRecordDto
                {
                    Enabled = true,
                    TriggerTag = 0,
                    TriggerType = SPLC_TriggerType.ON_CHANGE,
                    CompareOp = SPLC_CompareOp.EQ,
                    ThresholdLo = 1,
                    ActionTag = 8,
                    ActionType = SPLC_ActionType.SET_TAG,
                    ActionParam = 1
                }
            };

            var deployResult = await writer.DeployRulesAsync(
                slaveId,
                rules: sampleRules,
                timers: null,
                counters: null,
                mqttConfigs: new[] { block0, block1 },
                cancellationToken: ct
            );

            Console.WriteLine($"  -> Kết quả nạp: IsSuccess={deployResult.IsSuccess}, ErrorCode={deployResult.ErrorCode}");
            Assert(deployResult.IsSuccess, $"Nạp cấu hình MQTT thất bại: {deployResult.ErrorMessage}");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [PASS] Bước 3: Nạp cấu hình MQTT thành công qua Modbus FC16!\n");
            Console.ResetColor();
            passedSteps++;

            // STEP 4: Đọc ngược và Xác thực Khối 0 (Standard JSON) tại 0x0C00 (96 registers)
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BƯỚC 4: Đọc ngược Khối 0 tại 0x0C00 (96 regs / 192 bytes) & Verify Fields");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ResetColor();

            ushort[] block0Regs = await ReadHoldingRegistersChunkedAsync(client, slaveId, ModbusRegisterMap.MqttTelemetryTableBaseAddress, ModbusRegisterMap.MqttRegistersPerBlock, 32, ct);
            var decoded0 = MqttTelemetryCodec.DecodeBlock(block0Regs);

            Console.WriteLine($"  [Readback Khối 0]:");
            Console.WriteLine($"    - Magic Offset+0:     0x{block0Regs[0]:X4} (Control Flags)");
            Console.WriteLine($"    - Topic Giải mã:     '{decoded0.Topic}'");
            Console.WriteLine($"    - PublishMode:        {decoded0.PublishMode}");
            Console.WriteLine($"    - IntervalMs:         {decoded0.IntervalMs} ms");
            Console.WriteLine($"    - Deadband:           {decoded0.Deadband}");
            Console.WriteLine($"    - QoS / Retain:       QoS={decoded0.Qos}, Retain={decoded0.Retain}");
            Console.WriteLine($"    - PayloadMode:        {decoded0.PayloadMode}");

            Assert(decoded0.Topic == block0.Topic, $"Topic mismatch: Expected '{block0.Topic}', Got '{decoded0.Topic}'");
            Assert(decoded0.PublishMode == block0.PublishMode, $"PublishMode mismatch: Expected '{block0.PublishMode}', Got '{decoded0.PublishMode}'");
            Assert(decoded0.IntervalMs == block0.IntervalMs, $"IntervalMs mismatch: Expected {block0.IntervalMs}, Got {decoded0.IntervalMs}");
            Assert(Math.Abs(decoded0.Deadband - block0.Deadband) < 0.001, "Deadband mismatch");
            Assert(decoded0.Qos == block0.Qos, "QoS mismatch");
            Assert(decoded0.Retain == block0.Retain, "Retain mismatch");
            Assert(decoded0.PayloadMode == block0.PayloadMode, "PayloadMode mismatch");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [PASS] Bước 4: Khối 0 (Standard JSON) xác thực khớp 100% từng trường dữ liệu!\n");
            Console.ResetColor();
            passedSteps++;

            // STEP 5: Đọc ngược và Xác thực Khối 1 (Custom Template) tại 0x0C60 (96 registers)
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BƯỚC 5: Đọc ngược Khối 1 tại 0x0C60 (96 regs / 192 bytes) & Verify Custom Template");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ResetColor();

            ushort block1Address = ModbusRegisterMap.GetMqttBlockAddress(1);
            ushort[] block1Regs = await ReadHoldingRegistersChunkedAsync(client, slaveId, block1Address, ModbusRegisterMap.MqttRegistersPerBlock, 32, ct);
            var decoded1 = MqttTelemetryCodec.DecodeBlock(block1Regs);

            Console.WriteLine($"  [Readback Khối 1]:");
            Console.WriteLine($"    - Control Flags:      0x{block1Regs[0]:X4}");
            Console.WriteLine($"    - Topic Giải mã:     '{decoded1.Topic}'");
            Console.WriteLine($"    - PublishMode:        {decoded1.PublishMode}");
            Console.WriteLine($"    - Deadband:           {decoded1.Deadband}");
            Console.WriteLine($"    - PayloadMode:        {decoded1.PayloadMode}");
            Console.WriteLine($"    - Custom Template:    '{decoded1.CustomTemplate}' ({decoded1.CustomTemplate.Length} bytes)");

            Assert(decoded1.Topic == block1.Topic, $"Topic mismatch: Expected '{block1.Topic}', Got '{decoded1.Topic}'");
            Assert(decoded1.PublishMode == block1.PublishMode, $"PublishMode mismatch: Expected '{block1.PublishMode}', Got '{decoded1.PublishMode}'");
            Assert(Math.Abs(decoded1.Deadband - block1.Deadband) < 0.001, "Deadband mismatch");
            Assert(decoded1.PayloadMode == block1.PayloadMode, "PayloadMode mismatch");
            Assert(decoded1.CustomTemplate == block1.CustomTemplate, $"Template mismatch: Expected '{block1.CustomTemplate}', Got '{decoded1.CustomTemplate}'");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [PASS] Bước 5: Khối 1 (Custom Raw Template 128B) giải mã toàn vẹn 100%!\n");
            Console.ResetColor();
            passedSteps++;

            // STEP 6: Cập nhật động & Vô hiệu hóa Khối (Dynamic Update & Disable Test)
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BƯỚC 6: Kiểm tra Cập nhật Động (Thay đổi Chu kỳ = 5000ms, Topic mới)");
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ResetColor();

            block0.IntervalMs = 5000;
            block0.Topic = "synaptix/line1/slow";

            // Nạp lại cấu hình cập nhật
            var updateResult = await writer.DeployRulesAsync(
                slaveId,
                rules: sampleRules,
                timers: null,
                counters: null,
                mqttConfigs: new[] { block0 },
                cancellationToken: ct
            );

            Assert(updateResult.IsSuccess, "Cập nhật động thất bại");

            ushort[] updatedBlock0Regs = await ReadHoldingRegistersChunkedAsync(client, slaveId, ModbusRegisterMap.MqttTelemetryTableBaseAddress, ModbusRegisterMap.MqttRegistersPerBlock, 32, ct);
            var updatedDecoded0 = MqttTelemetryCodec.DecodeBlock(updatedBlock0Regs);

            Console.WriteLine($"  [Readback sau Cập nhật Khối 0]:");
            Console.WriteLine($"    - Topic:        '{updatedDecoded0.Topic}' (Kỳ vọng: 'synaptix/line1/slow')");
            Console.WriteLine($"    - IntervalMs:   {updatedDecoded0.IntervalMs} ms (Kỳ vọng: 5000 ms)");

            Assert(updatedDecoded0.Topic == "synaptix/line1/slow", "Topic sau cập nhật không khớp");
            Assert(updatedDecoded0.IntervalMs == 5000, "IntervalMs sau cập nhật không khớp");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [PASS] Bước 6: Cập nhật động và nạp lại cấu hình thời gian thực thành công!\n");
            Console.ResetColor();
            passedSteps++;
        }
        finally
        {
            await transport.DisposeAsync();
            mcuSerial.Close();
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("================================================================================");
        Console.WriteLine($"   KIỂM THỬ TRUYỀN THÔNG MQTT THÀNH CÔNG RỰC RỠ: {passedSteps}/{totalSteps} BƯỚC (100% PASS)  ");
        Console.WriteLine($"   Tổng số Frame Modbus RTU đã trao đổi trên UART: RX = {rxCount}, TX = {txCount}");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        return 0;
    }

    private static async Task<ushort[]> ReadHoldingRegistersChunkedAsync(
        ModbusRtuClient client,
        byte slaveId,
        ushort startAddress,
        ushort totalCount,
        ushort chunkSize = 32,
        CancellationToken ct = default)
    {
        var result = new ushort[totalCount];
        ushort read = 0;
        while (read < totalCount)
        {
            ushort countToRead = (ushort)Math.Min((int)chunkSize, totalCount - read);
            var chunk = await client.ReadHoldingRegistersAsync(slaveId, (ushort)(startAddress + read), countToRead, ct);
            chunk.CopyTo(result.AsMemory(read, countToRead));
            read += countToRead;
        }
        return result;
    }

    // =========================================================================
    // CHẾ ĐỘ 5: BỘ KIỂM THỬ TỰ ĐỘNG ĐỘC LẬP PHẦN CỨNG (CLEAN CODE BUG HUNTER)
    // =========================================================================
    private static async Task<int> RunAutoMqttSuiteAsync(string mcuPort, string studioPort, int baudRate, byte slaveId, CancellationToken ct)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("    SimplePLC Independent Hardware Automation Test Suite (Clean Code)          ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();
        Console.WriteLine($"Cổng MCU:        {mcuPort} (STM32 Simulator Slave ID {slaveId})");
        Console.WriteLine($"Cổng Studio:     {studioPort} (Studio Master Client)");
        Console.WriteLine($"Tốc độ Baud:     {baudRate} bps");
        Console.WriteLine("--------------------------------------------------------------------------------");

        await using var suite = new Automation.MqttHardwareTestSuite(mcuPort, studioPort, baudRate, slaveId);
        try
        {
            await suite.InitializeAsync(ct);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FATAL] Không thể khởi tạo môi trường kiểm thử: {ex.Message}");
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("-> Bắt đầu thực thi chuỗi ca kiểm thử tự động (Edge Cases, Boundaries, Stress)...");
        Console.ResetColor();

        var results = await suite.RunAllTestsAsync(ct);

        int passed = 0;
        int failed = 0;

        Console.WriteLine("\n--------------------------------------------------------------------------------");
        Console.WriteLine("                    BẢNG KẾT QUẢ AUTOMATION TEST CHI TIẾT                       ");
        Console.WriteLine("--------------------------------------------------------------------------------");

        foreach (var r in results)
        {
            if (r.Passed)
            {
                passed++;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"  [PASS] ");
                Console.ResetColor();
                Console.WriteLine($"{r.TestId}: {r.TestName,-55} ({r.ElapsedMs,4} ms)");
                if (!string.IsNullOrEmpty(r.Details))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"         -> {r.Details}");
                    Console.ResetColor();
                }
            }
            else
            {
                failed++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"  [FAIL] ");
                Console.ResetColor();
                Console.WriteLine($"{r.TestId}: {r.TestName,-55} ({r.ElapsedMs,4} ms)");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"         -> LỖI: {r.ErrorMessage}");
                Console.ResetColor();
            }
        }

        Console.WriteLine("--------------------------------------------------------------------------------");
        if (failed == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  TỔNG KẾT: {passed}/{results.Count} TEST CASES PASSED (100% THÀNH CÔNG)");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  TỔNG KẾT: {passed}/{results.Count} PASSED, {failed} FAILED (PHÁT HIỆN LỖI TRONG MÃ NGUỒN!)");
            Console.ResetColor();
        }
        Console.WriteLine("================================================================================\n");

        return failed == 0 ? 0 : 1;
    }
}
