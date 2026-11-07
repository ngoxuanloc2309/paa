# Tổng quan Danh mục Khối hàm & Quy ước Chân nối

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-BLK-000</span>
  <span><strong>Chương:</strong> 04. Từ điển Khối hàm (Blocks)</span>
  <span><strong>Không gian làm việc:</strong> Sơ đồ Logic — Logic Graph (Ctrl+2)</span>
  <span><strong>Tiêu chuẩn:</strong> IEC 61131-3 FBD &bull; ANSI/ISA-101</span>
</div>

Không gian **Sơ đồ Logic — Logic Graph Canvas** ([`Ctrl+2`](/vi/workspaces/logic-graph-tab)) trong **SynaptiX IDE** cung cấp môi trường thiết kế sơ đồ khối chức năng (FBD) tất định chuyên dụng cho điều khiển biên, thu thập dữ liệu truyền thông công nghiệp RS-485 và điện toán đám mây IIoT. Mỗi khối đồ họa tích hợp sẵn cơ chế kiểm tra chặt chẽ kiểu dữ liệu vật lý, mô phỏng chu kỳ chính xác trên máy tính (`F5`), biên dịch kiểm lỗi tiền triển khai (`F7`) và hiển thị trạng thái thời gian thực 20 FPS (`F8`) mà không cần viết mã kịch bản thủ công.

---

## 1. Bảng Danh mục Tổng thể các Khối hàm

Thư viện khối hàm được phân thành **4 nhóm chức năng** đồng nhất với thanh công cụ bên trái trên **SynaptiX IDE**. Nhấp vào mã khối bất kỳ để mở trang đặc tả kỹ thuật (datasheet) chi tiết của khối đó.

### 1.1 Chuỗi Luật Cốt lõi (Core Rule Pipeline)

Các khối thuộc Chuỗi Luật Cốt lõi được trình biên dịch hạ cấp trực tiếp thành các luật Sự kiện – Điều kiện – Hành động (ECA) thực thi đồng bộ ngay trong mỗi chu kỳ quét của **SynaptiX Runtime**.

| Mã khối | Tên khối hàm | Chân nối (`Vào` → `Ra`) | Vai trò Công nghiệp Trọng tâm |
| :--- | :--- | :--- | :--- |
| [`INPUT`](/vi/blocks/input) | **Khối Nguồn Tín hiệu Tag** | *(Không có)* → `Out` | Đưa giá trị Tag vật lý (`DI`, `DO`, `AI`) hoặc vùng nhớ ảo (`VFLAG`, `VREG`, `VREG_R`, `COUNTER`) vào mạng logic và hỗ trợ cưỡng bức/bơm giá trị thử nghiệm khi chạy Simulator. |
| [`TRIGGER`](/vi/blocks/trigger) | **Phát hiện Sườn & Lịch RTC** | `In` → `Out` | Phát hiện sườn lên (`0 → 1`), sườn xuống (`1 → 0`), biến thiên giá trị, lọc nhiễu chống dội (`ForMs`), chu kỳ lặp định kỳ và lịch thời gian thực (`RTC`). |
| [`COMPARATOR`](/vi/blocks/comparator) | **Bộ So sánh Quan hệ (`CMP`)** | `In` → `Out` | Đánh giá 7 toán tử quan hệ (`==`, `!=`, `>`, `<`, `>=`, `<=`, `BETWEEN`) khóa chặt theo kiểu dữ liệu `INT32`, `REAL` (dung sai `1e-5`) và `BOOL`. |
| [`GUARD`](/vi/blocks/guard) | **Cổng Khóa Liên động An toàn** | `In` → `Out` | Chặn hoặc cho phép xung kích hoạt đi qua dựa trên trạng thái Tag khóa liên động số (`Cho qua khi 1/ON` hoặc đảo logic `NOT khi 0/OFF`). |
| [`ACTION`](/vi/blocks/action) | **Khối Thực thi Lệnh Đích** | `In` → *(Không có)* | Kết thúc nhánh luật bằng thao tác ghi giá trị tất định (`SET_TAG`, `TOGGLE_TAG`, `INC_COUNTER`, `SCALE_TAG`), ghi từ xa, phát cảnh báo hoặc ghi nhật ký sự kiện. |

### 1.2 Định thời, Bộ đếm & Chuẩn hóa (Timing, Counting & Scaling)

Nhóm các khối lưu trạng thái phục vụ trễ đóng/ngắt, phát xung chuẩn xác, đếm sản phẩm và hiệu chuẩn tuyến tính hai điểm cho tín hiệu cảm biến.

