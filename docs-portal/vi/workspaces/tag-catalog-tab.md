# 3. Danh mục Biến — Tag Catalog (`Ctrl+3`)

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-WS-003</span>
  <span><strong>Không gian làm việc:</strong> Tab 3 &bull; Phím tắt <code>Ctrl+3</code></span>
  <span><strong>Phạm vi:</strong> Kiến trúc Bộ nhớ Tag, Kiểu Dữ liệu IEC 61131-3, Chống Dữ liệu Sai &amp; Nhập/Xuất CSV Hàng loạt</span>
  <span><strong>Tiêu chuẩn:</strong> IEC 61131-3 &bull; IEC/IEEE 82079-1 &bull; ANSI/ISA-101</span>
</div>

---

## 1. Mục đích & Vai trò Vận hành

**Danh mục Biến — Tag Catalog** (`Ctrl+3`) là nguồn chân lý duy nhất (Single Source of Truth) quản lý phân vùng bộ nhớ, định danh tín hiệu, hợp đồng kiểu dữ liệu và ánh xạ địa chỉ thanh ghi SCADA trong toàn bộ dự án **SynaptiX IDE**. Mọi cầu đấu vật lý, cờ trung gian nội bộ, thanh ghi tính toán tạm thời, thanh ghi lưu trữ bất biến trên Flash và bộ đếm sự kiện được tham chiếu tại [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab), [Bảng Luật — Rule Table](/vi/workspaces/rule-table-tab) hay [Giám sát Trực tiếp — Live Watch](/vi/workspaces/live-watch-tab) đều được khởi tạo và chuẩn hóa từ không gian này.

Về mặt kiến trúc phần cứng, **Tag Catalog** quản lý tối đa **128 biến người dùng thời gian thực (user tags)** cùng **8 biến hệ thống chỉ đọc (system tags)**, đảm nhiệm bốn chức năng kỹ thuật nền tảng:

1. **Phân vùng Bộ nhớ Đồng bộ Phần cứng:** Tổ chức các phân vùng I/O vật lý (`DI`, `DO`, `AI`), bộ nhớ đệm RAM (`VFLAG`, `VREG`), bộ nhớ lưu trữ mất điện không mất dữ liệu (`VREG_RETAIN`) và bộ đếm chuyên dụng (`COUNTER`) khớp chính xác với hồ sơ của **SynaptiX Edge Controller**.
2. **Thực thi Hợp đồng Kiểu Dữ liệu Chuẩn IEC 61131-3:** Khai báo tường minh định dạng lưu trữ nhị phân (`BOOL`, `INT32` hoặc số thực dấu phẩy động IEEE-754 `REAL`) cho từng thanh ghi, kết hợp cơ chế **Chống Dữ liệu Sai (Bad-Data Defense)** trên mọi ô nhập giá trị.
3. **Định danh Bí danh (Alias) & Minh bạch Địa chỉ SCADA:** Gắn tên thiết bị theo bản vẽ công nghệ P&ID trực tiếp vào cột **Alias**, đồng thời hiển thị tự động địa chỉ **Modbus Slave Map** tương ứng để tích hợp tức thời với màn hình HMI hoặc máy chủ SCADA.
4. **Khai báo Hàng loạt Tốc độ cao qua CSV chuẩn UTF-8:** Xuất và nhập toàn bộ danh mục I/O định dạng `*.csv` (mã hóa UTF-8 có BOM), cho phép đội ngũ thiết kế điện và tự động hóa biên tập hàng trăm đầu điểm trên Microsoft Excel mà không lỗi phông chữ tiếng Việt.

---

## 2. Bố cục Trực quan & Tham chiếu Điều khiển

![Không gian Danh mục Biến Tag Catalog SynaptiX](/images/synaptix_tag_catalog.png)

Tuân thủ tiêu chuẩn mật độ bảng dữ liệu công nghiệp IBM Carbon (chiều cao dòng gọn `28–32 px`, căn trái định danh, căn phải phông chữ đơn cách cho địa chỉ/giá trị số, căn giữa huy hiệu trạng thái), **Tag Catalog** bao gồm thanh công cụ lọc nhanh, **Thẻ Giải thích Phân vùng Tag** thu gọn và bảng dữ liệu chính **Tag DataGrid**.

