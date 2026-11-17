# SPLC-SPEC-IEC-002: Tiêu Chuẩn Hóa Kiểu Dữ Liệu IEC 61131-3 & Giao Thức MQTT Công Nghiệp

> **Tài liệu đặc tả kỹ thuật kiến trúc hệ thống SimplePLC**  
> **Cấp độ:** Senior System & Industrial Automation Architect  
> **Mã đặc tả:** `SPLC-SPEC-IEC-002`  
> **Phiên bản:** 1.0  
> **Trạng thái:** BAN HÀNH QUY CHUẨN (Architecture Normative)  
> **Áp dụng cho:** SimplePLC Studio (App), SimplePLC Protocol, SimplePLC MCU Firmware

---

## 1. Mục Tiêu & Cơ Sở Tiêu Chuẩn Quốc Tế

Tài liệu này xác lập **nguồn chân lý duy nhất (Single Source of Truth)** cho hệ thống kiểu dữ liệu và mô hình truyền thông MQTT IIoT trong nền tảng SimplePLC, nhằm xóa bỏ triệt để các giải pháp chắp vá (ad-hoc / anti-patterns) và đưa kiến trúc SimplePLC tiệm cận các hệ thống tự động hóa chuẩn mực toàn cầu (Siemens TIA Portal, Beckhoff TwinCAT, CODESYS).

### 1.1. Các Tiêu Chuẩn Quốc Tế Tham Chiếu
1. **IEC 61131-3 (Programmable Controllers - Part 3: Programming Languages):**
   - Quy chuẩn về các kiểu dữ liệu cơ bản: `BOOL`, `DINT` (32-bit Signed Integer), và **`REAL` (32-bit Floating Point IEEE-754)**.
2. **ISO/IEC 20922:2016 / MQTT v3.1.1 / Sparkplug B:**
   - Quy chuẩn truyền thông IIoT nhẹ: Định dạng bản tin telemetry có cấu trúc JSON Key-Value hoặc binary payload mang đầy đủ kiểu dữ liệu tự nhiên.
3. **IEC 62541 (OPC Unified Architecture):**
   - Nguyên tắc gán kiểu dữ liệu tại Nguồn (Variable Node) thay vì cấu hình tại điểm tiêu thụ (Consumer Endpoint).

---

## 2. Nguyên Tắc Cốt Lõi: Tag Catalog Là Nguồn Chân Lý Duy Nhất

```
               [ THIẾT KẾ CHUẨN IEC 61131-3 ]
               
                 ┌───────────────────────────┐
                 │        TAG CATALOG        │
                 │   Nguồn Chân Lý Duy Nhất   │
                 └─────────────┬─────────────┘
                               │
            ┌──────────────────┼──────────────────┐
            ▼                  ▼                  ▼
      Tag: BOOL           Tag: DINT           Tag: REAL
       (1-bit)         (32-bit Integer)    (32-bit Float32)
            │                  │                  │
   ┌────────┴────────┐ ┌───────┴────────┐ ┌───────┴────────┐
   │ DI, DO, VFLAG,  │ │ AI (raw), VREG,│ │ AI (scaled),   │
   │ Modbus Coil     │ │ Counter, Retain│ │ VREG (real)    │
   └────────┬────────┘ └───────┬────────┘ └───────┬────────┘
            │                  │                  │
            └──────────────────┼──────────────────┘
                               │ (Tự động kế thừa kiểu)
                               ▼
        ┌──────────────────────────────────────────────┐
        │       CÁC PHÂN HỆ TIÊU THỤ (CONSUMERS)       │
        ├──────────────────────────────────────────────┤
        │ 1. Live Watch Table: Tự format bool/int/real │
        │ 2. FBD Comparators: Tự so sánh int vs float  │
        │ 3. MQTT Publish: Tự format JSON chuẩn        │
        │    (Người dùng KHÔNG PHẢI cấu hình số lẻ!)   │
        └──────────────────────────────────────────────┘
```

### 2.1. Phân loại kiểu dữ liệu IEC 61131-3 trong SimplePLC

| Kiểu dữ liệu | Kích thước | Định dạng nhị phân | Nhóm Tag áp dụng | Mô tả hiển thị |
| :--- | :---: | :--- | :--- | :--- |
| **`BOOL`** | 1 bit (lưu 32-bit) | `0` (FALSE) hoặc `1` (TRUE) | `DI`, `DO`, `VFLAG`, `ModbusCoil` | BẬT/TẮT, ON/OFF |
| **`DINT`** | 32 bits | Số nguyên có dấu bù 2 (`int32_t`) | `VREG`, `VREG_RETAIN`, `COUNTER`, `AI` (chế độ thô) | Số nguyên, ví dụ: `1500` |
| **`REAL`** | 32 bits | Chuẩn IEEE-754 Single Precision (`float`) | `AI` (chế độ số thực), `VREG` (số thực) | Số thực dấu phẩy động, ví dụ: `24.5` |

