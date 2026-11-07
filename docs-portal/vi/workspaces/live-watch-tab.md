# 7. Giám sát Trực tiếp — Live Watch & Trends (`Ctrl+7`)

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-WS-007</span>
  <span><strong>Không gian làm việc:</strong> Tab 7 &bull; Phím tắt <code>Ctrl+7</code></span>
  <span><strong>Phạm vi:</strong> Luồng Telemetry Phản ứng 20 FPS, Đồ thị Sparkline, Chất lượng GOOD vs STALE &amp; Cưỡng bức Nghiệm thu</span>
  <span><strong>Tiêu chuẩn:</strong> IEC 61508-3 &bull; IEC/IEEE 82079-1 &bull; ANSI/ISA-101 &bull; ANSI Z535.6</span>
</div>

---

## 1. Mục đích & Vai trò Vận hành

**Không gian Giám sát Trực tiếp — Live Watch & Trends** (`Ctrl+7`) là bàn điều khiển trung tâm phục vụ giám sát dữ liệu thời gian thực, chẩn đoán chất lượng tín hiệu và kích hoạt thử nghiệm cơ cấu chấp hành của **SynaptiX IDE**. Được thiết kế dựa trên học thuyết **Sự thật Thị giác (Visual Truth — ANSI/ISA-101)** cùng kiến trúc truyền luồng phản ứng hướng sự kiện (Event-Driven Reactive Stream), không gian này phản ánh trung thực 1:1 trạng thái bộ nhớ vật lý và sức khỏe chu kỳ quét của **SynaptiX Edge Controller** (hoặc bộ mô phỏng ảo).

Trong quá trình kiểm tra đấu dây tủ điện (I/O loop check), tinh chỉnh thông số công nghệ và xử lý sự cố dây chuyền đang chạy, **Live Watch** đảm nhiệm bốn vai trò kỹ thuật cốt lõi:

1. **Luồng Telemetry Phản ứng 20 FPS Chống Đơ Giao diện:** Tiếp nhận dữ liệu thanh ghi tốc độ cao qua kênh truyền nội bộ hướng sự kiện và gộp khung hình thông minh (`50 ms` / `20 FPS` trên giao diện kết hợp làm mới thống kê `5 Hz`), đảm bảo bảng dữ liệu hàng trăm biến luôn mượt mà tuyệt đối.
2. **Đồ thị Xu hướng Ngắn Sparkline & Thống kê Phiên:** Vẽ trực tiếp đường biến thiên tín hiệu ngay trong từng dòng bảng kèm các chỉ số thống kê phiên (`Min`, `Max`, `Avg`, `TransitionCount`) và mã thập lục phân 32-bit thô (raw hex) để phát hiện tức thì nhiễu xung hoặc dội tiếp điểm cơ khí.
3. **Phân định Rạch ròi Chất lượng Tín hiệu `GOOD` và `STALE`:** Tách biệt rõ ràng giữa dữ liệu tươi vừa xác thực (`GOOD`) với dữ liệu cũ do rớt mạng/rút cáp (`STALE` — làm mờ dòng và gắn hậu tố `[Last]`), ngăn chặn tuyệt đối việc người vận hành lầm tưởng giá trị đóng băng cuối cùng là trạng thái an toàn hiện tại của máy.
4. **Ghi / Cưỡng bức Tín hiệu Nghiệm thu Có Bảo vệ Thời hạn (Safety Lease):** Cho phép kỹ sư có thẩm quyền bật **Manual Commissioning Mode** để đóng cắt thử ngõ ra (`DO`) và tinh chỉnh thanh ghi nội bộ (`VFLAG`, `VREG`, `VREG_RETAIN`, `COUNTER`) dưới sự giám sát của bộ đếm canh gác **Safety Lease** và cơ chế xác nhận lưu Flash tường minh.

---

## 2. Bố cục Trực quan & Tham chiếu Điều khiển

![Không gian Giám sát Trực tiếp và Đồ thị Xu hướng Live Watch SynaptiX](/images/synaptix_live_watch.png)

Giao diện **Live Watch** kết hợp đồng bộ giữa **Dải Chẩn đoán Sức khỏe Bộ điều khiển**, **Thanh Công cụ Lọc & Ghim Watchlist**, bảng dữ liệu mật độ cao **Live Telemetry & Sparkline DataGrid** và **Thanh Điều khiển Nghiệm thu Thủ công An toàn**.

