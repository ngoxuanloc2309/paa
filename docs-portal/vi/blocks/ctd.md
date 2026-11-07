# CTD — Bộ Đếm Lùi (Count Down Counter)

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-BLK-CTD</span>
  <span><strong>Nhóm khối hàm:</strong> Định thời, Bộ đếm &amp; Chuẩn hóa</span>
  <span><strong>Phân lớp thực thi:</strong> Bộ đếm giảm trạng thái kích hoạt theo sườn xung</span>
  <span><strong>Tiêu chuẩn:</strong> Mô hình Bộ đếm IEC 61131-3</span>
</div>

## 1. Tổng quan & Ngữ nghĩa Thực thi

Khối hàm **`CTD`** (**Count Down Counter — Bộ Đếm Lùi**) là bộ đếm giảm nạp sẵn định mức, được thiết kế chuyên biệt cho các bài toán theo dõi số lượng phôi còn lại trong khay cấp liệu, đếm ngược liều chiết rót, phát hiện cạn khoang chứa (Magazine Empty) và khóa liên động tuổi thọ dao cụ cơ khí.

Trong mỗi chu kỳ quét tất định của bộ thực thi SynaptiX:
1. **Ưu tiên Nạp Định mức / Khởi tạo lại (`R` / `ResetTag`):** Khi chân **`R`** (hoặc biến `ResetTag`) chuyển sang mức tích cực (`1`), thanh ghi **Giá trị Hiện tại (`CV`)** lập tức được nạp bằng **`PresetValue` (`PV`)** (`CV ← PV`). Với `PV > 0`, ngõ ra **`Q`** lập tức trở về mức `0` (`FALSE`).
2. **Đếm Lùi theo Sườn lên (`CD`):** Khi chân `R` ở mức `0`, mỗi sườn lên (`0 → 1`) xuất hiện tại chân **`CD`** sẽ giảm `CV` đi `1` đơn vị (`CV ← CV - 1`). Tín hiệu `CD` giữ nguyên mức `1` qua nhiều chu kỳ quét liên tiếp sẽ không làm giảm tiếp bộ đếm.
3. **Đánh giá Cạn Định mức (`CV ≤ 0`):** Ngõ ra **`Q`** bật lên mức `1` (`TRUE`) ngay khi `CV ≤ 0`, báo hiệu toàn bộ định mức nạp ban đầu đã được sử dụng hết.
4. **Đồng bộ Vùng nhớ Tag:** Giá trị `CV` cập nhật liên tục vào **`CvTag`** (`COUNTER0..COUNTER7` hoặc `VREG`), và trạng thái `Q` ghi ra **`OutputTag`**.

::: tip Sự Khác biệt Giữa Chân `R` của `CTU` và `CTD`
Ở khối [`CTU`](/vi/blocks/ctu), kích hoạt chân `R` sẽ xóa `CV → 0`. Ngược lại, ở khối **`CTD`**, kích hoạt chân `R` sẽ **nạp lại định mức xuất phát `CV ← PV`** (tương đương ngữ nghĩa chân `LD` — Load Preset trong chuẩn IEC 61131-3), giúp máy sẵn sàng ngay cho mẻ đếm lùi kế tiếp.
:::

---

## 2. Sơ đồ Chân & Hợp đồng Tín hiệu

Khối `CTD` sử dụng kiến trúc `CounterNodeViewModel` với `CounterMode = "CTD"`, bao gồm 2 chân vào ở cạnh trái và 1 chân ra ở cạnh phải:

| Tên chân | Chiều | Bản chất Tín hiệu | Kiểu dữ liệu | Điều kiện Hiển thị | Hợp đồng Chức năng |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **`CD`** | Ngõ vào | Sự kiện (`0 → 1`) | `BOOL` | Luôn hiển thị | **Xung Đếm Lùi (Count Down):** Giảm `CV` đi `1` tại mỗi sườn lên (`0 → 1`). Có thể nối dây trên Canvas hoặc gán trực tiếp qua `InputTag`. |
| **`R`** | Ngõ vào | Sự kiện / Mức | `BOOL` | Luôn hiển thị | **Chân Nạp Định mức (Load / Reset):** Nạp `CV ← PV` và đưa `Q → 0` (với `PV > 0`). Ưu tiên cao hơn `CD` nếu tác động đồng thời. |
| **`Q`** | Ngõ ra | Giá trị logic | `BOOL` | Luôn hiển thị | **Cờ Cạn Định mức (Zero Reached):** Xuất mức `1` (`TRUE`) khi `CV ≤ 0`; giữ mức `0` (`FALSE`) khi `CV > 0`. |

