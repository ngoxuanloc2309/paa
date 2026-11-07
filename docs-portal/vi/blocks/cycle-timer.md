# CYCLE_TIMER — Tạo Xung Chu kỳ Bất đối xứng

<div class="doc-meta-bar">
  <span><strong>Mã khối:</strong> CYCLE_TIMER (CYCLE)</span>
  <span><strong>Nhóm danh mục:</strong> Định thời, Bộ đếm &amp; Chuẩn hóa</span>
  <span><strong>Lớp thực thi:</strong> Bộ Tạo Dao động / Chớp Nháy Chu kỳ Bất đối xứng</span>
  <span><strong>Đích biên dịch:</strong> Động cơ Dao động Đơn điệu Hai Pha</span>
</div>

## 1. Tóm tắt Chức năng & Ngữ nghĩa Thực thi

Khối **`CYCLE_TIMER`** là **bộ phát xung vuông tuần hoàn bất đối xứng (asymmetric flasher / oscillator)** điều khiển theo mức. Chừng nào tín hiệu cho phép ở chân `In` còn giữ mức tích cực (`1`), khối `CYCLE_TIMER` liên tục đảo luân phiên ngõ ra nhị phân `Q` giữa **pha BẬT (`OnTimeMs`)** và **pha NGHỈ (`OffTimeMs`)** hoàn toàn độc lập, với pha khởi đầu tùy chọn.

- **Tỷ lệ Chu kỳ Bất đối xứng Độc lập:** Khác với các bit xung nhịp đối xứng `50%` cố định vốn đòi hỏi ghép thêm bộ đếm và bộ so sánh phức tạp, `CYCLE_TIMER` cho phép phối hợp trực tiếp pha đóng tính bằng mili-giây/giây với pha nghỉ tính bằng phút/giờ ngay trong một khối duy nhất (ví dụ: bơm mỡ bôi trơn `OnTimeMs = 500 ms` lặp lại sau mỗi `OffTimeMs = 15 min`).
- **Tùy chọn Pha Khởi đầu (`StartWithOn`):**
  - **`Bắt đầu bằng ON` (`true`):** Lập tức đóng `Q = 1` trong khoảng `OnTimeMs` ngay ở chu kỳ đầu tiên khi `In` lên `1`, sau đó chuyển sang nghỉ `Q = 0` trong `OffTimeMs` và lặp lại liên tục.
  - **`Bắt đầu bằng OFF` (`false`):** Vào pha nghỉ `Q = 0` hết khoảng `OffTimeMs` trước, sau đó mới phát xung `Q = 1`.
- **Ngắt Dứt khoát Khi Dừng Cho phép:** Ngay khoảnh khắc `In` hạ về `0`, bộ tạo dao động dừng tức thì, xóa bộ đếm pha nội bộ về `0` và đưa ngõ ra `Q` về trạng thái an toàn `0`.
- **Ứng dụng Công nghiệp Điển hình:** Đèn tháp chớp cảnh báo sự cố Andon và còi báo ngắt quãng, bơm mỡ bôi trơn ổ bi tự động định kỳ, phun sương làm mát nhà màng nông nghiệp công nghệ cao và sục rửa ngược màng lọc theo chu kỳ.

---

## 2. Sơ đồ Chân & Hợp đồng Tín hiệu

| Chân (Pin) | Chiều | Loại Tín hiệu | Kiểu Dữ liệu | Mô tả Kỹ thuật |
| :--- | :--- | :--- | :--- | :--- |
| `In` | Ngõ vào (Input) | Sự kiện / Thực thi (`IsEvent = true`) | Cấp Mức Cho phép (`BOOL`) | Cho phép dao động tuần hoàn `ON`/`OFF` chừng nào giữ mức `1`; dừng dao động và ép `Q = 0` ngay lập tức khi về `0`. |
| `Q` | Ngõ ra (Output) | Giá trị / Dữ liệu (`IsEvent = false`) | `BOOL` (`0` / `1`) | Ngõ ra chuỗi xung bất đối xứng gắn với `OutputTag`; nối dây tới [`ACTION`](/vi/blocks/action), [`MQTT_PUB`](/vi/blocks/mqtt-pub), [`HTTP_CLIENT`](/vi/blocks/http-client) hoặc [`TCP_CLIENT`](/vi/blocks/tcp-client). |

---

## 3. Tham số Inspector & Giới hạn Kiểm tra

