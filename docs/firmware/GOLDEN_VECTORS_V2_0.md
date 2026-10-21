# BẢNG VECTOR THAM CHẤU CHUẨN (GOLDEN VECTORS) GIAO THỨC V2.0

> **Tài liệu bàn giao cho MCU Firmware Team (`board_dev_gw`)**  
> **Cập nhật:** Ngày 20/10/2026  
> **Bộ kiểm thử tự động xác minh:** `SimplePLC.Protocol.Tests.GoldenVectorsAcceptanceTests` (100% Passed)

---

## 1. TỔNG QUAN

Tài liệu này cung cấp các chuỗi Hex Dump bộ nhớ thanh ghi Modbus Holding Registers chuẩn (Golden Vectors) được sinh từ Studio Codec v2.0 để team Firmware đối soát bit/byte offset, word order và endianness khi nạp cấu hình qua Modbus RTU / TCP.

Mỗi thanh ghi Modbus Holding Register là **16-bit (2 bytes)**, truyền theo Big-Endian (High Byte trước, Low Byte sau).

---

## 2. GOLDEN VECTOR 1: MB_READ (MODBUS MASTER READ BLOCK)

- **Vị trí thanh ghi:** `0x1000` (Block 0, 64 thanh ghi = 128 bytes)
- **Kịch bản cấu hình:**
  - Slave ID: `2`, Function Code: `0x03` (Read Holding Registers), Địa chỉ bắt đầu: `0x0100`, Đọc: `2` thanh ghi
  - Poll Interval: `500 ms` (`0x000001F4`), Timeout: `1000 ms` (`0x000003E8`)
  - Valid Tag: `16` (`0x0010`), Error Tag: `17` (`0x0011`), ErrCode Tag: `18` (`0x0012`)
  - 1 Pin ngõ vào: Pin 0 (`TargetTag=32`, `RegOffset=0`, `DataType=6 (Real)`, `Endianness=0 (BigEndian ABCD)`, `Multiplier=1.0f (0x3F800000)`, `Label="Temp"`)

### Hex Dump (64 thanh ghi / 128 bytes):

```text
[+00..+07]: 0001 0302 0100 0002 0000 01F4 0000 03E8 
[+08..+15]: 0010 0011 0012 0001 0000 0000 0000 0000 
[+16..+23]: 0020 0000 0006 0000 3F80 0000 5465 6D70 
[+24..+31]: 0000 0000 0000 0000 FFFF 0000 0000 0000 
[+32..+39]: 0000 0000 0000 0000 0000 0000 0000 0000 
[+40..+47]: FFFF 0000 0000 0000 0000 0000 0000 0000 
[+48..+55]: 0000 0000 0000 0000 FFFF 0000 0000 0000 
[+56..+63]: 0000 0000 0000 0000 0000 0000 0000 0000 
```

### Chi tiết giải mã từng thanh ghi:
- `+0`: `0x0001` - Control Flags (`Bit 0 = 1`: Enabled)
- `+1`: `0x0302` - High Byte: `0x03` (FC), Low Byte: `0x02` (Slave ID)
- `+2`: `0x0100` - Start Address = 256
- `+3`: `0x0002` - Reg Count = 2
- `+4..+5`: `0x0000 0x01F4` - Poll Interval = 500 ms (High Word first)
- `+6..+7`: `0x0000 0x03E8` - Timeout = 1000 ms (High Word first)
- `+8`: `0x0010` - Valid Tag Index = 16
- `+9`: `0x0011` - Error Tag Index = 17
- `+10`: `0x0012` - ErrCode Tag Index = 18
- `+11`: `0x0001` - Pin Count = 1
- `+12..+15`: `0x0000 ...` - Reserved
- `+16..+27` (Pin 0):
  - `+16`: `0x0020` - Target Tag Index = 32
  - `+17`: `0x0000` - Register Offset = 0
  - `+18`: `0x0006` - Data Type = 6 (REAL / Float32)
  - `+19`: `0x0000` - Endianness = 0 (Big-Endian ABCD)
  - `+20..+21`: `0x3F80 0x0000` - Multiplier = 1.0f (IEEE-754 High Word `0x3F80`, Low Word `0x0000`)
  - `+22..+27`: `0x5465 0x6D70 0x0000 0x0000 0x0000 0x0000` - Packed ASCII `"Temp\0\0..."`

---

## 3. GOLDEN VECTOR 2: MB_WRITE (MODBUS MASTER WRITE BLOCK)

- **Vị trí thanh ghi:** `0x1100` (Block 0, 64 thanh ghi = 128 bytes)

### 3.1 Chế độ DirectRegisters:
- Slave ID: `3`, FC: `0x10` (Write Multiple Registers), Addr: `0x0050`, PointCount: `2`
- Trigger: `0` (`OnTriggerRisingEdge`), Trig Tag: `5` (`0x0005`)
- Done/Busy/Error/ErrCode Tags: Không dùng (`0xFFFF`)
- Pin 0: `TargetTag=21`, `Offset=0`, `DataType=2 (Int16)`, `Multiplier=1.0f`, `Label="OutSp"`

```text
[+00..+07]: 0001 1003 0050 0002 0000 0120 0000 01F4 
[+08..+15]: 0000 012C 0000 0005 FFFF FFFF FFFF FFFF 
[+16..+23]: 0015 0000 0002 0000 3F80 0000 4F75 7453 
[+24..+31]: 7000 0000 0000 0000 FFFF 0000 0000 0000 
[+32..+39]: 0000 0000 0000 0000 0000 0000 0000 0000 
[+40..+47]: FFFF 0000 0000 0000 0000 0000 0000 0000 
[+48..+55]: 0000 0000 0000 0000 FFFF 0000 0000 0000 
[+56..+63]: 0000 0000 0000 0000 0000 0000 0000 0000 
```

