# Bảo mật MQTT TLS & Cấu hình Mạng Wi-Fi

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-TOOL-002</span>
  <span><strong>Phím tắt:</strong> <code>Ctrl+Shift+M</code> (MQTT) &bull; <code>Ctrl+Shift+W</code> (Wi-Fi)</span>
  <span><strong>Giao thức:</strong> MQTT v3.1.1 / v5.0 qua TLS 1.2 &amp; 1.3 &bull; IEEE 802.11b/g/n</span>
  <span><strong>Tiêu chuẩn Bảo mật:</strong> IEC 62443-4-2 (X.509 mTLS)</span>
</div>

Các dòng bộ điều khiển biên, Gateway và thiết bị DTU có kết nối mạng của SynaptiX sử dụng cơ chế quản lý hạ tầng mạng IP tập trung ở cấp độ toàn thiết bị. Thay vì phải nhập lặp lại tài khoản máy chủ hay mật khẩu mạng trong từng khối hàm riêng lẻ, kỹ sư chỉ cần thiết lập cấu hình máy chủ đám mây một lần tại **Hộp thoại MQTT Broker & Bảo mật TLS (`Ctrl+Shift+M`)** và cấp phát kết nối không dây tại **Hộp thoại Cấu hình & Quét Sóng Wi-Fi (`Ctrl+Shift+W`)**.

---

## Phần 1: Hộp thoại MQTT Broker & Bảo mật TLS (`Ctrl+Shift+M`)

Nhấn tổ hợp phím `Ctrl+Shift+M` (hoặc vào menu **Thiết bị -> Cấu hình MQTT Broker...**) để thiết lập phiên kết nối MQTT Client toàn cục dùng chung cho mọi khối [`MQTT_PUB`](/vi/blocks/mqtt-pub), [`MQTT_SUB`](/vi/blocks/mqtt-sub) trên Sơ đồ Logic cũng như kênh Uplink MQTT trong [DTU Gateway Studio](/vi/tools/dtu-gateway-studio).

![Hộp thoại Cấu hình MQTT Broker & Chứng chỉ Bảo mật X.509 mTLS](/images/synaptix_mqtt_broker.png)

### 1.1 Thông số Phiên Kết nối & Xác thực Cơ bản

| Tham số | Mặc định | Giới hạn / Định dạng | Giải thích Chức năng Kỹ thuật |
| :--- | :--- | :--- | :--- |
| **Kích hoạt Dịch vụ MQTT** | `Tắt` | `Bật` / `Tắt` | Công tắc tổng khởi chạy tiến trình MQTT Client bất đồng bộ trên thiết bị. |
| **Broker Host / IP** | *(Trống)* | Tối đa `64` ký tự ASCII | Tên miền đầy đủ FQDN (VD: `a1b2c3d4-ats.iot.ap-southeast-1.amazonaws.com`, `broker.emqx.io`) hoặc địa chỉ IPv4 nội bộ (VD: `192.168.1.200`). |
| **Cổng kết nối (`Port`)** | `1883` (`8883` khi bật TLS) | `1 .. 65535` | Tự động chuyển đổi thông minh giữa `1883` (TCP thuần) và `8883` (MQTTS mã hóa) khi gạt nút **Sử dụng TLS**, đồng thời vẫn cho phép nhập cổng tùy chỉnh của doanh nghiệp. |
| **Client ID** | Số Serial thiết bị | Tối đa `32` ký tự ASCII | Mã định danh phiên MQTT duy nhất. Phải khác biệt trên toàn máy chủ Broker để tránh hiện tượng hai thiết bị đá văng kết nối của nhau liên tục (`Session Takeover`). |
| **Username / Password** | *(Trống)* | Tối đa `32` ký tự ASCII/trường | Tài khoản xác thực cho các Broker dùng mật khẩu (EMQX, HiveMQ Cloud, Mosquitto, RabbitMQ). Để trống nếu Broker xác thực thuần bằng chứng chỉ mTLS (như AWS IoT Core). |
| **Chu kỳ Keep-Alive** | `60 giây` | `10 .. 3600 giây` | Khoảng thời gian nhàn rỗi tối đa trước khi thiết bị tự phát gói `PINGREQ` nhằm duy trì phiên NAT trên tường lửa và kiểm tra kết nối sống tới Broker. |
| **Clean Session** | `Bật` (`true`) | `true` / `false` | Khi `true`, thiết lập phiên sạch mỗi lần kết nối lại. Đặt `false` nếu muốn Broker lưu giữ hàng đợi lệnh điều khiển `QoS 1` / `QoS 2` gửi xuống khối [`MQTT_SUB`](/vi/blocks/mqtt-sub) trong lúc thiết bị tạm thời rớt mạng. |