### 2.1. Phân loại Phân vùng Bộ nhớ Tag & Đặc tính Phần cứng

Khi chọn bất kỳ nút lọc phân vùng nào, bảng dữ liệu sẽ lọc tương ứng và hiển thị thẻ tra cứu nhanh về ý nghĩa thiết bị hiện trường, vai trò trong tập luật và thông số phần cứng:

| Nhãn Phân vùng | Đặc tính Lưu trữ | Quyền Truy cập | Kiểu Dữ liệu Hỗ trợ | Vai trò Hiện trường & Thực thi Runtime |
| :--- | :--- | :--- | :--- | :--- |
| **`DI`** *(Discrete Input)* | Cầu đấu Vật lý | Chỉ đọc (RO) | `BOOL` | Ngõ vào số cách ly quang `24 VDC` đọc công tắc hành trình, cảm biến tiệm cận, nút nhấn tủ điện và tiếp điểm dừng khẩn E-Stop. |
| **`DO`** *(Discrete Output)* | Cầu đấu Vật lý | Đọc / Ghi (R/W) | `BOOL` | Ngõ ra rơ-le hoặc transistor tốc độ cao đóng cắt khởi động từ, van điện từ, đèn tháp Andon hoặc phát xung điều khiển. |
| **`AI`** *(Analog Input)* | Cầu đấu Vật lý | Chỉ đọc (RO) | `REAL` / `INT32` | Ngõ vào tương tự `4–20 mA` hoặc `0–10 V` (ADC 12-bit) đo áp suất, nhiệt độ, mức bồn và lưu lượng đường ống. |
| **`VFLAG`** *(Virtual Flag)* | RAM khả biến (SRAM) | Đọc / Ghi (R/W) | `BOOL` | Rơ-le trung gian nội bộ dùng chốt trạng thái tự giữ, cờ cho phép liên động tầng, cờ cảnh báo và bit nhận lệnh MQTT. |
| **`VREG`** *(Virtual Register)* | RAM khả biến (SRAM) | Đọc / Ghi (R/W) | `INT32` hoặc `REAL` | Thanh ghi 32-bit lưu giá trị kỹ thuật sau hiệu chuẩn `SCALE`, dữ liệu đọc từ RS-485 Modbus và biến trung gian tính toán. |
| **`VREG_RETAIN`** *(Lưu trữ Flash)* | Flash bất biến (Non-Volatile) | Đọc / Ghi (R/W) | `INT32` hoặc `REAL` | Thanh ghi lưu trữ bền vững khi mất nguồn (độ bền trên `100.000` chu kỳ ghi) dành cho ngưỡng cài đặt công thức, hệ số hiệu chuẩn và sản lượng tích lũy. |
| **`COUNTER`** *(Bộ đếm Sự kiện)* | SRAM / Đồng bộ Lưu trữ | Đọc / Ghi (R/W) | `INT32` / `REAL` | Thanh ghi tích lũy chuyên dụng được cập nhật bởi các lệnh `INC_COUNTER`, `CTU` và `CTD` trong bài toán đếm kiện hàng/mẻ sản xuất. |
| **`SYSTEM`** *(Biến Hệ thống)* | Nội bộ Firmware (`1000..1007`) | Chỉ đọc (RO) | `BOOL` / `INT32` | 8 biến xung nhịp & trạng thái chuẩn hóa: `SYS_FIRST_SCAN`, `SYS_ON`, `SYS_OFF`, `SYS_CLOCK_1S`, `SYS_CLOCK_100MS`, `SYS_RTC_HOUR`, `SYS_RTC_MINUTE` và `SYS_COMM_OK`. |

### 2.2. Tham chiếu Cột Bảng Dữ liệu & Thanh Công cụ

