# SPLC_SPEC_MODBUS_TCP_005: Giao Thức Modbus TCP & Tích Hợp Mạng IP

**Mã tài liệu:** SPLC_SPEC_MODBUS_TCP_005_ICD  
**Phiên bản:** 1.0  
**Ngày ban hành:** 20/10/2026  
**Trạng thái:** Chính thức (Approved / Implemented)  
**Tác giả:** Senior System Architect & Protocol Team  

---

## 1. Tổng Quan & Mục Tiêu Kỹ Thuật

Tài liệu này đặc tả quy chuẩn giao tiếp **Modbus TCP** (chuẩn IEC 61158 / Modbus IDA) trên dòng vi điều khiển công nghiệp SimplePLC (STM32F4/G4/H7 hoặc module Wi-Fi/Ethernet ST67/W5500/LAN8720).

### 1.1 Mục Tiêu
- Cho phép SimplePLC Studio, hệ thống SCADA/HMI, hoặc trạm Gateway giao tiếp với PLC qua hạ tầng mạng IP (Ethernet RJ45 hoặc Wi-Fi 802.11 b/g/n) thay thế hoặc song song với đường cáp truyền thống RS-485 / Modbus RTU.
- Loại bỏ hoàn toàn sự phụ thuộc vào cổng COM vật lý hoặc cáp chuyển USB-to-RS485.
- Duy trì 100% tính tương thích ngữ nghĩa với bộ thanh ghi (Register Map V2.0), quy trình nạp luật 3 pha (Staging -> CRC Check -> Atomic Commit), và cơ chế chẩn đoán (Diagnostic Control Lease Watchdog).

---

## 2. Phần Cứng & Cờ Năng Lực (Capabilities)

### 2.1 Cờ Nhận Diện Năng Lực Phần Cứng (Hardware Capabilities)
Firmware báo hiệu khả năng hỗ trợ Modbus TCP qua thanh ghi `0x0030` (`SPLC_HardwareCapabilities`):

| Bit | Hằng số | Giá trị | Mô tả |
| :--- | :--- | :--- | :--- |
| **Bit 10** | `SPLC_CAP_MODBUS_TCP` | `0x0400` | Phần cứng hỗ trợ Modbus TCP Server lắng nghe trên Port 502 (Ethernet/Wi-Fi) |

### 2.2 Cấu hình Cổng Mặc Định
- **Cổng TCP chuẩn (IANA Modbus):** `502`
- **Giao thức tầng truyền vận:** TCP/IP (Stream-oriented, tin cậy, tự động retransmit và ACK)
- **Tùy chọn Socket:** `TCP_NODELAY = 1` (Tắt thuật toán Nagle để đạt độ trễ phản hồi < 1ms)
- **Timeout giao dịch khuyến nghị:** 3000ms

---

## 3. Cấu Trúc Khung MBAP (Modbus Application Protocol)

Không giống như Modbus RTU sử dụng khoảng lặng thời gian (3.5 ký tự t3.5) và mã kiểm tra CRC-16 ở cuối khung, Modbus TCP sử dụng tiền tố **MBAP Header 7 bytes** ở đầu mỗi gói tin:

```
+---------------+-------------------+---------------+---------------+
| MBAP Header   | Function Code     | Data Field    | (Không có CRC)|
| (7 bytes)     | (1 byte)          | (N bytes)     |               |
+---------------+-------------------+---------------+---------------+
```

### 3.1 Chi Tiết MBAP Header

