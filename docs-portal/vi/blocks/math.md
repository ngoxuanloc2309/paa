# MATH — Khối Tính toán Số học

<div class="doc-meta-bar">
  <span><strong>Mã khối:</strong> MATH</span>
  <span><strong>Nhóm danh mục:</strong> Định thời, Bộ đếm &amp; Chuẩn hóa</span>
  <span><strong>Lớp thực thi:</strong> Khối Tính toán Số học ALU (IEC 61131-3)</span>
  <span><strong>Tiêu chuẩn:</strong> IEC 61131-3 ANY_NUM</span>
</div>

## 1. Tóm tắt Chức năng & Ngữ nghĩa Thực thi

Khối `MATH` (**Đơn vị Tính toán Số học — ALU**) thực hiện các phép toán đại số trên các toán hạng kiểu số (`INT32` và số thực `REAL` / `Float32`), sau đó ghi kết quả tính toán vào thanh ghi kết quả chỉ định (`OUT`).

- **Toán tử Số học Chuẩn IEC 61131-3:** Hỗ trợ đầy đủ các phép toán công nghiệp cơ bản: Phép Cộng (`ADD`), Phép Trừ (`SUB`), Phép Nhân (`MUL`), Phép Chia (`DIV`) và Lấy phần dư (`MOD`).
- **Mở rộng Chuỗi Toán hạng ($2 \dots 8$ Chân):** Khác với các khối toán học PLC truyền thống thường bị giới hạn ở 2 chân, khối `MATH` cho phép ghép nối mở rộng từ 2 đến 8 toán hạng đối với phép Cộng, Trừ và Nhân (tính tuần tự từ trái sang phải: $A - B - C - \dots$). Riêng phép Chia và Modulo luôn khóa chặt đúng 2 toán hạng để đảm bảo an toàn vận hành.
- **Tự động Suy diễn & Nâng kiểu Dữ liệu (`Auto`):** Khi ở chế độ `Tự động (Auto)`, khối tự động nâng kiểu tính toán lên số thực dấu phẩy động IEEE-754 `REAL` nếu có ít nhất một toán hạng mang kiểu số thực; ngược lại, khối thực hiện tính toán trên số nguyên có dấu 32-bit (`INT32`).
- **Bảo vệ Chống Chia cho 0 (Fault Protection):** Nếu số chia bằng 0 ($x / 0$), khối tự động chặn lỗi tràn runtime, giữ nguyên giá trị chu kỳ trước của ngõ ra và bật cờ cảnh báo lỗi chẩn đoán.
- **Bàn Thử nghiệm Chẩn đoán Thời gian thực (Live Simulation):** Hộp thoại cấu hình tích hợp sẵn bộ kiểm thử ALU cho phép kỹ sư nhập thử giá trị và kiểm tra công thức trước khi nạp xuống thiết bị.

---

## 2. Sơ đồ Chân & Hợp đồng Tín hiệu

Khối `MATH` thực hiện tính toán ở mỗi chu kỳ quét máy hoặc khi có kết nối dây dẫn dữ liệu:

| Chân (Pin) | Chiều | Loại Tín hiệu | Kiểu Dữ liệu | Mô tả Kỹ thuật |
| :--- | :--- | :--- | :--- | :--- |
| `IN1` .. `IN8` | Ngõ vào (Input) | Giá trị / Dữ liệu (`IsEvent = false`) | `INT32` \| `REAL` | Các toán hạng đầu vào của phép tính. Có thể liên kết với Tag biến (`AI`, `VREG`, `COUNTER`) hoặc đặt giá trị hằng số cố định. |
| `Out` | Ngõ ra (Output) | Giá trị / Dữ liệu (`IsEvent = false`) | `INT32` \| `REAL` | Kết quả của biểu thức toán học. Xuất giá trị tới các khối tiếp theo ([`COMPARATOR`](/vi/blocks/comparator), [`SCALE`](/vi/blocks/scale), [`ACTION`](/vi/blocks/action)) hoặc cập nhật vào `OutputTag`. |

---

## 3. Tham số Inspector & Cấu hình

| Tham số | Kiểu / Tùy chọn | Mặc định | Mô tả & Hành vi Hoạt động |
| :--- | :--- | :---: | :--- |
| Toán tử số học (Operator) | `ADD`, `SUB`, `MUL`, `DIV`, `MOD` | `ADD` (`+`) | Chọn phép tính số học được khối thực thi. |
| Chế độ kiểu dữ liệu | `Auto`, `INT32`, `REAL` | `Auto` | Lựa chọn kiểu dữ liệu tính toán. Khi ở chế độ `Auto`, tự động chuyển sang `REAL` nếu có toán hạng số thực. |
| Tag ngõ ra kết quả (OUT) | `TagModel?` (`VREG`) | `VREG0` | Thanh ghi ảo nhận kết quả tính toán cuối cùng. |
| Danh sách toán hạng (Operands) | $2 \dots 8$ Chân | 2 Chân (`IN1`, `IN2`) | Danh sách toán hạng ngõ vào. Mỗi chân có thể chọn chế độ Biến Tag hoặc Hằng số độc lập. |