| Điều khiển / Tên Cột | Căn lề & Thành phần UI | Hành vi Kỹ thuật |
| :--- | :--- | :--- |
| **Thanh Lọc Phân vùng (Filter Pills)** | Thanh lọc phía trên | Lọc nhanh danh mục 128 biến theo `ALL`, `DI`, `DO`, `AI`, `VFLAG`, `VREG`, `VREG_RETAIN`, `COUNTER` hoặc `SYSTEM` kèm số lượng biến từng nhóm. |
| **Ô Tìm kiếm Tức thời (Search)** | Thanh công cụ trên | Lọc dòng theo thời gian thực dựa trên `Tag Name`, `Alias`, `Group`, `Modbus Address` hoặc `Data Type`. |
| **Import CSV / Export CSV** | Thanh công cụ & Menu `File` | Nhập hoặc xuất tệp bảng tính CSV chuẩn UTF-8 (có BOM) phục vụ chỉnh sửa hàng loạt và kế thừa danh mục giữa các dự án. |
| **`Index`** | Căn phải, Monospace | Chỉ số định danh tất định trong bộ nhớ Runtime (`0..127` cho biến người dùng; `1000..1007` cho biến `SYSTEM`). |
| **`Tag Name`** | Căn trái, Nhãn mã cứng | Tên mã phần cứng cố định không thể đổi tên sai lệch (ví dụ: `DI0`, `DO3`, `AI1`, `VREG0`, `VREG_RETAIN0`). |
| **`Modbus Address`** | Căn phải, Nhãn Monospace | Hiển thị chính xác địa chỉ Coil/Register trong bản đồ SCADA Slave Map phục vụ kết nối RS-485 hoặc Modbus TCP. |
| **`Data Type`** | Căn giữa, Nhãn / Dropdown | Nhãn `BOOL` cố định cho nhóm biến số (`DI`, `DO`, `VFLAG`); danh sách chọn `INT32` / `REAL` trực tiếp cho nhóm thanh ghi (`VREG`, `VREG_RETAIN`). |
| **`Group`** | Ô văn bản chỉnh sửa trực tiếp | Nhãn phân nhóm cụm máy hoặc khu vực công nghệ (ví dụ: `Cum_Bom_Chinh`, `Lo_Hoi_1`, `Bang_Tai_A`). |
| **`Alias`** | Ô văn bản chỉnh sửa trực tiếp | Tên gợi nhớ kỹ thuật theo hồ sơ P&ID (ví dụ: `Bao_Qua_Nhiet_Bom_1` hoặc `Muc_Bon_Chua_Met`) hiển thị xuyên suốt toàn bộ IDE. |
| **`Value`** | Ô nhập Monospace + Nút `0↔1` | Hiển thị giá trị khởi tạo/mô phỏng kèm đèn LED trạng thái (biến `BOOL` tích hợp nút đảo nhanh `0↔1` an toàn kiểu; biến số áp dụng Bad-Data Defense). |

---

## 3. Hợp đồng Kiểu Dữ liệu, Chống Dữ liệu Sai & Đặc tả CSV

### 3.1. Hợp đồng Bộ nhớ Vật lý & Chu trình Tự động Chuẩn hóa

Trong **SynaptiX IDE**, kiểu dữ liệu là hợp đồng bộ nhớ vật lý của vi điều khiển chứ không phải nhãn trang trí giao diện. Mọi ô nhập giá trị trong **Tag Catalog** vận hành theo máy trạng thái hữu hạn 2 pha (**Đang gõ — Drafting** và **Xác nhận — Committed** khi nhấn `Enter` hoặc rời ô `LostFocus`):

| Kiểu Dữ liệu | Cấu trúc Vật lý dưới Runtime | Giá trị Hợp lệ Chuẩn | Tự động Chuẩn hóa (`Enter` / `LostFocus`) | Phòng vệ Chống Dữ liệu Sai (Auto-Revert) |
| :--- | :--- | :--- | :--- | :--- |
| **`BOOL`** | Trạng thái 1-bit rời rạc (`0` hoặc `1`) | `0` hoặc `1` (hoặc nút gạt `0↔1`) | `"true"`, `"on"`, `"bật"` → `"1"`<br/>`"false"`, `"off"`, `"tắt"` → `"0"` | Mọi chuỗi sai quy cách (`"2"`, `"abc"`) bị từ chối và **tự động trả về** giá trị `0` hoặc `1` hợp lệ trước đó. |
| **`INT32`** | Số nguyên có dấu bù 2 32-bit (`-2,147,483,648` .. `2,147,483,647`) | `-150`, `0`, `4095` | `"100.0"` hoặc `"100,0"` → `"100"` | Giá trị lẻ thập phân (`"25.5"`) hoặc chữ (`"abc"`) không tồn tại vật lý trong thanh ghi nguyên—bị từ chối và **khôi phục tức thì**. |
| **`REAL`** | Số thực dấu phẩy động 32-bit chuẩn IEEE-754 | `25.5`, `-0.75`, `101.325` | `"25"` hoặc `"25,0"` → `"25.0"` | Chuỗi phi số (`"NaN"`, `"loi"`) bị chặn hoàn toàn và **khôi phục về** giá trị số thực hợp lệ liền trước. |

