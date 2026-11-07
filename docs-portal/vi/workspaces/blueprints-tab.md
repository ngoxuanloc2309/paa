# 5. Thư viện Mẫu — Blueprints (`Ctrl+5`)

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-WS-005</span>
  <span><strong>Không gian làm việc:</strong> Tab 5 &bull; Phím tắt <code>Ctrl+5</code></span>
  <span><strong>Phạm vi:</strong> Thư viện Mẫu Tự động hóa Công nghiệp &amp; Điện toán Biên IIoT Tiền Kiểm chứng, Khởi tạo Nhanh &amp; Tùy biến</span>
  <span><strong>Tiêu chuẩn:</strong> IEC 61131-3 &bull; IEC/IEEE 82079-1 &bull; ANSI/ISA-101</span>
</div>

---

## 1. Mục đích & Vai trò Vận hành

**Thư viện Mẫu — Blueprints Library** (`Ctrl+5`) là kho lưu trữ kỹ thuật gồm **27 kiến trúc mẫu tự động hóa công nghiệp, đo lường hiệu chuẩn, truyền thông RS-485 và điện toán biên IIoT đã được kiểm chứng chuẩn hóa** tích hợp sẵn trong **SynaptiX IDE**. Thay vì phải dựng lại từ đầu các mạch điều khiển kinh điển—như chiếu sáng ca đêm vắt qua nửa đêm, tuyến tính hóa cảm biến `4–20 mA`, mạch khóa an toàn dừng khẩn E-Stop hay luồng đọc đồng hồ điện năng 3 pha đẩy lên MQTT—kỹ sư chỉ cần một cú nhấp chuột để khởi tạo trọn vẹn sơ đồ chuẩn lên [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab).

Trong thực tiễn tích hợp hệ thống tủ bảng điện và trạm thu thập dữ liệu, **Blueprints Library** rút ngắn thời gian triển khai trên ba phương diện:

1. **Kiến trúc Tham chiếu Chuẩn Công nghiệp:** Cung cấp các mạng mạch đã tích hợp sẵn bộ lọc chống nhiễu dội tiếp điểm (`ForMs`), cổng khóa liên động an toàn `GUARD`, lựa chọn đúng vùng nhớ lưu trữ mất điện (`VREG_RETAIN`) và độ chính xác số thực dấu phẩy động IEEE-754 `REAL`.
2. **Tự động Khai báo Biến & Đi dây Chuẩn Lưới:** Tự động ánh xạ các biến vật lý và biến nội bộ trong [Danh mục Biến — Tag Catalog](/vi/workspaces/tag-catalog-tab), dàn trang khối hàm thẳng hàng theo các cột tọa độ lưới (`X = 80, 340, 600, 860`) và nối hoàn chỉnh mọi dây **Value** và **Event**.
3. **Đào tạo Trực quan & Mô phỏng Tức thời (`F5`):** Cho phép kỹ sư nghiệm thu tải bất kỳ kịch bản mẫu nào, nhấn ngay `F5` để quan sát luồng tín hiệu chạy từng chu kỳ trên [Bộ Mô phỏng Offline](/vi/tools/simulator-and-live-debug), sau đó tinh chỉnh lại ngưỡng đặt hoặc số chân cầu đấu theo tủ điện thực tế.

---

## 2. Bố cục Trực quan & Tham chiếu Điều khiển

![Thư viện Mẫu Tự động hóa Công nghiệp SynaptiX Blueprints](/images/synaptix_blueprints.png)

**Blueprints Library** được thiết kế dạng lưới thẻ kỹ thuật nền xám trung tính chuẩn ANSI/ISA-101, hỗ trợ lọc nhanh theo chuyên ngành và tìm kiếm từ khóa tức thời:

### 2.1. Điều khiển Thư viện & Giải phẫu Thẻ Mẫu (Blueprint Card)

