# TON — Hẹn giờ Trễ Đóng (On-Delay)

<div class="doc-meta-bar">
  <span><strong>Mã khối:</strong> TON</span>
  <span><strong>Nhóm danh mục:</strong> Định thời, Bộ đếm &amp; Chuẩn hóa</span>
  <span><strong>Lớp thực thi:</strong> Bộ Hẹn giờ Lưu Trạng thái Đơn điệu (IEC 61131-3 TON)</span>
  <span><strong>Đích biên dịch:</strong> Macro Định thời / Máy Trạng thái Trễ Đóng</span>
</div>

## 1. Tóm tắt Chức năng & Ngữ nghĩa Thực thi

Khối **`TON`** (**Timer On-Delay — Hẹn giờ Trễ Đóng**) trì hoãn việc đóng ngõ ra nhị phân `Q` lên mức `1` cho đến khi tín hiệu điều khiển ở chân `In` duy trì liên tục ở mức tích cực (`1`) đủ khoảng thời gian đặt trước `PresetMs`.

- **Hợp đồng Duy trì Liên tục:** Ngay tại sườn lên của `In` (`0 → 1`), bộ đếm thời gian trôi qua `ET` bắt đầu tăng dựa trên đồng hồ mili-giây đơn điệu của bộ điều khiển trong khi `Q` vẫn giữ mức `0`. Chỉ khi `ET` chạm mốc `PresetMs`, ngõ ra `Q` mới chuyển sang `1` (`Q = ON`).
- **Xóa Tức thì Khi Ngõ vào Rớt Áp:** Nếu tín hiệu `In` tụt về `0` ở **bất kỳ thời điểm nào** — dù đang đếm dở (`0 < ET < PresetMs`) hay sau khi `Q` đã đóng lên `1` — cả `ET` lẫn `Q` đều lập tức trở về `0` ngay trong chu kỳ quét đó. Mọi gai nhiễu hoặc dao động chập chờn ngắn hơn `PresetMs` đều bị chặn đứng hoàn toàn.
- **Ứng dụng Công nghiệp Điển hình:** Lọc sóng dao động phao báo mức nước, khởi động tuần tự lệch pha nhiều động cơ công suất lớn tránh sụt áp lưới, xác nhận kẹt phôi trên băng tải (cảm biến quang bị che liên tục quá `5 s`) và chờ ổn định áp suất kẹp khí nén.

---

## 2. Sơ đồ Chân & Hợp đồng Tín hiệu

| Chân (Pin) | Chiều | Loại Tín hiệu | Kiểu Dữ liệu | Mô tả Kỹ thuật |
| :--- | :--- | :--- | :--- | :--- |
| `In` | Ngõ vào (Input) | Sự kiện / Thực thi (`IsEvent = true`) | Tín hiệu Cho phép (`BOOL`) | Bắt đầu đếm thời gian khi ở mức `1` và xóa tức thì `ET` cùng `Q` về `0` ngay khi rớt xuống `0`. Nhận dây từ [`INPUT`](/vi/blocks/input), [`TRIGGER`](/vi/blocks/trigger) hoặc [`GUARD`](/vi/blocks/guard). |
| `Q` | Ngõ ra (Output) | Giá trị / Dữ liệu (`IsEvent = false`) | `BOOL` (`0` / `1`) | Ngõ ra nhị phân trễ đóng gắn với `OutputTag`; kéo dây sang khối [`ACTION`](/vi/blocks/action), [`MQTT_PUB`](/vi/blocks/mqtt-pub), [`HTTP_CLIENT`](/vi/blocks/http-client) hoặc [`TCP_CLIENT`](/vi/blocks/tcp-client). |

---

## 3. Tham số Inspector & Giới hạn Kiểm tra

