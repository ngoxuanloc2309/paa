# Yêu cầu Hệ thống & Cài đặt

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-GS-001</span>
  <span><strong>Chương:</strong> 02. Cài đặt &amp; Khởi tạo Nhanh</span>
  <span><strong>Hệ điều hành:</strong> Windows 10 / Windows 11 (x64)</span>
  <span><strong>Tiêu chuẩn:</strong> IEC/IEEE 82079-1 &bull; ANSI/ISA-101</span>
</div>

Tài liệu này hướng dẫn chi tiết cấu hình phần cứng/phần mềm máy trạm khuyến nghị, cách kiểm tra trình điều khiển cổng nối tiếp ảo USB CDC, quy tắc mở cổng tường lửa mạng công nghiệp, quy trình cài đặt **SynaptiX IDE** lần đầu và cơ chế nâng cấp phiên bản qua **Trình Quản lý Cập nhật Tích hợp**.

---

## Yêu cầu Cấu hình Máy trạm Kỹ thuật

**SynaptiX IDE** là bộ công cụ kỹ thuật máy tính để bàn tăng tốc phần cứng phát triển trên nền tảng WPF và .NET 8. Trước khi cài đặt, hãy đối chiếu cấu hình máy tính xách tay hiện trường hoặc máy trạm phòng điều khiển với bảng thông số sau:

| Thành phần | Cấu hình Tối thiểu | Cấu hình Khuyến nghị Khi Nghiệm thu Hiện trường |
| :--- | :--- | :--- |
| **Hệ điều hành** | Windows 10 (64-bit, Phiên bản 1909 trở lên) | Windows 11 Pro / Enterprise (64-bit, 22H2+) |
| **Nền tảng Thực thi** | .NET 8.0 Desktop Runtime (`win-x64`) *(đóng gói sẵn trong bộ cài)* | Bản vá bảo trì .NET 8.0 LTS mới nhất |
| **Vi xử lý (CPU)** | Dual-Core x64 @ 2.0 GHz | Quad-Core Intel Core i5 / AMD Ryzen 5 trở lên |
| **Bộ nhớ RAM** | 4 GB RAM | 8 GB – 16 GB RAM (tối ưu khi mở đồ thị Live Watch đa kênh & AI Copilot) |
| **Dung lượng Ổ cứng** | Trống 250 MB trên SSD/HDD | Trống 1 GB trên ổ cứng NVMe SSD (lưu trữ dự án & xuất nhật ký CSV) |
| **Màn hình & Công thái học** | `1366 x 768` @ 100% DPI | **`1920 x 1080` (Full HD)** hoặc `2560 x 1440` tấm nền chống chói (chuẩn công thái học ca trực 8–12 giờ ANSI/ISA-101) |
| **Cổng Giao tiếp** | 01 cổng USB 2.0/3.0 Type-A hoặc Type-C | Cổng USB + Card mạng Ethernet 100/1000 Mbps hoặc Wi-Fi |

::: tip Bộ Cài đặt Độc lập Không Cần Mạng Internet (Offline Installer)
Gói cài đặt chính thức `win-x64 setup` của **SynaptiX IDE** đã đóng gói đầy đủ thư viện phụ thuộc cần thiết, cho phép kỹ sư cài đặt và vận hành trơn tru ngay trên các máy tính chuyên dụng trong mạng nhà máy cô lập (Air-gapped OT Network) không có kết nối Internet.
:::

---

## Quy trình Cài đặt & Khởi chạy Lần đầu

1. **Tải Gói Cài đặt Chính thức:** Tải tệp cài đặt `SynaptiX-IDE-Setup-win-x64.exe` từ cổng phân phối nội bộ hoặc kênh phát hành chính thức của SynaptiX.
2. **Thực thi Trình Cài đặt:** Nhấp đúp vào tệp cài đặt, xác nhận quyền quản trị Windows UAC (nếu cài cho toàn bộ người dùng trên máy) và chọn thư mục đích.
3. **Thư mục Lưu Dự án Mặc định:** Ngay trong lần khởi chạy đầu tiên, **SynaptiX IDE** tự động khởi tạo thư mục lưu trữ dự án tiêu chuẩn tại:

