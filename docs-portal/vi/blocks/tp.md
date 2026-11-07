# TP — Phát Xung Đơn ổn (Pulse)

<div class="doc-meta-bar">
  <span><strong>Mã khối:</strong> TP</span>
  <span><strong>Nhóm danh mục:</strong> Định thời, Bộ đếm &amp; Chuẩn hóa</span>
  <span><strong>Lớp thực thi:</strong> Bộ Phát Xung Đơn ổn Không Tái Kích (IEC 61131-3 TP)</span>
  <span><strong>Đích biên dịch:</strong> Macro Định thời / Máy Trạng thái Độ Rộng Xung Cố định</span>
</div>

## 1. Tóm tắt Chức năng & Ngữ nghĩa Thực thi

Khối **`TP`** (**Timer Pulse — Phát Xung Đơn ổn**) là bộ tạo xung vuông có độ rộng cố định tuyệt đối, **không tái kích hoạt trong khi xung đang phát**. Ngay khi phát hiện sườn lên tại chân `In` (`0 → 1`), khối `TP` lập tức bật ngõ ra `Q` lên mức `1` trong khoảng thời gian **chính xác bằng `PresetMs`**, sau đó hạ `Q` về `0`.

- **Cam kết Không Tái Kích (Non-Retriggerable):** Một khi chu trình phát xung đã bắt đầu (`0 < ET < PresetMs`), ngõ ra `Q` được khóa cứng ở mức `1` bất chấp mọi biến động tại chân `In`:
  - Nếu tín hiệu `In` rớt về `0` chỉ sau `10 ms`, ngõ ra `Q` vẫn duy trì mức `1` cho đến hết trọn vẹn `PresetMs`.
  - Nếu tín hiệu `In` dội nhấp nhả `1 → 0 → 1` liên tục giữa chừng, bộ đếm `ET` **không bị xóa lại cũng không bị cộng dồn kéo dài** — bảo đảm độ rộng xung chuẩn xác từng mili-giây.
- **Cơ chế Khóa Chống Dính Khi `In` Treo Mức Cao:** Nếu tới thời điểm `ET` chạm `PresetMs` mà tín hiệu `In` vẫn đang kẹt ở mức `1`, ngõ ra `Q` vẫn dứt khoát cắt về `0` và chỉ cho phép phát xung mới sau khi `In` đã trở về `0` rồi xuất hiện sườn lên `0 → 1` tiếp theo.
- **Ứng dụng Công nghiệp Điển hình:** Mở van định lượng hóa chất theo thể tích cố định, kích xy-lanh khí nén đẩy loại phế phẩm, giũ bụi túi lọc bằng van xung khí nén (jet-pulse), phát tiếng còi báo xác nhận ngắn và tạo xung Reset độ rộng chuẩn cho biến tần ngoại vi.

---

## 2. Sơ đồ Chân & Hợp đồng Tín hiệu

| Chân (Pin) | Chiều | Loại Tín hiệu | Kiểu Dữ liệu | Mô tả Kỹ thuật |
| :--- | :--- | :--- | :--- | :--- |
| `In` | Ngõ vào (Input) | Sự kiện / Thực thi (`IsEvent = true`) | Kích hoạt Sườn lên (`BOOL`) | Khởi phát một xung đơn `PresetMs` tại sườn lên `0 → 1` khi đang nghỉ; bỏ qua mọi dao động của `In` trong suốt thời gian xung đang phát. |
| `Q` | Ngõ ra (Output) | Giá trị / Dữ liệu (`IsEvent = false`) | `BOOL` (`0` / `1`) | Ngõ ra xung độ rộng cố định gắn với `OutputTag`; nối dây tới [`ACTION`](/vi/blocks/action), [`MQTT_PUB`](/vi/blocks/mqtt-pub), [`HTTP_CLIENT`](/vi/blocks/http-client) hoặc [`TCP_CLIENT`](/vi/blocks/tcp-client). |

---

## 3. Tham số Inspector & Giới hạn Kiểm tra

