# MQTT_PUB — Đẩy Telemetry IIoT lên Broker

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-BLK-MQTTPUB</span>
  <span><strong>Nhóm khối hàm:</strong> Điện toán Biên IIoT &amp; Mạng IP</span>
  <span><strong>Phân lớp thực thi:</strong> Bộ phát gói tin Telemetry bất đồng bộ từ Biên lên Broker</span>
  <span><strong>Tiêu chuẩn:</strong> OASIS MQTT v3.1.1 &bull; JSON Edge Telemetry</span>
</div>

## 1. Tổng quan & Ngữ nghĩa Thực thi

Khối hàm **`MQTT_PUB`** (**IIoT MQTT Telemetry Publisher**) đóng gói trực tiếp các biến đo lường đã chuẩn hóa, bộ đếm mẻ sản xuất và cờ cảnh báo sự cố thành bản tin chuẩn hóa rồi đẩy bất đồng bộ qua mạng Wi-Fi / Ethernet / 4G lên máy chủ MQTT Broker đã cấu hình tại [Cấu hình MQTT TLS & Mạng Wi-Fi](/vi/tools/mqtt-and-wifi-config).

![Giao diện Cấu hình Khối Đẩy Dữ liệu MQTT_PUB](/images/synaptix_mqtt_pub.png)

Khối `MQTT_PUB` hỗ trợ đồng thời cả **truyền đơn biến từ dây nối** lẫn **gom nhóm đa biến vào 1 gói JSON** mà không cần viết bất kỳ dòng mã lệnh kịch bản nào:
- **3 Cơ chế Kích hoạt (`PublishMode`):** Theo sườn xung sự kiện (`Event`), theo độ lệch biến thiên (`COV` `Deadband`) hoặc định kỳ theo thời gian (`Cyclic` `IntervalMs`).
- **3 Chế độ Đóng gói (`PayloadMode`):** Bảng ghép trường trực quan **JSON Visual Field Builder** (`WrapValues`, `IncludeDevice`, `IncludeTimestamp`), chuỗi số thô **Raw**, hoặc **Bản mẫu Tùy biến (`Custom` `<= 128 ký tự`)** với bộ giải mã biến Macro tất định ngay trên vi điều khiển.

---

## 2. Sơ đồ Chân & Hợp đồng Tín hiệu

Khối `MQTT_PUB` tự động đồng bộ các chân ngõ vào bên trái (`SynchronizeConnectors`) khớp chính xác với tổ hợp `PublishMode` và `PayloadMode` đang chọn:

| Tên chân | Chiều | Bản chất Tín hiệu | Kiểu dữ liệu | Điều kiện Hiển thị | Hợp đồng Chức năng |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **`Trig`** | Ngõ vào | Sự kiện (`0 → 1`) | `BOOL` | **Chỉ hiển thị khi `PublishMode = Event`** *(ẩn ở chế độ `COV` & `Cyclic`)* | **Xung Kích hoạt Gửi:** Chụp giá trị tức thời của các trường dữ liệu và đưa bản tin vào hàng đợi phát tại sườn lên (`0 → 1`). |
| **`Val`** | Ngõ vào | Giá trị | `BOOL` / `INT32` / `REAL` | Hiển thị khi `PayloadMode = Raw`, hoặc `JSON` chưa thêm trường, hoặc `Custom` chỉ dùng `` `{val}` `` | **Ngõ vào Giá trị Đơn:** Đại lượng đo lường chính được đóng gói phát đi. |
| **`<Field.Key>`** | Ngõ vào *(Động)* | Giá trị | `BOOL` / `INT32` / `REAL` | Hiển thị khi `PayloadMode = JSON` và `Fields.Count > 0` | **Chân Trường Dữ liệu JSON:** Mỗi dòng trong bảng ghép trường sinh ra 1 chân vào mang tên khóa JSON tương ứng (thay thế chân `Val`). |
| **`<Token>`** | Ngõ vào *(Động)* | Giá trị | `BOOL` / `INT32` / `REAL` | Hiển thị khi `PayloadMode = Custom` chứa `` `{val1}` ``..`` `{val8}` `` | **Chân Biến Bản mẫu:** Tự động trích xuất tối đa `8` chân dữ liệu từ các token khai báo trong mẫu JSON. |
| **`Done`** | Ngõ ra | Sự kiện (Xung) | `BOOL` | Luôn hiển thị | Phát xung `1` khi đẩy gói tin thành công (và nhận đủ xác nhận PUBACK với `QoS 1`/`2`). |
| **`Err`** | Ngõ ra | Giá trị | `BOOL` | Luôn hiển thị | Bật mức `1` khi mất kết nối tới MQTT Broker hoặc hết thời gian chờ truyền mạng. |

---

## 3. Tham số Cấu hình & Ràng buộc Inspector