| Mã khối | Tên khối hàm | Chân nối (`Vào` → `Ra`) | Vai trò Công nghiệp Trọng tâm |
| :--- | :--- | :--- | :--- |
| [`TON`](/vi/blocks/ton) | **Hẹn giờ Trễ Đóng (On-Delay)** | `In` → `Q` | Trì hoãn bật ngõ ra `Q` cho đến khi tín hiệu `In` duy trì liên tục đủ thời gian `PresetMs` (`ms`, `s`, `min`, `h`); triệt tiêu nhiễu chập chờn từ cảm biến. |
| [`TOF`](/vi/blocks/tof) | **Hẹn giờ Trễ Ngắt (Off-Delay)** | `In` → `Q` | Đóng `Q = 1` tức thì khi `In = 1` và duy trì `Q = 1` thêm khoảng `PresetMs` sau khi `In` ngắt `1 → 0` (ví dụ: quạt làm mát chạy trễ sau khi dừng trục chính). |
| [`TP`](/vi/blocks/tp) | **Phát Xung Đơn ổn (Pulse)** | `In` → `Q` | Tạo một xung vuông có độ rộng cố định đúng bằng `PresetMs` ngay tại sườn lên của `In`, bất chấp tín hiệu `In` dao động trong suốt thời gian phát xung. |
| [`CYCLE_TIMER`](/vi/blocks/cycle-timer) | **Tạo Xung Chu kỳ Bất đối xứng** | `In` → `Q` | Phát chuỗi xung vuông tuần hoàn với thời gian `OnTimeMs`, `OffTimeMs` độc lập và pha khởi đầu tùy chọn (`Bắt đầu bằng ON` / `OFF`) chừng nào `In = 1`. |
| [`CTU`](/vi/blocks/ctu) | **Bộ Đếm Tiến (Count Up)** | `CU`, `R` → `Q` | Tăng giá trị đếm tức thời `CV` tại mỗi sườn lên chân `CU` và xuất `Q = 1` khi `CV >= PV`; xóa `CV` về `0` khi kích chân `R`. |
| [`CTD`](/vi/blocks/ctd) | **Bộ Đếm Lùi (Count Down)** | `CD`, `R` → `Q` | Giảm `CV` lùi từ ngưỡng đặt `PV` tại mỗi sườn lên chân `CD` và xuất `Q = 1` khi `CV <= 0`; nạp lại `CV = PV` khi kích chân `R`. |
| [`SCALE`](/vi/blocks/scale) | **Tuyến tính hóa & Hiệu chuẩn 2 Điểm** | `In` → `Out` | Nội suy tuyến tính giá trị thô ADC hoặc thanh ghi truyền thông (`[RawMin..RawMax]`) sang đơn vị vật lý thực tế (`[EngMin..EngMax]`) kèm tùy chọn kẹp biên. |

### 1.3 Truyền thông Fieldbus & Xuất Phần cứng (Fieldbus & Hardware Actuation)

Các khối giao tiếp bất đồng bộ Modbus RTU Master trên bus RS-485 và điều khiển trực tiếp ngoại vi vi điều khiển tốc độ cao (`DO`, `PWM`, `PTO`).

| Mã khối | Tên khối hàm | Chân nối (`Vào` → `Ra`) | Vai trò Công nghiệp Trọng tâm |
| :--- | :--- | :--- | :--- |
| [`MB_READ`](/vi/blocks/mb-read) | **Đọc Thiết bị Modbus RTU** | `EN` → `Valid`, `Error`, `ErrCode`, `Pin1..N` | Thu thập dữ liệu định kỳ từ thiết bị đo RS-485 (`FC01`, `FC02`, `FC03`, `FC04`) và giải mã vùng nhớ (`INT16`, `UINT16`, `INT32`, `FLOAT32`) ra các chân dữ liệu định kiểu. |
| [`MB_WRITE`](/vi/blocks/mb-write) | **Ghi Chấp hành Modbus RTU** | `Trig`, `Pin1..N` (hoặc `Ch`/`Cmd`/`Param`) → `Done`, `Busy`, `Error`, `ErrCode` | Phát khung lệnh ghi đơn hoặc đa thanh ghi Modbus RTU (`FC05`, `FC06`, `FC15`, `FC16`) theo chế độ gắn chân tĩnh hoặc đánh chỉ số kênh động. |
| [`DIRECT_WRITE`](/vi/blocks/direct-write) | **Xuất Trực tiếp DO / PWM / PTO / Bus** | `Trig` *(tùy chọn)*, `Val` → `Done`, `Busy`, `Err` | Điều khiển trực tiếp ngoại vi phần cứng trên bo mạch gồm ngõ ra số (`DO`), điều chế độ rộng xung `PWM`, phát chuỗi xung `PTO` và ngõ ra bus nội bộ. |