| Offset | Trường | Kích thước | Thứ tự byte | Mô tả |
| :--- | :--- | :--- | :--- | :--- |
| `0..1` | **Transaction Identifier (TxID)** | 2 bytes | Big-Endian (BE) | Mã định danh phiên giao dịch do Client sinh ra. Server bắt buộc phải phản hồi chính xác TxID này. |
| `2..3` | **Protocol Identifier** | 2 bytes | Big-Endian (BE) | Luôn bằng `0x0000` (Modbus Protocol). Các giá trị khác coi là bất hợp lệ. |
| `4..5` | **Length Field** | 2 bytes | Big-Endian (BE) | Số lượng byte còn lại tiếp theo (bằng `1 + PDU Length` = Unit ID + FC + Data). |
| `6` | **Unit Identifier (Slave ID)** | 1 byte | Byte | Mã trạm Modbus (thường là `1` cho SimplePLC nội tại; hoặc ID của thiết bị hạ tầng nếu làm Gateway). |

---

## 4. Các Mã Lệnh Hỗ Trợ (Supported Function Codes)

SimplePLC hỗ trợ đầy đủ 3 Function Codes cốt lõi trên Modbus TCP:

### 4.1 FC 0x03 (03): Đọc Thanh Ghi Giữ Chỗ (Read Holding Registers)
- **Request PDU:** `[0x03][Addr Hi][Addr Lo][Count Hi][Count Lo]` (5 bytes PDU, MBAP Length = 6)
- **Response PDU:** `[0x03][Byte Count (N*2)][Reg 0 Hi][Reg 0 Lo]...`
- **Giới hạn:** Tối đa 125 thanh ghi mỗi phiên đọc (250 bytes).

### 4.2 FC 0x06 (06): Ghi Đơn Thanh Ghi (Write Single Register)
- **Request PDU:** `[0x06][Addr Hi][Addr Lo][Value Hi][Value Lo]` (5 bytes PDU, MBAP Length = 6)
- **Response PDU (Echo):** `[0x06][Addr Hi][Addr Lo][Value Hi][Value Lo]`

### 4.3 FC 0x10 (16): Ghi Nhiều Thanh Ghi (Write Multiple Registers)
- **Request PDU:** `[0x10][Addr Hi][Addr Lo][Count Hi][Count Lo][Byte Count][Data...]`
- **Response PDU:** `[0x10][Addr Hi][Addr Lo][Count Hi][Count Lo]` (5 bytes PDU, MBAP Length = 6)
- **Giới hạn:** Tối đa 123 thanh ghi mỗi phiên ghi (246 bytes).

---

## 5. Khung Xử Lý Lỗi Ngoại Lệ (Exception Responses)

Khi xảy ra lỗi (ngoài dải địa chỉ, tham số sai, hoặc thiết bị bận), Server trả về khung Exception:
- **PDU:** `[FC | 0x80] [Exception Code]`
  - `0x01` (`IllegalFunction`): Mã lệnh không được hỗ trợ.
  - `0x02` (`IllegalDataAddress`): Địa chỉ thanh ghi không tồn tại hoặc không có quyền truy cập.
  - `0x03` (`IllegalDataValue`): Kích thước chunk vượt quá giới hạn hoặc sai độ dài.
  - `0x04` (`SlaveDeviceFailure`): Lỗi phần cứng nội tại MCU.

---

## 6. Coexistence & Tính Toàn Vẹn Hệ Thống

1. **Truy cập đồng thời:** Cả Modbus RTU (Serial/RS-485) và Modbus TCP (Ethernet/Wi-Fi) cùng chia sẻ một không gian thanh ghi RAM và Flash duy nhất của MCU.
2. **Quy trình Staging & Commit:** Toàn bộ bảng Rule nạp qua TCP (tại địa chỉ `0x9010`) được bảo vệ bằng CRC-16 tính theo payload dữ liệu, kiểm tra nguyên tử tại `0xA000 (0xA5A5)` hoàn toàn tương đương như nạp qua RTU.
3. **Studio UI Integration:** SimplePLC Studio cho phép kỹ sư vận hành chuyển đổi chế độ kết nối giữa **RTU/USB** (chọn COM port) và **TCP** (nhập IP Host : Port) chỉ bằng một thao tác click chuột trên thanh Toolbar.
