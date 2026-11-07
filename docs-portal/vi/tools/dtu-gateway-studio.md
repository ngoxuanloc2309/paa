# Không gian DTU Gateway Studio (`Ctrl+Shift+D`)

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-TOOL-003</span>
  <span><strong>Phím tắt:</strong> <code>Ctrl+Shift+D</code> (hoặc <code>Ctrl+1</code> trên Preset DTU)</span>
  <span><strong>Phần cứng Mục tiêu:</strong> <code>SYNX-DTU-485</code> &bull; <code>SYNX-GW-220</code> &bull; Bộ điều khiển Lai (Hybrid)</span>
  <span><strong>Kênh Uplink:</strong> MQTT(s) &bull; HTTP(s) REST &bull; Raw TCP Socket</span>
</div>

**DTU Gateway Studio** là không gian làm việc chuyên biệt không cần viết mã (No-Code) dùng để cấu hình cầu nối dữ liệu từ chuẩn nối tiếp hiện trường RS-485 lên hạ tầng Cloud / SCADA ngay trong **SynaptiX IDE**. Khi khởi tạo dự án mới với cấu hình **SynaptiX DTU (`SYNX-DTU-485`)**, DTU Gateway Studio sẽ tự động mở làm màn hình làm việc chính. Đối với các dự án lai trên **SynaptiX Controller** hoặc **SynaptiX Gateway**, kỹ sư có thể gọi nhanh công cụ này bất kỳ lúc nào bằng phím tắt `Ctrl+Shift+D` (hoặc vào menu **Thiết bị -> Cấu hình DTU Gateway...**).

::: tip Hướng dẫn Cấu hình Nhanh Cầu nối DTU Gateway
Chỉ cần thực hiện 4 bước tuần tự theo danh mục điều hướng bên trái giao diện:
1. **Mục 0 (RS-485):** Khai báo danh sách cảm biến cần đọc (Slave ID, thanh ghi, chu kỳ quét) hoặc chế độ truyền trong suốt (Passthrough).
2. **Mục 1 (Wi-Fi):** Nhập tên Wi-Fi (SSID) và mật khẩu để thiết bị kết nối vào mạng nhà máy (hỗ trợ DHCP hoặc IP tĩnh).
3. **Mục 2 (Cloud Uplink):** Chọn giao thức đẩy dữ liệu: **MQTT** (đám mây AWS IoT, EMQX, HiveMQ), **HTTP REST Webhook** hoặc **TCP Socket**.
4. **Mục 3 (Payload / JSON):** Tùy chỉnh mẫu gói tin JSON và nhịp gửi định kỳ (ví dụ: mỗi `5 giây`).
:::

---

## 1. Bốn Phân mục Điều hướng Dọc (4 Guided Sidebar Sections)

Thanh điều hướng dọc bên trái của DTU Gateway Studio chia quy trình tích hợp thành 4 bước kỹ thuật tuần tự, đi kèm huy hiệu trạng thái trực quan (số kênh thanh ghi, tên `SSID`, giao thức Uplink và chu kỳ đẩy tin):

### Phân mục `0`: Quét Kênh RS-485 & Chế độ Cổng Nối tiếp (`RS-485`)
Thiết lập cổng vật lý (`RS-485 #1 .. #4`), thông số khung truyền (`1200 .. 115200 bps`, `8N1` / `8E1` / `8O1` / `8N2`), thời gian chờ phản hồi và chế độ vận hành:
- **Modbus RTU Master (`ModbusMaster`):** Khai báo danh sách thanh ghi cảm biến cần quét định kỳ (`Slave ID`, `FC01..FC04` / `FC05..FC16`, địa chỉ 1-based, kiểu `INT16`/`UINT16`/`INT32`/`REAL`/`BOOL`, thứ tự byte `ABCD`/`CDAB`/`BADC`/`DCBA`, hệ số nhân `Scale` và tên gợi nhớ Tag).
- **Modbus RTU Slave (`ModbusSlave`):** Đưa cổng về vai trò trạm ngoại vi phục vụ [Bản đồ Thanh ghi SCADA Cố định](/vi/tools/rs485-and-scada-slave) theo mã trạm `Slave ID (1..247)`.
- **Truyền Trong suốt (`Transference` / Passthrough):** Thu nhận nguyên vẹn chuỗi byte thô hoặc ký tự ASCII (đầu cân điện tử, máy quét barcode, máy in công nghiệp) dựa trên khoảng lặng ngắt khung `FrameTimeoutDelayMs` (mặc định `20 ms`, gói tối đa `512..2048 bytes`) và chuyển thẳng lên kênh Uplink.

