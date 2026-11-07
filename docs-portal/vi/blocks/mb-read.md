# MB_READ — Đọc Thiết bị Modbus RTU Master

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-BLK-MBREAD</span>
  <span><strong>Nhóm khối hàm:</strong> Truyền thông Fieldbus &amp; Xuất Phần cứng</span>
  <span><strong>Phân lớp thực thi:</strong> Bộ quét Modbus RTU Master bất đồng bộ không gây nghẽn</span>
  <span><strong>Tiêu chuẩn:</strong> Modbus Application Protocol V1.1b3 &bull; IEC 61131-3</span>
</div>

## 1. Tổng quan & Ngữ nghĩa Thực thi

Khối hàm **`MB_READ`** (**Modbus RTU Master Read**) cho phép thiết bị điều khiển biên SynaptiX đóng vai trò **Modbus RTU Master** trên mạng RS-485, chủ động thu thập dữ liệu từ các thiết bị đo lường ngoại vi (Slave) như đồng hồ điện năng đa năng 3 pha, biến tần (VFD), cảm biến nhiệt ẩm/áp suất đường ống, đồng hồ lưu lượng siêu âm và đầu cân điện tử công nghiệp.

![Hộp thoại Cấu hình Khối đọc Modbus RTU Master MB_READ](/images/synaptix_modbus_read.png)

Các cam kết kỹ thuật cốt lõi trong thời gian thực:
- **Không Chặn Chu kỳ Quét Logic (Non-Blocking):** Toàn bộ quá trình phát khung tin RS-485, thu DMA UART và đếm thời gian chờ `TimeoutMs` chạy trên một tiến trình truyền thông độc lập. Cảm biến phản hồi chậm hoặc đứt cáp hoàn toàn không bao giờ làm trễ chu kỳ quét tất định `10 ms` của sơ đồ điều khiển.
- **Đọc Gộp Một Khung — Giải mã Đa Chân Động:** Chỉ với 1 lệnh đọc liên tiếp (`StartAddress`, `RegCount`), khối tự động bóc tách tối đa **4 chân ngõ ra động** với độ lệch thanh ghi riêng, kiểu dữ liệu (`BOOL`, `INT16`, `UINT16`, `INT32`, `UINT32`, `REAL`), thứ tự đảo byte/word 32-bit và hệ số nhân tuyến tính `Multiplier`.
- **Chốt Dữ liệu Nguyên tử:** Giá trị sau giải mã cùng xung báo **`Valid`** (kéo dài đúng 1 chu kỳ quét) chỉ được cập nhật đồng thời vào bộ nhớ Tag sau khi kiểm tra mã CRC-16 hợp lệ 100%.

---

## 2. Sơ đồ Chân & Hợp đồng Tín hiệu

Khối `MB_READ` tự động điều chỉnh chân ngõ vào bên trái theo chế độ `ReadMode`, giữ cho sơ đồ khối luôn tinh gọn và chuẩn công thái học:

### 2.1. Nhóm Chân Điều khiển & Chẩn đoán Cố định

| Tên chân | Chiều | Bản chất Tín hiệu | Kiểu dữ liệu | Điều kiện Hiển thị | Hợp đồng Chức năng |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **`EN`** | Ngõ vào *(Nội bộ)* | Mức logic | `BOOL` | Quản lý tự động ở chế độ `Cyclic` | Cấp quyền hoạt động cho bộ lập lịch quét ngầm khi khối được triển khai trên sơ đồ. |
| **`Trig`** | Ngõ vào | Sự kiện (`0 → 1`) | `BOOL` | **Chỉ hiển thị khi `ReadMode = OnTriggerRisingEdge`** *(ẩn hoàn toàn ở chế độ `Cyclic`)* | **Xung Kích hoạt Đọc:** Phát đúng 1 khung yêu cầu đọc Modbus RTU tại mỗi sườn lên (`0 → 1`). |
| **`Valid`** | Ngõ ra | Sự kiện (Xung 1 chu kỳ) | `BOOL` | Luôn hiển thị | **Xung Báo Dữ liệu Mới Hợp lệ:** Lên mức `1` trong đúng 1 chu kỳ quét (`10 ms`) ngay khi khung phản hồi vượt qua bước kiểm tra CRC-16 và cập nhật xong các chân động. |
| **`Error`** (`Err`) | Ngõ ra | Giá trị (Chốt trạng thái) | `BOOL` | Luôn hiển thị | **Cờ Báo Lỗi Bus:** Lên mức `1` khi quá hạn chờ, sai CRC-16, thiết bị trả về mã lỗi Exception hoặc khung tin hỏng. Tự động xóa về `0` ở lần đọc thành công kế tiếp. |
| **`ErrCode`** | Ngõ ra | Giá trị số | `UINT16` | Luôn hiển thị | **Mã Chẩn đoán Lỗi:** `0` = Bình thường, `1` = Quá thời gian chờ (Timeout), `2` = Lỗi CRC-16, `3` = Ngoại lệ Modbus, `4` = Khung tin biến dạng. |

