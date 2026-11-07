# SCALE — Tuyến tính hóa & Hiệu chuẩn 2 Điểm

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-BLK-SCALE</span>
  <span><strong>Nhóm khối hàm:</strong> Định thời, Bộ đếm &amp; Chuẩn hóa</span>
  <span><strong>Phân lớp thực thi:</strong> Hàm truyền xử lý tín hiệu tương tự liên tục</span>
  <span><strong>Tiêu chuẩn:</strong> Chuẩn hóa Tín hiệu Đo lường IEC 61131-3</span>
</div>

## 1. Tổng quan & Ngữ nghĩa Thực thi

Khối hàm **`SCALE`** (**Analog Linear Scaler & Calibrator**) chuyển đổi giá trị nguyên thô đọc từ phần cứng (như điện áp `0–10,000 mV`, dòng vòng `4–20 mA`, xung ADC 12-bit `0–4095` hoặc thanh ghi cảm biến Modbus chưa quy đổi) sang đại lượng vật lý chuẩn dấu chấm động IEEE-754 (`bar`, `°C`, `m³/h`, `RPM`, `%`).

Ở mỗi chu kỳ quét, khối `SCALE` tính toán phương trình tuyến tính chuẩn công nghiệp kết hợp bộ khóa biên bão hòa an toàn:

$$y_{\text{raw}} = \text{Gain} \cdot x + \text{Offset}$$

$$y_{\text{out}} = \begin{cases} \min(\max(y_{\text{raw}}, \text{ClampMin}), \text{ClampMax}) & \text{khi } \text{IsClamped} = \text{true} \\ y_{\text{raw}} & \text{khi } \text{IsClamped} = \text{false} \end{cases}$$

Để loại bỏ sai sót tính nhẩm khi nghiệm thu tại hiện trường, thanh Inspector tích hợp sẵn **Công cụ Hiệu chuẩn 2 Điểm (2-Point Calibration Calculator)** tự động giải hệ phương trình tìm `Gain`, `Offset`, `ClampMin` và `ClampMax` từ 2 điểm đo chuẩn thực tế `(CalibRaw1, CalibEng1)` và `(CalibRaw2, CalibEng2)`.

---

## 2. Sơ đồ Chân & Hợp đồng Tín hiệu

Khối `SCALE` thực thi liên tục theo chu kỳ quét mà không cần chân kích hoạt xung sự kiện:

| Tên chân | Chiều | Bản chất Tín hiệu | Kiểu dữ liệu | Điều kiện Hiển thị | Hợp đồng Chức năng |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **`IN`** | Ngõ vào | Giá trị (`x`) | `INT32` / `REAL` | Luôn hiển thị | **Tín hiệu Thô Ngõ vào:** Nhận giá trị thô từ dây nối khối `INPUT` / `MB_READ` phía trước hoặc đọc trực tiếp từ `InputTag` (`AI0..AI3`, `VREG`). |
| **`OUT`** | Ngõ ra | Giá trị (`y`) | `REAL` (`FLOAT32`) | Luôn hiển thị | **Đại lượng Kỹ thuật Đã Quy đổi:** Xuất giá trị thực đã chuẩn hóa và kẹp biên sang khối [`COMPARATOR`](/vi/blocks/comparator), [`MQTT_PUB`](/vi/blocks/mqtt-pub) hoặc ghi vào `OutputTag`. |

---

## 3. Tham số Cấu hình & Ràng buộc Inspector

### 3.1. Nhóm Tham số Tuyến tính & Kẹp biên An toàn

| Tham số | Khóa Thuộc tính | Kiểu & Miền giá trị | Mặc định | Mô tả Kỹ thuật & Ràng buộc Vật lý |
| :--- | :--- | :--- | :---: | :--- |
| **Tag Ngõ vào** | `InputTag` | `TagModel?` | `null` | Biến tương tự/số nguyên nguồn (`AI0..AI3` hoặc `VREG`). Hiển thị huy hiệu `🔗 Connected: <Tag>` khi đã liên kết. |
| **Tag Ngõ ra** | `OutputTag` | `TagModel?` (`REAL`) | `null` | Thanh ghi ảo kiểu số thực `FLOAT32` (`VREG`) lưu kết quả sau quy đổi. |
| **Hệ số Khuếch đại (`Gain`)** | `Gain` | `Double` (`REAL`) | `0.01` | Độ dốc tuyến tính nhân trực tiếp với giá trị thô `x`. |
| **Độ lệch Điểm không (`Offset`)** | `Offset` | `Double` (`REAL`) | `0.0` | Hằng số tịnh tiến cộng thêm sau phép nhân `Gain`. Hỗ trợ giá trị âm (ví dụ `-4.0` để bù điểm gốc `4 mA`). |
| **Đơn vị Vật lý** | `Unit` | `String` | `"bar"` | Ký hiệu đơn vị kỹ thuật (`bar`, `°C`, `L/min`, `%`, `kPa`) hiển thị trên thân khối và Live Watch. |
| **Số chữ số Thập phân** | `DecimalPlaces` | `INT32` (`0..3`) | `1` | Độ phân giải phần lẻ (`0` đến `3` chữ số sau dấu chấm) hiển thị trực quan trên Canvas. |
| **Bật Kẹp biên (`IsClamped`)** | `IsClamped` | `BOOL` | `true` | Giới hạn cứng ngõ ra `OUT` nằm trong khoảng `[ClampMin .. ClampMax]`, ngăn hiện tượng đứt dây cảm biến `4–20 mA` sinh số âm làm sai lệch bộ điều khiển. |
| **Giới hạn Dưới (`ClampMin`)** | `ClampMin` | `Double` (`REAL`) | `0.0` | Mức sàn bão hòa dưới khi bật `IsClamped = true`. |
| **Giới hạn Trên (`ClampMax`)** | `ClampMax` | `Double` (`REAL`) | `100.0` | Mức trần bão hòa trên khi bật `IsClamped = true`. |