### Phân mục `1`: Kết nối Không dây Wi-Fi & Địa chỉ IPv4 (`Wi-Fi`)
Cấu hình phân hệ thu phát Wi-Fi 2.4 GHz (`WifiEnabled`, `WifiAutoReconnect`, `SSID`, `Password`, quét danh sách mạng lân cận kèm chỉ số sóng `dBm`) kết hợp chế độ **DHCP** hoặc **IP Tĩnh (`Static IPv4`)** (`StaticIp`, `SubnetMask`, `Gateway`, `Primary/Secondary DNS`) và kiểm tra địa chỉ `MAC` phần cứng.

### Phân mục `2`: Giao thức Đẩy Dữ liệu Đám mây (`Cloud Uplink`)
Định tuyến dữ liệu thu thập được lên một trong ba giao thức mạng công nghiệp:

| Chế độ Uplink | Tham số Cấu hình Trọng tâm | Cơ chế Độ tin cậy & Bảo mật |
| :--- | :--- | :--- |
| **`MQTT` (`MQTT / MQTTS`)** | Broker Host, Port (`1883`/`8883`), Client ID, Username/Password, Publish Topic, Subscribe Topic, `QoS 0/1/2`, `Retain`. | Hỗ trợ đầy đủ chứng chỉ X.509 Root CA & Mutual TLS (`mTLS`), ghi đè SNI, cùng bản tin di chúc **Last Will & Testament (LWT)**. |
| **`HTTP REST` (`POST` / `PUT`)** | Endpoint URL, Phương thức (`POST`/`PUT`), Custom Headers (`Authorization: Bearer ...`, `Content-Type`), Timeout (`giây`), Bảng ánh xạ trường. | Hỗ trợ kích hoạt **Tức thời khi nhận dữ liệu (`OnDataReceived`)** hoặc **Định kỳ theo thời gian (`Periodic`)** chuẩn RFC 9110. |
| **`TCP Socket` (`Client` / `Server`)** | Chế độ Socket (`Client` kết nối tới `RemoteHost:RemotePort`, hoặc `Server` lắng nghe tại `LocalPort`). | Tự động nối lại theo `ReconnectDelaySec`, gói giữ phiên `HeartbeatPayload` (`PING` mỗi `30 giây`) và **Gói Báo danh Định danh chuẩn IEC 62443** (`None`, `OnConnect`, hoặc `PrependToEveryPacket`). |

### Phân mục `3`: Định thời, Bộ Định dạng JSON & Ước tính Dung lượng (`Timing`)
Điều khiển nhịp quét/phát dữ liệu (`PollingIntervalMs`, `PublishIntervalSec`, tối ưu gửi khi thay đổi `SendOnChange`) và đóng gói bản tin theo 3 chế độ:
1. **Chuẩn Công nghiệp IEC (No-Code Standard):** Tích chọn trực quan các trường `Device ID`, `Timestamp UTC`, `Port Index`, `Sequence Counter`, `Hex Stream`, `Payload Length` kèm khung xem trước JSON tức thì.
2. **Mẫu JSON Tùy biến (`Custom JSON Template`):** Tự do thiết kế cấu trúc JSON riêng cho AWS IoT, ThingsBoard, ERP thông qua hệ thống Macro DTU (`{{DEVICE_ID}}`, `{{TIMESTAMP}}`, `{{PORT}}`, `{{BAUDRATE}}`, `{{SERIAL_PAYLOAD}}`, `{{HEX_STREAM}}`, `{{PAYLOAD_LEN}}`, `{{PACKET_SEQ}}`), tích hợp bộ kiểm tra lỗi cú pháp JSON thời gian thực.
3. **Luồng Byte Thô (`Raw Transparent Passthrough`):** Bỏ qua hoàn toàn lớp vỏ JSON để truyền thẳng luồng byte nhị phân/Modbus/ASCII qua TCP Socket.

::: info Công cụ Ước tính Dung lượng Gói cước 4G/LTE Hàng tháng
Phân mục `3` tự động tính toán **Tải Đường truyền RS-485 (`Estimated Bus Load %`)** và **Lưu lượng Dữ liệu Ước tính Hàng tháng (`MB/tháng`)** dựa trên số lượng thanh ghi, kích thước gói JSON và chu kỳ gửi `PublishIntervalSec` — giúp kỹ sư chọn đúng gói cước SIM M2M 4G/LTE cho các trạm quan trắc xa.
:::