### 3.1. Tham số Chủ đề Topic, QoS & Kích hoạt

| Tham số | Khóa Thuộc tính | Kiểu & Miền giá trị | Mặc định | Mô tả Kỹ thuật & Ràng buộc |
| :--- | :--- | :--- | :---: | :--- |
| **Chủ đề MQTT (`Topic`)** | `Topic` | `String` (UTF-8) | `"synaptix/telemetry/data"` | Đường dẫn chủ đề nhận tin. Hỗ trợ biến `` `{device}` `` và 3 mẫu nhanh (`synaptix/{device}/telemetry`, `synaptix/{device}/alarms`, `synaptix/{device}/status`). Không được chứa ký tự đại diện (`+`, `#`). |
| **Chất lượng Dịch vụ (`QoS`)** | `Qos` | `0` \| `1` \| `2` | `0` | `0`: Tối đa 1 lần (dữ liệu chu kỳ nhanh) · `1`: Ít nhất 1 lần (cảnh báo & đếm mẻ) · `2`: Chính xác 1 lần. |
| **Lưu Tin Cuối (`Retain`)** | `Retain` | `BOOL` | `false` | Khi bật `true`, Broker lưu giữ bản tin mới nhất để cấp ngay cho giao diện giám sát vừa mở lên. |
| **Chế độ Phát (`PublishMode`)** | `PublishMode` | `"Event"` \| `"COV"` \| `"Cyclic"` | `"Event"` | Chọn phát theo sườn xung, theo ngưỡng thay đổi giá trị (COV) hoặc theo chu kỳ thời gian. |
| **Ngưỡng Chết COV** | `Deadband` | `Double` (`> 0`) | `0.5` | Độ lệch tuyệt đối tối thiểu (`|x - x_trước| ≥ Deadband`) để kích hoạt gửi tin ở chế độ `COV`. |
| **Chu kỳ Phát Định kỳ** | `IntervalMs` | `UINT32` (`ms`) | `1000` | Khoảng thời gian lặp lại tính bằng mili-giây khi chọn `PublishMode = Cyclic`. |
| **Đơn vị Vật lý (`Unit`)** | `Unit` | `String` | `"bar"` | Chuỗi đơn vị dùng để điền vào biến `` `{unit}` `` trong bản mẫu tùy chỉnh. |

### 3.2. Ba Chế độ Tải trọng (`PayloadMode`) & Từ điển Biến Macro

| Chế độ Tải trọng | Công cụ Điều khiển | Định dạng Xuất & Giới hạn Firmware |
| :--- | :--- | :--- |
| **1. `JSON`** *(Visual Field Builder)* | Bảng `Fields` (`Key`, `Tag`, `Unit`), các cờ `WrapValues` (`true`), `IncludeDevice` (`true`), `IncludeTimestamp` (`true`) | Sinh chuỗi JSON chuẩn xác tự động. Khi `WrapValues = true`, dữ liệu bọc gọn trong `"values": { ... }`; khi `false`, các khóa nằm phẳng ngay ở cấp gốc cùng `"device"` và `"ts"`. |
| **2. `Raw`** | Chân `Val` hoặc biến `DataTag` | Xuất chuỗi giá trị số thuần túy (ví dụ `45.2` hoặc `1`) tối ưu băng thông tối đa. |
| **3. `Custom`** *(Bản mẫu)* | `CustomTemplate` (**giới hạn nghiêm ngặt `<= 128 ký tự`**) | Mẫu JSON/CSV tùy biến giải mã trực tiếp trên vi điều khiển. Có bộ đếm ký tự trực tiếp (`0 / 128`) và kiểm tra danh sách token hợp lệ. |

### Bảng Biến Macro Hỗ trợ trong Chế độ `Custom` (Tối đa `128` Ký tự)

| Cú pháp Token | Giá trị Thay thế lúc Chạy thực | Ví dụ Kết quả |
| :--- | :--- | :--- |
| `` `{val}` `` | Giá trị của ngõ vào chính / biến `DataTag` | `12.45` |
| `` `{val1}` `` .. `` `{val8}` `` | Giá trị của Trường / Chân cắm thứ `1..8` theo kiểu tự nhiên của Tag | `248` |
| `` `{val1:0}` `` .. `` `{val8:3}` `` | Giá trị Trường `1..8` ép định dạng cố định `0..3` chữ số thập phân | `` `{val1:2}` `` → `12.40` |
| `` `{tag}` ``, `` `{tag1}` `` .. `` `{tag8}` `` | Tên định danh của Tag chính hoặc Tag gắn ở Trường `1..8` | `"VREG0_PipeBar"` |
| `` `{vals}` `` | Mảng JSON gộp toàn bộ giá trị các trường đã khai báo | `[12.45, 38.2, 1]` |
| `` `{ts}` `` | Nhãn thời gian thực Unix Epoch (mili-giây / giây) | `1730880000125` |
| `` `{device}` `` | Mã định danh thiết bị điều khiển biên SynaptiX | `"SynaptiX_Controller"` |
| `` `{unit}` `` | Chuỗi đơn vị khai báo tại thuộc tính `Unit` | `"bar"` |

