# INPUT — Khối Nguồn Tín hiệu Tag

<div class="doc-meta-bar">
  <span><strong>Mã khối:</strong> INPUT</span>
  <span><strong>Nhóm danh mục:</strong> Chuỗi Luật Cốt lõi (Core Rule Pipeline)</span>
  <span><strong>Lớp thực thi:</strong> Nguồn Quét Đồng bộ (Độ trễ 0 chu kỳ)</span>
  <span><strong>Đích biên dịch:</strong> Liên kết Vùng nhớ Ảnh Tiến trình (Process Image)</span>
</div>

## 1. Tóm tắt Chức năng & Ngữ nghĩa Thực thi

Khối **`INPUT`** là điểm khởi đầu cung cấp tín hiệu cho mọi nhánh điều khiển trên **Sơ đồ Logic (Logic Graph Canvas)** của **SynaptiX IDE**. Khối liên kết trực tiếp tới một kênh vào/ra vật lý hoặc một biến nhớ ảo trong [Danh mục Biến — Tag Catalog](/vi/workspaces/tag-catalog-tab) và phát giá trị tức thời của Tag đó ra chân `Out` ở mỗi chu kỳ quét.

- **Đọc Ảnh Tiến trình Tất định (Process Image):** Ngay đầu mỗi chu kỳ quét của **SynaptiX Runtime**, toàn bộ trạng thái ngõ vào phần cứng được chốt vào vùng đệm ảnh tiến trình bất biến. Mọi khối `INPUT` cùng tham chiếu đến một Tag sẽ nhận cùng một giá trị đồng nhất tuyệt đối trong suốt chu kỳ quét đó, loại bỏ hoàn toàn hiện tượng tranh chấp dữ liệu (race condition).
- **Biên dịch Không Tốn Chu kỳ Phụ:** Bản thân khối `INPUT` không tiêu tốn slot lệnh trung gian; khi biên dịch (`F7`), chỉ số Tag và hợp đồng kiểu dữ liệu vật lý (`BOOL`, `INT32` hoặc số thực IEEE-754 `REAL`) được ánh xạ thẳng vào các khối phía sau như [`TRIGGER`](/vi/blocks/trigger), [`COMPARATOR`](/vi/blocks/comparator), [`SCALE`](/vi/blocks/scale), [`TON`](/vi/blocks/ton), [`CTU`](/vi/blocks/ctu) hoặc [`MQTT_PUB`](/vi/blocks/mqtt-pub).
- **Ứng dụng Công nghiệp Điển hình:** Đọc công tắc hành trình và nút dừng khẩn cấp (`DI`), giám sát phản hồi trạng thái cuộn hút (`DO`), đưa tín hiệu tương tự `4–20 mA` / `0–10 V` (`AI`) vào bộ hiệu chuẩn tuyến tính, hoặc dẫn cờ trạng thái nội bộ (`VFLAG`) và thanh ghi lưu giữ mất điện (`VREG_R`) sang nhiều nhánh logic song song.

---

## 2. Sơ đồ Chân & Hợp đồng Tín hiệu

Khối `INPUT` đóng vai trò nguồn phát tín hiệu thuần túy nên không có chân ngõ vào và sở hữu duy nhất một chân giá trị ngõ ra.

| Chân (Pin) | Chiều | Loại Tín hiệu | Kiểu Dữ liệu | Mô tả Kỹ thuật |
| :--- | :--- | :--- | :--- | :--- |
| `Out` | Ngõ ra (Output) | Giá trị / Dữ liệu (`IsEvent = false`) | Đa hình (`BOOL` \| `INT32` \| `REAL`) | Xuất giá trị thời gian thực kèm siêu dữ liệu kiểu của `Tag` đang chọn. Hỗ trợ rẽ nhánh `1:N` (fan-out) tới nhiều khối phía sau cùng lúc. |

---

## 3. Tham số Inspector & Giới hạn Kiểm tra

Khi nhấp chọn khối `INPUT` trên Canvas, bảng thuộc tính **Inspector** bên phải hiển thị đầy đủ các tham số cấu hình sau:

| Tham số | Kiểu / Tùy chọn | Mặc định | Ràng buộc & Giới hạn | Hành vi Thời gian thực |
| :--- | :--- | :--- | :--- | :--- |
| `Tag` | Bộ chọn Tag (`DI`, `DO`, `AI`, `VFLAG`, `VREG`, `VREG_R`, `COUNTER`) | `DI0` (ngõ vào số đầu tiên khả dụng) | Phải trỏ tới một Tag hợp lệ đã khai báo (`Index < 65535`). Bỏ trống sẽ báo lỗi biên dịch trước khi nạp. | Quyết định địa chỉ vùng nhớ vật lý được lấy mẫu mỗi chu kỳ quét và khóa chặt kiểu dữ liệu (`BOOL`, `INT32` hoặc `REAL`) cho các khối nối tiếp. |
| `CustomLabel` | Chuỗi văn bản tự do | Rỗng *(hiển thị `Tag.Name`, ví dụ `DI0`)* | Nhãn hiển thị tùy chọn; không làm thay đổi mã Tag hay tên bí danh (Alias) gốc trong Tag Catalog. | Thay thế tiêu đề trên đầu khối đồ họa giúp sơ đồ dễ đọc hơn (ví dụ: `"Phao Mức Cao Bồn Chứa"`). |
| `TagValueText` *(Simulator)* | Ô nhập Khóa theo Kiểu / Nút gạt (`[ 0 ]` `[ 1 ]` với `BOOL`; ô số với `INT32` / `REAL`) | `"0"` (`"0.0"` đối với `REAL`) | Khả dụng khi thiết kế Offline & chạy Mô phỏng (`F5`). Chịu sự kiểm soát của cơ chế Bảo vệ Dữ liệu Xấu. | Bơm trực tiếp giá trị giả lập vào bộ nhớ mô phỏng để kiểm thử toàn bộ chuỗi phát hiện sườn, hẹn giờ và so sánh ngưỡng. |

### Các Vùng nhớ Tag được Hỗ trợ

| Nhãn Loại Tag | Vùng nhớ Vật lý / Ảo | Kiểu Dữ liệu Gốc | Nguồn Tín hiệu Điển hình |
| :--- | :--- | :--- | :--- |
| `DI` | Ngõ vào Số (Discrete Input) | `BOOL` (`0` hoặc `1`) | Chân ngõ vào cách ly quang trên thiết bị (`24 VDC`, cảm biến tiệm cận, nút nhấn). |
| `DO` | Phản hồi Ngõ ra Số (Discrete Output) | `BOOL` (`0` hoặc `1`) | Trạng thái đóng/ngắt hiện hành của rơ-le, transistor hoặc SSR trên bo mạch. |
| `AI` | Ngõ vào Tương tự (Analog Input) | `INT32` / `REAL` | Kênh chuyển đổi ADC tích hợp (`4–20 mA`, `0–10 V`, cảm biến nhiệt độ). |
| `VFLAG` | Cờ Ảo Nội bộ (Virtual Flag) | `BOOL` (`0` hoặc `1`) | Bit nhớ trung gian lưu trên RAM phục vụ khóa liên động đa nhánh. |
| `VREG` | Thanh ghi Ảo (Virtual Register) | `INT32` / `REAL` | Thanh ghi 32-bit lưu trên RAM chứa kết quả tính toán, quy đổi hoặc dữ liệu truyền thông. |
| `VREG_R` | Thanh ghi Lưu giữ (Retentive Register) | `INT32` / `REAL` | Thanh ghi 32-bit lưu giữ bền vững khi mất điện (công thức mẻ, hệ số hiệu chuẩn, ngưỡng đặt). |
| `COUNTER` | Thanh ghi Đếm (`CV`) | `INT32` | Giá trị đếm lũy kế hiện hành của các bộ đếm xung phần cứng hoặc bộ đếm logic. |

---

## 4. Tương tác Kích thích trong Chế độ Mô phỏng (`F5`)

Trong quá trình chạy mô phỏng ngoại tuyến (`F5`), khối `INPUT` cho phép kỹ sư thử nghiệm tín hiệu trực tiếp:
- **Đảo Nhanh Nhị phân (`BOOL`):** Nhấp trực tiếp nút chuyển trên thân khối để đảo giá trị giữa `0` (`○ 0`) và `1` (`● 1`).
- **Chu trình Bơm Thử 4 Mức (`AI` / `VREG`):** Nhấp nút kiểm tra nhanh trên khối để xoay vòng qua 4 mốc thử chuẩn `0 → 50 → 90 → 100 → 0`, hoặc nhập con số bất kỳ vào ô `TagValueText` trên Inspector.

---

## 5. Cơ chế Bảo vệ Nhập liệu & Chẩn đoán Trực tuyến