| Tham số | Kiểu / Tùy chọn | Mặc định | Ràng buộc & Giới hạn | Hành vi Thời gian thực |
| :--- | :--- | :--- | :--- | :--- |
| `OnDurationValueText` + `OnTimeUnit` | Số không âm + Đơn vị (`ms`, `s`, `min`, `h`) | `"1"` `s` (`1000 ms`) | Quy đổi đơn vị độc lập; kẹp `OnTimeMs` trong dải `[1 .. 4,294,967,295] ms`. | Thiết lập độ rộng pha đóng (`Q = 1`) mỗi chu kỳ và hiển thị dòng xác nhận `= 1,000 ms (1s)`. |
| `OffDurationValueText` + `OffTimeUnit` | Số không âm + Đơn vị (`ms`, `s`, `min`, `h`) | `"1"` `s` (`1000 ms`) | Quy đổi đơn vị độc lập; kẹp `OffTimeMs` trong dải `[1 .. 4,294,967,295] ms`. | Thiết lập độ rộng pha nghỉ (`Q = 0`) mỗi chu kỳ và hiển thị dòng xác nhận `= 1,000 ms (1s)`. |
| `StartWithOn` (`StartPhaseIndex`) | `Bắt đầu bằng ON` (`0`) \| `Bắt đầu bằng OFF` (`1`) | `Bắt đầu bằng ON` (`true`) | Bộ chọn 2 chế độ pha đầu. | Quyết định `Q` bật `1` ngay (`OnTimeMs`) hay chờ nghỉ `0` trước (`OffTimeMs`) khi `In` chuyển `0 → 1`. |
| `InputTag` | Tag Số (`DI`, `DO`, `VFLAG`) | Tự động nhận từ dây nối vào | Phải tham chiếu tới Tag số nhị phân. | Tín hiệu duy trì mức cho phép bộ tạo dao động hoạt động khi `!= 0`. |
| `OutputTag` (`Q`) | Tag Số Cho phép Ghi (`DO`, `VFLAG`) | Chưa gán *(bắt buộc chọn)* | Phải là Tag nhị phân cho phép ghi (`DO` hoặc `VFLAG`). | Đảo luân phiên giữa `1` (`OnTimeMs`) và `0` (`OffTimeMs`) khi `In == 1`; ép về `0` khi `In == 0`. |
| `CustomLabel` | Chuỗi văn bản tự do | Rỗng (`"Cycle Timer"`) | Nhãn tùy biến trên sơ đồ. | Ghi đè tiêu đề hiển thị trên thẻ khối đồ họa. |

::: warning Lưu ý Chọn Ngõ ra Bán dẫn Cho Tần số Chớp Cao
Đối với các chu kỳ chớp đèn nhanh (`OnTimeMs` hoặc `OffTimeMs` nhỏ hơn `2,000 ms`), bắt buộc gán `OutputTag` vào **ngõ ra bán dẫn Transistor / MOSFET / SSR** thay vì rơ-le cơ khí để bảo vệ tuổi thọ tiếp điểm.
:::

---

## 4. Quy tắc Dao động Chu kỳ & Pha Hoạt động

Khi tín hiệu kích hoạt `In` ở mức `1`, khối `CYCLE_TIMER` dao động luân phiên giữa 2 pha:
- **Pha Bật (`OnTime`):** Ngõ ra `Q = 1` trong khoảng thời gian `OnTimeMs`.
- **Pha Nghỉ (`OffTime`):** Ngõ ra `Q = 0` trong khoảng thời gian `OffTimeMs`.
- **Lựa chọn Pha Khởi đầu (`StartWithOn`):** Nếu `StartWithOn = true`, chu kỳ bắt đầu ngay bằng Pha Bật (`Q = 1`). Nếu `false`, khối sẽ nghỉ `OffTimeMs` trước khi phát xung bật đầu tiên.
- **Dừng Khẩn cấp Tức thì:** Ngay khi `In` tụt về `0`, ngõ ra `Q` lập tức ngắt về `0` và thời gian đếm pha được xóa an toàn.

---

## 5. Cơ chế Bảo vệ Nhập liệu & Chẩn đoán Trực tuyến

### 5.1 Hai Bộ Quy đổi Đơn vị Độc lập & Chống Nhập sai

- **Tự do Phối hợp Khác Thang Thời gian:** `OnTimeUnit` và `OffTimeUnit` vận hành tách biệt hoàn toàn — kỹ sư dễ dàng đặt `Pha ON = 500 ms` đi kèm `Pha OFF = 10 min` mà không cần quy đổi thủ công `10 phút = 600,000 ms`. Cả hai ô đều hiển thị tường minh số mili-giây thực tế ngay bên dưới (`CalculatedOnMsSummary` và `CalculatedOffMsSummary`).
- **Kẹp Biên Chống Chu kỳ Bằng 0:** Tự động chuẩn hóa dấu phẩy thập phân (`"0,5"` → `"0.5"`), kẹp giá trị `"0"` về mức tối thiểu `1 ms` để ngăn vòng lặp chia không, và loại bỏ mọi ký tự không hợp lệ.

### 5.2 Nhãn Chẩn đoán trong Mô phỏng (`F5`) & Gỡ lỗi Trực tuyến (`F8`)

