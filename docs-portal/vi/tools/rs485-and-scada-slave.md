# Cấu hình Bus RS-485 & Bản đồ Thanh ghi SCADA Slave

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-TOOL-001</span>
  <span><strong>Phím tắt:</strong> <code>Ctrl+Shift+B</code></span>
  <span><strong>Giao thức:</strong> Modbus RTU (TIA/EIA-485-A) &bull; Modbus TCP (Cổng 502)</span>
  <span><strong>Tiêu chuẩn:</strong> Modbus Application Protocol V1.1b3 &bull; IEC 61131-3</span>
</div>

Mọi thiết bị **Bộ điều khiển Biên SynaptiX (SynaptiX Edge Controller)**, **Trạm I/O Từ xa (Remote I/O)** và **DTU Gateway** được trang bị chip thu phát vật lý TIA/EIA-485 đều hỗ trợ cấu hình độc lập từng cổng nối tiếp thành **Modbus RTU Master (Client)** hoặc **Modbus RTU Slave (Server)**. Đồng thời, các dòng thiết bị hỗ trợ mạng IP còn cung cấp song song **Bản đồ Thanh ghi SCADA Slave Cố định (Public Fixed SCADA Slave Map)** qua giao thức **Modbus TCP (Cổng 502)** để tích hợp độ trễ bằng không với các nền tảng SCADA, DCS và màn hình HMI công nghiệp.

---

## Phần 1: Hộp thoại Cấu hình Bus RS-485 (`Ctrl+Shift+B`)

Nhấn tổ hợp phím `Ctrl+Shift+B` từ bất kỳ không gian làm việc nào (hoặc vào menu **Thiết bị -> Cấu hình Cổng RS-485...**) để mở hộp thoại **Cấu hình Cổng RS-485 Modbus**. Tùy theo cấu hình phần cứng (Device Preset) đang chọn, SynaptiX IDE sẽ hiển thị từ `RS-485 #1` đến `RS-485 #4` (`PortIndex 0..3`) tại danh sách cổng bên trái.

### Quy trình 3 Bước Cấu hình Cổng RS-485 Nhanh

1. **Bước 1 — Mở Hộp thoại:** Nhấn `Ctrl+Shift+B` trên bàn phím.
2. **Bước 2 — Chỉ định Vai trò Vận hành:**
   - Chọn **Modbus RTU Master**: Dùng khi bạn cần chủ động đọc dữ liệu từ đồng hồ điện, biến tần, cảm biến qua các khối [`MB_READ`](/vi/blocks/mb-read) và [`MB_WRITE`](/vi/blocks/mb-write).
   - Chọn **Modbus RTU Slave**: Dùng khi cần cấp dữ liệu lên màn hình HMI cảm ứng tại tủ điện hoặc hệ thống SCADA qua địa chỉ Slave ID (`1..247`).
3. **Bước 3 — Đặt Tốc độ Baud & Lưu:** Chọn Baudrate (mặc định `9600` hoặc `19200`), Parity (`None`, `Even`, `Odd`) và nhấn nút **Lưu Cấu hình** để áp dụng.

### 1.1 Vai trò Vận hành Từng Cổng: Master so với Slave

Mỗi chip thu phát RS-485 vật lý hoạt động bán song công (half-duplex) và phải được chỉ định một vai trò tất định duy nhất tại một thời điểm:

| Vai trò Vận hành | Hành vi trên Đường truyền | Khối Hàm Cho phép | Kịch bản Ứng dụng Điển hình |
| :--- | :--- | :--- | :--- |
| **Modbus RTU Master** | Chủ động phát khung đọc tuần hoàn (`FC01..FC04`) và lệnh ghi chấp hành theo sự kiện/COV (`FC05, FC06, FC15, FC16`) xuống các trạm Slave hiện trường. | [`MB_READ`](/vi/blocks/mb-read), [`MB_WRITE`](/vi/blocks/mb-write) | Quét đồng hồ điện năng, cảm biến môi trường, đầu cân loadcell và điều khiển biến tần hoặc cụm van. |
| **Modbus RTU Slave (Server)** | Thụ động lắng nghe các truy vấn gửi tới **Slave ID (`1..247`)** của thiết bị và phản hồi trực tiếp Bản đồ Thanh ghi SCADA Cố định từ RAM với độ phức tạp `O(1)`. | Không cho phép khối Master trên cổng này (được trình biên dịch khóa an toàn) | Phục vụ màn hình cảm ứng HMI tại tủ điện (Weintek, Pro-face, Siemens Comfort) hoặc bộ điều khiển thượng tầng. |

::: danger Chốt chặn An toàn Trình biên dịch — Chống Xung đột Cổng Master/Slave
Khi một cổng RS-485 (ví dụ `RS-485 #1`) được chuyển sang vai trò **Modbus RTU Slave**, trình biên dịch SynaptiX tự động kích hoạt khóa độc quyền phần cứng. Bất kỳ khối [`MB_READ`](/vi/blocks/mb-read) hoặc [`MB_WRITE`](/vi/blocks/mb-write) nào trên Sơ đồ Logic định tuyến vào cổng đó sẽ bị chặn ngay ở bước tiền kiểm tra với mã lỗi `ERR_MB_SLAVE_PORT_CONFLICT`, ngăn chặn hoàn toàn hiện tượng đụng độ đa Master trên chip thu phát vật lý.
:::

### 1.2 Thông số Khung truyền & Định thời Nối tiếp

Mọi thiết bị đấu nối chung trên một tuyến cáp xoắn đôi RS-485 bắt buộc phải thống nhất thông số khung truyền nối tiếp:

| Thông số | Dải Giá trị Hỗ trợ | Mặc định | Khuyến nghị Kỹ thuật |
| :--- | :--- | :--- | :--- |
| **Kích hoạt cổng (`Enabled`)** | `Hoạt động (Active)` / `Vô hiệu (Disabled)` | `Active` (`#1`, `#2`) | Tắt ngắt UART trên các cổng không đấu nối để tối ưu hóa chu kỳ quét. |
| **Tốc độ truyền (`Baud Rate`)** | `1200`, `2400`, `4800`, `9600`, `19200`, `38400`, `57600`, `115200` bps | `9600` (`#1`), `19200` (`#2`) | Chọn `9600` hoặc `19200` bps cho tuyến cáp dài trên `300 m`; dùng `38400..115200` bps cho kết nối ngắn trong tủ điện. |
| **Data Bits / Parity / Stop Bits** | `8N1`, `8E1`, `8O1`, `8N2` (cùng `7E1`/`7O1` cổ điển) | `8N1` (`#1`), `8E1` (`#2`) | Đặc tả Modbus RTU khuyến nghị `8E1` (Chẵn, 1 stop bit) hoặc `8N2`; tuy nhiên `8N1` là cấu hình phổ biến nhất trên cảm biến công nghiệp hiện đại. |
| **Slave ID** *(Vai trò Slave)* | `1 .. 247` | `1` | Phải duy nhất trên toàn tuyến cáp RS-485. Địa chỉ `0` dành riêng cho lệnh ghi quảng bá (Broadcast). |
| **Response Timeout** *(Vai trò Master)* | `50 .. 5000 ms` | `200 ms` | Thời gian chờ tối đa trước khi kết luận thiết bị ngoại vi mất phản hồi và gắn cờ chất lượng `[STALE]` sau khi hết lượt thử lại. |
| **Inter-Frame / Turnaround Delay** | `5 .. 1000 ms` | `20 ms` | Khoảng lặng chuyển chiều thu/phát giữa 2 khung liên tiếp (luôn lớn hơn ngưỡng tĩnh `3.5` ký tự `t3.5` của chuẩn RTU), giúp các thiết bị ngoại vi tốc độ chậm kịp nhả chân điều khiển `DE/RE`. |
| **Số lần thử lại (`Retry Count`)** | `0 .. 5` | `1` | Số lần tự động phát lại gói tin khi gặp lỗi CRC hoặc quá hạn phản hồi trước khi ghi nhận lỗi đường truyền. |

