# Giải phẫu Giao diện Kỹ thuật SynaptiX IDE (UI Tour)

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-GS-002</span>
  <span><strong>Chương:</strong> 02. Cài đặt &amp; Khởi tạo Nhanh</span>
  <span><strong>Áp dụng cho:</strong> SynaptiX IDE v2.x Workbench</span>
  <span><strong>Tiêu chuẩn:</strong> ANSI/ISA-101.01 &bull; Microsoft Fluent 2</span>
</div>

Giao diện **SynaptiX IDE** được thiết kế tuân thủ nghiêm ngặt chuẩn công thái học phòng điều khiển **ANSI/ISA-101 ("Nguyên lý Xám — The Gray Principle")** nhằm bảo vệ thị lực kỹ sư trong suốt ca làm việc kéo dài 8–12 giờ: 90% diện tích màn hình sử dụng tông xám đá phiến trung tính chống chói, dành trọn 10% màu sắc bão hòa độ tương phản cao cho các trạng thái vận hành thực tế — **Xanh Lục Bảo** (Đã kết nối / Dây dẫn có điện / Chạy tốt), **Vàng Hổ Phách** (Cảnh báo / Cưỡng bức biến / Chờ biên dịch lại), **Đỏ Thẫm** (Lỗi biên dịch / Khóa cổng nạp) và **Xanh Dầu Công nghiệp** (Đối tượng đang chọn).

![Giải phẫu Toàn cảnh Bàn làm việc Kỹ thuật SynaptiX IDE](/images/synaptix_main_anatomy.png)

---

## Kiến trúc Bàn làm việc 6 Phân vùng (6-Zone Workbench)

Cửa sổ chính của **SynaptiX IDE** được quy hoạch thành 6 phân vùng chức năng cố định, không chồng lấp hộp thoại nổi, giúp kỹ sư luôn kiểm soát trọn vẹn ngữ cảnh hệ thống:

<div class="synx-ide-mockup">
  <div class="synx-ide-topbar">
    <span>Phân vùng 1: Thanh Menu Lệnh (Tệp • Chỉnh sửa • Hiển thị • Thiết bị • Công cụ)</span>
    <span style="color: #60a5fa;">[Cụm Kết nối: USB CDC / Modbus TCP • F4]</span>
  </div>
  <div class="synx-ide-navtabs">
    <span class="synx-ide-tab">Ctrl+1 Device</span>
    <span class="synx-ide-tab is-active">Ctrl+2 Logic Graph</span>
    <span class="synx-ide-tab">Ctrl+3 Tag Catalog</span>
    <span class="synx-ide-tab">Ctrl+4 Rule Table</span>
    <span class="synx-ide-tab">Ctrl+5 Blueprints</span>
    <span class="synx-ide-tab">Ctrl+6 Deploy</span>
    <span class="synx-ide-tab">Ctrl+7 Live Watch</span>
  </div>
  <div class="synx-ide-body">
    <div class="synx-ide-panel synx-ide-panel-left">
      <div class="synx-ide-panel-header">Phân vùng 3 • Hộp Khối Hàm</div>
      <p style="font-size: 0.76rem; color: var(--vp-c-text-2); margin: 0;">Khối Logic &amp; Hẹn giờ (TON, TOF, CTU...)<br/>Khối Ngoại vi (MB_READ, MQTT_PUB...)</p>
      <div style="margin-top: auto; font-size: 0.72rem; color: var(--vp-c-text-3);">Rộng 265 px cố định</div>
    </div>
    <div class="synx-ide-panel synx-ide-panel-center">
      <div>
        <div class="synx-ide-panel-header">Phân vùng 4 • Mặt vẽ Logic Vô tận (Canvas)</div>
        <p style="font-size: 0.78rem; color: var(--vp-c-text-2); margin: 0;">Kéo-thả khối hàm, nối dây tất định 4 giai đoạn, hiển thị trạng thái On/Off trực tiếp.</p>
      </div>
      <div style="background-color: var(--vp-c-bg); border: 1px dashed var(--vp-c-divider); border-radius: 4px; padding: 8px; font-size: 0.74rem; color: var(--vp-c-text-2);">
        <strong>Ngăn Mô phỏng Động cơ Quét (Phía dưới Canvas)</strong> — Chạy chu kỳ 10ms mô phỏng độc lập (F5)
      </div>
    </div>
    <div class="synx-ide-panel synx-ide-panel-right">
      <div class="synx-ide-panel-header">Phân vùng 5 • Thuộc tính &amp; AI</div>
      <p style="font-size: 0.76rem; color: var(--vp-c-text-2); margin: 0;">Cấu hình chân I/O, thời gian trễ ms, ngưỡng so sánh của khối đang chọn.</p>
      <div style="margin-top: auto; font-size: 0.72rem; color: var(--vp-c-text-3);">Rộng 320 px + Cột AI Copilot</div>
    </div>
  </div>
  <div class="synx-ide-statusbar">
    <span>Phân vùng 6: Trạng thái Chu kỳ (Scan: 10.0ms • Jitter &lt;0.5ms • Mode: RUN)</span>
    <span>CPU: 8% • RAM: 42 KB / 64 KB</span>
  </div>