### 3.2 Chế độ ChannelIndexed (Kênh điều khiển mở rộng):
- Slave ID: `1`, FC: `0x10`, BaseAddressCommand: `0x0200`, ChannelCount: `8`
- BaseAddressParam: `0x0120` (`High 0x0000, Low 0x0120`), CyclicInterval: `250 ms` (`0x00FA`)
- TriggerMode: `2` (`Cyclic`), Timeout: `1000 ms` (`0x03E8`), ControlFlags: `0x0005` (Bit 0 Enabled + Bit 2 WriteMode ChannelIndexed)

```text
[+00..+07]: 0005 1001 0200 0008 0000 0120 0000 00FA 
[+08..+15]: 0000 03E8 0002 FFFF FFFF FFFF FFFF FFFF 
[+16..+23]: FFFF FFFF FFFF 0000 0000 0000 0000 0000 
[+24..+31]: 0000 0000 0000 0000 0000 0000 0000 0000 
[+32..+39]: 0000 0000 0000 0000 0000 0000 0000 0000 
[+40..+47]: 0000 0000 0000 0000 0000 0000 0000 0000 
[+48..+55]: 0000 0000 0000 0000 0000 0000 0000 0000 
[+56..+63]: 0000 0000 0000 0000 0000 0000 0000 0000 
```

---

## 4. GOLDEN VECTOR 3: MQTT SUB (MQTT SUBSCRIPTION BLOCK)

- **Vị trí thanh ghi:** `0x0D80` (Block 0, 64 thanh ghi = 128 bytes)
- **Kịch bản cấu hình:**
  - Topic: `"plc/commands"`, QoS: `1`, Ignore Retained on Boot: `true`, FailSafe Action: `ResetToDefault` (`Code 2`)
  - Timeout: `5000 ms` (`0x00001388`), Pin Count: `2`
  - Control flags: `0x0053` (`Bit 0: Enabled (1)`, `Bit 1: QoS 1 (2)`, `Bit 4: IgnoreRetain (16)`, `Bit 5..6: FailSafe ResetToDefault (64)`)
  - Pin 0: Target = `Tag 1 (VFLAG1)`, DataType = `1 (Bool)`, Default = `0`, Key = `"start"`
  - Pin 1: Target = `Tag 37 (VREG5)`, DataType = `3 (Float)`, Default = `1500.5f` (`0x44BB9000`), Key = `"speed"`

### Hex Dump (64 thanh ghi / 128 bytes):

```text
[+00..+07]: 0053 0000 0000 1388 0002 0000 0000 0000 
[+08..+15]: 706C 632F 636F 6D6D 616E 6473 0000 0000 
[+16..+23]: 0000 0000 0000 0000 0000 0000 0000 0000 
[+24..+31]: 0001 0001 0000 0000 7374 6172 7400 0000 
[+32..+39]: 0000 0000 0025 0003 44BB 9000 7370 6565 
[+40..+47]: 6400 0000 0000 0000 FFFF 0000 0000 0000 
[+48..+55]: 0000 0000 0000 0000 0000 0000 FFFF 0000 
[+56..+63]: 0000 0000 0000 0000 0000 0000 0000 0000 
```

---

## 5. GOLDEN VECTOR 4: DIRECT_WRITE / PULSE_OUT (ACTUATOR PWM / STEPPER)

- **Vị trí thanh ghi:** `0x1200` (Block 0, 16 thanh ghi = 32 bytes)
- **Kịch bản cấu hình:**
  - TargetPin: `0 (DO0)`, Mode: `1 (PwmDuty)`, TriggerMode: `1 (Continuous)`, Flags: `0x01 (Active-High)`
  - PulseWidth: `50 us` (`0x0032`), **Tần số PWM: `1000 Hz`** (`0x000003E8` - **Big-Endian: High Word Reg 4 = `0x0000`, Low Word Reg 5 = `0x03E8`**)
  - Duty Min: `0%`, Duty Max: `100%` (`Reg 6 = 0x6400`), Channel Tag: `20` (`0x0014`), Param Tag: Không dùng (`0xFFFF`)
  - Default Color RGB: `0x00FF8800` (`Reg 9 = 0x00FF, Reg 10 = 0x8800`)

### Hex Dump (16 thanh ghi / 32 bytes):

```text
[+00..+07]: 0100 0001 0101 0032 0000 03E8 6400 0014 
[+08..+15]: FFFF 00FF 8800 0000 0000 0000 0000 0000 
```

---

## 6. GHI CHÚ KỸ THUẬT QUAN TRỌNG CHO FIRMWARE

1. **Word Order cho số 32-bit (uint32 / float32):**
   - Tất cả các tham số cấu hình 32-bit (Timeout, Interval, Frequency, Multiplier) được đóng gói theo **Big-Endian Word Order**:
     - `Reg[0]`: High Word (`bits 31..16`)
     - `Reg[1]`: Low Word (`bits 15..0`)
2. **Ký tự chuỗi (ASCII Packing):**
   - `Reg = (Byte[0] << 8) | Byte[1]`.
   - Kết thúc chuỗi bằng byte `0x00`.
3. **Thanh ghi không cấu hình (Unbound / Unused):**
   - Chỉ số Tag không cấu hình luôn mang giá trị `0xFFFF`.
   - Các pin chưa sử dụng có `TargetTagIndex = 0xFFFF`.
