# Hướng Dẫn Sử Dụng System Tags & Mô Phỏng Chu Kỳ Quét Trong SynaptiX IDE
**Tài liệu:** `SYSTEM_TAGS_AND_SIMULATION_USER_MANUAL`  
**Phiên bản:** 1.0.0  
**Áp dụng:** SynaptiX IDE (WPF / .NET 8) & SimplePLC Firmware V2+  
**Tiêu chuẩn công nghiệp:** IEC 61131-3, IEC 61508-3 SIL2/3, ANSI/ISA-101  

---

## 1. Tổng Quan Về System Tags (Biến Hệ Thống)

Trong lập trình điều khiển tự động hóa công nghiệp (TIA Portal Siemens, Studio 5000 Rockwell, TwinCAT Beckhoff, CODESYS), các biến hệ thống (**System Tags**) là những cờ nhớ logic và nhịp xung được firmware PLC tự động cập nhật ở mỗi đầu chu kỳ quét (Scan Cycle).

Việc sử dụng System Tags mang lại những lợi ích vượt trội:
- **Tối ưu tài nguyên:** Không cần tạo thêm khối hàm Timer hoặc cờ nhớ phụ trợ (Virtual Flag) chỉ để chớp tắt đèn tháp hay bắt nhịp khởi động.
- **Tính xác định (Determinism):** Đồng bộ hoàn toàn theo chu kỳ quét thời gian thực của PLC.
- **Tiêu chuẩn hóa:** Các kỹ sư vận hành quen thuộc với Siemens hay Rockwell có thể áp dụng ngay lập tức trên SynaptiX IDE.

---

## 2. Bảng Danh Mục 8 System Tags Tiêu Chuẩn (Core-8)

Tất cả 8 System Tags đều được đặt trong nhóm `"SYSTEM"` trên Tag Catalog, mang thuộc tính **Read-Only (Chỉ Đọc)** và chiếm vùng nhớ Modbus Holding Registers `42081..42088` (Offset `0x0820..0x0827`):

> **Về chỉ số Tag Index (1000..1007):**  
> Index này là số nhận dạng định tuyến trực tiếp nạp xuống firmware MCU (trường `ushort TriggerTag`, `GuardTag`, `ActionTag` trong struct Rule 32 byte), hoàn toàn không phải chỉ là số thứ tự hiển thị UI. Việc cố định dải 1000..1007 giúp logic Rule được bảo toàn nguyên vẹn trên mọi biến thể phần cứng (từ các dòng ESP32 có 8/16 DI đến các dòng công nghiệp 64 DI), không bao giờ bị đè trùng với dải Tag người dùng (0..127).

| Index | Tên Tag | Kiểu | Địa Chỉ Modbus | Diễn Giải & Ứng Dụng Thực Tế |
| :---: | :--- | :---: | :---: | :--- |
| **1000** | `SYS_FIRST_SCAN` | `BOOL` | `42081 (0x0820)` | **Xung quét đầu tiên:** Tích cực mức `1` duy nhất ở vòng quét #1 ngay sau khi cấp nguồn/khởi động PLC, sau đó tự về `0`. Dùng nạp cấu hình ban đầu, reset biến đếm, homing động cơ. |
| **1001** | `SYS_ON` | `BOOL` | `42082 (0x0821)` | **Luôn Bật (Always TRUE):** Luôn bằng `1`. Dùng cho các chuỗi logic cần kích hoạt liên tục hoặc làm tiếp điểm kiểm tra luôn thỏa mãn. |
| **1002** | `SYS_OFF` | `BOOL` | `42083 (0x0822)` | **Luôn Tắt (Always FALSE):** Luôn bằng `0`. Dùng để khóa tạm thời các nhánh logic khi bảo trì hoặc debug. |
| **1003** | `SYS_CLOCK_1S` | `BOOL` | `42084 (0x0823)` | **Xung nhịp 1 giây (1.0 Hz):** Dạng sóng vuông 500ms ON / 500ms OFF. Dùng nhấp nháy đèn tháp vàng (Warning), xung tim nhịp (Heartbeat), chu kỳ đo đạc 1s. |
| **1004** | `SYS_CLOCK_100MS`| `BOOL` | `42085 (0x0824)` | **Xung nhịp 100ms (10.0 Hz):** Dạng sóng vuông 50ms ON / 50ms OFF. Dùng nhấp nháy đèn đỏ báo động khẩn (Fast Flash / Alarm), còi ngắt quãng. |
| **1005** | `SYS_RTC_HOUR` | `INT16`| `42086 (0x0825)` | **Giờ hiện tại (0..23):** Lấy từ đồng hồ RTC (1 thanh ghi 16-bit). Dùng điều khiển phân ca làm việc, chiếu sáng nhà xưởng theo giờ, bật gia nhiệt trước ca. |
| **1006** | `SYS_RTC_MINUTE` | `INT16`| `42087 (0x0826)` | **Phút hiện tại (0..59):** Lấy từ đồng hồ RTC (1 thanh ghi 16-bit). Dùng kích hoạt gửi báo cáo viễn trắc MQTT định kỳ mỗi đầu giờ. |
| **1007** | `SYS_COMM_OK` | `BOOL` | `42088 (0x0827)` | **Truyền thông kết nối tốt:** Luôn bằng `1` khi kết nối thiết bị tốt, bằng `0` khi mất kết nối / timeout. |

