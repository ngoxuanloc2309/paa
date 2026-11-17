# Kế Hoạch Kiểm Thử Tích Hợp Phần Cứng (Hardware Integration Test Plan)
## SimplePLC Studio ⟷ STM32 Native USB CDC (Contract Platform V2.0)

> **Mã tài liệu**: `SPLC-TEST-HIT-V2.0`  
> **Trạng thái phần mềm**: Software Architecture Ready / Frozen Candidate (Platform V2.0)  
> **Trạng thái phần cứng**: Hardware Integration & Simulator Verified (Hỗ trợ Wire Profile V2, Diag 0x0A20, FB 0x0B00, RTC 0x0810)  
> **Thiết bị mục tiêu**: Bo mạch vi điều khiển STM32 (F4/G4/H7) tích hợp TinyUSB CDC + Modbus RTU (NanoModbus)  

---

## 1. Mục Tiêu & Phạm Vi Kiểm Thử

Kế hoạch kiểm thử này nhằm mục đích:
1. Xác nhận tính tương thích vật lý và độ ổn định của kênh truyền thông Native USB CDC giữa ứng dụng C# (.NET 8) và firmware STM32.
2. Kiểm tra tính toàn vẹn của Khế ước Dữ liệu Platform V2.0 (Self-Describing Descriptors 0x0000..0x0029, Wire Profile V2, 32-byte rule record, CRC-16/MODBUS, Staging buffer và Atomic Commit).
3. Đảm bảo toàn diện các năng lực mở rộng V2.0: Hệ thống Function Block chuyên dụng (`0x0B00..0x0B7F`), Cơ chế phân quyền Chẩn đoán & Cưỡng bức Tag (`0x0A20`), Đồng hồ RTC thời gian thực (`0x0810..0x0813`), và Lưu trữ biến tự giữ Retain Flash.
4. Đảm bảo ứng dụng SimplePLC Studio vận hành mượt mà (0% lag UI, Zero Memory Leak) trong điều kiện tải cao và xử lý lỗi phần cứng linh hoạt (hot-unplug, reboot).

---

## 2. Thiết Lập Môi Trường Bench Test

### 2.1. Thiết Bị & Công Cụ
- **Hardware Host**: Máy tính chạy Windows 10/11 x64 với cổng USB 2.0/3.0.
- **Phần cứng PLC**: Bo mạch vi điều khiển STM32 (ví dụ STM32F401/F411 BlackPill, STM32G474RE, hoặc bo SimplePLC custom board).
- **Cáp kết nối**: Cáp USB Type-C truyền dữ liệu chuẩn (hỗ trợ Data + VBUS).
- **Công cụ đo kiểm hỗ trợ**:
  - `tools/SimplePLC.HardwareTest`: Công cụ runner tự động hóa bench test 7 bước qua cổng UART/VCP (hỗ trợ chế độ Full Test, Diag Scenarios, MCU Server Daemon).
  - Logic Analyzer / Wireshark (với driver USBPcap) để bắt gói tin USB CDC 64-byte chunks khi cần debug sâu.
  - Phần mềm terminal cổng COM (PuTTY / TeraTerm) hoặc Modbus Poll để kiểm chứng độc lập.

### 2.2. Thông Số Kỹ Thuật Kênh Truyền
- **Giao thức truyền dẫn**: Native USB CDC (Virtual COM Port).
- **Giao thức ứng dụng**: Modbus RTU Framing (Slave ID = 1, Baudrate 115200 8-N-1).
- **Kích thước gói USB FS**: Cắt gói tự động ở tầng TinyUSB theo từng khối tối đa 64 bytes.
- **Kích thước thanh ghi Modbus**: 16-bit Big-Endian (High Byte trước, Low Byte sau).
- **Độ toàn vẹn**: CRC-16/MODBUS chuẩn (Polynomial `0xA001`, Init `0xFFFF`).

---

## 3. Ma Trận Ca Kiểm Thử Tích Hợp (HIT-001 đến HIT-014)