---

### 1.2 Bản tin Di chúc Last Will & Testament (LWT — Phát hiện Mất nguồn Tức thời)

Khi một trạm biên tại nhà máy bị cúp nguồn 24V DC đột ngột hoặc đứt cáp quang WAN, thiết bị không thể kịp gửi gói tin `DISCONNECT` hợp lệ. Tính năng **Last Will & Testament (LWT)** đăng ký trước một bản tin khẩn cấp ngay lúc bắt tay với Broker; ngay khi quá hạn `1.5x Keep-Alive` mà không thấy tín hiệu sống, Broker sẽ tự động phát bản tin báo mất kết nối lên Topic giám sát:

| Trường Cấu hình LWT | Giá trị Mặc định | Khuyến nghị Vận hành Thực tế |
| :--- | :--- | :--- |
| **Kích hoạt LWT (`WillEnabled`)** | `Tắt` | **Luôn Bật** cho mọi trạm thu thập dữ liệu và điều khiển từ xa. |
| **Topic Di chúc (`WillTopic`)** | `synaptix/device/status` | Đặt theo phân cấp nhà máy, ví dụ: `factory/line1/node01/status`. |
| **Nội dung Di chúc (`WillPayload`)** | `offline` | Chuỗi văn bản thuần báo trạng thái ngắt kết nối (tối đa 30 ký tự). Khuyến nghị dùng text thuần (`offline`, `0`, `lost`) thay cho JSON để tránh ký tự `"` gây lỗi lệnh AT của module Wi-Fi. |
| **Mức QoS Di chúc (`WillQos`)** | `QoS 1` | Chọn `QoS 1` (Ít nhất một lần) để đảm bảo phần mềm giám sát chắc chắn nhận được cảnh báo rớt trạm. |
| **Lưu giữ (`WillRetain`)** | `true` | Đặt `true` để bất kỳ màn hình Web/SCADA nào vừa mở lên đều biết ngay trạng thái trực tuyến hiện hành của trạm. |

---

### 1.3 Xác thực Chứng chỉ Số X.509 Server TLS & Mutual TLS (`mTLS`)

Đáp ứng tiêu chuẩn an ninh mạng công nghiệp **IEC 62443-4-2**, SynaptiX IDE tích hợp sẵn bộ phân tích chứng chỉ X.509 và cơ chế nạp phân mảnh an toàn hỗ trợ cả **TLS Một chiều (Server TLS)** lẫn **TLS Xác thực Hai chiều (`mTLS`)**:

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>PHA 1</span>
      </div>
      <span class="synx-node-status">X.509 PARSE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Giải Mã X.509</span>
        <span class="synx-node-alias">Kiểm Tra Subject</span>
        <span class="synx-node-val">Hạn Dùng &amp; Fingerprint</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Flash</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,13 L5,13 L5,9.5 L8,9.5 L8,6 L11,6 L11,3 L14,3" /></svg>
        <span>PHA 2</span>
      </div>
      <span class="synx-node-status">TRANSFER</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">IDE ➔ Thiết Bị</span>
        <span class="synx-node-alias">Chuyển Giao Khóa</span>
        <span class="synx-node-val">CA + Client Cert + Key</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">:8883</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M10,1 L5,8.5 L8.5,8.5 L6,15 L11,7.5 L7.5,7.5 Z" /></svg>
        <span>PHA 3</span>
      </div>
      <span class="synx-node-status">TLS 1.3</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">TLS Bắt Tay</span>
        <span class="synx-node-alias">Cổng 8883 MQTTS</span>
        <span class="synx-node-val">TCP SYN + ClientHello</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">mTLS</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 L13.5,4 L13.5,9.5 Q13.5,13.5 8,15 Q2.5,13.5 2.5,9.5 L2.5,4 Z" /></svg>
        <span>PHA 4</span>
      </div>
      <span class="synx-node-status">MUTUAL</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Đối Chiếu mTLS</span>
        <span class="synx-node-alias">Hai Chiều Xác Thực</span>
        <span class="synx-node-val">Verify CA &amp; Sign</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Connack</span>
  </div>

  <div class="synx-canvas-node is-step is-done">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>PHA 5</span>
      </div>
      <span class="synx-node-status is-active">● CONNECTED</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Phiên Bảo Mật</span>
        <span class="synx-node-alias">MQTT CONNACK</span>
        <span class="synx-node-val">Kết Nối OK (0x00)</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

