# Kiến trúc Bộ nhớ Tag & Kiểu Dữ liệu Vật lý

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-CON-002</span>
  <span><strong>Chương:</strong> 01. Tổng quan &amp; Kiến trúc Cốt lõi</span>
  <span><strong>Áp dụng cho:</strong> SynaptiX IDE v2.x &bull; Tag Memory Contract</span>
  <span><strong>Tiêu chuẩn:</strong> IEC 61131-3 &bull; ANSI/ISA-101</span>
</div>

Trong **SynaptiX IDE**, mọi chân tín hiệu cảm biến vật lý, ngõ ra rơ-le, cờ trạng thái nội bộ, hệ số hiệu chuẩn và thanh ghi truyền thông đều được ánh xạ vào một **Mô hình Bộ nhớ Tag Hợp nhất (Unified Tag Memory Model)** với sức chứa tối đa **128 Tag thời gian thực**.

Được xây dựng từ nguyên lý nền tảng theo chuẩn IEC 61131-3, **SynaptiX IDE** coi kiểu dữ liệu không phải là nhãn hiển thị hình thức trên giao diện, mà là **hợp đồng bộ nhớ vật lý nghiêm ngặt**, kết hợp cùng **Máy Trạng thái Nhập liệu (Bad-Data Defense)** nhằm đảm bảo tuyệt đối rằng mọi con số kỹ sư nhìn thấy trên màn hình phản ánh chính xác 1:1 giá trị thực thi bên trong **Bộ Điều khiển Biên SynaptiX**.

---

## Mô hình Bộ nhớ Tag Hợp nhất (Tối đa 128 Tag)

Tùy theo [Cấu hình Thiết bị Định sẵn](/vi/concepts/ecosystem-overview) được chọn, dự án sẽ cấp phát tối đa 7 phân vùng Tag vật lý và Tag ảo liên tiếp, bên cạnh nhóm biến chẩn đoán hệ thống:

| Phân vùng Tag | Dải Định danh | Kiểu Mặc định | Kiểu Dữ liệu Cho phép | Quyền Truy cập | Đặc tính Lưu trữ | Mục đích Kỹ thuật Chính |
| :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Ngõ vào Số (Digital Inputs)** | `DI0 .. DI7` | `BOOL` | `BOOL` (Cố định) | Chỉ đọc* | Phần cứng Trực tiếp | Đọc tín hiệu cách ly quang từ công tắc hành trình, cửa an toàn, cảm biến tiệm cận. |
| **Ngõ ra Số (Digital Outputs)** | `DO0 .. DO7` | `BOOL` | `BOOL` (Cố định) | Đọc / Ghi | Tạm thời (RAM) | Điều khiển rơ-le, van điện từ, quạt làm mát, còi báo hoặc xuất xung PWM/PTO. |
| **Ngõ vào Tương tự (Analog Inputs)** | `AI0 .. AI3` | `INT32` | `INT32`, `REAL` | Chỉ đọc* | Phần cứng Trực tiếp | Kênh đo ADC phần cứng (`4-20 mA`, `0-10 V`, giá trị thô hoặc số thực đã quy đổi). |
| **Cờ Ảo Nội bộ (Virtual Flags)** | `VFLAG0 .. VFLAG31` | `BOOL` | `BOOL` (Cố định) | Đọc / Ghi | Tạm thời (SRAM) | Cờ chốt trung gian, ngõ ra hoàn thành Timer, trạng thái liên động và lệnh điều khiển từ xa. |
| **Thanh ghi Ảo (Virtual Registers)** | `VREG0 .. VREG31` | `INT32` | `INT32`, `REAL` | Đọc / Ghi | Tạm thời (SRAM) | Thanh ghi đệm 32-bit lưu kết quả khối [`SCALE`](/vi/blocks/scale), dữ liệu đọc [`MB_READ`](/vi/blocks/mb-read) và lệnh [`MQTT_SUB`](/vi/blocks/mqtt-sub). |
| **Thanh ghi Lưu trữ (Retentive)** | `VREG_RETAIN0 .. VREG_RETAIN31` | `INT32` | `INT32`, `REAL` | Đọc / Ghi | **Bộ nhớ Flash Bền vững** | Ngưỡng cài đặt (Setpoint), hệ số hiệu chuẩn và sản lượng tích lũy được bảo toàn khi mất điện. |
| **Bộ đếm (Counters)** | `COUNTER0 .. COUNTER7` | `INT32` | `INT32` (Cố định) | Đọc / Ghi | Tạm thời / Quản lý | Bộ tích lũy số nguyên có dấu 32-bit cho khối [`CTU`](/vi/blocks/ctu), [`CTD`](/vi/blocks/ctd) và lệnh cộng/trừ. |