| Tham số | Kiểu / Tùy chọn | Mặc định | Ràng buộc & Giới hạn | Hành vi Thời gian thực |
| :--- | :--- | :--- | :--- | :--- |
| `DurationValueText` | Số thực / Số nguyên không âm | `"3"` | Chấp nhận dấu chấm hoặc dấu phẩy (`"0.25"`, `"0,25"`). Kẹp `PresetMs` trong dải `[1 .. 4,294,967,295] ms`. | Nhân với đơn vị `TimeUnit` để xác lập độ rộng xung phần cứng `PresetMs`. |
| `TimeUnit` | `ms`, `s`, `min`, `h` | `"s"` (`×1,000`) | Hệ số quy đổi: `ms = 1`, `s = 1,000`, `min = 60,000`, `h = 3,600,000`. | Tự động định dạng gọn đơn vị khi mở dự án và hiển thị dòng xác nhận `= 3,000 ms (3s)`. |
| `PresetMs` | Số nguyên không dấu 32-bit (`uint`) | `3000` (`3,000 ms`) | `1 .. 4,294,967,295 ms` (~`49.7 ngày`). | Độ rộng thời gian cố định mà `Q` duy trì mức `1` cho mỗi lần kích hoạt hợp lệ. |
| `InputTag` | Tag Số (`DI`, `DO`, `VFLAG`) | Tự động nhận từ dây nối vào | Phải tham chiếu tới Tag số nhị phân. | Được kiểm tra sườn lên (`0 → 1`) và trạng thái nhả về `0` ở mỗi chu kỳ quét. |
| `OutputTag` (`Q`) | Tag Số Cho phép Ghi (`DO`, `VFLAG`) | Chưa gán *(bắt buộc chọn)* | Phải là Tag nhị phân cho phép ghi (`DO` hoặc `VFLAG`). | Bật lên `1` đúng khoảng `PresetMs` rồi hạ dứt khoát về `0`. |
| `CustomLabel` | Chuỗi văn bản tự do | Rỗng (`"TP Timer"`) | Nhãn tùy biến trên sơ đồ. | Ghi đè tiêu đề hiển thị trên thẻ khối đồ họa. |

---

## 4. Quy tắc Phát Xung Đơn ổn & Cơ chế Bảo vệ

Bảng dưới đây làm rõ tính miễn nhiễm hoàn toàn với nhiễu dội giữa xung (khối `TP` phớt lờ mọi biến động của `In` trong lúc đang phát xung) và trạng thái khóa bảo vệ an toàn khi cảm biến bị kẹt ở mức `1` quá thời gian `PresetMs`:

| Tình huống Hiện trường | Diễn biến Chân `In` | Độ rộng Ngõ ra `Q` | Cơ chế Bảo vệ Công nghiệp |
| :--- | :--- | :--- | :--- |
| **Xung Kích Rất Ngắn (`20 ms`)** | `0 → 1 → 0` chớp nhoáng | Đúng bằng `PresetMs` | Kéo dài xung cảm biến tốc độ cao đủ thời gian hút cuộn van cơ khí |
| **Tín hiệu Dội Nhiễu Giữa Chừng** | `0 → 1 → 0 → 1` liên hồi | Đúng bằng `PresetMs` | Chống bơm đúp liều lượng hoặc làm giãn sai độ rộng xung |
| **Cảm biến Bị Kẹt Dính (`In = 1` mãi)** | `0 → 1` (không hạ về `0`) | Đúng `PresetMs` rồi cắt về `0` | Chống tràn hóa chất khi chai lọ dừng kẹt ngay trước mắt thần quang |

---

## 5. Cơ chế Bảo vệ Nhập liệu & Chẩn đoán Trực tuyến

### 5.1 Độ Chính xác Mili-giây & Bảo vệ Dữ liệu Xấu

- **Cấu hình Mili-giây & Phần lẻ Giây Tiện lợi:** Các ứng dụng van giũ bụi hoặc xy-lanh bắn phế phẩm thường cần độ rộng xung dưới 1 giây (`150 ms` hoặc `0.25 s`). Kỹ sư có thể chọn đơn vị `ms` nhập `"150"` (hoặc chọn `s` nhập `"0,25"`), hệ thống khóa chính xác `PresetMs` kèm dòng xác nhận `= 250 ms (0.25s)`.
- **Chặn Biên 0 & Dữ liệu Rác:** Nhập `"0"` tự động kẹp về `1 ms`; số âm hoặc chuỗi ký tự sai bị loại bỏ hoàn toàn, không làm hỏng thông số đang lưu.

### 5.2 Nhãn Chẩn đoán trong Mô phỏng (`F5`) & Gỡ lỗi Trực tuyến (`F8`)

- **Theo dõi Tiến trình Phát Xung (`F5`):** Hiển thị `Q = ON` kèm bộ đếm `ElapsedMs / PresetMs` trong suốt chu kỳ xung và tự tắt về `OFF` ngay khi đủ thời gian dù nút mô phỏng `INPUT` vẫn đang giữ ở `● 1`.
- **Giám sát Xung Thực tế 20 FPS (`F8`):** Bám sát trạng thái `OutputTag` tốc độ 20 FPS trên sơ đồ kèm kiểm soát chất lượng `[STALE]`.

---

## 6. Kịch bản Đấu nối Công nghiệp Thực tế

