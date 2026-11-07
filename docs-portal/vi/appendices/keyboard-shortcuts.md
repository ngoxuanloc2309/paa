# Bảng Phím tắt & Thao tác Nhanh Trên Canvas

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-APPX-001</span>
  <span><strong>Phạm vi:</strong> Phím tắt Toàn cục &bull; Sơ đồ Khối Logic &bull; Thao tác Chuột</span>
  <span><strong>Nền tảng:</strong> Windows 10 / 11 Desktop (<code>SynaptiX IDE</code>)</span>
  <span><strong>Tiêu chuẩn:</strong> Công thái học Thiết kế Tủ điện Tốc độ cao</span>
</div>

**SynaptiX IDE** được tối ưu hóa cho phong cách thao tác bàn phím tốc độ cao trong suốt quá trình thiết kế mạch điều khiển, nghiệm thu tủ điện tại nhà máy và chẩn đoán sự cố trực tuyến. Toàn bộ phím tắt dưới đây hoạt động xuyên suốt cửa sổ phần mềm (và tự động nhường quyền khi con trỏ đang gõ văn bản bên trong ô nhập liệu).

---

## 1. Chuyển đổi Nhanh Không gian Làm việc (`Ctrl+1` .. `Ctrl+7`)

Chuyển tức thì giữa 7 phân hệ kỹ thuật chính bằng hàng phím số phía trên (`1..7`) hoặc cụm phím số phụ (`NumPad1..NumPad7`):

| Phím tắt | Phím Tương đương | Không gian Đích | Chức năng Kỹ thuật Trọng tâm |
| :--- | :--- | :--- | :--- |
| `Ctrl+1` | `Ctrl+NumPad1` | **[1. Thiết bị — Device](/vi/workspaces/device-tab)** *(hoặc [DTU Studio](/vi/tools/dtu-gateway-studio) trên Preset DTU)* | Chọn cấu hình phần cứng, kết nối USB/TCP, đồng bộ đồng hồ thực RTC & chẩn đoán sức khỏe trạm. |
| `Ctrl+2` | `Ctrl+NumPad2` | **[2. Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab)** | Thiết kế lưu đồ khối FBD, đi dây tín hiệu, gỡ lỗi trực tuyến `F8` & ngăn mô phỏng `F5`. |
| `Ctrl+3` | `Ctrl+NumPad3` | **[3. Danh mục Biến — Tag Catalog](/vi/workspaces/tag-catalog-tab)** | Quản lý biến vật lý & biến ảo (`BOOL`, `INT32`, `REAL`), tên gợi nhớ, đơn vị & nhập/xuất CSV. |
| `Ctrl+4` | `Ctrl+NumPad4` | **[4. Bảng Luật — Rule Table](/vi/workspaces/rule-table-tab)** | Kiểm tra dạng bảng các luật 4 tầng `Trigger -> Condition -> Guard -> Action` sau biên dịch. |
| `Ctrl+5` | `Ctrl+NumPad5` | **[5. Thư viện Mẫu — Blueprints](/vi/workspaces/blueprints-tab)** | Kho mạch mẫu công nghiệp chuẩn hóa sẵn sàng chèn nhanh vào dự án. |
| `Ctrl+6` | `Ctrl+NumPad6` | **[6. Triển khai — Deploy](/vi/workspaces/deploy-tab)** | Tiền kiểm tra an toàn, đối chiếu CRC-16 & nạp nguyên tử hai pha xuống bộ nhớ Flash. |
| `Ctrl+7` | `Ctrl+NumPad7` | **[7. Giám sát — Live Watch](/vi/workspaces/live-watch-tab)** | Bảng theo dõi biến mật độ cao, đồ thị xu hướng đa kênh & cưỡng bức biến có khóa bảo vệ. |

---

## 2. Vận hành, Biên dịch & Nghiệm thu Hiện trường (`F4` .. `F11`)