1. **Chứng chỉ Gốc Máy chủ (`Root CA Certificate` — `BrowseCaCert`):**
   - Nạp tệp `.crt`, `.cer` hoặc `.pem` (ví dụ: *Amazon Root CA 1*, *DigiCert Global Root G2* hoặc CA nội bộ của nhà máy).
   - SynaptiX IDE tự động giải mã và hiển thị minh bạch **Chủ thể (`Subject`)**, **Tổ chức cấp (`Issuer`)**, **Ngày hết hạn (`Expiry`)** và **Mã băm `SHA-256` (`Fingerprint`)** ngay trên giao diện.
2. **Chứng chỉ Thiết bị & Khóa Bí mật hai chiều (`mTLS` — `UseMtls`):**
   - Bắt buộc khi kết nối tới **AWS IoT Core**, **Azure Event Grid MQTT** hoặc cụm **EMQX Enterprise** bảo mật cao.
   - Hỗ trợ nạp cặp tệp rời (**Client Certificate** `.crt`/`.pem` + **Private Key** `.key`/`.pem`) hoặc gói gộp có mật khẩu bảo vệ **PKCS#12** (`.pfx` / `.p12`).
3. **Tùy chọn Đàm phán TLS Nâng cao:**
   - **Phiên bản TLS:** `Tự động (TLS 1.2 / 1.3)` (mặc định), `Chỉ TLS 1.2`, hoặc `Chỉ TLS 1.3`.
   - **Ghi đè SNI Hostname (`SniHostname`):** Chỉ định chuỗi Server Name Indication khi kết nối qua IP trung gian tới bộ cân bằng tải đám mây đa thuê bao.
   - **Bỏ qua Lỗi Khớp Tên miền (`IgnoreHostname`):** Hữu ích khi kết nối tới Broker nội bộ nhà máy qua địa chỉ IP tĩnh (VD: `192.168.10.50`) trong khi chứng chỉ chỉ khai báo tên máy chủ DNS nội bộ.
   - **Cho phép Chứng chỉ Tự ký (`AllowSelfSigned`):** Chỉ sử dụng khi thử nghiệm phòng Lab; **luôn tắt trong môi trường sản xuất** và nạp tệp Root CA chuẩn.

::: tip Kiểm thử Bắt tay TLS Trực tiếp (`Kiểm tra Kết nối`)
Nhấn nút **Kiểm tra Kết nối (`Test Connection`)** ngay trong hộp thoại `Ctrl+Shift+M` trước khi nạp xuống thiết bị. SynaptiX IDE sẽ mở phiên kết nối TCP + `SslStream` thực tế (bao gồm cả xác thực chứng chỉ hai chiều mTLS với ngưỡng chờ `6 giây`) và báo cáo ngay phiên bản giao thức TLS cùng thuật toán mã hóa đã đàm phán thành công!
:::

### 1.4 Cơ chế Đồng bộ Thông minh trong Deploy (Smart Sync & CRC Checksum)

