# ACTION — Khối Thực thi Lệnh Đích

<div class="doc-meta-bar">
  <span><strong>Mã khối:</strong> ACTION</span>
  <span><strong>Nhóm danh mục:</strong> Chuỗi Luật Cốt lõi (Core Rule Pipeline)</span>
  <span><strong>Lớp thực thi:</strong> Khối Đích Thực thi Cuối nhánh (Sink Node)</span>
  <span><strong>Đích biên dịch:</strong> Giai đoạn 4 của Luật (Deterministic Output Commit)</span>
</div>

## 1. Tóm tắt Chức năng & Ngữ nghĩa Thực thi

Khối **`ACTION`** là điểm kết thúc thực thi của một nhánh trong Chuỗi Luật Cốt lõi. Mỗi khi có xung sự kiện đi tới chân `In` — sau khi đã vượt qua các khâu kiểm tra của [`TRIGGER`](/vi/blocks/trigger), [`COMPARATOR`](/vi/blocks/comparator), [`GUARD`](/vi/blocks/guard), [`TON`](/vi/blocks/ton), [`CTU`](/vi/blocks/ctu) hoặc chân báo hoàn tất của các khối truyền thông — khối `ACTION` thực thi nguyên tử thao tác ghi lên `TargetTag` hoặc hệ thống cảnh báo/nhật ký của bộ điều khiển.

- **Cập nhật Vùng nhớ Đồng bộ Chu kỳ Quét:** Các thao tác biến đổi giá trị Tag (`SET_TAG`, `TOGGLE_TAG`, `INC_COUNTER`, `SCALE_TAG`) được chốt tất định trong pha xuất ngõ ra của chu kỳ quét hiện hành, đảm bảo thời gian đáp ứng cơ cấu chấp hành hoàn toàn nhất quán.
- **Khóa Chặt Tham số theo Kiểu Tag Đích:** Ngay khi chọn `TargetTag`, ô tham số `ActionParam` trên Inspector tự động chuyển đổi giao diện tương ứng — hiển thị cặp nút nhị phân `[ 0 ]` / `[ 1 ]` đối với cuộn hút/cờ `BOOL`, bộ kiểm tra số nguyên cho thanh ghi `INT32`, và định dạng dấu phẩy động IEEE-754 (`0.0###`) cho thanh ghi `REAL`.
- **Ứng dụng Công nghiệp Điển hình:** Đóng/cắt ngõ ra rơ-le hoặc transistor (`DO`), chốt/xóa cờ trạng thái nội bộ (`VFLAG`), đảo trạng thái bật/tắt bằng một nút nhấn đơn (`TOGGLE_TAG`), cộng dồn bộ đếm sản lượng (`INC_COUNTER`), kích hoạt cảnh báo vận hành (`SEND_ALARM`) và lưu vết nhật ký sự kiện (`LOG_EVENT`).

---

## 2. Sơ đồ Chân & Hợp đồng Tín hiệu

Khối `ACTION` đóng vai trò điểm kết thúc nhánh luật nên chỉ có một chân sự kiện ngõ vào và không có chân ngõ ra.

| Chân (Pin) | Chiều | Loại Tín hiệu | Kiểu Dữ liệu | Mô tả Kỹ thuật |
| :--- | :--- | :--- | :--- | :--- |
| `In` | Ngõ vào (Input) | Sự kiện / Thực thi (`IsEvent = true`) | Xung Sự kiện (Event Pulse) | Kích hoạt thực thi tức thì lệnh `ActionType` đã cấu hình tại mỗi xung sự kiện đi vào hoặc khi ngõ ra `Q` của Timer/Counter chuyển mức tích cực. |

---

## 3. Tham số Inspector & Giới hạn Kiểm tra