### 1.3 Bảng Kênh Master I/O & Bộ lọc Tag An toàn Kiểu Vật lý

Khi cổng ở chế độ **Modbus RTU Master**, hộp thoại cung cấp **Bảng Kênh Master I/O** trực quan với các cơ chế bảo vệ dữ liệu:
- **Xem trước Khung Hex Thời gian thực:** Tự động tính toán chuỗi 8 byte khung truyền RTU gửi đi kèm mã kiểm tra **CRC-16/MODBUS** chuẩn xác (`[ Slave ] [ FC ] [ OffsetHi OffsetLo ] [ QtyHi QtyLo ] [ CRCLo CRCHi ]`) và khuôn mẫu gói phản hồi dự kiến.
- **Khóa Bảo vệ Chân Phần cứng & Kiểu Dữ liệu:** Các hàm đọc (`FC01..FC04`) tự động ẩn toàn bộ các Tag chỉ đọc do phần cứng quét (`DI`, `AI`, `SYS_*`), ngăn chặn việc ghi đè xung đột với bộ lấy mẫu ADC/GPIO nội vi. Kiểu `BOOL` chỉ cho phép chọn cờ bit (`VFLAG`, `DO`), trong khi `INT16`/`INT32` và `REAL` (`Float32`) tự động lọc đúng các thanh ghi `VREG` / `VREG_RETAIN` tương thích.
- **Ước tính Chu kỳ Quét Vòng (`EstimatedScanCycleMs`):** Tự động tính toán tổng thời gian quét hết toàn bộ kênh đọc dựa trên tốc độ Baud và độ trễ chuyển khung.

### 1.4 Quy chuẩn Đấu nối Vật lý RS-485, Cực tính A/B & Điện trở Đầu cuối

```text
[Cổng Master / Giám sát]                             [Trạm Slave #1]               [Trạm Slave #N]
   +---------------+                                 +---------------+             +---------------+
   |  D+ / A (+)   |---------------------------------|  D+ / A (+)   |------ ... --|  D+ / A (+)   |
   |  D- / B (-)   |---------------------------------|  D- / B (-)   |------ ... --|  D- / B (-)   |
   |  SG / GND     |---------------------------------|  SG / GND     |------ ... --|  SG / GND     |
   +---------------+                                 +---------------+             +---------------+
      [120 Ohm BẬT]                                     [120 Ohm TẮT]                 [120 Ohm BẬT]
```

::: tip Danh mục Kiểm tra Đấu nối Tuyến cáp TIA/EIA-485
1. **Đấu nối Nối tiếp Chuỗi (Daisy-Chain):** Luôn đi dây nối tiếp từ trạm này sang trạm kế tiếp. Tuyệt đối tránh đấu hình sao (Star), mạch vòng hoặc rẽ nhánh chữ T quá dài (`> 1 m`) gây phản xạ sóng làm sai lệch bit ở tốc độ `19200+ bps`.
2. **Điện trở Đầu cuối `120 Ohm`:** Chỉ bật công tắc điện trở đầu cuối `120 Ohm` tại **đúng 2 thiết bị nằm ở 2 đầu mút vật lý** của tuyến cáp. Không bật điện trở trên các trạm trung gian vì sẽ làm sụt trở kháng tổng của mạng.
3. **Cáp Xoắn đôi Chống nhiễu & Tiếp địa Một điểm:** Sử dụng cáp xoắn đôi bọc kim chuyên dụng chuẩn công nghiệp `AWG 22..24` (trở kháng đặc tính `120 Ohm`). Chỉ nối lớp giáp chống nhiễu (Shield) xuống tiếp địa (`PE`) tại **một đầu duy nhất** (thường tại tủ điện trung tâm) để triệt tiêu dòng điện lặp đất (Ground Loop).
4. **Dây Tham chiếu Tín hiệu (`GND` / `SG`):** Luôn đấu chung chân mass tín hiệu cách ly `GND`/`SG` giữa các trạm để giữ điện áp đồng pha nằm trong ngưỡng an toàn `-7 V .. +12 V`.
:::