| Phím tắt | Lệnh Thực thi | Hành vi Vận hành Chi tiết |
| :--- | :--- | :--- |
| `F4` | **Kết nối / Ngắt kết nối Thiết bị** | Đóng hoặc mở phiên truyền thông trực tuyến (USB CDC COM ảo hoặc Modbus TCP `:502`) tới thiết bị SynaptiX. |
| `F5` | **Bật / Tắt Bộ Mô phỏng Ngoại tuyến** | Khởi chạy hoặc đóng ngăn [Mô phỏng Chu kỳ Chính xác Ngoại tuyến](/vi/tools/simulator-and-live-debug) trên máy tính. |
| `Ctrl+F5` | **Nạp Chương trình Xuống Thiết bị** | Biên dịch toàn bộ dự án và mở quy trình **Triển khai Nguyên tử** hai pha xuống phần cứng. |
| `F6` | **Đọc Cấu hình Từ Thiết bị (`Upload`)** | Đọc ngược chương trình đang chạy, bảng biến, cấu hình RS-485 và DTU từ phần cứng lên giao diện IDE. |
| `F7` | **Biên dịch Sơ đồ & Khối hàm** | Kiểm tra ngữ pháp đồ thị, đối chiếu kiểu dữ liệu vật lý, phát hiện xung đột ghi biến và sinh mã nhị phân. |
| `F8` | **Bật / Tắt Gỡ lỗi Trực tuyến (`Live Debug`)** | Phủ lớp dây dẫn phát sáng 20 FPS, giá trị chân cắm thực tế, thanh đếm `ET/PT` và cảnh báo `[STALE]` lên Sơ đồ Logic. |
| `F11` | **Chế độ Toàn màn hình (`Full Screen`)** | Tối đa hóa diện tích bản vẽ khi làm việc trên màn hình laptop nhỏ tại hiện trường tủ điện. |

---

## 3. Hộp thoại Truyền thông Công nghiệp & Trợ lý AI Copilot

| Phím tắt | Công cụ / Hộp thoại Mở ra | Tài liệu Hướng dẫn |
| :--- | :--- | :--- |
| `Ctrl+Space` | **Đóng / Mở Trợ lý SynaptiX AI Copilot** | Khung trợ lý tổng hợp sơ đồ khối từ ngôn ngữ tự nhiên & gợi ý sửa lỗi biên dịch ([Chi tiết](/vi/tools/ai-copilot)). |
| `Ctrl+Shift+B` | **Cấu hình Bus RS-485 & Bản đồ SCADA Slave** | Thiết lập vai trò Master/Slave cho `RS-485 #1..#4`, tốc độ Baud, bảng kênh & xuất bảng địa chỉ Slave ([Chi tiết](/vi/tools/rs485-and-scada-slave)). |
| `Ctrl+Shift+M` | **Cấu hình MQTT Broker & Bảo mật TLS** | Cài đặt địa chỉ Broker, tài khoản, di chúc LWT & nạp chứng chỉ X.509 Root CA / `mTLS` ([Chi tiết](/vi/tools/mqtt-and-wifi-config)). |
| `Ctrl+Shift+W` | **Cấu hình Wi-Fi & Quét Sóng Trực tiếp** | Cài đặt mạng không dây `STA` 2.4 GHz, khảo sát sóng `RSSI` tại tủ điện & địa chỉ IPv4 Tĩnh/DHCP ([Chi tiết](/vi/tools/mqtt-and-wifi-config)). |
| `Ctrl+Shift+D` | **Không gian DTU Gateway Studio** | Mở giao diện cấu hình cầu nối RS-485 lên Cloud (`MQTT` / `HTTP` / `TCP`) & phân tích khung truyền ([Chi tiết](/vi/tools/dtu-gateway-studio)). |

---

## 4. Quản lý Tệp Dự án & Chỉnh sửa Bảng nhớ Tạm

| Phím tắt | Phím Tương đương | Thao tác | Phạm vi & Ghi chú |
| :--- | :--- | :--- | :--- |
| `Ctrl+N` | — | **Tạo Dự án Mới** | Mở hộp thoại chọn Cấu hình Thiết bị (Device Preset) để khởi tạo dự án sạch. |
| `Ctrl+O` | — | **Mở Dự án** | Nạp tệp dự án SynaptiX đã lưu từ ổ cứng. |
| `Ctrl+S` | — | **Lưu Dự án** | Lưu tệp dự án hiện hành (đồng thời tự động biên dịch kiểm tra khi đang đứng tại Sơ đồ Logic). |
| `Ctrl+Shift+S` | — | **Lưu Dự án Thành...** | Nhân bản dự án hiện tại sang tên tệp hoặc thư mục lưu trữ mới. |
| `Ctrl+Z` | — | **Hoàn tác (`Undo`)** | Khôi phục lại thao tác đặt khối, nối dây, đổi tham số, căn chỉnh hoặc chèn mạch AI gần nhất. |
| `Ctrl+Y` | `Ctrl+Shift+Z` | **Làm lại (`Redo`)** | Thực hiện lại thao tác vừa hoàn tác trước đó. |
| `Ctrl+X` | — | **Cắt Khối Đang Chọn** | Sao chép các khối đang chọn vào bộ nhớ tạm và xóa gọn khỏi bản vẽ. |
| `Ctrl+C` | — | **Sao chép Khối Đang Chọn** | Sao chép các khối đã chọn (giữ nguyên tham số cấu hình và dây nối nội bộ giữa chúng). |
| `Ctrl+V` | — | **Dán Khối** | Dán cụm khối từ bộ nhớ tạm lên bản vẽ kèm độ lệch tọa độ thông minh tránh đè khối cũ. |
| `Ctrl+D` | — | **Nhân bản Nhanh (`Duplicate`)** | Nhân đôi ngay cụm khối đang chọn chỉ với một lần nhấn phím. |
| `Ctrl+A` | — | **Chọn Toàn bộ Khối** | Bôi chọn tất cả khối hàm hiện có trên Sơ đồ Logic. |
| `Delete` | — | **Xóa Phần tử Đang Chọn** | Xóa toàn bộ các khối hàm và/hoặc đường dây tín hiệu đang được bôi chọn. |