### 2.1. Kiến trúc Luồng Telemetry Phản ứng (`50 ms` / `20 FPS`)

Khác với các phần mềm cổ điển dùng vòng lặp hỏi vòng gây treo giao diện hoặc chiếm dụng băng thông nối tiếp khi chạy nền, **SynaptiX IDE** chỉ kích hoạt luồng thu thập tốc độ cao đúng khi người dùng mở **Live Watch** (`Ctrl+7`) hoặc bật **Gỡ lỗi Trực tuyến** (`F8`):

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>NGUỒN 10ms</span>
      </div>
      <span class="synx-node-status is-live">● 100 Hz</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">SynaptiX Runtime</span>
        <span class="synx-node-alias">Quét Phần Cứng</span>
        <span class="synx-node-val">Hard Real-Time</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Push</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>BỘ ĐỆM</span>
      </div>
      <span class="synx-node-status">ATOMIC</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">IRuntimeStateStore</span>
        <span class="synx-node-alias">Bộ Nhớ Không Khóa</span>
        <span class="synx-node-val">Snapshots 20 FPS</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Render</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>GIAO DIỆN</span>
      </div>
      <span class="synx-node-status">20 FPS</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Live Watch UI</span>
        <span class="synx-node-alias">WPF DataGrid</span>
        <span class="synx-node-val">Đồ Thị Sparkline</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Truth</span>
  </div>

  <div class="synx-canvas-node is-step is-done">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 L13.5,4 L13.5,9.5 Q13.5,13.5 8,15 Q2.5,13.5 2.5,9.5 L2.5,4 Z" /></svg>
        <span>CHẤT LƯỢNG</span>
      </div>
      <span class="synx-node-status is-active">● VERIFIED</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Sự Thật Thị Giác</span>
        <span class="synx-node-alias">Bảo Vệ Người Dùng</span>
        <span class="synx-node-val">GOOD vs STALE</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

### 2.2. Tham chiếu Dải Sức khỏe Phần cứng & Thanh Công cụ

| Điều khiển / Chỉ số Chẩn đoán | Vị trí | Ý nghĩa Kỹ thuật & Hành vi Vận hành |
| :--- | :--- | :--- |
| **Chỉ số Sức khỏe Bộ điều khiển** (`Uptime`, `CPU`, `RAM`, `Scan Time`) | Dải thông tin trên cùng | Hiển thị thời gian chạy liên tục (`Xd Yh Zm Ws`), tải vi xử lý (`%`), mức dùng SRAM (`%`), thời gian quét hiện tại/cực đại (`ms`) và **Nguyên nhân Khởi động lại Gần nhất** (`Power-On`, `Hardware Pin`, `Software Command`, `Watchdog Timer`, `Brownout Detector`). |
| **Huy hiệu Trực tuyến & Đồng hồ Poll** | Góc phải thanh công cụ | Hiển thị trạng thái phiên (`ONLINE` / `OFFLINE`) và nhãn thời gian chính xác tới mili-giây (`HH:mm:ss.fff`) của gói dữ liệu hợp lệ gần nhất. |
| **Ô Tìm kiếm Tức thời** | Góc trái thanh công cụ | Lọc dòng trực tiếp theo `Tag Name`, `Alias`, địa chỉ hex `Modbus Address`, `Data Type` hoặc `Index`. |
| **Bộ lọc Ghim `★ Watchlist`** | Thanh công cụ trên | Chỉ hiển thị các biến trọng yếu đã đánh dấu sao (`★`). Danh sách ghim được lưu bền vững trong tệp dự án `.synx` và hỗ trợ menu chuột phải (**Pin / Unpin / Clear Watchlist**). |
| **Nhãn Lọc Phân vùng (Filter Chips)** | Dải lọc phía trên | Lọc nhanh theo nhóm `ALL`, `DI`, `DO`, `AI`, `VFLAG`, `VREG`, `RETAIN` (`VREG_RETAIN`) hoặc `COUNTER`. |
| **Nút `Manual Commissioning Mode`** | Nút tác vụ thanh công cụ | Bật hoặc thoát chế độ cưỡng bức/ghi thử nghiệm hiện trường (bắt buộc xác nhận hộp thoại cảnh báo an toàn công nghiệp khi kích hoạt). |
| **Thanh Cảnh báo Lưu trữ Flash** (`Commit Retain` / `Discard Retain`) | Dải thông báo ngữ cảnh | Tự động xuất hiện ngay khi có biến `VREG_RETAIN` bị thay đổi trong chế độ Manual Mode, giúp kỹ sư chủ động chọn ghi vĩnh viễn xuống Flash hoặc hủy bỏ giá trị thử nghiệm. |