| Thành phần Giao diện | Vị trí | Chức năng & Hành vi Vận hành |
| :--- | :--- | :--- |
| **Thanh Lọc Lĩnh vực (Category Bar)** | Dải lọc phía trên | Lọc nhanh 27 mẫu thiết kế theo `All` hoặc từng nhóm trong **7 lĩnh vực kỹ thuật chuyên ngành**. |
| **Ô Tìm kiếm Từ khóa** | Góc phải thanh công cụ | Tìm kiếm tức thời theo tên bài toán, mô tả nguyên lý, tên khối hàm (`TON`, `SCALE`, `MQTT_PUB`, `MB_READ`) hoặc tên Tag sử dụng. |
| **Huy hiệu Phân loại Chuyên ngành** | Góc trên trái thẻ | Hiển thị lĩnh vực ứng dụng của mẫu (ví dụ: `Analog & Instrumentation` hoặc `IIoT Cloud & MQTT`). |
| **Tiêu đề & Phụ đề Vận hành** | Phần đầu thẻ | Tóm tắt bài toán máy công nghiệp hoặc trạm IIoT mục tiêu cùng cơ chế kích hoạt/chấp hành chính. |
| **Thuyết minh Kỹ thuật Chi tiết** | Phần thân thẻ | Giải thích đặc tính cảm biến đầu vào, thời gian trễ chống nhiễu, logic khóa an toàn và hành vi thực thi dưới Runtime. |
| **Khung kê khai `Tags Used`** | Khung chìm chân thẻ | Liệt kê đầy đủ danh sách các biến `DI`, `DO`, `AI`, `VFLAG`, `VREG`, `VREG_RETAIN` hoặc `COUNTER` mà mẫu sẽ tự động cấu hình. |
| **Nút `Apply Blueprint`** | Góc dưới phải thẻ | Khởi tạo toàn bộ mạng khối, cập nhật bí danh/kiểu dữ liệu Tag, đi dây hoàn chỉnh và chuyển ngay sang [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab) (`Ctrl+2`). |

### 2.2. Danh mục Đầy đủ 7 Nhóm Kỹ thuật (27 Mẫu Chuẩn hóa)

| Nhóm Lĩnh vực Kỹ thuật | Các Mẫu Thiết kế Tiền Kiểm chứng Điển hình | Tổ hợp Khối hàm Minh họa |
| :--- | :--- | :--- |
| **1. Logic Rời rạc & Khóa Liên động An toàn** | &bull; Cảnh báo Gọi Trạm Andon (lọc trễ `15 s`)<br/>&bull; Bộ Đếm Sản lượng Dây chuyền Lưu trữ Flash<br/>&bull; Nút Nhấn Gọi Vật tư Tự giữ Đảo trạng thái<br/>&bull; Bơm Thoát nước Hố thu Tự động Mức cao<br/>&bull; Khóa Liên động Cửa Bảo vệ Tiếp điểm Đảo<br/>&bull; Khóa Cắt Nguồn Động lực Tổng<br/>&bull; Đèn Tháp Trạng thái Máy Công cụ<br/>&bull; Mạch Dừng khẩn E-Stop Kép An toàn | `INPUT`, `TRIGGER` (`ON_RISE`, `ForMs`), `GUARD` (thuận & đảo), `ACTION` (`SET_TAG`, `TOGGLE_TAG`, `INC_COUNTER`) |
| **2. Tín hiệu Tương tự & Đo lường Hiệu chuẩn** | &bull; Bảo vệ Cắt Quá nhiệt Thiết bị<br/>&bull; Giám sát Áp suất Khí nén / Thủy lực<br/>&bull; Cửa sổ Áp suất Vận hành An toàn (`BETWEEN`)<br/>&bull; Quy đổi Cảm biến Áp suất `0–10 V` sang `Bar`<br/>&bull; Quy đổi Cảm biến Nhiệt độ `4–20 mA` sang `°C`<br/>&bull; Hiệu chuẩn Cảm biến Siêu âm Mức bồn (`Mét`)<br/>&bull; Báo động Nhiệt độ Chính xác Float32 `75.5 °C`<br/>&bull; Phát Cảnh báo Khẩn cấp Quá áp Đường ống | `INPUT` (`AI`), `SCALE` (tuyến tính hóa 2 điểm), `COMPARATOR` (`>`, `<`, `BETWEEN`, `REAL`), `ACTION` (`SEND_ALARM`) |
| **3. Định thời Chu kỳ & Bộ Hẹn giờ IEC 61131-3** | &bull; Bơm Dầu Bôi trơn Ổ trục Định kỳ (`INTERVAL`)<br/>&bull; Khởi động Bơm Tăng áp Trễ Đóng (`TON`)<br/>&bull; Quạt Làm mát Trục chính Trễ Ngắt (`TOF`)<br/>&bull; Phát Xung Khí nén Đẩy Phôi Đơn ổn (`TP`) | `TRIGGER` (`INTERVAL`), `TON`, `TOF`, `TP`, `ACTION` |
| **4. Bộ Đếm Mẻ Sản xuất (Batch Counters)** | &bull; Bộ Đếm Đóng Thùng Carton (`CTU`)<br/>&bull; Bộ Đếm Lùi Phôi Cấp liệu Khay nạp (`CTD`) | `CTU`, `CTD`, `COUNTER`, `VREG_RETAIN` |
| **5. Lập lịch Thời gian Thực (RTC Scheduling)** | &bull; Bật Chiếu sáng Nhà xưởng Ca sáng `07:00`<br/>&bull; Đèn An ninh Vành đai Qua Nửa đêm `18:00–06:00` | `TRIGGER` (`TIME_WINDOW` trong ngày & vắt qua `00:00`), `ACTION` |
| **6. Điện toán Biên IIoT & Đám mây MQTT** | &bull; Đẩy Bản tin JSON Telemetry theo Sự kiện (`MQTT_PUB`)<br/>&bull; Nhận Lệnh Điều khiển JSON từ Cloud (`MQTT_SUB`) | `MQTT_PUB` (macro chuỗi JSON), `MQTT_SUB` (bóc tách khóa JSON), `GUARD` |
| **7. Truyền thông RS-485 & Logistics Thông minh** | &bull; Đọc Chu kỳ Cảm biến Modbus RTU (`MB_READ`)<br/>&bull; Ghi Tần số Đặt Biến tần VFD (`MB_WRITE`)<br/>&bull; Hệ thống Phân loại 18 Ngăn Đèn dẫn đường (`DIRECT_WRITE`)<br/>&bull; Thu thập Đồng hồ Điện 3 Pha (`MFM383A → MQTT`) | `MB_READ`, `MB_WRITE`, `DIRECT_WRITE`, `SCALE`, `MQTT_PUB` |