| Tham số | Kiểu / Tùy chọn | Mặc định | Ràng buộc & Giới hạn | Hành vi Thời gian thực |
| :--- | :--- | :--- | :--- | :--- |
| `ActionType` | `SET_TAG`, `TOGGLE_TAG`, `INC_COUNTER`, `WRITE_REMOTE`, `SEND_ALARM`, `LOG_EVENT`, `SCALE_TAG` | `SET_TAG` | Chế độ `TOGGLE_TAG` yêu cầu Tag đích kiểu `BOOL`; chế độ `INC_COUNTER` yêu cầu Tag đích kiểu số nguyên/bộ đếm. | Chọn phương thức tác động lên vùng nhớ hoặc hệ thống sự kiện khi chân `In` kích hoạt (xem ma trận 7 chế độ bên dưới). |
| `TargetTag` | Bộ chọn Tag Cho phép Ghi (`DO`, `VFLAG`, `VREG`, `VREG_R`, `COUNTER`, `MB_COIL`, `MB_HOLDING`) | Chưa gán *(bắt buộc chọn)* | Không cho phép ghi đè lên các ngõ vào vật lý chỉ đọc (`DI`, `AI`). Tự động gỡ `TargetTag` nếu đổi sang `ActionType` không tương thích kiểu. | Biến đích trong ảnh tiến trình hoặc Tag đối tượng gắn với bản tin cảnh báo/nhật ký. |
| `ActionParam` | Giá trị Khóa theo Kiểu (`[ 0 ]` `[ 1 ]` với `BOOL`; ô số `INT32` hoặc `REAL`) | `1` (`"1"` hoặc `"1.0"`) | Khóa chặt theo `TargetTag.DataType`. Tự động ẩn khi chọn `ActionType = TOGGLE_TAG`. | Giá trị gán trực tiếp (`SET_TAG`), lượng số nguyên cộng thêm (`INC_COUNTER`), giá trị ghi từ xa (`WRITE_REMOTE`) hoặc hệ số tỷ lệ (`SCALE_TAG`). |
| `CustomLabel` | Chuỗi văn bản tự do | Rỗng (`"Action"`) | Nhãn tùy biến trên sơ đồ. | Ghi đè tiêu đề hiển thị trên đầu khối đồ họa. |

### Ma trận Tham chiếu 7 Chế độ `ActionType`

| Chế độ | Biểu thức trên Khối | Kiểu `TargetTag` Hợp lệ | Hành vi Thực thi Tất định |
| :--- | :--- | :--- | :--- |
| `SET_TAG` | `TAG = 1` / `TAG = 25.0` | `BOOL`, `INT32`, `REAL` | Ghi thẳng giá trị `ActionParam` vào `TargetTag` (tự động đóng gói chuẩn bit IEEE-754 32-bit nếu đích là `REAL`). |
| `TOGGLE_TAG` | `TOGGLE TAG` | `BOOL` (`DO`, `VFLAG`, `MB_COIL`) | Đảo trạng thái nhị phân nguyên tử (`0 → 1` hoặc `1 → 0`) sau mỗi xung sự kiện đi tới. |
| `INC_COUNTER` | `TAG += N` | `INT32` (`COUNTER`, `VREG`, `VREG_R`) | Cộng lượng số nguyên có dấu `ActionParam` vào giá trị hiện hành (`TargetTag = TargetTag + ActionParam`). |
| `WRITE_REMOTE` | `MODBUS N` | Tag Mạng / Fieldbus | Đẩy yêu cầu ghi giá trị `ActionParam` ra Tag từ xa (đối với cấu hình đa thanh ghi RS-485 chuyên sâu, xem khối [`MB_WRITE`](/vi/blocks/mb-write)). |
| `SEND_ALARM` | `ALARM TAG` | Mọi Tag Đã Khai báo | Phát bản ghi cảnh báo ưu tiên cao gắn kèm mã `TargetTag` phục vụ giám sát SCADA và cửa sổ Live Watch. |
| `LOG_EVENT` | `LOG TAG` | Mọi Tag Đã Khai báo | Ghi nhận một dòng nhật ký sự kiện kèm dấu thời gian vào bộ nhớ chẩn đoán của bộ điều khiển. |
| `SCALE_TAG` | `TAG ← SCALE` | `INT32`, `REAL` (`VREG`, `VREG_R`) | Ghi kết quả quy đổi tuyến tính từ nhánh [`SCALE`](/vi/blocks/scale) phía trước vào thanh ghi đích `TargetTag`. |