### 2.3. Tham chiếu Cột Bảng Telemetry, Trạng thái Chất lượng & Công cụ Cưỡng bức

| Tên Cột | Thành phần UI & Kiểu Chữ | Chi tiết Hiển thị & Thao tác |
| :--- | :--- | :--- |
| **`★` (Ghim)** | Nút Ngôi sao Tương tác | Đánh dấu ghim các biến quan trọng vào danh sách theo dõi nhanh `Watchlist` của dự án `.synx`. |
| **`#` & `Tag Name`** | Chỉ số & Mã cứng Monospace | Số thứ tự biến tất định và mã định danh phần cứng (`DI0`, `DO0`, `AI0`, `VREG0`). |
| **`Description / Alias`** | Văn bản căn trái | Tên gợi nhớ công nghệ P&ID đồng bộ trực tiếp từ [Danh mục Biến — Tag Catalog](/vi/workspaces/tag-catalog-tab) (`Ctrl+3`). |
| **`Modbus Address` & `Data Type`** | Nhãn Monospace & Huy hiệu Kiểu | Hiển thị địa chỉ thanh ghi SCADA và hợp đồng kiểu vật lý (`BOOL`, `INT32`, `REAL`). |
| **`Live Value` & `Sparkline`** | Đèn LED + Giá trị Chuẩn + Đồ thị Mini | &bull; **`BOOL`:** Hiển thị rõ `● 1` (xanh lá hoạt động) hoặc `○ 0` (xám trung tính) kèm số lần chuyển trạng thái.<br/>&bull; **`INT32` / `REAL`:** Số Monospace căn phải + đồ thị **Sparkline** thời gian thực; rê chuột hiển thị mã hex 32-bit thô cùng `Min` / `Max` / `Avg` toàn phiên.<br/>&bull; **Nhãn Trạng thái:** Gắn tiền tố `[F]` (màu vàng cam) khi đang cưỡng bức; gắn hậu tố `[Last]` (làm mờ) khi tín hiệu rơi vào trạng thái `STALE`. |
| **`Manual Control / Force`** | Công cụ Tác động An toàn Kiểu | &bull; **`DI` / `AI` (Chỉ đọc):** Hiển thị `--` (ngõ vào vật lý không cho phép cưỡng bức giả bằng phần mềm).<br/>&bull; **`DO` / `VFLAG`:** Công tắc trượt nhị phân `0 / 1` chuẩn công nghiệp + nút nhả cưỡng bức `Release` (`✕`).<br/>&bull; **`VREG` / `VREG_RETAIN` / `COUNTER`:** Ô nhập số tích hợp Bad-Data Defense + nút `Set` + nút `Release` (`✕`). |

### 2.4. Bảng Chân lý Chất lượng Tín hiệu (`GOOD` so với `STALE`)

| Trạng thái Chất lượng | Điều kiện Phát sinh | Biểu hiện Trực quan trên Live Watch | Hành động Bắt buộc của Kỹ sư |
| :--- | :--- | :--- | :--- |
| **`GOOD`** | Gói dữ liệu phản hồi mới nhất vượt qua kiểm tra CRC đúng hạn định. | Độ tương phản sắc nét 100%, đèn LED xanh/xám rõ ràng, đồng hồ `HH:mm:ss.fff` nhảy liên tục, đường Sparkline cập nhật mượt. | Cho phép giám sát vận hành và thực hiện thử nghiệm nghiệm thu bình thường. |
| **`STALE` / `UNKNOWN`** | Tuột cáp USB/Ethernet, sập nguồn tủ điện hoặc quá hạn phản hồi gói tin (`Reconnecting`). | Toàn bộ dòng dữ liệu giảm độ sáng làm mờ ngay lập tức, giá trị gắn hậu tố **`[Last]`**, đồng hồ Poll dừng tại thời điểm hợp lệ cuối và khóa toàn bộ nút ghi/cưỡng bức. | **Tuyệt đối KHÔNG dựa vào giá trị `[Last]` để phán đoán an toàn cơ khí.** Phải khôi phục kết nối (`F4`) về `GOOD` trước khi thao tác. |

