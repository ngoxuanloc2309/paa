# -*- coding: utf-8 -*-
"""
SynaptiX Industrial User Manual - Vietnamese Curated Content
Curated for high technical density, concise industrial readability, and strict IEC/IEEE standards.
Target page count: ~55-60 pages A4.
"""

import os

PUBLIC_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "public"))
IMG_DIR = os.path.abspath(os.path.join(PUBLIC_DIR, "images")).replace("\\", "/")

def img(name: str) -> str:
    return f"file:///{IMG_DIR}/{name}"

CHAPTERS_VI = [
    {
        "id": "ch1",
        "num": 1,
        "title": "Chương 1: Giới thiệu Hệ sinh thái Thiết bị & Kiến trúc Cốt lõi",
        "sections": [
            ("1.1", "Tổng quan Nền tảng & Danh mục Thiết bị SynaptiX IIoT"),
            ("1.2", "Kiến trúc Bộ nhớ Tag & Kiểu Dữ liệu Công nghiệp"),
            ("1.3", "Chu trình Quét Thực thi Tất định (Deterministic Execution)"),
        ],
        "content": f"""
        <section class="section-container" id="sec-1-1">
          <h2 class="section-title"><span class="sec-num">1.1</span> Tổng quan Nền tảng &amp; Danh mục Thiết bị SynaptiX IIoT</h2>
          <p><strong>SynaptiX IDE</strong> là môi trường kỹ thuật lập trình điều khiển logic khối hàm (FBD), cấu hình mạng truyền thông trường và quản trị thiết bị biên thuộc hệ sinh thái của <strong>Công ty Cổ phần Công nghệ Synaptix</strong> (<code>synaptix-io.com</code>). Phần mềm được kiến trúc theo chuẩn <strong>IEC 61131-3</strong>, tối ưu cho chu trình quét tất định trên vi điều khiển thời gian thực và tích hợp điện toán đám mây AI/IoT theo phương châm <em>"Edge &bull; Gateway &bull; Cloud &bull; AI"</em>.</p>

          <div class="table-caption">Bảng 1.1: Danh mục Phần cứng Thiết bị trong Hệ sinh thái SynaptiX IIoT</div>
          <table>
            <thead>
              <tr>
                <th style="width: 22%;">Dòng Thiết bị</th>
                <th style="width: 20%;">Mã Phần cứng</th>
                <th style="width: 33%;">Giao tiếp &amp; Năng lực Xử lý</th>
                <th style="width: 25%;">Ứng dụng Trọng tâm</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>Data Logger</strong></td>
                <td><code>miniLog MLV2</code></td>
                <td>Lưu trữ dữ liệu Flash tại chỗ, 4G LTE Cat-1, Modbus RS-485, pin nuôi RTC.</td>
                <td>Trạm quan trắc môi trường, trạm đo lưu lượng nước, trạm khí tượng.</td>
              </tr>
              <tr>
                <td><strong>Remote I/O</strong></td>
                <td><code>XL-WFIO-44</code></td>
                <td>4 Digital Input, 4 Relay Output, Wi-Fi 2.4GHz công nghiệp, Modbus TCP.</td>
                <td>Điều khiển đóng cắt bơm/quạt từ xa, giám sát tiếp điểm phụ tủ điện.</td>
              </tr>
              <tr>
                <td><strong>Industrial Gateway</strong></td>
                <td><code>XLink Gateway</code></td>
                <td>Đa giao thức: LoRa 433/868MHz, Zigbee 3.0 Mesh, RS-485, Ethernet LAN.</td>
                <td>Thu thập mạng cảm biến không dây nhà xưởng, chuyển đổi Modbus sang MQTT.</td>
              </tr>
              <tr>
                <td><strong>HMI Smart Display</strong></td>
                <td><code>HMI 7-inch CM4</code></td>
                <td>Màn cảm ứng công nghiệp 7", tích hợp Raspberry Pi CM4 chạy Edge AI, 2x Ethernet.</td>
                <td>Bảng điều khiển tại chỗ máy CNC, giám sát hiệu suất OEE máy đóng gói.</td>
              </tr>
              <tr>
                <td><strong>Edge Controller</strong></td>
                <td><code>SYNX-CTRL-884</code></td>
                <td>8 DI, 8 DO, 4 AI (4-20mA), 2 cổng RS-485 độc lập, vi điều khiển ARM Cortex-M4.</td>
                <td>Điều khiển chu trình tự động hóa máy móc, khóa liên động an toàn dây chuyền.</td>
              </tr>
            </tbody>
          </table>

          <div class="callout callout-info">
            <div class="callout-header">ℹ️ HỆ SINH THÁI MỞ ĐỒNG NHẤT</div>
            Toàn bộ các dòng thiết bị phần cứng trong bảng trên đều sử dụng chung một tập lệnh logic, một cấu trúc bộ nhớ Tag và được lập trình, cấu hình trực tiếp từ cùng một giao diện <strong>SynaptiX IDE</strong>.
          </div>
        </section>

        <section class="section-container" id="sec-1-2">
          <h2 class="section-title"><span class="sec-num">1.2</span> Kiến trúc Bộ nhớ Tag &amp; Kiểu Dữ liệu Công nghiệp</h2>
          <p>Tuân thủ nguyên tắc <strong>Chân lý Phần cứng (Hardware Ground Truth)</strong>, dữ liệu trong SynaptiX không đơn thuần là các nhãn hiển thị mà là <strong>hợp đồng bộ nhớ vật lý</strong> phản ánh chính xác cấu trúc RAM/Flash của vi điều khiển. Hệ thống phân chia không gian nhớ thành các phân vùng Tag cố định, tránh hiện tượng phân mảnh:</p>

          <div class="table-caption">Bảng 1.2: Không gian Bộ nhớ Tag Vật lý &amp; Ánh xạ Thanh ghi</div>
          <table>
            <thead>
              <tr>
                <th style="width: 15%;">Phân vùng Tag</th>
                <th style="width: 15%;">Kiểu Dữ liệu</th>
                <th style="width: 12%;">Quyền Truy cập</th>
                <th style="width: 28%;">Kích thước &amp; Giới hạn</th>
                <th style="width: 30%;">Chức năng Kỹ thuật</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><code>DI0 .. DI7</code></td>
                <td><code>BOOL</code> (1-bit)</td>
                <td>Chỉ đọc (RO)</td>
                <td>8 ngõ vào số cách ly quang (Opto)</td>
                <td>Nút nhấn, công tắc hành trình, cảm biến tiệm cận, tiếp điểm E-Stop.</td>
              </tr>
              <tr>
                <td><code>DO0 .. DO7</code></td>
                <td><code>BOOL</code> (1-bit)</td>
                <td>Đọc / Ghi (RW)</td>
                <td>8 ngõ ra Relay hoặc Transistor NPN</td>
                <td>Khởi động từ, cuộn hút van điện từ, đèn tháp tín hiệu, còi báo động.</td>
              </tr>
              <tr>
                <td><code>AI0 .. AI3</code></td>
                <td><code>REAL</code> (Float32)</td>
                <td>Chỉ đọc (RO)</td>
                <td>4 kênh ADC 12-bit (0-10V / 4-20mA)</td>
                <td>Cảm biến áp suất, lưu lượng, nhiệt độ PT100, mức bồn analog.</td>
              </tr>
              <tr>
                <td><code>VFLAG0 .. 31</code></td>
                <td><code>BOOL</code> (1-bit)</td>
                <td>Đọc / Ghi (RW)</td>
                <td>32 cờ nhớ ảo nội tại trong RAM</td>
                <td>Biến trung gian liên động logic, cờ báo trạng thái chế độ Auto/Manual.</td>
              </tr>
              <tr>
                <td><code>VREG0 .. 31</code></td>
                <td><code>INT32 / REAL</code></td>
                <td>Đọc / Ghi (RW)</td>
                <td>32 thanh ghi số nguyên hoặc thực 32-bit</td>
                <td>Ngưỡng so sánh nhiệt độ, thời gian đặt timer, kết quả tính toán số học.</td>
              </tr>
              <tr>
                <td><code>VREG_RETAIN</code></td>
                <td><code>INT32 / REAL</code></td>
                <td>Đọc / Ghi (RW)</td>
                <td>16 thanh ghi lưu Flash khi mất điện</td>
                <td>Tham số cấu hình hệ thống, số ca sản xuất, lưu lượng tích lũy tổng.</td>
              </tr>
              <tr>
                <td><code>COUNTER0 .. 7</code></td>
                <td><code>INT32</code> (32-bit)</td>
                <td>Đọc / Ghi (RW)</td>
                <td>8 thanh ghi tích lũy bộ đếm sản phẩm</td>
                <td>Sản lượng máy đóng gói, số lần dập khuôn, nhịp xung Encoder.</td>
              </tr>
            </tbody>
          </table>

          <div class="callout callout-warning">
            <div class="callout-header">⚠️ KHÔNG GHI ĐÈ LÊN PHÂN VÙNG CHỈ ĐỌC (RO)</div>
            Các phân vùng <code>DI</code> và <code>AI</code> là cổng đọc trạng thái vật lý thực tế. Mọi lệnh ghi (ACTION hoặc Modbus SCADA Write) vào các biến này sẽ bị Trình biên dịch báo lỗi <code>ERR_ACTION_READONLY_TARGET</code> ngay khi nhấn <code>F7</code>.
          </div>
        </section>

        <section class="section-container" id="sec-1-3">
          <h2 class="section-title"><span class="sec-num">1.3</span> Chu trình Quét Thực thi Tất định (Deterministic Execution)</h2>
          <p>Mỗi chu kỳ quét (Scan Cycle) trên bộ điều khiển SynaptiX được thực thi tuần tự tuyến tính <strong>tất định 100%</strong> theo mô hình 4 giai đoạn khép kín, triệt tiêu hoàn toàn rủi ro tranh chấp tài nguyên (Race Condition) và đảm bảo không có cấp phát bộ nhớ động (Zero-Allocation Hot Path):</p>

          <ol>
            <li><strong>Giai đoạn 1 — Chụp ảnh Ngõ vào (Input Snapshot):</strong> Bộ điều khiển quét đồng thời toàn bộ chân vật lý <code>DI0..DI7</code>, giải mã dòng tương tự <code>AI0..AI3</code> và lấy dữ liệu đệm từ cổng RS-485/Ethernet vào bộ nhớ ảnh ngõ vào.</li>
            <li><strong>Giai đoạn 2 — Đánh giá Logic Khối hàm (Logic Execution):</strong> Toàn bộ mạng FBD và chuỗi luật được duyệt theo thứ tự cấu trúc tô-pô. Các cổng khóa <code>GUARD</code> thẩm tra điều kiện an toàn trước khi cho phép kích hoạt tác động <code>ACTION</code>.</li>
            <li><strong>Giai đoạn 3 — Cập nhật Bộ nhớ Đích (Memory Commit):</strong> Kết quả tính toán từ các khối <code>MATH</code>, bộ đếm thời gian <code>TON</code> và cờ logic được chốt nguyên tử (Atomic Commit) vào RAM.</li>
            <li><strong>Giai đoạn 4 — Xuất Phần cứng &amp; Telemetry (Output Flush &amp; Comms):</strong> Chốt đồng loạt trạng thái <code>DO0..DO7</code> ra rơ-le vật lý và đẩy các gói tin telemetry MQTT/Modbus ra mạng ngoài.</li>
          </ol>
        </section>
        """
    },
    {
        "id": "ch2",
        "num": 2,
        "title": "Chương 2: Cài đặt Phần mềm & Kết nối Thiết bị",
        "sections": [
            ("2.1", "Yêu cầu Hệ thống & 5 Bước Cài đặt Inno Setup"),
            ("2.2", "Kết nối Cáp USB CDC & Cấu hình Cổng COM Ảo"),
            ("2.3", "Kết nối Mạng Ethernet / Modbus TCP & Đặt IP Tĩnh"),
        ],
        "content": f"""
        <section class="section-container" id="sec-2-1">
          <h2 class="section-title"><span class="sec-num">2.1</span> Yêu cầu Hệ thống &amp; 5 Bước Cài đặt Inno Setup</h2>
          <p>Phần mềm SynaptiX IDE được đóng gói bằng trình cài đặt công nghiệp chuẩn <strong>Inno Setup 6</strong> với thuật toán nén nguyên khối <strong>LZMA2 ultra</strong>, tích hợp sẵn nhân thực thi .NET 8 độc lập (Self-Contained) và công cụ giả lập phần cứng ảo <code>Tools/SimplePLC.McuEmulator.exe</code>.</p>

          <div class="table-caption">Bảng 2.1: Cấu hình Phần cứng Máy tính Kỹ thuật Yêu cầu</div>
          <table>
            <thead>
              <tr>
                <th style="width: 25%;">Hạng mục Phần cứng</th>
                <th style="width: 35%;">Cấu hình Tối thiểu</th>
                <th style="width: 40%;">Cấu hình Khuyến nghị Vận hành Nhà máy</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>Hệ điều hành</strong></td>
                <td>Windows 10 64-bit (Version 1809 trở lên)</td>
                <td>Windows 11 Pro 64-bit / Windows Server 2022</td>
              </tr>
              <tr>
                <td><strong>Bộ vi xử lý (CPU)</strong></td>
                <td>Intel Core i3 / AMD Ryzen 3 (2.0 GHz)</td>
                <td>Intel Core i5 / i7 Thế hệ 11+ (4 nhân, 3.2 GHz trở lên)</td>
              </tr>
              <tr>
                <td><strong>Bộ nhớ RAM</strong></td>
                <td>4 GB RAM trống</td>
                <td>8 GB &ndash; 16 GB DDR4/DDR5</td>
              </tr>
              <tr>
                <td><strong>Ổ đĩa lưu trữ</strong></td>
                <td>1.5 GB ổ đĩa SSD khả dụng</td>
                <td>Ổ đĩa NVMe SSD (đảm bảo tốc độ mở dự án &lt; 2 giây)</td>
              </tr>
              <tr>
                <td><strong>Cổng giao tiếp</strong></td>
                <td>1 cổng USB 2.0 / USB-C truyền dữ liệu</td>
                <td>2 cổng USB-C, 1 cổng Ethernet RJ45 100/1000 Mbps</td>
              </tr>
            </tbody>
          </table>

          <div class="figure-container">
            <img src="{img('synaptix_setup_step1_welcome.png')}" alt="Màn hình Chào mừng Cài đặt SynaptiX IDE">
            <div class="figure-caption">Hình 2.1: Bước 1 &ndash; Màn hình Chào mừng Trình cài đặt Tiêu chuẩn Inno Setup 6</div>
          </div>

          <div class="figure-container">
            <img src="{img('synaptix_setup_step2_license.png')}" alt="Thỏa thuận Bản quyền EULA">
            <div class="figure-caption">Hình 2.2: Bước 2 &ndash; Thỏa thuận Cấp phép Sử dụng Phần mềm (EULA)</div>
          </div>

          <div class="figure-container">
            <img src="{img('synaptix_setup_step3_directory.png')}" alt="Chọn Thư mục Cài đặt SynaptiX IDE">
            <div class="figure-caption">Hình 2.3: Bước 3 &ndash; Chọn Thư mục Doanh nghiệp (%ProgramFiles%\\Synaptix Automation)</div>
          </div>

          <div class="figure-container">
            <img src="{img('synaptix_setup_step4_tasks.png')}" alt="Tùy chọn Bổ sung Cài đặt">
            <div class="figure-caption">Hình 2.4: Bước 4 &ndash; Tự động Đăng ký Đuôi File (.splc) và Bộ Giả lập McuEmulator</div>
          </div>

          <div class="figure-container">
            <img src="{img('synaptix_setup_step5_finish.png')}" alt="Hoàn tất Cài đặt SynaptiX IDE">
            <div class="figure-caption">Hình 2.5: Bước 5 &ndash; Hoàn tất Cài đặt và Tùy chọn Khởi chạy Ngay</div>
          </div>
        </section>

        <section class="section-container" id="sec-2-2">
          <h2 class="section-title"><span class="sec-num">2.2</span> Kết nối Cáp USB CDC &amp; Cấu hình Cổng COM Ảo</h2>
          <p>Để nạp chương trình và cấu hình trực tiếp từ máy tính, kết nối cổng USB-C của thiết bị SynaptiX Controller với cổng USB máy tính qua cáp truyền dữ liệu chuẩn:</p>
          <ol>
            <li>Cắm cáp USB-C. Thiết bị tự động khởi tạo giao tiếp <strong>USB CDC (Communications Device Class)</strong>, Windows tự động nhận diện driver cổng nối tiếp ảo (Virtual COM Port) mà không cần cài thêm driver bên ngoài.</li>
            <li>Nhấn phím <code>F4</code> (hoặc bấm biểu tượng <strong>Kết nối Thiết bị</strong> trên thanh công cụ IDE).</li>
            <li>Trong danh sách cổng kết nối, chọn đúng tên cổng (ví dụ: <code>COM3 &bull; SynaptiX Controller (STMicroelectronics Virtual COM Port)</code>), tốc độ mặc định <code>115200 bps, 8N1</code>.</li>
            <li>Nhấn <strong>Kết nối</strong>. Thanh trạng thái dưới đáy màn hình chuyển sang màu xanh ngọc <code>CONNECTED [COM3 &bull; 115200]</code>.</li>
          </ol>

          <div class="callout callout-danger">
            <div class="callout-header">🛑 CẢNH BÁO BẪY CÁP SẠC 2 SỢI</div>
            Hơn 60% trường hợp phần mềm không nhận cổng COM tại hiện trường là do dùng nhầm cáp sạc điện thoại giá rẻ (chỉ có 2 sợi nguồn mà thiếu cặp dây dữ liệu <code>D+/D-</code>). Luôn sử dụng cáp USB-C đi kèm theo hộp thiết bị Synaptix.
          </div>
        </section>

        <section class="section-container" id="sec-2-3">
          <h2 class="section-title"><span class="sec-num">2.3</span> Kết nối Mạng Ethernet / Modbus TCP &amp; Đặt IP Tĩnh</h2>
          <p>Đối với các thiết bị lắp đặt cố định trong tủ điều khiển trung tâm (như <code>miniLog MLV2</code>, <code>XLink Gateway</code>, <code>SYNX-CTRL-884</code>), kết nối qua cáp mạng RJ45 là giải pháp tối ưu chống nhiễu công nghiệp:</p>
          <ul>
            <li>Cổng mạng mặc định của thiết bị chạy ở địa chỉ IP Tĩnh: <code>192.168.1.200</code>, Subnet: <code>255.255.255.0</code>, Cổng Modbus TCP: <code>502</code>.</li>
            <li>Để kết nối, đặt card mạng máy tính cùng lớp mạng (ví dụ: <code>192.168.1.50</code>).</li>
            <li>Trên SynaptiX IDE, nhấn <code>F4</code>, chọn tab <strong>Ethernet / Modbus TCP</strong>, nhập IP <code>192.168.1.200</code> và Port <code>502</code>, sau đó bấm <strong>Kết nối</strong>.</li>
          </ul>
        </section>
        """
    },
    {
        "id": "ch3",
        "num": 3,
        "title": "Chương 3: Giải phẫu 7 Phân hệ Không gian Làm việc (Workspaces)",
        "sections": [
            ("3.1", "Tổng quan Cấu trúc Không gian Làm việc & Phím Nóng"),
            ("3.2", "Phân hệ Thiết bị (Device) & Sơ đồ Logic (Logic Graph)"),
            ("3.3", "Danh mục Biến (Tag Catalog), Bảng Luật & Triển khai Nạp (Deploy Gate)"),
        ],
        "content": f"""
        <section class="section-container" id="sec-3-1">
          <h2 class="section-title"><span class="sec-num">3.1</span> Tổng quan Cấu trúc Không gian Làm việc &amp; Phím Nóng</h2>
          <p>Giao diện SynaptiX IDE được thiết kế tuân thủ nghiêm ngặt tiêu chuẩn công thái học <strong>ANSI/ISA-101</strong> với nguyên tắc <em>"The Gray Principle (Quy tắc 90/10)"</em>: 90% diện tích màn hình sử dụng gam màu xám công nghiệp trung tính để bảo vệ mắt kỹ sư trong ca trực dài, 10% màu sắc rực rỡ chỉ dành riêng cho các tín hiệu cảnh báo và trạng thái mang điện thời gian thực.</p>

          <div class="table-caption">Bảng 3.1: 7 Phân hệ Không gian Làm việc Cốt lõi &amp; Phím tắt Chuyển đổi Nhanh</div>
          <table>
            <thead>
              <tr>
                <th style="width: 18%;">Không gian Làm việc</th>
                <th style="width: 14%;">Phím tắt</th>
                <th style="width: 38%;">Chức năng Kỹ thuật &amp; Nhiệm vụ Chính</th>
                <th style="width: 30%;">Tiêu chuẩn / Công cụ Tích hợp</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>1. Thiết bị (Device)</strong></td>
                <td><code>Ctrl + 1</code></td>
                <td>Nhận diện phần cứng, kiểm tra thông số chip MCU, đồng bộ giờ RTC, cài đặt cổng RS-485 và cấu hình Wi-Fi.</td>
                <td>Hộp thoại Thông tin Thiết bị, Quét sóng Wi-Fi, Đồng bộ RTC.</td>
              </tr>
              <tr>
                <td><strong>2. Sơ đồ Logic (Graph)</strong></td>
                <td><code>Ctrl + 2</code></td>
                <td>Bản vẽ thiết kế khối hàm FBD trực quan, kéo dây nối tín hiệu, gỡ lỗi trực tuyến <code>F8</code> sáng dây dẫn.</td>
                <td>Canvas IEC 61131-3, Thước căn chỉnh Snap-to-Grid, Mini-map.</td>
              </tr>
              <tr>
                <td><strong>3. Danh mục Biến (Tags)</strong></td>
                <td><code>Ctrl + 3</code></td>
                <td>Khai báo nhãn định danh (Alias), gán đơn vị kỹ thuật, định hình kiểu dữ liệu và bảng ánh xạ thanh ghi.</td>
                <td>Bảng dữ liệu mật độ cao chuẩn IBM Carbon, Tìm kiếm tức thì.</td>
              </tr>
              <tr>
                <td><strong>4. Bảng Luật (Rules)</strong></td>
                <td><code>Ctrl + 4</code></td>
                <td>Xem dạng bảng khai báo quan hệ điều khiển (Trigger &rarr; Condition &rarr; Guard &rarr; Action).</td>
                <td>Kiểm soát tính tường minh, lọc luật theo trạng thái kích hoạt.</td>
              </tr>
              <tr>
                <td><strong>5. Thư viện Mẫu (Blueprints)</strong></td>
                <td><code>Ctrl + 5</code></td>
                <td>Kho mạch mẫu chuẩn công nghiệp: Khởi động Sao-Tam giác, Bơm luân phiên, Đổi tín hiệu 4-20mA, Đọc điện năng.</td>
                <td>Tái sử dụng mạch logic đã kiểm chứng 1-click kéo thả.</td>
              </tr>
              <tr>
                <td><strong>6. Triển khai (Deploy)</strong></td>
                <td><code>Ctrl + 6</code></td>
                <td>Trạm kiểm soát an toàn trước khi nạp: Tiền kiểm tra biên dịch <code>F7</code>, so sánh sai khác (Diff) và nạp nguyên tử.</td>
                <td>Cơ chế Two-Phase Commit an toàn cho vi điều khiển biên.</td>
              </tr>
              <tr>
                <td><strong>7. Giám sát (Live Watch)</strong></td>
                <td><code>Ctrl + 7</code></td>
                <td>Bảng dữ liệu đo lường thời gian thực 20 FPS, vẽ đồ thị biến thiên đa kênh và cơ chế cưỡng bức biến an toàn.</td>
                <td>Đồ thị xu hướng Trend Chart, Cưỡng bức Tag có Lease bảo vệ.</td>
              </tr>
            </tbody>
          </table>

          <div class="figure-container">
            <img src="{img('synaptix_main_anatomy.png')}" alt="Giải phẫu Giao diện Người dùng SynaptiX IDE">
            <div class="figure-caption">Hình 3.1: Giải phẫu Tổng thể Giao diện Kỹ thuật SynaptiX IDE (7 Tab Workspaces)</div>
          </div>
        </section>

        <section class="section-container" id="sec-3-2">
          <h2 class="section-title"><span class="sec-num">3.2</span> Phân hệ Thiết bị (Device) &amp; Sơ đồ Logic (Logic Graph)</h2>
          <p>Phân hệ <strong>Device (Ctrl+1)</strong> cung cấp cái nhìn toàn diện về tình trạng sức khỏe của bo mạch: phiên bản Firmware, điện áp nguồn cấp 24V, nhiệt độ chip MCU và cho phép đồng bộ đồng hồ thời gian thực RTC với máy tính chỉ bằng 1 cú nhấp chuột.</p>
          <p>Phân hệ <strong>Logic Graph (Ctrl+2)</strong> là trái tim của IDE. Tại đây, kỹ sư thiết kế mạch điều khiển bằng cách kéo các khối từ thanh công cụ bên trái thả vào Canvas, nhấp chuột vào chân cắm ngõ ra kéo sang chân ngõ vào của khối tiếp theo. Toàn bộ dây dẫn được định tuyến trực giao (Orthogonal Routing) sắc cạnh, không rối mắt.</p>

          <div class="figure-container">
            <img src="{img('synaptix_logic_editor.png')}" alt="Không gian Sơ đồ Logic FBD">
            <div class="figure-caption">Hình 3.2: Không gian Thiết kế Sơ đồ Logic Khối hàm FBD (Logic Graph Canvas)</div>
          </div>
        </section>

        <section class="section-container" id="sec-3-3">
          <h2 class="section-title"><span class="sec-num">3.3</span> Danh mục Biến (Tag Catalog), Bảng Luật &amp; Triển khai Nạp (Deploy Gate)</h2>
          <p>Tại <strong>Tag Catalog (Ctrl+3)</strong>, mọi biến vật lý và ảo đều được quản lý tập trung. Kỹ sư có thể đặt tên gợi nhớ thân thiện (ví dụ: đổi <code>AI0</code> thành <code>Tank_Temperature</code>, đơn vị <code>°C</code>) để hiển thị đồng bộ lên toàn bộ bản vẽ FBD.</p>
          <p>Trước khi chương trình được nạp xuống phần cứng, phân hệ <strong>Deploy (Ctrl+6)</strong> kích hoạt cổng kiểm soát an toàn nghiêm ngặt (Verification Gate). Nếu phát hiện bất kỳ lỗi lửng dây, ghi đè biến chỉ đọc hoặc chia cho 0, nút Nạp sẽ bị khóa cưỡng bức để bảo vệ tuyệt đối tủ điện.</p>

          <div class="figure-container">
            <img src="{img('synaptix_deploy_gate.png')}" alt="Phân hệ Triển khai Nạp Deploy Gate">
            <div class="figure-caption">Hình 3.3: Phân hệ Triển khai Nạp An toàn Hai Pha (Atomic Deploy Gate)</div>
          </div>
        </section>
        """
    },
    {
        "id": "ch4",
        "num": 4,
        "title": "Chương 4: Thiết kế Sơ đồ Logic Khối hàm FBD Canvas",
        "sections": [
            ("4.1", "Thao tác Kéo-Thả, Định tuyến Dây & Nguyên tắc Kiểu Dữ liệu"),
            ("4.2", "Chuỗi Khóa Liên động Cốt lõi: INPUT &rarr; TRIGGER &rarr; COMPARATOR &rarr; GUARD &rarr; ACTION"),
            ("4.3", "Thực hành Dự án Mẫu 10 Phút: Mạch Khóa Quá nhiệt & Cửa An toàn Bồn Khuấy"),
        ],
        "content": f"""
        <section class="section-container" id="sec-4-1">
          <h2 class="section-title"><span class="sec-num">4.1</span> Thao tác Kéo-Thả, Định tuyến Dây &amp; Nguyên tắc Kiểu Dữ liệu</h2>
          <p>Thiết kế mạch logic trên SynaptiX IDE tuân thủ nghiêm ngặt 3 nguyên lý hình học công nghiệp:</p>
          <ul>
            <li><strong>Thao tác Chuột:</strong> Nhấp giữ chuột trái vào chân ra (màu cam/xanh) kéo dây sang chân vào. Nhấn giữ phím <code>Space</code> + Rê chuột để di chuyển vùng nhìn (Pan Canvas). Lăn con trỏ chuột để thu phóng (Zoom) từ 25% đến 200%.</li>
            <li><strong>Tự động Khóa Kiểu Dữ liệu (Type-Safe Pin Binding):**</strong> Chân tín hiệu số (<code>BOOL</code>) chỉ cho phép kết nối với chân <code>BOOL</code>; chân số nguyên (<code>INT32</code>) và số thực (<code>REAL</code>) không thể đấu nhầm vào cổng kích hoạt logic mà bắt buộc phải qua khối <code>COMPARATOR</code> hoặc <code>SCALE</code> để quy đổi.</li>
            <li><strong>Xóa &amp; Hiệu chỉnh:</strong> Chọn dây nối hoặc khối hàm và nhấn phím <code>Delete</code> để xóa. Bấm đúp vào thân khối để mở hộp thoại cấu hình chi tiết.</li>
          </ul>
        </section>

        <section class="section-container" id="sec-4-2">
          <h2 class="section-title"><span class="sec-num">4.2</span> Chuỗi Khóa Liên động Cốt lõi: INPUT &rarr; TRIGGER &rarr; COMPARATOR &rarr; GUARD &rarr; ACTION</h2>
          <p>Mọi thuật toán điều khiển trong SynaptiX đều được xây dựng dựa trên <strong>Chuỗi Mạch 5 Khâu Cốt lõi (Core Pipeline)</strong> đảm bảo tính mạch lạc và cô lập sự cố:</p>

          <div class="synx-node-chain">
            <div class="synx-canvas-node is-input">
              <div class="synx-node-header"><span>INPUT</span><span style="font-size: 5pt;">● NGUỒN</span></div>
              <div class="synx-node-body">
                <span class="synx-node-tag">AI0 &bull; REAL</span>
                <span class="synx-node-alias">TankTemp</span>
                <span class="synx-node-val">72.4 °C</span>
              </div>
            </div>

            <div class="synx-node-wire">
              <div class="synx-wire-line"></div>
              <span class="synx-wire-badge">Giá trị</span>
            </div>

            <div class="synx-canvas-node is-comparator">
              <div class="synx-node-header"><span>COMPARATOR</span><span style="font-size: 5pt;">● SO SÁNH</span></div>
              <div class="synx-node-body">
                <span class="synx-node-tag">&gt; 65.0 °C</span>
                <span class="synx-node-alias">Ngưỡng Nhiệt</span>
                <span class="synx-node-val">TRUE</span>
              </div>
            </div>

            <div class="synx-node-wire">
              <div class="synx-wire-line"></div>
              <span class="synx-wire-badge">BOOL</span>
            </div>

            <div class="synx-canvas-node is-guard">
              <div class="synx-node-header"><span>GUARD</span><span style="font-size: 5pt;">● KHÓA AN TOÀN</span></div>
              <div class="synx-node-body">
                <span class="synx-node-tag">DI0 == 1</span>
                <span class="synx-node-alias">Cửa Đóng Kín</span>
                <span class="synx-node-val">PASS</span>
              </div>
            </div>

            <div class="synx-node-wire">
              <div class="synx-wire-line"></div>
              <span class="synx-wire-badge">CHO PHÉP</span>
            </div>

            <div class="synx-canvas-node is-action">
              <div class="synx-node-header"><span>ACTION</span><span style="font-size: 5pt;">● THỰC THI</span></div>
              <div class="synx-node-body">
                <span class="synx-node-tag">DO0 = 1</span>
                <span class="synx-node-alias">Quạt Làm Mát</span>
                <span class="synx-node-val">ACTIVE</span>
              </div>
            </div>
          </div>
          <div class="figure-caption">Hình 4.1: Cấu trúc Chuỗi Mạch Khóa Liên động Tiêu chuẩn (Core Logic Pipeline)</div>
        </section>

        <section class="section-container" id="sec-4-3">
          <h2 class="section-title"><span class="sec-num">4.3</span> Thực hành Dự án Mẫu 10 Phút: Mạch Khóa Quá nhiệt &amp; Cửa An toàn Bồn Khuấy</h2>
          <p>Bài toán công nghiệp: Điều khiển đóng quạt làm mát <code>DO0</code> khi nhiệt độ bồn <code>AI0</code> vượt ngưỡng <code>65.0 °C</code> và cửa an toàn <code>DI0</code> đang đóng kín. Ngắt quạt khi nhiệt độ hạ xuống dưới <code>60.0 °C</code> (trễ nhiệt Hysteresis 5°C chống nhấp nhả rơ-le) hoặc khi có người mở cửa an toàn (<code>DI0 == 0</code>).</p>

          <ol>
            <li><strong>Tạo Dự án Mới:</strong> Bấm <code>Ctrl+N</code>, đặt tên dự án <code>Tank_Cooling_Safety</code>, chọn cấu hình phần cứng mục tiêu <code>SYNX-CTRL-884</code>.</li>
            <li><strong>Kéo Khối INPUT:</strong> Thả 1 khối <code>INPUT</code> vào Canvas, chọn biến <code>AI0</code>, đặt Alias <code>Tank_Temp</code>.</li>
            <li><strong>Kéo Khối COMPARATOR:</strong> Chọn toán tử <code>&gt; Greater Than</code>, gán giá trị ngưỡng <code>65.0</code>. Nối ngõ ra của <code>INPUT</code> vào chân <code>In</code> của <code>COMPARATOR</code>.</li>
            <li><strong>Kéo Khối GUARD:</strong> Gán biến khóa liên động <code>DI0</code> (Door Switch). Nối ngõ ra <code>COMPARATOR</code> vào chân <code>In</code> của <code>GUARD</code>.</li>
            <li><strong>Kéo Khối ACTION:</strong> Chọn hành động <code>Set 1</code> gán vào đích <code>DO0</code> (Cooling Fan). Nối ngõ ra <code>GUARD</code> vào chân <code>Exec</code> của <code>ACTION</code>.</li>
            <li><strong>Biên dịch &amp; Kiểm thử:</strong> Nhấn <code>F7</code> để biên dịch, sau đó nhấn <code>F5</code> mở bộ mô phỏng để gạt thử công tắc <code>DI0</code> và kéo thanh trượt <code>AI0</code> kiểm tra phản ứng!</li>
          </ol>
        </section>
        """
    },
    {
        "id": "ch5",
        "num": 5,
        "title": "Chương 5: Thư viện Khối Hàm Thông dụng (Function Blocks)",
        "sections": [
            ("5.1", "Các Khối Định thời: TON, TOF, TP & CYCLE_TIMER"),
            ("5.2", "Các Khối Bộ đếm Sản lượng: CTU & CTD"),
            ("5.3", "Khối Tuyến tính hóa & Hiệu chuẩn Cảm biến: SCALE"),
            ("5.4", "Cụm Khối Tính toán Số học: MATH (ALU Unit)"),
        ],
        "content": f"""
        <section class="section-container" id="sec-5-1">
          <h2 class="section-title"><span class="sec-num">5.1</span> Các Khối Định thời: TON, TOF, TP &amp; CYCLE_TIMER</h2>
          <p>Bộ định thời trong SynaptiX hoạt động dựa trên bộ đếm thời gian thực (Monotonic Clock) với độ phân giải mili-giây, không bị ảnh hưởng bởi tải xử lý của hệ thống:</p>

          <div class="table-caption">Bảng 5.1: Đặc tả Thông số Kỹ thuật Các Khối Định thời Chuẩn IEC 61131-3</div>
          <table class="spec-table-2col">
            <thead><tr><th style="width: 25%;">Khối Hàm</th><th>Nguyên lý Hoạt động &amp; Cổng Chân I/O</th></tr></thead>
            <tbody>
              <tr>
                <td class="col-pin"><strong>TON</strong><br/>(On-Delay Timer)</td>
                <td class="col-desc">
                  <strong>Trễ Đóng:</strong> Khi ngõ vào <code>IN = 1</code>, bộ đếm <code>ET</code> bắt đầu tăng dần từ 0. Khi <code>ET &gt;= PT</code> (thời gian đặt), ngõ ra <code>Q</code> chuyển lên mức <code>1</code>. Nếu <code>IN = 0</code> bất kỳ lúc nào, <code>ET</code> lập tức reset về 0 và <code>Q = 0</code>.<br/>
                  <em>Ứng dụng:</em> Trễ khởi động bơm chống sụt áp lưới điện, lọc nhiễu phao nước chập chờn.
                </td>
              </tr>
              <tr>
                <td class="col-pin"><strong>TOF</strong><br/>(Off-Delay Timer)</td>
                <td class="col-desc">
                  <strong>Trễ Ngắt:</strong> Khi <code>IN = 1</code>, ngõ ra <code>Q = 1</code> ngay lập tức. Khi <code>IN</code> chuyển về <code>0</code>, <code>Q</code> vẫn tiếp tục duy trì mức <code>1</code> suốt khoảng thời gian <code>PT</code> trước khi ngắt hẳn về <code>0</code>.<br/>
                  <em>Ứng dụng:</em> Quạt hút khí tiếp tục chạy thêm 60 giây sau khi tắt lò nhiệt để xả hết khí độc.
                </td>
              </tr>
              <tr>
                <td class="col-pin"><strong>TP</strong><br/>(Pulse Timer)</td>
                <td class="col-desc">
                  <strong>Phát Xung Đơn Ổn:</strong> Khi phát hiện sườn lên của <code>IN</code>, ngõ ra <code>Q</code> lập tức bật lên mức <code>1</code> và duy trì đúng khoảng thời gian <code>PT</code> cố định, hoàn toàn không phụ thuộc vào việc tín hiệu <code>IN</code> giữ lâu hay ngắt sớm.<br/>
                  <em>Ứng dụng:</em> Xung kích van xả khí nén bắn bụi túi lọc, kích súng phun sơn tự động.
                </td>
              </tr>
              <tr>
                <td class="col-pin"><strong>CYCLE_TIMER</strong><br/>(Asymmetric Osc)</td>
                <td class="col-desc">
                  <strong>Dao Động Bất Đối Xứng:</strong> Tự động nhấp nháy ngõ ra <code>Q</code> lặp đi lặp lại với thời gian sáng (<code>T_ON</code>) và thời gian tắt (<code>T_OFF</code>) độc lập nhau mà không cần đấu vòng lặp 2 timer phức tạp.<br/>
                  <em>Ứng dụng:</em> Nhấp nháy đèn còi cảnh báo sự cố (bật 500ms, tắt 1500ms), sục khí bể cá cảnh.
                </td>
              </tr>
            </tbody>
          </table>
        </section>

        <section class="section-container" id="sec-5-2">
          <h2 class="section-title"><span class="sec-num">5.2</span> Các Khối Bộ đếm Sản lượng: CTU &amp; CTD</h2>
          <p>Các khối đếm số lượng xung chuyển trạng thái từ mức <code>0 &rarr; 1</code> tại chân ngõ vào <code>CU</code> hoặc <code>CD</code>:</p>
          <ul>
            <li><strong>CTU (Count Up):</strong> Tăng giá trị đếm hiện tại <code>CV</code> lên 1 mỗi khi có sườn lên ở chân <code>CU</code>. Khi <code>CV &gt;= PV</code> (ngưỡng đặt sản lượng), ngõ ra <code>Q = 1</code>. Chân <code>Reset = 1</code> đưa <code>CV</code> về 0.</li>
            <li><strong>CTD (Count Down):</strong> Giảm giá trị <code>CV</code> đi 1 khi có sườn lên ở chân <code>CD</code>. Khi <code>CV &lt;= 0</code>, ngõ ra <code>Q = 1</code>. Chân <code>Load = 1</code> nạp lại giá trị <code>PV</code> ban đầu.</li>
          </ul>
        </section>

        <section class="section-container" id="sec-5-3">
          <h2 class="section-title"><span class="sec-num">5.3</span> Khối Tuyến tính hóa &amp; Hiệu chuẩn Cảm biến: SCALE</h2>
          <p>Khối <strong>SCALE</strong> thực hiện chuyển đổi tuyến tính 2 điểm giữa giá trị đo thô từ cảm biến (Raw ADC hoặc mA/V) sang giá trị vật lý thực tế theo công thức toán học:</p>
          <div style="text-align: center; margin: 2mm 0; font-weight: bold; color: #004f71;">
            OUT = OutMin + [ (IN - InMin) / (InMax - InMin) ] &times; (OutMax - OutMin)
          </div>
          <p>Khối tích hợp sẵn cơ chế kẹp biên an toàn (Clamp Protection): nếu giá trị cảm biến vượt ngoài khoảng <code>[InMin, InMax]</code> do đứt dây hoặc chập mạch, ngõ ra <code>OUT</code> sẽ được khóa chặt ở cận an toàn <code>OutMin</code> hoặc <code>OutMax</code> để không làm tràn số hệ thống.</p>
        </section>

        <section class="section-container" id="sec-5-4">
          <h2 class="section-title"><span class="sec-num">5.4</span> Cụm Khối Tính toán Số học: MATH (ALU Unit)</h2>
          <p>Cụm khối <strong>MATH</strong> đảm nhiệm các phép tính số học tổ hợp trên 2 toán hạng ngõ vào <code>IN1</code> và <code>IN2</code> cho cả hai kiểu dữ liệu số nguyên bù 2 (<code>INT32</code>) và số thực dấu chấm động chuẩn IEEE 754 (<code>REAL</code>):</p>

          <div class="table-caption">Bảng 5.2: Bảng Toán tử &amp; Mã Lệnh ALU Khối MATH</div>
          <table>
            <thead>
              <tr>
                <th style="width: 15%;">Toán tử</th>
                <th style="width: 25%;">Công thức Tính toán</th>
                <th style="width: 30%;">Bảo vệ Tràn số &amp; Chia cho 0</th>
                <th style="width: 30%;">Ví dụ Ứng dụng Thực tế</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><code>ADD</code></td>
                <td><code>OUT = IN1 + IN2</code></td>
                <td>Kẹp biên bão hòa INT_MAX / REAL_MAX</td>
                <td>Tổng hợp lưu lượng nước từ 2 đường ống cấp vào bồn.</td>
              </tr>
              <tr>
                <td><code>SUB</code></td>
                <td><code>OUT = IN1 - IN2</code></td>
                <td>Kẹp biên bão hòa INT_MIN / REAL_MIN</td>
                <td>Trừ trọng lượng bao bì (Tare Deduction) trên cân băng tải.</td>
              </tr>
              <tr>
                <td><code>MUL</code></td>
                <td><code>OUT = IN1 * IN2</code></td>
                <td>Tự động nâng độ chính xác 64-bit nội bộ</td>
                <td>Tính công suất tiêu thụ tức thời (<code>P = U * I</code>).</td>
              </tr>
              <tr>
                <td><code>DIV</code></td>
                <td><code>OUT = IN1 / IN2</code></td>
                <td>Bật cờ <code>0xE001</code> (Zero-Div), giữ nguyên OUT</td>
                <td>Tính tốc độ trung bình chuyền may (Sản lượng / Thời gian).</td>
              </tr>
              <tr>
                <td><code>MOD</code></td>
                <td><code>OUT = IN1 % IN2</code></td>
                <td>Chia lấy phần dư nguyên (chỉ áp dụng INT32)</td>
                <td>Chia nhóm sản phẩm đóng thùng (12 lon/thùng).</td>
              </tr>
              <tr>
                <td><code>ABS</code></td>
                <td><code>OUT = |IN1|</code></td>
                <td>Lấy giá trị tuyệt đối không âm</td>
                <td>Tính độ lệch áp suất tuyệt đối giữa 2 buồng phòng sạch.</td>
              </tr>
            </tbody>
          </table>

          <div class="figure-container">
            <img src="{img('synaptix_math_config_dialog.png')}" alt="Hộp thoại Cấu hình Khối MATH">
            <div class="figure-caption">Hình 5.1: Giao diện Hộp thoại Cấu hình Khối MATH (Chọn Toán tử, Kiểu dữ liệu &amp; Cổng chân)</div>
          </div>

          <h3 class="subsection-title">Ví dụ Thực tế 1: Tổng Hợp Lưu Lượng Nước Cấp Lò Hơi (Toán tử ADD)</h3>
          <p>Thu thập lưu lượng tức thời từ hai nhánh cấp nước vào bồn lò hơi qua hai đồng hồ đo chuẩn analog: Nhánh 1 đấu vào <code>AI0</code> (FlowLine1, m3/h) và Nhánh 2 đấu vào <code>AI1</code> (FlowLine2, m3/h). Khối MATH thực hiện phép cộng tổng <code>TotalFlow = AI0 + AI1</code> và xuất kết quả ra thanh ghi <code>VREG0</code>. Nếu tổng lưu lượng vượt ngưỡng <code>120.0 m3/h</code>, hệ thống tự động kích hoạt van xả an toàn <code>DO0</code>.</p>

          <div class="synx-node-chain">
            <div class="synx-canvas-node is-input">
              <div class="synx-node-header"><span>INPUT</span><span style="font-size: 5pt;">● NHÁNH 1</span></div>
              <div class="synx-node-body">
                <span class="synx-node-tag">AI0 &bull; REAL</span>
                <span class="synx-node-alias">FlowLine1</span>
                <span class="synx-node-val">58.4 m3/h</span>
              </div>
            </div>
            <div class="synx-node-wire"><div class="synx-wire-line"></div><span class="synx-wire-badge">IN1</span></div>
            <div class="synx-canvas-node is-math">
              <div class="synx-node-header"><span>MATH (ADD)</span><span style="font-size: 5pt;">● CỘNG</span></div>
              <div class="synx-node-body">
                <span class="synx-node-tag">IN1 + IN2</span>
                <span class="synx-node-alias">Totalizer</span>
                <span class="synx-node-val">122.9 m3/h</span>
              </div>
            </div>
            <div class="synx-node-wire"><div class="synx-wire-line"></div><span class="synx-wire-badge">OUT</span></div>
            <div class="synx-canvas-node is-comparator">
              <div class="synx-node-header"><span>COMPARATOR</span><span style="font-size: 5pt;">● GIỚI HẠN</span></div>
              <div class="synx-node-body">
                <span class="synx-node-tag">&gt; 120.0</span>
                <span class="synx-node-alias">High Limit</span>
                <span class="synx-node-val">TRUE</span>
              </div>
            </div>
            <div class="synx-node-wire"><div class="synx-wire-line"></div><span class="synx-wire-badge">EXEC</span></div>
            <div class="synx-canvas-node is-action">
              <div class="synx-node-header"><span>ACTION</span><span style="font-size: 5pt;">● VAN XẢ</span></div>
              <div class="synx-node-body">
                <span class="synx-node-tag">DO0 = 1</span>
                <span class="synx-node-alias">Relief Valve</span>
                <span class="synx-node-val">OPEN</span>
              </div>
            </div>
          </div>
          <div class="figure-caption">Hình 5.2: Sơ đồ Mạch Tổng Hợp Lưu Lượng Nước &amp; Cảnh Báo Ngưỡng (Khối MATH ADD)</div>

          <h3 class="subsection-title">Ví dụ Thực tế 2: Trừ Trọng Lượng Bao Bì &amp; Ngắt Đóng Bao Silo (Toán tử SUB)</h3>
          <p>Trên cân đóng bao bột mịn, loadcell cân toàn phần trả về trọng lượng tổng bao gồm vỏ bao và sản phẩm tại <code>VREG0</code> (GrossWeight, kg). Trọng lượng bao bì được lưu tĩnh tại <code>VREG1</code> (TareWeight = 1.25 kg). Khối MATH thực hiện phép trừ <code>NetWeight = GrossWeight - TareWeight</code> xuất ra <code>VREG2</code> kèm cơ chế chặn cận dưới (Lower clamp). Khi trọng lượng tịnh đạt đủ <code>50.0 kg</code>, khối ACTION lập tức ngắt cửa xả liệu Silo <code>DO1</code>.</p>
        </section>
        """
    },
    {
        "id": "ch6",
        "num": 6,
        "title": "Chương 6: Mô phỏng Offline, Nạp An toàn & Giám sát Live Watch",
        "sections": [
            ("6.1", "Bộ Mô phỏng Ngoại tuyến (F5) & Nhích Từng Bước Quét (StepScan)"),
            ("6.2", "Khóa An toàn Biên dịch (Pre-flight Gate) & Cơ chế Nạp Nguyên tử"),
            ("6.3", "Gỡ lỗi Trực tuyến trên Sơ đồ (F8) & Nhận diện Cảnh báo [STALE]"),
            ("6.4", "Giám sát &amp; Cưỡng bức Biến An toàn trên Live Watch (Ctrl+7)"),
        ],
        "content": f"""
        <section class="section-container" id="sec-6-1">
          <h2 class="section-title"><span class="sec-num">6.1</span> Bộ Mô phỏng Ngoại tuyến (F5) &amp; Nhích Từng Bước Quét (StepScan)</h2>
          <p>Nhấn phím <code>F5</code> trên bàn phím để kích hoạt cửa sổ <strong>Offline Simulator</strong>. Bộ mô phỏng chạy cùng một nhân thực thi tất định (Deterministic Runtime Engine) kết hợp đồng hồ ảo <code>SimulatedClock</code>, cho phép kiểm thử 100% logic mà <strong>không cần kết nối bất kỳ thiết bị phần cứng nào</strong>:</p>

          <div class="figure-container">
            <img src="{img('synaptix_simulator.png')}" alt="Bộ Mô phỏng Ngoại tuyến Chu kỳ Chính xác F5">
            <div class="figure-caption">Hình 6.1: Giao diện Bộ Mô phỏng Ngoại tuyến F5 (Bơm Tín hiệu Ảo, Đồng hồ Mô phỏng &amp; Vết Luật)</div>
          </div>

          <ul>
            <li><strong>Chạy Liên tục / Tạm dừng (ToggleEngine):</strong> Chạy chu kỳ quét ảo theo thời gian thực hoặc đóng băng hệ thống để kiểm tra biến.</li>
            <li><strong>Nhích Từng Bước Quét (StepScan):</strong> Mỗi lần nhấn nút, đồng hồ ảo chỉ tiến đúng <strong>1 chu kỳ quét duy nhất</strong> (Scan #N &rarr; Scan #N+1). Tính năng này là vũ khí tối thượng để bắt các lỗi xung cạnh lên (Rising Edge) diễn ra quá nhanh!</li>
            <li><strong>Bơm Tín hiệu Ngõ vào:</strong> Gạt công tắc ảo <code>[ 0 ] / [ 1 ]</code> cho các ngõ vào số <code>DI0..DI7</code> hoặc kéo thanh trượt dòng/áp cho <code>AI0..AI3</code>.</li>
            <li><strong>Tua Nhanh Thời gian (SimSpeed):</strong> Tăng tốc thời gian lên <code>5x</code>, <code>10x</code> để kiểm chứng các bộ hẹn giờ dài nhiều giờ chỉ trong vài giây.</li>
          </ul>
        </section>

        <section class="section-container" id="sec-6-2">
          <h2 class="section-title"><span class="sec-num">6.2</span> Khóa An toàn Biên dịch (Pre-flight Gate) &amp; Cơ chế Nạp Nguyên tử</h2>
          <p>Để ngăn chặn rủi ro làm gián đoạn dây chuyền, SynaptiX IDE triển khai quy trình nạp 2 giai đoạn (Two-Phase Commit):</p>
          <ol>
            <li><strong>Giai đoạn 1 (Validation &amp; Diff):</strong> Nhấn <code>Ctrl+F5</code> (hoặc mở <code>Ctrl+6</code>). IDE biên dịch kiểm tra toàn bộ đồ thị. Màn hình hiển thị bảng so sánh sai khác (Diff Viewer) chỉ rõ những khối và thông số sắp sửa thay đổi so với chương trình đang chạy trong thiết bị.</li>
            <li><strong>Giai đoạn 2 (Atomic Flash Commit):</strong> Toàn bộ bytecode mới được truyền xuống vùng đệm Flash thứ hai. Sau khi kiểm tra mã kiểm tra toàn vẹn CRC-32 thành công, vi điều khiển mới tráo con trỏ thực thi sang chương trình mới trong thời gian chưa đầy <strong>2 mili-giây</strong>. Nếu xảy ra mất điện hoặc tuột cáp giữa chừng, thiết bị tự động hoàn nguyên về chương trình cũ an toàn tuyệt đối.</li>
          </ol>
        </section>

        <section class="section-container" id="sec-6-3">
          <h2 class="section-title"><span class="sec-num">6.3</span> Gỡ lỗi Trực tuyến trên Sơ đồ (F8) &amp; Nhận diện Cảnh báo [STALE]</h2>
          <p>Sau khi nạp chương trình xuống thiết bị đang kết nối qua USB hoặc Ethernet (<code>F4</code>), mở Sơ đồ Logic (<code>Ctrl+2</code>) và nhấn phím <code>F8</code> để bật chế độ <strong>Online Live Debug</strong>:</p>
          <ul>
            <li>🟢 <strong>Dây dẫn sáng xanh:</strong> Đang mang tín hiệu logic mức <code>1</code> (TRUE).</li>
            <li>⚫ <strong>Dây dẫn xám mờ:</strong> Tín hiệu đang ở mức <code>0</code> (FALSE).</li>
            <li>🔢 <strong>Huy hiệu số tức thời:</strong> Hiển thị giá trị thực tế của các biến đo lường và tiến độ đếm thời gian <code>ET/PT</code> ở tốc độ 20 khung hình/giây (20 FPS).</li>
            <li>⚠️ <strong>Huy hiệu [STALE]:</strong> Nếu một cảm biến RS-485 bị đứt dây hoặc mất nguồn quá thời gian chờ, khối <code>MB_READ</code> tương ứng sẽ lập tức bật nhãn <strong>[STALE]</strong> giúp kỹ sư khoanh vùng vị trí đứt cáp ngay lập tức.</li>
          </ul>
        </section>

        <section class="section-container" id="sec-6-4">
          <h2 class="section-title"><span class="sec-num">6.4</span> Giám sát &amp; Cưỡng bức Biến An toàn trên Live Watch (Ctrl+7)</h2>
          <p>Phân hệ <strong>Live Watch (Ctrl+7)</strong> cung cấp bảng dữ liệu giám sát mật độ cao theo chuẩn IBM Carbon và đồ thị xu hướng thời gian thực (Trend Chart). Tại đây, kỹ sư có thể cưỡng bức (Force) giá trị ngõ ra hoặc biến cờ để thử nghiệm máy:</p>

          <div class="figure-container">
            <img src="{img('synaptix_live_watch.png')}" alt="Phân hệ Giám sát Trực tuyến Live Watch">
            <div class="figure-caption">Hình 6.2: Bảng Dữ liệu &amp; Đồ thị Biến thiên Thời gian thực Live Watch (Ctrl+7)</div>
          </div>

          <div class="callout callout-warning">
            <div class="callout-header">⚠️ KHÓA BẢO VỆ THỜI GIAN CƯỠNG BỨC (FORCE LEASE)</div>
            Mọi thao tác cưỡng bức giá trị trên Live Watch đều bắt buộc phải thiết lập thời gian hết hạn (Lease Time, ví dụ: 30 giây hoặc 5 phút). Khi hết thời gian bảo vệ, hệ thống sẽ tự động hủy lệnh cưỡng bức và trả biến về quyền điều khiển tự động của logic, ngăn ngừa việc kỹ sư quên nhả cưỡng bức gây sự cố dây chuyền.
          </div>
        </section>
        """
    },
    {
        "id": "ch7",
        "num": 7,
        "title": "Chương 7: Truyền thông Công nghiệp & Điện toán Biên IIoT",
        "sections": [
            ("7.1", "Giao thức Modbus RTU / TCP Master & Slave"),
            ("7.2", "Đẩy Telemetry & Nhận Lệnh Điều khiển Đám mây: MQTT & HTTP"),
            ("7.3", "Bảo mật Chứng chỉ X.509 MQTT TLS & Mạng Wi-Fi"),
            ("7.4", "Không gian DTU Gateway Studio & Mạng Không dây Zigbee Mesh"),
        ],
        "content": f"""
        <section class="section-container" id="sec-7-1">
          <h2 class="section-title"><span class="sec-num">7.1</span> Giao thức Modbus RTU / TCP Master &amp; Slave</h2>
          <p>SynaptiX Controller trang bị 2 cổng RS-485 độc lập cách ly quang chống sét lan truyền, hỗ trợ cấu hình linh hoạt vừa làm <strong>Master</strong> thu thập cảm biến hiện trường, vừa làm <strong>Slave</strong> phục vụ hệ thống SCADA/HMI nhà máy:</p>
          <ul>
            <li><strong>Khối MB_READ (Master Read):</strong> Quét định kỳ thanh ghi từ các thiết bị ngoại vi (đồng hồ đo điện năng Schneider/Acrochip, cảm biến nhiệt độ/độ ẩm RS-485, biến tần INVT/Yaskawa) theo mã hàm <code>FC01, FC02, FC03, FC04</code>. Hỗ trợ đọc gộp khối liên tục để tối ưu băng thông đường truyền.</li>
            <li><strong>Khối MB_WRITE (Master Write):</strong> Ghi lệnh điều khiển (Run/Stop, cài đặt tần số Hz, đặt ngưỡng cảnh báo) xuống thiết bị chấp hành theo mã hàm <code>FC05, FC06, FC16</code>.</li>
            <li><strong>Bản đồ SCADA Slave Cố định:</strong> Khi hoạt động ở chế độ Slave (qua cổng RS-485 thứ 2 hoặc cổng Ethernet Modbus TCP Port 502), thiết bị cung cấp bản đồ thanh ghi cố định (xem chi tiết tại Phụ lục B).</li>
          </ul>
        </section>

        <section class="section-container" id="sec-7-2">
          <h2 class="section-title"><span class="sec-num">7.2</span> Đẩy Telemetry &amp; Nhận Lệnh Điều khiển Đám mây: MQTT &amp; HTTP</h2>
          <p>Khối <strong>MQTT_PUB</strong> tự động thu thập tối đa 8 biến Tag từ bộ nhớ, đóng gói thành bản tin định dạng chuẩn JSON và xuất bản (Publish) lên máy chủ MQTT Broker định kỳ hoặc khi có biến đổi đột ngột:</p>
          <pre><code>// Bản tin JSON Telemetry xuất bản bởi khối MQTT_PUB lên Topic: factory/cell1/telemetry
{{
  "device_id": "SYNX-CTRL-884-01",
  "timestamp": 1731998400,
  "temperature": 72.4,
  "pressure": 4.82,
  "fan_status": 1,
  "error_code": 0
}}</code></pre>
          <p>Ngược lại, khối <strong>MQTT_SUB</strong> đăng ký (Subscribe) lắng nghe lệnh điều khiển từ đám mây. Khối được tích hợp cơ chế <strong>Nguyên tử Toàn vẹn (Atomic All-or-Nothing)</strong>: nếu bản tin JSON gửi xuống bị lỗi cú pháp hoặc thiếu trường dữ liệu bắt buộc, khối sẽ hủy bỏ toàn bộ gói tin và phát xung cảnh báo <code>Err</code> thay vì ghi nhận dữ liệu chắp vá gây nguy hiểm cho máy móc.</p>
        </section>

        <section class="section-container" id="sec-7-3">
          <h2 class="section-title"><span class="sec-num">7.3</span> Bảo mật Chứng chỉ X.509 MQTT TLS &amp; Mạng Wi-Fi</h2>
          <p>Để ngăn chặn các cuộc tấn công nghe lén hoặc chèn lệnh giả mạo trên đường truyền Internet, phân hệ truyền thông hỗ trợ mã hóa <strong>TLS 1.3 với xác thực hai chiều qua chứng chỉ số X.509</strong>:</p>
          <ul>
            <li>Hỗ trợ nạp tệp chứng chỉ gốc Root CA (<code>.pem / .crt</code>) trực tiếp vào vùng nhớ bảo mật của phần cứng.</li>
            <li>Tự động xác thực tính hợp lệ của tên miền máy chủ Broker (SAN / CN Check) và kiểm tra hạn dùng chứng chỉ dựa trên đồng hồ thời gian thực RTC.</li>
            <li>Module Wi-Fi công nghiệp hỗ trợ chuẩn mã hóa bảo mật WPA2-Personal và WPA3-Enterprise, tự động kết nối lại khi mất sóng và có cơ chế giám sát chất lượng tín hiệu RSSI.</li>
          </ul>
        </section>

        <section class="section-container" id="sec-7-4">
          <h2 class="section-title"><span class="sec-num">7.4</span> Không gian DTU Gateway Studio &amp; Mạng Không dây Zigbee Mesh</h2>
          <p>Đối với các nhà xưởng quy mô lớn có hàng trăm cảm biến phân tán, thiết bị <strong>XLink Gateway</strong> đóng vai trò làm điều phối viên (Coordinator) mạng <strong>Zigbee 3.0 Mesh</strong>:</p>
          <ul>
            <li>Các nút cảm biến không dây tự động kết nối và tìm đường truyền tối ưu về Gateway qua các nút trung chuyển (Routers).</li>
            <li>Trình cấu hình <strong>DTU Gateway Studio</strong> trong IDE hiển thị sơ đồ cây tô-pô mạng trực quan, mức sóng LQI giữa các nút và tình trạng pin nuôi của từng cảm biến.</li>
          </ul>
        </section>
        """
    },
    {
        "id": "ch8",
        "num": 8,
        "title": "Chương 8: Kịch bản Ứng dụng Công nghiệp Thực tế (Application Notes)",
        "sections": [
            ("8.1", "Kịch bản AN-001: Trạm Bơm Nước Luân phiên Tự động theo Lịch & Bảo vệ Quá nhiệt"),
            ("8.2", "Kịch bản AN-002: Trạm Giám sát Điện năng & Môi trường Đẩy Dữ liệu Cloud"),
        ],
        "content": f"""
        <section class="section-container" id="sec-8-1">
          <h2 class="section-title"><span class="sec-num">8.1</span> Kịch bản AN-001: Trạm Bơm Nước Luân phiên Tự động theo Lịch &amp; Bảo vệ Quá nhiệt</h2>
          <p><strong>Mục tiêu:</strong> Điều khiển hệ thống cấp nước gồm 2 bơm nước công nghiệp (Bơm 1 - <code>DO0</code>, Bơm 2 - <code>DO1</code>) chạy luân phiên đổi ca mỗi 6 giờ để cân bằng hao mòn cơ khí. Hệ thống giám sát nhiệt độ cuộn dây động cơ qua cảm biến <code>AI0</code> (Bơm 1) và <code>AI1</code> (Bơm 2):</p>
          <ul>
            <li><strong>Luật Khởi động:</strong> Phao báo cạn bể hút <code>DI0 == 1</code> (có nước) và phao báo đầy bồn chứa <code>DI1 == 0</code> (cần nước) kích hoạt chu kỳ bơm. Khối <code>CYCLE_TIMER</code> luân phiên đóng rơ-le Bơm 1 và Bơm 2 mỗi 21.600 giây (6 giờ).</li>
            <li><strong>Luật Bảo vệ Khẩn cấp:</strong> Nếu nhiệt độ cuộn dây của bơm đang chạy vượt quá <code>85.0 °C</code>, khối <code>COMPARATOR</code> lập tức kích hoạt cổng <code>GUARD</code> ngắt bơm đó ngay lập tức, chuyển cờ báo sự cố <code>VFLAG10 = 1</code> và tự động gọi bơm dự phòng vào thay thế để không làm gián đoạn việc cấp nước nhà máy!</li>
          </ul>
        </section>

        <section class="section-container" id="sec-8-2">
          <h2 class="section-title"><span class="sec-num">8.2</span> Kịch bản AN-002: Trạm Giám sát Điện năng &amp; Môi trường Đẩy Dữ liệu Cloud</h2>
          <p><strong>Mục tiêu:</strong> Thu thập thông số từ trạm biến áp qua thiết bị <code>miniLog MLV2</code> gồm: Điện áp 3 pha, Dòng tải, Công suất tổng kWh (đọc qua Modbus RS-485 từ đồng hồ điện lực) và Nhiệt độ phòng trạm (đọc qua chân <code>AI0</code> 4-20mA):</p>
          <ul>
            <li>Khối <code>MB_READ</code> quét các thanh ghi đo đếm mỗi 2 giây qua cổng RS-485 #1.</li>
            <li>Khối <code>SCALE</code> quy đổi tín hiệu 4-20mA từ <code>AI0</code> sang dải nhiệt độ <code>-20.0 .. +80.0 °C</code>.</li>
            <li>Khối <code>MQTT_PUB</code> gom toàn bộ các thông số trên vào bản tin JSON và đẩy lên máy chủ đám mây <code>synaptix-io.com</code> định kỳ mỗi 10 giây qua kênh truyền 4G LTE bảo mật TLS X.509.</li>
          </ul>
        </section>
        """
    },
    {
        "id": "app_a",
        "num": "A",
        "title": "Phụ lục A: Bảng Phím tắt Thao tác Nhanh trên Canvas",
        "sections": [
            ("A.1", "Danh mục Phím Nóng & Cử chỉ Chuột Kỹ thuật"),
        ],
        "content": f"""
        <section class="section-container" id="sec-a-1">
          <h2 class="section-title"><span class="sec-num">A.1</span> Danh mục Phím Nóng &amp; Cử chỉ Chuột Kỹ thuật</h2>
          <div class="table-caption">Bảng A.1: Phím Tắt Thao Tác Hệ Thống &amp; Thiết Kế Bản Vẽ FBD</div>
          <table>
            <thead>
              <tr>
                <th style="width: 25%;">Phím tắt / Thao tác</th>
                <th style="width: 35%;">Chức năng Thực thi</th>
                <th style="width: 40%;">Mẹo Sử dụng Nhanh</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><code>F4</code></td>
                <td>Mở hộp thoại Kết nối Thiết bị (USB CDC / Modbus TCP)</td>
                <td>Quét nhanh các cổng COM hoặc IP đang trực tuyến.</td>
              </tr>
              <tr>
                <td><code>F5</code></td>
                <td>Mở Bộ Mô phỏng Ngoại tuyến (Offline Simulator)</td>
                <td>Kiểm thử chu trình quét và bơm tín hiệu ảo không cần phần cứng.</td>
              </tr>
              <tr>
                <td><code>F7</code></td>
                <td>Biên dịch Dự án &amp; Kiểm tra Lỗi Cú pháp Logic</td>
                <td>Quét toàn bộ dây nối và kiểm tra quy tắc an toàn.</td>
              </tr>
              <tr>
                <td><code>F8</code></td>
                <td>Bật / Tắt Gỡ lỗi Trực tuyến trên Sơ đồ (Live Debug)</td>
                <td>Sáng xanh dây dẫn mang điện và hiển thị số tức thời trên canvas.</td>
              </tr>
              <tr>
                <td><code>Ctrl + F5</code></td>
                <td>Biên dịch &amp; Nạp Nhanh xuống Thiết bị (Fast Deploy)</td>
                <td>Mở cổng kiểm soát nạp an toàn hai pha (Atomic Commit).</td>
              </tr>
              <tr>
                <td><code>Ctrl + 1 .. 7</code></td>
                <td>Chuyển đổi tức thì giữa 7 Không gian Làm việc</td>
                <td><code>Ctrl+2</code> mở sơ đồ logic; <code>Ctrl+7</code> mở bảng giám sát.</td>
              </tr>
              <tr>
                <td><code>Space + Kéo Chuột</code></td>
                <td>Di chuyển vùng nhìn Canvas (Pan Viewport)</td>
                <td>Điều hướng nhanh trên các bản vẽ sơ đồ khối quy mô lớn.</td>
              </tr>
              <tr>
                <td><code>Lăn Con trỏ Chuột</code></td>
                <td>Thu phóng Bản vẽ (Zoom In / Zoom Out)</td>
                <td>Phóng to soi chi tiết chân cắm; thu nhỏ xem toàn cảnh mạch.</td>
              </tr>
              <tr>
                <td><code>Delete</code></td>
                <td>Xóa Khối hàm hoặc Dây nối đang chọn</td>
                <td>Có thể bấm <code>Ctrl+Z</code> để hoàn tác ngay lập tức.</td>
              </tr>
              <tr>
                <td><code>Ctrl + Space</code></td>
                <td>Kích hoạt Trợ lý Kỹ thuật SynaptiX AI Copilot</td>
                <td>Gợi ý hoàn thiện sơ đồ mạch hoặc hỏi đáp tài liệu kỹ thuật.</td>
              </tr>
            </tbody>
          </table>
        </section>
        """
    },
    {
        "id": "app_b",
        "num": "B",
        "title": "Phụ lục B: Bản đồ Thanh ghi SCADA Slave Cố định",
        "sections": [
            ("B.1", "Bản đồ Địa chỉ Modbus Holding Registers (0-based & 1-based)"),
        ],
        "content": f"""
        <section class="section-container" id="sec-b-1">
          <h2 class="section-title"><span class="sec-num">B.1</span> Bản đồ Địa chỉ Modbus Holding Registers (0-based &amp; 1-based)</h2>
          <p>Khi thiết bị SynaptiX hoạt động ở chế độ Modbus Slave phục vụ hệ thống SCADA/HMI qua cổng RS-485 hoặc cổng Ethernet Modbus TCP (Port 502), không gian bộ nhớ được ánh xạ cố định theo bảng chuẩn sau:</p>

          <div class="table-caption">Bảng B.1: Bản đồ Ánh xạ Thanh ghi Modbus Holding Registers (FC03, FC04, FC06, FC16)</div>
          <table>
            <thead>
              <tr>
                <th style="width: 18%;">Địa chỉ (0-based)</th>
                <th style="width: 18%;">Địa chỉ SCADA (1-based)</th>
                <th style="width: 18%;">Phân vùng Tag</th>
                <th style="width: 16%;">Quyền Truy cập</th>
                <th style="width: 30%;">Quy cách Byte &amp; Định dạng</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><code>0x0000 .. 0x0007</code></td>
                <td><code>40001 .. 40008</code></td>
                <td><code>DI0 .. DI7</code></td>
                <td>Chỉ đọc (RO)</td>
                <td>Bit packed (0 = Ngắt, 1 = Đóng)</td>
              </tr>
              <tr>
                <td><code>0x0010 .. 0x0017</code></td>
                <td><code>40017 .. 40024</code></td>
                <td><code>DO0 .. DO7</code></td>
                <td>Đọc / Ghi (RW)</td>
                <td>Bit packed (0 = Off, 1 = On)</td>
              </tr>
              <tr>
                <td><code>0x0064 .. 0x006B</code></td>
                <td><code>40101 .. 40108</code></td>
                <td><code>AI0 .. AI3</code></td>
                <td>Chỉ đọc (RO)</td>
                <td>2 thanh ghi 16-bit / kênh (IEEE 754 Float32, Big-Endian)</td>
              </tr>
              <tr>
                <td><code>0x00C8 .. 0x00DF</code></td>
                <td><code>40201 .. 40224</code></td>
                <td><code>VFLAG0 .. 31</code></td>
                <td>Đọc / Ghi (RW)</td>
                <td>Cờ nhớ nhị phân điều khiển từ xa</td>
              </tr>
              <tr>
                <td><code>0x012C .. 0x016B</code></td>
                <td><code>40301 .. 40364</code></td>
                <td><code>VREG0 .. 31</code></td>
                <td>Đọc / Ghi (RW)</td>
                <td>Thanh ghi số thực Float32 hoặc số nguyên INT32</td>
              </tr>
              <tr>
                <td><code>0x0190 .. 0x01CF</code></td>
                <td><code>40401 .. 40464</code></td>
                <td><code>VREG_RETAIN</code></td>
                <td>Đọc / Ghi (RW)</td>
                <td>Thanh ghi lưu Flash vĩnh viễn (Setpoint, cấu hình)</td>
              </tr>
              <tr>
                <td><code>0x01F4 .. 0x0203</code></td>
                <td><code>40501 .. 40516</code></td>
                <td><code>COUNTER0 .. 7</code></td>
                <td>Đọc / Ghi (RW)</td>
                <td>Giá trị tích lũy bộ đếm sản phẩm (INT32)</td>
              </tr>
              <tr>
                <td><code>0x03E8 .. 0x03EB</code></td>
                <td><code>41001 .. 41004</code></td>
                <td><code>SYS_DIAG</code></td>
                <td>Chỉ đọc (RO)</td>
                <td>Mã lỗi chẩn đoán phần cứng, tải CPU (%), thời gian quét (ms)</td>
              </tr>
            </tbody>
          </table>
        </section>
        """
    },
    {
        "id": "app_c",
        "num": "C",
        "title": "Phụ lục C: Bảng Mã Lỗi Hệ thống & Cây Chẩn đoán Sự cố",
        "sections": [
            ("C.1", "Danh mục Mã Lỗi Vận hành & Cây Quyết định Chẩn đoán"),
        ],
        "content": f"""
        <section class="section-container" id="sec-c-1">
          <h2 class="section-title"><span class="sec-num">C.1</span> Danh mục Mã Lỗi Vận hành &amp; Cây Quyết định Chẩn đoán</h2>
          <div class="table-caption">Bảng C.1: Bảng Mã Lỗi Phổ Biến &amp; Quy Trình Xử Lý Nhanh Tại Tủ Điện</div>
          <table>
            <thead>
              <tr>
                <th style="width: 18%;">Mã Lỗi / Hiện tượng</th>
                <th style="width: 25%;">Tên Kỹ thuật</th>
                <th style="width: 27%;">Nguyên nhân Thực tế Điển hình</th>
                <th style="width: 30%;">3 Bước Xử lý Triệt để Tại Hiện trường</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>Huy hiệu [STALE]</strong></td>
                <td>Mất Dữ liệu Truyền thông RS-485 / MQTT</td>
                <td>Đứt dây tín hiệu A/B, cảm biến mất nguồn 24V, hoặc lệch tốc độ Baud rate.</td>
                <td>1. Đo nguồn 24V cấp cho cảm biến.<br/>2. Mở Wire Inspector (Ctrl+Shift+D) đọc thử 1 lần.<br/>3. Đảo chéo 2 đầu dây RS-485 A(+) và B(-).</td>
              </tr>
              <tr>
                <td><strong>Lỗi 0x02</strong></td>
                <td>Modbus Illegal Data Address</td>
                <td>SCADA đọc vượt quá giới hạn thanh ghi hoặc đọc vắt ngang qua 2 phân vùng khác nhau.</td>
                <td>1. Kiểm tra độ lệch địa chỉ 0-based so với 1-based (+1).<br/>2. Tách lệnh đọc thành 2 chu kỳ riêng biệt.</td>
              </tr>
              <tr>
                <td><strong>Lỗi 0xE001</strong></td>
                <td>Phép tính Chia cho 0 (Zero-Divide)</td>
                <td>Toán hạng <code>IN2</code> của khối <code>MATH (DIV/MOD)</code> bằng 0 hoặc cảm biến đo về 0.</td>
                <td>1. Đặt khối <code>COMPARATOR</code> chặn điều kiện <code>IN2 != 0</code>.<br/>2. Dùng khối <code>GUARD</code> trước khi thực hiện phép chia.</td>
              </tr>
              <tr>
                <td><strong>Lỗi 0xE002</strong></td>
                <td>Tràn Số Học (ALU Overflow)</td>
                <td>Tích phép nhân hoặc tổng vượt quá giới hạn cực đại của <code>INT32</code>.</td>
                <td>1. Chuyển kiểu dữ liệu sang <code>REAL</code> (Float32).<br/>2. Đặt khối <code>SCALE</code> thu hẹp dải đo trước khi tính.</td>
              </tr>
              <tr>
                <td><strong>Không nhận COM</strong></td>
                <td>USB CDC Port Not Found</td>
                <td>Dùng nhầm cáp sạc 2 sợi, hoặc cổng COM đang bị phần mềm ModScan/PuTTY khóa.</td>
                <td>1. Đổi sang cáp USB truyền dữ liệu chính hãng.<br/>2. Đóng toàn bộ các phần mềm terminal khác.<br/>3. Nhấn F4 quét lại cổng kết nối.</td>
              </tr>
            </tbody>
          </table>
        </section>
        """
    }
]