---

## 3. Quy trình Thao tác Chuẩn từng Bước

### Quy trình 3.1: Khởi tạo và Chạy Mô phỏng Một Mẫu Công nghiệp

1. Nhấn `Ctrl+5` để mở **Thư viện Mẫu (Blueprints Library)**.
2. Chọn nhãn lĩnh vực trên thanh lọc (ví dụ: **Analog & Instrumentation**) hoặc gõ từ khóa `4-20mA` vào ô tìm kiếm.
3. Xem trước mục **`Tags Used`** trên thẻ mẫu để nắm danh sách các ngõ vào vật lý (`AI0`) và thanh ghi ảo (`VREG0`) sẽ được cấu hình.
4. Nhấn nút **Apply Blueprint**.
5. **SynaptiX IDE** lập tức dàn dựng toàn bộ khối hàm thẳng hàng theo lưới kỹ thuật (`X = 80, 340, 600, 860`), nối dây hoàn chỉnh và tự động chuyển màn hình sang [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab) (`Ctrl+2`).
6. Nhấn ngay phím `F5` để bật **Bộ Mô phỏng Offline**, kéo thanh trượt giả lập tín hiệu `AI0` hoặc gạt công tắc `DI0` để quan sát giá trị truyền dẫn trực quan qua từng khối hàm.

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>BƯỚC 1</span>
      </div>
      <span class="synx-node-status">SELECT</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Chọn Mẫu Sẵn</span>
        <span class="synx-node-alias">Thư Viện Chuẩn</span>
        <span class="synx-node-val">Ctrl+5 Blueprints</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Apply</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>BƯỚC 2</span>
      </div>
      <span class="synx-node-status">TAGS</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Khởi Tạo Tag</span>
        <span class="synx-node-alias">Tự Động Khai Báo</span>
        <span class="synx-node-val">Alias & Kiểu Dữ Liệu</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Spawn</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 L13.5,4 L13.5,9.5 Q13.5,13.5 8,15 Q2.5,13.5 2.5,9.5 L2.5,4 Z" /></svg>
        <span>BƯỚC 3</span>
      </div>
      <span class="synx-node-status">CANVAS</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Bố Trí Khối Hàm</span>
        <span class="synx-node-alias">Lưới Tọa Độ Chuẩn</span>
        <span class="synx-node-val">Ctrl+2 Logic Graph</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Tune</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,13 L5,13 L5,9.5 L8,9.5 L8,6 L11,6 L11,3 L14,3" /></svg>
        <span>BƯỚC 4</span>
      </div>
      <span class="synx-node-status">PARAMS</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Tùy Biến Ngưỡng</span>
        <span class="synx-node-alias">Khớp Tủ Điện</span>
        <span class="synx-node-val">Right Inspector</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Run</span>
  </div>

  <div class="synx-canvas-node is-step is-done">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>BƯỚC 5</span>
      </div>
      <span class="synx-node-status is-active">● OPERATE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Vận Hành Máy</span>
        <span class="synx-node-alias">Mô Phỏng & Nạp</span>
        <span class="synx-node-val">F7 / F5 / Ctrl+F5</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