</div>

---

## Phân vùng 1: Thanh Menu Lệnh & Cụm Kết nối Hợp nhất

Nằm dọc theo cạnh trên cùng của cửa sổ, Phân vùng 1 kết hợp thanh thực đơn chuẩn công nghiệp (`Tệp`, `Chỉnh sửa`, `Hiển thị`, `Thiết bị`, `Công cụ`, `Trợ giúp`) với **Cụm Kết nối Thiết bị Hợp nhất** ở góc phải:

- **Công tắc Phân đoạn Giao thức (`USB CDC` vs. `Modbus TCP`):** Chuyển đổi tức thời giữa cáp USB tại chỗ và kết nối mạng LAN từ xa bên trong khung chọn cố định chiều rộng `210 px` chống giật bố cục:
  - **Chế độ `USB CDC`:** Liệt kê danh sách cổng `COMx` khả dụng kèm nút **Làm mới cổng (Refresh Ports)** một chạm.
  - **Chế độ `Modbus TCP`:** Cho phép nhập trực tiếp cặp **`Địa chỉ IP : Cổng`** (ví dụ `192.168.1.50 : 502`).
- **Tổ hợp Lệnh Vòng đời Thiết bị & Phím chức năng:**
  - **Kết nối / Ngắt kết nối (`F4`):** Mở hoặc đóng phiên giao tiếp trực tuyến có kiểm soát với **Thiết bị Biên SynaptiX**.
  - **Mô phỏng Offline (`F5`):** Khởi chạy nhân mô phỏng chu kỳ thực ngay trên máy tính không cần phần cứng.
  - **Đọc chương trình từ thiết bị (`F6` — Upload):** Tải ngược cấu hình đang vận hành trên bộ điều khiển về IDE.
  - **Biên dịch kiểm tra logic (`F7` — Compile):** Thẩm định toàn bộ dây nối, kiểu dữ liệu Tag và giới hạn tài nguyên, hạ bậc sơ đồ xuống bảng luật tất định.
  - **Triển khai nguyên tử (`Ctrl+F5` — Deploy):** Thực thi cổng kiểm tra an toàn tiền triển khai và ghi bền vững hai pha xuống bộ nhớ Flash thiết bị.
  - **Gỡ lỗi trực tuyến (`F8` — Live Debug):** Truyền trực tiếp giá trị biến và tô sáng luồng dây dẫn đang dẫn điện ở tốc độ 20 FPS ngay trên sơ đồ.
- **Nút Bật/Tắt AI Chatbox (`Ctrl+Space`):** Đóng/mở bảng trợ lý kỹ thuật [SynaptiX AI Copilot](/vi/tools/ai-copilot) bên cạnh phải.
- **Chuyển đổi Ngôn ngữ Tức thời (`VI` / `EN`):** Đặt tại **Công cụ (Tools) -> Ngôn ngữ (Language)**, chuyển đổi đồng bộ 100% nhãn giao diện, chú giải và thông báo lỗi ngay lập tức không cần khởi động lại.

---

## Phân vùng 2: Thanh Chuyển đổi 7 Không gian Làm việc (`Ctrl+1` .. `Ctrl+7`)

