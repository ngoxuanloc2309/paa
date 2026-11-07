# Hệ sinh thái & Cấu hình Thiết bị Định sẵn

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-CON-001</span>
  <span><strong>Chương:</strong> 01. Tổng quan &amp; Kiến trúc Cốt lõi</span>
  <span><strong>Áp dụng cho:</strong> SynaptiX IDE v2.x &bull; Contract Profile V2</span>
  <span><strong>Tiêu chuẩn:</strong> IEC/IEEE 82079-1 &bull; ANSI/ISA-101</span>
</div>

**SynaptiX IDE** là môi trường kỹ thuật công nghiệp định hướng phần mềm (software-first) phát triển trên nền tảng .NET 8, chuyên dụng để thiết kế logic điều khiển biên tất định, cấu hình truyền thông IIoT, mô phỏng kiểm thử, nạp chương trình nguyên tử và giám sát thời gian thực cho toàn bộ **Hệ sinh thái Tự động hóa Biên & IIoT SynaptiX**.

Thay vì buộc kỹ sư tích hợp phải sử dụng nhiều phần mềm rời rạc cho điều khiển I/O cục bộ, đọc thiết bị RS-485 Modbus RTU và viết kịch bản đẩy dữ liệu đám mây MQTT/HTTP, **SynaptiX IDE** hợp nhất sơ đồ khối trực quan, hợp đồng bộ nhớ vật lý nghiêm ngặt, trình mô phỏng chu kỳ chính xác, nạp Flash hai pha an toàn và bảng giám sát trực tuyến 20 FPS trong một bàn làm việc duy nhất.

---

## Kiến trúc Tổng thể & Vai trò trong Hệ thống

Trong nhà máy công nghiệp hoặc trạm quan trắc phân tán, **Thiết bị Biên SynaptiX (SynaptiX Edge Device)** đóng vai trò cầu nối trực tiếp giữa tầng chấp hành hiện trường (cảm biến, rơ-le, biến tần, đồng hồ điện năng) và tầng giám sát điều hành IT/OT (**SynaptiX IDE**, hệ thống SCADA nội bộ, MQTT Broker và dịch vụ REST Webhook).

<div class="synx-arch-grid">
  <!-- Card 1: Field -->
  <div class="synx-arch-card">
    <div class="synx-arch-header">
      <div class="synx-arch-icon">1</div>
      <div class="synx-arch-title-wrap">
        <h4 class="synx-arch-title">Tầng Thiết bị Hiện trường</h4>
        <span class="synx-arch-subtitle">Field Instrumentation</span>
      </div>
    </div>
    <div class="synx-arch-items">
      <div class="synx-arch-item">
        <strong>Tín hiệu I/O Trực tiếp (Discrete & Analog)</strong>
        <span>DI0..7 (Optocoupler), DO0..7 (Transistor), AI0..3 (4-20mA / 0-10V)</span>
        <span class="synx-arch-badge">IEC 61131-2</span>
      </div>
      <div class="synx-arch-item">
        <strong>RS-485 Fieldbus Slaves</strong>
        <span>Biến tần VFD, Đồng hồ điện đa năng, Cảm biến nhiệt ẩm công nghiệp</span>
        <span class="synx-arch-badge">Modbus RTU • 9600..115200 bps</span>
      </div>
    </div>
  </div>

  <!-- Card 2: Edge -->
  <div class="synx-arch-card">
    <div class="synx-arch-header">
      <div class="synx-arch-icon">2</div>
      <div class="synx-arch-title-wrap">
        <h4 class="synx-arch-title">Bộ điều khiển Biên SynaptiX</h4>
        <span class="synx-arch-subtitle">Core Deterministic Runtime</span>
      </div>
    </div>
    <div class="synx-arch-items">
      <div class="synx-arch-item">
        <strong>Bộ nhớ Tag Hợp nhất (Unified Tag Catalog)</strong>
        <span>128 ô nhớ toàn cục: DI, DO, AI, VREG, VREG_RETAIN, COUNTER</span>
        <span class="synx-arch-badge">Zero-Allocation</span>
      </div>
      <div class="synx-arch-item">
        <strong>Động cơ Quét Tất định (Deterministic Scan)</strong>
        <span>Chu kỳ 10ms ổn định: Đọc ngõ vào → Tính toán FBD → Ghi ngõ ra</span>
        <span class="synx-arch-badge">Hard Real-time</span>
      </div>
      <div class="synx-arch-item">
        <strong>Worker Truyền thông Bất đồng bộ</strong>
        <span>Modbus Master Poller, MQTT Client, HTTP Webhook, TCP Client</span>
        <span class="synx-arch-badge">Non-blocking DMA</span>
      </div>
    </div>
  </div>

  <!-- Card 3: Supervisory -->
  <div class="synx-arch-card">
    <div class="synx-arch-header">
      <div class="synx-arch-icon">3</div>
      <div class="synx-arch-title-wrap">
        <h4 class="synx-arch-title">Tầng Kỹ thuật & Đám mây</h4>
        <span class="synx-arch-subtitle">IT / OT Supervisory Layer</span>
      </div>
    </div>
    <div class="synx-arch-items">
      <div class="synx-arch-item">
        <strong>SynaptiX IDE Engineering Workbench</strong>
        <span>Nạp giao dịch nguyên tử 2 pha, chẩn đoán Live Watch 20 FPS</span>
        <span class="synx-arch-badge">USB CDC / Modbus TCP :502</span>
      </div>
      <div class="synx-arch-item">
        <strong>Trạm SCADA / HMI Master</strong>
        <span>Ignition, WinCC, Kepware truy xuất Bản đồ Modbus Slave cố định</span>
        <span class="synx-arch-badge">FC01..04 / FC05..16</span>
      </div>
      <div class="synx-arch-item">
        <strong>Nền tảng IIoT Đám mây</strong>
        <span>Gửi Telemetry JSON định kỳ lên AWS IoT, EMQX, HiveMQ Cloud</span>
        <span class="synx-arch-badge">MQTT TLS 1.2/1.3</span>
      </div>
    </div>
  </div>
