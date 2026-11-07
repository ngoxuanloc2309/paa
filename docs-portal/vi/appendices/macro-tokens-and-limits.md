# Từ điển Macro Bản tin & Giới hạn Tài nguyên Hệ thống

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-APPX-002</span>
  <span><strong>Áp dụng cho:</strong> <code>MQTT_PUB</code> &bull; <code>HTTP_CLIENT</code> &bull; <code>TCP_CLIENT</code> &bull; <code>DTU Studio</code></span>
  <span><strong>Kiến trúc Runtime:</strong> Cấp phát Bộ nhớ Tĩnh Tất định (Không phân mảnh Heap)</span>
  <span><strong>Tiêu chuẩn:</strong> IEC 61131-3 &bull; Giao thức SynaptiX V2.0</span>
</div>

Nhằm đảm bảo chu kỳ quét tất định dưới mili-giây và triệt tiêu hoàn toàn hiện tượng phân mảnh bộ nhớ động khi vận hành liên tục nhiều năm trong tủ điện nhà máy, firmware biên SynaptiX cấp phát tĩnh toàn bộ vùng nhớ cho biến Tag, bảng luật, khối hàm và bộ đệm chuỗi truyền thông. Hãy tra cứu các bảng định mức dưới đây khi thiết kế mẫu JSON tùy biến hoặc lựa chọn phần cứng cho dự án.

---

## Phần 1: Từ điển Macro Mẫu Bản tin Khối hàm (`Payload Template Macros`)

Khi đặt chế độ `PayloadMode` sang **`Custom`** bên trong khối [`MQTT_PUB`](/vi/blocks/mqtt-pub), [`HTTP_CLIENT`](/vi/blocks/http-client) hoặc [`TCP_CLIENT`](/vi/blocks/tcp-client), nhân thực thi trên thiết bị sẽ thay thế trực tiếp các thẻ macro ngoặc nhọn đơn bằng giá trị thực tế trong bộ nhớ Tag ngay tại thời điểm phát tin:

| Thẻ Macro | Khối Hàm Hỗ trợ | Quy tắc Thay thế Giá trị | Trạng thái Đầu vào Mẫu | Chuỗi Kết quả Xuất ra |
| :--- | :--- | :--- | :--- | :--- |
| `{val}` | `MQTT_PUB`, `HTTP_CLIENT`, `TCP_CLIENT` | Giá trị của biến chính được gán (`DataTag` hoặc dây nối đầu tiên `Pin 1`). Số nguyên in thẳng; số thực `REAL` in gọn dấu chấm động. | `VREG0 = 25.48` (`REAL`) | `25.48` |
| `{val1}` .. `{val8}` | `MQTT_PUB`, `HTTP_CLIENT` | Giá trị của trường/chân thứ `1` đến thứ `8` trong bảng Visual Field Builder hoặc chân động. | `Pin 1 = 230.4`, `Pin 2 = 12` | `230.4` / `12` |
| `{val1:0}` .. `{val8:0}` | `MQTT_PUB`, `HTTP_CLIENT` | Làm tròn và định dạng giá trị `REAL` / `INT32` với **`0` chữ số thập phân** (số nguyên chẵn). | `Pin 1 = 84.67` | `85` |
| `{val1:1}` .. `{val8:1}` | `MQTT_PUB`, `HTTP_CLIENT` | Định dạng cố định đúng **`1` chữ số sau dấu chấm thập phân**. | `Pin 1 = 25.0` | `25.0` |
| `{val1:2}` .. `{val8:2}` | `MQTT_PUB`, `HTTP_CLIENT` | Định dạng cố định đúng **`2` chữ số thập phân** (chuẩn đo lường áp suất, điện năng, tần số). | `Pin 1 = 12.3456` | `12.35` |
| `{val1:3}` .. `{val8:3}` | `MQTT_PUB`, `HTTP_CLIENT` | Định dạng cố định đúng **`3` chữ số thập phân** (độ chính xác cao cho độ rung, dòng `4..20 mA`). | `Pin 1 = 4.5` | `4.500` |
| `{tag}` | `MQTT_PUB`, `HTTP_CLIENT`, `TCP_CLIENT` | Tên định danh chuẩn của biến Tag nguồn chính. | `DataTag = VREG0` | `VREG0` |
| `{tag1}` .. `{tag8}` | `MQTT_PUB`, `HTTP_CLIENT` | Tên khóa JSON hoặc tên biến của trường thứ `1` đến thứ `8`. | `Khóa trường 1 = "temp_c"` | `temp_c` |
| `{vals}` | `MQTT_PUB`, `HTTP_CLIENT` | Tự động ghép toàn bộ các trường `1..N` thành danh sách cặp `"khóa":giá_trị` phân cách bởi dấu phẩy! | `temp_c=28.4`, `rh=61` | `"temp_c":28.4,"rh":61` |
| `{ts}` | `MQTT_PUB`, `HTTP_CLIENT`, `TCP_CLIENT` | Dấu thời gian chuẩn Unix Epoch UTC tính bằng giây (từ đồng hồ thực RTC / NTP trên mạch). | `2026-11-06T07:00:00Z` | `1793948400` |
| `{device}` | `MQTT_PUB`, `HTTP_CLIENT`, `TCP_CLIENT` | Mã định danh trạm / Client ID duy nhất của thiết bị biên. | Trạm `SYNX-GW-01` | `SYNX-GW-01` |
| `{unit}` | `MQTT_PUB`, `HTTP_CLIENT` | Chuỗi đơn vị kỹ thuật khai báo trên biến hoặc khối (`bar`, `°C`, `kW`, `%`). | `Unit = "bar"` | `bar` |