### 2.2. Nhóm Chân Dữ liệu Ngõ ra Động (Danh sách `Pins`)

Mỗi dòng khai báo trong bảng **Dynamic Output Pins** sẽ sinh ra một chân ngõ ra mang tên tương ứng nằm bên dưới chân `ErrCode` (tối đa `4` chân động / khối), xuất trực tiếp giá trị đã giải mã và nhân tỷ lệ (`BOOL`, `INT32` hoặc `REAL`) đồng thời ghi thẳng vào biến đích `TargetTag`.

---

## 3. Tham số Cấu hình & Ràng buộc Inspector

Nhấp nút **`[ Cấu hình Modbus Read... ]`** trên thanh Inspector (hoặc nhấp đúp vào khối) để mở hộp thoại kỹ thuật 2 cột:

### 3.1. Tham số Cổng Bus, Địa chỉ & Định thời

| Tham số | Khóa Thuộc tính | Kiểu & Miền giá trị | Mặc định | Mô tả Kỹ thuật & Ràng buộc |
| :--- | :--- | :--- | :---: | :--- |
| **Cổng vật lý RS-485** | `PortIndex` | `0` (`RS-485 #1`) \| `1` (`RS-485 #2`) | `0` | Chọn cổng thu phát RS-485 bán song công đã cấu hình tốc độ Baud/Parity tại [Cấu hình RS-485 & Bản đồ SCADA Slave](/vi/tools/rs485-and-scada-slave). |
| **Địa chỉ Trạm (`Slave ID`)** | `SlaveId` | `Byte` (`1..247`) | `1` | Địa chỉ định danh duy nhất của thiết bị cảm biến ngoại vi trên tuyến cáp RS-485. |
| **Mã Hàm Đọc (`FC`)** | `FunctionCode` | `FC 01` \| `FC 02` \| `FC 03` \| `FC 04` | `FC 03` | `FC 01`: Đọc Coils (`0x`) · `FC 02`: Đọc Ngõ vào Rời rạc (`1x`) · `FC 03`: Đọc Holding Registers (`4x`) · `FC 04`: Đọc Input Registers (`3x`). |
| **Địa chỉ Bắt đầu** | `StartAddress` | `UINT16` (`0..65535`) | `0` | Địa chỉ thanh ghi/coil gốc tính từ `0` trong khung truyền PDU của thiết bị ngoại vi (`0x0000..0xFFFF`). |
| **Số lượng Thanh ghi** | `RegCount` | `UINT16` (`1..125`) | `2` | Tổng số thanh ghi 16-bit (hoặc coil) liên tiếp đọc về trong 1 gói tin. Phải bao phủ đủ `RegOffset + Độ rộng từ` của mọi chân động. |
| **Chế độ Đọc** | `ReadMode` | `Cyclic` \| `OnTriggerRisingEdge` | `Cyclic` | `Cyclic` tự động quét định kỳ theo `PollIntervalMs` (0 chân vào); `OnTriggerRisingEdge` hiển thị chân `Trig` để đọc theo sự kiện. |
| **Chu kỳ Quét** | `PollIntervalMs` | `UINT32` (`50..60000 ms`) | `500` | Khoảng cách thời gian giữa 2 lần đọc liên tiếp tính bằng mili-giây (chỉ hiệu lực khi `ReadMode = Cyclic`). |
| **Thời gian Chờ Phản hồi** | `TimeoutMs` | `UINT32` (`50..5000 ms`) | `300` | Ngưỡng thời gian chờ tối đa trước khi kết luận mất phản hồi và bật `Error = 1` (`ErrCode = 1`). |

### 3.2. Cấu trúc Giải mã Chân Ngõ ra Động (`ModbusPinBindingViewModel`)

