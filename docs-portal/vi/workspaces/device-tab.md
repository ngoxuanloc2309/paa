# 1. Không gian Thiết bị — Device (`Ctrl+1`)

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-WS-001</span>
  <span><strong>Không gian làm việc:</strong> Tab 1 &bull; Phím tắt <code>Ctrl+1</code></span>
  <span><strong>Phạm vi:</strong> Định danh Phần cứng, Kết nối Truyền thông, Đồng bộ RTC &amp; Cấu hình Ngoại vi</span>
  <span><strong>Tiêu chuẩn:</strong> IEC/IEEE 82079-1 &bull; ANSI/ISA-101</span>
</div>

---

## 1. Mục đích & Vai trò Vận hành

**Không gian Thiết bị — Device Workspace** (`Ctrl+1`) là trung tâm nghiệm thu phần cứng và quản trị kết nối truyền thông của **SynaptiX IDE**. Trước khi thiết kế các mạng luật điều khiển tất định hoặc giám sát tín hiệu hiện trường, kỹ sư sử dụng không gian này để thiết lập kênh giao tiếp vật lý hoặc mô phỏng, kiểm tra năng lực phần cứng mục tiêu, đồng bộ đồng hồ thời gian thực (RTC) và cấu hình các bus ngoại vi tích hợp trên bo mạch.

Xét theo vòng đời kỹ thuật nhà máy, **Device Workspace** đảm nhiệm bốn vai trò cốt lõi:

1. **Lựa chọn Giao thức & Quản lý Phiên kết nối (`F4`):** Chuyển đổi tức thời giữa cáp cấu hình nối tiếp cục bộ (**USB CDC**), mạng Ethernet công nghiệp (**Modbus TCP**), hoặc bộ mô phỏng chu kỳ chính xác tích hợp sẵn (`SIMULATOR (VIRTUAL)`), đi kèm cơ chế tự động khôi phục phiên khi đứt cáp tạm thời.
2. **Nhận diện Phần cứng & Khám phá Năng lực Thiết bị:** Truy vấn **SynaptiX Edge Controller** để đọc phân lớp thiết bị, cấu hình I/O vật lý, phiên bản firmware, chuẩn giao thức truyền thông và hạn mức tài nguyên—tự động đồng bộ [Danh mục Biến — Tag Catalog](/vi/workspaces/tag-catalog-tab) và hộp công cụ khối hàm trên [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab) khớp tuyệt đối với phần cứng thực tế.
3. **Đồng bộ Đồng hồ Thời gian Thực (RTC) & Múi giờ:** Đọc vi mạch RTC có pin nuôi dự phòng (hoặc bộ đếm `SysTick`), chẩn đoán trạng thái pin CMOS/nguồn nuôi và ghi chuẩn thời gian Epoch UTC kèm độ lệch múi giờ mà không tự ý ghi đè lịch vận hành của nhà máy khi vừa cắm cáp.
4. **Cấu hình Ngoại vi & DTU Hợp nhất:** Tích hợp **Device Configuration Studio** bố cục 3 cột ngay trong Tab 1 để thiết lập cổng RS-485 Modbus, mạng Wi-Fi, kết nối đám mây MQTT/TLS và chu kỳ đóng gói bản tin telemetry.

::: tip GIẢ LẬP HỒ SƠ PHẦN CỨNG KHI THIẾT KẾ OFFLINE
Khi thiết kế chương trình ngoại tuyến chưa cắm phần cứng thực tế, bạn có thể chọn **Hồ sơ Thiết bị (Device Preset)** (`Remote I/O`, `IIoT Gateway`, `Datalogger`, hoặc `Edge Controller`) trên thanh công cụ hoặc kết nối vào `SIMULATOR (VIRTUAL)`. **SynaptiX IDE** sẽ áp dụng ngay số lượng chân I/O và cơ chế khóa/mở khối hàm tương ứng để bản vẽ offline tương thích 1:1 với thiết bị ngoài tủ điện.
:::

---

## 2. Bố cục Trực quan & Tham chiếu Điều khiển

Quy trình làm việc tại **Device Workspace** kết hợp xuyên suốt giữa **Thanh Kết nối Nhanh (Top Quick-Connect Bar)**, bảng **Định danh & Hồ sơ Phần cứng (Device Identity)**, hộp thoại **Đồng bộ Đồng hồ RTC** và không gian **Device Configuration Studio 3 Cột** bên trong Tab 1.