---

## Phần 2: Bản đồ Thanh ghi Modbus Slave Cố định cho SCADA / HMI

Khi thiết bị SynaptiX vận hành ở chế độ **Modbus RTU Slave** (qua cổng nối tiếp RS-485) hoặc mở dịch vụ **Modbus TCP tại Cổng `502`** (Unit ID `1`, hoặc bất kỳ Unit ID `1..255`), các hệ thống SCADA/HMI bên ngoài sẽ truy xuất trực tiếp vào **Bản đồ Bộ nhớ Cố định từ Nhà máy (Factory Fixed Memory Map)** ánh xạ thẳng tới RAM thời gian thực.

### 2.1 Cơ chế Bảo vệ Phân vùng & Kiểm tra Ranh giới Bộ nhớ

Tuân thủ tiêu chuẩn an toàn chức năng công nghiệp (IEC 61508-3), bộ lắng nghe Slave kiểm tra chặt chẽ quyền hạn và ranh giới phân vùng trên từng gói tin PDU:

| Quy tắc Bảo vệ | Bất biến Kỹ thuật Được Thực thi | Mã Ngoại lệ Modbus Trả về Khi Vi phạm |
| :--- | :--- | :--- |
| **Khóa Cứng Vùng Chỉ Đọc** | Các ngõ vào vật lý (`DI`, `DI_WORD`, `AI0..AI3`) và vùng chẩn đoán (`SYS_DIAG`) bị khóa cứng không cho phép ghi từ xa. | `0x03` (`Illegal Data Value` / Từ chối ghi) hoặc `0x01` (`Illegal Function` khi gọi ghi trên `FC02`/`FC04`) |
| **Chặn Địa chỉ Ngoài Bản đồ** | Mọi truy vấn đọc/ghi rơi vào khoảng trống dự phòng hoặc nằm ngoài bản đồ SCADA công khai đều bị từ chối tức thì. | `0x02` (`Illegal Data Address`) |
| **Chống Đọc/Ghi Vượt Ranh giới Phân vùng** | Một lệnh đọc/ghi đa thanh ghi (`FC03` / `FC16`) bắt buộc phải nằm trọn trong một phân vùng chức năng duy nhất (ví dụ: không được đọc vắt ngang từ `VREG` sang `VREG_RETAIN` trong cùng một khung truyền). | `0x02` (`Illegal Data Address`) |
| **Tính Nguyên tử 32-Bit (Atomic Alignment)** | Lệnh ghi vào các biến 32-bit (`VREG`, `VREG_RETAIN`, `COUNTER`) được chốt nguyên tử qua `FC16` (`2` thanh ghi/biến) để chu trình quét logic không bao giờ đọc phải nửa thanh ghi 16-bit đang ghi dở. | `0x02` / `0x03` nếu ghi lệch địa chỉ lẻ |

---

### 2.2 Vùng Truy xuất Cấp Bit: Ngõ vào Số (`FC02`) & Coils (`FC01`, `FC05`, `FC15`)

Các trình điều khiển SCADA/HMI có thể đọc từng chân cảm biến số và đóng/ngắt trực tiếp từng rơ-le hoặc cờ điều khiển:

| Phân vùng & Mã hàm | Địa chỉ Modbus (1-Based) | Địa chỉ Wire 0-Based (Hex) | Dải Tag SynaptiX | Kiểu Dữ liệu | Quyền | Mô tả Chức năng & Quy mô theo Preset |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Discrete Inputs** (`FC02`) | `10001 .. 10008` *(tối đa `10016`)* | `0x0000 .. 0x0007` *(`.. 0x000F`)* | `DI0 .. DI7` *(tối đa `DI15`)* | `BOOL` (`1-bit`) | `RO` | Trạng thái tức thời của các chân ngõ vào số cách ly quang (`0 = TẮT`, `1 = BẬT`). `DI0..DI7` khả dụng trên dòng 8-DI tiêu chuẩn. |
| **Coils Ngõ ra Vật lý** (`FC01, FC05, FC15`) | `00001 .. 00008` *(tối đa `00016`)* | `0x0000 .. 0x0007` *(`.. 0x000F`)* | `DO0 .. DO7` *(tối đa `DO15`)* | `BOOL` (`1-bit`) | `RW` | Đọc trạng thái hoặc điều khiển đóng/ngắt trực tiếp ngõ ra Rơ-le / Transistor (`DO0..DO7` trên dòng 8-DO). |
| **Coils Cờ Logic Nội vi** (`FC01, FC05, FC15`) | `00101 .. 00132` | `0x0064 .. 0x0083` | `VFLAG0 .. VFLAG31` | `BOOL` (`1-bit`) | `RW` | 32 biến cờ logic trung gian chuyên dùng làm nút nhấn `Start` / `Stop` / `Reset` từ màn hình HMI/SCADA và khóa liên động mềm. |

---

### 2.3 Thanh ghi Ngõ vào (`FC04` — `3xxxx`) & Thanh ghi Gom Bit / Chẩn đoán

Các kênh tương tự và thông số chẩn đoán hệ thống hỗ trợ đọc qua **Input Registers (`FC04`, `3xxxx`)** đồng thời được phản chiếu sang vùng **Holding Registers (`FC03`, `4xxxx`)** để các màn hình HMI dễ dàng gom chung vào một lệnh đọc khối `FC03`:

| Địa chỉ 1-Based (`3xxxx` / `4xxxx`) | Địa chỉ Wire 0-Based (Hex) | Tag / Khối SynaptiX | Kiểu Dữ liệu | Số Regs | Quyền | Cấu trúc Chi tiết & Đơn vị Kỹ thuật |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `40001` | `0x0000` | `DI_WORD` | `UINT16` | `1` | `RO` | Thanh ghi gom 16 bit trạng thái `DI0..DI15` (`Bit 0 = DI0`, `Bit 7 = DI7`). Tối ưu băng thông khi hiển thị dãy đèn trạng thái trên HMI. |
| `40002` | `0x0001` | `DO_WORD` | `UINT16` | `1` | `RW` | Thanh ghi gom 16 bit ngõ ra `DO0..DO15` (`Bit 0 = DO0`, `Bit 7 = DO7`). Cho phép ghi điều khiển đồng thời nhiều ngõ ra. |
| `40003 .. 40004` | `0x0002 .. 0x0003` | `VFLAG_DWORD` | `UINT32` | `2` | `RW` | Cặp thanh ghi gom 32 cờ logic `VFLAG0..VFLAG31` (High Word First). |
| `30017 .. 30024` / `40017 .. 40024` | `0x0010 .. 0x0017` | `AI0 .. AI3` | `FLOAT32` (IEEE-754) | `8` (`2/kênh`) | `RO` | 4 ngõ vào tương tự (`0..10 V` / `4..20 mA`), chuẩn Big-Endian `ABCD`: `AI0` = `0x0010` (`30017`/`40017`), `AI1` = `0x0012` (`30019`/`40019`), `AI2` = `0x0014` (`30021`/`40021`), `AI3` = `0x0016` (`30023`/`40023`). |
| `30241 .. 30242` / `40241 .. 40242` | `0x00F0 .. 0x00F1` | `SYS_UPTIME` | `UINT32` | `2` | `RO` | Thời gian hoạt động liên tục của thiết bị tính bằng giây kể từ lần cấp nguồn gần nhất (`ABCD`). |
| `30243` / `40243` | `0x00F2` | `SYS_SCANTIME` | `UINT16` | `1` | `RO` | Chu kỳ quét logic thực tế đo theo mili-giây (`ms`). |
| `30244` / `40244` | `0x00F3` | `SYS_STATUS` | `UINT16` | `1` | `RO` | Từ trạng thái sức khỏe & kết nối thiết bị (`Bit 0 = Run OK`, `Bit 1 = Lỗi Bus`, `Bit 2 = Đã kết nối Cloud`). |
| `30245 .. 30250` / `40245 .. 40250` | `0x00F4 .. 0x00F9` | `SYS_RTC` | `UINT16[6]` | `6` | `RO` | Thời gian thực RTC gồm 6 thanh ghi: `[Năm, Tháng, Ngày, Giờ, Phút, Giây]`. |

