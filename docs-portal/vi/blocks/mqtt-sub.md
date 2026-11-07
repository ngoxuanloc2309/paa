# MQTT_SUB — Nhận Lệnh Điều khiển & Điểm đặt JSON (Inbound)

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-BLK-MQTTSUB</span>
  <span><strong>Nhóm khối hàm:</strong> Điện toán Biên IIoT &amp; Mạng IP</span>
  <span><strong>Phân lớp thực thi:</strong> Khối nguồn lắng nghe ngầm &amp; Phân tích JSON nguyên tử</span>
  <span><strong>Tiêu chuẩn:</strong> OASIS MQTT v3.1.1 &bull; Phòng vệ Dữ liệu Xấu IEC 62443</span>
</div>

## 1. Tổng quan & Ngữ nghĩa Thực thi

Khối hàm **`MQTT_SUB`** (**IIoT MQTT Inbound Subscriber**) là **khối nguồn lắng nghe ngầm thuần túy**, chuyên đăng ký nhận gói tin điều khiển và điểm đặt (Setpoint) từ máy chủ Cloud / MES / WMS, kiểm tra nghiêm ngặt cấu trúc và dải giới hạn an toàn, sau đó bóc tách từng trường dữ liệu ra chân cắm sơ đồ khối và vùng nhớ Tag nội bộ.

![Hộp thoại Cấu hình Khối Nhận Dữ liệu MQTT_SUB & Khung Giả lập JSON](/images/synaptix_mqtt_sub.png)

Các cơ chế an toàn công nghiệp cốt lõi:
- **Kiến trúc Lắng nghe Thuần túy Không Chân Vào:** Khối `MQTT_SUB` **không có chân `EN` hay `Trig`** ở cạnh trái — khối tự động lắng nghe ngầm liên tục ngay khi kết nối Broker thông suốt, giữ cho sơ đồ điều khiển luôn thoáng gọn.
- **Giải mã JSON Nguyên tử "Tất cả hoặc Không gì cả" (Atomic All-or-Nothing):** Mọi gói tin đến đều phải vượt qua bước kiểm tra toàn diện trên **100% các chân động** trước khi chạm vào bộ nhớ thực thi. Chỉ cần 1 trường bị thiếu, sai kiểu dữ liệu hoặc vượt ngưỡng `[MinValue .. MaxValue]`, **không một chân nào bị thay đổi**, bộ đếm gói loại bỏ `DroppedCount` tăng lên và chân `Err` bật cảnh báo kèm mã lỗi chính xác.
- **Chống Lệnh Tồn đọng Lúc Khởi động & Fail-Safe Mất Mạng:** Cờ `IgnoreRetainedOnBoot` chặn đứng các bản tin lưu cữu (`Retained`) từ hôm trước làm máy tự chạy bất ngờ khi vừa đóng điện, kết hợp bộ canh thời gian `FailSafeTimeoutMs` tự động đưa hệ thống về trạng thái an toàn khi đứt mạng đám mây.

---

## 2. Sơ đồ Chân & Hợp đồng Tín hiệu

Khối `MQTT_SUB` để trống hoàn toàn cạnh trái (0 chân vào) và xuất `2 + N` chân ở cạnh phải:

| Tên chân | Chiều | Bản chất Tín hiệu | Kiểu dữ liệu | Điều kiện Hiển thị | Hợp đồng Chức năng |
| :--- | :---: | :---: | :---: | :---: | :--- |
| *(Không có)* | Ngõ vào | — | — | Luôn để trống | Khối nguồn tự lắng nghe ngầm; không cần nối dây cấp nguồn cho phép. |
| **`Valid`** | Ngõ ra | Sự kiện (Xung 1 chu kỳ) | `BOOL` | Luôn hiển thị | **Xung Chốt Dữ liệu Hợp lệ:** Phát xung mức `1` kéo dài đúng 1 chu kỳ quét (`10 ms`) ngay khi bản tin JSON vượt qua toàn bộ điều kiện kiểm tra và cập nhật đồng thời các chân ra. |
| **`Err`** | Ngõ ra | Giá trị (Chốt trạng thái) | `BOOL` | Luôn hiển thị | **Cờ Báo Lỗi Gói Tin / Mạng:** Bật mức `1` khi lỗi cú pháp JSON, thiếu trường, sai kiểu, vượt ngưỡng Min/Max, tràn hàng đợi FIFO hoặc đứt mạng quá hạn. Tự xóa về `0` khi nhận gói hợp lệ mới. |
| **`<Pin.Label>`** | Ngõ ra *(Động)* | Giá trị | `BOOL`, `INT16`, `INT32`, `FLOAT`, `STRING` | 1 chân ứng với mỗi dòng trong `Pins` | **Giá trị Trường JSON Trích xuất:** Xuất dữ liệu bóc từ `JsonPath` (tự động nhận diện cả JSON phẳng `{"grid": 5}` lẫn JSON bọc `{"values": {"grid": 5}}`) và ghi vào `TargetTag`. |