### 2.1. Bảng Định danh Thiết bị & Hồ sơ Phần cứng (Device Identity)

![Định danh Thiết bị và Hồ sơ Phần cứng SynaptiX](/images/synaptix_device_info.png)

Ngay khi bắt tay kết nối thành công bằng phím `F4` (hoặc mở menu **Device → Device Info**), **SynaptiX IDE** truy vấn **SynaptiX Device** và hiển thị hợp đồng phần cứng qua bốn nhóm thông số chuẩn hóa:

| Trường Thông tin / Điều khiển | Khu vực Giao diện | Mô tả & Hành vi Vận hành |
| :--- | :--- | :--- |
| **Chuyển mạch Giao thức** (`USB CDC` / `Modbus TCP`) | Thanh Kết nối Nhanh | Nút gạt chọn phương thức truyền dẫn. Chế độ `USB CDC` hiển thị danh sách cổng COM và nút làm mới; chế độ `Modbus TCP` hiển thị ô nhập địa chỉ `Host : Port` (mặc định `127.0.0.1 : 502`). |
| **Bộ chọn Cổng Đích / Endpoint** | Thanh Kết nối Nhanh | Liệt kê các cổng nối tiếp USB CDC trên Windows cùng mục ảo `SIMULATOR (VIRTUAL)` ở chế độ Serial, hoặc địa chỉ IPv4 và cổng TCP ở chế độ Ethernet. |
| **Nút Kết nối / Ngắt kết nối (`F4`)** | Thanh Kết nối Nhanh | Khởi tạo hoặc đóng phiên truyền thông. Phản hồi rõ 5 trạng thái tất định: `Disconnected`, `Connecting`, `Connected`, `Reconnecting` (tự động thử lại khi nhiễu/rút cáp) và `Restarting`. |
| **Nhãn Dòng Thiết bị & Phân lớp** | Tiêu đề Định danh | Hiển thị phân lớp phần cứng (`Remote I/O`, `Gateway`, `Datalogger`, hoặc `Controller`) cùng biến thể cấu hình I/O vật lý (ví dụ: `8DI / 8DO / 4AI` hoặc `16DI / 16DO`). |
| **Phiên bản Phần cứng & Firmware** | Thông số Phiên bản | Báo cáo phiên bản mạch in (`HW v...`), bản dựng firmware **SynaptiX Runtime** đang chạy (`FW v...`), chuẩn khung truyền (`Wire Profile V2`) và định dạng tập luật (`Rule Format v2.0`). |
| **Ma trận Tài nguyên Phần cứng** | Hạn mức Bộ nhớ | Hiển thị chính xác số lượng biến theo từng phân vùng (`DI`, `DO`, `AI`, `VFLAG`, `VREG`, `VREG_RETAIN`, `COUNTER`) và ngân sách luật ECA tối đa (`MaxRules = 100`). |
| **Chẩn đoán Kênh truyền & Slave ID** | Trạng thái Đường truyền | Hiển thị tên cổng đang mở, tốc độ truyền UART hoặc trạng thái socket TCP, địa chỉ Modbus Slave ID và độ ổn định phản hồi gói tin. |

### 2.2. Hộp thoại Đồng bộ Đồng hồ Thời gian Thực (RTC Sync)

![Hộp thoại Đồng bộ Đồng hồ Thời gian Thực RTC](/images/synaptix_rtc_sync.png)

Truy cập từ menu **Device → Sync Clock**, công cụ đồng bộ RTC đảm bảo độ chính xác thời gian thực cho các luật lịch biểu (`TIME_WINDOW`), bộ đếm ca sản xuất và nhãn thời gian chuẩn ISO-8601 trên gói tin MQTT:

| Điều khiển / Chỉ báo | Chức năng & Hành vi Kỹ thuật |
| :--- | :--- |
| **Nhãn Nguồn Xung nhịp** (`Hardware RTC IC` vs `Software Clock`) | Cho biết **SynaptiX Edge Controller** đang duy trì thời gian bằng vi mạch RTC thạch anh có pin nuôi độc lập hay bộ đếm mềm `SysTick` trên vi điều khiển. |
| **Cảnh báo Pin yếu / Mất nguồn RTC** | Phát cảnh báo rõ ràng nếu điện áp pin nuôi dự phòng xuống thấp hoặc thiết bị vừa mất nguồn trên dòng không có pin RTC, giúp kỹ sư kiểm tra trước khi chạy lịch tự động. |
| **Thời gian Hiện hành trên Thiết bị** | Đọc trực tiếp giờ nội bộ và độ lệch UTC đang lưu trong **SynaptiX Runtime** mà không tự ý ghi đè cấu hình của bộ điều khiển khi kết nối. |
| **Bộ chọn Múi giờ (Timezone Selector)** | Hỗ trợ chế độ `[Default] Current PC Time` (kế thừa múi giờ của máy tính kỹ thuật) hoặc chọn tường minh bất kỳ múi giờ tiêu chuẩn nào từ `UTC-12:00` đến `UTC+14:00`. |
| **Ghi đè Ngày/Giờ Thủ công** | Ô nhập định dạng `YYYY-MM-DD HH:mm:ss` tích hợp cơ chế chống dữ liệu sai (Bad-Data Defense), phục vụ kiểm thử kịch bản giao ca, qua nửa đêm hoặc hệ thống mạng cô lập (air-gapped). |
| **Khung Xem trước Trực tiếp (1 Hz)** | Cập nhật mỗi giây hiển thị chính xác thời gian cục bộ và độ lệch UTC sẽ được nạp xuống phần cứng khi nhấn nút **Sync Clock**. |

### 2.3. Không gian Cấu hình Thiết bị 3 Cột (Device Configuration Studio)

Phần thân chính của Tab 1 (`Ctrl+1`) tích hợp không gian cấu hình ngoại vi 3 cột trực quan:

| Phân vùng Giao diện | Phím tắt & Phân mục Con | Vai trò Vận hành |
| :--- | :--- | :--- |
| **Cột Trái — Điều hướng Phân hệ** | `Ctrl+Shift+B` (`RS-485 Modbus`)<br/>`Ctrl+Shift+W` (`Wi-Fi Network`)<br/>`Ctrl+Shift+M` (`Cloud Uplink`)<br/>`Timing & Payload` &bull; `Quick Wizard` | Hiển thị huy hiệu trạng thái sẵn sàng của từng giao diện phần cứng, mở trình thuật sĩ **Quick Wizard** 3 bước và cung cấp nút **Save Configuration**. |
| **Cột Giữa — Biểu mẫu Tham số** | Các thẻ cấu hình chuyên biệt theo phân hệ | Cung cấp trường nhập chuẩn hóa cho tốc độ Baud/Parity RS-485 (`9600`–`115200`), SSID/mật khẩu Wi-Fi, địa chỉ MQTT Broker/chứng chỉ TLS và chu kỳ phát JSON. |
| **Cột Phải — Thanh Kiểm tra Ngữ cảnh** | Bảng Phân tích & Sẵn sàng Thực thi | Cung cấp sơ đồ đấu dây nhanh, tóm tắt vùng thanh ghi Modbus Slave, ước tính kích thước gói tin và cảnh báo tiền kiểm tra. |
| **Lệnh Quản trị Thiết bị (Menu Device)** | `F6` (Upload from Device)<br/>`Ctrl+F5` (Atomic Deploy)<br/>`Reboot Device` &bull; `Factory Reset` | Đọc ngược toàn bộ cấu hình từ thiết bị lên IDE, nạp nguyên tử hai pha, khởi động lại mềm bộ điều khiển hoặc khôi phục cài đặt gốc (có hộp thoại xác nhận an toàn). |

---

## 3. Quy trình Thao tác Chuẩn từng Bước

### Quy trình 3.1: Kết nối tới Bộ Điều khiển Vật lý hoặc Bộ Mô phỏng Ảo

1. Nhấn `Ctrl+1` để mở **Không gian Thiết bị (Device Workspace)**.
2. Trên **Thanh Kết nối Nhanh**, chọn phương thức kết nối phù hợp:
   - **Kết nối Cáp USB Cục bộ:** Gạt sang `USB CDC`, nhấn biểu tượng **Làm mới cổng COM** nếu vừa cắm cáp và chọn đúng cổng `COMx` (hoặc chọn `SIMULATOR (VIRTUAL)` để chạy thử nghiệm không cần phần cứng).
   - **Kết nối Mạng Ethernet / Gateway:** Gạt sang `Modbus TCP` rồi nhập địa chỉ IPv4 và cổng TCP của thiết bị (mặc định `502`).