| Mã test | Tên ca kiểm thử | Mục tiêu kỹ thuật | Kết quả kỳ vọng |
| :--- | :--- | :--- | :--- |
| **HIT-001** | USB CDC Enumeration | Nhận diện thiết bị STM32 VCP trên Windows Device Manager | Cổng COM xuất hiện, driver nạp thành công, DTR assertion không gây lỗi |
| **HIT-002** | Device Descriptor Read | Đọc 10 thanh ghi định danh tại `0x0000..0x0009` | Nhận đúng DeviceClass, Variant, FW v2.0.0, HW v1.0.0, RuleFormat v2.0 |
| **HIT-003** | Compatibility Validation | Thẩm định tương thích qua `StandardDeviceCompatibilityValidator` | Khớp profile -> Cho phép kết nối; Sai version -> Báo lỗi từ chối rõ ràng |
| **HIT-004** | Health Telemetry Read | Đọc 10 thanh ghi sức khỏe tại `0x0800..0x0809` | Uptime tăng dần, CPU load < 20%, RAM usage hợp lý, ScanTime nominal 10 ms (< 15 ms) |
| **HIT-005** | Runtime Tag Polling Loop | Poll liên tục 124 tags (`0x0900..0x09F7`) chu kỳ 50-100ms | 0% CRC error, không drop frame, UI mượt mà, latency < 15ms |
| **HIT-006** | Staging Buffer & Commit | Nạp bảng luật (1..100 rules) qua Staging `0x9010` và Commit `0xA000` | CRC-16 `0x9003` khớp 100%, Commit `0xA5A5` thành công, Active Version tăng |
| **HIT-007** | Read-Back Verification | Đọc lại Active Rule Table tại `0x0100..0x073F` | Dữ liệu giải mã khớp bit-for-bit với cấu hình nạp ban đầu |
| **HIT-008** | System Command REBOOT | Ghi lệnh REBOOT (`0x0A00 = 1`) và xử lý Expected Disconnect | Phiên làm việc đóng sạch, cổng COM ngắt, reconnect thành công sau 3 giây |
| **HIT-009** | Flash Retention Test | Khởi động lại nguồn vật lý (Power Cycle) | Cấu hình rule trong Flash không bị mất, Active Version và CRC giữ nguyên |
| **HIT-010** | Robustness & Hot-Unplug | Rút cáp đột ngột khi đang polling và chạy soak test 8 giờ | App không crash, tài nguyên giải phóng sạch sẽ, không rò rỉ bộ nhớ |
| **HIT-011** | Diagnostic Session & Lease | Điều khiển chẩn đoán `0x0A20`, Tag Forcing, Safety Interlock, Watchdog Lease 3000ms | Chặn ghi khi `ENGINE_RUNNING`, tự động nhả quyền khi đứt nhịp tim |
| **HIT-012** | Dedicated Function Blocks | Đọc/Ghi 8 Dedicated Timers & 8 Counters (`0x0B00..0x0B7F`) | Bộ đếm thời gian chạy chính xác thời gian thực, Q trip chuẩn xác |
| **HIT-013** | RTC Clock & Synchronization | Đọc/Ghi RTC `0x0810..0x0813`, bảo toàn cờ pin/phần cứng | Giờ MCU đồng bộ chuẩn xác với PC, bảo toàn `HW_PRESENT` & `BATTERY_LOW` |
| **HIT-014** | Retain Flash Persistence | Sửa biến Retain trong Diag Mode và ghi Flash (`COMMIT_RETAIN 0x0A20 = 4`) | Giá trị biến Retain được lưu vào Flash và khôi phục sau Power Cycle |

---

## 4. Quy Trình Thực Hiện Chi Tiết

### HIT-001: USB CDC Enumeration & Handshake
1. **Thao tác**: Cắm cáp USB nối bo STM32 vào cổng USB máy tính.
2. **Quan sát**:
   - Windows Device Manager nhận cổng COM mới (ví dụ: `COM5` - `STMicroelectronics Virtual COM Port` hoặc `TinyUSB CDC`).
   - Mở SimplePLC Studio, mở danh sách cổng kết nối.
3. **Tiêu chí chấp thuận (Pass Criteria)**:
   - Cổng COM xuất hiện trong dropdown.
   - Nhấn "Kết nối" (Connect), `UsbCdcTransport` mở cổng thành công với `DtrEnable = true`.

---

### HIT-002: Device Descriptor & Identification Read
1. **Thao tác**: Kích hoạt use case kết nối thiết bị (`ConnectDeviceUseCase`).
2. **Giao thức Modbus**:
   - Gửi yêu cầu Modbus FC03 Read Holding Registers: Start Address = `0x0000`, Quantity = `10`.