---

## 3. Tham số Cấu hình & Ràng buộc Inspector

Khi chọn khối `CTD` trên [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab), bảng **Inspector** bên phải cung cấp các thông số sau:

| Tham số | Khóa Thuộc tính | Kiểu & Miền giá trị | Mặc định | Mô tả Kỹ thuật & Ràng buộc Vật lý |
| :--- | :--- | :--- | :---: | :--- |
| **Nhãn Tùy chỉnh** | `CustomLabel` | `String` | `"CTD Counter"` | Tên hiển thị tùy chọn trên thẻ khối ngoài vùng vẽ Canvas. |
| **Chế độ Bộ đếm** | `CounterMode` | `"CTU"` \| `"CTD"` | `"CTD"` | Chọn chế độ đếm lùi (`CD`, `CV ≤ 0`, nạp lại `CV ← PV` khi kích hoạt `R`). |
| **Giá trị Đặt trước (`PV`)** | `PresetValue` | `INT32` (`≥ 1`) | `10` | Quỹ số lượng ban đầu được nạp vào `CV` mỗi khi kích hoạt `R` / `ResetTag`. Tuân thủ kiểm tra nghiêm ngặt kiểu số nguyên `INT32`. |
| **Tag Nguồn Đếm Lùi** | `InputTag` | `TagModel?` (`BOOL`) | `null` | Biến số tùy chọn (`DI0..DI7`, `VBIT`) cấp xung `CD` trực tiếp không cần dây nối trên sơ đồ. |
| **Tag Giá trị Hiện tại (`CV`)** | `CvTag` | `TagModel?` (`INT32`) | `COUNTER0` | Thanh ghi đếm (`COUNTER0..COUNTER7`) hoặc thanh ghi ảo (`VREG`) lưu số lượng còn lại. |
| **Tag Ngõ ra (`Q`)** | `OutputTag` | `TagModel?` (`BOOL`) | `null` | Biến logic đích nhận mức `1` khi `CV ≤ 0` (ví dụ: đèn báo hết phôi hoặc cờ dừng máy). |
| **Tag Nạp lại (`R`)** | `ResetTag` | `TagModel?` (`BOOL`) | `None` | Biến logic tùy chọn thực hiện lệnh nạp `CV ← PV` khi chuyển lên mức `1`. |

---

## 4. Minh họa Chu trình Đếm Lùi theo Chu kỳ Quét (`PV = 3`)

Bảng dưới đây minh họa chu trình đếm lùi thực tế khi nạp khay 3 phôi và ngắt báo hết khay khi `CV ≤ 0`:

| Sự kiện Chu kỳ Quét | Chân `CD` | Chân `R` | Số lượng Còn lại `CV` | Ngõ ra `Q` (`CV ≤ 0`) | Nhãn Trạng thái trên Canvas |
| :--- | :---: | :---: | :---: | :---: | :--- |
| Công nhân nhấn nút Nạp đầy khay | `0` | `0 → 1` | `3` | `0` | `PV: 3` (`3 / 3`) |
| Nhả nút Nạp khay | `0` | `0` | `3` | `0` | `PV: 3` (`3 / 3`) |
| Cấp phôi lần thứ 1 | `0 → 1` | `0` | `2` | `0` | `PV: 3` (`2 / 3`) |
| Cấp phôi lần thứ 2 | `0 → 1` | `0` | `1` | `0` | `PV: 3` (`1 / 3`) |
| Cấp phôi lần thứ 3 (Hết khay) | `0 → 1` | `0` | `0` | **`1`** | **`Q = ON`** (`0 / 3`) |
| Thay khay mới & nhấn Nạp lại | `0` | `0 → 1` | `3` | `0` | `PV: 3` (`3 / 3`) |

---

## 5. Kiểm tra Hợp lệ, Xử lý Lỗi & Chẩn đoán Trực tuyến

- **Lưu ý Trạng thái Khởi động Ban đầu:** Nếu biến `CvTag` khởi tạo ở giá trị `0` và chưa nhận xung nạp tại chân `R`, biểu thức `CV ≤ 0` sẽ lập tức đúng (`Q = 1` — báo hết phôi an toàn). Hãy cấp xung `R` sau khi thay khay hoặc khai báo giá trị khởi tạo khác `0` cho `CvTag` trong [Danh mục Biến — Tag Catalog](/vi/workspaces/tag-catalog-tab) nếu máy khởi động sẵn cùng khay đầy.
- **Bảo vệ Chống Tràn Dưới (Underflow Clamp):** Toán tử đếm trong firmware khóa cứng giới hạn dưới của `INT32`, đảm bảo các xung `CD` phát sinh thêm khi `CV ≤ 0` không bao giờ làm tràn vòng thanh ghi sang số dương.
- **Giám sát Trực tuyến (`F8`):** Hiển thị trực quan lượng tồn kho **`{CurrentCount} / {PresetValue}`** ngay trên thân khối và bật huy hiệu **`Q = ON`** khi chạm mốc `0`.