3. Nhấn phím `F4` (hoặc bấm nút **Connect**).
4. Quan sát huy hiệu trạng thái chuyển từ `Connecting` sang `Connected` (màu xanh lá chuẩn ISA-101) và kiểm tra bảng **Device Identity** để xác nhận phiên bản firmware cùng cấu hình I/O.
5. Xác nhận **SynaptiX IDE** đã tự động đồng bộ hồ sơ phần cứng trong [Danh mục Biến — Tag Catalog](/vi/workspaces/tag-catalog-tab) khớp với số kênh `DI`, `DO`, `AI` thực tế của thiết bị.

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>BƯỚC 1</span>
      </div>
      <span class="synx-node-status">MEDIUM</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Chọn Giao Thức</span>
        <span class="synx-node-alias">Cổng Kết Nối</span>
        <span class="synx-node-val">USB CDC / TCP :502</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Connect</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>BƯỚC 2</span>
      </div>
      <span class="synx-node-status">HANDSHAKE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Bắt Tay Kết Nối</span>
        <span class="synx-node-alias">Phím Nhanh F4</span>
        <span class="synx-node-val">Huy Hiệu Connected</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Read FW</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 L13.5,4 L13.5,9.5 Q13.5,13.5 8,15 Q2.5,13.5 2.5,9.5 L2.5,4 Z" /></svg>
        <span>BƯỚC 3</span>
      </div>
      <span class="synx-node-status">IDENTIFY</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Nhận Diện Target</span>
        <span class="synx-node-alias">Hồ Sơ Phần Cứng</span>
        <span class="synx-node-val">Đọc FW &amp; I/O Map</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Sync</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,13 L5,13 L5,9.5 L8,9.5 L8,6 L11,6 L11,3 L14,3" /></svg>
        <span>BƯỚC 4</span>
      </div>
      <span class="synx-node-status">SYNC</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Tự Động Đồng Bộ</span>
        <span class="synx-node-alias">Cập Nhật Danh Mục</span>
        <span class="synx-node-val">Tag Catalog &amp; Toolbox</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Ready</span>
  </div>

  <div class="synx-canvas-node is-step is-done">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>BƯỚC 5</span>
      </div>
      <span class="synx-node-status is-active">● READY</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Phiên Sẵn Sàng</span>
        <span class="synx-node-alias">Giám Sát &amp; Nạp</span>
        <span class="synx-node-val">Live Watch / Deploy</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

### Quy trình 3.2: Đồng bộ Đồng hồ Thời gian Thực (RTC) cho Thiết bị

1. Khi phiên kết nối đang ở trạng thái `Connected`, chọn menu **Device → Sync Clock** trên thanh lệnh chính.
2. Kiểm tra **Nhãn Nguồn Xung nhịp** và đảm bảo không có cảnh báo sụt áp pin dự phòng RTC.
3. Giữ nguyên tùy chọn `[Default] Current PC Time` nếu muốn đồng bộ theo giờ và múi giờ của máy tính kỹ thuật, hoặc chọn múi giờ cố định của nhà máy trong ô **Timezone Selector**.
4. *(Tùy chọn)* Bật chế độ **Manual Date/Time Override** và nhập chuỗi `YYYY-MM-DD HH:mm:ss` nếu cần giả lập thời điểm chuyển ca hoặc kiểm thử luật hẹn giờ qua nửa đêm.
5. Đối chiếu thông số tại **Khung Xem trước Trực tiếp**, sau đó nhấn **Sync Clock** để ghi đồng thời chuẩn Epoch UTC và độ lệch múi giờ xuống **SynaptiX Edge Controller**.

### Quy trình 3.3: Cấu hình Ngoại vi RS-485, Wi-Fi và Đám mây IIoT

