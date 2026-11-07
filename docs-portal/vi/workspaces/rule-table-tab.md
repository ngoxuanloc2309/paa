# 4. Bảng Luật — Rule Table (`Ctrl+4`)

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-WS-004</span>
  <span><strong>Không gian làm việc:</strong> Tab 4 &bull; Phím tắt <code>Ctrl+4</code></span>
  <span><strong>Phạm vi:</strong> Kiểm tra Tập Luật ECA Sau Biên dịch, Truy vết Khối Macro, Bảng Phân hệ &amp; Quản trị Ngân sách 100 Luật</span>
  <span><strong>Tiêu chuẩn:</strong> IEC 61131-3 &bull; IEC/IEEE 82079-1 &bull; ANSI/ISA-101</span>
</div>

---

## 1. Mục đích & Vai trò Vận hành

**Bảng Luật — Rule Table** (`Ctrl+4`) là sổ cái thực thi tất định của **SynaptiX IDE**. Mỗi khi kỹ sư nhấn phím `F7` (**Compile Rules**) trên [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab), toàn bộ cấu trúc mạng khối trực quan được trình biên dịch hạ cấp (lowering) thành mảng phẳng các luật **Sự kiện – Điều kiện – Hành động (ECA)** có thời gian thực thi hằng số, đánh chỉ số từ `0..99` (hiển thị trực quan là `R1..R100`), đi kèm danh sách kênh truyền thông ngoại vi và khối hàm có trạng thái.

Trong giai đoạn nghiệm thu tại xưởng (FAT), chạy thử tại nhà máy (SAT) và kiểm toán an toàn công nghiệp, **Rule Table** đóng bốn vai trò cốt lõi:

1. **Kiểm chứng Kết quả Biên dịch Tất định:** Minh bạch hóa 100% cấu trúc 4 tầng (`Trigger`, `Comparator / Threshold`, `Guard`, `Action`) mà **SynaptiX Runtime** sẽ quét tuần tự trong từng chu kỳ máy.
2. **Quản trị Ngân sách Tài nguyên Phần cứng (`0..100` Luật):** Giám sát trực quan mức tiêu thụ bộ nhớ luật so với **trần phần cứng 100 luật**, bao gồm cả cơ chế bung đa luật tự động của các khối hàm chuẩn IEC (`TON`, `TOF`, `TP`, `CTU`, `CTD`).
3. **Kiểm tra Tập trung 4 Phân hệ Runtime:** Rà soát đồng bộ cả 4 bảng cấu hình sau biên dịch—**Logic Rules**, **Modbus Master**, **MQTT Services** và **Function Blocks**—dưới dạng bảng cô đọng mà không cần cuộn tìm trên các bản vẽ lớn.
4. **Tối ưu hóa Tập Luật & Đọc Ngược Phần cứng:** Cung cấp công cụ **Deduplicate Rules** tự động gộp xóa các luật trùng lặp, bật/tắt nhanh từng luật riêng lẻ, xuất báo cáo CSV nghiệm thu hoặc đọc trực tiếp tập luật đang chạy dưới thiết bị lên IDE qua phím `F6`.

---

## 2. Bố cục Trực quan & Tham chiếu Điều khiển

![Không gian Bảng Luật ECA Rule Table SynaptiX](/images/synaptix_rule_table.png)

Giao diện **Rule Table** tích hợp **Thanh Ngân sách Phần cứng** ở tiêu đề trên, **4 Thẻ Phân hệ Con (Sub-Tabs)**, **Thanh Công cụ Lọc & Tối ưu**, bảng dữ liệu mật độ cao **ECA Rule DataGrid** và khay **Logic Flow Inspector** thu gọn ở cạnh dưới.

### 2.1. Định mức Ngân sách 100 Luật Phần cứng

SynaptiX Controller cung cấp ngân sách cố định **100 khe luật (`R1..R100`)**. Khi biên dịch (`F7`), các khối đồ họa trên bản vẽ sẽ tiêu thụ số lượng khe luật tương ứng:

| Loại Khối trên Bản vẽ Sơ đồ Logic | Số khe Luật Tiêu thụ | Ghi chú Vận hành |
| :--- | :---: | :--- |
| **Chuỗi mạch logic đơn thuần** (`Input -> Trigger -> Action`) | **1 Luật** | Chiếm 1 khe nhớ thực thi chuẩn. |
| **Khối Hẹn giờ** ([`TON`](/vi/blocks/ton), [`TOF`](/vi/blocks/tof), [`TP`](/vi/blocks/tp)) | **2 Luật** | 1 luật kiểm soát thời gian đếm và 1 luật kích hoạt ngõ ra. |
| **Khối Bộ đếm** ([`CTU`](/vi/blocks/ctu), [`CTD`](/vi/blocks/ctd)) | **4 Luật** | Gồm các luật đếm xung, đặt lại (Reset) và so sánh ngưỡng. |
| **Kênh Modbus Master & MQTT** | *Không tốn khe luật* | Được quản lý riêng biệt tại các thẻ phân hệ con. |

### 2.2. Tham chiếu Thanh Công cụ, Phân hệ Con & Thanh Ngân sách

| Điều khiển / Thẻ Phân hệ | Vị trí | Chức năng & Hành vi Vận hành |
| :--- | :--- | :--- |
| **Thanh Ngân sách Luật** (`0..100` / `%`) | Góc trên bên phải | Hiển thị tỷ lệ sử dụng bộ nhớ luật theo thời gian thực (`ActiveCount / 100` và `MemoryUsagePercent`), giúp kiểm soát dư địa mở rộng của bộ điều khiển. |
| **New Rule / Edit on Canvas** | Góc trên bên trái | Khởi tạo nhanh dòng luật dạng bảng hoặc chuyển thẳng sang mạng khối tương ứng trên [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab) (`Ctrl+2`). |
| **Phân hệ 1: `Logic Rules`** | Thẻ con mặc định | Liệt kê toàn bộ danh sách luật ECA (`R1..R100` ứng với các khe nhớ `0..99`) được thực thi theo thứ tự chỉ số tăng dần nghiêm ngặt. |
| **Phân hệ 2: `Modbus Master`** | Thẻ con thứ hai | Tổng hợp toàn bộ các kênh đọc định kỳ `MB_READ` và lệnh ghi chấp hành `MB_WRITE` trên bus RS-485 (Slave ID, Function Code, Thanh ghi, Chu kỳ, Tag đích). |
| **Phân hệ 3: `MQTT Services`** | Thẻ con thứ ba | Tổng hợp toàn bộ chủ đề phát dữ liệu `MQTT_PUB` (Topic, QoS, mẫu Payload) và các khóa nhận lệnh điều khiển JSON `MQTT_SUB`. |
| **Phân hệ 4: `Function Blocks`** | Thẻ con thứ tư | Liệt kê danh sách các thực thể khối định thời và bộ đếm có trạng thái (`TON`, `TOF`, `TP`, `CYCLE_TIMER`, `CTU`, `CTD`) cùng giá trị đặt (`PT`/`PV`). |
| **Nút Lọc Trạng thái** (`All` / `Active` / `Disabled`) | Thanh công cụ lọc | Lọc riêng các luật đang kích hoạt chạy thực tế hoặc các luật đang tạm ngắt trong quá trình thử nghiệm hiện trường. |
| **Deduplicate Rules** | Thanh công cụ lọc | Quét toàn bộ bảng để phát hiện các luật trùng khớp hoàn toàn chữ ký `Trigger + Comparator + Guard + Action`, tự động loại bỏ bản sao thừa và đánh lại số thứ tự `R1..Rn`. |
| **Upload from Device (`F6`) & Export CSV** | Thanh công cụ lọc | Tải ngược bảng luật thực tế từ **SynaptiX Edge Controller** đang kết nối hoặc xuất ma trận Nhân – Quả (Cause & Effect) ra tệp CSV phục vụ hồ sơ nghiệm thu. |
| **Ô Tìm kiếm Tức thời** | Phía phải thanh công cụ | Lọc tức thời theo Mã luật (`R12`), Tên biến (`AI0`), Bí danh Alias, nhãn Macro hoặc từ khóa trong câu diễn giải Narrative. |

