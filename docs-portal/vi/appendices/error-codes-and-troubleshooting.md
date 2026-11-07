# Bảng Mã lỗi & Quy trình Chẩn đoán Sự cố

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-APPX-003</span>
  <span><strong>Phạm vi:</strong> Ngoại lệ Modbus &bull; Mã lỗi MQTT/TLS/Wi-Fi/HTTP/TCP &bull; Lỗi Biên dịch</span>
  <span><strong>Đối tượng:</strong> Kỹ sư Nghiệm thu Tủ điện &amp; Bảo trì Nhà máy</span>
  <span><strong>Tiêu chuẩn:</strong> Cô lập Sự cố Có Cấu trúc theo IEC/IEEE 82079-1</span>
</div>

Phụ lục này tổng hợp đầy đủ bảng mã ngoại lệ giao thức chuẩn quốc tế, mã trạng thái kết nối không dây/đám mây, danh mục lỗi tiền kiểm tra của trình biên dịch và cây quyết định xử lý sự cố từng bước tại hiện trường.

---

## 1. Mã Ngoại lệ Giao thức Modbus & Lỗi Đường truyền Master

### 1.1 Bảng Mã Ngoại lệ Modbus PDU Tiêu chuẩn (`0x01 .. 0x0B`)
Khi một trạm Modbus Slave (thiết bị SynaptiX đang phục vụ SCADA, hoặc đồng hồ đo hiện trường được quét bởi [`MB_READ`](/vi/blocks/mb-read) / [`MB_WRITE`](/vi/blocks/mb-write)) nhận được gói tin đúng CRC nhưng không thể thực thi, nó sẽ bật bit cao nhất của Mã hàm (`FC | 0x80`, ví dụ `0x83` cho `FC03`) kèm 1 byte mã lỗi:

| Mã Hex | Tên Ngoại lệ Chuẩn | Nguyên nhân Phổ biến trong Hệ thống SynaptiX | Hướng Khắc phục Triệt để |
| :--- | :--- | :--- | :--- |
| `0x01` | `Illegal Function` | Thiết bị ngoại vi không hỗ trợ mã hàm vừa gửi (VD: gửi lệnh ghi đơn `FC06` vào đồng hồ chỉ cho phép ghi khối `FC16`, hoặc gọi ghi vào vùng `FC02`/`FC04`). | Đổi khối `MB_WRITE` từ `FC06` sang `FC16` (bắt buộc với biến 32-bit `INT32`/`REAL`), hoặc đối chiếu tài liệu của cảm biến. |
| `0x02` | `Illegal Data Address` | Địa chỉ thanh ghi + độ dài đọc/ghi vượt ra ngoài bản đồ hoặc đọc vắt ngang qua 2 phân vùng khác nhau trên [Bản đồ SCADA Slave Cố định](/vi/tools/rs485-and-scada-slave). | Kiểm tra độ lệch địa chỉ `+1` (`40001` 1-based so với `0x0000` 0-based); tách lệnh đọc vắt phân vùng thành 2 lệnh riêng. |
| `0x03` | `Illegal Data Value` | Cố tình ghi dữ liệu vào vùng phần cứng **Chỉ đọc (`RO`)** (`DI`, `AI0..AI3`, `SYS_DIAG`), hoặc yêu cầu số lượng thanh ghi bằng `0` / `> 125`. | Đảm bảo SCADA/Master chỉ phát lệnh ghi vào các phân vùng `RW` (`DO`, `VFLAG`, `VREG`, `VREG_RETAIN`, `COUNTER`). |
| `0x04` | `Server Device Failure` | Lỗi phần cứng nội tại trên thiết bị đo hiện trường (VD: đứt đầu dò nhiệt PT100 hoặc lỗi bộ nhớ cảm biến). | Kiểm tra màn hình báo lỗi tại chỗ và nguồn nuôi 24V DC của thiết bị đo. |
| `0x06` | `Server Device Busy` | Thiết bị đang bận chốt dữ liệu vào bộ nhớ Flash hoặc đang giữ phiên độc quyền từ một trình cấu hình khác. | Thực hiện đọc/ghi lại sau `500..1000 ms` hoặc đóng cửa sổ phần mềm thứ hai đang chiếm quyền kết nối. |
| `0x0A` / `0x0B` | `Gateway Path Unavailable` / `Target Failed to Respond` | Đã kết nối thông tới bộ chuyển đổi Modbus TCP-sang-RTU qua mạng LAN, nhưng trạm RS-485 phía sau không trả lời. | Kiểm tra Slave ID, tốc độ Baud và dây `A/B` của nhánh RS-485 phía sau Gateway. |