Nhằm đảm bảo trải nghiệm vận hành nhất quán và không làm phân mảnh quy trình, SynaptiX IDE **không sử dụng nút nạp chứng chỉ rời**. Toàn bộ việc nạp chứng chỉ được tự động hóa hoàn toàn trong quy trình **Deploy (`F5`)**:
* **Kiểm tra Đối soát Checksum (`0x0F43..0x0F46`):** Trước khi nạp, Studio đọc mã kiểm tra CRC-16 của các chứng chỉ đang lưu trong Flash MCU.
* **Tối ưu hóa Tốc độ Deploy:** Nếu chứng chỉ trên thiết bị đã đầy đủ và khớp 100% với file trên máy tính, Studio tự động bỏ qua bước nạp phân đoạn chunk, giúp quá trình nạp logic FBD diễn ra tức thời (<100ms) và bảo vệ số lần xóa/ghi của chip nhớ Flash.
* **Tự động Cập nhật:** Khi người dùng thay đổi file chứng chỉ mới hoặc nạp vào thiết bị trắng, Studio tự động phân mảnh theo chuẩn `SPLC-SPEC-MQTT-003` (`0x0F00`), bắt tay ACK (`0x0F40`) và chốt Commit nguyên tử (`0x0F42 = 0xA5A5`).

---

## Phần 2: Cấu hình & Trình Quét Sóng Wi-Fi Trực tiếp (`Ctrl+Shift+W`)

Đối với các dòng bộ điều khiển và DTU Gateway tích hợp Wi-Fi, nhấn `Ctrl+Shift+W` (hoặc vào menu **Thiết bị -> Cấu hình Mạng Wi-Fi...**) để thiết lập chế độ **Trạm Thu phát (`Station - STA`)** băng tần 2.4 GHz IEEE 802.11b/g/n và giám sát cường độ sóng thực tế tại vị trí lắp đặt.

### 2.1 Thông số Kết nối Trạm Wi-Fi & Tự động Tái kết nối

| Thông số | Mặc định | Giới hạn / Cơ chế | Khuyến nghị Triển khai |
| :--- | :--- | :--- | :--- |
| **Kích hoạt Wi-Fi (`IsEnabled`)** | `Tắt` | `Bật` / `Tắt` | Bật phân hệ thu phát không dây ở chế độ Station (`STA`). |
| **Tự động Kết nối lại (`AutoReconnect`)** | `Bật` (`true`) | Tự động thử lại giãn cách | **Luôn bật** để thiết bị tự động bắt sóng lại ngay khi bộ phát sóng AP nhà xưởng khởi động lại hoặc sau nhiễu tức thời. |
| **Tên mạng Wi-Fi (`Ssid`)** | *(Trống)* | Tối đa `32` byte UTF-8 | Nhập tay (cho mạng ẩn SSID) hoặc chọn 1 chạm từ **Trình Quét Trạm phát Trực tiếp**. |
| **Mật khẩu (`Password`)** | *(Trống)* | Tối đa `64` ký tự ASCII | Tương thích chuẩn Open, WPA-PSK, WPA2-PSK, WPA/WPA2-Mixed và WPA3-SAE. Có nút con mắt ẩn/hiện kiểm tra ký tự. |

---

### 2.2 Trình Quét Trạm phát Wi-Fi Trực tiếp (`AvailableAps`)

Khi SynaptiX IDE đang kết nối trực tuyến (`F4`) với thiết bị thật qua cáp USB CDC hoặc Ethernet, hãy chuyển sang **Tab 2: Quét Mạng Wi-Fi** (hoặc nhấn nút **Quét Wi-Fi**):

1. Phần mềm ra lệnh cho **chính ăng-ten của bo mạch SynaptiX** đang đặt trong tủ điện thực hiện quét khảo sát môi trường sóng thực tế xung quanh tủ (trả về tối đa `8` trạm phát mạnh nhất), thay vì quét bằng card Wi-Fi của máy tính kỹ sư.
2. Trong vòng `<= 5 giây`, bảng `AvailableAps` hiển thị đầy đủ **Tên SSID**, **Chuẩn Bảo mật** (`OPEN`, `WPA2-PSK`, `WPA3-PSK`), **Kênh tần số**, **Cường độ `RSSI (dBm)`** và **Chất lượng Tín hiệu (`%`)**.
3. Nhấp chọn trạm phát mong muốn và bấm **Áp dụng Mạng Đã chọn**, tên SSID chính xác tuyệt đối sẽ được điền về Tab 1, loại bỏ hoàn toàn lỗi gõ sai chữ hoa/thường.

### 2.3 Bảng Đánh giá Cường độ Sóng (`RSSI` & Chất lượng `%`) Tại Hiện trường

