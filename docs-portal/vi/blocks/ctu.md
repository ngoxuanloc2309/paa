# CTU — Bộ Đếm Tiến (Count Up Counter)

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-BLK-CTU</span>
  <span><strong>Nhóm khối hàm:</strong> Định thời, Bộ đếm &amp; Chuẩn hóa</span>
  <span><strong>Phân lớp thực thi:</strong> Bộ tích lũy trạng thái kích hoạt theo sườn xung</span>
  <span><strong>Tiêu chuẩn:</strong> Mô hình Bộ đếm IEC 61131-3</span>
</div>

## 1. Tổng quan & Ngữ nghĩa Thực thi

Khối hàm **`CTU`** (**Count Up Counter — Bộ Đếm Tiến**) là bộ tích lũy sự kiện tất định kích hoạt theo sườn lên, chuyên dùng cho các bài toán đếm sản phẩm trên băng tải, định lượng mẻ đóng gói, tổng hợp số lượng ca máy và theo dõi chu kỳ bảo trì cơ khí.

Trong mỗi chu kỳ quét tất định của bộ thực thi thời gian thực SynaptiX:
1. **Kiểm tra Ưu tiên Xóa (`R` / `ResetTag`):** Nếu tín hiệu xóa chuyển sang mức tích cực (hoặc đang giữ ở mức `1`), thanh ghi **Giá trị Hiện tại (`CV`)** lập tức được đưa về `0`, đồng thời ngõ ra **`Q`** hạ xuống `0` (`FALSE`).
2. **Phát hiện Sườn lên (`CU`):** Khi chân xóa không tích cực và tín hiệu tại chân **`CU`** chuyển trạng thái từ `0 → 1` (sườn lên), giá trị `CV` tăng thêm đúng `+1` (`CV ← CV + 1`). Nếu chân `CU` giữ nguyên mức `1` qua các chu kỳ quét kế tiếp, bộ đếm **không** tăng thêm cho tới khi `CU` trở về `0` và tạo sườn lên mới.
3. **So sánh Ngưỡng Đặt (`CV ≥ PV`):** Ngõ ra **`Q`** xuất mức `1` (`TRUE`) bất cứ khi nào `CV ≥ PresetValue` (`PV`), và giữ mức `0` (`FALSE`) khi `CV < PresetValue`.
4. **Đồng bộ Vùng nhớ Tag:** Giá trị `CV` được ánh xạ trực tiếp vào biến **`CvTag`** (`COUNTER0..COUNTER7` hoặc `VREG`), còn trạng thái `Q` được ghi ra **`OutputTag`**.

::: info Hành vi Đếm Sau Khi Đạt Ngưỡng PV
Nếu chân `CU` tiếp tục nhận thêm xung sườn lên sau khi `CV` đã chạm mốc `PV` (khi `R` vẫn bằng `0`), thanh ghi `CV` tiếp tục đếm tăng an toàn tới giới hạn trần số nguyên 32-bit có dấu (`+2,147,483,647`, chống tràn vòng số âm), trong khi ngõ ra `Q` duy trì mức `1` cho đến khi có lệnh Reset.
:::

---

## 2. Sơ đồ Chân & Hợp đồng Tín hiệu

Khối `CTU` cung cấp 2 chân sự kiện ngõ vào cố định ở cạnh trái và 1 chân trạng thái logic ở cạnh phải:

| Tên chân | Chiều | Bản chất Tín hiệu | Kiểu dữ liệu | Điều kiện Hiển thị | Hợp đồng Chức năng |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **`CU`** | Ngõ vào | Sự kiện (`0 → 1`) | `BOOL` | Luôn hiển thị | **Xung Đếm Tiến (Count Up):** Tăng `CV` thêm `+1` tại mỗi sườn lên. Có thể nối dây từ khối phía trước trên Canvas hoặc gán trực tiếp qua `InputTag`. |
| **`R`** | Ngõ vào | Sự kiện / Mức | `BOOL` | Luôn hiển thị | **Chân Xóa (Reset):** Đưa `CV → 0` và hạ `Q → 0`. Luôn có quyền ưu tiên cao hơn chân `CU` nếu cả hai cùng tích cực trong một chu kỳ quét. |
| **`Q`** | Ngõ ra | Giá trị logic | `BOOL` | Luôn hiển thị | **Cờ Đạt Định mức:** Xuất mức `1` (`TRUE`) khi `CV ≥ PV`; giữ mức `0` (`FALSE`) khi `CV < PV`. |

---

## 3. Tham số Cấu hình & Ràng buộc Inspector

Khi nhấp chọn khối `CTU` trên [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab), thanh **Inspector** bên phải hiển thị các thuộc tính cấu hình sau:

| Tham số | Khóa Thuộc tính | Kiểu & Miền giá trị | Mặc định | Mô tả Kỹ thuật & Ràng buộc Vật lý |
| :--- | :--- | :--- | :---: | :--- |
| **Nhãn Tùy chỉnh** | `CustomLabel` | `String` | `"CTU Counter"` | Tiêu đề định danh hiển thị trên thẻ khối ngoài vùng vẽ Canvas. |
| **Chế độ Bộ đếm** | `CounterMode` | `"CTU"` \| `"CTD"` | `"CTU"` | Chọn chế độ đếm tiến (`CU`, `CV ≥ PV`). Nếu chuyển sang `"CTD"`, chân `CU` tự động đổi thành `CD`. |
| **Giá trị Đặt trước (`PV`)** | `PresetValue` | `INT32` (`≥ 1`) | `10` | Số lượng xung mục tiêu để kích hoạt `Q = 1`. Áp dụng cơ chế Chống nhập sai kiểu (tự động hoàn nguyên hoặc chuẩn hóa nếu gõ số thập phân). |
| **Tag Nguồn Đếm** | `InputTag` | `TagModel?` (`BOOL`) | `null` | Gán trực tiếp biến ngõ vào số (`DI0..DI7`, `VBIT`) làm nguồn xung `CU` khi không nối dây từ khối khác. |
| **Tag Giá trị Hiện tại (`CV`)** | `CvTag` | `TagModel?` (`INT32`) | `COUNTER0` | Thanh ghi bộ đếm phần cứng (`COUNTER0..COUNTER7`) hoặc thanh ghi ảo (`VREG`) lưu giá trị đếm tức thời. |
| **Tag Ngõ ra (`Q`)** | `OutputTag` | `TagModel?` (`BOOL`) | `null` | Biến đích (`DO`, `RELAY`, `VBIT`) nhận kết quả so sánh `CV ≥ PV` ở mỗi chu kỳ quét. |
| **Tag Xóa (`R`)** | `ResetTag` | `TagModel?` (`BOOL`) | `None` | Biến tùy chọn dùng để xóa `CV → 0` khi lên mức `1`, bổ trợ hoặc thay thế cho dây nối vào chân `R`. |

::: warning Hợp đồng Vùng nhớ Số nguyên trên `PresetValue` và `CvTag`
Theo chuẩn [Kiến trúc Bộ nhớ Tag & Kiểu Dữ liệu](/vi/concepts/data-model-and-types), số xung đếm là đại lượng vật lý rời rạc (`INT32`). Trường `CvTag` chỉ chấp nhận thanh ghi số nguyên (`COUNTER0..7` hoặc `VREG` kiểu `INT32`), và ô `PresetValue` sẽ tự động từ chối chuỗi ký tự sai định dạng ngay khi nhấn `Enter` hoặc rời ô nhập (`LostFocus`).
:::

---

## 4. Minh họa Chu trình Đếm theo Chu kỳ Quét (`PV = 3`)

Bảng dưới đây minh họa chu trình đếm thực tế khi đếm 3 sản phẩm và nhận xung xóa `R` từ cơ cấu chấp hành:

| Sự kiện Chu kỳ Quét | Chân `CU` | Chân `R` | Thanh ghi `CV` | Ngõ ra `Q` (`CV ≥ 3`) | Nhãn Trạng thái trên Canvas |
| :--- | :---: | :---: | :---: | :---: | :--- |
| Khởi động ban đầu | `0` | `0` | `0` | `0` | `PV: 3` (`0 / 3`) |
| Chai thứ 1 đi qua cảm biến | `0 → 1` | `0` | `1` | `0` | `PV: 3` (`1 / 3`) |
| Chai thứ 1 vẫn che tia quang | `1` | `0` | `1` *(không có sườn)* | `0` | `PV: 3` (`1 / 3`) |
| Chai thứ 2 đi qua cảm biến | `0 → 1` | `0` | `2` | `0` | `PV: 3` (`2 / 3`) |
| Chai thứ 3 đi qua cảm biến | `0 → 1` | `0` | `3` | **`1`** | **`Q = ON`** (`3 / 3`) |
| Cơ cấu đẩy thùng gửi xung xóa | `0` | `0 → 1` | `0` | `0` | `PV: 3` (`0 / 3`) |

---

## 5. Kiểm tra Hợp lệ, Xử lý Lỗi & Chẩn đoán Trực tuyến

- **Kiểm tra Tiền biên dịch (`F7`):**
  - Xác thực điều kiện `PresetValue > 0`. Nếu đặt `PresetValue ≤ 0`, trình biên dịch phát cảnh báo vì ngõ ra `Q` sẽ lập tức lên mức `TRUE` ngay từ lúc `CV = 0`.
  - Kiểm tra biến `CvTag` đã được gán vào thanh ghi `COUNTER` hoặc `VREG` hợp lệ và không bị xung đột ghi trùng từ một khối bộ đếm khác.