</div>

### Quy trình Kỹ thuật Định hướng Hồ sơ Tài nguyên

Mọi dự án trong **SynaptiX IDE** đều được chi phối bởi một **Hồ sơ Tài nguyên Thiết bị (Product Resource Profile)** tường minh. Khi kỹ sư chọn hoặc nhận diện tự động cấu hình thiết bị mục tiêu, phần mềm sẽ tự động thiết lập đồng bộ:

1. **Cấp phát Không gian Bộ nhớ Tag:** Khởi tạo chính xác số lượng kênh vật lý và thanh ghi ảo (`DI`, `DO`, `AI`, `VFLAG`, `VREG`, `VREG_RETAIN`, `COUNTER`) trong [Danh mục Biến — Tag Catalog](/vi/workspaces/tag-catalog-tab).
2. **Hộp công cụ Khối hàm Thích ứng Phần cứng:** Tự động bật hoặc khóa các khối truyền thông (`MB_READ`, `MB_WRITE`, `MQTT_PUB`, `MQTT_SUB`, `HTTP_CLIENT`, `TCP_CLIENT`) trên [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab) dựa theo năng lực phần cứng — hoặc chuyển thẳng sang giao diện chuyên dụng [DTU Gateway Studio](/vi/tools/dtu-gateway-studio) khi chọn dòng thiết bị DTU.
3. **Cổng Kiểm tra Biên dịch & Tiền Triển khai:** Kiểm soát chặt chẽ giới hạn số lượng luật tối đa và cờ năng lực ngoại vi khi biên dịch (`F7`) và [Triển khai Nguyên tử](/vi/workspaces/deploy-tab) (`Ctrl+F5`), ngăn chặn tuyệt đối việc nạp nhầm cấu hình vượt quá tài nguyên thiết bị thực tế.

---

## Bảng So sánh 7 Cấu hình Thiết bị Định sẵn (Built-In Presets)

**SynaptiX IDE** tích hợp sẵn **7 mẫu cấu hình thiết bị thương mại tiêu chuẩn**, đáp ứng trọn vẹn từ module I/O phân tán, bộ điều khiển biên độc lập, bộ ghi dữ liệu cảm biến đến cổng chuyển đổi giao thức và thiết bị truyền dữ liệu công nghiệp DTU.

### Bảng Thông số Dung lượng Tài nguyên