3. **Tiêu chí chấp thuận (Pass Criteria)**:
   - Thanh ghi 0: `DeviceClass` = 0x0001 (Remote I/O).
   - Thanh ghi 1: `DeviceVariant` = 0x0001.
   - Thanh ghi 2..4: `HwVersion` = 1.0.0.
   - Thanh ghi 5..7: `FwVersion` = 1.7.0.
   - Thanh ghi 8: `ProtocolVersion` = 0x0107.
   - Thanh ghi 9: `RuleFormatVersion` = 0x0107.
   - Studio hiển thị thông tin firmware và phần cứng chính xác trên thanh trạng thái.

---

### HIT-003: Compatibility Validation Matrix
1. **Thao tác**:
   - Thử nghiệm 1 (Hợp lệ): Kết nối với firmware v1.7.0 -> Kết nối thành công, chuyển trạng thái `Connected`.
   - Thử nghiệm 2 (Không tương thích): Chỉnh firmware giả lập trả về `RuleFormatVersion = 0x0200` (Version 2.0).
2. **Tiêu chí chấp thuận (Pass Criteria)**:
   - Thử nghiệm 1: Phiên làm việc `DeviceSession` được kích hoạt, các nút chức năng Deploy/Monitor mở khóa.
   - Thử nghiệm 2: Ứng dụng từ chối kết nối, hiển thị hộp thoại cảnh báo phiên bản không tương thích, cổng COM tự động đóng an toàn.

---

### HIT-004: Device Health Telemetry & Diagnostic Registers
1. **Thao tác**: Đọc định kỳ vùng thanh ghi sức khỏe thiết bị (`0x0800..0x0809`, 10 thanh ghi).
2. **Tiêu chí chấp thuận (Pass Criteria)**:
   - `UptimeSeconds` (`0x0800..0x0801`) tăng đều mỗi giây.
   - `CpuLoadPercent` (`0x0804`) hiển thị giá trị hợp lý (0 - 100%).
   - `ScanTimeMs` (`0x0806..0x0807`) phản ánh chu kỳ quét PLC thực tế của firmware (chu kỳ danh định nominal 10 ms, dao động 9..11 ms).
   - `HealthFlags` (`0x0803`) không có bit cảnh báo lỗi phần cứng (`HEALTH_OK = 0`).

---

### HIT-005: Runtime Tag Polling Loop & Throughput
1. **Thao tác**: Bật chế độ giám sát trực tuyến (Online Monitoring) với chu kỳ quét 50ms.
2. **Giao thức Modbus**: FC03 Read Holding Registers từ `0x0900` (60 tags = 120 thanh ghi).
3. **Tiêu chí chấp thuận (Pass Criteria)**:
   - Truy vấn diễn ra liên tục mà không làm đơ giao diện WPF (UI 60 FPS mượt mà nhờ Async Lease Coordinator).
   - Tỷ lệ lỗi CRC: 0% trong 10.000 truy vấn liên tục.
   - Khi kích hoạt công tắc tại ngõ vào vật lý DI0..DI7 trên bo STM32, đèn chỉ thị trên giao diện Studio đổi màu tương ứng trong vòng < 50ms.

---

### HIT-006: Staging Buffer Transfer & Atomic Commit
1. **Thao tác**:
   - Chuẩn bị 3 kịch bản:
     - Kịch bản A: 1 rule (16 registers = 32 bytes).
     - Kịch bản B: 10 rules (160 registers = 320 bytes).
     - Kịch bản C: 100 rules (1600 registers = 3200 bytes, giới hạn tối đa của V1.7).
   - Nhấn "Nạp cấu hình" (Deploy) từ SimplePLC Studio.
2. **Các bước thực thi tự động**:
   - Bước 1: Ghi payload vào Staging Buffer (`0x9010`) chia thành các chunk 60 registers (FC16).
   - Bước 2: Đọc thanh ghi `CONFIG_STATUS` (`0x9000`) = 1 (Ready) và `STAGING_CRC16` (`0x9003`).
   - Bước 3: Studio so sánh CRC-16 đã tính với CRC-16 do MCU báo cáo.
   - Bước 4: Nếu khớp, gửi lệnh Commit: ghi `0xA5A5` vào thanh ghi `0xA000`.
   - Bước 5: Đọc `ACTIVE_CONFIG_VERSION` (`0xA001`) xác nhận phiên bản mới đã áp dụng.
3. **Tiêu chí chấp thuận (Pass Criteria)**:
   - Cả 3 kịch bản nạp thành công 100%.
   - Không có hiện tượng timeout hay tràn bộ đệm Modbus.
   - Thanh tiến trình hiển thị đúng 5 bước và báo hoàn thành.