::: danger CAM KẾT SỰ THẬT THỊ GIÁC (VISUAL TRUTH — ANSI/ISA-101)
**SynaptiX IDE** nghiêm cấm hành vi tự động cắt phần thập phân âm thầm. Việc gõ `"25.5"` vào một biến kiểu `INT32` sẽ không bao giờ để lại chữ `"25.5"` trên màn hình trong khi phần cứng lưu số `25`. Giao diện luôn tự động khôi phục về số nguyên đã xác nhận để thông số kỹ sư nhìn thấy khớp 1:1 với bộ nhớ **SynaptiX Runtime**.
:::

### 3.2. Đặc tả Định dạng Tệp CSV Nhập / Xuất Hàng loạt

Chức năng **File → Import / Export → Tags CSV** đọc và xuất tệp CSV tuân thủ chuẩn RFC-4180 được mã hóa **UTF-8 kèm BOM** (giúp mở trực tiếp bằng Microsoft Excel hiển thị chuẩn xác 100% tiếng Việt có dấu).

- **Quy tắc Đối sánh khi Nhập (Import):** Từng dòng dữ liệu được ánh xạ ưu tiên theo cột `Name` chuẩn (không phân biệt hoa thường, ví dụ `VREG_RETAIN0`), sau đó mới đối chiếu theo `Index`. Các kênh nằm ngoài cấu hình phần cứng hiện hành sẽ được bỏ qua an toàn mà không làm hỏng dữ liệu hợp lệ.
- **Quy tắc Dấu phẩy & Ngoặc kép:** Các chuỗi `Alias` có chứa dấu phẩy hoặc khoảng trắng đặc biệt được đặt trong cặp dấu ngoặc kép chuẩn (`"..."`).

Ví dụ nội dung tệp mẫu `synaptix_tags.csv`:

```csv
Index,Name,Alias,Kind,Channel,Group,Value
0,DI0,"Chuoi_An_Toan_EStop_OK",DI,0,Mach_An_Toan,1
1,DI1,"Nut_Nhan_Khoi_Dong_May",DI,1,Tu_Van_Hanh,0
8,DO0,"Contactor_Bom_Cap_Chinh",DO,0,Cum_Bom,0
16,AI0,"Ap_Suat_Ong_Gop_Bar",AI,0,Do_Luong,6.25
24,VFLAG0,"Co_Cho_Phep_Chay_Tu_Dong",VFLAG,0,Trang_Thai_Logic,1
40,VREG0,"Luu_Luong_Quy_Doi_LPM",VREG,0,Tinh_Toan,120.0
56,VREG_RETAIN0,"Nguong_Bao_Dong_Ap_Suat_Cao",VREG_RETAIN,0,Cai_Dat_Nguong,8.5
64,COUNTER0,"So_Kien_Hang_Hoan_Thanh",COUNTER,0,Dong_Goi,0
```

---

## 4. Quy trình Thao tác Chuẩn từng Bước

### Quy trình 4.1: Đặt Bí danh Kỹ thuật (Alias) & Cấu hình Thanh ghi Ngưỡng `REAL`

1. Nhấn `Ctrl+3` để mở **Danh mục Biến (Tag Catalog)**.
2. Nhấp vào nút lọc **`VREG_RETAIN`** trên thanh công cụ để lọc riêng nhóm thanh ghi lưu trữ Flash bất biến phục vụ lưu thông số cài đặt không bị mất khi cúp điện.
3. Tại dòng `VREG_RETAIN0`, mở danh sách chọn ở cột **`Data Type`** và chuyển từ `INT32` sang `REAL` nếu ngưỡng điều khiển cần độ chính xác phần thập phân (ví dụ: `75.5` °C).
4. Nhấp vào cột **`Group`** để gán tên phân khu (`Lo_Nhiet_1`) và nhập tên gợi nhớ `Nguong_Cat_Qua_Nhiet_C` vào cột **`Alias`**.
5. Nhập `75.5` vào cột **`Value`** rồi nhấn `Enter`. Kể từ lúc này, cả tên bí danh và hợp đồng kiểu `REAL` sẽ xuất hiện đồng bộ trong mọi hộp chọn Tag trên [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab) và [Giám sát — Live Watch](/vi/workspaces/live-watch-tab).