::: tip Mẹo Viết Mẫu JSON Tùy biến Gọn nhẹ (`<= 128 ký tự`)
Khi thiết kế bản tin cho AWS IoT Core hoặc ThingsBoard nằm gọn trong giới hạn bộ đệm `128` ký tự của vi điều khiển, hãy kết hợp `{vals}` hoặc định dạng làm tròn `{val1:2}` để tối ưu từng byte băng thông:
```json
{"id":"{device}","t":{ts},"d":{"p_bar":{val1:2},"q_lpm":{val2:1},"ok":{val3:0}}}
```
Bộ đếm ký tự trực tiếp (`Độ dài / 128`) và trình kiểm tra cú pháp trên hộp thoại sẽ cảnh báo tức thì nếu bạn gõ sai tên macro hoặc vượt quá chiều dài cho phép!
:::

### 1.1 Hệ thống Macro Chuyển tiếp Nối tiếp trong DTU Gateway Studio
Bên trong công cụ [DTU Gateway Studio (`Ctrl+Shift+D`)](/vi/tools/dtu-gateway-studio), tab **Custom JSON Template** hỗ trợ thêm bộ từ khóa ngoặc nhọn kép dành riêng cho thiết kế luồng truyền trong suốt RS-485: `{{DEVICE_ID}}`, `{{TIMESTAMP}}`, `{{PORT}}`, `{{BAUDRATE}}`, `{{SERIAL_PAYLOAD}}`, `{{HEX_STREAM}}`, `{{PAYLOAD_LEN}}` và `{{PACKET_SEQ}}`.

---

## Phần 2: Bảng Giới hạn Tài nguyên & Bộ đệm Chuỗi Toàn Hệ thống

Mọi thông số trần dưới đây được đồng bộ chặt chẽ giữa bộ kiểm tra tiền biên dịch của SynaptiX IDE và bộ nhớ vi điều khiển:

### 2.1 Quỹ Tài nguyên Biến, Luật & Khối Hàm Thực thi

