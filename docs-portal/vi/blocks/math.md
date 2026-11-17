# MATH — Khối Tính toán Số học

<div class="doc-meta-bar">
  <span><strong>Mã khối:</strong> MATH</span>
  <span><strong>Nhóm danh mục:</strong> Định thời, Bộ đếm &amp; Chuẩn hóa</span>
  <span><strong>Lớp thực thi:</strong> Khối Tính toán Số học ALU (IEC 61131-3)</span>
  <span><strong>Tiêu chuẩn:</strong> IEC 61131-3 ANY_NUM</span>
</div>

## 1. Tóm tắt Chức năng & Ngữ nghĩa Thực thi

Khối **`MATH`** (**Đơn vị Tính toán Số học — ALU**) thực hiện các phép toán đại số trên các toán hạng kiểu số (`INT32` và số thực `REAL` / `Float32`), sau đó ghi kết quả tính toán vào thanh ghi kết quả chỉ định (`OUT`).

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
| **Toán tử số học (Operator)** | `ADD`, `SUB`, `MUL`, `DIV`, `MOD` | `ADD` (`+`) | Chọn phép tính số học được khối thực thi. |
| **Chế độ kiểu dữ liệu** | `Auto`, `INT32`, `REAL` | `Auto` | Lựa chọn kiểu dữ liệu tính toán. Khi ở chế độ `Auto`, tự động chuyển sang `REAL` nếu có toán hạng số thực. |
| **Tag ngõ ra kết quả (OUT)** | `TagModel?` (`VREG`) | `VREG0` | Thanh ghi ảo nhận kết quả tính toán cuối cùng. |
| **Danh sách toán hạng (Operands)** | $2 \dots 8$ Chân | 2 Chân (`IN1`, `IN2`) | Danh sách toán hạng ngõ vào. Mỗi chân có thể chọn chế độ Biến Tag hoặc Hằng số độc lập. |

---

## 4. Đặc tả Toán tử & Quy tắc Kiểu Dữ liệu IEC 61131-3

### 4.1 Bảng Đặc tả Toán tử Số học

| Toán tử | Ký hiệu | Số chân cho phép | Công thức Đại số (Từ trái sang phải) | Ý nghĩa Ứng dụng Công nghiệp |
| :--- | :---: | :---: | :--- | :--- |
| **Cộng (`ADD`)** | `+` | $2 \dots 8$ | $OUT = IN_1 + IN_2 + \dots + IN_n$ | Tính tổng lưu lượng, tổng năng lượng, hoặc bù trừ điểm 0/offset. |
| **Trừ (`SUB`)** | `-` | $2 \dots 8$ | $OUT = IN_1 - IN_2 - \dots - IN_n$ | Tính chênh lệch áp suất ($\Delta P$), trừ bì mẻ cân (Gross - Tare). |
| **Nhân (`MUL`)** | `*` | $2 \dots 8$ | $OUT = IN_1 \times IN_2 \times \dots \times IN_n$ | Tính công suất tức thời ($P = U \times I$), tính khối lượng từ thể tích và tỷ trọng. |
| **Chia (`DIV`)** | `/` | Đúng 2 chân | $OUT = IN_1 / IN_2$ | Tính tỷ lệ phần trăm, tính trung bình. Tự động bảo vệ chống chia cho 0. |
| **Lấy dư (`MOD`)** | `%` | Đúng 2 chân | $OUT = IN_1 \pmod{IN_2}$ | Chia lấy phần dư cho thuật toán chia ca, quay vòng bộ nhớ buffer tuần hoàn. |

### 4.2 Quy tắc Nâng kiểu Tự động (Type Promotion)

Khi đặt **Chế độ kiểu dữ liệu** là **`Tự động (Auto)`**:

| Tập hợp kiểu dữ liệu đầu vào | Chế độ Thực thi | Kiểu Kết quả Ngõ ra | Ghi chú Vận hành |
| :--- | :---: | :---: | :--- |
| Toàn bộ toán hạng là `INT32` (hoặc số nguyên) | `INT32` | `INT32` | Làm tròn cụt về 0 đối với phần dư phép chia nguyên. |
| Có ít nhất một toán hạng mang kiểu `REAL` | `REAL` | `REAL` (`Float32`) | Tự động chuyển toàn bộ phép tính sang số thực IEEE-754. |
| Chọn ép kiểu tường minh `INT32` | `INT32` | `INT32` | Cắt bỏ phần thập phân ở ngõ ra. |
| Chọn ép kiểu tường minh `REAL` | `REAL` | `REAL` (`Float32`) | Quy đổi số nguyên sang số thực trước khi tính. |

---

## 5. Hộp thoại Cấu hình Chuyên dụng (`MathConfigDialogView`)