### 3.2. Công cụ Hiệu chuẩn 2 Điểm (`CalculateFromCalibPoints`)

Thay vì phải tự bấm máy tính giải phương trình đường thẳng, kỹ sư chỉ cần nhập 2 điểm chuẩn thực tế vào mục **2-Point Calibration** rồi nhấn nút **Tính Gain & Offset**:

| Ô Nhập Hiệu chuẩn | Thuộc tính ViewModel | Mặc định | Ý nghĩa Kỹ thuật |
| :--- | :--- | :---: | :--- |
| **Điểm 1 — Giá trị Thô (`X1`)** | `CalibRaw1` (`CalibX1`) | `0` | Giá trị mV / xung ADC / thanh ghi ở ngưỡng thấp (ví dụ: `2000` mV ứng với `4 mA`). |
| **Điểm 1 — Đại lượng Thực (`Y1`)** | `CalibEng1` (`CalibY1`) | `0` | Giá trị vật lý chuẩn tại Điểm 1 (ví dụ: `0.0` bar). |
| **Điểm 2 — Giá trị Thô (`X2`)** | `CalibRaw2` (`CalibX2`) | `10000` | Giá trị mV / xung ADC / thanh ghi ở toàn thang đo (ví dụ: `10000` mV ứng với `20 mA`). |
| **Điểm 2 — Đại lượng Thực (`Y2`)** | `CalibEng2` (`CalibY2`) | `100` | Giá trị vật lý chuẩn tại Điểm 2 (ví dụ: `16.0` bar). |

Khi nhấn nút tính toán, SynaptiX IDE tự động xác định:

$$\text{Gain} = \text{round}\!\left(\frac{Y_2 - Y_1}{X_2 - X_1},\; 6\right), \qquad \text{Offset} = \text{round}\!\left(Y_1 - \text{Gain} \cdot X_1,\; 6\right)$$

đồng thời tự động gán `ClampMin = min(Y1, Y2)`, `ClampMax = max(Y1, Y2)` và bật `IsClamped = true`.

### Bảng Tra cứu Nhanh Các Chuẩn Cảm biến Công nghiệp

| Chuẩn Cảm biến | Dải Tín hiệu Thô (`X1 .. X2`) | Dải Đo Thực tế (`Y1 .. Y2`) | `Gain` Tính được | `Offset` Tính được |
| :--- | :---: | :---: | :---: | :---: |
| **Áp suất `0–10 V` (`0..10 bar`)** | `0 .. 10000` (mV) | `0.0 .. 10.0` bar | `0.001` | `0.0` |
| **Áp suất `4–20 mA` (`0..16 bar` qua trở `500 Ω`)** | `2000 .. 10000` (mV) | `0.0 .. 16.0` bar | `0.002` | `-4.0` |
| **Nhiệt độ PT100 `4–20 mA` (`-50..+150 °C`)** | `4000 .. 20000` (µA) | `-50.0 .. 150.0` °C | `0.0125` | `-100.0` |
| **Mức bồn `ADC 12-Bit` (`0..4095` → `0..100 %`)** | `0 .. 4095` (xung) | `0.0 .. 100.0` % | `0.02442` | `0.0` |

---

## 4. Quy tắc Tính toán & Kẹp Biên An toàn

Khối `SCALE` tính toán giá trị ngõ ra theo 2 bước tức thời trong vòng quét:
1. **Quy đổi tuyến tính:** $y = \text{Gain} \cdot x + \text{Offset}$ (với $x$ là giá trị thô từ ngõ vào `InputTag` hoặc chân `In`).
2. **Kẹp biên an toàn (`IsClamped = true`):** Nếu giá trị tính ra vượt ra ngoài khoảng `[ClampMin .. ClampMax]`, ngõ ra tự động được kẹp về giá trị biên gần nhất, bảo vệ hệ thống khỏi hiện tượng cảm biến đứt dây sinh ra số âm hoặc vọt lố.