### 1.2 Lỗi Vật lý Đường truyền RS-485 Master (`MB_READ` / `MB_WRITE` / Wire Inspector)

| Mã Chẩn đoán Đường truyền | Biểu hiện Quan sát được | Nguyên nhân Vật lý Điển hình | Thao tác Xử lý Nhanh |
| :--- | :--- | :--- | :--- |
| **`ERR_TIMEOUT`** *(Không có byte RX)* | `ResponseLatency = —`, biến chuyển sang huy hiệu **`[STALE]`** sau khi hết lượt thử lại. | Đấu ngược cực hai dây `A(+)` / `B(-)`, sai Slave ID, lệch tốc độ Baud, cảm biến mất nguồn, hoặc đặt `ResponseTimeoutMs` quá ngắn. | Đảo chéo cặp dây `D+/A` và `D-/B` tại một đầu; mở **Moxa Wire Inspector (`Đọc Thử 1 Lần`)** trong `Ctrl+Shift+D` để dò trực tiếp. |
| **`ERR_CRC_MISMATCH`** | Có nhận được byte phản hồi nhưng sai lệch bit, trượt kiểm tra mã CRC-16. | Thiếu điện trở đầu cuối `120 Ohm` trên cáp dài, nhiễu biến tần xuyên vào cáp không bọc kim, hoặc **trùng Slave ID** khiến 2 trạm trả lời đè lên nhau! | Bật điện trở `120 Ohm` ở đúng 2 đầu mút mạng, nối đất vỏ bọc kim tại 1 điểm tủ chính và rà soát trùng địa chỉ Slave ID. |
| **`ERR_FRAMING` / `OVERRUN`** | Vỡ bit đầu/cuối khung truyền hoặc mất byte đuôi. | Lệch bit chẵn lẻ (`8N1` so với `8E1`), hoặc thiết bị ngoại vi nhả chân thu/phát `DE/RE` chậm hơn khoảng nghỉ giữa 2 khung. | Đồng bộ chuẩn `Parity` (`8E1` / `8N1`) và tăng `InterFrameDelayMs` từ `20 ms` lên `35..50 ms`. |

---

## 2. Mã Chẩn đoán Mạng Wi-Fi, MQTT/TLS, HTTP REST & TCP Socket