- **Tóm tắt Tỷ lệ Chu kỳ & Nhãn Chớp Trực tiếp:** Thân khối hiển thị rõ `CYCLE (250ms/1.75s) → DO3` cùng dòng thông số `ON: 250ms | OFF: 1.75s`, trong khi nhãn trạng thái nhấp nháy trực quan giữa `● ON` (xanh lá kích hoạt) và `● OFF` (xám trung tính) cả khi chạy Simulator (`F5`) lẫn Live Debug 20 FPS (`F8`).

---

## 6. Kịch bản Đấu nối Công nghiệp Thực tế

### Bài toán: Đèn Tháp Chớp Cảnh báo Lỗi Máy Bất đối xứng (`Chớp 250 ms` / `Nghỉ 1750 ms`)

Mỗi khi cờ chốt sự cố máy (`VFLAG0 = 1`) kích hoạt, đèn tháp cảnh báo LED đấu tại ngõ ra bán dẫn `DO3` phải phát chuỗi **chớp sáng ngắn hiệu suất cao** (`Sáng 250 ms`, `Tắt 1,750 ms` = chu kỳ `2.0 s`, hệ số lấp đầy `12.5%`) bắt đầu sáng ngay lập tức ở khoảnh khắc phát sinh lỗi, và tắt hẳn ngay khi người vận hành nhấn nút Xóa Lỗi (`DI4`).

### Khai báo Bảng Tag

| Tên Tag | Phân loại | Kiểu Dữ liệu | Mô tả / Tên Bí danh (Alias) |
| :--- | :--- | :--- | :--- |
| `VFLAG0` | `VFLAG` | `BOOL` | `Cờ Chốt Sự cố Máy Đang Tồn tại (1 = Có lỗi)` |
| `DI4` | `DI` | `BOOL` | `Nút nhấn Xác nhận Xóa Lỗi trên Mặt tủ` |
| `DO3` | `DO` | `BOOL` | `Đèn Tháp LED Cảnh báo (Ngõ ra Transistor)` |

### Trình tự Đấu nối trên Canvas

1. **Nhánh 1 (Bộ Dao động Chớp Đèn):** Đặt khối **`INPUT`** (`Tag = VFLAG0`, `CustomLabel = "Cờ Sự cố Máy"`) và nối `INPUT.Out` → **`CYCLE_TIMER`**.
2. Trên bảng **Inspector** của `CYCLE_TIMER`, thiết lập:
   - `OnDurationValueText = "250"`, `OnTimeUnit = "ms"` (`= 250 ms`)
   - `OffDurationValueText = "1750"`, `OffTimeUnit = "ms"` (hoặc `"1.75"` `s`, `= 1,750 ms`)
   - `StartWithOn = Bắt đầu bằng ON`
   - `OutputTag = DO3` (`CustomLabel = "Chớp Đèn Cảnh báo"`)
3. **Nhánh 2 (Nút nhấn Xóa Lỗi):** Đặt khối **`INPUT`** (`Tag = DI4`) → **[`TRIGGER`](/vi/blocks/trigger)** (`ON_RISE`, `ForMs = 50` ms) → **[`ACTION`](/vi/blocks/action)** (`SET_TAG`, `TargetTag = VFLAG0`, `ActionParam = 0`).
4. Nhấn `F5` chạy mô phỏng: gạt `VFLAG0` lên `1`, quan sát `DO3` lập tức chớp sáng `● ON` (`250 ms`) và nghỉ `● OFF` (`1,750 ms`) tuần hoàn; bấm nút `DI4` xóa `VFLAG0 = 0`, xác nhận đèn `DO3` tắt dứt khoát về `0`.

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
        <span class="synx-node-tag">VFLAG0 • BOOL</span>
        <span class="synx-node-alias">Cờ Chốt Sự Cố</span>
        <span class="synx-node-val">Phát Hiện Lỗi Máy</span>
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
        <span>CYCLE_TIMER</span>
      </div>
      <span class="synx-node-status is-active">● 250 / 1750 ms</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">ON 250ms / OFF 1750ms</span>
        <span class="synx-node-alias">Chớp Đèn Còi DO3</span>
        <span class="synx-node-val">Tuần Hoàn Cảnh Báo</span>
      </div>
      <div class="synx-node-pin is-out">Q ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">STROBE</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● FLASH</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">DO3 = 1</span>
        <span class="synx-node-alias">Đèn Tháp Cảnh Báo</span>
        <span class="synx-node-val">Chớp Vàng Báo Động</span>
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
        <span class="synx-node-tag">DI4 • BOOL</span>
        <span class="synx-node-alias">Nút Nhấn Xóa Lỗi</span>
        <span class="synx-node-val">Xác Nhận Người Vận Hành</span>
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
        <span class="synx-node-tag">50 ms Dwell</span>
        <span class="synx-node-alias">Lọc Sườn Lên</span>
        <span class="synx-node-val">Bắt Xung Nhấn Reset</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">RESET</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-pass">● CLEAR</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">VFLAG0 = 0</span>
        <span class="synx-node-alias">Xóa Cờ Sự Cố</span>
        <span class="synx-node-val">Tắt Chớp Đèn Còi DO3</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>