```text
%USERPROFILE%\Documents\SynaptiX\Projects
```
4. **Chuyển đổi Ngôn ngữ Giao diện Tức thời:** Kỹ sư có thể chuyển đổi qua lại giữa **Tiếng Việt** và **English** bất kỳ lúc nào tại menu **Công cụ (Tools) -> Ngôn ngữ (Language)** mà không cần khởi động lại phần mềm hay đóng dự án đang mở.

---

## Kiểm tra Trình điều khiển USB CDC & Tường lửa Mạng

**SynaptiX IDE** hỗ trợ kết nối tới **Bộ Điều khiển Biên SynaptiX** và **Cổng DTU** qua hai phương thức vật lý chính: **Cổng nối tiếp ảo USB CDC** (cắm cáp trực tiếp tại tủ điện) và **Modbus TCP** (qua mạng LAN Ethernet / Wi-Fi).

### 1. Xác minh Cổng Nối tiếp Ảo USB CDC trên Windows

Windows 10 và Windows 11 đều tích hợp sẵn trình điều khiển chuẩn USB Communications Device Class (`usbser.sys`) của Microsoft — kỹ sư cắm cáp là nhận diện ngay mà không cần cài thêm driver bên thứ ba.

Cách kiểm tra nhanh khi cắm cáp USB từ **Thiết bị SynaptiX** đã cấp nguồn vào máy tính:
1. Nhấn tổ hợp `Win + X` và chọn **Device Manager** (Trình quản lý thiết bị).
2. Mở rộng nhánh **Ports (COM & LPT)**.
3. Kiểm tra sự xuất hiện của mục **USB Serial Device (`COMx`)** (ví dụ `COM3` hoặc `COM7`) hoạt động bình thường không có biểu tượng chấm than vàng.
4. Quay lại **SynaptiX IDE**, gạt công tắc giao thức góc trên bên phải sang **`USB CDC`**, nhấn nút **Làm mới cổng (Refresh Ports)** cạnh ô chọn cổng, chọn đúng `COMx` và nhấn **Kết nối (`F4`)**.

::: warning Xử lý Sự cố Không Thấy Cổng COM Khi Cắm Cáp USB
- **Cáp USB Chỉ Sạc (Charge-Only Cable):** Rất nhiều sợi cáp USB-C / Micro-USB dân dụng bị lược bỏ cặp dây dữ liệu `D+` / `D-`. Hãy luôn sử dụng cáp dữ liệu USB có bọc chống nhiễu đạt chuẩn công nghiệp.
- **Tranh chấp Cổng COM Độc quyền:** Trên Windows, mỗi cổng nối tiếp `COMx` chỉ cho phép duy nhất một phần mềm mở tại một thời điểm. Hãy đóng các phần mềm đọc cổng COM hoặc công cụ quét Modbus bên ngoài trước khi nhấn **Kết nối (`F4`)** trên **SynaptiX IDE**.
:::

### 2. Bảng Quy tắc Cổng Mạng & Tường lửa Công nghiệp (VLAN / Firewall)

Khi kết nối cấu hình từ xa qua **Modbus TCP** hoặc kiểm thử luồng đẩy dữ liệu đám mây trong mạng nội bộ nhà máy, hãy đảm bảo Windows Defender Firewall và danh sách kiểm soát truy cập (ACL) trên bộ định tuyến mở các cổng sau:

| Giao thức / Dịch vụ | Giao thức & Cổng | Chiều Kết nối | Mục đích Vận hành Kỹ thuật |
| :--- | :---: | :--- | :--- |
| **Cấu hình & SCADA Modbus TCP** | `TCP 502` *(tùy chỉnh được)* | Máy trạm / SCADA -> Thiết bị SynaptiX | Kết nối từ xa trên **SynaptiX IDE** (`F4`), Nạp nguyên tử (`Ctrl+F5`), Giám sát Live Watch (`Ctrl+7`) và SCADA đọc Slave. |
| **MQTT Broker (Tiêu chuẩn)** | `TCP 1883` | Thiết bị SynaptiX -> Broker | Truyền gói tin Telemetry (`MQTT_PUB`) và nhận lệnh điều khiển (`MQTT_SUB`) trong mạng LAN nội bộ. |
| **MQTT Bảo mật qua TLS** | `TCP 8883` | Thiết bị SynaptiX -> Cloud Broker | Truyền thông IIoT mã hóa TLS kèm xác thực chứng chỉ số hai chiều trên môi trường Internet. |
| **HTTP / HTTPS Webhook** | `TCP 80` / `TCP 443` | Thiết bị SynaptiX -> Máy chủ REST | Khối `HTTP_CLIENT` gửi dữ liệu REST API và **SynaptiX IDE** kiểm tra bản cập nhật mới (`TCP 443`). |
| **Raw Socket / Luồng DTU** | `Cổng TCP` tùy chọn | Thiết bị SynaptiX -> Máy chủ Đích | Truyền luồng Socket TCP thô (`TCP_CLIENT` và chế độ [DTU Gateway Studio](/vi/tools/dtu-gateway-studio)). |