---

### HIT-007: Active Table Read-Back & Bit-Accuracy Verification
1. **Thao tác**: Nhấn "Tải cấu hình từ PLC" (Fetch/Load Active Rules) sau khi đã nạp ở HIT-006.
2. **Giao thức Modbus**: Đọc từ vùng Active Rule Table (`0x0100`, N * 16 registers).
3. **Tiêu chí chấp thuận (Pass Criteria)**:
   - Dữ liệu quy tắc giải mã lên màn hình Rule Table khớp bit-for-bit (từng ngưỡng `ThresholdLo`, `ThresholdHi`, `ForMs`, `GuardTag`, `ActionParam`).
   - Dấu kiểm tra CRC-16 tính trên bảng đọc về trùng khớp hoàn toàn với CRC-16 của file nhị phân cấu hình gốc.

---

### HIT-008: System Command REBOOT & Expected Disconnect
1. **Thao tác**: Nhấn lệnh "Khởi động lại PLC" (Reboot Device).
2. **Cơ chế xử lý**:
   - Studio chiếm lease độc quyền (`DeviceOperation.Reboot`).
   - Gửi lệnh ghi `0x0A00 = 1`.
   - Studio **không chờ xác nhận `DONE`** tại `0x0A01` mà chủ động đóng `DeviceSession` và giải phóng cổng COM (Expected Disconnect).
3. **Tiêu chí chấp thuận (Pass Criteria)**:
   - Cổng COM trên máy tính ngắt kết nối và tự động xuất hiện lại sau 1-2 giây khi STM32 reboot xong.
   - SimplePLC Studio chuyển về trạng thái `Disconnected` sạch sẽ mà không báo lỗi crash hoặc văng Exception.
   - Nhấn "Kết nối lại" sau 3 giây thành công bình thường.

---

### HIT-009: Flash Non-Volatile Memory Retention Test
1. **Thao tác**:
   - Sau khi hoàn thành HIT-006 (đã commit cấu hình vào flash của STM32), rút cáp nguồn USB hoàn toàn khỏi bo mạch.
   - Chờ 10 giây.
   - Cắm lại cáp USB và mở kết nối từ Studio.
   - Đọc thông tin Active Configuration Version (`0xA001`) và tải lại Rule Table.
2. **Tiêu chí chấp thuận (Pass Criteria)**:
   - Toàn bộ danh sách Rule vẫn nguyên vẹn như trước khi mất nguồn.
   - MCU khởi động và chạy ngay lập tức với bảng Rule đã lưu trong Flash mà không cần nạp lại từ máy tính.

---

### HIT-010: Robustness, Hot-Unplug & 8-Hour Soak Test
1. **Thao tác**:
   - **Thử nghiệm A (Hot-Unplug)**: Đang trong quá trình polling liên tục (HIT-005) hoặc đang nạp staging (HIT-006), đột ngột rút mạnh cáp USB.
   - **Thử nghiệm B (Soak Test)**: Kết nối và bật polling liên tục trong thời gian 8 giờ liên tục qua đêm.
2. **Tiêu chí chấp thuận (Pass Criteria)**:
   - Thử nghiệm A: Ứng dụng phát hiện mất kết nối, chuyển `IsActive = false`, hủy session an toàn, không treo thread, không lock cổng COM. Cắm lại cáp kết nối lại bình thường.
   - Thử nghiệm B: Sau 8 giờ polling (hơn 500.000 yêu cầu Modbus), mức sử dụng RAM của SimplePLC Studio giữ nguyên ổn định (không phát sinh rò rỉ bộ nhớ / memory leak), tỷ lệ lỗi khung truyền < 0.001%.

---

### HIT-011: Diagnostic Session, Safety Interlock & Watchdog Lease (0x0A20)
1. **Thao tác**:
   - Khi thiết bị ở trạng thái bình thường (`ENGINE_RUNNING`), gửi lệnh ghi trực tiếp vào ngõ ra `DO0` (`0x0910 = 1`).
   - Gửi lệnh `CMD_ENTER_DIAG` (`0x0A20 = 1`) để chiếm quyền điều khiển chẩn đoán.
   - Ghi cưỡng bức `DO0 = 1` và kiểm tra phản hồi từ thiết bị.
   - Ngừng gửi heartbeat nhịp tim quá 3000ms để kiểm tra cơ chế Watchdog Lease Timeout.