*\*Lưu ý: Các biến `DI` và `AI` mang thuộc tính chỉ đọc đối với logic người dùng khi chạy trên thiết bị thật vì giá trị của chúng được lấy mẫu trực tiếp từ chân phần cứng đầu mỗi chu kỳ quét. Trong chế độ [Mô phỏng Offline](/vi/tools/simulator-and-live-debug) (`F5`), kỹ sư có thể chủ động bật/tắt `DI` và bơm giá trị thử nghiệm cho `AI`.*

::: tip Bảo toàn Dữ liệu Qua Sự cố Mất điện với `VREG_RETAIN`
Các biến ảo thông thường (`VFLAG0..31` và `VREG0..31`) nằm trên vùng nhớ SRAM nên sẽ trở về `0` (`0.0` đối với `REAL`) khi khởi động lại nguồn. Đối với mọi ngưỡng cảnh báo, thông số cài đặt vận hành hoặc bộ đếm sản lượng cần giữ nguyên sau khi cúp điện nhà máy, hãy luôn gắn vào dải **`VREG_RETAIN0 .. VREG_RETAIN31`** để nhân thực thi tự động lưu bền vững vào bộ nhớ Flash không bay hơi.
:::

### Tên Gợi nhớ (Alias) & Hiển thị Kép

Trong [Danh mục Biến — Tag Catalog](/vi/workspaces/tag-catalog-tab) (`Ctrl+3`), mỗi Tag đều cho phép đặt **Tên gợi nhớ (Alias)** sát với quy trình công nghệ (ví dụ gán `TankTemp` cho `AI0` và `SafetyDoorOk` cho `DI0`). Ngay sau khi đặt tên, toàn bộ danh sách chọn biến, tiêu đề khối trên [Sơ đồ Logic](/vi/workspaces/logic-graph-tab) và bảng đồ thị [Live Watch](/vi/workspaces/live-watch-tab) sẽ hiển thị đồng thời dạng `[AI0] — TankTemp`, giúp kỹ sư vừa nắm rõ ý nghĩa công nghệ vừa đối chiếu ngay được chân đấu dây vật lý.

### Nhóm Thẻ Hệ Thống Định Sẵn (Industrial System Tags)

Bên cạnh 128 Tag người dùng thông thường, **SynaptiX IDE** tích hợp sẵn **8 Thẻ Hệ thống chuẩn công nghiệp** (tương tự như trong Siemens TIA Portal hay CODESYS) giúp kỹ sư lập trình các yêu cầu vận hành phổ biến mà không cần tạo thêm cờ nhớ hay khối Timer phụ trợ:

| Tên Thẻ Hệ Thống | Kiểu Dữ liệu | Giá trị & Ý nghĩa Vận hành | Ứng dụng Thực tế Điển hình |
| :--- | :---: | :--- | :--- |
| **`SYS_FIRST_SCAN`** | `BOOL` | Tự động lên mức `1` trong **chu kỳ quét đầu tiên** ngay sau khi cấp nguồn/nạp chương trình, sau đó tự về `0`. | Kích hoạt chu trình thiết lập gốc máy (Homing), nạp giá trị mặc định cho van/động cơ. |
| **`SYS_ON`** | `BOOL` | Luôn giữ mức `1` (Hằng số TRUE vĩnh viễn). | Cung cấp tín hiệu kích hoạt liên tục cho các khối cần chạy thường trực. |
| **`SYS_OFF`** | `BOOL` | Luôn giữ mức `0` (Hằng số FALSE vĩnh viễn). | Ngắt tạm thời một nhánh logic hoặc khóa an toàn. |
| **`SYS_CLOCK_1S`** | `BOOL` | Xung vuông chu kỳ `1.0 giây` (`500 ms` Bật / `500 ms` Tắt, tần số `1.0 Hz`). | Chớp đèn tháp báo lỗi (Tower Light), nháy còi cảnh báo không cần thêm khối TON. |
| **`SYS_CLOCK_100MS`**| `BOOL` | Xung vuông chu kỳ `100 mili-giây` (`50 ms` Bật / `50 ms` Tắt, tần số `10.0 Hz`). | Nhịp lấy mẫu dữ liệu tốc độ cao, chớp đèn LED báo bận. |
| **`SYS_RTC_HOUR`** | `INT32` | Giờ thực tế (`0 .. 23`) lấy trực tiếp từ chip đồng hồ RTC phần cứng. | Hẹn giờ bật đèn nhà xưởng, kích hoạt hệ thống tưới theo ca làm việc. |
| **`SYS_RTC_MINUTE`** | `INT32` | Phút thực tế (`0 .. 59`) lấy trực tiếp từ chip đồng hồ RTC phần cứng. | Hẹn giờ chính xác theo phút trong ngày. |
| **`SYS_COMM_OK`** | `BOOL` | Bật mức `1` khi đường truyền kết nối truyền thông ngoại vi thông suốt. | Khóa liên động an toàn khi thiết bị ngoại vi mất kết nối. |

