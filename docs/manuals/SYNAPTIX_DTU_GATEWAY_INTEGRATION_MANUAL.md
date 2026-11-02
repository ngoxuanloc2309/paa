# TÀI LIỆU HƯỚNG DẪN TÍCH HỢP & VẬN HÀNH THIẾT BỊ DTU GATEWAY
## (SYNAPTIX INDUSTRIAL DATA TRANSFER UNIT MANUAL)

---

## 1. Tổng Quan Về Thiết Bị DTU Gateway (Overview)

**SynaptiX DTU (Data Transfer Unit - Model: `SYNX-DTU-485`)** là dòng thiết bị Gateway truyền thông công nghiệp đa năng, đóng vai trò cầu nối dữ liệu thông minh giữa các thiết bị ngoại vi hiện trường qua giao tiếp nối tiếp **RS-485** lên hệ thống mạng Internet / Cloud / SCADA thông qua các giao thức **MQTT(s)**, **HTTP(s) REST API** và **TCP Socket (Client/Server)**.

```mermaid
flowchart LR
    subgraph Hiện Trường (Field Devices)
        S1["Cảm biến Nhiệt/Ẩm RS-485"] --> RS485["Bus RS-485 (Port 1..4)"]
        S2["Đồng hồ Điện MFM383A"] --> RS485
        S3["Biến tần / PLC Slave"] --> RS485
    end

    RS485 --> DTU["SynaptiX DTU Gateway\n(SYNX-DTU-485)"]

    subgraph Internet & Cloud
        DTU -- "RS485 to MQTT" --> MQTT["MQTT Broker\n(AWS / HiveMQ / EMQX)"]
        DTU -- "RS485 to HTTP" --> HTTP["HTTP REST Server\n(Cloud Web API / ERP)"]
        DTU -- "RS485 to TCP" --> TCP["TCP Socket Server / Client\n(SCADA / In-house Server)"]
    end
```

### Các Đặc Tính Kỹ Thuật Nổi Bật:
1. **Đa chức năng cổng RS-485:**
   - **Truyền trong suốt (Transference / Passthrough):** Chuyển tiếp nguyên trạng luồng dữ liệu byte thô hoặc text giữa Serial và Network theo độ trễ ngắt khung (Delimiter Timeout).
   - **Modbus RTU Master:** Tự động gửi lệnh đọc dữ liệu định kỳ từ các Slave hiện trường, phân tích cú pháp và đóng gói JSON.
   - **Modbus RTU Slave:** Đáp ứng các lệnh đọc/ghi từ SCADA, PLC hoặc HMI bên ngoài vào bảng nhớ nội bộ.
2. **Đa kênh Uplink mạng Internet:**
   - **MQTT(s) Client:** Hỗ trợ xác thực TLS/SSL x509, Keep-Alive, QoS 0/1/2, Retain message và bản tin di chúc **Last Will & Testament (LWT)**.
   - **HTTP(s) REST Client:** Hỗ trợ phương thức POST/PUT, cấu hình Endpoint URL, tùy biến Custom Headers (`Authorization`, `Content-Type`), thiết lập thời gian timeout và chu kỳ gửi tự động.
   - **TCP Socket Client / Server:** Hỗ trợ cả 2 chế độ (Client tự động kết nối ra máy chủ từ xa kèm cơ chế tự khôi phục kết nối Reconnect Delay; Server mở cổng lắng nghe kết nối nội bộ) cùng tính năng Heartbeat giữ phiên.
3. **Bộ định dạng bản tin JSON linh hoạt (JSON Payload Formatter):**
   - Hỗ trợ xây dựng template JSON động bằng hệ thống Macro (`{{DEVICE_ID}}`, `{{TIMESTAMP}}`, `{{PORT}}`, `{{BAUDRATE}}`, `{{SERIAL_PAYLOAD}}`, `{{HEX_STREAM}}`).
   - Tích hợp công cụ kiểm tra cú pháp JSON Syntax Validator và Live Preview thời gian thực.
4. **Bảng điều khiển Giám sát & Bắn thử gói tin (Diagnostic Terminal):**
   - Giúp kỹ sư kiểm thử truyền gói tin trực tiếp mà không cần đấu nối phần cứng vật lý.

---

## 2. Quy Hoạch Vùng Nhớ Thanh Ghi Nhị Phân (Holding Register Map)

Theo tiêu chuẩn khế ước truyền thông **Wire Protocol V2.0 (SPLC-SPEC-DTU-001 & SPLC-SPEC-MQTT-004)**, toàn bộ tham số của DTU được ánh xạ cố định và kiểm soát chặt chẽ:

| Dải Địa Chỉ | Số Lượng Regs | Kích Thước | Phân Vùng Chức Năng | Ghi Chú |
|:---|:---:|:---:|:---|:---|
| `0x0E60 - 0x0E7F` | 32 | 64 Bytes | **MQTT Last Will & Testament (LWT)** | Will Topic, Will Payload, QoS, Retain |
| `0x1600 - 0x163F` | 64 | 128 Bytes | **DTU HTTP REST Client Config** | Endpoint URL, Method, Timeout, Headers |
| `0x1640 - 0x167F` | 64 | 128 Bytes | **DTU TCP Socket Config** | Mode Client/Server, IP/Host, Ports, Heartbeat |
| `0x1680 - 0x16BF` | 64 | 128 Bytes | **DTU Routing & Serial Mode** | Port Index, Serial Mode, Uplink Mode, Delimiter |

> [!NOTE]
> **Đặc tả bộ nhớ Firmware:** Toàn bộ dải thanh ghi `0x1600..0x16BF` chứa các trường cấu hình nhị phân có cấu trúc (`DtuHttpConfig`, `DtuTcpConfig`, `DtuRouting`). Dải này **hoàn toàn không có thanh ghi lưu trữ chuỗi JSON Template**. Mọi cấu hình Template JSON động đều do App Studio quản lý trong file dự án (`.splc`), firmware không cần phải nạp hay giải mã template chuỗi tự do.

---

## 3. Hướng Dẫn Cấu Hình Chi Tiết Trên Synaptix IDE

### Bước 1: Khởi tạo hoặc Mở dự án DTU
- Trong menu **Tệp (File) -> Tạo dự án mới...**: Chọn Preset **`SynaptiX DTU`**.
- Khi mở dự án DTU, Synaptix IDE tự động hiển thị không gian làm việc chuyên biệt **DTU Gateway Studio**.
- *Lưu ý (Chế độ Hybrid):* Đối với các dự án Controller hoặc Remote I/O đã có sẵn, kỹ sư có thể mở cấu hình DTU bất kỳ lúc nào bằng tổ hợp phím **`Ctrl+Shift+D`** hoặc vào menu **Thiết bị -> Cấu hình DTU Gateway...**.

### Bước 2: Cấu hình Cổng RS-485 (Tab 1)
1. **Cổng RS-485 áp dụng:** Chọn từ Cổng 1 đến Cổng 4 (tương ứng `PortIndex = 0..3`).
2. **Chức năng cổng (Serial Mode) & Thứ bậc hiệu lực với `0x1400`:**
   - `0x1400` quản lý thông số vật lý của cổng (Baudrate, DataBits, Parity, StopBits).
   - Khi `0x1680` kích hoạt chế độ DTU:
     - *Truyền trong suốt (Transference / Mode 0):* Cổng được bàn giao độc quyền cho module DTU Stream Engine. Toàn bộ luồng UART DMA byte thô chuyển thẳng lên kênh Uplink. Tại `0x1400`, các parser Modbus Master / Slave trên cổng này sẽ được tạm ngưng để tránh xung đột.
     - *Modbus RTU Master (Mode 1):* Cổng hoạt động theo vai trò Master Poller tiêu chuẩn.
     - *Modbus RTU Slave (Mode 2):* Cổng hoạt động theo vai trò Slave Server với Slave ID đã chọn.
3. **Thông số đường truyền:** Thiết lập Baud rate (9600, 19200, 38400, 57600, 115200 bps), Data bits (8/7), Parity (None/Even/Odd), Stop bits (1/2).
4. **Delimiter Timeout (ms):** Thời gian chờ không có byte mới trên đường dây RS-485 để kết luận kết thúc một frame (mặc định 20ms).
5. **Kích thước gói tin tối đa:** Mặc định 512 bytes (tối đa 2048 bytes).

### Bước 3: Cấu hình Chế Độ Truyền Nhận Internet (Tab 2)
Kỹ sư lựa chọn một trong các chế độ Uplink:
- **Chế độ 1: RS-485 to MQTT(s)**
  - Nhập địa chỉ Broker Host (VD: `broker.emqx.io` hoặc IP nội bộ).
  - Cổng kết nối (1883 cho Plain TCP hoặc 8883 cho TLS có mã hóa).
  - Client ID, Username, Password.
  - Publish Topic nhận dữ liệu DTU (VD: `factory/line1/dtu/telemetry`).
  - Cấu hình bản tin di chúc **LWT (Last Will & Testament)**: Giúp Cloud phát hiện tức thời khi DTU bị mất nguồn hoặc đứt cáp mạng.