---

## 5. Kiểm tra Hợp lệ, Xử lý Lỗi & Chẩn đoán Trực tuyến

- **Chống Chia Cho 0 Khi Hiệu chuẩn:** Nếu người dùng nhập trùng 2 điểm thô (`|CalibRaw2 - CalibRaw1| < 1e-6`), công cụ tính nhanh sẽ tự động chặn phép chia nhằm bảo vệ hệ thống khỏi giá trị `NaN` hoặc `Infinity`.
- **Tự động Chuẩn hóa Thứ tự Biên Kẹp:** Ngay cả khi cấu hình cảm biến tỷ lệ nghịch có `CalibY1 > CalibY2`, thuật toán kẹp biên vẫn tự động lấy `min(ClampMin, ClampMax)` và `max(ClampMin, ClampMax)` chính xác tuyệt đối.
- **Bảo toàn Độ chính xác Dấu chấm động:** Luôn gán `OutputTag` vào một thanh ghi ảo kiểu **`REAL` (`FLOAT32`)** trong [Danh mục Biến — Tag Catalog](/vi/workspaces/tag-catalog-tab) để phần thập phân (như `6.45 bar`) được lưu trữ nguyên vẹn trong bộ nhớ mà không bị làm tròn thành số nguyên.
- **Thanh Tiến trình Trực quan (`F8`):** Hiển thị công thức toán học trực tiếp (`y = 0.002·AI0 - 4`) khi tĩnh và cập nhật tức thời giá trị thực kèm phần trăm toàn thang đo **`6.4 bar (40%)`** khi chạy trực tuyến.

---

## 6. Ví dụ Đấu nối Công nghiệp Thực tế

### Giám sát & Cắt Bảo vệ Quá áp Đường ống Bơm Tăng áp `4–20 mA` (`0.0 .. 16.0 bar`)

**Bài toán:** Cảm biến áp suất `4–20 mA` đấu qua điện trở chuẩn `500 Ω` vào kênh `AI0` (`2000 mV` ứng với `0 bar`, `10000 mV` ứng với `16 bar`). Yêu cầu chặn nhiễu âm khi đứt cáp về `0.0 bar`, lưu áp suất thực vào `VREG0_PipeBar` (`REAL`), đẩy telemetry và ngắt rơ-le bơm khi áp suất vượt `12.5 bar`.

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>INPUT</span>
      </div>
      <span class="synx-node-status is-live">● AI0</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Cảm Biến 4-20mA</span>
        <span class="synx-node-alias">Áp Suất Thô AI0</span>
        <span class="synx-node-val">2000..10000 mV</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Raw ADC</span>
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
        <span class="synx-node-tag">Quy Đổi Áp Suất</span>
        <span class="synx-node-alias">Thanh Ghi VREG0</span>
        <span class="synx-node-val">0.0 .. 16.0 bar</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">13.1 bar</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● TRIP HIGH</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Bảo Vệ Quá Áp</span>
        <span class="synx-node-alias">Ngưỡng Giới Hạn</span>
        <span class="synx-node-val">&gt; 12.5 bar</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">TRIP</span>
  </div>

  <div class="synx-canvas-node is-direct">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>DIRECT_WRITE</span>
      </div>
      <span class="synx-node-status is-active">● SHUTDOWN</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Ngắt Rơ-le Bơm</span>
        <span class="synx-node-alias">Dừng Khẩn Cấp</span>
        <span class="synx-node-val">DO0 = 0</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

**Các bước cấu hình:**
1. Tạo biến `VREG0_PipeBar` trong [Danh mục Biến — Tag Catalog](/vi/workspaces/tag-catalog-tab) với kiểu dữ liệu **`REAL` (`FLOAT32`)**.
2. Đặt khối **`SCALE`** lên Canvas, mở mục **2-Point Calibration** trong Inspector và nhập:
   - `CalibRaw1` = `2000`, `CalibEng1` = `0`
   - `CalibRaw2` = `10000`, `CalibEng2` = `16`
   - Nhấn **Tính Gain & Offset** → hệ thống tự động điền `Gain = 0.002`, `Offset = -4`, `ClampMin = 0`, `ClampMax = 16`, `IsClamped = true`.
3. Đặt `Unit` = `"bar"`, `DecimalPlaces` = `2`, `InputTag` = `AI0` và `OutputTag` = `VREG0_PipeBar`.
4. Nối chân **`OUT`** sang khối [`COMPARATOR`](/vi/blocks/comparator) (`> 12.5`) và khối [`MQTT_PUB`](/vi/blocks/mqtt-pub) (`COV`, `Deadband = 0.2`).