::: tip Dễ Dàng Chọn Thẻ Hệ Thống Trên Mọi Khối FBD
Trong danh sách thả xuống chọn biến (Tag Selector) của các khối `INPUT`, `TRIGGER`, `GUARD` hay `COMPARATOR`, nhóm Thẻ Hệ thống được nhóm riêng với biểu tượng bánh răng đặc trưng, giúp bạn chọn ngay lập tức mà không sợ nhầm lẫn với các chân I/O vật lý.
:::

---

## Hệ thống Kiểu Dữ liệu Vật lý Nghiêm ngặt

Vi điều khiển và nhân thực thi công nghiệp vận hành thuần túy trên các mẫu bit nhị phân trong thanh ghi 32-bit. **SynaptiX IDE** thực thi nhất quán 3 kiểu dữ liệu vật lý trên toàn bộ hệ thống:

### 1. `BOOL` — Trạng thái Nhị phân 1-Bit
- **Miền giá trị hợp lệ:** Chỉ nhận giá trị `0` (Sai / Mức thấp / Tắt) hoặc `1` (Đúng / Mức cao / Bật).
- **Áp dụng cho:** Cố định bắt buộc đối với `DI0..7`, `DO0..7` và `VFLAG0..31`.
- ** Cam kết trên giao diện:** Mọi vị trí cấu hình hằng số logic đều cung cấp nút gạt nhị phân tường minh (`0` / `1`) hoặc chuẩn hóa chuỗi từ khóa logic về đúng `"0"` hoặc `"1"`.

### 2. `INT32` — Số Nguyên Có Dấu 32-Bit (Bù 2 — Two's Complement)
- **Miền giá trị hợp lệ:** Các số nguyên tròn trịa từ `-2,147,483,648` đến `+2,147,483,647` (`-2^31` đến `2^31 - 1`).
- **Áp dụng cho:** Cố định đối với `COUNTER0..7`; tùy chọn trên `AI0..3`, `VREG0..31` và `VREG_RETAIN0..31`.
- **Chân lý Vật lý — Vì sao số lẻ `25.5` Không Thể Tồn Tại trong `INT32`:**
  Trong kiến trúc thanh ghi số nguyên bù 2 của vi xử lý, trọng số của từng bit biểu diễn lũy thừa nguyên của 2 (`2^0 = 1`, `2^1 = 2`, ..., `2^30`). **Hoàn toàn không tồn tại bất kỳ bit nào lưu phần lẻ thập phân.** Một con số như `25.5` về mặt vật lý không thể nằm trong thanh ghi `INT32` nếu không bị cắt cụt ngầm thành `25` hoặc phải nhân hệ số định điểm (`255` đơn vị `0.1`). Vì hành vi tự ý cắt bỏ phần thập phân ngầm sẽ gây sai lệch nghiêm trọng giữa ngưỡng kỹ sư tưởng mình đã nhập và ngưỡng thiết bị thực thi, **SynaptiX IDE** chặn đứng và từ chối mọi giá trị có phần lẻ khác không (như `"25.5"`) trên các ô nhập kiểu `INT32`.

### 3. `REAL` — Số Thực Dấu Phẩy Động 32-Bit Chuẩn IEEE-754
- **Miền giá trị hợp lệ:** Số thực chính xác đơn chuẩn IEEE-754 binary32 (`1` bit dấu, `8` bit mũ, `23` bit định trị; độ chính xác ~7 chữ số có nghĩa).
- **Áp dụng cho:** Tùy chọn trực tiếp trên `AI0..3`, `VREG0..31` và `VREG_RETAIN0..31` trong bảng [Tag Catalog](/vi/workspaces/tag-catalog-tab).
- **Biểu diễn Bộ nhớ & Hiển thị:** Được ánh xạ nguyên vẹn 32-bit theo chuẩn IEEE-754 (`SingleToInt32Bits`) và luôn được **SynaptiX IDE** hiển thị kèm tối thiểu một chữ số sau dấu chấm thập phân (ví dụ `"25.0"`, `"65.25"`) để kỹ sư phân biệt tức thì bằng mắt thường giữa biến `REAL` và biến `INT32`.