*(Tra cứu đầy đủ ngân sách bộ nhớ tại [Từ điển Macro & Giới hạn Tài nguyên](/vi/appendices/macro-tokens-and-limits)).*

---

## 4. Chế độ Phát Tin & Đóng gói Tải trọng

Khối `MQTT_PUB` chuyển dữ liệu cảm biến thành bản tin mạng theo 3 phương thức linh hoạt:
- **Chế độ Kích hoạt (`PublishMode`):** Phát tin theo sườn lên chân `Trig`, phát định kỳ theo chu kỳ `IntervalMs`, hoặc chỉ phát khi dữ liệu biến thiên vượt ngưỡng (`Δ ≥ Deadband`).
- **Đóng gói linh hoạt:** Hỗ trợ tạo tự động gói JSON chuẩn (`Visual Field Builder`), chuỗi số thô (`Raw`) hoặc mẫu tùy biến theo chuẩn MES/ERP (`Custom <= 128 ký tự`).
- **Hàng đợi ngầm không chặn:** Gói tin được đưa vào hàng đợi mạng bất đồng bộ, hoàn toàn không làm gián đoạn chu kỳ quét điều khiển thời gian thực.

---

## 5. Kiểm tra Hợp lệ, Xử lý Lỗi & Chẩn đoán Trực tuyến

- **Kiểm tra & Làm sạch Chủ đề Topic:** Nếu `Topic` để trống, chứa khoảng trắng hoặc lẫn ký tự đại diện đăng ký nhận (`+`, `#`), cờ `HasTopicError` lập tức báo đỏ. Nút **Sanitize** (`SanitizeTopicCommand`) giúp tự động chuẩn hóa đường dẫn hợp lệ chỉ sau 1 cú nhấp chuột.
- **Khóa Trần `128` Ký tự & Kiểm soát Token Lạ:** Ở chế độ `Custom`, nếu mẫu vượt quá `128` ký tự hoặc chứa token không nằm trong tập hỗ trợ của firmware, `CustomTemplateValidationError` sẽ chặn cảnh báo ngay từ khâu thiết kế.
- **Xem trước Payload Thời gian thực (`PreviewPayloadText`):** Hiển thị trực tiếp gói JSON hoàn chỉnh ngay trong Inspector, đồng thời cập nhật số bản tin đã phát (`PublishCount`), thời điểm phát gần nhất và nội dung gói cuối khi bật [Gỡ lỗi Trực tuyến](/vi/tools/simulator-and-live-debug) (`F8`).

---

## 6. Ví dụ Đấu nối Công nghiệp Thực tế

### Gom Nhóm Đa Cảm biến Trạm Bơm Đẩy Đám mây (`JSON` Visual Field Builder + `COV`)

**Bài toán:** Gom 3 thông số của cụm bơm gồm áp suất đầu đẩy (`VREG0_PipeBar`), dòng tải động cơ (`VREG1_MotorAmp`) và trạng thái chạy bơm (`DO0_PumpRun`) vào một bản tin JSON duy nhất gửi lên topic `synaptix/skid01/telemetry` (`QoS 1`) mỗi khi áp suất biến thiên từ `0.2 bar` trở lên.

```json
{
  "device": "SynaptiX_Skid01",
  "ts": 1730880420000,
  "values": {
    "pressure_bar": 6.45,
    "motor_amps": 14.2,
    "pump_running": 1
  }
}
```

**Các bước cấu hình:**
1. Kéo khối **`MQTT_PUB`** ra Canvas, đặt `Topic = "synaptix/skid01/telemetry"`, `Qos = 1`, `Retain = false`.
2. Chọn `PublishMode = "COV"` và nhập `Deadband = 0.2` (chân `Trig` tự động ẩn gọn).
3. Chọn `PayloadMode = "JSON"`, giữ bật `WrapValues = true`, `IncludeDevice = true`, `IncludeTimestamp = true`, sau đó thêm 3 dòng trong bảng **Fields**:
   - `pressure_bar` → `VREG0_PipeBar` (`REAL`)
   - `motor_amps` → `VREG1_MotorAmp` (`REAL`)
   - `pump_running` → `DO0_PumpRun` (`BOOL`)
4. Nối dây trực tiếp từ các khối đo lường phía trước vào 3 chân `pressure_bar`, `motor_amps` và `pump_running` vừa sinh ra trên thân khối.