---

## 5. Căn lề, Dàn đều Đa khối & Bắt dính Lưới trên Canvas

Bôi chọn từ 2 khối hàm trở lên trên [Sơ đồ Logic (`Ctrl+2`)](/vi/workspaces/logic-graph-tab) để dàn trang bản vẽ kỹ thuật thẳng hàng tức thì:

| Phím tắt Chính | Phím Thay thế trên Canvas | Lệnh Căn chỉnh | Quy tắc Hình học |
| :--- | :--- | :--- | :--- |
| `Alt+Left` | `Ctrl+Shift+Left` | **Căn thẳng Mép Trái** | Đưa tọa độ mép trái `X` của các khối về thẳng cột với khối ngoài cùng bên trái. |
| `Alt+Right` | `Ctrl+Shift+Right` | **Căn thẳng Mép Phải** | Căn thẳng mép phải các khối theo khối ngoài cùng bên phải. |
| `Alt+Up` | `Ctrl+Shift+Up` | **Căn thẳng Mép Trên** | Đưa tọa độ mép trên `Y` của các khối về ngang hàng với khối cao nhất. |
| `Alt+Down` | `Ctrl+Shift+Down` | **Căn thẳng Mép Dưới** | Căn thẳng mép dưới các khối theo khối thấp nhất. |
| `Alt+C` | `Ctrl+Shift+C` | **Căn Giữa Theo Trục Dọc** | Xếp tâm ngang của các khối thẳng hàng trên cùng một trục cột `X`. |
| `Alt+M` | `Ctrl+Shift+M` *(trên canvas)* | **Căn Giữa Theo Hàng Ngang** | Xếp tâm dọc các khối thẳng tắp trên một đường ống tín hiệu ngang `Y`. |
| `Alt+Shift+H` | `Ctrl+Shift+H` | **Chia đều Khoảng cách Ngang** | Phân bố đều khoảng hở ngang giữa `3` khối trở lên từ trái sang phải. |
| `Alt+Shift+V` | `Ctrl+Shift+V` | **Chia đều Khoảng cách Dọc** | Phân bố đều khoảng hở dọc giữa `3` khối trở lên từ trên xuống dưới. |
| `Alt+G` | `Ctrl+Shift+G` | **Bắt dính Toàn bộ vào Lưới** | Làm tròn tọa độ mọi khối trên bản vẽ khớp chính xác vào mắt lưới tiêu chuẩn `20 px`. |

---

## 6. Thao tác Chuột & Điều hướng Khung nhìn Sơ đồ Logic

| Thao tác Chuột / Phím Bổ trợ | Tác động Trên Không gian Bản vẽ |
| :--- | :--- |
| **Cuộn Con lăn Chuột (`Mouse Wheel`)** | Phóng to / thu nhỏ mượt mà lấy vị trí trỏ chuột hiện tại làm tâm hội tụ. |
| **Nhấn giữ Chuột giữa & Kéo** *(hoặc Chuột phải kéo trên nền trống)* | Trượt ngang/dọc khung nhìn (`Pan`) tự do trên các sơ đồ lớn nhiều nhánh. |
| **Nhấn giữ Chuột trái & Quét Khung** | Vẽ khung chữ nhật quét chọn đồng thời nhiều khối hàm trên vùng bản vẽ. |
| **`Ctrl + Nhấp Chuột trái`** *(hoặc `Shift + Nhấp Chuột trái`)* | Thêm hoặc bớt từng khối hàm hay đường dây tín hiệu riêng lẻ vào danh sách đang chọn. |
| **Nhấp Đúp Chuột trái vào Khối trong Hộp công cụ** | Tự động thả khối mới ra bản vẽ và nối dây tiếp nối ngay sau khối đang chọn (`InsertNextConnectedNode`). |
| **Kéo từ Chân Ra -> Chân Vào** | Tạo dây dẫn tín hiệu có kiểm tra tương thích kiểu dữ liệu; chọn dây rồi nhấn `Delete` để cắt dây. |
