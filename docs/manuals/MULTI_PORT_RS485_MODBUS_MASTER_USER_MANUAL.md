# HƯỚNG DẪN CẤU HÌNH ĐA CỔNG RS-485 MODBUS MASTER
## (Multi-Port RS-485 Modbus Master User & Technical Manual)

> **Mã đặc tả:** `SPLC-DOC-MBM-PORT-001` | **Mã chuẩn:** `SPLC-SPEC-MBM-003`  
> **Áp dụng cho:** `SimplePLC / SynaptiX IDE v2.6.0+`  
> **Tiêu chuẩn tham chiếu:** Modbus Serial Line Protocol V1.02, Modbus Application Protocol V1.1b3, IEC 61131-3, ISA-101.

---

## 1. TỔNG QUAN HỆ THỐNG ĐA CỔNG RS-485

Trong các hệ thống tự động hóa công nghiệp và tòa nhà thông minh, thiết bị điều khiển thường phải kết nối đồng thời với nhiều mạng thiết bị chấp hành và cảm biến hoạt động ở các tốc độ truyền thông (Baud rate) hoặc cấu hình khung truyền (Framing) khác nhau:
- **Cổng 1 (`RS-485 #1`):** Mạng cảm biến môi trường tốc độ trung bình (`9600-8-N-1`).
- **Cổng 2 (`RS-485 #2`):** Mạng biến tần (VFD) hoặc đồng hồ đo năng lượng chuẩn IEC (`19200-8-E-1`).
- **Cổng 3 (`RS-485 #3`):** Cụm chỉ thị phân loại hàng hóa Sort-to-Light tốc độ cao (`115200-8-N-1`).
- **Cổng 4 (`RS-485 #4`):** Dự phòng mở rộng ngoại vi hoặc kết nối thiết bị bên thứ 3.

```
       +--------------------------------------------------------------+
       |           SynaptiX Controller / Remote I/O Gateway           |
       |  (Hardware Capabilities: 0x0A08 SPLC_CAP_MODBUS_MASTER)      |
       +--------------------------------------------------------------+
           |                |                 |                |
       UART1 (Port 1)   UART2 (Port 2)    UART3 (Port 3)   UART4 (Port 4)
       [9600-8-N-1]     [19200-8-E-1]     [115200-8-N-1]   [38400-8-N-2]
           |                |                 |                |
           v                v                 v                v
     +------------+   +------------+    +------------+   +------------+
     | Cảm biến   |   | Biến tần   |    | Cụm van &  |   | Đồng hồ    |
     | nhiệt/ẩm   |   | VFD / Bơm  |    | Đèn chỉ thị|   | đo điện áp |
     | (MB_READ)  |   | (MB_READ/WR|    | (MB_WRITE) |   | (MB_READ)  |
     +------------+   +------------+    +------------+   +------------+
```

---

## 2. QUY HOẠCH THANH GHI PHẦN CỨNG MCU (`0x1400 - 0x143F`)

Hệ thống quy hoạch phân vùng 64 thanh ghi (128 bytes) nhị phân cố định, hỗ trợ tối đa **4 cổng RS-485 vật lý**, mỗi cổng chiếm đúng **16 thanh ghi (32 bytes)**:

| Chỉ số Cổng | Địa chỉ Base | Độ dài | Tên Cổng Mặc định | Cấu hình Mặc định Ban đầu |
|:---:|:---:|:---:|:---|:---|
| **Cổng 1 (`Port 0`)** | `0x1400 .. 0x140F` | 16 Regs (32B) | `RS-485 #1` | `9600-8-N-1` · Timeout `200ms` · Retries `1` · **ENABLED** |
| **Cổng 2 (`Port 1`)** | `0x1410 .. 0x141F` | 16 Regs (32B) | `RS-485 #2` | `19200-8-E-1` · Timeout `200ms` · Retries `1` · **ENABLED** |
| **Cổng 3 (`Port 2`)** | `0x1420 .. 0x142F` | 16 Regs (32B) | `RS-485 #3` | `9600-8-N-1` · Timeout `200ms` · Retries `1` · *Disabled* |
| **Cổng 4 (`Port 3`)** | `0x1430 .. 0x143F` | 16 Regs (32B) | `RS-485 #4` | `9600-8-N-1` · Timeout `200ms` · Retries `1` · *Disabled* |

### Cấu trúc 16 thanh ghi của mỗi cổng:

| Offset | Tên trường | Kiểu dữ liệu | Mô tả chi tiết |
|:---:|:---|:---:|:---|
| `+0` | `CONTROL_AND_PORT` | `uint16` | **Bit 8:** `ENABLED` (1=Bật, 0=Tắt) · **Bit 0..7:** `PORT_NUMBER` (1..4) |
| `+1..+2` | `BAUD_RATE` | `uint32` (Big-Endian) | Tốc độ truyền baud rate (ví dụ: `9600`, `19200`, `38400`, `57600`, `115200`) |
| `+3` | `PARITY` | `uint16` | `0` = None, `1` = Even (Chẵn chuẩn IEC), `2` = Odd (Lẻ) |
| `+4` | `FRAMING` | `uint16` | **High Byte:** `DATA_BITS` (8 hoặc 7) · **Low Byte:** `STOP_BITS` (1 hoặc 2) |
| `+5` | `TIMEOUT_MS` | `uint16` | Thời gian chờ phản hồi tối đa từ thiết bị Slave (`50..5000 ms`, mặc định `200ms`) |
| `+6` | `DELAY_MS` | `uint16` | Thời gian trễ giải phóng bus giữa các khung (`10..500 ms`, mặc định `20ms`) |
| `+7` | `RETRY_COUNT` | `uint16` | Số lần phát lại khi lỗi timeout (`0..5`, mặc định `1`) |
| `+8..+13` | `PORT_NAME` | `ASCII[12]` (6 Regs) | Tên nhãn đại diện của cổng (ví dụ `"RS-485 #1"`, `"VFD_BUS"`) |
| `+14..+15` | `RESERVED` | `uint16[2]` | Luôn ghi `0x0000`; thiết bị nhận bỏ qua |

---

## 3. HƯỚNG DẪN THAO TÁC TRÊN SYNAPTIX IDE

### Bước 1: Mở Hộp thoại Cấu hình Cổng RS-485
1. Trên thanh Menu chính, chọn: **`Thiết bị (Device)` $\rightarrow$ `Cấu hình Cổng RS-485 Modbus Master...`**
2. Hoặc nhấn tổ hợp phím tắt nhanh: **`Ctrl + Shift + B`**.

### Bước 2: Thiết lập thông số cổng
Hộp thoại cấu hình xuất hiện kích thước chuẩn công nghiệp `1020 x 680 (hỗ trợ thu phóng tối thiểu 920 x 560, độc lập Minimize/Maximize)`:
- **Danh sách bên trái:** Bấm chọn cổng cần cấu hình (`Cổng 1` đến `Cổng 4`). Xem nhanh trạng thái `ACTIVE` / `DISABLED` và tóm tắt tốc độ.
- **Bảng chi tiết bên phải:**
  - **Kích hoạt cổng:** Tích chọn checkbox nếu sử dụng cổng này.
  - **Chế độ hoạt động (Mode):** Chọn Modbus Master (điều khiển thiết bị ngoại vi qua MB_READ/MB_WRITE) hoặc Modbus Slave (mở bảng ánh xạ nhớ Sandbox cho SCADA/HMI đọc/ghi).
  - **Tên cổng:** Nhập tên định danh (tối đa 12 ký tự, ví dụ: `SENSOR_BUS`, `INVERTER`).
  - **Baud Rate:** Chọn tốc độ phù hợp với mạng Slave (`9600`, `19200`, `38400`, `57600`, `115200`).
  - **Parity:** Chọn `None`, `Even` hoặc `Odd`.
  - **Stop Bits:** Chọn `1 Bit` hoặc `2 Bits`.
  - **Timeout (ms):** Đặt thời gian chờ (`200ms` khuyến nghị cho tốc độ $\ge 9600$).
  - **Inter-frame Delay (ms):** Đặt thời gian nghỉ giữa 2 lệnh Modbus (`20ms`).
  - **Retries:** Đặt số lần thử lại (`1`).
- Nhấn **`[ Áp dụng cấu hình ]`** để lưu lại vào file dự án `.splcproj`.

---

## 4. LIÊN KẾT KHỐI `MB_READ` VÀ `MB_WRITE` VỚI CỔNG RS-485

Mỗi khối giao tiếp trên FBD Canvas có thể định tuyến lưu lượng qua một cổng vật lý cụ thể:

### Trong khối `MB_READ` (Đọc Modbus Master):
1. Nhấp đúp vào khối `MB_READ` trên canvas để mở hộp thoại cấu hình.
2. Tại mục **Cổng truyền thông RS-485**, chọn cổng đích:
   - `Cổng 1 (RS-485 #1)`
   - `Cổng 2 (RS-485 #2)`
   - `Cổng 3 (RS-485 #3)`
   - `Cổng 4 (RS-485 #4)`
3. Khối trên Canvas tự động cập nhật nhãn tóm tắt:
   `MB_READ [Port 2|ID:1|FC03|Addr:0] (2 Pins)`

