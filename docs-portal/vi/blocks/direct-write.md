# DIRECT_WRITE — Xuất Trực tiếp Cọc Phần cứng (DO / PWM / PTO / Bus)

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-BLK-DIRWRITE</span>
  <span><strong>Nhóm khối hàm:</strong> Truyền thông Fieldbus &amp; Xuất Phần cứng</span>
  <span><strong>Phân lớp thực thi:</strong> Trình điều khiển ngoại vi Timer / GPIO phần cứng tất định</span>
  <span><strong>Tiêu chuẩn:</strong> Cơ cấu Chấp hành Số, PWM &amp; Phát xung PTO theo IEC 61131-3</span>
</div>

## 1. Tổng quan & Ngữ nghĩa Thực thi

Khối hàm **`DIRECT_WRITE`** (**Bộ Xuất Trực tiếp Cọc Phần cứng / Phát xung**) liên kết trực tiếp tới một chân ngõ ra vật lý duy nhất (**`DO0 .. DO7`**) trên bộ điều khiển biên SynaptiX, khai mở toàn bộ sức mạnh của ngoại vi bộ định thời phần cứng (Hardware Timer / DMA) vượt xa kiểu đóng cắt coil thông thường.

Mỗi khối `DIRECT_WRITE` hỗ trợ **4 chế độ điều khiển phần cứng chuyên sâu**:
1. **Ngõ ra Số (`DigitalOutput` — DO `0/1`):** Đóng cắt rơ-le `24 VDC`, van điện từ, khởi động từ và đèn báo với tùy chọn đảo cực tính `IsActiveHigh` cùng trạng thái an toàn `FailSafeState` khi dừng máy hoặc gặp sự cố.
2. **Điều chế Độ rộng Xung (`PwmDuty` — `0..100%`):** Phát xung PWM phần cứng ở tần số `PwmFrequencyHz` kèm khóa biên `[DutyMin .. DutyMax]` cho van tuyến tính tỷ lệ, điều tốc động cơ DC, thanh sấy nhiệt và điều chỉnh độ sáng LED.
3. **Phát Chuỗi Xung Bước (`PulseTrain` — PTO):** Xuất chính xác số lượng xung bước (`Pulses`) ở tần số `Freq` Hz (được bảo vệ bởi trần `MaxFrequencyHz` và độ rộng xung `PulseWidthUs`) cho bộ điều khiển động cơ bước Stepper và Servo (`STEP/DIR`).
4. **Bus Chấp hành Định địa chỉ (`AddressableBus`):** Điều khiển dải đèn LED thông minh hoặc cụm cơ cấu chấp hành nối tiếp 1 dây (`1..ChannelCount`) với chế độ độc quyền kênh `IsExclusiveChannel` và mã màu mặc định `DefaultColorRgb`.

---

## 2. Sơ đồ Chân & Hợp đồng Tín hiệu

Khối `DIRECT_WRITE` tự động tái cấu hình các chân ngõ vào bên trái (`SynchronizeConnectors`) tùy theo tổ hợp **`Mode` × `TriggerMode`**, đảm bảo kỹ sư chỉ nhìn thấy đúng các chân có ý nghĩa vật lý ở chế độ đang chọn:

### 2.1. Ma trận Chân Ngõ vào Động theo Chế độ