### Thứ tự hiển thị & Đa ngôn ngữ (Localization)
1. **Thứ tự ưu tiên User Tags:** Trong bảng Tag Catalog và các danh sách thả xuống (ComboBox) của khối Logic Editor, các tag vật lý của người dùng (`DI0..DI7`, `DO0..DO7`, `AI0..`) luôn được đưa lên đầu danh sách để kỹ sư dễ thao tác nhất. Nhóm System Tags chuẩn được bố trí ở cuối danh sách.
2. **Ngôn ngữ thuần nhất:** Nhãn định danh (Alias) của System Tags được chuẩn hóa đa ngôn ngữ không bị pha trộn Anh - Việt:
   - Khi chọn Tiếng Việt: `"Xung quét đầu tiên (SYS_FIRST_SCAN)"`, `"Luôn Bật (SYS_ON)"`, v.v.
   - Khi chọn Tiếng Anh: `"First Scan Pulse (SYS_FIRST_SCAN)"`, `"Always ON (SYS_ON)"`, v.v.
   - Hệ thống tự động chuyển ngữ tức thời khi người dùng thay đổi ngôn ngữ giao diện trong Cài đặt mà không cần khởi động lại.
3. **Bảo vệ tính toàn vẹn (Read-Only):** Cột Alias của System Tags bị khóa chỉnh sửa trên giao diện (Read-Only), đảm bảo các kỹ sư vận hành không vô tình đổi tên biến chuẩn của hệ thống.

---

## 3. Kết Nối Trực Tiếp Trên Sơ Đồ Khối (`Input -> Action`)

Từ phiên bản V2, SynaptiX IDE hỗ trợ tính năng **kết nối trực tiếp**:
- Người dùng có thể kéo dây trực tiếp từ khối **Input (chọn System Tag)** vào thẳng khối **Action** mà không bắt buộc phải chèn thêm khối Trigger trung gian.
- Trình biên dịch `RuleCompiler` sẽ tự động phân tích và gán xung kích hoạt `OnRise` (bắt sườn lên), giúp đồ thị logic cực kỳ gọn gàng và dễ đọc.

### Ví Dụ 1: Nhấp nháy đèn tháp màu vàng khi máy chờ (Warning Flash)
1. Thêm khối **Input**: Chọn Tag `SYS_CLOCK_1S`.
2. Thêm khối **Action**: Chọn Tag `DO1` (Đèn vàng), Lệnh `Toggle State`.
3. Nối trực tiếp ngõ ra khối Input vào ngõ vào khối Action.
4. Nhấn **Biên dịch & Chạy mô phỏng**: Đèn `DO1` trên Virtual I/O Board sẽ nhấp nháy đều đặn mỗi 1 giây!

### Ví Dụ 2: Khởi tạo trạng thái ban đầu bằng `SYS_FIRST_SCAN`
1. Thêm khối **Input**: Chọn Tag `SYS_FIRST_SCAN`.
2. Thêm khối **Action**: Chọn Tag `DO7` (Đèn Ready), Lệnh `Set Value = 1`.
3. Nối ngõ ra Input vào ngõ vào Action.
4. Khi bắt đầu chạy chu kỳ quét (Scan #1), `DO7` lập tức bật lên `1` và giữ nguyên trạng thái ở các chu kỳ tiếp theo.

---

## 4. Giám Sát Trực Quan Trên Virtual I/O Simulation Board

Bảng điều khiển thiết bị ảo (**Virtual I/O Board**) được tích hợp thêm **Phần 4: Biến Hệ Thống & Xung Nhịp (SYS_*)**:
- Hiển thị đầy đủ 8 đèn LED trạng thái tương ứng với 8 System Tags.
- Đèn LED `SYS_CLOCK_1S` và `SYS_CLOCK_100MS` nhấp nháy nhịp nhàng theo thời gian thực mô phỏng.
- Đèn `SYS_ON` luôn sáng xanh, `SYS_OFF` luôn tắt.
- Các tag số học `SYS_RTC_HOUR` và `SYS_RTC_MINUTE` hiển thị chính xác giá trị thời gian hiện hành.