| Tên Cấu hình (Preset) | Họ Thiết bị | I/O Vật lý Cục bộ (`DI / DO / AI`) | Vùng nhớ Ảo & Lưu trữ (`VFLAG / VREG / RETAIN / CNT`) | Tổng số Tag (Trần: 128) | Số Luật Tối đa (Trần: 100) |
| :--- | :--- | :---: | :---: | :---: | :---: |
| **SynaptiX Remote I/O** | Remote I/O (Mặc định) | `8 / 8 / 4` | `32 / 32 / 32 / 8` | `124` | `100` |
| **SynaptiX Compact I/O** | Remote I/O (Nhỏ gọn) | `4 / 4 / 0` | `16 / 16 / 16 / 4` | `60` | `50` |
| **SynaptiX Relay Unit** | Remote I/O (Rơ-le số) | `8 / 8 / 0` | `32 / 32 / 32 / 8` | `120` | `100` |
| **SynaptiX Data Logger** | Thiết bị Ghi dữ liệu | `4 / 2 / 4` | `16 / 32 / 32 / 4` | `94` | `50` |
| **SynaptiX Gateway** | Cổng Giao thức | `2 / 2 / 0` | `32 / 32 / 32 / 4` | `104` | `50` |
| **SynaptiX Controller** | Bộ Điều khiển Biên | `8 / 8 / 4` | `32 / 32 / 32 / 8` | `124` | `100` |
| **SynaptiX DTU** | Thiết bị Truyền tin RS-485 | `0 / 0 / 0` | `32 / 32 / 32 / 4` | `100` | `50` |

### Ma trận Phân hệ Phần cứng & Giao thức Hỗ trợ

Mỗi cấu hình khai báo một mặt nạ năng lực phần cứng (Hardware Capabilities) để **SynaptiX IDE** thẩm định tự động ngay từ khâu thiết kế sơ đồ:

| Tên Cấu hình (Preset) | Logic Cơ sở & Retain | Đồng hồ RTC | Thẻ nhớ SD Datalog | Modbus RTU Master | Modbus TCP | Ethernet / Wi-Fi | MQTT Pub/Sub | HTTP Client | Luồng TCP Trong suốt |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **SynaptiX Remote I/O** | Có | — | — | Có | — | — | — | — | — |
| **SynaptiX Compact I/O** | Có | — | — | Có | — | — | — | — | — |
| **SynaptiX Relay Unit** | Có | — | — | Có | — | — | — | — | — |
| **SynaptiX Data Logger** | Có | Có | Có | — | — | — | — | — | — |
| **SynaptiX Gateway** | Có | — | — | Có | Có | Ethernet | Có | Có | Có |
| **SynaptiX Controller** | Có | Có | Có | Có | — | Ethernet + Wi-Fi | Có | Có | Có |
| **SynaptiX DTU** | Nhân DTU | — | — | Có | Có | Ethernet + Wi-Fi | Có | Có | Có |

::: info Hướng dẫn Chọn Nhanh giữa SynaptiX Gateway, SynaptiX Controller và SynaptiX DTU
- Chọn **`SynaptiX Controller`** khi bài toán cần đầy đủ kênh I/O vật lý tại chỗ (`8 DI / 8 DO / 4 AI`), lập lịch theo thời gian thực RTC, ghi thẻ nhớ SD, đọc cảm biến RS-485 Master đồng thời đẩy dữ liệu đám mây qua Ethernet/Wi-Fi.
- Chọn **`SynaptiX Gateway`** khi trọng tâm là cầu nối chuyển đổi giao thức giữa bus RS-485 hiện trường và mạng Ethernet (`Modbus TCP`, `MQTT`, `HTTP`) kết hợp một số tiếp điểm cảnh báo cục bộ (`2 DI / 2 DO`).
- Chọn **`SynaptiX DTU`** khi triển khai thiết bị truyền nhận dữ liệu RS-485 lên đám mây thuần túy (`0` kênh I/O cục bộ). Cấu hình này sẽ tự động kích hoạt không gian làm việc chuyên biệt [DTU Gateway Studio](/vi/tools/dtu-gateway-studio) thay cho mặt vẽ kéo-thả dây nối.
:::

---

## Các Chế độ Khởi tạo Cấu hình trong Hộp thoại Dự án Mới

Khi khởi tạo dự án qua menu **File -> New Project** (`Ctrl+N`), **SynaptiX IDE** mở hộp thoại **Khởi tạo Dự án Mới** kèm thẻ **Xem trước Ma trận Phần cứng** cập nhật tức thời ở cột bên phải.

![Hộp thoại Khởi tạo Dự án Mới & Ma trận Tài nguyên Thiết bị trên SynaptiX IDE](/images/synaptix_new_project.png)

Kỹ sư có thể thiết lập hồ sơ tài nguyên mục tiêu theo 3 phương thức linh hoạt:

### 1. Chọn Mẫu Cấu hình Thương mại Định sẵn (Standard Preset)
Chọn trực tiếp một trong **7 cấu hình định sẵn** từ danh sách thả xuống. Khung xem trước bên phải sẽ lập tức tính toán và hiển thị dải địa chỉ biến tương ứng (ví dụ `8 (DI0 - DI7)`, `32 (RETAIN0 - RETAIN31)`) cùng tổng dung lượng sử dụng (`124 / 128 tags`).

### 2. Tự động Nhận diện từ Thiết bị Đang kết nối (Detect from Connected Device)
Nếu một **Thiết bị Biên SynaptiX** thực tế đang kết nối với máy tính qua cổng **USB CDC** hoặc **Modbus TCP** (`F4`), kỹ sư chỉ cần nhấn nút **Detect from Connected Device**:
- **SynaptiX IDE** truy vấn trực tiếp định danh thiết bị (Descriptor) và bảng phân bổ tài nguyên thực tế từ bộ điều khiển đang trực tuyến.
- Phiên bản phần cứng (`HW`), phiên bản firmware (`FW`), số kênh I/O, số thanh ghi lưu trữ và trần số luật được đồng bộ chính xác 100% vào dự án.
- Thiết bị vật lý đóng vai trò **Nguồn Chân lý Duy nhất (Single Source of Truth)**, loại bỏ hoàn toàn nguy cơ chọn nhầm cấu hình khi làm việc tại tủ điện hiện trường.

::: warning Yêu cầu Phiên Kết nối Đang Hoạt động
Nếu chưa mở phiên kết nối thiết bị mà nhấn **Detect from Connected Device**, hộp thoại sẽ hiển thị thông báo nhắc kỹ sư kết nối thiết bị qua thanh công cụ phía trên (`F4`) trước, hoặc tiếp tục thiết kế ngoại tuyến bằng một trong 7 mẫu định sẵn.
:::

### 3. Chế độ Tùy chỉnh Tài nguyên (Custom Resource Mode)
Đối với các biến thể phần cứng chuyên biệt hoặc dự án OEM cần tinh chỉnh dung lượng từng phân vùng bộ nhớ trong phạm vi kiến trúc Wire Profile V2, chế độ tùy chỉnh cho phép định cấu hình từng nhóm tài nguyên trong giới hạn trần:

| Nhóm Tài nguyên | Tiền tố Tag | Dải Tùy chỉnh | Trần Kiến trúc Tối đa |
| :--- | :--- | :---: | :---: |
| **Ngõ vào Số (Digital Inputs)** | `DI` | `0 .. 8` | `8` |
| **Ngõ ra Số (Digital Outputs)** | `DO` | `0 .. 8` | `8` |
| **Ngõ vào Tương tự (Analog Inputs)** | `AI` | `0 .. 4` | `4` |
| **Cờ Ảo Nội bộ (Virtual Flags)** | `VFLAG` | `0 .. 32` | `32` |
| **Thanh ghi Ảo (Virtual Registers)** | `VREG` | `0 .. 32` | `32` |
| **Thanh ghi Lưu trữ (Retentive Registers)** | `VREG_RETAIN` | `0 .. 32` | `32` |
| **Bộ đếm Dải rộng (Counters)** | `COUNTER` | `0 .. 8` | `8` |
| **Luật Logic Biên dịch (Max Rules)** | `Rule #` | `1 .. 100` | `100` |

::: danger Ràng buộc Trần Kiến trúc Cứng
Tổng số lượng Tag khai báo của tất cả 7 phân vùng cộng lại không bao giờ được vượt quá **128 Tag**, và số lượng luật tối đa không vượt quá **100 luật**. Cả bộ dựng cấu hình miền (Domain Builder) lẫn cổng kiểm tra trước khi nạp đều từ chối mọi hồ sơ vượt ngưỡng này.
:::

---

## Tài liệu Liên quan

- [Kiến trúc Bộ nhớ Tag & Kiểu Dữ liệu](/vi/concepts/data-model-and-types) — Giải phẫu chi tiết 7 phân vùng Tag, hợp đồng bộ nhớ vật lý `BOOL`/`INT32`/`REAL` và cơ chế Chống Nhập sai Dữ liệu.
- [Chu trình Thực thi Tất định](/vi/concepts/execution-pipeline) — Cơ chế phối hợp giữa chuỗi luật 4 giai đoạn, khối hàm có trạng thái và tiến trình truyền thông bất đồng bộ.
- [Dự án Đầu tiên trong 10 Phút](/vi/getting-started/quickstart-first-project) — Thực hành từng bước với cấu hình `SynaptiX Controller`.