---

## 3. Quy trình Thao tác Chuẩn từng Bước

### Quy trình 3.1: Thiết lập Danh sách Ghim Watchlist & Chẩn đoán Nhiễu qua Sparkline

1. Kết nối tới **SynaptiX Edge Controller** (`F4`) và nhấn `Ctrl+7` để mở **Live Watch & Trends**.
2. Kiểm tra huy hiệu góc trên hiển thị `ONLINE`, đồng hồ mili-giây `Last Poll` đang chạy đều và dải **Controller Health** báo cáo thời gian quét ổn định (thường `< 5 ms`) không có cảnh báo khởi động lại bất thường.
3. Gõ tên cụm thiết bị vào ô **Search** hoặc bấm nhãn lọc (`AI`, `DO`, `VREG`) để tìm các cảm biến/chấp hành đang cần kiểm tra thông mạch, sau đó nhấp biểu tượng ngôi sao **`★`** ở đầu mỗi dòng.
4. Nhấn nút lọc **`★ Watchlist`** trên thanh công cụ để thu gọn màn hình chỉ còn đúng các tín hiệu trọng tâm, rồi nhấn `Ctrl+S` để lưu danh sách ghim vào tệp dự án `.synx`.
5. Quan sát đường đồ thị **Sparkline** trên các kênh tương tự/thanh ghi và rê chuột vào ô **`Live Value`** để kiểm tra mã hex 32-bit thô cùng biên độ dao động `Min`, `Max`, `Avg`.

### Quy trình 3.2: Cưỡng bức Ngõ ra & Tinh chỉnh Ngưỡng An toàn trong Chế độ Nghiệm thu

::: danger CẢNH BÁO NGUY HIỂM CHUYỂN ĐỘNG CƠ KHÍ & ĐÓNG ĐIỆN TRỰC TIẾP
Thao tác cưỡng bức ngõ ra số (`DO`) hoặc ghi trực tiếp vào thanh ghi điều khiển (`VFLAG`, `VREG`, `VREG_RETAIN`) trên **SynaptiX Edge Controller** đang nối tải thực tế sẽ đóng điện trực tiếp vào rơ-le, khởi động từ, van điện từ, động cơ và thanh đốt—bỏ qua các điều kiện cảm biến đầu vào thông thường!
- Phải quan sát trực tiếp hiện trường và đảm bảo không có nhân sự nào đứng trong vùng chuyển động cơ khí trước khi bật **Manual Commissioning Mode**.
- Luôn đảm bảo mạch Dừng khẩn cấp phần cứng (Hardwired E-Stop) và quy trình khóa cô lập nguồn (LOTO) hoạt động độc lập hoàn toàn với phần mềm.
:::

::: warning CƠ CHẾ TỰ ĐỘNG NHẢ CƯỠNG BỨC THEO THỜI HẠN AN TOÀN (SAFETY LEASE)
Mọi lệnh cưỡng bức nghiệm thu đều được duy trì bằng xung nhịp gia hạn **Safety Lease** liên tục giữa **SynaptiX IDE** và **SynaptiX Runtime**. Nếu máy tính kỹ thuật bị gập màn hình ngủ, tuột cáp USB hoặc rớt mạng Ethernet, bộ đếm thời hạn phần cứng dưới bộ điều khiển sẽ tự động hết hạn và giải phóng các trạng thái cưỡng bức nhằm ngăn chặn hiểm họa kẹt treo cơ cấu chấp hành không người giám sát.
:::