### Quy trình 4.2: Khai báo Danh mục I/O Hàng loạt từ Bảng tính Excel (CSV)

1. Xuất tệp khung chuẩn của phần cứng hiện tại qua menu **File → Export → Tags CSV** (hoặc nút **Export CSV** trên thanh công cụ) và lưu thành `synaptix_tags.csv`.
2. Mở `synaptix_tags.csv` bằng Microsoft Excel, điền hàng loạt cột `Alias`, `Group` và giá trị khởi tạo `Value` dựa trên bản vẽ đấu nối tủ điện, sau đó lưu lại tệp (giữ nguyên chuẩn UTF-8).
3. Quay lại **SynaptiX IDE**, chọn **File → Import → Tags CSV** và trỏ tới tệp `synaptix_tags.csv`.
4. Kiểm tra các bí danh vừa cập nhật trên bảng và nhấn `F7` để trình biên dịch kiểm tra lại tính tương thích kiểu dữ liệu trên toàn bộ tập luật.

---

## 5. Thực hành Kỹ thuật Tốt nhất & Xử lý Tình huống Biên

- **Bảo vệ Tuổi thọ Bộ nhớ Flash trên `VREG_RETAIN`:** Mặc dù vùng nhớ `VREG_RETAIN` trên phần cứng được thiết kế chống mài mòn công nghiệp (độ bền trên `100.000` chu kỳ xóa/ghi), kỹ sư tuyệt đối không nên gán các giá trị cảm biến biến thiên liên tục mỗi `100 ms` vào `VREG_RETAIN`. Hãy sử dụng **`VREG`** (RAM tốc độ cao không giới hạn số lần ghi) cho phép tính tức thời và dành riêng **`VREG_RETAIN`** cho ngưỡng đặt công thức, hệ số hiệu chuẩn hoặc tổng sản lượng ca.
- **Thay đổi Kiểu Dữ liệu trên Biến Đã Nối Dây:** Khi chuyển đổi một biến `VREG` hoặc `VREG_RETAIN` giữa `INT32` và `REAL`, phương thức đóng gói nhị phân và chế độ so sánh số thực (`FloatCompare`) của bộ điều khiển sẽ thay đổi tương ứng. Hãy luôn nhấn `F7` ngay sau khi đổi kiểu dữ liệu để trình kiểm tra tĩnh rà soát toàn bộ các khối `COMPARATOR`, `SCALE` và `MB_READ` đang tham chiếu tới biến đó.
- **Tận dụng Nhóm Biến Hệ thống `SYSTEM`:** Thay vì tự dựng mạch tạo xung nhấp nháy đèn báo hoặc xung khởi tạo chu kỳ đầu, hãy nối trực tiếp tới các biến chuẩn hóa sẵn có như `SYS_FIRST_SCAN` (xung đơn kích hoạt duy nhất ở vòng quét đầu tiên khi cấp nguồn), `SYS_CLOCK_1S` (xung nhịp `1 Hz`) hoặc `SYS_COMM_OK` (cờ giám sát truyền thông ổn định).

---

## Tài liệu Liên quan

- [Kiến trúc Bộ nhớ Tag & Kiểu Dữ liệu](/vi/concepts/data-model-and-types)
- [2. Sơ đồ Logic — Logic Graph (`Ctrl+2`)](/vi/workspaces/logic-graph-tab)
- [7. Giám sát — Live Watch (`Ctrl+7`)](/vi/workspaces/live-watch-tab)
- [Cấu hình RS-485 & Bản đồ SCADA Slave](/vi/tools/rs485-and-scada-slave)
- [Từ điển Macro & Giới hạn Tài nguyên](/vi/appendices/macro-tokens-and-limits)