### 2.3. Tham chiếu Cột Bảng Luật Logic (Logic Rules DataGrid)

| Tiêu đề Cột | Định dạng Hiển thị | Ý nghĩa Kỹ thuật & Đọc hiểu |
| :--- | :--- | :--- |
| **`Enabled`** | Ô tích chọn (Checkbox) | Bật hoặc tạm vô hiệu hóa khe luật (`0..99`) mà không cần xóa cấu hình hay cắt dây trên bản vẽ trực quan. |
| **`ID`** | Chỉ số Monospace (`R1`..`R100`) | Thứ tự ưu tiên quét tất định. Luật `R1` (khe `0`) luôn được đánh giá trước `R2` (khe `1`) trong cùng một vòng quét. |
| **`Trigger` & `Comparator / Threshold`** | Tóm tắt Tầng 1 & Tầng 2 | Hiển thị biến nguồn giám sát, chế độ kích hoạt (`ON_RISE`, `ON_FALL`, `ON_CHANGE`, `INTERVAL`, `TIME_WINDOW`), thời gian lọc chống dội (`ForMs`), toán tử quan hệ (`==`, `!=`, `>`, `>=`, `<`, `<=`, `BETWEEN`) và ngưỡng so sánh (`INT32` hoặc `REAL`). |
| **`Guard`** | Tầng 3 — Khóa Liên động | Hiển thị biến điều kiện an toàn và mức logic yêu cầu (`== 1` thuận hoặc `== 0` đảo), hoặc hiển thị `--` nếu luật không qua cổng khóa. |
| **`Action`** | Tầng 4 — Lệnh Chấp hành | Hiển thị mã lệnh đầu ra (`SET_TAG`, `TOGGLE_TAG`, `INC_COUNTER`, `WRITE_REMOTE`, `SEND_ALARM`, `LOG_EVENT`), biến đích và tham số ghi. |
| **`Macro`** | Huy hiệu Truy vết Nguồn | Cho biết dòng luật này được sinh tự động từ khối hàm IEC cấp cao nào (ví dụ: `TON #1`, `CTU #2`) hay từ chuỗi mạch đơn thuần. |
| **`Narrative`** | Câu Đặc tả Tự nhiên | Câu thuyết minh nguyên lý Nhân – Quả tự động sinh ra để kỹ sư đối chiếu nhanh với quy trình vận hành chuẩn (SOP). |
| **`Actions`** | Nút Thao tác Dòng | Cung cấp nút **Edit in Logic Graph** (nhảy về đúng nhánh mạch nguồn trên Canvas) và nút **Delete Rule** (xóa luật). |
| **Khay Logic Flow Inspector** | Thanh chi tiết dưới cùng | Trực quan hóa dòng luật đang chọn thành sơ đồ 4 khối nối tiếp (`Trigger → Comparator → Guard → Action`) giúp rà soát từng tham số rõ ràng. |

---

## 3. Quy trình Thao tác Chuẩn từng Bước

### Quy trình 3.1: Kiểm toán Tập Luật & Ngân sách Phần cứng Sau khi Biên dịch

1. Sau khi hoàn tất thiết kế hoặc chỉnh sửa mạch trên [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab) (`Ctrl+2`), nhấn phím `F7` (**Compile Rules**).
2. Nhấn `Ctrl+4` để chuyển sang **Bảng Luật (Rule Table)**.
3. Quan sát **Thanh Ngân sách Luật** ở góc trên bên phải để đảm bảo tổng số luật sử dụng nằm trong giới hạn **100 luật** (`0..99`) của phần cứng.
4. Nhấp chọn từng dòng luật quan trọng để mở khay **Logic Flow Inspector** bên dưới, đối chiếu kỹ biến nguồn, kiểu dữ liệu ngưỡng (`INT32` hay `REAL`), cực tính khóa an toàn `Guard` và biến tác động đầu ra.
5. Chuyển lần lượt qua các thẻ con **Modbus Master**, **MQTT Services** và **Function Blocks** để rà soát tổng thể chu kỳ đọc RS-485 và danh sách chủ đề MQTT trước khi nạp xuống thiết bị.