| Tham số | Kiểu / Tùy chọn | Mặc định | Ràng buộc & Giới hạn | Hành vi Thời gian thực |
| :--- | :--- | :--- | :--- | :--- |
| `DurationValueText` | Số thực / Số nguyên không âm | `"3"` | Chấp nhận dấu chấm hoặc dấu phẩy (`"2.5"`, `"2,5"`). Giá trị `PresetMs` quy đổi được kẹp trong dải `[1 .. 4,294,967,295] ms`. | Nhân với hệ số đơn vị `TimeUnit` để tính ra số mili-giây phần cứng chính xác (`PresetMs`). |
| `TimeUnit` | `ms`, `s`, `min`, `h` | `"s"` (`×1,000`) | Hệ số quy đổi: `ms = 1`, `s = 1,000`, `min = 60,000`, `h = 3,600,000`. | Tự động chọn đơn vị gọn nhất khi mở dự án (`3,600,000 ms` → `1 h`, `60,000 ms` → `1 min`, `3,000 ms` → `3 s`) và hiển thị dòng đối chiếu `= 3,000 ms (3s)`. |
| `PresetMs` | Số nguyên không dấu 32-bit (`uint`) | `3000` (`3,000 ms`) | Tối thiểu `1 ms`, tối đa `4,294,967,295 ms` (~`49.7 ngày`). | Ngưỡng thời gian đích tính bằng mili-giây cần duy trì liên tục để bật `Q = 1`. |
| `InputTag` | Tag Số (`DI`, `DO`, `VFLAG`) | Tự động nhận từ dây nối vào | Phải là Tag kiểu số nhị phân. | Được giám sát ở mọi chu kỳ quét để điều khiển máy trạng thái `TON`. |
| `OutputTag` (`Q`) | Tag Số Cho phép Ghi (`DO`, `VFLAG`) | Chưa gán *(bắt buộc chọn)* | Phải là Tag nhị phân cho phép ghi (`DO` hoặc `VFLAG`). | Nhận trực tiếp trạng thái ngõ ra `Q` (`1` khi `ET >= PresetMs`, `0` khi `In == 0`). |
| `CustomLabel` | Chuỗi văn bản tự do | Rỗng (`"TON Timer"`) | Nhãn tùy biến trên sơ đồ. | Ghi đè tiêu đề hiển thị trên thẻ khối đồ họa. |

---

## 4. Quy tắc Định thời & Giản đồ Pha Tín hiệu

Bảng dưới đây minh họa rõ sự khác biệt giữa **xung chập chờn ngắn** (tụt về `0` trước `PresetMs` nên `Q` giữ nguyên `0`) và **tín hiệu duy trì hợp lệ** (đạt `ET >= PresetMs` để đóng `Q = 1` cho tới khi `In` ngắt):

| Giai đoạn Tín hiệu | Ngõ vào `In` | Thời gian Đếm `ET` | Ngõ ra `Q` | Nhãn Trạng thái trên Canvas |
| :--- | :--- | :--- | :--- | :--- |
| **1. Trạng thái Nghỉ** | `0` | `0 ms` | `0` | `3s (OFF)` / `PT: 3000 ms` |
| **2. Xung Nhiễu Ngắn (`< PresetMs`)** | `1 → 0` sớm | Tăng dở rồi xóa ngay về `0 ms` | `0` (Không đổi) | Lập tức quay về `3s (OFF)` |
| **3. Đang Đếm Trễ (`< PresetMs`)** | `1` | `0 → PresetMs` | `0` | `45% (3s)` · `1350 / 3000 ms` |
| **4. Đủ Thời gian Đặt** | `1` | `PresetMs` (`3000 ms`) | `1` (`ON`) | `Q = ON` · `DONE · 3000 ms` |
| **5. Ngắt Tín hiệu Vào** | `0` | `0 ms` | `0` (`OFF`) | `3s (OFF)` |

---

## 5. Cơ chế Bảo vệ Nhập liệu & Chẩn đoán Trực tuyến

### 5.1 Quy đổi Đa Đơn vị & Chống Nhập sai Dữ liệu

- **Hỗ trợ Số Thập phân Dấu chấm / Phẩy:** Nhập `"1,5"` kèm đơn vị `"min"` sẽ tự động chuyển đổi dấu phẩy và tính chính xác `PresetMs = 90,000 ms`, hiển thị dòng xác nhận `= 90,000 ms (90s)` ngay bên dưới giúp kỹ sư kiểm tra chéo độ phân giải mili-giây mà không cần tính nhẩm.
- **Kẹp Biên Dưới & Bảo vệ Bộ nhớ:** Nhập `"0"` tự động kẹp an toàn về `1 ms`; các chuỗi âm (`"-5"`) hoặc ký tự chữ (`"abc"`) bị loại bỏ hoàn toàn mà không làm sai lệch giá trị `PresetMs` đang lưu.

### 5.2 Nhãn Chẩn đoán trong Mô phỏng (`F5`) & Gỡ lỗi Trực tuyến (`F8`)