| Phân hệ Tài nguyên | Trần Tối đa Toàn Dự án | Chi tiết Định mức Trên Từng Khối / Phần tử |
| :--- | :--- | :--- |
| **Tổng số Biến Thực thi (`Max Tags`)** | `128` biến | Căn chỉnh `32-bit`/biến (`BOOL`, `INT32`, hoặc số thực `REAL` IEEE-754), cộng thêm `8` biến hệ thống chỉ đọc `SYS_*`. |
| **Số Luật Logic Tối đa (`Max Rules`)** | `100` luật *(hoặc `50` trên dòng Compact/Gateway)* | Quét tuần tự tất định từ trên xuống dưới (`Trigger -> Condition -> Guard -> Action`). |
| **Khối Bộ định thời Phần cứng (`Timers`)** | `8` bộ hoạt động đồng thời | Dùng chung cho các khối [`TON`](/vi/blocks/ton), [`TOF`](/vi/blocks/tof), [`TP`](/vi/blocks/tp) và [`CYCLE_TIMER`](/vi/blocks/cycle-timer). |
| **Khối Bộ đếm Phần cứng (`Counters`)** | `8` bộ hoạt động đồng thời | Dùng chung cho [`CTU`](/vi/blocks/ctu) và [`CTD`](/vi/blocks/ctd) (thanh ghi đếm `INT32`, hỗ trợ lưu giữ chống mất điện). |
| **Khối Quét Đọc Modbus (`MB_READ`)** | `4` khối / dự án | Tối đa `4` chân ngõ ra ánh xạ biến trên mỗi khối `MB_READ` (nhãn chân `<= 12 ký tự`). |
| **Khối Ghi Chấp hành Modbus (`MB_WRITE`)** | `4` khối / dự án | Hỗ trợ chế độ `Channel-Indexed` (độc quyền kênh) hoặc `Direct Registers` (`FC05, FC06, FC15, FC16`). |
| **Khối Xuất Phần cứng Trực tiếp (`DIRECT_WRITE`)** | `8` khối (`DO0 .. DO7`) | Mỗi cọc ngõ ra vật lý quản lý bởi tối đa `1` khối chuyên trách (`DO`, `PWM`, `PTO`, hoặc `AddressableBus`). |
| **Khối Xuất bản MQTT (`MQTT_PUB`)** | `4` khối / dự án | Gom nhóm tối đa `8` trường đo lường (`{val1}..{val8}`) trong một khối `MQTT_PUB`. |
| **Khối Đăng ký Nhận lệnh (`MQTT_SUB`)** | `2` khối / dự án | Tối đa `4` chân bóc tách khóa JSON nguyên tử / khối; **Hàng đợi đệm `FIFO <= 16` bản tin**. |
| **Số Cổng Vật lý RS-485** | Tối đa `4` cổng (`#1 .. #4`) | Cấu hình độc lập từng cổng làm `Modbus RTU Master`, `Modbus RTU Slave`, hoặc `DTU Transference`. |

### 2.2 Giới hạn Chiều dài Chuỗi Ký tự & Bộ đệm Giao thức

| Tham số Chuỗi / Bộ đệm | Giới hạn Ký tự / Byte Chính xác | Quy tắc Kiểm tra Hợp lệ |
| :--- | :--- | :--- |
| **`Custom JSON Template` (`MQTT_PUB` / `HTTP`)** | `<= 128 ký tự` | Kiểm tra trực tiếp khi gõ (`CustomTemplateLength <= 128`). |
| **`MQTT Topic` (`MQTT_PUB` / `MQTT_SUB`)** | `<= 32 ký tự` | Đường dẫn Topic UTF-8; ký tự đại diện `+`/`#` chỉ hợp lệ ở `MQTT_SUB`, bị cấm ở `MQTT_PUB`. |
| **Khóa JSON Nhận lệnh (`MQTT_SUB Pin.JsonKey`)** | `<= 12 ký tự` | Tên thuộc tính JSON phân biệt chữ hoa/thường trên từng chân ra. |
| **Địa chỉ `MQTT Broker Host`** | `<= 64 ký tự` | Tên miền FQDN hoặc địa chỉ IPv4 trong hộp thoại `Ctrl+Shift+M`. |
| **`MQTT Client ID / Username / Password`** | `<= 32 ký tự` / trường | Chuỗi định danh và mật khẩu xác thực ASCII trong `Ctrl+Shift+M`. |
| **Địa chỉ `Url` của `HTTP_CLIENT`** | `<= 64 ký tự` | Bắt buộc bắt đầu bằng `http://` hoặc `https://`; tự động giới hạn ở `64` ký tự. |
| **Tiêu đề `CustomHeaders` của `HTTP_CLIENT`** | `<= 56 ký tự` | Chuỗi Header rút gọn, ví dụ `Authorization: Bearer <token>`. |
| **Máy chủ `RemoteHost` của `TCP_CLIENT`** | `<= 48 ký tự` | Địa chỉ IPv4 hoặc tên máy chủ Socket từ xa (giới hạn khối `TCP_CLIENT <= 48 ký tự`). |
| **Chuỗi Heartbeat / Báo danh `TCP`** | `<= 16 ký tự` (Ping) / `<= 32 ký tự` (Reg) | Chuỗi giữ kết nối (VD: `PING`) và mã trạm báo danh chuẩn IEC 62443. |
| **`Wi-Fi SSID` / `Mật khẩu Wi-Fi`** | `<= 32 ký tự` (SSID) / `<= 64 ký tự` (Pass) | Thông tin trạm thu Wi-Fi trong `Ctrl+Shift+W`; quét tối đa `8` trạm AP mạnh nhất quanh tủ. |