| Chế độ Hoạt động (`Mode`) | Khi `Continuous` hoặc `OnChangeOfValue` (COV) | Khi `OnTriggerRisingEdge` | Ngữ nghĩa Tín hiệu Chân Vào |
| :--- | :---: | :---: | :--- |
| **1. `DigitalOutput`** | `[In]`, `[En]` | `[Trig]`, `[In]`, `[En]` | `In` (`BOOL`): mức xuất (`0`/`1`) · `En` (`BOOL`): cổng cho phép xuất · `Trig`: sườn lên chốt xuất. |
| **2. `PwmDuty`** | `[Duty]` | `[Trig]`, `[Duty]` | `Duty` (`INT32`/`REAL`, `0..100%`): độ rộng xung mục tiêu, tự động kẹp trong `[DutyMin..DutyMax]`. *(Ẩn chân `Param`)*. |
| **3. `PulseTrain` (PTO)** | `[Pulses]`, `[Freq]` | `[Trig]`, `[Pulses]`, `[Freq]` | `Pulses` (`INT32`): tổng số xung bước cần phát · `Freq` (`INT32` Hz): tần số phát xung, giới hạn bởi `MaxFrequencyHz`. |
| **4. `AddressableBus`** | `[Chan]`, `[Param]` | `[Chan]`, `[Param]` | `Chan` (`1..ChannelCount`): địa chỉ mắt/kênh · `Param` (`UINT32` mã màu RGB / tham số). *(Ẩn chân `Trig`)*. |

### 2.2. Nhóm Chân Ngõ ra Cố định

| Tên chân | Chiều | Bản chất Tín hiệu | Kiểu dữ liệu | Điều kiện Hiển thị | Hợp đồng Chức năng |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **`Done`** | Ngõ ra | Sự kiện (Xung) | `BOOL` | Luôn hiển thị | Phát xung mức `1` khi hoàn tất lệnh xuất theo sườn kích hoạt hoặc ngay khi phát đủ chuỗi xung PTO yêu cầu. |
| **`Err`** | Ngõ ra | Giá trị | `BOOL` | Luôn hiển thị | Bật mức `1` nếu chân `TargetPin` vượt quá số cổng DO vật lý của thiết bị hoặc xảy ra xung đột ngoại vi. |

---

## 3. Tham số Cấu hình & Ràng buộc Inspector

Nhấp nút **`[ Cấu hình... ]`** (hoặc nhấp đúp vào khối) để mở **Hộp thoại Cấu hình Xuất Phần cứng Trực tiếp**:

### 3.1. Nhóm Tham số Cọc Vật lý & Kích hoạt

| Tham số | Khóa Thuộc tính | Kiểu & Miền giá trị | Mặc định | Mô tả Kỹ thuật & Bảo vệ Phần cứng |
| :--- | :--- | :--- | :---: | :--- |
| **Cọc Vật lý Đích** | `TargetPin` | `DO0 .. DO7` | `DO0` | Chân ngõ ra phần cứng do khối trực tiếp điều khiển. Tự động đối chiếu với số cổng DO của [Cấu hình Thiết bị](/vi/concepts/ecosystem-overview) hiện hành. |
| **Chế độ Vận hành** | `Mode` | `DigitalOutput`, `PwmDuty`, `PulseTrain`, `AddressableBus` | `DigitalOutput` | Chọn chức năng ngoại vi GPIO / Hardware Timer cho chân `TargetPin`. |
| **Chế độ Kích hoạt** | `TriggerMode` | `Continuous`, `OnTriggerRisingEdge`, `OnChangeOfValue` | `OnTriggerRisingEdge` | `Continuous`: cập nhật mỗi chu kỳ quét · `OnTriggerRisingEdge`: xuất tại sườn lên chân `Trig` · `OnChangeOfValue`: chỉ xuất khi `Δ ≥ Deadband`. |
| **Ngưỡng Chết COV** | `Deadband` | `Float` (`≥ 0.0`) | `0.5` | Độ thay đổi tối thiểu để cập nhật phần cứng ở chế độ `OnChangeOfValue` (chỉ hiển thị khi `IsCovMode = true`). |

### 3.2. Tham số Chuyên sâu theo Từng Chế độ