- **Chế độ 2: RS-485 to HTTP(s) REST**
  - Phương thức: `POST` hoặc `PUT`.
  - Endpoint URL: `https://api.factory-cloud.io/v1/sensors/push`.
  - Request Headers: Thêm token xác thực (VD: `Authorization: Bearer <TOKEN>`).
  - **Chế độ kích hoạt (Trigger Mode - RFC 9110):**
    * *Khi nhận dữ liệu RS-485 (On Serial Data Received - Event-Driven):* Gửi ngay lập tức khi nhận được 1 frame từ RS-485 (thích hợp cho đầu cân, máy in, máy quét barcode).
    * *Định kỳ theo thời gian (Periodic Interval):* Gửi theo chu kỳ số giây đã cài đặt.
- **Chế độ 3: RS-485 to TCP Socket (Client/Server)**
  - *TCP Client:* Nhập IP/Domain máy chủ đích và Cổng từ xa. DTU tự động thiết lập socket và tái kết nối theo chu kỳ `Reconnect Delay`.
  - *TCP Server:* Nhập Cổng lắng nghe cục bộ (`Local Port`). Các client SCADA kết nối vào IP của DTU để thu thập dữ liệu.
  - *Heartbeat Keep-Alive:* Định kỳ gửi chuỗi ký tự giữ phiên (VD: `PING`) để tránh router ngắt phiên NAT.
  - **Gói tin Báo danh Định danh (Registration / Login Packet - IEC 62443-4-2 CR 1.2):**
    * *Không sử dụng (None):* Truyền luồng socket trực tiếp.
    * *Gửi khi vừa kết nối (Send on Connect):* Ngay khi vừa thông socket TCP, DTU tự động gửi chuỗi mã trạm (VD: `ID:FACTORY_GATEWAY_01` hoặc macro `{{DEVICE_ID}}`) để Server nhận diện nguồn.
    * *Gắn tiền tố vào đầu mỗi gói tin (Prepend to Every Packet):* Gắn mã trạm vào đầu mỗi frame dữ liệu gửi lên.

### Bước 4: Định Dạng Dữ Liệu (Tab 3: Payload Formatter — 3 Chế Độ)
Hệ thống cung cấp 3 chế độ định dạng dữ liệu linh hoạt:
1. **Chế độ Chuẩn Công Nghiệp (Standard IEC Telemetry - No-Code):**
   - Tích chọn trực tiếp các trường: `Device ID`, `Timestamp UTC`, `Port Index`, `Sequence Counter`, `Hex Stream`, `Payload Length`.
   - DTU tự động đóng gói JSON chuẩn hóa quốc tế (chuẩn ISA-95 / IEC 62541).
2. **Chế độ Tùy Biến (Custom JSON Template):**
   - Soạn thảo mẫu JSON tự do cho các nền tảng đặc thù (AWS IoT Core, ThingsBoard, ERP) bằng hệ thống Macro (`{{DEVICE_ID}}`, `{{TIMESTAMP}}`, `{{PORT}}`, `{{BAUDRATE}}`, `{{SERIAL_PAYLOAD}}`, `{{HEX_STREAM}}`, `{{PAYLOAD_LEN}}`, `{{PACKET_SEQ}}`).
3. **Chế độ Dữ Liệu Thô (Raw Transparent Passthrough):**
   - Bỏ qua hoàn toàn tầng đóng gói JSON. Toàn bộ luồng byte RS-485 được chuyển tiếp nguyên bản lên mạng (phù hợp cho Server SCADA socket truyền thống).
Cú pháp mẫu tiêu chuẩn trên giao diện Studio:
```json
{
  "device_id": "{{DEVICE_ID}}",
  "timestamp": "{{TIMESTAMP}}",
  "port": {{PORT}},
  "baudrate": {{BAUDRATE}},
  "payload": "{{SERIAL_PAYLOAD}}",
  "hex_stream": "{{HEX_STREAM}}"
}
```
* **Cơ chế Phía Studio:** Tab này là công cụ thiết kế, xem trước trực quan (Live Preview) và lưu vào metadata dự án (`_metadata.DtuConfig.CustomJsonTemplate`).
* **Cơ chế Phía Firmware (Tùy theo kênh Uplink):**
  - **Uplink HTTP REST (`0x1600`):** Firmware không cần parser macro. Firmware đóng gói JSON chuẩn dựa trên các trường cấu hình ở `HttpFields` hoặc đẩy trực tiếp raw body theo `Content-Type`.
  - **Uplink MQTT(s):** Nếu người dùng yêu cầu template tùy biến, template đó được nạp xuống vi điều khiển qua phân hệ **MQTT Telemetry Blocks (`0x0C00..0x0D7F`)** theo đặc tả `SPLC-SPEC-MQTT-001`, trong đó mỗi khối có 64 thanh ghi (128 bytes) dành riêng cho `PAYLOAD_TEMPLATE`.
  - **Uplink TCP Socket / Truyền trong suốt:** Firmware chuyển tiếp byte thô trực tiếp (Raw Stream), hoàn toàn không bọc thêm JSON.