---

### 2.4 Thanh ghi Giữ (`FC03`, `FC06`, `FC16` — `4xxxx`)

Toàn bộ biến tính toán 32-bit, thanh ghi lưu giữ không mất điện và bộ đếm sự kiện được bố trí liên tiếp theo cặp 2 thanh ghi trên mỗi phần tử:

| Phân vùng | Dải Modbus (1-Based) | Địa chỉ Wire 0-Based (Hex) | Công thức Tính Địa chỉ (`Index` = `0..N-1`) | Kiểu Dữ liệu | Số Regs / Biến | Quyền | Mô tả Chức năng |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`VREG0 .. VREG31`** | `40033 .. 40096` | `0x0020 .. 0x005F` | `40033 + (Index * 2)` *(Hex: `0x0020 + Index * 2`)* | `INT32` hoặc `FLOAT32` | `2` (`32-bit`) | `RW` | 32 thanh ghi biến trung gian lưu giá trị quy đổi cảm biến, kết quả tính toán và dữ liệu cầu nối Modbus/MQTT. Kiểu dữ liệu tuân theo khai báo trong Danh mục Biến (`INT32` hoặc `REAL`). |
| **`VREG_RETAIN0 .. VREG_RETAIN31`** | `40097 .. 40160` | `0x0060 .. 0x009F` | `40097 + (Index * 2)` *(Hex: `0x0060 + Index * 2`)* | `INT32` hoặc `FLOAT32` | `2` (`32-bit`) | `RW` | 32 thanh ghi lưu giữ Retentive bảo toàn giá trị khi mất nguồn (chuyên dùng lưu ngưỡng cảnh báo, hệ số hiệu chuẩn và công thức mẻ sản xuất). |
| **`COUNTER0 .. COUNTER7`** | `40161 .. 40176` | `0x00A0 .. 0x00AF` | `40161 + (Index * 2)` *(Hex: `0x00A0 + Index * 2`)* | `INT32` | `2` (`32-bit`) | `RW` | 8 thanh ghi giá trị đếm hiện hành (`CV`) của khối [`CTU`](/vi/blocks/ctu) / [`CTD`](/vi/blocks/ctd). Có thể ghi `0` qua `FC16` từ SCADA để xóa bộ đếm sản lượng từ xa. |

::: info Xuất Bảng Địa chỉ Bàn giao SCADA (`CSV` / `Markdown`)
Trong **Hộp thoại Cấu hình Cổng RS-485 (`Ctrl+Shift+B`)**, khi chọn vai trò **Slave**, bạn có thể lọc nhanh theo các tab phân vùng chuẩn Moxa (**Tất cả**, **Holding Registers 4xxxx**, **Coils 0xxxx**, **Discrete Inputs 1xxxx**) và nhấn **Xuất tài liệu**. SynaptiX IDE sẽ xuất tệp bảng tính `.csv` (mã hóa UTF-8 BOM mở chuẩn tiếng Việt trên Excel) hoặc bảng `.md` để bàn giao trực tiếp cho kỹ sư tích hợp SCADA.
:::