2. **Tiêu chí chấp thuận (Pass Criteria)**:
   - Khi ở `ENGINE_RUNNING`, lệnh ghi trực tiếp bị từ chối với ngoại lệ an toàn (Safety Interlock).
   - Khi ở `DIAG_CONTROL`, cờ `LEASE_ACTIVE` bật, cưỡng bức I/O thành công bit-exact.
   - Khi hết hạn 3000ms không có nhịp tim, MCU tự động thu hồi quyền, chuyển về `ENGINE_RUNNING` và reset toàn bộ các ngõ ra đã cưỡng bức về mức an toàn (Fail-Safe).

---

### HIT-012: Wire Contract V2 Dedicated Function Blocks (0x0B00..0x0B7F)
1. **Thao tác**:
   - Cấu hình Timer TON 0 tại `0x0B00` với thời gian trễ `PresetMs = 1000` và kích hoạt `IN = 1`.
   - Poll định kỳ thanh ghi `0x0B00..0x0B07` và quan sát thời gian trôi qua `ElapsedMs` cùng cờ đầu ra `Q`.
2. **Tiêu chí chấp thuận (Pass Criteria)**:
   - `ElapsedMs` tăng dần tuyến tính theo chu kỳ quét thời gian thực của MCU.
   - Khi `ElapsedMs >= 1000`, cờ `Q` chuyển từ 0 lên 1 và giữ mức 1 cho đến khi `IN = 0` hoặc nhận lệnh `Reset`.

---

### HIT-013: RTC Clock Synchronization & Read-Before-Write Protection (0x0810..0x0813)
1. **Thao tác**:
   - Đọc 4 thanh ghi RTC từ MCU tại `0x0810..0x0813`.
   - Thực hiện đồng bộ giờ máy tính xuống MCU qua giao thức Read-Before-Write (FC03 kiểm tra -> FC16 ghi bảo toàn cờ).
   - Kiểm tra hiển thị trên thanh trạng thái Studio và cờ cảnh báo pin yếu `BATTERY_LOW` (`0x0004`).
2. **Tiêu chí chấp thuận (Pass Criteria)**:
   - Giờ hệ thống MCU khớp với giờ UTC/Local của máy tính (độ trôi < 2 giây).
   - Các cờ phần cứng `HW_PRESENT` (`0x0002`) và `BATTERY_LOW` (`0x0004`) được bảo toàn tuyệt đối, không bị ghi đè thành 0.
   - Khi `BATTERY_LOW = 1`, thanh trạng thái hiển thị rõ ràng badge cảnh báo pin yếu.

---

### HIT-014: Retain Variables Flash Persistence (0x0A20 COMMIT_RETAIN)
1. **Thao tác**:
   - Bật Diag Mode (`0x0A20 = 1`), ghi giá trị mới cho biến tự giữ `RET_100` (`0x09A8 = 8888`).
   - Kiểm tra cờ `RETAIN_DIRTY` (`0x0002`) trong thanh ghi cờ Diag `0x0A22`.
   - Gửi lệnh `CMD_COMMIT_RETAIN` (`0x0A20 = 4`) để lưu biến vào Flash.
   - Khởi động lại nguồn vật lý (Power Cycle) bo mạch STM32 và đọc lại giá trị `RET_100`.
2. **Tiêu chí chấp thuận (Pass Criteria)**:
   - Cờ `RETAIN_DIRTY` bật ngay sau khi ghi và xóa sạch sau khi commit Flash thành công.
   - Sau khi khởi động lại nguồn, biến `RET_100` vẫn giữ nguyên giá trị `8888` đã lưu trong Flash.

---

## 5. Biên Bản Bàn Giao & Tiêu Chuẩn Đạt Chuẩn (Sign-Off)

Hệ thống được coi là **Đạt Chuẩn Tích Hợp Phần Cứng (Hardware Integration Qualified)** khi:
- [ ] 14/14 bài test từ `HIT-001` đến `HIT-014` đều có kết quả **PASS**.
- [ ] Không có bất kỳ lỗi không lường trước (Unhandled Exception) nào phát sinh trong quá trình thử nghiệm.
- [ ] Kết quả kiểm thử tự động trên công cụ `tools/SimplePLC.HardwareTest` đạt 100% tỷ lệ thành công.
- [ ] Tài liệu kết quả kiểm thử được ký xác nhận bởi Kỹ sư Firmware và Kỹ sư Phần mềm.