### 5.1 Bảo vệ Chống Nhập sai Dữ liệu (`SmartInputParser`)

Nhằm bảo đảm **Cam kết Sự thật Thị giác** — con số hiển thị trên màn hình luôn khớp 1:1 với vùng nhớ vật lý — ô `TagValueText` áp dụng máy trạng thái xác nhận hai pha ngay khi nhấn `Enter` hoặc rời tiêu điểm chuột (`LostFocus`):

- **Tag Số (`BOOL`):** Hiển thị cặp nút nhị phân chuyên biệt `[ 0 ]` và `[ 1 ]`, đồng thời hỗ trợ chuẩn hóa dung thứ (`"true"`, `"on"`, `"bật"` → `"1"`; `"false"`, `"off"`, `"tắt"` → `"0"`). Mọi chuỗi sai quy cách sẽ lập tức hoàn nguyên về giá trị `"0"` hoặc `"1"` hợp lệ trước đó.
- **Tag Số nguyên (`INT32`):** Chỉ chấp nhận số nguyên có dấu 32-bit. Tự động làm sạch đuôi `.0` vô hại (`"100.0"` → chuẩn hóa thành `"100"`), nhưng **từ chối tuyệt đối phần lẻ thập phân** như `"25.5"` hoặc ký tự chữ (`"abc"`), tự động hoàn nguyên `TagValueText` về số nguyên đang lưu trong bộ nhớ.
- **Tag Số thực (`REAL`):** Chấp nhận cả dấu chấm lẫn dấu phẩy thập phân (`"25,5"` → `"25.5"`) và tự động thêm hậu tố `.0` cho số tròn (`"25"` → `"25.0"`) để khẳng định vùng nhớ đang vận hành theo chuẩn dấu phẩy động IEEE-754 32-bit.

### 5.2 Nhãn Chẩn đoán trong Mô phỏng (`F5`) & Gỡ lỗi Trực tuyến (`F8`)

| Chế độ Vận hành | Chỉ báo Trực quan trên Khối | Hành vi Chẩn đoán |
| :--- | :--- | :--- |
| **Offline / Thiết kế** | Nhãn loại Tag (`DI`, `AI`, `VREG_R`) + `ValueBadgeText` | Hiển thị giá trị khởi tạo hoặc mô phỏng được định dạng chuẩn theo kiểu dữ liệu (`0`/`1`, số nguyên hoặc số thực `0.0###`). |
| **Mô phỏng (`F5`)** | Nút tương tác `○ 0` / `● 1` hoặc nút xoay vòng giá trị | Viền khối và dây dẫn ngõ ra sáng màu xanh lá khi Tag số mang mức `1` hoặc Tag tương tự lớn hơn `0`. |
| **Gỡ lỗi Trực tuyến (`F8`)** | Nhãn `LiveBadgeText` cập nhật 20 FPS (`1`, `0`, `1,250`, `36.5`) | Phản ánh giá trị phần cứng thời gian thực. Chỉ các Tag kiểu `BOOL` mới đổi màu viền kích hoạt xanh lá; các thanh ghi số giữ nền xám trung tính chuẩn ISA-101 để chống mỏi mắt. |
| **Suy giảm Chất lượng** | Hậu tố ` [STALE]` / Giá trị biết gần nhất | Tự động gắn ` [STALE]` khi dữ liệu quá hạn làm tươi, và giữ lại mẫu đo cuối cùng khi rút cáp để kỹ sư dễ dàng khoanh vùng điểm đứt kết nối. |

---

## 6. Kịch bản Đấu nối Công nghiệp Thực tế

### Bài toán: Rẽ nhánh Một Cảm biến Áp suất Tương tự cho Hai Cấp Bảo vệ Quá áp

Một cảm biến áp suất đường ống đẩy `4–20 mA` đấu vào kênh `AI0` (đo dải `0–10000 mbar`) cần đồng thời kích hoạt **Cờ Cảnh báo Áp suất Cao** tại ngưỡng `8000 mbar` và **Ngắt Khẩn cấp Khởi động từ Bơm Tăng áp** tại ngưỡng `9500 mbar` từ cùng một khối nguồn `INPUT`.

### Khai báo Bảng Tag

| Tên Tag | Phân loại | Kiểu Dữ liệu | Mô tả / Tên Bí danh (Alias) |
| :--- | :--- | :--- | :--- |
| `AI0` | `AI` | `INT32` | `Áp suất Đường đẩy (0-10000 mbar)` |
| `VFLAG0` | `VFLAG` | `BOOL` | `Cờ Cảnh báo Áp suất Cao` |
| `DO0` | `DO` | `BOOL` | `Contactor Bơm Tăng áp Chính` |