### Bài toán: Van Bơm Phụ gia Dây chuyền Chiết rót Chai (`750 ms` Chuẩn Thể tích `15 mL`)

Khi chai đi ngang cảm biến quang phản xạ gương (`DI0`) trên băng tải chiết rót, van điện từ tốc độ cao (`DO2`) phải mở **đúng `750 ms`** để bơm chuẩn `15 mL` hương liệu — với điều kiện biến tần băng tải đang chạy (`DI1 = 1`). Ngay cả khi băng tải dừng đột ngột làm một chai nằm chắn ngang mắt quang `DI0`, van `DO2` vẫn bắt buộc phải đóng kín sau đúng `750 ms` để không gây tràn nguyên liệu.

### Khai báo Bảng Tag

| Tên Tag | Phân loại | Kiểu Dữ liệu | Mô tả / Tên Bí danh (Alias) |
| :--- | :--- | :--- | :--- |
| `DI0` | `DI` | `BOOL` | `Cảm biến Quang Phát hiện Chai (0 -> 1 tại cạnh đầu chai)` |
| `DI1` | `DI` | `BOOL` | `Tín hiệu Khóa liên động Băng tải Đang Chạy` |
| `DO2` | `DO` | `BOOL` | `Van Điện từ Định lượng Phụ gia` |
| `VREG_R1` | `VREG_R` | `INT32` | `Bộ đếm Lũy kế Số chai Đã Chiết` |

### Trình tự Đấu nối trên Canvas

1. Đặt khối **`INPUT`** (`Tag = DI0`) → **[`TRIGGER`](/vi/blocks/trigger)** (`TriggerType = ON_RISE`, `ForMs = 15` ms lọc nhiễu lóa vỏ chai) → **[`GUARD`](/vi/blocks/guard)** (`GuardTag = DI1`, `Negate = false`).
2. Nối chân `GUARD.Out` → khối **`TP`** (`DurationValueText = "750"`, `TimeUnit = "ms"`, `OutputTag = DO2`, `CustomLabel = "Xung Chiết 750ms"`).
3. Nối tiếp `TP.Q` → **[`ACTION`](/vi/blocks/action)** (`ActionType = INC_COUNTER`, `TargetTag = VREG_R1`, `ActionParam = 1`) để cộng sản lượng mỗi lần bơm thành công.
4. Nhấn `F5` kiểm thử: bật `DI1 = 1`, gạt `DI0` lên `1` và **giữ nguyên `DI0 = 1`** (giả lập kẹt chai trước cảm biến). Xác nhận van `DO2` chỉ mở đúng `750 ms` rồi ngắt hẳn về `0`, không bao giờ phun lặp cho tới khi `DI0` hạ về `0` và đón chai mới.

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
        <span class="synx-node-alias">Cảm Biến Quang</span>
        <span class="synx-node-val">Phát Hiện Chai</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">EDGE</span>
  </div>

  <div class="synx-canvas-node is-trigger">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M10,1 L5,8.5 L8.5,8.5 L6,15 L11,7.5 L7.5,7.5 Z" /></svg>
        <span>TRIGGER</span>
      </div>
      <span class="synx-node-status is-rise">● ON_RISE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">15 ms Dwell</span>
        <span class="synx-node-alias">Lọc Chống Nhiễu</span>
        <span class="synx-node-val">Bắt Sườn Chai Vào</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">TRUE</span>
  </div>

  <div class="synx-canvas-node is-guard">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 L13.5,4 L13.5,9.5 Q13.5,13.5 8,15 Q2.5,13.5 2.5,9.5 L2.5,4 Z" /></svg>
        <span>GUARD</span>
      </div>
      <span class="synx-node-status is-pass">● PASS</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">DI1 == 1</span>
        <span class="synx-node-alias">Khóa Băng Tải</span>
        <span class="synx-node-val">Băng Tải Đang Chạy</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">ENABLE</span>
  </div>

  <div class="synx-canvas-node is-timer">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 A6.5,6.5 0 1 1 1.5,8 M8,1.5 L8,8 L11,10" /></svg>
        <span>TP TIMER</span>
      </div>
      <span class="synx-node-status is-active">● 750 ms</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">PT = 750 ms • DO2</span>
        <span class="synx-node-alias">Xung Định Lượng</span>
        <span class="synx-node-val">Phun Phụ Gia 750ms</span>
      </div>
      <div class="synx-node-pin is-out">Q ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">DONE</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● +1</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">INC VREG_R1</span>
        <span class="synx-node-alias">Đếm Sản Lượng</span>
        <span class="synx-node-val">Cộng 1 Chai Hoàn Tất</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>