### 1.4 Điện toán Biên IIoT & Mạng IP (IIoT Cloud & IP Network)

Nhóm khối truyền thông mạng IP không gây nghẽn (non-blocking) phục vụ đẩy dữ liệu MQTT TLS, nhận lệnh JSON từ Cloud, gọi REST Webhook và giao tiếp Raw TCP Socket.

| Mã khối | Tên khối hàm | Chân nối (`Vào` → `Ra`) | Vai trò Công nghiệp Trọng tâm |
| :--- | :--- | :--- | :--- |
| [`MQTT_PUB`](/vi/blocks/mqtt-pub) | **Đẩy Telemetry lên Broker** | `Trig`, `Val1..N` → `Done`, `Err` | Đóng gói giá trị Tag và biến Macro hệ thống thành bản tin JSON hoặc chuỗi tùy chỉnh, đẩy lên MQTT / TLS Broker với cấu hình `QoS` và `Retain`. |
| [`MQTT_SUB`](/vi/blocks/mqtt-sub) | **Nhận Lệnh Điều khiển JSON** | `EN` → `Valid`, `Err`, `Pin1..N` | Đăng ký lắng nghe Topic điều khiển từ Cloud, bóc tách khóa JSON ra các chân dữ liệu và phát xung `Valid` ngay khi nhận gói tin hợp lệ. |
| [`HTTP_CLIENT`](/vi/blocks/http-client) | **Giao tiếp REST Webhook** | `Trig`, `Val1..N` → `Busy`, `Done`, `Error`, `StatusCode` | Gửi truy vấn `GET`, `POST` hoặc `PUT` HTTP/HTTPS kèm Header tùy biến và biểu mẫu JSON tới máy chủ MES, ERP hoặc dịch vụ Webhook. |
| [`TCP_CLIENT`](/vi/blocks/tcp-client) | **Truyền nhận Raw Socket** | `Send`, `DataIn` → `Conn`, `Done`, `Error`, `DataOut` | Duy trì kết nối TCP Socket thô gửi/nhận chuỗi ASCII hoặc khung byte Hex với đầu đọc mã vạch, máy in công nghiệp, đầu cân điện tử. |

---

## 2. Ngữ nghĩa Chân nối & Quy tắc Ngữ pháp Đi dây

Mỗi chân kết nối (Connector Port) trên sơ đồ khối của **SynaptiX IDE** thuộc một trong hai miền tín hiệu tách biệt. Nắm vững sự khác biệt giữa **Chân Giá trị / Dữ liệu** và **Chân Sự kiện / Thực thi** giúp kỹ sư thiết kế mạch logic chuẩn xác ngay từ đầu và tránh lỗi cấu trúc khi biên dịch (`F7`).

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>INPUT</span>
      </div>
      <span class="synx-node-status is-live">● VALUE PIN</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Nguồn Dữ Liệu</span>
        <span class="synx-node-alias">Tag Vật Lý / Ảo</span>
        <span class="synx-node-val">DI, AI, VREG, Flag</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Analog Raw</span>
  </div>

  <div class="synx-canvas-node is-timer">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,13 L5,13 L5,9.5 L8,9.5 L8,6 L11,6 L11,3 L14,3" /></svg>
        <span>SCALE</span>
      </div>
      <span class="synx-node-status is-active">● CALIBRATED</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Quy Đổi Tuyến Tính</span>
        <span class="synx-node-alias">Đơn Vị Kỹ Thuật</span>
        <span class="synx-node-val">Sang Bar, °C, RPM</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">REAL Val</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● RELATIONAL</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">So Sánh Ngưỡng</span>
        <span class="synx-node-alias">Đánh Giá Quan Hệ</span>
        <span class="synx-node-val">Xuất Cờ BOOL Logic</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>
</div>

<div style="text-align: center; margin: 4px 0; color: var(--vp-c-brand-1); font-size: 0.8rem; font-weight: 600;">
  ↓ Cờ BOOL chuyển tiếp sang Miền Sự kiện (Kích hoạt Xung Thực thi)
</div>