| Cột Cấu hình | Khóa Thuộc tính | Tùy chọn / Phạm vi | Mặc định | Ghi chú Kỹ thuật |
| :--- | :--- | :--- | :---: | :--- |
| **Tên Chân (`Label`)** | `Label` | `String` | `"Val"` | Nhãn hiển thị của chân cắm bên cạnh phải khối (ví dụ: `Pressure`, `Voltage_Avg`). |
| **Độ lệch (`RegOffset`)** | `RegOffset` | `UINT16` (`0 .. RegCount - 1`) | `0` | Khoảng cách thanh ghi tính từ `StartAddress` (`+0`, `+1`, `+2`, ...). |
| **Kiểu Dữ liệu** | `DataType` | `BOOL`, `INT16`, `UINT16`, `INT32`, `UINT32`, `REAL` (`FLOAT32`) | `INT16` | `BOOL` / `INT16` / `UINT16` chiếm `1` thanh ghi; `INT32` / `UINT32` / `REAL` chiếm `2` thanh ghi liên tiếp. |
| **Thứ tự Byte/Word** | `Endianness` | `BigEndian_ABCD`, `MidBig_CDAB` *(Word-Swap)*, `ByteSwap_BADC`, `LittleEndian_DCBA` | `BigEndian_ABCD` | Áp dụng cho kiểu 32-bit (`INT32`, `UINT32`, `REAL`). Hơn 70% đồng hồ & biến tần công nghiệp (Schneider PM2200, INVT, Delta) dùng chuẩn **`MidBig_CDAB` (Word-Swap)**. |
| **Hệ số Nhân (`Multiplier`)** | `Multiplier` | `Float` (`≠ 0`) | `1.0` | Quy đổi dấu chấm tĩnh tuyến tính (`Giá trị = Thô × Multiplier`), ví dụ `0.1` đổi `253` thành `25.3 °C`. |
| **Biến Đích (`TargetTag`)** | `TargetTag` | `TagModel?` | `null` | Tự động đồng bộ hai chiều kiểu dữ liệu tương thích (`BOOL` ↔ `Bool`, `FLOAT32` ↔ `Real`). |

::: warning Cảnh báo Hệ số Nhân Số lẻ trên Biến `INT32`
Nếu nhập `Multiplier` là số thập phân (như `0.1` hoặc `0.01`) nhưng biến đích `TargetTag` đang mang kiểu số nguyên `INT32`, SynaptiX IDE sẽ tô viền cam cảnh báo (`⚠ Multiplier số lẻ sẽ làm tròn giá trị thành số nguyên trên tag INT32`). Hãy chuyển `TargetTag` sang biến kiểu **`REAL` (`FLOAT32`)** hoặc đặt `Multiplier = 1`.
:::

---

## 4. Cơ chế Đọc Ngầm Không Nghẽn (Non-Blocking Polling)

Khối `MB_READ` chạy trên tiến trình truyền thông ngầm, **hoàn toàn không làm chậm vòng quét điều khiển**:
- **Chế độ Định kỳ (`Cyclic`):** Tự động phát lệnh đọc theo chu kỳ `PollIntervalMs` (mặc định `500 ms`).
- **Chế độ Kích hoạt (`OnTriggerRisingEdge`):** Chỉ phát lệnh đọc khi có sườn lên ở chân `Trig`.
- **Cập nhật nguyên tử:** Khi nhận đủ khung tin và kiểm tra đúng CRC, dữ liệu được giải mã và cập nhật tức thì vào các chân ngõ ra kèm theo xung báo `Valid = 1` trong 1 chu kỳ quét.

---

## 5. Kiểm tra Hợp lệ, Xử lý Lỗi & Chẩn đoán Trực tuyến

### Bảng Tra cứu Mã Lỗi `ErrCode` & Khắc phục Sự cố Hiện trường

| `ErrCode` | Định danh Lỗi | Nguyên nhân Gốc rễ | Biện pháp Xử lý Khuyến nghị |
| :---: | :--- | :--- | :--- |
| **`0`** | `ERR_NONE` | Phản hồi hợp lệ, giải mã thành công. | Hệ thống hoạt động tốt; huy hiệu trên khối báo **`VALID`** hoặc **`N polls`**. |
| **`1`** | `ERR_TIMEOUT` | Hết thời gian `TimeoutMs` không nhận được phản hồi. | Kiểm tra nguồn `24 VDC` cấp cho cảm biến, kiểm tra đấu ngược cực `A(+)` / `B(-)`, đối chiếu `SlaveId`, tốc độ Baud/Parity, hoặc tăng `TimeoutMs` lên `500–1000 ms`. |
| **`2`** | `ERR_CRC` | Nhận được gói tin nhưng sai mã kiểm tra CRC-16. | Lắp điện trở đầu cuối `120 Ω` ở hai đầu tuyến cáp, dùng cáp xoắn đôi bọc chống nhiễu (STP) tách xa cáp động lực biến tần và nối đất vỏ chống nhiễu tại một điểm. |
| **`3`** | `ERR_EXCEPTION` | Thiết bị Slave trả mã ngoại lệ (`0x01` Sai mã hàm, `0x02` Sai địa chỉ). | Kiểm tra tài liệu cảm biến hỗ trợ `FC 03` hay `FC 04`, thử dịch địa chỉ `±1` (quy ước đếm từ 0 so với từ 1) và giảm `RegCount` nếu vượt vùng nhớ của cảm biến. |
| **`4`** | `ERR_MALFORMED` | Độ dài khung tin bị cụt hoặc lệch trường Byte Count. | Kiểm tra đầu cos đấu dây lỏng lẻo, sai cấu hình Stop-bit/Parity hoặc xung nhiễu đóng cắt trên đường truyền. |