---

## 3. Tham số Cấu hình & Ràng buộc Inspector

### 3.1. Tham số Đăng ký Topic, Hàng đợi & An toàn Fail-Safe

| Tham số | Khóa Thuộc tính | Kiểu & Miền giá trị | Mặc định | Mô tả Kỹ thuật & Ý nghĩa An toàn Công nghiệp |
| :--- | :--- | :--- | :---: | :--- |
| **Chủ đề Lắng nghe (`Topic`)** | `Topic` | `String` | `"synaptix/commands/inbound"` | Bộ lọc chủ đề MQTT (có nút làm sạch `SanitizeTopic`; hỗ trợ ký tự đại diện đơn cấp `+` và đa cấp `#` chuẩn MQTT). |
| **Mức Dịch vụ (`QoS`)** | `Qos` | `0` \| `1` \| `2` | `1` | Mặc định `QoS 1` (giao ít nhất 1 lần) đảm bảo không thất lạc lệnh điều khiển từ xa. |
| **Chế độ Hàng đợi** | `QueueMode` | `FIFO` \| `LatestWins` | `FIFO` | **`FIFO`** (`Sức chứa = 16`): xử lý tuần tự từng gói tin theo thứ tự đến (1 gói / chu kỳ quét) để không nuốt đơn phân loại hàng gửi dồn dập. **`LatestWins`**: hủy gói cũ, chỉ áp dụng giá trị điểm đặt mới nhất. |
| **Sức chứa Hàng đợi** | `QueueCapacity` | `INT32` | `16` | Độ sâu bộ đệm vòng khi hoạt động ở chế độ `QueueMode = FIFO`. |
| **Bỏ qua Tin Lưu cữu Lúc Bật máy** | `IgnoreRetainedOnBoot` | `BOOL` | `true` | Tự động loại bỏ gói tin gắn cờ `Retained` mà Broker đẩy xuống ngay lúc vừa kết nối, chống kích hoạt cơ cấu chấp hành ngoài ý muốn. |
| **Thời gian Chờ Fail-Safe** | `FailSafeTimeoutMs` (`TimeoutMs`) | `UINT32` (`ms`, `0` = Tắt) | `0` | Bộ canh thời gian mất liên lạc (mili-giây). Nếu quá hạn không nhận được tin mới hợp lệ, thực thi `FailSafeAction` và báo mã `TimeoutExpired`. |
| **Hành vi Fail-Safe** | `FailSafeAction` | `KeepLastValue` \| `ResetToZero` \| `ResetToDefault` | `KeepLastValue` | Chiến lược dự phòng áp dụng cho toàn bộ chân động khi hết thời gian chờ hoặc rớt kết nối Broker. |

### 3.2. Cấu hình Chân Ngõ ra Động (`MqttPinBindingViewModel`)