::: warning Đồng bộ Tự động Khi Chuyển đổi Kiểu Dữ liệu Tag
Khi bạn đổi kiểu dữ liệu của một Tag trong [Tag Catalog](/vi/workspaces/tag-catalog-tab) (ví dụ chuyển `AI0` từ `INT32` sang `REAL`), mọi khối [`COMPARATOR`](/vi/blocks/comparator) hoặc [`ACTION`](/vi/blocks/action) đang tham chiếu tới Tag đó sẽ lập tức cập nhật quy tắc kiểm tra hằng số theo kiểu mới. Các mẫu bit đặc biệt như `NaN` hay `Infinity` cũng được lọc an toàn về `"0.0"`.
:::

---

## Cơ chế Tự động Bảo vệ khi Nhập liệu (Bad-Data Defense)

Trong vận hành tự động hóa, việc lỡ tay gõ nhầm một giá trị sai (như gõ số lẻ `"25.5"` vào biến số nguyên đặt ngưỡng rơ-le) có thể gây sai lệch logic và dừng chuyền sản xuất. **SynaptiX IDE** tự động bảo vệ an toàn dữ liệu trên toàn bộ giao diện (Property Inspector, Tag Catalog, Simulator, Live Watch) theo 2 quy tắc trực quan:

::: tip Quy tắc Nhập liệu An toàn & Tự động Chuẩn hóa
- **Tự động làm sạch định dạng:** Khi nhập `100.0` hoặc `100,0` vào biến số nguyên `INT32`, phần mềm tự chuẩn hóa thành `100`. Đối với biến Logic (`BOOL`), bạn có thể gõ nhanh `1`, `0`, `on`, `off`, `bật`, `tắt`.
- **Tự động hoàn nguyên (Auto-Revert):** Nếu bạn vô tình gõ sai kiểu (gõ `"25.5"` vào `INT32` hoặc gõ chữ vào ô số), ngay khi bạn nhấn <kbd>Enter</kbd> hoặc click chuột ra ngoài, ô nhập liệu sẽ **lập tức khôi phục về giá trị hợp lệ trước đó**. Dữ liệu trong bộ nhớ thiết bị được bảo toàn tuyệt đối, không bao giờ để lọt giá trị rác.
:::

| Kiểu Dữ liệu Đích | Giá trị Bạn Gõ vào | Kết quả Xử lý | Giá trị Thực tế Ghi nhận |
| :---: | :--- | :--- | :---: |
| **`INT32`** | `"100.0"` hoặc `"100,0"` | **Tự động chuẩn hóa** (bỏ đuôi thập phân `.0`) | `100` |
| **`INT32`** | `"25.5"` hoặc `"abc"` | **Tự động hoàn nguyên** (từ chối số lẻ/chữ trên số nguyên) | *Giữ nguyên giá trị cũ* |
| **`REAL`** | `"25"` hoặc `"25,5"` | **Tự động chuẩn hóa** (`25` $\rightarrow$ `25.0`; đổi `,` thành `.`) | `25.0` / `25.5` |
| **`BOOL`** | `"1"`, `"on"`, `"bật"` / `"0"`, `"off"`, `"tắt"` | **Tự động chuẩn hóa** thành `1` hoặc `0` | `1` / `0` |

---

## Tài liệu Liên quan

- [Không gian Danh mục Biến — Tag Catalog (`Ctrl+3`)](/vi/workspaces/tag-catalog-tab) — Cách đặt Alias, chuyển đổi kiểu `INT32`/`REAL` và nhập/xuất danh sách Tag qua tệp CSV.
- [Chu trình Thực thi Tất định](/vi/concepts/execution-pipeline) — Cách các Tag luân chuyển qua chuỗi luật, bộ định thời, bộ đếm và các khối truyền thông.
- [SCALE — Khối Tuyến tính hóa & Hiệu chuẩn 2 Điểm](/vi/blocks/scale) — Quy đổi giá trị ADC thô `INT32` (`AI0..3`) sang đơn vị vật lý `REAL`.