---

## Quản lý Nâng cấp Phiên bản với Software Update Manager

**SynaptiX IDE** trang bị sẵn **Trình Quản lý Cập nhật Phần mềm** thiết kế chuẩn công nghiệp — tuyệt đối không bao giờ tự ý tải ngầm gây nghẽn băng thông hay tự khởi động lại máy khi kỹ sư đang kết nối trực tuyến với thiết bị tại hiện trường.

![Hộp thoại Kiểm tra & Cập nhật Phiên bản Tích hợp trên SynaptiX IDE](/images/synaptix_update_dialog.png)

### Quy trình Kiểm tra & Cập nhật An toàn

1. **Huy hiệu Thông báo Tinh tế dưới Thanh Trạng thái:** Khi máy trạm có kết nối Internet (`HTTPS / TCP 443`), **SynaptiX IDE** kiểm tra nhẹ nhàng bản phát hành mới ở chế độ nền. Nếu có bản mới hơn, một huy hiệu màu xám công nghiệp **Update Available** sẽ xuất hiện gọn gàng ở góc dưới bên phải thanh trạng thái mà không làm gián đoạn thao tác vẽ sơ đồ.
2. **Kiểm tra Thủ công Bất kỳ Lúc nào:** Kỹ sư có thể chủ động kiểm tra ngay qua menu **Trợ giúp (Help) -> Kiểm tra cập nhật (Check for Updates...)** hoặc nhấp trực tiếp vào huy hiệu dưới thanh trạng thái.
3. **Rà soát Thông số Bản phát hành & Nhật ký Thay đổi (Changelog):** Hộp thoại cập nhật cung cấp đầy đủ thông tin minh bạch:
   - Đối chiếu rõ **Phiên bản Hiện tại** và **Phiên bản Mới nhất** (ví dụ `v2.2.1 -> v2.3.0`).
   - Hiển thị **Dung lượng gói cài đặt** (`MB`) và nhãn kiến trúc (`win-x64 setup`).
   - Khung **Nhật ký Thay đổi Kỹ thuật (Changelog)** liệt kê chi tiết các khối hàm mới, cải tiến giao thức và các lỗi đã khắc phục.
4. **Tải xuống Một Chạm & Cơ chế Dự phòng Qua Trình duyệt:**
   - Nhấn **Cập nhật ngay (Update Now)** để tải gói cài đặt kèm thanh tiến trình phần trăm trực quan, hoặc chọn **Để sau (Later)** để tiếp tục hoàn tất ca nghiệm thu hiện tại.
   - Trong môi trường mạng doanh nghiệp giới hạn truy cập API, bộ cập nhật tự động chuyển hướng dự phòng thông minh và cung cấp sẵn nút **Mở trong trình duyệt (Open in Browser)** để tải gói cài đặt thủ công dễ dàng.

::: danger Lưu Dự án & Ngắt Kết nối Thiết bị Trước Khi Nâng cấp
Trước khi nhấn **Cập nhật ngay (Update Now)** để chạy bộ cài phiên bản mới, hãy luôn lưu lại dự án đang làm việc (`Ctrl+S`) và ngắt phiên kết nối trực tuyến với thiết bị (`F4`) nhằm đảm bảo an toàn tuyệt đối cho dữ liệu dự án.
:::

---

## Bước Tiếp theo

- [Giải phẫu Giao diện SynaptiX IDE (UI Tour)](/vi/getting-started/ui-tour) — Làm chủ bố cục 6 phân vùng công thái học, thanh kết nối nhanh và hệ thống phím tắt.
- [Dự án Đầu tiên trong 10 Phút](/vi/getting-started/quickstart-first-project) — Thực hành trọn vẹn từ tạo dự án, khai báo Tag, vẽ mạch bảo vệ quá nhiệt, mô phỏng Offline đến nạp xuống thiết bị và giám sát đồ thị.