---

## 4. Đặc tả Toán tử & Quy tắc Kiểu Dữ liệu IEC 61131-3

### 4.1 Bảng Đặc tả Toán tử Số học

| Toán tử | Ký hiệu | Số chân cho phép | Công thức Đại số (Từ trái sang phải) | Ý nghĩa Ứng dụng Công nghiệp |
| :--- | :---: | :---: | :--- | :--- |
| Cộng (`ADD`) | `+` | $2 \dots 8$ | $OUT = IN_1 + IN_2 + \dots + IN_n$ | Tính tổng lưu lượng, tổng năng lượng, hoặc bù trừ điểm 0/offset. |
| Trừ (`SUB`) | `-` | $2 \dots 8$ | $OUT = IN_1 - IN_2 - \dots - IN_n$ | Tính chênh lệch áp suất ($\Delta P$), trừ bì mẻ cân (Gross - Tare). |
| Nhân (`MUL`) | `*` | $2 \dots 8$ | $OUT = IN_1 \times IN_2 \times \dots \times IN_n$ | Tính công suất tức thời ($P = U \times I$), tính khối lượng từ thể tích và tỷ trọng. |
| Chia (`DIV`) | `/` | Đúng 2 chân | $OUT = IN_1 / IN_2$ | Tính tỷ lệ phần trăm, tính trung bình. Tự động bảo vệ chống chia cho 0. |
| Lấy dư (`MOD`) | `%` | Đúng 2 chân | $OUT = IN_1 \pmod{IN_2}$ | Chia lấy phần dư cho thuật toán chia ca, quay vòng bộ nhớ buffer tuần hoàn. |

### 4.2 Quy tắc Nâng kiểu Tự động (Type Promotion)

Khi đặt **Chế độ kiểu dữ liệu** là `Tự động (*Auto*)`:

| Tập hợp kiểu dữ liệu đầu vào | Chế độ Thực thi | Kiểu Kết quả Ngõ ra | Ghi chú Vận hành |
| :--- | :---: | :---: | :--- |
| Toàn bộ toán hạng là `INT32` (hoặc số nguyên) | `INT32` | `INT32` | Làm tròn cụt về 0 đối với phần dư phép chia nguyên. |
| Có ít nhất một toán hạng mang kiểu `REAL` | `REAL` | `REAL` (`Float32`) | Tự động chuyển toàn bộ phép tính sang số thực IEEE-754. |
| Chọn ép kiểu tường minh `INT32` | `INT32` | `INT32` | Cắt bỏ phần thập phân ở ngõ ra. |
| Chọn ép kiểu tường minh `REAL` | `REAL` | `REAL` (`Float32`) | Quy đổi số nguyên sang số thực trước khi tính. |

---

## 5. Hộp thoại Cấu hình Chuyên dụng (`MathConfigDialogView`)

Khi nhấn nút `Cấu hình Khối...` trên thanh Inspector hoặc nhấp đúp vào khối trên Canvas, hộp thoại cấu hình nâng cao chuẩn công nghiệp Fluent 2 Desktop sẽ xuất hiện:

![Hộp thoại Cấu hình Khối MATH trên SynaptiX IDE](/images/synaptix_math_config_dialog.png)

### 5.1 Kiến trúc Hai Cột Master-Detail

Hộp thoại được thiết kế phân chia 2 khu vực chức năng độc lập:
- **Cột Trái (Thiết lập Master & Bảng Toán hạng):**
  - **Nhóm 1 — Tham số chung:** Chọn phép toán (`ADD`, `SUB`, `MUL`, `DIV`, `MOD`), chế độ kiểu dữ liệu (`Auto`, `INT32`, `REAL`) và thanh ghi nhận kết quả (`OUT`).
  - **Nhóm 2 — Bảng toán hạng ngõ vào:** Hỗ trợ từ 2 đến 8 chân. Mỗi chân cho phép chuyển đổi linh hoạt giữa chế độ **Biến Tag** (chọn từ danh mục tag) hoặc **Hằng số** (nhập giá trị tĩnh). Có thể nhấn `+ Thêm Chân Toán Hạng` hoặc xóa chân tùy ý.