1. Tại thanh điều hướng bên trái của **Device Workspace** (`Ctrl+1`), chọn **RS-485 Modbus** (`Ctrl+Shift+B`) để thiết lập tốc độ Baud, bit dữ liệu, Parity, Stop bits và địa chỉ SCADA Slave ID nội bộ. Xem chi tiết bản đồ thanh ghi tại [Cấu hình RS-485 & Bản đồ SCADA Slave](/vi/tools/rs485-and-scada-slave).
2. Chọn **Wi-Fi Network** (`Ctrl+Shift+W`) và **Cloud Uplink** (`Ctrl+Shift+M`) trên các dòng thiết bị hỗ trợ mạng để khai báo thông tin trạm Wi-Fi, máy chủ MQTT Broker, chứng chỉ bảo mật TLS và cấu trúc chủ đề (Topic). Tham khảo chuyên sâu tại [Cấu hình MQTT TLS & Mạng Wi-Fi](/vi/tools/mqtt-and-wifi-config) và [Không gian DTU Gateway Studio](/vi/tools/dtu-gateway-studio).
3. Nhấn **Save Configuration** ở cột trái, sau đó nhấn `Ctrl+F5` (**Atomic Deploy**) hoặc chuyển sang [Không gian Triển khai — Deploy](/vi/workspaces/deploy-tab) để nạp đồng bộ cấu hình ngoại vi và luật điều khiển xuống phần cứng.

---

## 4. Thực hành Kỹ thuật Tốt nhất & Xử lý Tình huống Biên

::: warning BẢO TOÀN MÚI GIỜ KHI KẾT NỐI THỤ ĐỘNG
Thao tác kết nối vào **SynaptiX Edge Controller** bằng phím `F4` chỉ thực hiện **đọc không phá hủy (non-destructive read)** thông tin RTC và hồ sơ phần cứng. **SynaptiX IDE** tuyệt đối không tự động ghi đè giờ hay múi giờ của thiết bị ngay lúc cắm cáp—ngăn ngừa rủi ro khi kỹ sư truy cập từ xa ở múi giờ khác vô tình làm lệch các khung giờ `TIME_WINDOW` đang điều khiển dây chuyền. Đồng hồ chỉ cập nhật khi bạn chủ động nhấn **Sync Clock**.
:::

- **Tự động Khôi phục Kết nối khi Nhiễu Công nghiệp (`Reconnecting`):** Trong môi trường tủ điện có biến tần công suất lớn hoặc khi đóng/cắt nguồn thử nghiệm, cổng USB hoặc phiên TCP có thể gián đoạn thoáng qua. **SynaptiX IDE** sẽ tự động chuyển sang trạng thái `Reconnecting`—thực hiện chu kỳ thử lại nhanh rồi chuyển sang chế độ lắng nghe nền không gây đơ giao diện—và khôi phục phiên giám sát ngay khi **SynaptiX Device** hoạt động trở lại.
- **Khóa Khối hàm Theo Năng lực Phần cứng Thực tế:** Khi kết nối vào dòng thiết bị **Remote I/O** tinh gọn không trang bị chip Wi-Fi/Ethernet, các khối truyền thông IP (`MQTT_PUB`, `MQTT_SUB`, `HTTP_CLIENT`, `TCP_CLIENT`) sẽ tự động chuyển sang trạng thái khóa kèm chú giải rõ ràng trên [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab), loại bỏ hoàn toàn lỗi thiết kế vượt quá khả năng thực thi của phần cứng.
- **Luôn Đọc Cấu hình Hiện trường (`F6`) Trước khi Chỉnh sửa:** Khi tiếp nhận bảo trì một bộ điều khiển đang vận hành thực tế tại nhà máy, hãy luôn nhấn `F6` (**Upload from Device**) trước tiên để tải toàn bộ tập luật, khối hàm, thông số RS-485 và cấu hình MQTT từ thiết bị về **SynaptiX IDE** nhằm sao lưu dự phòng trước khi can thiệp.

---

## Tài liệu Liên quan

- [Hệ sinh thái & Cấu hình Thiết bị](/vi/concepts/ecosystem-overview)
- [2. Sơ đồ Logic — Logic Graph (`Ctrl+2`)](/vi/workspaces/logic-graph-tab)
- [3. Danh mục Biến — Tag Catalog (`Ctrl+3`)](/vi/workspaces/tag-catalog-tab)
- [Cấu hình RS-485 & Bản đồ SCADA Slave](/vi/tools/rs485-and-scada-slave)
- [Cấu hình MQTT TLS & Mạng Wi-Fi](/vi/tools/mqtt-and-wifi-config)
- [Không gian DTU Gateway Studio](/vi/tools/dtu-gateway-studio)