- **Mô phỏng Offline (`F5`) & Giám sát Trực tuyến (`F8`):**
  - Thân khối hiển thị trực tiếp tỷ lệ đếm thời gian thực **`{CurrentCount} / {PresetValue}`** (ví dụ: `7 / 10`).
  - Huy hiệu trạng thái chuyển từ màu xám trung tính **`PV: 10`** sang xanh ngọc **`Q = ON`** ngay khoảnh khắc `CV ≥ PV`.
  - Nếu mất kết nối truyền thông khi đang giám sát trực tuyến, `LiveVisualState` chuyển sang trạng thái `Stale` để bảo toàn sự thật thị giác thay vì hiển thị số liệu giả.

---

## 6. Ví dụ Đấu nối Công nghiệp Thực tế

### Trạm Đóng Thùng Tự động (24 Chai / Thùng Carton)

**Bài toán:** Đếm số lượng chai đi qua cảm biến quang (`DI0`) trên băng tải chiết rót. Ngay khi đủ `24` chai vào thùng, kích hoạt van điện từ gạt thùng (`DO0`) và đẩy bản tin hoàn tất mẻ sản xuất lên Broker qua khối [`MQTT_PUB`](/vi/blocks/mqtt-pub), sau đó xóa bộ đếm về `0` khi công tắc hành trình (`DI1`) xác nhận thùng mới đã vào vị trí.

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
        <span class="synx-node-alias">Cảm Biến Quang</span>
        <span class="synx-node-val">Đếm Chai Vào (CU)</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">INC</span>
  </div>

  <div class="synx-canvas-node is-counter">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M5.5,3 L5.5,13 M10.5,3 L10.5,13 M2,6.5 L14,6.5 M2,10 L14,10" /></svg>
        <span>CTU COUNTER</span>
      </div>
      <span class="synx-node-status is-active">● 24 / 24</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● CU</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">PV = 24 • CV COUNTER0</span>
        <span class="synx-node-alias">Đếm Đủ 24 Chai/Thùng</span>
        <span class="synx-node-val">Q = 1 khi CV &gt;= 24</span>
      </div>
      <div class="synx-node-pin is-out">Q ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">FULL</span>
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
        <span class="synx-node-tag">DO0 = 1</span>
        <span class="synx-node-alias">Van Gạt Thùng Đầy</span>
        <span class="synx-node-val">Đẩy Thùng Ra Băng Tải</span>
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
        <span class="synx-node-tag">DI1 • BOOL</span>
        <span class="synx-node-alias">Công Tắc Thùng Mới</span>
        <span class="synx-node-val">Vào Vị Trí (R)</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">RESET</span>
  </div>

  <div class="synx-canvas-node is-counter">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M5.5,3 L5.5,13 M10.5,3 L10.5,13 M2,6.5 L14,6.5 M2,10 L14,10" /></svg>
        <span>CTU RESET</span>
      </div>
      <span class="synx-node-status is-pass">● 0 / 24</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● R</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Reset Về 0</span>
        <span class="synx-node-alias">COUNTER0 = 0</span>
        <span class="synx-node-val">Sẵn Sàng Thùng Mới</span>
      </div>
      <div class="synx-node-pin is-out">Q ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">RETRACT</span>
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
        <span class="synx-node-alias">Thu Van Gạt Thùng</span>
        <span class="synx-node-val">Đóng Chặn Thùng Mới</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

**Các bước cấu hình:**
1. Mở [Danh mục Biến — Tag Catalog](/vi/workspaces/tag-catalog-tab) (`Ctrl+3`), đặt tên `DI0` là `DI0_PhotoSensor`, `DI1` là `DI1_CaseCleared`, `COUNTER0` là `CNT_CaseBottles` và `DO0` là `DO0_Diverter`.
2. Kéo khối **`CTU`** ra vùng vẽ Canvas và thiết lập trong Inspector:
   - `PresetValue` = `24`
   - `CvTag` = `CNT_CaseBottles` (`COUNTER0`)
   - `OutputTag` = `DO0_Diverter` (`DO0`)
3. Nối dây từ khối `DI0_PhotoSensor` vào chân **`CU`** và từ `DI1_CaseCleared` vào chân **`R`**.
4. Nối chân **`Q`** sang chân `Trig` của khối [`MQTT_PUB`](/vi/blocks/mqtt-pub) (`PublishMode = Event`) để báo cáo tức thời mỗi thùng hàng hoàn thành lên hệ thống quản lý sản xuất.