### Quy trình 3.2: Tùy biến Mẫu Thiết kế Khớp với Cầu đấu Tủ điện Thực tế

1. Sau khi áp dụng mẫu lên [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab), nhấp chọn bất kỳ khối **INPUT**, **GUARD** hoặc **ACTION** nào cần đổi chân đấu dây thực tế (ví dụ: chuyển ngõ ra từ `DO0` sang `DO4`).
2. Chọn lại biến đích trong **Bảng Thuộc tính Bên phải (Right Inspector)**, đồng thời điều chỉnh các thông số công nghệ (như tăng thời gian trễ `TON` từ `5000 ms` lên `10000 ms` hoặc đổi địa chỉ Slave ID của khối `MB_READ`).
3. Chuyển sang [Danh mục Biến — Tag Catalog](/vi/workspaces/tag-catalog-tab) (`Ctrl+3`) nếu bạn muốn việt hóa hoặc đổi lại cột **Alias** theo đúng ký hiệu bản vẽ thiết kế điện của nhà máy.
4. Nhấn `F7` để biên dịch và xác nhận không còn cảnh báo tĩnh nào trong [Bảng Luật — Rule Table](/vi/workspaces/rule-table-tab) (`Ctrl+4`).

---

## 4. Thực hành Kỹ thuật Tốt nhất & Xử lý Tình huống Biên

::: warning LƯU DỰ ÁN TRƯỚC KHI ÁP DỤNG MẪU MỚI LÊN BẢN VẼ
Thao tác nhấn **Apply Blueprint** sẽ khởi tạo một bản vẽ sạch độc lập cho kiến trúc mẫu đó trên [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab) hiện hành. Nếu dự án đang mở của bạn có chứa các nhánh mạch tự vẽ chưa lưu, hãy nhấn `Ctrl+S` để lưu tệp `.synx` trước—hoặc mở thêm một cửa sổ dự án nháp để áp dụng mẫu rồi sao chép (`Ctrl+C`) và dán (`Ctrl+V`) cụm nhánh đó ghép vào dự án tổng.
:::

- **Đối chiếu Năng lực Phần cứng với Các Mẫu IIoT & RS-485:** Các mẫu thuộc Nhóm 6 và Nhóm 7 (`MQTT_PUB`, `MQTT_SUB`, `MB_READ`, `MB_WRITE`) yêu cầu thiết bị mục tiêu có trang bị giao tiếp Wi-Fi/Ethernet hoặc cổng thu phát RS-485. Hãy kiểm tra đúng biến thể phần cứng tại [Không gian Thiết bị — Device](/vi/workspaces/device-tab) (`Ctrl+1`) trước khi thực hiện nạp xuống hiện trường (`Ctrl+F5`).
- **Đồng bộ Đồng hồ RTC cho Các Mẫu Lập lịch Qua Nửa đêm:** Khi triển khai mẫu *Đèn An ninh Vành đai Qua Nửa đêm `18:00–06:00`*, đừng quên đồng bộ giờ chuẩn và độ lệch múi giờ cho bộ điều khiển qua công cụ **Device → Sync Clock** (xem tại [Không gian Thiết bị — Device](/vi/workspaces/device-tab)) để khung giờ đóng/cắt vận hành chính xác tuyệt đối theo ca nhà máy.

---

## Tài liệu Liên quan

- [2. Sơ đồ Logic — Logic Graph (`Ctrl+2`)](/vi/workspaces/logic-graph-tab)
- [3. Danh mục Biến — Tag Catalog (`Ctrl+3`)](/vi/workspaces/tag-catalog-tab)
- [Tổng quan Danh mục & Quy ước Chân](/vi/blocks/overview)
- [Kịch bản Tích hợp Thực tế (App Notes)](/vi/tools/application-notes)
- [Mô phỏng Offline & Gỡ lỗi Trực tuyến](/vi/tools/simulator-and-live-debug)