<div class="synx-node-chain" style="margin-top: 12px;">
  <div class="synx-canvas-node is-trigger">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M10,1 L5,8.5 L8.5,8.5 L6,15 L11,7.5 L7.5,7.5 Z" /></svg>
        <span>TRIGGER</span>
      </div>
      <span class="synx-node-status is-rise">● EVENT PIN</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Phát Hiện Sườn</span>
        <span class="synx-node-alias">Bắt Xung Kích Hoạt</span>
        <span class="synx-node-val">Sườn Lên / Lọc Dội / RTC</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">PULSE</span>
  </div>

  <div class="synx-canvas-node is-guard">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 L13.5,4 L13.5,9.5 Q13.5,13.5 8,15 Q2.5,13.5 2.5,9.5 L2.5,4 Z" /></svg>
        <span>GUARD</span>
      </div>
      <span class="synx-node-status is-pass">● GATE PIN</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Cổng Khóa An Toàn</span>
        <span class="synx-node-alias">Liên Động Nối Tiếp</span>
        <span class="synx-node-val">Cho Qua Khi Đúng (AND)</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">PERMIT</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● COMMIT</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Chấp Hành Tất Định</span>
        <span class="synx-node-alias">Thực Thi Đích</span>
        <span class="synx-node-val">SET, RESET, TOGGLE</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

### 2.1 Phân biệt Chân Giá trị / Dữ liệu và Chân Sự kiện / Thực thi

| Miền Tín hiệu | Các Chân Đại diện | Ngữ nghĩa trong Chu kỳ Quét |
| :--- | :--- | :--- |
| **Chân Giá trị / Dữ liệu** (`IsEvent = false`) | `INPUT.Out`, `SCALE.In` / `Out`, `COMPARATOR.In` / `Out`, `TON.Q`, `TOF.Q`, `TP.Q`, `CYCLE_TIMER.Q`, `CTU.Q`, `CTD.Q`, các chân dữ liệu động trên `MB_READ` & `MQTT_SUB` | Mang giá trị trạng thái vật lý hoặc vùng nhớ liên tục (`BOOL`, `INT32` hoặc `REAL`) được cập nhật ở mọi chu kỳ quét. Liên kết Tag từ khối nguồn tự động lan truyền sang khối nhận qua dây nối. |
| **Chân Sự kiện / Thực thi** (`IsEvent = true`) | `TRIGGER.Out`, `GUARD.In` / `Out`, `ACTION.In`, `TON.In`, `TOF.In`, `TP.In`, `CYCLE_TIMER.In`, `CTU.CU` / `R`, `CTD.CD` / `R`, `Trig` / `Send` / `Done` / `Valid` / `Err` | Biểu diễn xung chuyển trạng thái rời rạc, cổng điều kiện cho phép hoặc xung báo hoàn tất giao dịch bất đồng bộ dùng để kích hoạt khối kế tiếp. |

### 2.2 Quy tắc Ngữ pháp Đi dây Cấu trúc

Trình biên dịch sơ đồ của **SynaptiX IDE** kiểm tra tính toàn vẹn cấu trúc mạng FBD theo thời gian thực ngay khi kéo dây trên Canvas và trong bước kiểm tra tiền triển khai (`F7`):

1. **Cho phép Rẽ nhánh Ngõ ra (`1:N`), Cấm Chập Ngõ vào (`1:1` trên mỗi chân nhận):** Một chân ngõ ra (như `INPUT.Out` hoặc `TRIGGER.Out`) có thể kéo dây đồng thời tới nhiều khối phía sau. Ngược lại, mỗi chân ngõ vào chỉ được phép nhận **tối đa một dây nối đến**.
2. **Đồ thị Có hướng Không Chu trình (DAG):** Một khối không thể tự nối dây vào chính nó và không cho phép tạo vòng lặp dây nối khép kín trực tiếp trên cùng một mạng tổ hợp. Để phản hồi trạng thái sang chu kỳ quét kế tiếp, hãy dùng khối [`ACTION`](/vi/blocks/action) ghi vào biến trung gian (`VFLAG` hoặc `VREG`) và đọc lại thông qua một khối [`INPUT`](/vi/blocks/input) độc lập.
3. **Ranh giới Khối Nguồn & Khối Đích:** Khối [`INPUT`](/vi/blocks/input) đóng vai trò nguồn phát thuần túy (không có chân vào), trong khi khối [`ACTION`](/vi/blocks/action) đóng vai trò điểm kết thúc nhánh luật (không có chân ra).
4. **Tự động Tổng hợp Trigger khi Đấu tắt:** Cho phép nối dây trực tiếp `INPUT → ACTION` hoặc `INPUT → GUARD`; trình biên dịch sẽ tự động tổng hợp điều kiện chuyển trạng thái phù hợp mà vẫn bảo toàn tính tất định.
5. **Quy tắc Đơn tầng đối với Trigger & Guard:** Không nối trực tiếp `TRIGGER → TRIGGER`, không nối chuỗi `GUARD → GUARD` liên tiếp và không nối ngược `GUARD → TRIGGER`. Khi cần kết hợp nhiều điều kiện khóa liên động đồng thời, hãy tổng hợp trạng thái trung gian qua cờ ảo `VFLAG`.
6. **Tự động Căn lưới Thông minh:** Khi đang chọn một khối trên Canvas và nhấp thêm khối tương thích trên thanh công cụ, phần mềm tự động đặt khối mới cách `240 px` về bên phải theo lưới chuẩn `20 px`, căn thẳng hàng ngang hai chân cắm và nối dây tự động.