- **Cột Phải (Bàn Chẩn đoán ALU & Mô phỏng Trực tiếp):**
  - **Xem trước Công thức Đại số:** Hiển thị tức thời biểu thức toán học tương ứng với các chân đã chọn (ví dụ: `VREG10 = IN1 + IN2 + IN3`).
  - **Bàn Thử Nghiệm Mô Phỏng:** Cho phép kỹ sư gõ thử các giá trị giả định cho từng chân toán hạng để kiểm tra tính đúng đắn trước khi nạp xuống thiết bị.
  - **Kết quả & Huy hiệu An toàn:** Hiển thị tức thời kết quả đầu ra ALU, kiểu dữ liệu suy diễn (`REAL` hoặc `INT32`) và cờ trạng thái (`✓ TRẠNG THÁI HỢP LỆ` hoặc `FAULT: DIV_BY_ZERO`).

---

## 6. Ví dụ Ứng dụng Thực tế & Sơ đồ Mạch Đi Dây

### 6.1. Ví dụ 1: Tính Tổng Lưu Lượng Nước Cấp Lò Hơi 3 Tuyến Ống & Cắt Báo Động Quá Tải (`ADD`)

**Mục tiêu bài toán:**
Một trạm lò hơi công nghiệp thu gom nước cấp từ 3 đường ống phụ, được đo bởi 3 cảm biến lưu lượng tức thời (`AI0`, `AI1`, `AI2`, đơn vị $\text{m}^3/\text{h}$, đã hiệu chuẩn về dạng số thực `REAL` tại các thanh ghi ảo `VREG0`, `VREG1`, `VREG2`).
Khối `MATH` thực hiện phép cộng đại số 3 toán hạng để tính tổng lưu lượng cấp vào bộ góp và lưu kết quả vào `VREG10_TotalFlow`:

$$\text{Tổng Lưu Lượng (VREG10)} = \text{VREG0} + \text{VREG1} + \text{VREG2}$$

Khi tổng lưu lượng vượt quá giới hạn thiết kế an toàn của bộ góp hơi ($> 80.0\text{ m}^3/\text{h}$), khối `COMPARATOR` sẽ kích hoạt rơ-le còi đèn báo động quá tải `DO0_ALARM_HIGH`.

#### Sơ đồ Mạch Logic Khối hàm trên Canvas:

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>INPUT (3x)</span>
      </div>
      <span class="synx-node-status is-live">● VREG0..2</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">3 Tuyến Cấp Nước</span>
        <span class="synx-node-alias">Lưu lượng Line 1-3</span>
        <span class="synx-node-val">25.5 + 30.2 + 14.3</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Flow m³/h</span>
  </div>

  <div class="synx-canvas-node is-timer">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M4,7 L12,7 M8,3 L8,11 M14,14 L18,18 M18,14 L14,18 M4,16 L10,16 M4,18 L10,18" /></svg>
        <span>MATH</span>
      </div>
      <span class="synx-node-status is-active">● ADD (+)</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● IN1..3</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Phép Cộng 3 Chân</span>
        <span class="synx-node-alias">VREG10_TotalFlow</span>
        <span class="synx-node-val">70.0 m³/h (REAL)</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">70.0 m³/h</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● SO SÁNH</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Giám Sát Giới Hạn</span>
        <span class="synx-node-alias">Ngưỡng An Toàn</span>
        <span class="synx-node-val">&gt; 80.0 m³/h</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">ALARM</span>
  </div>

  <div class="synx-canvas-node is-direct">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● KÍCH HOẠT</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Rơ-le Báo Động</span>
        <span class="synx-node-alias">Còi Đèn Cảnh Báo</span>
        <span class="synx-node-val">DO0 = 1</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

#### Bảng Tham số Cấu hình Khối MATH:

| Tham số Cấu hình | Giá trị Thiết lập | Ý nghĩa Kỹ thuật |
| :--- | :--- | :--- |
| Toán tử số học (Operator) | `ADD` (`+`) | Cộng dồn lưu lượng đồng thời từ cả 3 tuyến ống. |
| Chế độ kiểu (Data Type Mode) | `Auto (ANY_NUM)` | Tự động nâng kiểu lên `REAL` vì các toán hạng mang kiểu số thực. |
| Tag ngõ ra kết quả (OUT) | `VREG10_TotalFlow` | Thanh ghi lưu tổng lưu lượng để hiển thị SCADA và so sánh ngưỡng. |
| Chân toán hạng IN1 | `VREG0_Line1Flow` | Tín hiệu lưu lượng tức thời Tuyến 1 ($25.5\text{ m}^3/\text{h}$). |
| Chân toán hạng IN2 | `VREG1_Line2Flow` | Tín hiệu lưu lượng tức thời Tuyến 2 ($30.2\text{ m}^3/\text{h}$). |
| Chân toán hạng IN3 | `VREG2_Line3Flow` | Tín hiệu lưu lượng tức thời Tuyến 3 ($14.3\text{ m}^3/\text{h}$). |

#### Bảng Ma trận Kiểm thử & Nghiệm thu Thực tế:

| Kịch bản Vận hành | Line 1 (`VREG0`) | Line 2 (`VREG1`) | Line 3 (`VREG2`) | Tổng ALU (`VREG10`) | Trạng thái So sánh (`> 80.0`) | Ngõ ra Còi Báo (`DO0`) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| Vận hành Bình thường | $25.5\text{ m}^3/\text{h}$ | $30.2\text{ m}^3/\text{h}$ | $14.3\text{ m}^3/\text{h}$ | $70.0\text{ m}^3/\text{h}$ | `FALSE` (Chưa vượt ngưỡng) | `0` (Tắt còi báo) |
| Một tuyến bảo trì | $0.0\text{ m}^3/\text{h}$ | $35.0\text{ m}^3/\text{h}$ | $20.0\text{ m}^3/\text{h}$ | $55.0\text{ m}^3/\text{h}$ | `FALSE` (Chưa vượt ngưỡng) | `0` (Tắt còi báo) |
| Đỉnh tải vượt ngưỡng | $32.0\text{ m}^3/\text{h}$ | $38.5\text{ m}^3/\text{h}$ | $15.5\text{ m}^3/\text{h}$ | $86.0\text{ m}^3/\text{h}$ | `TRUE` (Vượt ngưỡng 80.0) | `1` (Bật còi báo động) |

---

### 6.2. Ví dụ 2: Trừ Bì Cân Phễu Đóng Bao & Tự Động Ngắt Nạp Định Lượng (`SUB`)

**Mục tiêu bài toán:**
Hệ thống cân đóng bao tự động sử dụng đầu cân loadcell analog đọc tổng khối lượng phễu chứa qua kênh `AI0` ($0 \dots 100\text{ kg}$). Khi bao rỗng được đưa vào kẹp, hệ thống chốt giá trị bì lưu vào `VREG1_TareWeight` ($2.5\text{ kg}$).
Khối `MATH` thực hiện phép trừ đại số để suy ra khối lượng tịnh thực tế:

$$\text{Khối lượng Tịnh (VREG2\_NetWeight)} = \text{AI0\_GrossWeight} - \text{VREG1\_TareWeight}$$

Khi khối lượng tịnh đạt đủ định lượng bao chuẩn ($50.0\text{ kg}$), khối `COMPARATOR` đóng van cấp liệu nạp bao `DO1_VALVE_CLOSE`.

#### Sơ đồ Mạch Logic Khối hàm trên Canvas:

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>INPUT</span>
      </div>
      <span class="synx-node-status is-live">● AI0 / VREG1</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Cảm Biến Cân</span>
        <span class="synx-node-alias">Tổng Cân &amp; Giá Trị Bì</span>
        <span class="synx-node-val">Gross: 52.5 kg, Tare: 2.5 kg</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Weight (kg)</span>
  </div>

  <div class="synx-canvas-node is-timer">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M4,7 L12,7 M8,3 L8,11 M14,14 L18,18 M18,14 L14,18 M4,16 L10,16 M4,18 L10,18" /></svg>
        <span>MATH</span>
      </div>
      <span class="synx-node-status is-active">● SUB (-)</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● IN1, IN2</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Phép Trừ Bì</span>
        <span class="synx-node-alias">VREG2_NetWeight</span>
        <span class="synx-node-val">50.0 kg (Net)</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">50.0 kg</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● ĐỦ ĐỊNH LƯỢNG</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Kiểm Tra Trọng Lượng</span>
        <span class="synx-node-alias">Định Lượng Chuẩn</span>
        <span class="synx-node-val">&gt;= 50.0 kg</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">CUTOFF</span>
  </div>

  <div class="synx-canvas-node is-direct">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● ĐÓNG VAN</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Ngắt Van Định Lượng</span>
        <span class="synx-node-alias">Khóa Xả Liệu</span>
        <span class="synx-node-val">DO1 = 0 (Close)</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

#### Bảng Cấu hình Khối Trừ Bì:
- **Toán tử:** `SUB (-)` (Trừ đại số).
- **Chân IN1:** `AI0_GrossWeight` (Tổng cân đọc từ loadcell).
- **Chân IN2:** `VREG1_TareWeight` (Trọng lượng vỏ bao bì chốt lúc kẹp bao).
- **Ngõ ra OUT:** `VREG2_NetWeight` (Khối lượng tịnh thực của nguyên liệu).
- **Phòng vệ an toàn:** Khi bao bì được nhấc ra khỏi bàn cân (`Gross < Tare`), giá trị ngõ ra có thể âm tạm thời. Kỹ sư kết hợp cờ kẹp biên của khối [`SCALE`](/vi/blocks/scale) (`ClampMin = 0.0`) để giữ cho giá trị tịnh hiển thị trên bảng điện tử không bị số âm gây nhiễu loạn ca làm việc.