| Chế độ | Tham số | Khóa Thuộc tính | Mặc định | Ý nghĩa Vật lý & Ràng buộc |
| :--- | :--- | :--- | :---: | :--- |
| **1. `DigitalOutput`** | **Cực tính Tác động** | `IsActiveHigh` | `true` | `true`: Mức cao tích cực (`1 = 24V ON, 0 = 0V OFF`) · `false`: Mức thấp tích cực (`0 = 24V ON, 1 = 0V OFF`). |
| **1. `DigitalOutput`** | **Trạng thái Fail-Safe** | `FailSafeState` | `0` | `0`: Ngắt điện an toàn (`OFF`) khi dừng/lỗi · `1`: Đóng điện an toàn (`ON`) khi dừng/lỗi (ví dụ van nước giải nhiệt thường mở điện). |
| **2. `PwmDuty`** | **Tần số Sóng mang PWM** | `PwmFrequencyHz` | `1000` Hz | Tần số băm xung tính bằng Hz (bắt buộc dùng trên ngõ ra bán dẫn Transistor, không dùng rơ-le cơ). |
| **2. `PwmDuty`** | **Giới hạn Độ rộng Xung** | `DutyMin` .. `DutyMax` | `0` .. `100` % | Ngưỡng kẹp phần trăm công suất tối thiểu và tối đa (`0%..100%`). |
| **2. `PwmDuty`** | **Độ rộng Xung Khởi tạo** | `InitialDuty` | `0` % | Giá trị phần trăm xung xuất sẵn ngay khi vừa cấp nguồn khởi động. |
| **3. `PulseTrain`** | **Độ rộng Xung cao** | `PulseWidthUs` | `50` µs | Thời gian duy trì mức cao của mỗi xung tính bằng micro-giây (phù hợp opto driver Stepper/Servo từ `5..100 µs`). |
| **3. `PulseTrain`** | **Tần số Xung Tối đa** | `MaxFrequencyHz` | `20000` Hz | Trần tốc độ phát xung an toàn (`Hz`) chống mất bước cơ khí do quá tốc. |
| **4. `AddressableBus`** | **Số lượng Kênh / Điểm** | `ChannelCount` | `18` | Tổng số mắt LED / điểm chấp hành mắc nối tiếp trên đường dây. |
| **4. `AddressableBus`** | **Độc quyền Kênh** | `IsExclusiveChannel` | `true` | `true`: Chỉ sáng 1 điểm `Chan` đang chọn (One-Hot) · `false`: Điều khiển độc lập nhiều điểm cùng lúc. |
| **4. `AddressableBus`** | **Màu Mặc định RGB** | `DefaultColorRgb` | `0x00FF00` | Mã màu 24-bit RGB dự phòng (`#00FF00` xanh lá) khi `Param` bằng `0`. |

::: danger Cảnh báo Hỏng Tiếp điểm Rơ-le Cơ khí khi Băm xung PWM / PTO
Tuyệt đối không cấu hình chế độ `PwmDuty` hoặc `PulseTrain` tần số cao lên các cọc ngõ ra dùng rơ-le cơ khí (Electromechanical Relay) — đóng cắt hàng nghìn lần mỗi giây sẽ làm dính cháy tiếp điểm chỉ sau vài phút. Luôn chọn cọc ngõ ra bán dẫn Transistor tốc độ cao theo tài liệu phần cứng thiết bị.
:::

---

## 4. Cơ chế Điều khiển Ngõ ra Trực tiếp

Khối `DIRECT_WRITE` can thiệp trực tiếp vào ngoại vi phần cứng theo chế độ cấu hình:
- **Ngõ ra Số (`DigitalOutput`):** Đóng/cắt rơ-le hoặc cọc transistor `DO0..DO7` với cực tính tùy chọn (`Active-High` hoặc `Active-Low`) và trạng thái an toàn Fail-Safe khi dừng máy.
- **Băm xung PWM (`PwmDuty`):** Xuất xung điều tốc với tần số sóng mang `PwmFrequencyHz` và độ rộng xung kẹp trong dải an toàn `[DutyMin .. DutyMax]%`.
- **Phát xung bước PTO (`PulseTrain`):** Điều khiển động cơ bước/servo qua chuỗi xung tần số cao giới hạn an toàn dưới `MaxFrequencyHz`.
- **Bus định địa chỉ (`AddressableBus`):** Điều khiển cụm đèn chỉ thị hoặc dàn cơ cấu chấp hành nối tiếp theo chế độ chọn kênh.

---