---

## 6. Ví dụ Đấu nối Công nghiệp Thực tế

### Khóa Liên động Báo Hết Phôi Khay Cấp liệu CNC (`50` Phôi / Khay)

**Bài toán:** Mỗi khay nhôm chứa `50` phôi gia công. Khi người vận hành thay khay mới và nhấn nút xác nhận `DI2_TrayReloaded`, bộ đếm nạp `50`. Mỗi hành trình xi-lanh đẩy phôi (`DI0_FeederStroke`) giảm số lượng còn lại đi `1`. Khi hết sạch phôi (`CV = 0 → Q = 1`), bật đèn tháp cảnh báo vàng (`DO1_TrayEmptyLamp`) và khóa chu trình tự động qua khối [`GUARD`](/vi/blocks/guard).

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
        <span class="synx-node-tag">DI0 • BOOL</span>
        <span class="synx-node-alias">Hành Trình Feeder</span>
        <span class="synx-node-val">Xung Đẩy Phôi (CD)</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">DEC</span>
  </div>

  <div class="synx-canvas-node is-counter">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M5.5,3 L5.5,13 M10.5,3 L10.5,13 M2,6.5 L14,6.5 M2,10 L14,10" /></svg>
        <span>CTD COUNTER</span>
      </div>
      <span class="synx-node-status is-active">● 0 / 50</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● CD</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">PV = 50 • CV COUNTER1</span>
        <span class="synx-node-alias">Đếm Lùi Phôi Còn Lại</span>
        <span class="synx-node-val">Q = 1 khi CV == 0</span>
      </div>
      <div class="synx-node-pin is-out">Q ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">EMPTY</span>
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
        <span class="synx-node-tag">DO1 = 1</span>
        <span class="synx-node-alias">Đèn Báo Hết Phôi</span>
        <span class="synx-node-val">Bật Đèn Vàng Khay Rỗng</span>
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
        <span class="synx-node-tag">DI2 • BOOL</span>
        <span class="synx-node-alias">Nút Nạp Khay Mới</span>
        <span class="synx-node-val">Xác Nhận Nạp Phôi (LD)</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">LOAD</span>
  </div>

  <div class="synx-canvas-node is-counter">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M5.5,3 L5.5,13 M10.5,3 L10.5,13 M2,6.5 L14,6.5 M2,10 L14,10" /></svg>
        <span>CTD RELOAD</span>
      </div>
      <span class="synx-node-status is-active">● 50 / 50</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● LD</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Đặt Lại COUNTER1 = 50</span>
        <span class="synx-node-alias">Nạp Đầy 50 Phôi</span>
        <span class="synx-node-val">Q Chuyển Về 0</span>
      </div>
      <div class="synx-node-pin is-out">Q ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">CLEAR</span>
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
        <span class="synx-node-tag">DO1 = 0</span>
        <span class="synx-node-alias">Tắt Đèn Báo Hết Phôi</span>
        <span class="synx-node-val">Cho Phép Máy Chạy Tiếp</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

**Các bước cấu hình:**
1. Khai báo `DI0_FeederStroke`, `DI2_TrayReloaded`, `CNT_RemainingBlanks` (`COUNTER1`, giá trị đầu `50`) và `DO1_TrayEmptyLamp` trong [Danh mục Biến — Tag Catalog](/vi/workspaces/tag-catalog-tab).
2. Đặt khối **`CTD`** lên vùng vẽ và cấu hình:
   - `PresetValue` = `50`
   - `CvTag` = `CNT_RemainingBlanks` (`COUNTER1`)
   - `OutputTag` = `DO1_TrayEmptyLamp`
3. Nối `DI0_FeederStroke` vào chân **`CD`** và `DI2_TrayReloaded` vào chân **`R`**.
4. Chạy kiểm thử trên [Mô phỏng Offline](/vi/tools/simulator-and-live-debug) (`F5`): kích xung `DI2_TrayReloaded` để nạp `50 / 50` (`Q = 0`), và kiểm tra đèn báo sáng ngay khi bộ đếm lùi về `0 / 50` (`Q = ON`).