---

## 3. Chú giải Nhãn Trạng thái (Status Pill) & Chất lượng Tín hiệu Trực tuyến

Trong chế độ **Mô phỏng Offline** (`F5`) và **Gỡ lỗi Trực tuyến — Live Debug** (`F8`), mỗi khối hiển thị nhãn trạng thái (Status Pill) tuân thủ nghiêm ngặt **Nguyên lý Xám ANSI/ISA-101** — giữ nền xám trung tính ở trạng thái nghỉ và chỉ bật màu xanh lá, cam hổ phách hoặc đỏ tươi khi phát sinh trạng thái vận hành thực tế hoặc lỗi phần cứng.

| Nhãn trên Canvas | Trạng thái Thị giác | Khối Áp dụng | Ý nghĩa Kỹ thuật & Hướng Xử lý |
| :--- | :--- | :--- | :--- |
| `READY` / `SẴN SÀNG` | Xám Trung tính (Chờ) | `MB_READ`, `MB_WRITE`, `MQTT_PUB`, `MQTT_SUB`, `HTTP_CLIENT` | Khối đã cấu hình hợp lệ, tiến trình giao tiếp nền đã sẵn sàng chờ tín hiệu cho phép hoặc xung kích hoạt. |
| `VALID` | Xung Xanh lá | `MB_READ`, `MQTT_SUB` | Khung phản hồi Modbus RTU (đúng mã kiểm tra CRC-16) hoặc gói tin JSON từ Broker vừa được giải mã thành công ra các chân dữ liệu. |
| `Q = ON` | Xanh lá Kích hoạt | `TON`, `TOF`, `TP`, `CTU`, `CTD` | Bộ hẹn giờ đã đếm đủ thời gian đặt hoặc Bộ đếm đạt ngưỡng `PV`, đóng ngõ ra `Q` lên mức logic `1` (riêng `CYCLE_TIMER` hiển thị `● ON` / `● OFF`). |
| `DONE #N` / `HOÀN TẤT #N` | Xanh lá / Xanh lam | `HTTP_CLIENT`, `MB_WRITE`, `DIRECT_WRITE`, `MQTT_PUB` | Bộ đếm lũy kế xác nhận `N` giao dịch đã thực thi thành công kể từ lúc khởi chạy (ví dụ: `HOÀN TẤT #12` hoặc `HTTP 200 (#12)`). |
| `TX...` / `ĐANG GỬI...` | Cam Hổ phách (Bận) | `HTTP_CLIENT`, `MB_WRITE`, `MQTT_PUB` | Tiến trình truyền thông bất đồng bộ đang đẩy khung tin qua cổng UART/RS-485 hoặc mạng TCP/TLS trong khi chu kỳ quét điều khiển chính vẫn chạy bình thường. |
| `ERR` / `LỖI: ...` | Đỏ Báo lỗi (`Danger`) | Khối Fieldbus, Xuất Trực tiếp & IIoT | Phát hiện sự cố giao tiếp hoặc phần cứng (quá thời gian chờ Slave, sai CRC, lỗi mã HTTP, mất kết nối Broker hoặc vượt dải kênh). Kiểm tra chân `ErrCode` hoặc cửa sổ Live Watch. |
| `[STALE]` | Hậu tố Cảnh báo Cam | Mọi Khối Gắn Tag (`INPUT`, `GUARD`, `ACTION`, `CMP`) | Đính kèm sau giá trị trực tuyến (ví dụ: `1 [STALE]` hoặc `450 [STALE]`) khi chất lượng dữ liệu suy giảm sang `Stale` do đứt cáp truyền thông hoặc quá hạn làm tươi dữ liệu. |

::: tip Lưu giữ Giá trị Cuối khi Ngắt Kết nối
Khi ngắt phiên **Gỡ lỗi Trực tuyến** (`F8`), các khối gắn Tag vẫn hiển thị giá trị mẫu thu thập cuối cùng kèm chú thích rõ ràng (`[Last Known: ...]` / giá trị biết gần nhất) giúp kỹ sư đối chiếu hiện trường mà không nhầm lẫn giữa dữ liệu lưu đệm với tín hiệu đang sống.
:::