---

## 2. Trình Hướng dẫn Nhanh 1-2-3 (`Quick Wizard`) Cho Hiện trường

Khi cần cấu hình nhanh các cảm biến Modbus tiêu chuẩn ngay tại tủ điện nhà máy, hãy nhấn nút **Hướng dẫn Nhanh 1-2-3 (`Quick Wizard`)** trên thanh công cụ để hoàn tất trong 3 bước gọn nhẹ:

1. **Bước 1 — Khai báo Thanh ghi Cảm biến (`FastRegisters`):** Chọn tốc độ Baud (`9600 8N1`), nhập Slave ID, địa chỉ thanh ghi, kiểu dữ liệu, hệ số nhân `Scale` và tên biến (VD: `NhietDo_C`, `CongSuat_kW`). Nút **Tiếp theo** tự động mở khóa ngay khi có ít nhất 1 kênh hợp lệ.
2. **Bước 2 — Chọn Máy chủ Đích Uplink:** Chọn `MQTT`, `HTTP REST` hoặc `TCP Socket` và điền địa chỉ Host/URL cùng Topic/Header nhận tin.
3. **Bước 3 — Thiết lập Chu kỳ, Xem trước JSON & Hoàn tất:** Chọn chu kỳ đẩy tin `PublishIntervalSec` (mặc định `10 giây`), kiểm tra bản tin mẫu trên thẻ `LivePayloadPreview`, và nhấn **Áp dụng & Sinh Sơ đồ** để hoàn thành trong chưa đầy 60 giây.

---

## 3. Trình Phân tích Khung Wire Chuẩn Moxa & Bộ Bắt Gói tin Trực tiếp

Cột **Chẩn đoán Cảm biến & Phân tích Khung truyền (`Inspector`)** có thể đóng/mở bên phải cung cấp khả năng soi chiếu sâu tới từng byte trên đường dây RS-485 mà không cần dùng thêm cáp chuyển đổi USB-RS485 rời hay phần mềm thử nghiệm bên thứ ba:

```text
+-------------------------------------------------------------------------+
| PHÂN TÍCH KHUNG TRUYỀN MODBUS (MOXA STYLE)      [● COM4 · SYNX-DTU-485] |
+-------------------------------------------------------------------------+
| Kênh đích: Slave 1 · FC03 · 40001 (NhietDo_C)    Độ trễ: 14 ms  [TỐT]   |
| Giá trị Kỹ thuật: 28.4 °C                        Thanh ghi Thô: 284     |
| ----------------------------------------------------------------------- |
| Khung Hỏi TX (8B): [ 01 ] [ 03 ] [ 00 00 ] [ 00 01 ] [ 84 0A ] (CRC OK) |
| Phản hồi RX (7B):  [ 01 ] [ 03 ] [ 02 ] [ 01 1C ] [ B8 49 ]    (CRC OK) |
+-------------------------------------------------------------------------+
```

### 3.1 Trình Phân tích Khung Wire Modbus Chuẩn Moxa
- **Bóc tách Cấu trúc Byte (`ToggleWireFrame`):** Tách bạch rõ ràng từng thành phần trong gói hỏi `TX` và gói trả lời `RX`: `[ Slave ID ]`, `[ Mã hàm FC ]`, `[ Địa chỉ Wire 0-Based ]`, `[ Số Thanh ghi / Số Byte ]`, `[ Dữ liệu ]` và `[ Mã kiểm tra CRC-16 ]`.
- **Đọc Thử Đơn lẻ & Quét Liên tục Thời gian thực:** Nhấn **Đọc Thử 1 Lần (`Read Once`)** để phát lệnh kiểm tra ngay qua bo mạch đang kết nối, hoặc bật **Quét Liên tục (`Continuous Polling`)** để theo dõi **Giá trị Đã Quy đổi**, **Giá trị Thanh ghi Thô** và **Độ trễ Phản hồi (`ms`)** cập nhật trực tiếp.
- **Gợi ý Chẩn đoán Lỗi Tự động:** Các dải băng trạng thái chuẩn màu ANSI/ISA-101 (`Tốt`, `Cảnh báo`, `Lỗi`) tự động giải thích nguyên nhân Timeout, sai mã kiểm tra CRC hoặc mã ngoại lệ Modbus (`0x01..0x0B`) kèm hướng khắc phục đấu nối/địa chỉ.