### Quy trình 3.2: Khử Trùng lặp Nhánh Mạch & Xuất Ma trận Nghiệm thu CSV

1. Khi ghép nối nhiều mẫu thiết kế từ [Thư viện Mẫu — Blueprints](/vi/workspaces/blueprints-tab) hoặc sao chép nhiều cụm nhánh lớn, mở **Rule Table** (`Ctrl+4`) và nhấn nút **Deduplicate Rules**.
2. **SynaptiX IDE** tự động đối chiếu chữ ký ngữ nghĩa của từng luật, loại bỏ các dòng trùng lặp dư thừa và sắp xếp lại chỉ số `R1..Rn` liền mạch.
3. Nhấn nút **Export CSV** trên thanh công cụ để xuất bảng đặc tả ma trận điều khiển đính kèm vào hồ sơ hoàn công tủ điều khiển.

---

## 4. Thực hành Kỹ thuật Tốt nhất & Xử lý Tình huống Biên

::: warning TÍNH TẤT ĐỊNH CỦA THỨ TỰ QUÉT TUẦN TỰ (`R1` → `R100`)
**SynaptiX Runtime** luôn quét các luật đang bật theo đúng thứ tự chỉ số tăng dần từ khe `0` (`R1`) đến khe `99` (`R100`) trong mỗi chu kỳ quét. Nếu cả luật `R5` (ghi `DO0 = 1`) và luật `R12` (ghi `DO0 = 0`) cùng thỏa mãn điều kiện trong cùng một chu kỳ máy, nguyên tắc **lệnh ghi sau cùng quyết định (last-writer-wins)** trên vùng đệm ảnh ngõ ra sẽ áp dụng (`DO0` mang giá trị `0` khi chốt chu kỳ). Hãy gõ tên ngõ ra (ví dụ `DO0`) vào ô **Search** của **Rule Table** để rà soát toàn bộ các luật cùng tác động lên một chấp hành.
:::

- **Tính toán Dung lượng khi Dùng Khối Hàm Trạng thái (Macro Expansion):** Do mỗi khối hẹn giờ `TON`/`TOF`/`TP` sử dụng **2 khe luật** và mỗi bộ đếm `CTU`/`CTD` sử dụng **4 khe luật**, một dự án gồm 10 bộ hẹn giờ và 5 bộ đếm sẽ chiếm `10 × 2 + 5 × 4 = 40` khe luật, còn lại **60 khe luật** cho các nhánh điều khiển trực tiếp. Hãy dựa vào cột huy hiệu **`Macro`** để nhận diện chính xác nhóm luật thuộc về từng khối hàm.
- **Tạm Cô lập Luật Không Phá hủy Bản vẽ khi Nghiệm thu (Commissioning Mute):** Khi cần tạm ngắt còi báo động hoặc băng tải kế tiếp để hiệu chuẩn cảm biến tại hiện trường, bạn chỉ cần bỏ tích cột **`Enabled`** của dòng luật đó trong **Rule Table** rồi nhấn `Ctrl+F5` để nạp—toàn bộ dây nối trên bản vẽ Logic Graph vẫn được giữ nguyên vẹn để bật lại ngay sau khi hiệu chuẩn xong.

---

## Tài liệu Liên quan

- [Chu trình Thực thi Tất định](/vi/concepts/execution-pipeline)
- [2. Sơ đồ Logic — Logic Graph (`Ctrl+2`)](/vi/workspaces/logic-graph-tab)
- [6. Triển khai — Deploy (`Ctrl+6`)](/vi/workspaces/deploy-tab)
- [Tổng quan Danh mục & Quy ước Chân](/vi/blocks/overview)
- [Từ điển Macro & Giới hạn Tài nguyên](/vi/appendices/macro-tokens-and-limits)