| Trường Cấu hình | Khóa Thuộc tính | Tùy chọn Hỗ trợ | Mô tả Chi tiết |
| :--- | :--- | :--- | :--- |
| **Tên Trường / Đường dẫn JSON** | `FieldName` / `JsonPath` | ví dụ: `grid`, `setpoint_temp`, `values.speed` | Tên khóa JSON cần bóc tách; tự động đồng bộ làm nhãn chân cắm `Label` trên vùng vẽ Canvas. |
| **Kiểu Dữ liệu** | `DataType` | `Int16`, `Int32`, `Float`, `Bool`, `String` | Hợp đồng kiểu dữ liệu đích (`Bool` hiểu cả `true`/`false`, `1`/`0`, `"TRUE"`/`"FALSE"`). |
| **Giới hạn Min / Max** | `MinValue`, `MaxValue` | `Double?` (tùy chọn) | Từ chối toàn bộ bản tin (`OutOfRange`) nếu giá trị số vượt ra ngoài khoảng an toàn `[MinValue .. MaxValue]`. |
| **Giá trị Mặc định An toàn** | `DefaultValue` | `Double` (`0.0`) | Giá trị an toàn nạp xuống chân ra khi kích hoạt `FailSafeAction = ResetToDefault`. |
| **Biến Đích (`TargetTag`)** | `TargetTag` | `TagModel?` | Gán trực tiếp vào biến `VREG`, `VBIT` hoặc `DO` trong [Danh mục Biến — Tag Catalog](/vi/workspaces/tag-catalog-tab). |

---

## 4. Nguyên lý Giải mã Nguyên tử Toàn vẹn (Atomic All-or-Nothing)

Để bảo vệ an toàn cho máy móc trước các lệnh điều khiển sai cú pháp từ đám mây, khối `MQTT_SUB` áp dụng cơ chế phân giải nguyên tử:
- **Nguyên tử toàn vẹn (All-or-Nothing):** Toàn bộ các trường trong gói tin JSON phải hợp lệ đồng thời (đúng cú pháp, đủ trường `JsonPath`, đúng kiểu dữ liệu và nằm trong dải an toàn `[MinValue .. MaxValue]`).
- **Nếu 100% hợp lệ:** Cập nhật đồng loạt các chân ngõ ra và tag liên kết trong cùng 1 chu kỳ quét, đồng thời phát 1 xung `Valid = 1`.
- **Nếu có bất kỳ lỗi nào:** Khối lập tức từ chối toàn bộ gói tin, kích hoạt cờ `Err = 1` và bảo lưu nguyên trạng bộ nhớ hiện tại. Không bao giờ cập nhật nửa vời làm chập chờn trạng thái thiết bị.

---

## 5. Kiểm tra Hợp lệ, Xử lý Lỗi & Chẩn đoán Trực tuyến

### Bảng Mã Lỗi Chẩn đoán Chuẩn hóa (`MqttErrorCode`)

| Mã số | Định danh Enum | Điều kiện Phát sinh Sự cố | Cam kết Bảo vệ Bộ nhớ Thực thi |
| :---: | :--- | :--- | :--- |
| **`0`** | `None` | Phân tích và chốt dữ liệu thành công. | Cập nhật đồng bộ tất cả các chân; tăng `MsgCount`. |
| **`1`** | `ConnectionLost` | Đứt kết nối mạng TCP/TLS tới Broker. | Kích hoạt `FailSafeAction`; bật cờ `Err = 1`. |
| **`2`** | `JsonSyntaxError` | Chuỗi nhận về sai cú pháp ngoặc `{}`, dấu phẩy hoặc nháy kép. | Hủy gói tin (`DroppedCount++`); giữ nguyên giá trị cũ. |
| **`3`** | `MissingField` | Bản tin thiếu mất khóa `JsonPath` đã khai báo. | Hủy nguyên tử toàn gói; chỉ rõ tên trường bị thiếu (`FaultyField`). |
| **`4`** | `TypeMismatch` | Truyền chuỗi chữ (ví dụ `"abc"`) vào chân số `Int16`/`Float`. | Hủy nguyên tử toàn gói; không làm hỏng thanh ghi số. |
| **`5`** | `OutOfRange` | Giá trị vượt khỏi ngưỡng an toàn `[MinValue .. MaxValue]`. | Hủy nguyên tử toàn gói; chặn điểm đặt nguy hiểm cho thiết bị. |
| **`6`** | `QueueOverflow` | Tốc độ gửi dồn dập vượt quá `16` ngăn hàng đợi `FIFO`. | Cảnh báo quá tải để kỹ sư tối ưu tần suất gửi hoặc chu kỳ xử lý. |
| **`7`** | `TimeoutExpired` | Quá thời gian `FailSafeTimeoutMs` không có gói tin mới. | Chuyển về `KeepLastValue`, `ResetToZero` hoặc `ResetToDefault`. |