### 3.2 Bộ Bắt Gói tin & Màn hình Chẩn đoán Trực tiếp (`Diagnostic Terminal`)
Chuyển sang tab **Giám sát & Bắn thử Trực tiếp** để kiểm thử luồng chuyển tiếp từ RS-485 lên Cloud:
- **Bắn thử Chuỗi ASCII hoặc Mã HEX (`TerminalSendFormat`):** Cho phép bơm thử chuỗi văn bản UTF-8 (VD: `TEMP:28.4,HUM:62.1`) hoặc chuỗi byte Hex nhị phân (VD: `01 03 00 00 00 02 C4 0B`).
- **Nhật ký Gói tin Song công:** Ghi vết chi tiết kèm dấu thời gian cho từng khung `[TX RS-485]`, `[RX RS-485]`, kết quả đối chiếu CRC-16, kết quả biên dịch mẫu JSON và phản hồi xác nhận từ máy chủ `[UPLINK MQTT/HTTP/TCP]` (`PUBACK` / `HTTP 200 OK`).

---

## 4. Tự động Đồng bộ Sơ đồ Khối FBD & Đọc Ngược Cấu hình từ Thiết bị

DTU Gateway Studio liên kết **đồng bộ hai chiều** với kiến trúc đồ thị logic của SynaptiX IDE, không bị đóng kín cứng nhắc như các phần mềm cấu hình DTU rời rạc truyền thống:

### 4.1 Tự động Sinh & Nối dây Khối hàm FBD (`AutoGenerateFbd`)
Khi bật tùy chọn **Tự động sinh sơ đồ khối FBD (`AutoGenerateFbd = true`)**, ngay khi lưu/áp dụng cấu hình DTU, hệ thống sẽ tự động kiến tạo toàn bộ mạch truyền thông trên [Sơ đồ Logic (`Ctrl+2`)](/vi/workspaces/logic-graph-tab):
- Khởi tạo các thanh ghi `VREG` đúng kiểu dữ liệu vật lý trong [Danh mục Biến (`Ctrl+3`)](/vi/workspaces/tag-catalog-tab) kèm tên gợi nhớ và đơn vị đo.
- Đặt và cấu hình các khối quét [`MB_READ`](/vi/blocks/mb-read) gom nhóm tối ưu theo từng Slave ID.
- Tự động đi dây từ các chân ngõ ra cảm biến sang khối [`MQTT_PUB`](/vi/blocks/mqtt-pub), [`HTTP_CLIENT`](/vi/blocks/http-client) hoặc [`TCP_CLIENT`](/vi/blocks/tcp-client) — nhờ đó kỹ sư có thể dễ dàng gắn thêm các khối so sánh cảnh báo tại chỗ [`COMPARATOR`](/vi/blocks/comparator), hiệu chuẩn [`SCALE`](/vi/blocks/scale) hoặc trễ chống nhiễu [`TON`](/vi/blocks/ton) ngay trên cùng một lưu đồ!

### 4.2 Đọc Ngược Cấu hình từ Thiết bị Vật lý (`F6` / `UploadFromDevice`)
Khi cắm cáp vào một thiết bị `SYNX-DTU-485` đang vận hành tại nhà máy và nhấn phím `F6` (hoặc nút **Đọc từ Thiết bị** trên thanh tiêu đề DTU Studio), SynaptiX IDE sẽ tải toàn bộ ảnh cấu hình đang hoạt động trong bộ nhớ Flash lên máy tính, tự động chuyển sang giao diện **DTU Gateway Studio** và khôi phục chính xác 100% thông số cổng RS-485, mạng Wi-Fi, cấu hình Uplink MQTT/HTTP/TCP, bản tin di chúc LWT và sơ đồ FBD.

---

## Tài liệu Liên quan

- [Cấu hình RS-485 & Bản đồ SCADA Slave (`Ctrl+Shift+B`)](/vi/tools/rs485-and-scada-slave)
- [Bảo mật MQTT TLS & Cấu hình Wi-Fi (`Ctrl+Shift+M` / `W`)](/vi/tools/mqtt-and-wifi-config)
- [Kịch bản Tích hợp Thực tế (AN-001 & AN-002)](/vi/tools/application-notes)