---

### 2.5 Quy ước Thứ tự Word 32-Bit (`ABCD` Big-Endian) cho Ignition, WinCC & Kepware

Mọi số nguyên 32-bit (`INT32`, `UINT32`) và số thực dấu phẩy động chuẩn IEEE-754 (`FLOAT32` / `REAL`) trên Bản đồ Thanh ghi SynaptiX đều tuân thủ chuẩn **Big-Endian Mạng — High Word First, High Byte First (`ABCD`)**:
- **Thanh ghi `N` (`Offset + 0`):** Chứa các bit cao `31..16` (`Byte A` = MSB, `Byte B`)
- **Thanh ghi `N + 1` (`Offset + 1`):** Chứa các bit thấp `15..0` (`Byte C`, `Byte D` = LSB)

Ví dụ: Giá trị số thực `FLOAT32` bằng `25.5` (`0x41CC0000`) lưu tại `VREG0` (`40033..40034`, wire offset `0x0020..0x0021`) sẽ đặt `0x41CC` ở thanh ghi `40033` và `0x0000` ở thanh ghi `40034`.

| Nền tảng SCADA / OPC-UA | Chế độ Đánh địa chỉ | Cấu hình Đảo Word / Byte 32-Bit | Ví dụ Cú pháp Khai báo cho `VREG0` (`REAL` tại `40033`) |
| :--- | :--- | :--- | :--- |
| **Inductive Automation Ignition** | 1-Based (`OneBasedAddressing = true`) | `ReverseWordOrder = false` (`ABCD` High Word First) | `[SynaptiX_Unit1]1.HRF33` (`HRF` = Holding Register Float, `HRI` = Int32) |
| **PTC Kepware (KEPServerEX)** | 1-Based (`Modbus` thập phân) | **First Word Low = Tắt (`0`)** *(High Word First)* | `400033@FLOAT` hoặc `400033@LONG` (6 chữ số) / `40033` (5 chữ số) |
| **Siemens WinCC Unified / TIA Portal** | 1-Based Modbus TCP/RTU Tiêu chuẩn | Big-Endian mặc định (Không cần đảo Word) | `%MD` / `4x40033` (Kiểu dữ liệu `Real` hoặc `DInt`) |
| **Weintek EasyBuilder Pro** | 1-Based (`4x` / `0x` / `1x`) | `32-bit Float` / `32-bit Signed` — Chuyển đổi `ABCD` | Thiết bị `MODBUS RTU / TCP` -> `4x-40033` (`32-bit Float`) |

::: warning Phòng tránh Lỗi Lệch 1 Thanh ghi (`Off-by-One`)
Nếu thư viện hoặc driver SCADA của bạn đánh địa chỉ theo **PDU Offset 0-based** (như `pymodbus`, Node-RED Modbus, hoặc Ignition bật `Zero-Based Addressing`), hãy lấy địa chỉ Holding Register trừ đi `40001` — ví dụ đọc offset `32` (`0x0020`) cho `VREG0`, **không phải** `33`. Nếu đọc nhầm từ offset `33` (`0x0021`), SCADA sẽ ghép nửa thấp của `VREG0` với nửa cao của `VREG1` gây sai lệch hoàn toàn giá trị số thực!
:::

---

## Tài liệu Liên quan

- [MB_READ — Khối Đọc Thiết bị Modbus RTU](/vi/blocks/mb-read)
- [MB_WRITE — Khối Ghi Chấp hành Modbus RTU](/vi/blocks/mb-write)
- [Không gian DTU Gateway Studio (`Ctrl+Shift+D`)](/vi/tools/dtu-gateway-studio)
- [Bảng Mã lỗi & Chẩn đoán Sự cố](/vi/appendices/error-codes-and-troubleshooting)