| Phân hệ | Mã Trạng thái / Lỗi | Ý nghĩa Kỹ thuật | Cách Khắc phục |
| :--- | :--- | :--- | :--- |
| **Wi-Fi (`Ctrl+Shift+W`)** | `AUTH_FAILED` (`4`) | Bộ phát Wi-Fi từ chối mật khẩu xác thực WPA2/WPA3. | Bấm biểu tượng con mắt kiểm tra chính xác chữ hoa/thường của mật khẩu; lưu ý module công nghiệp hoạt động ở băng tần **`2.4 GHz`** (không kết nối vào mạng chỉ phát riêng `5 GHz`). |
| **Wi-Fi (`Ctrl+Shift+W`)** | `SSID_NOT_FOUND` (`5`) | Không tìm thấy tên mạng SSID đã cấu hình quanh vị trí lắp đặt. | Mở **Trình Quét Sóng Trực tiếp** ngay khi đóng cửa tủ; lắp ăng-ten kéo dài SMA ra ngoài vỏ tủ sắt nếu `RSSI <= -78 dBm`. |
| **MQTT / TLS (`Ctrl+Shift+M`)** | `TLS_CERT_INVALID` / `AUTH_EX` | Xác thực chuỗi chứng chỉ X.509 thất bại, chứng chỉ hết hạn, hoặc tên miền không khớp SAN. | Nạp đúng tệp Root CA `.pem`/`.crt`, kiểm tra năm đồng hồ RTC (`>= 2026`), hoặc tích `IgnoreHostname` nếu gọi tới Broker nội bộ bằng địa chỉ IP tĩnh. |
| **MQTT Broker** | `CONNACK 0x04 / 0x05` | Sai Username/Password hoặc tài khoản bị chặn quyền ACL trên Topic. | Kiểm tra lại tài khoản và quyền xuất bản/đăng ký (ACL) của `ClientId` trên máy chủ Broker. |
| **Khối `MQTT_SUB`** | Xung `Err` / `PARSE_ERR` | Bản tin JSON sai cú pháp, thiếu khóa `JsonKey` bắt buộc, hoặc gửi chuỗi chữ vào chân số `INT32`/`REAL`. | Xem `LastReceivedPayload`; cơ chế **Nguyên tử Toàn vẹn** đã chặn an toàn gói lỗi. Sửa lại cấu trúc JSON từ phía phần mềm gửi. |
| **Khối `HTTP_CLIENT`** | `StatusCode 400 / 401 / 403` | Máy chủ Web API từ chối cú pháp JSON (`400`) hoặc thiếu/sai mã `Authorization` (`401`/`403`). | Kiểm tra khung xem trước `PreviewPayloadText` và chuỗi `CustomHeaders` (`<= 56 ký tự`). |
| **Khối `HTTP_CLIENT`** | `StatusCode 408 / 5xx / 0` | Lỗi phân giải tên miền DNS, tường lửa chặn, hoặc máy chủ phản hồi quá hạn `TimeoutSec`. | Khi đặt IP Tĩnh (`Static IPv4`), **bắt buộc phải điền đúng `Gateway` và `DNS (8.8.8.8)`** để thiết bị phân giải được tên miền Internet! |
| **Khối `TCP_CLIENT`** | `SOCKET_REFUSED / RESET` | Máy chủ đích chưa mở cổng lắng nghe hoặc bộ định tuyến NAT ngắt kết nối nhàn rỗi. | Kiểm tra cổng mở trên Server và bật `HeartbeatIntervalSec = 30 s` (`PING`) để giữ phiên liên tục. |

---

## 3. Bảng Mã Lỗi Tiền Kiểm tra Trình Biên dịch (`F7`)

Khi nhấn `F7` (hoặc `Ctrl+F5`), trình biên dịch quét kiểm tra toàn bộ tính toàn vẹn của đồ thị trước khi sinh mã thực thi:

| Nhóm Lỗi Biên dịch | Nguyên nhân Trên Sơ đồ Khối | Cách Khắc phục Trên Sơ đồ Logic (`Ctrl+2`) |
| :--- | :--- | :--- |
| **Khối Action Thiếu Đầu vào** (`ERR_ACTION_NO_INCOMING`) | Khối [`ACTION`](/vi/blocks/action) nằm rời rạc, chưa có dây tín hiệu điều khiển nối vào chân `Exec`. | Nối ngõ ra từ `INPUT`, `TRIGGER`, `COMPARATOR`, `GUARD` hoặc Timer/Counter vào khối `ACTION`, hoặc xóa khối thừa. |
| **Trigger / Guard Chưa Đấu Dây** (`ERR_TRIGGER_NO_INPUT` / `ERR_GUARD_NO_TRIGGER`) | Khối `TRIGGER` thiếu nguồn `INPUT`, hoặc cổng `GUARD` không có nhánh kích hoạt đi vào. | Đấu một khối `INPUT` vào `TRIGGER`, và đặt `GUARD` nối tiếp giữa nhánh điều kiện với khối `ACTION`. |
| **Biến Khóa Guard Không Phải Boolean** (`ERR_GUARD_NOT_BOOLEAN`) | Cổng khóa an toàn `GUARD` đang trỏ nhầm vào thanh ghi số `INT32` hoặc `REAL` thay vì biến bit `BOOL`. | Chọn lại biến kiểu `BOOL` (`DI`, `DO`, `VFLAG`) cho `GUARD`, hoặc đặt khối `COMPARATOR` phía trước để chuyển sang kết quả logic. |
| **Ghi Đè Lên Chân Chỉ Đọc** (`ERR_ACTION_READONLY_TARGET`) | Khối `ACTION`, `SCALE`, `MB_READ` hoặc `MQTT_SUB` trỏ đích ghi vào chân phần cứng chỉ đọc (`DI`, `AI`, `SYS_*`). | Đổi biến đích sang vùng cho phép ghi như `DO`, `VFLAG`, `VREG` hoặc `VREG_RETAIN`. |
| **Xung đột Nhiều Nguồn Ghi Cùng Biến** (`ERR_CONFLICTING_WRITERS`) | Hai nhánh luật vô điều kiện cùng ghi đè một biến trong mỗi chu kỳ quét, hoặc hai khối `DIRECT_WRITE` tranh chấp chung một chân `DO`. | Chuyển sang cơ chế chốt `Set` / `Reset` loại trừ nhau, gom về một khối xuất duy nhất, hoặc tách sang chân `DO` khác. |
| **Vòng Lặp Tổ hợp Khép kín** (`ERR_CIRCULAR_DEPENDENCY`) | Các khối tổ hợp nối vòng tròn trực tiếp (`Tag A -> Tag B -> Tag A`) gây dao động không trễ. | Ngắt vòng phản hồi thông qua khối có trạng thái nhớ (`TON`, `CTU`) hoặc khối bắt sườn xung `TRIGGER`. |
| **Đặt Khối Master Trên Cổng Slave** (`ERR_MB_SLAVE_PORT_CONFLICT`) | Khối [`MB_READ`](/vi/blocks/mb-read) hoặc [`MB_WRITE`](/vi/blocks/mb-write) đang chọn cổng RS-485 đã cấu hình làm **Modbus RTU Slave**. | Chuyển khối Master sang cổng nối tiếp thứ hai (`RS-485 #2`) hoặc đổi vai trò cổng về **Master** trong hộp thoại `Ctrl+Shift+B`. |
| **Vượt Trần Tài nguyên Phần cứng** (`ERR_CAPACITY_EXCEEDED`) | Số lượng luật hoặc khối vượt định mức của Preset (`MaxRules`, `8` Timer, `8` Counter, `4` `MQTT_PUB`, `2` `MQTT_SUB`, `4` `MB_READ`). | Gom nhiều biến lẻ vào một khối `MB_READ` đa chân / `MQTT_PUB` đa trường, hoặc nâng cấp lên [Preset Thiết bị](/vi/appendices/macro-tokens-and-limits) cao hơn. |

---

## 4. Cây Quyết định Chẩn đoán Nhanh Tại Hiện trường