---

## Phần 3: Ma trận Định mức Tài nguyên Theo 7 Cấu hình Thiết bị (`Presets`)

Khi khởi tạo dự án mới (`Ctrl+N`), SynaptiX IDE tự động định hình bảng biến, danh mục khối khả dụng và trần biên dịch khớp chính xác với **Cấu hình Phần cứng (Device Preset)** đã chọn:

| Tên Cấu hình (`Preset`) | Mã Model Phần cứng | `DI` | `DO` | `AI` | `VFLAG` | `VREG` | `VREG_RETAIN` | `COUNTER` | Tổng số Tag | Trần Luật | Giao thức & Năng lực Phần cứng Tích hợp |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **SynaptiX Remote I/O** | `SYNX-RIO-884` | `8` | `8` | `4` | `32` | `32` | `32` | `8` | `124` | `100` | Logic Cơ sở, Lưu giữ Retentive, RS-485 Master/Slave |
| **SynaptiX Compact I/O** | `SYNX-RIO-440` | `4` | `4` | `0` | `16` | `16` | `16` | `4` | `60` | `50` | Logic Cơ sở, Lưu giữ Retentive, RS-485 Master/Slave |
| **SynaptiX Relay Unit** | `SYNX-RIO-880` | `8` | `8` | `0` | `32` | `32` | `32` | `8` | `120` | `100` | Logic Cơ sở, Lưu giữ Retentive, RS-485 Master/Slave |
| **SynaptiX Data Logger** | `SYNX-DL-424` | `4` | `2` | `4` | `16` | `32` | `32` | `4` | `94` | `50` | Logic Cơ sở, Retentive, Đồng hồ RTC, Ghi thẻ nhớ SD |
| **SynaptiX Gateway** | `SYNX-GW-220` | `2` | `2` | `0` | `32` | `32` | `32` | `4` | `104` | `50` | Logic, Retentive, Ethernet, Modbus Master & TCP, MQTT Pub/Sub, HTTP, TCP Stream |
| **SynaptiX Controller** | `SYNX-CTRL-884` | `8` | `8` | `4` | `32` | `32` | `32` | `8` | `124` | `100` | Toàn diện: Logic, Retentive, RTC, SD, Ethernet, Wi-Fi, Modbus, MQTT Pub/Sub, HTTP, TCP Stream |
| **SynaptiX DTU** | `SYNX-DTU-485` | `0` | `0` | `0` | `32` | `32` | `32` | `4` | `100` | `50` | Chuyên dụng Cầu nối: Ethernet, Wi-Fi, RS-485 Master/Slave, Modbus TCP, MQTT Pub/Sub, HTTP, TCP Stream |

---

## Tài liệu Liên quan

- [Hệ sinh thái & Cấu hình Thiết bị](/vi/concepts/ecosystem-overview)
- [Kiến trúc Bộ nhớ Tag & Kiểu Dữ liệu Vật lý](/vi/concepts/data-model-and-types)
- [MQTT_PUB](/vi/blocks/mqtt-pub) &bull; [HTTP_CLIENT](/vi/blocks/http-client) &bull; [TCP_CLIENT](/vi/blocks/tcp-client)