- **Trình Mô phỏng Khung Hex Trực tiếp:** Ngay trong hộp thoại cấu hình `MB_READ`, cột bên phải hiển thị chuỗi Hex Request chuẩn xác (tự động tính sẵn 2 byte CRC-16) và cho phép dán chuỗi Hex phản hồi giả lập để kiểm chứng thứ tự byte `CDAB` / `ABCD` ngay tại bàn làm việc.

---

## 6. Ví dụ Đấu nối Công nghiệp Thực tế

### Giám sát Tủ Điện Động lực với Đồng hồ Đa năng Schneider PM2200 (`FC 03`, `CDAB` Float32)

**Bài toán:** Quét điện áp pha trung bình và tổng công suất tác dụng mỗi `1000 ms` qua cổng `RS-485 #1` từ đồng hồ đo điện năng (`SlaveId = 2`), kích hoạt cảnh báo thấp áp khi điện áp tụt dưới `195.0 V`, đồng thời đẩy dữ liệu tiêu thụ điện lên máy chủ MQTT.

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,5 L14,5 M14,5 L10,2 M14,5 L10,8 M14,11 L2,11 M2,11 L6,8 M2,11 L6,14" /></svg>
        <span>MB_READ</span>
      </div>
      <span class="synx-node-status is-live">● RS485 #1</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Slave 2 • FC 03</span>
        <span class="synx-node-alias">Đồng Hồ PM2200</span>
        <span class="synx-node-val">Quét Chu Kỳ 1000ms</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">192.4 V</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● LT 195.0V</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">&lt; 195.0 V</span>
        <span class="synx-node-alias">Giám Sát Thấp Áp</span>
        <span class="synx-node-val">Điện Áp Pha Sụt Giảm</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">TRUE</span>
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
        <span class="synx-node-tag">VFLAG0 = 1</span>
        <span class="synx-node-alias">Cờ Báo Động Thấp Áp</span>
        <span class="synx-node-val">Khóa Phụ Tải Động Lực</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

<div class="synx-node-chain" style="margin-top: 12px;">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,5 L14,5 M14,5 L10,2 M14,5 L10,8 M14,11 L2,11 M2,11 L6,8 M2,11 L6,14" /></svg>
        <span>MB_READ</span>
      </div>
      <span class="synx-node-status is-live">● OFFSET 2</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">VREG_ActiveKw</span>
        <span class="synx-node-alias">Tổng Công Suất P</span>
        <span class="synx-node-val">Float32 MidBig CDAB</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">42.8 kW</span>
  </div>

  <div class="synx-canvas-node is-trigger">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,14 L8,8 M5,10 A4,4 0 0 1 11,10 M3,7.5 A7,7 0 0 1 13,7.5 M1.5,5 A9.5,9.5 0 0 1 14.5,5" /></svg>
        <span>MQTT_PUB</span>
      </div>
      <span class="synx-node-status is-active">● TELEMETRY</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">synaptix/power</span>
        <span class="synx-node-alias">Đẩy Lên Cloud SCADA</span>
        <span class="synx-node-val">Chu Kỳ Giám Sát</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">BUS_OK</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-pass">● CLEAR</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">DO2 = 0</span>
        <span class="synx-node-alias">Đèn Bus RS-485 OK</span>
        <span class="synx-node-val">Không Báo Lỗi Truyền Thông</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

**Các bước cấu hình:**
1. Thiết lập cổng `RS-485 #1` ở tốc độ `9600 bps, 8E1, Master` trong màn hình [Cấu hình RS-485 & Bản đồ SCADA Slave](/vi/tools/rs485-and-scada-slave).
2. Kéo khối **`MB_READ`** ra vùng vẽ và cài đặt: `PortIndex = RS-485 #1`, `SlaveId = 2`, `FunctionCode = FC 03`, `StartAddress = 3028`, `RegCount = 4`, `ReadMode = Cyclic`, `PollIntervalMs = 1000`, `TimeoutMs = 400`.
3. Thêm 2 chân ngõ ra động trong bảng **Pins**:
   - `Voltage_V`: `RegOffset = 0`, `DataType = REAL`, `Endianness = MidBig_CDAB`, `Multiplier = 1.0`, `TargetTag = VREG_GridVolt` (`REAL`).
   - `Power_kW`: `RegOffset = 2`, `DataType = REAL`, `Endianness = MidBig_CDAB`, `Multiplier = 1.0`, `TargetTag = VREG_ActiveKw` (`REAL`).
4. Nối chân `Voltage_V` vào khối [`COMPARATOR`](/vi/blocks/comparator), chân `Power_kW` vào [`MQTT_PUB`](/vi/blocks/mqtt-pub) và chân `Error` ra đèn báo lỗi truyền thông tại tủ điện.