1. Tại màn hình **Live Watch** (`Ctrl+7`), nhấn nút **`Manual Commissioning Mode`** trên thanh công cụ, đọc kỹ và xác nhận hộp thoại cảnh báo an toàn vận hành.
2. **Thử nghiệm Đóng/Cắt Ngõ ra Số (`DO`) hoặc Cờ Nội bộ (`VFLAG`):**
   - Gạt công tắc nhị phân **`0 / 1`** tại cột **`Manual Control / Force`**.
   - Quan sát tiền tố **`[F]`** màu vàng cam xuất hiện tại cột giá trị và kiểm tra rơ-le/đèn báo ngoài tủ điện tác động đúng thiết kế.
   - Nhấn nút **`Release` (`✕`)** ngay cạnh công tắc để trả quyền điều khiển biến đó về lại cho chu trình quét luật ECA tự động.
3. **Tinh chỉnh Thanh ghi Số (`VREG`, `VREG_RETAIN` hoặc `COUNTER`):**
   - Nhập giá trị số đúng kiểu vào ô nhập trên dòng tương ứng (được bảo vệ bởi Bad-Data Defense: tự động chặn số lẻ `"25.5"` trên `INT32`; chuẩn hóa `"25"` thành `"25.0"` trên `REAL`) rồi nhấn **`Set`**.
   - Nếu bạn vừa tinh chỉnh thông số cài đặt thuộc nhóm **`VREG_RETAIN`** và muốn lưu vĩnh viễn vào bộ nhớ Flash chống mất điện, hãy nhấn **`Commit Retain`** trên thanh thông báo nổi lên (hoặc chọn **`Discard Retain`** để khôi phục lại giá trị cũ).
4. Ngay sau khi hoàn tất nghiệm thu từng cụm mạch, hãy nhả toàn bộ các biến đang mang nhãn `[F]` và tắt **`Manual Commissioning Mode`** về trạng thái **OFF** trước khi bàn giao tủ điện cho ca sản xuất.

---

## 4. Thực hành Kỹ thuật Tốt nhất & Xử lý Tình huống Biên

- **Vì sao Không thể Cưỡng bức `DI` và `AI` trên Live Watch:** Tuân thủ nghiêm ngặt nguyên lý chân lý phần cứng (Hardware Ground Truth), các cầu đấu ngõ vào vật lý (`DI`, `AI`) luôn phản ánh trung thực mức điện áp/dòng điện thực tế trên chân vi điều khiển (`IsForceable = false`). Nếu muốn thử nghiệm nhánh logic phía sau mà không tác động cảm biến thật ngoài hiện trường, hãy kiểm thử ngoại tuyến bằng [Bộ Mô phỏng Chu kỳ Chính xác (`F5`)](/vi/tools/simulator-and-live-debug) hoặc thiết kế song song một biến cờ nghiệm thu `VFLAG`.
- **Chẩn đoán Sự cố Nguồn Tủ điện qua `Last Reset Reason`:** Khi xử lý các ca máy tự dừng chập chờn không rõ nguyên nhân, hãy kiểm tra ngay chỉ báo **Last Reset Reason** trên dải Controller Health. Nếu thiết bị ghi nhận `Brownout Detector`, nguyên nhân gốc nằm ở bộ nguồn tổ ong `24 VDC` bị sụt áp khi đóng tải lớn chứ không phải lỗi chương trình điều khiển.
- **Kết hợp Song song Live Watch với Gỡ lỗi Trực tuyến (`F8`):** Trong khi `Ctrl+7` tối ưu cho việc theo dõi bảng tổng hợp và đồ thị Sparkline, việc nhấn phím `F8` ngay trên [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab) (`Ctrl+2`) sẽ phủ trực tiếp cùng một luồng dữ liệu `IRuntimeStateStore` lên từng chân khối hàm và đường dây vuông góc trên bản vẽ.

---

## Tài liệu Liên quan

- [Kiến trúc Bộ nhớ Tag & Kiểu Dữ liệu](/vi/concepts/data-model-and-types)
- [3. Danh mục Biến — Tag Catalog (`Ctrl+3`)](/vi/workspaces/tag-catalog-tab)
- [6. Triển khai — Deploy (`Ctrl+6`)](/vi/workspaces/deploy-tab)
- [Mô phỏng Offline & Gỡ lỗi Trực tuyến](/vi/tools/simulator-and-live-debug)
- [Bảng Mã lỗi & Chẩn đoán Sự cố](/vi/appendices/error-codes-and-troubleshooting)