Khi nhấn nút **`Cấu hình Khối...`** trên thanh Inspector hoặc nhấp đúp vào khối trên Canvas, hộp thoại cấu hình nâng cao sẽ xuất hiện:

```
┌───────────────────────────────────────────────┬───────────────────────────────────────────────┐
│ BẢNG TRÁI: Tham số & Danh sách Toán hạng      │ BẢNG PHẢI: Chẩn đoán ALU & Mô phỏng Trực tiếp │
├───────────────────────────────────────────────┼───────────────────────────────────────────────┤
│ • Tham số chung:                              │ • Xem trước công thức đại số:                 │
│   Toán tử (+, -, *, /, %)                     │   VREG0 = AI0 + AI1 + 10                      │
│   Chế độ kiểu dữ liệu (Auto, INT32, REAL)     │                                               │
│   Tag ngõ ra (OUT)                            │ • Bàn thử nghiệm giá trị mô phỏng:            │
│                                               │   Nhập số thử nghiệm cho từng toán hạng       │
│ • Danh sách toán hạng (2 .. 8 chân):          │                                               │
│   Chế độ: Biến Tag / Hằng số                  │ • Kết quả ALU Output:                         │
│   Chọn Tag hoặc nhập số kèm huy hiệu kiểu     │   Hiển thị tức thời kết quả + Huy hiệu hợp lệ │
│   Nút [+ Thêm Chân] và nút [Xóa]              │                                               │
│                                               │ • Tóm tắt quy tắc số học công nghiệp          │
└───────────────────────────────────────────────┴───────────────────────────────────────────────┘
```

### 5.1 Bàn Chẩn đoán & Thử nghiệm Thời gian thực

Khu vực bên phải của hộp thoại đóng vai trò là môi trường chạy thử nghiệm nhanh:
1. **Xem trước Công thức Đại số:** Hiển thị tức thời biểu thức toán học sinh ra từ các chân toán hạng đã chọn.
2. **Nhập Thử Nghiệm Giá Trị:** Kỹ sư có thể gõ trực tiếp số giả định cho từng chân mà không làm ảnh hưởng đến giá trị thực tế của PLC.
3. **Kết quả và Huy hiệu An toàn:** Hiển thị tức thời kết quả tính toán (`ALU Output Result`), kiểu dữ liệu suy diễn (`Inferred Type: INT32/REAL`) và trạng thái an toàn:
   - `✓ VALID`: Phép toán hợp lệ và tính toán bình thường.
   - `FAULT: DIV_BY_ZERO`: Cảnh báo lỗi chia cho 0 khi số chia bằng 0.

---

## 6. Ví dụ Ứng dụng Thực tế

### Bài toán: Tính Tổng Lưu lượng Nước Cấp Lò hơi từ 3 Tuyến Ống

Một trạm lò hơi công nghiệp thu gom nước cấp từ 3 đường ống phụ, được đo bằng 3 cảm biến lưu lượng (`AI0`, `AI1`, `AI2` có đơn vị $\text{m}^3/\text{h}$, đã hiệu chuẩn sang `REAL` tại `VREG0`, `VREG1`, `VREG2`). Yêu cầu tính tổng lưu lượng nước cấp và lưu vào `VREG10`:

$$\text{Tổng Lưu Lượng (VREG10)} = \text{VREG0} + \text{VREG1} + \text{VREG2}$$

### Các bước Cấu hình trên SynaptiX IDE:

1. Kéo khối **`MATH`** từ nhóm công cụ **Analog & Actuators** vào Logic Graph Canvas.
2. Mở hộp thoại cấu hình bằng cách nhấn nút **`Cấu hình Khối...`**.
3. Chọn **Toán tử số học** là `+ Cộng đại số (ADD)`, **Chế độ kiểu dữ liệu** là `Tự động suy diễn (Auto)`.
4. Chọn **Tag ngõ ra kết quả (OUT)** là `VREG10`.
5. Nhấn **`+ Thêm Chân (Add Pin)`** để thêm toán hạng thứ 3 (`IN3`).
6. Thiết lập 3 chân:
   - `IN1`: Biến Tag $\rightarrow$ `VREG0`
   - `IN2`: Biến Tag $\rightarrow$ `VREG1`
   - `IN3`: Biến Tag $\rightarrow$ `VREG2`
7. Tại bảng **Giá trị mô phỏng** bên phải, nhập thử các giá trị (`25.5`, `30.2`, `14.3`) và quan sát kết quả tính ra `70.0` với huy hiệu `✓ VALID` và kiểu `REAL`.
8. Nhấn **`Lưu Cấu Hình (Apply)`** để áp dụng vào sơ đồ logic.
9. Nối dây ngõ ra `MATH.Out` tới khối [`COMPARATOR`](/vi/blocks/comparator) để cảnh báo khi tổng lưu lượng vượt ngưỡng thiết kế.