### Trình tự Đấu nối trên Canvas

1. Nhấn `Ctrl+2` mở **Sơ đồ Logic**, thêm một khối **`INPUT`**, chọn `Tag = AI0` và đặt `CustomLabel = "Áp suất Đường đẩy"`.
2. Đang chọn khối `INPUT`, thêm nối tiếp khối **[`COMPARATOR`](/vi/blocks/comparator)** (`CompareOp = GTE`, `ThresholdLo = 8000`) → **[`TRIGGER`](/vi/blocks/trigger)** (`TriggerType = ON_RISE`, `ForMs = 500`) → **[`ACTION`](/vi/blocks/action)** (`ActionType = SET_TAG`, `TargetTag = VFLAG0`, `ActionParam = 1`).
3. Kéo nhánh dây thứ hai (rẽ nhánh `1:N` từ cùng chân `INPUT.Out`) sang khối **[`COMPARATOR`](/vi/blocks/comparator)** thứ hai (`CompareOp = GTE`, `ThresholdLo = 9500`) → **[`TRIGGER`](/vi/blocks/trigger)** (`TriggerType = ON_RISE`, `ForMs = 100`) → **[`ACTION`](/vi/blocks/action)** (`ActionType = SET_TAG`, `TargetTag = DO0`, `ActionParam = 0`).
4. Nhấn `F5` bật **Simulator**, chọn khối `INPUT` `AI0`, nhập `"8200"` vào ô `TagValueText` rồi nhấn `Enter`: quan sát cờ `VFLAG0` bật lên `1` sau `500 ms` trong khi `DO0` vẫn giữ mức `1`. Tiếp tục nhập `"9600"`, xác nhận `DO0` ngắt tức thì về `0` sau đúng `100 ms`.

<div class="synx-node-chain" style="margin-bottom: 12px;">
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
        <span class="synx-node-tag">AI0 • INT16</span>
        <span class="synx-node-alias">Áp Suất Đường Đẩy</span>
        <span class="synx-node-val">Cảm Biến Áp Lực</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">8200</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● GTE 8000</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">&gt;= 8000</span>
        <span class="synx-node-alias">Cảnh Báo Sớm</span>
        <span class="synx-node-val">Chạm Mức Báo Động</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">TRUE</span>
  </div>

  <div class="synx-canvas-node is-trigger">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M10,1 L5,8.5 L8.5,8.5 L6,15 L11,7.5 L7.5,7.5 Z" /></svg>
        <span>TRIGGER</span>
      </div>
      <span class="synx-node-status is-rise">● 500 ms</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">500 ms Filter</span>
        <span class="synx-node-alias">Lọc Chống Nhiễu Áp</span>
        <span class="synx-node-val">Xác Nhận Quá Áp Sớm</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">ENABLE</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● SET 1</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">VFLAG0 = 1</span>
        <span class="synx-node-alias">Cờ Báo Động VFLAG0</span>
        <span class="synx-node-val">Bật Đèn Cảnh Báo Vàng</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

<div class="synx-node-chain" style="margin-top: 12px;">
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
        <span class="synx-node-tag">AI0 • INT16</span>
        <span class="synx-node-alias">Áp Suất Đường Đẩy</span>
        <span class="synx-node-val">Cảm Biến Áp Lực</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">9600</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● GTE 9500</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">&gt;= 9500</span>
        <span class="synx-node-alias">Quá Áp Nguy Hiểm</span>
        <span class="synx-node-val">Ngưỡng Trip Khẩn Cấp</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">TRIP</span>
  </div>

  <div class="synx-canvas-node is-trigger">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M10,1 L5,8.5 L8.5,8.5 L6,15 L11,7.5 L7.5,7.5 Z" /></svg>
        <span>TRIGGER</span>
      </div>
      <span class="synx-node-status is-rise">● 100 ms</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">100 ms Filter</span>
        <span class="synx-node-alias">Cắt Nhanh Khẩn Cấp</span>
        <span class="synx-node-val">Bảo Vệ Vỡ Đường Ống</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">CUTOUT</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-pass">● SET 0</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">DO0 = 0</span>
        <span class="synx-node-alias">Ngắt Khẩn Bơm DO0</span>
        <span class="synx-node-val">Dừng Bơm Ngay Lập Tức</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>