Nằm bên trái thanh điều hướng, 7 thẻ (Tab) không gian làm việc được sắp xếp đúng trình tự tư duy triển khai dự án từ trái sang phải. Kỹ sư có thể chuyển nhanh giữa các Tab bằng phím tắt `Ctrl + Số` (hỗ trợ cả hàng phím số chính và cụm Numpad):

| Phím tắt | Không gian Làm việc | Nhiệm vụ Kỹ thuật Trọng tâm | Tài liệu Chi tiết |
| :---: | :--- | :--- | :--- |
| `Ctrl+1` | **1. Thiết bị (Device)** | Kiểm tra định danh phần cứng, phiên bản FW/HW, phân bổ tài nguyên, cấu hình RS-485, MQTT/Wi-Fi và đồng bộ RTC. | [Tab Thiết bị — Device](/vi/workspaces/device-tab) |
| `Ctrl+2` | **2. Sơ đồ Logic (Logic Graph)** | Thiết kế trực quan các khối điều khiển & IIoT trên mặt vẽ vô tận (hoặc cấu hình kênh DTU khi chọn thiết bị DTU). | [Tab Sơ đồ Logic](/vi/workspaces/logic-graph-tab) |
| `Ctrl+3` | **3. Danh mục Biến (Tag Catalog)** | Quản lý tối đa 128 Tag vật lý/ảo, đặt tên gợi nhớ Alias, chọn kiểu `BOOL`/`INT32`/`REAL` và nhập/xuất CSV. | [Tab Danh mục Biến](/vi/workspaces/tag-catalog-tab) |
| `Ctrl+4` | **4. Bảng Luật (Rule Table)** | Kiểm toán danh sách luật 4 giai đoạn sau biên dịch dưới dạng bảng cô đọng và truy ngược về sơ đồ gốc. | [Tab Bảng Luật](/vi/workspaces/rule-table-tab) |
| `Ctrl+5` | **5. Thư viện Mẫu (Blueprints)** | Khởi tạo nhanh các mẫu mạch tự động hóa & truyền thông IIoT chuẩn công nghiệp đã kiểm chứng sẵn. | [Tab Thư viện Mẫu](/vi/workspaces/blueprints-tab) |
| `Ctrl+6` | **6. Triển khai (Deploy)** | Đối chiếu cổng kiểm tra tương thích phần cứng, thanh dung lượng tài nguyên và nạp nguyên tử hai pha. | [Tab Triển khai](/vi/workspaces/deploy-tab) |
| `Ctrl+7` | **7. Giám sát (Live Watch)** | Theo dõi bảng giá trị thực 20 FPS, vẽ đồ thị xu hướng đa kênh, cưỡng bức/ghi biến và ghi nhật ký CSV. | [Tab Giám sát Live Watch](/vi/workspaces/live-watch-tab) |

---

## Phân vùng 3: Hộp Công cụ Khối Hàm & Phím tắt Cấu hình Giao thức

Khi mở **Sơ đồ Logic** (`Ctrl+2`), cột bên trái rộng `265 px` hiển thị **Hộp Công cụ Khối Hàm (Block Toolbox)**:

1. **Ô Tìm kiếm Lọc Tức thời:** Gõ nhanh tên khối (như `TON`, `MQTT`, `SCALE`, `Guard`) để lọc danh sách theo thời gian thực.
2. **8 Nhóm Chức năng Kiểu Thanh Ray DIN:** Phân loại khoa học toàn bộ [18 Khối Hàm](/vi/blocks/overview):
   - *Vào/Ra Quá trình (Process I/O):* [`INPUT`](/vi/blocks/input), [`ACTION`](/vi/blocks/action)
   - *Logic & Khóa Liên động:* [`TRIGGER`](/vi/blocks/trigger), [`GUARD`](/vi/blocks/guard), [`COMPARATOR`](/vi/blocks/comparator)
   - *Bộ Định thời (Timers):* [`TON`](/vi/blocks/ton), [`TOF`](/vi/blocks/tof), [`TP`](/vi/blocks/tp), [`CYCLE_TIMER`](/vi/blocks/cycle-timer)
   - *Bộ Đếm (Counters):* [`CTU`](/vi/blocks/ctu), [`CTD`](/vi/blocks/ctd)
   - *Tương tự & Cơ cấu Chấp hành:* [`SCALE`](/vi/blocks/scale), [`DIRECT_WRITE`](/vi/blocks/direct-write)
   - *Truyền thông Modbus Fieldbus:* [`MB_READ`](/vi/blocks/mb-read), [`MB_WRITE`](/vi/blocks/mb-write)
   - *Đám mây MQTT IIoT:* [`MQTT_PUB`](/vi/blocks/mqtt-pub), [`MQTT_SUB`](/vi/blocks/mqtt-sub)
   - *Cổng Mạng & Luồng Socket:* [`HTTP_CLIENT`](/vi/blocks/http-client), [`TCP_CLIENT`](/vi/blocks/tcp-client)