---

## 4. Cơ chế Thực thi Nguyên tử trong Vòng quét

Khối `ACTION` đóng vai trò chấp hành ở chặng cuối của chuỗi luật 4 giai đoạn. Ngay khi nhận được xung kích hoạt tại chân `In`:
- **Thực thi lập tức:** Lệnh gán (`SET_TAG`), đảo trạng thái (`TOGGLE_TAG`) hoặc tăng bộ đếm (`INC_COUNTER`) được ghi thẳng vào bộ nhớ biến `TargetTag` trong cùng chu kỳ quét hiện hành.
- **Bảo toàn kiểu dữ liệu:** Giá trị ghi luôn tuân thủ kiểu vật lý của `TargetTag` (số nguyên `INT32`, số thực `REAL` hoặc logic `BOOL`).
- **Xuất ngõ ra vật lý:** Mọi thay đổi trên biến `DO` được chuyển tới cọc ngõ ra phần cứng ngay ở bước xuất ảnh tiến trình ở cuối chu kỳ quét.

---

## 5. Cơ chế Bảo vệ Nhập liệu & Chẩn đoán Trực tuyến

### 5.1 Bảo vệ Dữ liệu Xấu & Chống Gán Nhầm Kiểu Đích

- **Tự động Gỡ Tag Đích Khi Đổi Chế độ Không Tương thích:** Nếu `TargetTag` đang trỏ vào thanh ghi số nguyên (`VREG0`) mà kỹ sư chuyển `ActionType` sang `TOGGLE_TAG`, khối `ACTION` tự động xóa chọn `TargetTag` để ngăn lỗi đảo bit nhầm trên thanh ghi 32-bit. Ngược lại, chuyển sang `INC_COUNTER` cũng tự động gỡ bỏ các Tag kiểu `BOOL`.
- **Chuẩn hóa & Hoàn nguyên khi Xác nhận (`CommitActionParam`):**
  - **Đích Nhị phân (`BOOL`):** Hiển thị nút bấm `[ 0 ]` và `[ 1 ]`; khi chuyển từ Tag nguyên đang đặt `ActionParam = 50` sang Tag `DO`, hệ thống tự động kẹp chuẩn về `"1"`.
  - **Đích Số nguyên (`INT32`):** Tự động chuẩn hóa `"100.0"` → `"100"`, đồng thời từ chối và hoàn nguyên ngay lập tức nếu nhập số thập phân `"25.5"` hoặc ký tự chữ `"abc"` khi nhấn `Enter` / `LostFocus`.
  - **Đích Số thực (`REAL`):** Tự động chuẩn hóa `"25"` → `"25.0"` và `"25,5"` → `"25.5"`, ánh xạ chính xác cấu trúc IEEE-754 32-bit xuống bộ nhớ.

### 5.2 Nhãn Chẩn đoán trong Mô phỏng (`F5`) & Gỡ lỗi Trực tuyến (`F8`)

- **Báo Đã Xuất Lệnh trong Mô phỏng (`F5`):** Bật sáng nhãn `● ĐÃ XUẤT LỆNH` (`● BẬT`) mỗi khi nhánh luật tác động, hoặc hiển thị trực tiếp giá trị sau quy đổi khi chạy chế độ `SCALE_TAG`.
- **Đọc Phản hồi Đích 20 FPS (`F8`):** Hiển thị liên tục trạng thái thực tế của `TargetTag` ngay trên thân khối — ví dụ `Bom_Khuay [Current: ON]` hoặc `San_Luong [Current: 1,420]`, tự động đính kèm ` [STALE]` khi suy giảm chất lượng truyền thông và `[Last Known: ...]` khi ngắt kết nối.

---

## 6. Kịch bản Đấu nối Công nghiệp Thực tế

### Bài toán: Điều khiển Đảo Trạng thái Động cơ Khuấy bằng Một Nút nhấn (`TOGGLE_TAG`) Kèm Đếm Số Lần Vận hành (`INC_COUNTER`)