---

## 3. Khế Ước Bộ Nhớ 32-Bit & Modbus Wire Contract

### 3.1. Cấu trúc giá trị Tag trong bộ nhớ MCU (C Struct)
Mọi Tag trong SimplePLC đều chiếm dụng đúng **4 bytes (32 bits = 2 holding registers Modbus)**. Do đó, việc lưu trữ `DINT` hay `REAL` hoàn toàn bảo toàn kích thước vùng nhớ Modbus:

```c
/* Khế ước ô nhớ 32-bit chuẩn hóa cho từng Tag trên MCU */
typedef union SPLC_PACKED {
    int32_t  as_dint;    /* Kiểu số nguyên DINT (mặc định) */
    float    as_real;    /* Kiểu số thực REAL chuẩn IEEE-754 */
    uint32_t as_raw32;   /* Dữ liệu nhị phân thô 32-bit */
} SPLC_TagValue_t;
```

### 3.2. Truyền thông Modbus Runtime Tags (`0x0900`)
- Địa chỉ cơ sở: `0x0900`
- Mỗi Tag: Chiếm 2 thanh ghi liên tiếp (`High Word` truyền trước, `Low Word` truyền sau).
- Khi Tag là `REAL`: Client (App) và Server (MCU) đọc/ghi nhị phân thông qua:
  $$\text{value}_{\text{float}} = \text{BitConverter.Int32BitsToSingle}(\text{raw32})$$
- Tuyệt đối không áp dụng phép nhân/chia $10^{dec}$ cho các biến kiểu `REAL`.

---

## 4. Chuẩn Hóa Phân Hệ MQTT Telemetry (Publish)

### 4.1. Loại bỏ triệt để Anti-Pattern "Số chữ số lẻ" (`Offset +15`)
1. **Phía Studio (App):**
   - Loại bỏ hoàn toàn cột nhập "Số chữ số lẻ" / "Decimals" khỏi giao diện khối MQTT (`MqttConfigDialogView.xaml`).
   - Khối MQTT Publish tự động truy vấn `Tag.DataType` từ Tag Catalog:
     - Nếu `Tag.DataType == TagDataType.Float32` (`REAL`): Tự động serialize ra chuỗi số thực (ví dụ `24.5`).
     - Nếu `Tag.DataType == TagDataType.Int32` (`DINT`): Tự động serialize ra chuỗi số nguyên (ví dụ `245`).
     - Nếu `Tag.DataType == TagDataType.Boolean` (`BOOL`): Serialize `true/false` hoặc `1/0`.
2. **Phía Wire Contract & MCU:**
   - Offset `+15` trong bảng Telemetry (`0x0C00..0x0D7F`) được phục hồi về đúng nguyên bản là `reserved_header` (luôn ghi `0x0000`).
   - MCU khi format bản tin MQTT chỉ việc kiểm tra Tag Type: nếu là `REAL` thì dùng format `%.2f` hoặc `%f`, nếu là `DINT` thì `%d`.

### 4.2. Chuẩn hóa Bộ Token Custom Template
Để loại bỏ nguy cơ tràn bộ đệm RAM và quá tải thời gian thực trên MCU:
1. **Chế độ mặc định ưu tiên:** Khuyến nghị người dùng dùng **Standard JSON Mode**. MCU tự động xuất bản payload JSON chuẩn:
   ```json
   {"ts": 1728900000, "dev": "SPLC_01", "AI0": 24.5, "DO0": 1}
   ```
2. **Chế độ Custom Template:**
   - Loại bỏ hoàn toàn token `{valK:N}`.
   - Chỉ hỗ trợ các token chuẩn macro: `{val1}`..`{val8}`, `{tag1}`..`{tag8}`, `{vals}`, `{device}`, `{ts}`.
   - Giới hạn chuỗi template: Tối đa 128 bytes. Compiler Studio sẽ kiểm tra độ dài mở rộng dự kiến (Expected Expanded Size) tại thời điểm Compile. Nếu vượt quá 128 bytes, Studio sẽ chặn nạp và báo lỗi rõ ràng cho kỹ sư.

### 4.3. Thuật toán Biến thiên Dữ liệu (Deadband COV)
Trường `DEADBAND` tại offset `+4..5` là một số thực **IEEE-754 Float32**.
- Đối với Tag kiểu `REAL`: Thuật toán COV trên MCU so sánh trực tiếp số thực:
  $$\Delta = |v_{\text{current}} - v_{\text{last}}| \ge \text{Deadband}_{\text{float}}$$
- Tuyệt đối không quy đổi sang số nguyên nhân $10^{dec}$.

---

## 5. Chuẩn Hóa Thăm Dò Năng Lực Phần Cứng (Device Capability Matrix)

Thay vì gửi lệnh thăm dò ad-hoc thanh ghi `0x0E54` trước mỗi lần Deploy:

### 5.1. Khai báo Năng lực tại Vùng Nhận Diện Hệ Thống (`0x0A00`)
Tại thanh ghi `0x0A08` (thuộc vùng System Identity `0x0A00..0x0A0F`), MCU cung cấp thanh ghi 32-bit `HardwareCapabilities`:

```c
typedef enum {
    SPLC_CAP_BASE_LOGIC   = 0x0001,  /* Hỗ trợ Logic Engine FBD cơ bản */
    SPLC_CAP_RETENTIVE    = 0x0002,  /* Hỗ trợ vùng nhớ Retentive NVRAM */
    SPLC_CAP_RTC          = 0x0004,  /* Hỗ trợ Real-Time Clock phần cứng */
    SPLC_CAP_ETHERNET     = 0x0008,  /* Hỗ trợ cổng mạng vật lý */
    SPLC_CAP_MQTT_PUB     = 0x0010,  /* Hỗ trợ phân hệ MQTT Telemetry */
    SPLC_CAP_MQTT_SUB     = 0x0020,  /* Hỗ trợ phân hệ MQTT Subscribe */
    SPLC_CAP_SD_DATALOG   = 0x0040   /* Hỗ trợ thẻ nhớ MicroSD */
} SPLC_HardwareCapabilities_t;
```

### 5.2. Nguyên tắc Tương tác của Studio
- **Lúc Kết nối (Connection Handshake):** Studio đọc `0x0A08` một lần duy nhất.
- Nếu `(Capabilities & SPLC_CAP_MQTT_PUB) == 0`:
  - Studio hiển thị thông báo trên Device Info panel: *"Phần cứng không hỗ trợ MQTT"*.
  - Trong logic editor, khối MQTT sẽ có viền cảnh báo không tương thích phần cứng.
  - Khi Deploy, Studio tự động bỏ qua việc nạp phân vùng MQTT mà không làm gián đoạn việc nạp Rule Table logic.

---

## 6. Đặc Tả Giao Thức MQTT Subscribe (Phase 2 Wire Contract)

### 6.1. Bản chất Kiến trúc của MQTT Subscribe trong PLC
Khối MQTT Subscribe là cơ chế **cho phép hệ thống Cloud/SCADA ghi trực tiếp giá trị vào một Tag nội bộ của PLC** thông qua giao thức MQTT.

```
   [ Cloud / SCADA / App ]
              │ (Publish JSON: {"target_temp": 65.5})
              ▼
       [ MQTT Broker ]
              │ (Deliver to Subscribed Topic)
              ▼
      [ SimplePLC MCU ]
              │ 1. Parse JSON Key "target_temp"
              │ 2. Ép kiểu giá trị (65.5f)
              ▼
   [ Target Tag: VREG0 (REAL) = 65.5f ]
```

### 6.2. Cấu trúc Vùng Nhớ Wire Contract cho Khối Subscribe
- **Phân vùng Modbus:** `0x0C80..0x0CFF` (dành cho tối đa 4 khối Subscribe, mỗi khối 32 registers = 64 bytes).
- **Layout chi tiết từng khối Subscribe (64 bytes):**

| Offset (Word) | Byte Size | Kiểu dữ liệu | Tên trường | Ý nghĩa |
| :---: | :---: | :---: | :--- | :--- |
| `+0..15` | 32 bytes | `char[32]` | `SUB_TOPIC` | Topic MQTT cần đăng ký (ASCII, null-terminated) |
| `+16` | 2 bytes | `uint16_t` | `TARGET_TAG` | Chỉ số Tag đích (0..127) nhận giá trị |
| `+17` | 2 bytes | `uint8_t[2]` | `FLAGS_QOS` | Byte cao = `Enabled (1/0)`, Byte thấp = `QoS (0/1)` |
| `+18..33` | 32 bytes | `char[32]` | `JSON_KEY` | Key cần bóc tách trong JSON (ví dụ `"setpoint"`). Nếu rỗng: nhận toàn bộ raw payload. |

---

## 7. Lộ Trình Triển Khai & Cam Kết Kỷ Luật Kỹ Thuật

1. **Giai đoạn 1 (Ngay lập tức - App Core):**
   - Nâng cấp Tag Catalog trong Studio: Mở cột `DataType` (`BOOL`, `DINT`, `REAL`).
   - Xóa bỏ cột "Số chữ số lẻ" khỏi UI MQTT Publish.
   - Sửa `MqttPublishNodeViewModel` để tự động suy luận định dạng số thực/số nguyên từ Tag được gắn.
2. **Giai đoạn 2 (Firmware Alignment):**
   - Chuyển giao tài liệu `SPLC-SPEC-IEC-002` này cho đội Firmware làm căn cứ kỹ thuật chuẩn.
   - Đội Firmware loại bỏ xử lý offset `+15`, chuyển sang format trực tiếp từ kiểu dữ liệu của Tag.
   - Triển khai Wire Contract Subscribe Phase 2 theo Mục 6.