- **Thanh Tiến trình Đếm Thực (`F5`):** Khi chạy Simulator, khối `TON` hiển thị trực quan phần trăm hoàn thành (`66% (3s)`) cùng số mili-giây tức thời (`2000 / 3000 ms`), và chuyển sang nhãn xanh lá `Q = ON` ngay khi đạt ngưỡng.
- **Giám sát Trực tuyến 20 FPS (`F8`):** Liên kết trực tiếp với `OutputTag`, phản ánh trung thực trạng thái đóng/cắt của `Q` trên phần cứng và tự động chuyển sang cảnh báo `Stale` nếu mất làm tươi dữ liệu.

---

## 6. Kịch bản Đấu nối Công nghiệp Thực tế

### Bài toán: Chống Dao động Mặt nước Phao Hố Thu Nước thải Cho Bơm Chìm `7.5 kW`

Trong hố thu nước thải ngầm, dòng nước chảy mạnh làm phao báo mức cao (`DI0`) chao đảo liên tục trước khi mực nước thực sự dâng đầy. Nếu đóng khởi động từ bơm chìm `7.5 kW` (`DO0`) ngay theo từng cơn sóng vỗ sẽ gây dính tiếp điểm contactor và quá nhiệt cuộn dây động cơ. Sử dụng khối **`TON`** đặt trễ **`5 giây`** buộc phao phải ngập liên tục đủ `5 s` mới đóng bơm.

### Khai báo Bảng Tag

| Tên Tag | Phân loại | Kiểu Dữ liệu | Mô tả / Tên Bí danh (Alias) |
| :--- | :--- | :--- | :--- |
| `DI0` | `DI` | `BOOL` | `Phao Báo Mức Cao Hố Thu Nước thải` |
| `DO0` | `DO` | `BOOL` | `Contactor Bơm Chìm Hố Thu (7.5 kW)` |

### Trình tự Đấu nối trên Canvas

1. Đặt khối **`INPUT`** (`Tag = DI0`, `CustomLabel = "Phao Mức Cao"`).
2. Chọn khối `INPUT` rồi nhấp thêm khối **`TON`** trên thanh công cụ để tự động nối dây `INPUT.Out → TON.In`.
3. Trên bảng **Inspector** của `TON`, nhập `DurationValueText = "5"`, chọn `TimeUnit = "s"` (kiểm tra dòng quy đổi `= 5,000 ms (5s)`) và gán `OutputTag = DO0`.
4. Kéo thêm dây từ `TON.Q` sang khối **[`MQTT_PUB`](/vi/blocks/mqtt-pub)** (`Topic = "nhamay/hothu/bom_chay"`) để gửi bản tin báo trạng thái bơm khởi động lên phòng điều hành trung tâm.
5. Nhấn `F5` kiểm thử: bật `DI0` lên `1` trong `2 giây` rồi tắt về `0` — xác nhận `ET` xóa ngay về `0 ms` và `DO0` không đóng. Tiếp theo bật giữ `DI0 = 1` đủ `5 giây` — xác nhận nhãn `Q = ON` bật sáng và bơm `DO0` vận hành ổn định.

<div class="synx-node-chain">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>INPUT</span>
      </div>
      <span class="synx-node-status is-live">● LIVE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">DI0 • BOOL</span>
        <span class="synx-node-alias">Phao Mức Cao</span>
        <span class="synx-node-val">Bồn Nước Đầy</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">TRUE</span>
  </div>

  <div class="synx-canvas-node is-timer">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 A6.5,6.5 0 1 1 1.5,8 M8,1.5 L8,8 L11,10" /></svg>
        <span>TON TIMER</span>
      </div>
      <span class="synx-node-status is-active">● 5000 ms</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">PT = 5 s • DO0</span>
        <span class="synx-node-alias">Lọc Trễ Chống Sóng</span>
        <span class="synx-node-val">Q = ON sau 5s</span>
      </div>
      <div class="synx-node-pin is-out">Q ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">PULSE</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,14 L8,8 M5,10 A4,4 0 0 1 11,10 M3,7.5 A7,7 0 0 1 13,7.5 M1.5,5 A9.5,9.5 0 0 1 14.5,5" /></svg>
        <span>MQTT_PUB</span>
      </div>
      <span class="synx-node-status is-active">● TELEMETRY</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● Trig</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Topic QoS 1</span>
        <span class="synx-node-alias">Báo Bơm Khởi Động</span>
        <span class="synx-node-val">Đẩy Lên Scada Cloud</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>