- **Khung Bơm Thử nghiệm Gói JSON Trực tiếp (`SimInputPayload`):** Ngay trên thanh Inspector và trong hộp thoại cấu hình `MQTT_SUB` đều tích hợp sẵn ô **Giả lập Gói tin JSON** (`SimulateSend`). Kỹ sư có thể dán trực tiếp mẫu JSON từ đội phần mềm Cloud và nhấn **Chạy thử** để kiểm tra kết quả bóc tách từng chân và thử nghiệm chặn lỗi vượt biên mà chưa cần dựng MQTT Broker thật!

---

## 6. Ví dụ Đấu nối Công nghiệp Thực tế

### Nhận Điểm đặt Nhiệt độ Chiller Từ xa Có Khóa Biên Bảo vệ (`LatestWins` + Watchdog)

**Bài toán:** Nhận lệnh cài đặt nhiệt độ nước lạnh từ hệ thống BMS (`{"temp_sp": 7.5, "enable": true}`) trên topic `synaptix/hvac/chiller1/setpoint`, khóa cứng dải cho phép `[4.0 °C .. 18.0 °C]` để ngăn lỗi nhập nhầm số âm làm đóng băng dàn bay hơi, và tự động lùi về mức tiết kiệm `12.0 °C` nếu mất kết nối đám mây quá `60,000 ms`.

<div class="synx-node-chain">
  <div class="synx-canvas-node is-trigger">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,14 L8,8 M5,10 A4,4 0 0 1 11,10 M3,7.5 A7,7 0 0 1 13,7.5 M1.5,5 A9.5,9.5 0 0 1 14.5,5" /></svg>
        <span>MQTT_SUB</span>
      </div>
      <span class="synx-node-status is-active">● WATCHDOG</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">LatestWins • 60s</span>
        <span class="synx-node-alias">Điểm Đặt BMS</span>
        <span class="synx-node-val">{temp_sp: 7.5, en: 1}</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">7.5 °C</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● GT SP</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">AI0 &gt; temp_sp</span>
        <span class="synx-node-alias">So Sánh Nhiệt Nước</span>
        <span class="synx-node-val">Nước Chiller Cần Làm Lạnh</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">TRUE</span>
  </div>

  <div class="synx-canvas-node is-guard">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 L13.5,4 L13.5,9.5 Q13.5,13.5 8,15 Q2.5,13.5 2.5,9.5 L2.5,4 Z" /></svg>
        <span>GUARD</span>
      </div>
      <span class="synx-node-status is-pass">● PASS</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">RemoteEn == 1</span>
        <span class="synx-node-alias">Cổng Cho Phép</span>
        <span class="synx-node-val">Khóa Lệnh Từ Xa</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">ENABLE</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● SET 1</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">DO0 = 1</span>
        <span class="synx-node-alias">Máy Nén Chiller</span>
        <span class="synx-node-val">Đóng Contactor Động Lực</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

**Các bước cấu hình:**
1. Kéo khối **`MQTT_SUB`** ra vùng vẽ và cài đặt `Topic = "synaptix/hvac/chiller1/setpoint"`, `Qos = 1`, `QueueMode = LatestWins`, `IgnoreRetainedOnBoot = true`.
2. Đặt `FailSafeTimeoutMs = 60000` và `FailSafeAction = ResetToDefault`.
3. Thêm 2 chân ngõ ra động:
   - `temp_sp`: `DataType = Float`, `MinValue = 4.0`, `MaxValue = 18.0`, `DefaultValue = 12.0`, `TargetTag = VREG_TempSp` (`REAL`).
   - `enable`: `DataType = Bool`, `DefaultValue = 0`, `TargetTag = VBIT_RemoteEn` (`BOOL`).
4. Nhập thử chuỗi `{"temp_sp": 25.0, "enable": true}` vào ô **Simulation** trong hộp thoại và xác nhận hệ thống báo lỗi `OutOfRange` ngay lập tức, bảo vệ tuyệt đối thanh ghi `VREG_TempSp`!