3. **Tự động Khóa Khối Không Tương thích Phần cứng:** Nếu cấu hình dự án không hỗ trợ ngoại vi tương ứng (ví dụ khối `MQTT_PUB` trên thiết bị `SynaptiX Compact I/O` không có mạng), thẻ khối sẽ tự làm mờ kèm chú giải lý do rõ ràng.
4. **Phím tắt Mở Nhanh Hộp thoại Cấu hình Giao thức Toàn cục:** Truy cập từ bất kỳ đâu qua menu **Thiết bị (Device)** hoặc tổ hợp phím:
   - `Ctrl+Shift+B` — [Cấu hình Cổng RS-485 & Bản đồ SCADA Slave](/vi/tools/rs485-and-scada-slave)
   - `Ctrl+Shift+M` — [Cấu hình Kết nối MQTT Broker & Chứng chỉ TLS](/vi/tools/mqtt-and-wifi-config)
   - `Ctrl+Shift+W` — [Cấu hình Mạng Không dây Wi-Fi](/vi/tools/mqtt-and-wifi-config)
   - `Ctrl+Shift+D` — [Cấu hình Cổng DTU & Trình Bắt Gói tin](/vi/tools/dtu-gateway-studio)

---

## Phân vùng 4: Mặt vẽ Logic Vô tận (hoặc DTU Gateway Studio)

Phân vùng 4 là không gian thiết kế trung tâm:

- **Lưới Kỹ thuật Chính xác 20 px:** Hỗ trợ kéo trượt/phóng thu mượt mà, quét chọn nhiều khối, đi dây vuông góc 90 độ chuẩn bản vẽ điện với vùng bắt chuột thông minh rộng `12 px`, cùng bộ phím tắt căn chỉnh kiểu CAD (`Alt + Phím mũi tên` căn biên, `Alt+Shift+H/V` dàn đều khoảng cách, `Alt+G` bắt dính/ẩn hiện lưới).
- **Ngăn Mô phỏng Offline Tích hợp (`F5`):** Trượt mở gọn gàng ngay cạnh dưới mặt vẽ khi bật chế độ mô phỏng để kỹ sư gạt thử công tắc `DI`, kéo thanh trượt `AI` và bơm gói JSON giả lập trong khi mắt vẫn quan sát trực tiếp luồng dây sáng xanh bên trên.
- **Cổng Phê duyệt An toàn AI (Human Affirmation Gate):** Khi [SynaptiX AI Copilot](/vi/tools/ai-copilot) đề xuất sơ đồ mạch, các khối xem trước dạng nét đứt sẽ hiện kèm thanh xác nhận **Chấp nhận (Accept) / Hủy bỏ (Reject)** và huy hiệu kiểm chứng `0 xung đột ghi`.
- **Tự động Chuyển sang DTU Gateway Studio:** Khi dự án chọn mẫu **`SynaptiX DTU`**, Phân vùng 4 tự động chuyển sang giao diện bảng cấu hình tuyến đọc RS-485 và đẩy dữ liệu đám mây chuyên biệt của [DTU Gateway Studio](/vi/tools/dtu-gateway-studio).

---

## Phân vùng 5: Bảng Thuộc tính Theo Ngữ cảnh (Property Inspector)

Neo cố định bên phải (`320 px`), **Property Inspector** tự động hiển thị biểu mẫu cấu hình chuyên sâu của khối đang được chọn trên mặt vẽ:

- **Biểu mẫu Tham số An toàn Kiểu:** Cung cấp danh sách chọn biến hiển thị kép (`[Tag] — Alias`), bộ chọn toán tử so sánh, thời gian đặt Timer, kiểu giải mã thanh ghi Modbus và trình soạn thảo khung bản tin JSON có kiểm tra cú pháp Macro.
- **Bảo vệ Nhập liệu Thực thi Trực tiếp:** Toàn bộ ô nhập ngưỡng và hằng số đều kích hoạt [Máy Trạng thái Chống Nhập sai](/vi/concepts/data-model-and-types) — tự động chuẩn hóa `"25"` thành `"25.0"` cho kiểu `REAL` và hoàn nguyên tức thì nếu nhập sai kiểu khi nhấn `Enter` hoặc rời ô.
- **Cột AI Copilot Co giãn (`Ctrl+Space`):** Mở thêm ở ngoài cùng bên phải với thanh kéo chia cột rộng `5 px` khi cần trao đổi với trợ lý AI và thu gọn hoàn toàn về `0 px` khi đóng.

---

## Phân vùng 6: Thanh Trạng thái Chẩn đoán & Telemetry Phần cứng

Neo dọc cạnh dưới cùng (cao `36 px`), Phân vùng 6 tích hợp nhóm nút thao tác nhanh phong cách VS Code ở bên trái và dải đồng hồ sức khỏe phần cứng ở bên phải:

- **Cụm Bên Trái — Trạng thái Biên dịch & Điều khiển Nhanh:**
  - Báo hiệu trực quan **Trạng thái Cú pháp & Biên dịch** (`Hợp lệ` màu xanh lục, `Chờ biên dịch lại` màu cam, `Lỗi cú pháp` màu đỏ) kèm nhãn chế độ sửa luật.
  - Cụm nút phẳng thao tác nhanh: **Bản vẽ mới**, **Mở Bảng Luật**, **Biên dịch (`F7`)**, **Lưu dự án (`Ctrl+S`)**, **Nạp thiết bị (`Ctrl+F5`)**, **Chạy Mô phỏng (`F5`)**, **Live Debug (`F8`)** *(phát sáng thành viên thuốc xanh `LIVE DEBUG` có chấm nhịp đập khi đang gỡ lỗi trực tuyến)* và **Bật/Tắt Lưới**.
- **Cụm Bên Phải — Huy hiệu Cảnh báo & Thông tin Thiết bị:**
  - **Huy hiệu Cập nhật Phần mềm:** Chỉ hiện lên nhẹ nhàng khi phát hiện bản phát hành IDE mới.
  - **Cảnh báo Pin Nuôi RTC Yếu:** Viền đỏ cảnh báo sớm khi pin cúc áo nuôi đồng hồ thời gian thực trên mạch xuống thấp.
  - **Đồng hồ Thời gian Thực Thiết bị (RTC Badge):** Hiển thị giờ thực tế trên bộ điều khiển khi kết nối; nhấp chuột trực tiếp vào huy hiệu để đồng bộ tức thời giờ thiết bị theo giờ máy tính.
  - **Huy hiệu Cấu hình Thiết bị Đích:** Hiển thị tên dòng thiết bị và tổng số Tag; nhấp vào để mở hộp thoại thông tin chi tiết phần cứng.

::: tip Chế độ Toàn màn hình Khi Làm việc Trên Laptop Hiện trường (`F11`)
Nhấn phím **`F11`** bất kỳ lúc nào để bật/tắt chế độ Toàn màn hình (Full Screen), giúp mở rộng tối đa diện tích mặt vẽ sơ đồ và đồ thị xu hướng trên các màn hình laptop kỹ thuật kích thước gọn `1366 x 768`.
:::

---

## Bước Tiếp theo

- [Dự án Đầu tiên trong 10 Phút](/vi/getting-started/quickstart-first-project) — Áp dụng thực tế toàn bộ 6 phân vùng qua bài thực hành thiết kế mạch làm mát bảo vệ quá nhiệt hoàn chỉnh.
- [Phụ lục Bảng Phím tắt & Thao tác Canvas](/vi/appendices/keyboard-shortcuts) — Bảng tổng hợp đầy đủ mọi tổ hợp phím tắt để tra cứu nhanh.