### Trong khối `MB_WRITE` (Ghi Modbus Master):
1. Nhấp đúp vào khối `MB_WRITE`.
2. Tại mục **Cổng truyền thông RS-485**, chọn cổng nối với cơ cấu chấp hành mong muốn.
3. Khi biên dịch (F7) và nạp (Deploy Ctrl+F5), trình biên dịch tự động đưa `PortIndex (0..3)` vào Header của khối tại MCU, cho phép bộ định thời phần cứng gửi khung Modbus RTU ra đúng chân UART tương ứng.

---

## 5. TÍNH TOÁN TIMING & NGUYÊN TẮC AN TOÀN TRUYỀN THÔNG

### Công thức tính thời gian truyền khung Modbus RTU:
Thời gian truyền 1 byte:
$$T_{byte} = \frac{1 + DataBits + ParityBit + StopBits}{BaudRate} \text{ (giây)}$$

Ví dụ: Tại tốc độ $9600 \text{ bps}$, định dạng $8\text{-}N\text{-}1$ ($10 \text{ bits/byte}$):
$$T_{byte} = \frac{10}{9600} \approx 1.042 \text{ ms/byte}$$

Khung tin Modbus đọc 2 thanh ghi (8 bytes request, 9 bytes response):
- Thời gian phát lệnh (TX): $8 \times 1.042 \text{ ms} \approx 8.3 \text{ ms}$
- Thời gian Slave xử lý nội bộ: $\approx 5 \text{ .. } 20 \text{ ms}$
- Thời gian nhận phản hồi (RX): $9 \times 1.042 \text{ ms} \approx 9.4 \text{ ms}$
- Tổng thời gian 1 lượt giao dịch: $\approx 25 \text{ .. } 40 \text{ ms}$

> [!TIP] Khuyến nghị đặt Response Timeout
> Luôn đặt `ResponseTimeoutMs` $\ge 3 \times$ thời gian giao dịch lý thuyết (tối thiểu `150ms .. 200ms`) để tránh báo lỗi giả khi Slave xử lý chu kỳ ADC.

---

## 6. SƠ ĐỒ ĐẤU NỐI DÂY VẬT LÝ RS-485

```
Master Controller (Port 1)                     Modbus Slave #1        Modbus Slave #2
+------------------------+                     +---------------+      +---------------+
| TX+/RX+ (A) -----------|-------------------->| (A)           |----->| (A)           |
| TX-/RX- (B) -----------|-------------------->| (B)           |----->| (B)           |
| Signal GND (G) --------|-------------------->| (GND)         |----->| (GND)         |
+------------------------+                     +---------------+      +-------+-------+
                                                                              | Trở đầu cuối
                                                                           [ 120 Ω ]
                                                                              |
                                                                       (Giữa chân A & B)
```

1. **Dây cáp:** Sử dụng cáp xoắn đôi có vỏ bọc chống nhiễu (Shielded Twisted Pair - STP, trở kháng đặc tính $120\,\Omega$, ví dụ Belden 9841).
2. **Trở đầu cuối (Termination Resistor):** Lắp điện trở $120\,\Omega$ ($0.25\text{W}$) tại 2 điểm mút xa nhất của đường bus.
3. **Tiếp địa chống nhiễu (Shielding):** Vỏ kim loại bọc chống nhiễu chỉ nối đất tại **MỘT ĐIỂM DUY NHẤT** tại tủ điều khiển trung tâm (Master) để triệt tiêu dòng rò vòng đất (Ground Loop).

---

## 7. XỬ LÝ SỰ CỐ (TROUBLESHOOTING)

| Hiện tượng | Nguyên nhân phổ biến | Biện pháp xử lý |
|:---|:---|:---|
| Khối `MB_READ` báo `ERR: TIMEOUT` liên tục | Đấu ngược dây A/B, sai Baudrate hoặc sai Cổng | Kiểm tra đèn LED TX/RX trên cổng tương ứng. Đảo chân A và B. Xác nhận Baudrate trùng khớp với cài đặt công tắc DIP trên Slave. |
| Khối `MB_WRITE` thỉnh thoảng mất lệnh | Bus bị bão hòa hoặc Slave không kịp xử lý | Tăng `InterFrameDelayMs` từ `20ms` lên `50ms` hoặc tăng `Deadband` để tránh phát quá dày. |
| CRC Error (Lỗi tổng kiểm) | Nhiễu điện từ do đi chung máng dây biến tần / động cơ | Đi cáp RS-485 cách xa máng dây động lực $\ge 20\text{ cm}$. Đấu điện trở $120\,\Omega$ ở hai đầu mút bus. |
| Cổng không gửi tín hiệu | Cổng đang ở trạng thái `DISABLED` trong Menu Cấu hình | Vào `Ctrl+Shift+B`, kiểm tra checkbox `Kích hoạt cổng này trên thiết bị` đã được tích chọn và nạp lại Deploy (`Ctrl+F5`). |