## 5. Kiểm tra Hợp lệ, Xử lý Lỗi & Chẩn đoán Trực tuyến

- **Cảnh báo Vượt Số Cổng Phần cứng `(!)`:** Nếu chuyển đổi dự án từ dòng thiết bị 8 ngõ ra xuống dòng thiết bị 4 ngõ ra (`DO0..DO3`), mọi khối `DIRECT_WRITE` đang trỏ tới `DO4..DO7` lập tức bật cờ `IsHardwarePinOutOfRange = true`, hiển thị cảnh báo **`PIN: DO4 (!)`** ngay trên tiêu đề khối, đổi huy hiệu sang **`LỖI: VƯỢT QUÁ DO`** và khóa nạp an toàn tại màn hình [Triển khai — Deploy](/vi/workspaces/deploy-tab).
- **Kiểm tra Xung đột Trùng Chân Cứng:** Trình biên dịch tự động phát hiện và ngăn chặn trường hợp hai khối `DIRECT_WRITE` cùng chiếm dụng một chân vật lý `DOx` với các chế độ xung đột nhau.
- **Khung Giả lập Phân tích Tín hiệu:** Hộp thoại cấu hình cung cấp sẵn khu vực thử nghiệm trực quan (`SimDoValue`, `SimPwmDuty`, `SimPtoPulses`/`SimPtoFreq`, `SimBusChannel`) để kiểm tra thông số xung xuất ra trước khi nạp xuống mạch.

---

## 6. Ví dụ Đấu nối Công nghiệp Thực tế

### Điều tốc Tuyến tính Quạt Giải nhiệt Tủ điện bằng PWM (`SCALE` → `DIRECT_WRITE` trên `DO0`)

**Bài toán:** Chuyển đổi nhiệt độ trong tủ điều khiển (`25.0 °C .. 65.0 °C`) qua khối [`SCALE`](/vi/blocks/scale) thành độ rộng xung `20% .. 100%` tần số `1000 Hz` tại ngõ ra bán dẫn `DO0`, chỉ cập nhật phần cứng khi phần trăm thay đổi từ `1.0%` trở lên (`OnChangeOfValue`).

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>INPUT</span>
      </div>
      <span class="synx-node-status is-live">● AI1</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Cảm Biến NTC</span>
        <span class="synx-node-alias">Nhiệt Độ Tủ Điện</span>
        <span class="synx-node-val">AI1 • INT32</span>
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
      <span class="synx-node-status is-active">● 25..65°C</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Quy Đổi Duty %</span>
        <span class="synx-node-alias">Độ Rộng Xung Quạt</span>
        <span class="synx-node-val">20.0 .. 100.0 %</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Duty %</span>
  </div>

  <div class="synx-canvas-node is-direct">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>DIRECT_WRITE</span>
      </div>
      <span class="synx-node-status is-active">● 1000Hz PWM</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● Duty</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Ngõ Ra Bán Dẫn</span>
        <span class="synx-node-alias">PWM Quạt Tủ</span>
        <span class="synx-node-val">DO0 • PwmDuty</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

**Các bước cấu hình:**
1. Cấu hình khối [`SCALE`](/vi/blocks/scale) bằng Hiệu chuẩn 2 Điểm (`X1 = 250, Y1 = 20`; `X2 = 650, Y2 = 100`, `IsClamped = true`) để ngõ ra tỷ lệ tuyến tính trong dải `20.0 .. 100.0 %`.
2. Đặt khối **`DIRECT_WRITE`** lên vùng vẽ và thiết lập trong hộp thoại cấu hình:
   - `TargetPin = DO0`
   - `Mode = PwmDuty`
   - `PwmFrequencyHz = 1000`, `DutyMin = 20`, `DutyMax = 100`, `InitialDuty = 25`
   - `TriggerMode = OnChangeOfValue`, `Deadband = 1.0`
3. Lúc này cạnh trái khối tự động thu gọn chỉ còn duy nhất 1 chân **`Duty`**; nối trực tiếp chân `OUT` của `SCALE` vào chân **`Duty`**.