| Triệu chứng Sự cố | Nguyên nhân Khả dĩ Nhất | 3 Bước Xử lý Tức thì Tại Hiện trường |
| :--- | :--- | :--- |
| **Không nhận Cổng ảo USB CDC** | Dùng nhầm cáp sạc 2 sợi (thiếu cặp dữ liệu D+/D-) hoặc phần mềm khác đang khóa độc quyền cổng COM. | 1. Đổi sang cáp USB-C chuẩn truyền dữ liệu.<br/>2. Kiểm tra Windows Device Manager mục Ports (COM & LPT).<br/>3. Đóng terminal/ModScan khác, nhấn **F4** quét lại. |
| **Từ chối Kết nối Modbus TCP (:502)** | Sai lớp mạng IPv4 máy tính, trùng IP tĩnh hoặc tường lửa VLAN chặn cổng 502. | 1. Dùng lệnh `ping <IP>` trên máy tính để kiểm tra vật lý.<br/>2. Xác minh máy tính cùng lớp mạng với thiết bị (VD `192.168.1.x /24`).<br/>3. Mở thông cổng TCP 502 trên switch quản lý mạng. |
| **Biến Truyền thông báo [STALE]** | Mất nguồn cảm biến RS-485, ngược cực A/B, sai Baud rate/Parity hoặc lệch offset 40001. | 1. Nhấn **Ctrl+Shift+D** Wire Inspector, bấm *Đọc thử 1 lần*.<br/>2. Nếu 0 byte RX: đảo 2 dây RS-485 A(+) và B(-).<br/>3. Nếu lỗi CRC: đồng bộ Parity (8N1/8E1) và đóng trở cuối 120Ω. |

### Quy trình Xử lý 3 Tình huống Phổ biến Nhất

1. **Không tìm thấy cổng COM ảo `USB CDC` trên SynaptiX IDE:**
   - **Bẫy cáp sạc không truyền dữ liệu:** Hơn 60% trường hợp không nhận cổng tại công trường là do dùng nhầm dây cáp sạc điện thoại chỉ có 2 sợi nguồn mà thiếu cặp dây dữ liệu `D+/D-`. Hãy đổi sang cáp dữ liệu chuẩn và kiểm tra mục **Ports (COM & LPT)** trong **Windows Device Manager**.
   - **Cổng COM đang bị khóa độc quyền:** Hệ điều hành Windows chỉ cho phép duy nhất 1 ứng dụng mở cổng nối tiếp tại một thời điểm. Hãy tắt các phần mềm ModScan, Hercules, PuTTY hoặc cửa sổ IDE thứ hai trước khi nhấn `F4`.
2. **Kết nối `Modbus TCP` cổng `502` báo `Connection Refused` hoặc `Timeout`:**
   - Kiểm tra card mạng máy tính đã đặt cùng lớp mạng IPv4 với thiết bị SynaptiX chưa (VD: `192.168.1.x / 24`) và thử lệnh `ping <ip-thiet-bi>` trên PowerShell.
   - Đảm bảo tường lửa nhà máy hoặc Switch chia VLAN không chặn cổng TCP `502`. Luôn cấu hình **IP Tĩnh (`Static IPv4`)** cho mọi thiết bị kết nối với SCADA/HMI.
3. **Biến truyền thông báo huy hiệu `[STALE]` khi bật Gỡ lỗi Trực tuyến (`F8`):**
   - Nhấn `Ctrl+Shift+D`, chọn kênh bị lỗi, bung khung **Moxa Wire Frame Inspector** và bấm **Đọc Thử 1 Lần (`Read Once`)**:
     - **Không có byte `RX` trả về:** Kiểm tra nguồn 24V nuôi cảm biến, xác nhận đúng Slave ID và thử đảo hai đầu dây `RS-485 A(+)` và `B(-)`.
     - **Báo ngoại lệ `0x02`:** Giảm số lượng thanh ghi đọc gộp hoặc cộng/trừ độ lệch địa chỉ gốc `40001`.

---

## Tài liệu Liên quan

- [Cấu hình RS-485 & Bản đồ SCADA Slave (`Ctrl+Shift+B`)](/vi/tools/rs485-and-scada-slave)
- [Bảo mật MQTT TLS & Cấu hình Wi-Fi (`Ctrl+Shift+M` / `W`)](/vi/tools/mqtt-and-wifi-config)
- [Mô phỏng Ngoại tuyến (`F5`) & Gỡ lỗi Trực tuyến (`F8`)](/vi/tools/simulator-and-live-debug)