Một nút nhấn nhả tự phục hồi trên mặt tủ (`DI2`) được dùng để bật/tắt luân phiên khởi động từ bồn khuấy hóa chất (`DO1`), đồng thời mỗi lần thao tác sẽ tự động cộng thêm `1` vào thanh ghi lưu giữ mất điện (`VREG_R0`) để theo dõi chu kỳ bảo trì tiếp điểm contactor.

### Khai báo Bảng Tag

| Tên Tag | Phân loại | Kiểu Dữ liệu | Mô tả / Tên Bí danh (Alias) |
| :--- | :--- | :--- | :--- |
| `DI2` | `DI` | `BOOL` | `Nút nhấn Bật/Tắt Bồn Khuấy (Nhấn nhả NO)` |
| `DO1` | `DO` | `BOOL` | `Contactor Động cơ Khuấy` |
| `VREG_R0` | `VREG_R` | `INT32` | `Bộ đếm Lũy kế Số lần Đóng cắt Bảo trì` |

### Trình tự Đấu nối trên Canvas

1. Đặt khối **`INPUT`** (`Tag = DI2`) → **[`TRIGGER`](/vi/blocks/trigger)** (`TriggerType = ON_RISE`, `ForMs = 60` ms lọc dội tiếp điểm nút nhấn).
2. Nối chân `TRIGGER.Out` sang khối **`ACTION`** thứ nhất cấu hình `ActionType = TOGGLE_TAG`, `TargetTag = DO1` (`CustomLabel = "Đảo Bồn Khuấy"`).
3. Rẽ nhánh dây thứ hai từ cùng chân `TRIGGER.Out` sang khối **`ACTION`** thứ hai cấu hình `ActionType = INC_COUNTER`, `TargetTag = VREG_R0`, `ActionParam = 1` (`CustomLabel = "Đếm Chu kỳ Đóng cắt"`).
4. Nhấn `F5` kiểm thử trên **Simulator**: nhấp liên tiếp nút mô phỏng `DI2` (`0 → 1` giữ trên `60 ms`), xác nhận `DO1` đảo trạng thái gọn gàng `0 → 1 → 0 → 1` trong khi `VREG_R0` tăng đều `1 → 2 → 3` không hề bị đếm đúp do dội nút nhấn.

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>INPUT</span>
      </div>
      <span class="synx-node-status is-live">● DI2</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Nút Nhấn Tủ</span>
        <span class="synx-node-alias">Bồn Khuấy (NO)</span>
        <span class="synx-node-val">DI2 • BOOL</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">DI2 Raw</span>
  </div>

  <div class="synx-canvas-node is-trigger">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M10,1 L5,8.5 L8.5,8.5 L6,15 L11,7.5 L7.5,7.5 Z" /></svg>
        <span>TRIGGER</span>
      </div>
      <span class="synx-node-status is-rise">● 60ms DWELL</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Lọc Dội Tiếp Điểm</span>
        <span class="synx-node-alias">Sườn Lên Khởi Động</span>
        <span class="synx-node-val">ON_RISE • 60 ms</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">PULSE</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● TOGGLE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Đảo Bồn Khuấy</span>
        <span class="synx-node-alias">Contactor Động Cơ</span>
        <span class="synx-node-val">TOGGLE DO1</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

<div class="synx-node-chain" style="margin-top: 12px;">
  <div class="synx-canvas-node is-trigger">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M10,1 L5,8.5 L8.5,8.5 L6,15 L11,7.5 L7.5,7.5 Z" /></svg>
        <span>TRIGGER</span>
      </div>
      <span class="synx-node-status is-rise">● BRANCH</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Rẽ Nhánh Dây</span>
        <span class="synx-node-alias">Cùng Xung Kích Hoạt</span>
        <span class="synx-node-val">TRIGGER.Out</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">PULSE</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● COUNTER</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Đếm Lần Đóng Cắt</span>
        <span class="synx-node-alias">Thanh Ghi Lưu Giữ</span>
        <span class="synx-node-val">VREG_R0 += 1</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>