### Bước 4.1: Lộ Trình Tính Năng Firmware & Hardware Capabilities
- Năng lực phần mềm DTU được khai báo độc lập với hạ tầng truyền dẫn vật lý (Ethernet / Wi-Fi / LTE).
- **Lộ trình hỗ trợ firmware:**
  - *Giai đoạn 1 (MVP - Bắt buộc):* Chế độ **Truyền trong suốt (Transference) qua TCP Client** và **MQTT Telemetry**.
  - *Giai đoạn 2:* Hỗ trợ **HTTP REST POST Client** (`HTTP_CLIENT = 0x1000`).
  - *Giai đoạn 3:* Hỗ trợ **TCP Server**.

### Bước 5: Kiểm tra với Live Diagnostic Terminal (Tab 4)
1. Chọn định dạng dữ liệu bắn thử: **ASCII Text** (chuỗi ký tự UTF-8 như `TEMP:29.8,HUM:55.4`) hoặc **HEX Bytes** (chuỗi mã Hex như `01 03 00 00 00 02 C4 0B` để kiểm thử bản tin Modbus RTU nhị phân). Nhập chuỗi vào ô truyền thử.
2. Nhấn **`🚀 Gửi Thử Nghiệm (Send Test)`**.
3. Quan sát kết quả xử lý thời gian thực trên màn hình đen Terminal công nghiệp:
   - Gói tin ghi nhận tại RS-485.
   - Quá trình chuyển đổi định dạng JSON.
   - Bản tin xuất bản lên kênh Uplink tương ứng kèm xác nhận (ACK).

### Bước 6: Nạp Cấu Hình Xuống Thiết Bị (Deploy)
- Nhấn nút **`🚀 Nạp DTU xuống Thiết Bị`** trên thanh tiêu đề. Toàn bộ cấu hình sẽ được chuyển đổi nhị phân, kiểm tra checksum và nạp qua quy trình Staging an toàn vào vi điều khiển (`0x1600..0x16BF`).

### Bước 7: Đọc Cấu Hình Từ Thiết Bị (Upload)
- Khi kết nối với bo mạch DTU Gateway vật lý đã cấu hình trước đó, kỹ sư có thể đọc toàn bộ thông số đang chạy trên MCU lên giao diện SynaptiX IDE:
  1. Vào menu **`PLC` -> `Đọc cấu hình từ thiết bị (Upload)...`** (hoặc phím tắt trên thanh công cụ).
  2. Studio sẽ tự động gửi lệnh Modbus FC03 đọc toàn bộ hình ảnh thiết bị (`FullDeviceConfigurationDto`):
     - Định tuyến Routing RS-485 (`0x1680..0x16BF`).
     - Cấu hình HTTP REST Client (`0x1600..0x163F`).
     - Cấu hình TCP Socket Client / Server (`0x1640..0x167F`).
     - Cấu hình MQTT Broker & LWT Di chúc (`0x0E00..0x0E7F`).
  3. Đối với thiết bị thuần DTU Gateway (`device_class == 5`), Studio **tự động chuyển ngay sang màn hình `DTU Gateway Studio` (Tab 0)** và điền chính xác toàn bộ thông số vừa tải lên từ thiết bị (Baud Rate, Chế độ truyền, URL, TCP Host/Port, JSON Template).

---

## 4. Kịch Bản Demo Thực Chiến Mẫu (Step-by-Step Demo Scenario)

### Tình huống: Giám sát Cảm biến Môi trường Nhà xưởng qua MQTT
1. **Thiết bị đầu vào:** Cảm biến nhiệt độ, độ ẩm phát chuỗi text `TEMP:31.2,HUM:70.1` qua cổng RS-485 mỗi 2 giây với tốc độ 9600-8-N-1.
2. **Yêu cầu:** Đóng gói thành bản tin JSON và xuất bản lên MQTT Broker `broker.hivemq.com:1883`, Topic `synaptix/factory/env`.
3. **Thực hiện:**
   - Tại Tab RS-485: Chọn Mode `Transference`, Baud Rate `9600`, Delimiter `20ms`.
   - Tại Tab Uplink: Chọn `MQTT`, Host `broker.hivemq.com`, Port `1883`, Topic `synaptix/factory/env`.
   - Tại Tab JSON: Giữ khuôn mẫu JSON tiêu chuẩn.
   - Tại Tab Terminal: Nhập `TEMP:31.2,HUM:70.1` và nhấn `Gửi Thử Nghiệm`.
   - Kết quả: Terminal hiển thị log `[TX RS-485]`, `[UPLINK MQTT] Publishing to synaptix/factory/env`, `[MQTT BROKER ACK] PUBACK received`.
   - Nhấn `Lưu Cấu Hình` hoặc `Nạp DTU xuống Thiết Bị`.