SynaptiX IDE quy đổi tuyến tính chỉ số `RSSI (dBm)` sang phần trăm chất lượng sóng theo công thức chuẩn `Quality% = clamp(2 * (RSSI + 100), 0, 100)`:

| Dải Cường độ (`dBm`) | Chất lượng Sóng (`%`) | Đánh giá Đường truyền RF & Khuyến nghị Lắp đặt Tủ điện |
| :--- | :--- | :--- |
| `>= -50 dBm` | `100%` | **Rất mạnh:** Tầm nhìn thẳng tới AP công nghiệp; tối ưu cho `MQTT_PUB` chu kỳ ngắn và `Modbus TCP`. |
| `-65 .. -51 dBm` | `70% .. 98%` | **Tốt (Khuyến nghị):** Đảm bảo vận hành sản xuất liên tục 24/7 với biên độ dự phòng suy hao an toàn. |
| `-75 .. -66 dBm` | `50% .. 68%` | **Trung bình / Cảnh báo:** Dễ trễ gói tin khi đặt gần biến tần công suất lớn. **Bắt buộc kéo ăng-ten dây nối dài SMA ra ngoài vỏ tủ sắt!** |
| `<= -76 dBm` | `<= 48%` | **Yếu / Chập chờn:** Nguy cơ cao lỗi bắt tay TLS và rớt phiên TCP. Cần bổ sung bộ phát AP công nghiệp hoặc chuyển sang cáp Ethernet / RS-485. |

---

### 2.4 Địa chỉ IPv4 Động (`DHCP`) so với Tĩnh (`Static IP`) & Giám sát Trực tiếp

| Chế độ IPv4 | Các trường Cấu hình | Khi nào Nên Sử dụng |
| :--- | :--- | :--- |
| **DHCP (Cấp phát tự động)** *(Mặc định)* | Tự động nhận IP, Subnet Mask, Gateway và DNS từ Router mạng nhà máy. | Phù hợp khi thiết bị chỉ chủ động đẩy dữ liệu chiều đi (`MQTT_PUB`, `HTTP_CLIENT`) lên máy chủ đám mây. |
| **Static IPv4 (IP Tĩnh)** | **Static IP** (`192.168.1.50`), **Subnet Mask** (`255.255.255.0`), **Gateway** (`192.168.1.1`), **Primary DNS** (`8.8.8.8`). | **Bắt buộc** khi hệ thống SCADA/HMI chủ động truy vấn vào thiết bị qua **Modbus TCP (Cổng `502`)** hoặc **TCP Server**, đảm bảo địa chỉ IP của trạm không bao giờ bị đổi sau khi mất điện. |

Khi đang kết nối trực tuyến với phần cứng (`IsDeviceConnected == true`), thanh **Giám sát Trạng thái Trực tiếp** tự động làm tươi mỗi `1.5 giây`, cung cấp:
- **Trạng thái Kết nối:** `Đã ngắt kết nối`, `Đang kết nối...`, `Đã kết nối`, `Đã nhận IP`, `Sai mật khẩu xác thực`, hoặc `Không tìm thấy SSID`.
- **Địa chỉ IPv4 Thực tế (`CurrentIp`)** và **Địa chỉ Vật lý `MAC Address`** (hỗ trợ bộ phận IT khai báo danh sách trắng MAC Whitelist trên mạng VLAN nhà máy).
- **Thao tác Thử nghiệm Nóng:** Bấm **Kết nối Ngay** hoặc **Ngắt kết nối** để kiểm chứng thông mạng ngay trước khi nhấn **Lưu** cấu hình vào dự án và bộ nhớ thiết bị.

---

## Tài liệu Liên quan

- [MQTT_PUB — Khối Đẩy Telemetry lên Broker](/vi/blocks/mqtt-pub)
- [MQTT_SUB — Khối Nhận Lệnh Điều khiển JSON](/vi/blocks/mqtt-sub)
- [Không gian DTU Gateway Studio (`Ctrl+Shift+D`)](/vi/tools/dtu-gateway-studio)
- [Bảng Mã lỗi & Chẩn đoán Sự cố](/vi/appendices/error-codes-and-troubleshooting)
