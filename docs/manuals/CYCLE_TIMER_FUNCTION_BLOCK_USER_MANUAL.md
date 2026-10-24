# SỔ TAY HƯỚNG DẪN KHỐI CHỨC NĂNG CYCLE TIMER
## (REPEAT CYCLE / ASYMMETRICAL PERIODIC TIMER - BỘ ĐỊNH THỜI CHU KỲ TUẦN HOÀN)

---

**Mã tài liệu:** `MAN-SPLC-CYC-2026-V2.0`  
**Phiên bản áp dụng:** `SimplePLC / SynaptiX IDE v2.6.0+`  
**Tiêu chuẩn quốc tế tham chiếu:** 
- **IEC 61131-3:2013** (Programmable controllers — Programming languages — Timers & Function Blocks)
- **ISA-101.01-2015** (Human-Machine Interfaces for Process Automation Systems)
- **IEC 60073:2002** (Basic and safety principles for man-machine interface — Coding principles for indicators and actuators)

---

## 1. TỔNG QUAN & TRIẾT LÝ THIẾT KẾ CÔNG NGHIỆP

Trong các hệ thống tự động hóa công nghiệp và nông nghiệp thông minh, bài toán **lặp lại chu kỳ BẬT / TẮT tuần hoàn** là một trong những nhu cầu phổ biến nhất:
* **Chu kỳ ngắn (Short Pulse Oscillations):** Đèn tháp cảnh báo chớp nháy 1s ON / 1s OFF; còi chíp báo sự cố 200ms ON / 800ms OFF.
* **Chu kỳ dài (Long Period Machine Cycles):** Bơm tưới tiêu chạy 15 phút, nghỉ 45 phút; van xả đáy bồn khí nén mở 5 giây sau mỗi 30 phút; quạt hút thông gió trang trại chạy 3 phút, nghỉ 10 phút cả ngày lẫn đêm.

Khác với các phần mềm PLC truyền thống bắt buộc kỹ sư phải tự ghép 2 khối `TON` ngược chiều nhau hoặc viết mạch lật phức tạp, **SimplePLC Studio (SynaptiX IDE)** trang bị khối **`Cycle Timer`** (`REPEAT_CYCLE`) với giao diện trực quan, thân thiện cho mọi đối tượng kỹ thuật viên và kỹ sư vận hành.

```
       +------------------------------------+
       | #1 🕒 Cycle Timer         [CYCLE]  |
       |                                    |
       |   T_ON : 10 s                      |  (Thời gian BẬT)
       |   T_OFF: 30 s                      |  (Thời gian NGHỈ)
       |                                    |
In ───►| [>]       ● ON (4s / 10s)      [>] |───► Q (Đèn / Còi / Bơm)
       +------------------------------------+
```

---

## 2. BẢNG THAM SỐ & ĐẶC TẢ GIAO TIẾP

### 2.1. Các Cổng Kết Nối (Connectors / Pins)

| Tên Cổng | Loại | Hướng | Kiểu Dữ Liệu | Chức Năng & Hành Vi Fail-Safe |
| :---: | :---: | :---: | :---: | :--- |
| **`In`** | Event / Logic | Input | `Boolean` | **Tín hiệu kích hoạt / Cho phép chạy:**<br>• Khi `In = 1`: Khối bắt đầu chạy chu kỳ tuần hoàn ($T_{ON} \leftrightarrow T_{OFF}$).<br>• Khi `In = 0`: **Khóa an toàn ngắt tức thời (Fail-safe)**, ngõ ra $Q$ lập tức tắt về `0`, bộ đếm thời gian được xóa về `0`. |
| **`Q`** | State / Signal | Output | `Boolean` | **Ngõ ra điều khiển:**<br>• Phát tín hiệu mức `1` (ON) trong pha $T_{ON}$.<br>• Phát tín hiệu mức `0` (OFF) trong pha $T_{OFF}$.<br>• Nối trực tiếp tới các khối `Action` (`DO0..DOn`, `VFLAG`, v.v.). |

---

### 2.2. Các Tham Số Cấu Hình (Inspector Properties)

| Tham Số | Ký Hiệu | Đơn Vị Hỗ Trợ | Mặc Định | Mô Tả Ý Nghĩa |
| :--- | :---: | :---: | :---: | :--- |
| **Thời gian BẬT** | $T_{ON}$ | `ms`, `s`, `min`, `h` | `1 s` | Khoảng thời gian ngõ ra $Q$ duy trì ở mức cao `1` (ON) trong mỗi chu kỳ. |
| **Thời gian NGHỈ** | $T_{OFF}$ | `ms`, `s`, `min`, `h` | `1 s` | Khoảng thời gian ngõ ra $Q$ duy trì ở mức thấp `0` (OFF) trong mỗi chu kỳ. |
| **Pha Khởi Đầu** | `StartPhase` | Enum | `ON First` | **Lựa chọn trạng thái khi vừa kích hoạt chân `In`:**<br>• `Bật trước (ON First)`: Ngay khi $In = 1$, $Q$ bật `1` ngay trong $T_{ON}$, sau đó mới nghỉ $T_{OFF}$.<br>• `Nghỉ trước (OFF First)`: Khi $In = 1$, $Q$ giữ `0` trong $T_{OFF}$, sau đó mới bật $T_{ON}$. |
| **Nhãn Khối** | `CustomLabel` | Ký tự | Trống | Tên gợi nhớ hiển thị trên đỉnh khối (ví dụ: *"Bơm Luân Phiên 1"*, *"Đèn Tháp Báo Lỗi"*). |

---

## 3. GIẢN ĐỒ THỜI GIAN (TIMING DIAGRAMS)

### 3.1. Chế Độ "Bật Trước" (Start With ON - Mặc Định)
Phù hợp 90% bài toán thực tế: Cần kích hoạt thiết bị ngay lập tức khi có lệnh (bơm chạy ngay, đèn còi hú ngay).

```text
Tín hiệu In : ───┌───────────────────────────────────────────────┐──────
                 │                                               │
Ngõ ra Q    : ───┌───────────┐           ┌───────────┐           └───0──
                 │   T_ON    │   T_OFF   │   T_ON    │   T_OFF   │ (Tắt tức thì
              ───┘           └───────────┘           └───────────┘  khi In=0)
```

---

### 3.2. Chế Độ "Nghỉ Trước" (Start With OFF)
Phù hợp bài toán đo đạc định kỳ hoặc xả khí ngưng tụ: Chờ đủ thời gian nghỉ mới thực hiện thao tác xả ngắn hạn.

```text
Tín hiệu In : ───┌───────────────────────────────────────────────┐──────
                 │                                               │
Ngõ ra Q    : ───│           ┌───────────┐           ┌───────────└───0──
                 │   T_OFF   │   T_ON    │   T_OFF   │   T_ON    │
              ───└───────────┘           └───────────┘           └──────
```

---

## 4. BA KỊCH BẢN ỨNG DỤNG THỰC TẾ ĐIỂN HÌNH

### Kịch Bản 1: Đèn Tháp Cảnh Báo Nhấp Nháy (Tower Light Flasher)
* **Yêu cầu:** Khi hệ thống có sự cố (`DI0_ALARM = 1`), đèn tháp đỏ `DO0_RED` nhấp nháy chu kỳ 1 giây (0.5s sáng / 0.5s tắt). Khi hết lỗi (`DI0_ALARM = 0`), đèn tắt hoàn toàn.
* **Cài đặt:**
  * Mode: `CYCLE`
  * $T_{ON} = 500 \text{ ms}$
  * $T_{OFF} = 500 \text{ ms}$
  * StartPhase: `ON First`
  * Nối chân: `DI0` $\rightarrow$ `Cycle Timer.In`, `Cycle Timer.Q` $\rightarrow$ `DO0`.

---

### Kịch Bản 2: Bơm Nông Nghiệp / Sục Khí Chạy Luân Phiên
* **Yêu cầu:** Bơm sục khí ao tôm chạy 20 phút, nghỉ 40 phút rồi tự động lặp lại liên tục khi công tắc tự động `DI1_AUTO` bật.
* **Cài đặt:**
  * Mode: `CYCLE`
  * $T_{ON} = 20 \text{ min}$
  * $T_{OFF} = 40 \text{ min}$
  * StartPhase: `ON First`
  * Nối chân: `DI1` $\rightarrow$ `Cycle Timer.In`, `Cycle Timer.Q` $\rightarrow$ Khởi động từ bơm `DO1`.

---

### Kịch Bản 3: Van Xả Nước Ngưng Máy Nén Khí (Moisture Drain Valve)
* **Yêu cầu:** Máy nén khí hoạt động cần xả nước ngưng đáy bình: Cứ mỗi 30 phút, mở van xả trong đúng 5 giây rồi đóng lại.
* **Cài đặt:**
  * Mode: `CYCLE`
  * $T_{ON} = 5 \text{ s}$
  * $T_{OFF} = 30 \text{ min}$
  * StartPhase: `OFF First` (Nghỉ 30 phút trước, sau đó mới xả 5s).
  * Nối chân: `DI2_COMP_RUN` $\rightarrow$ `Cycle Timer.In`, `Cycle Timer.Q` $\rightarrow$ Van điện từ xả nước `DO2`.

---

## 5. CƠ CHẾ AN TOÀN DƯỚI TẦNG COMPILER & FIRMWARE

1. **Khóa liên động Controlled Multi-Writer Group:**
   * Dưới tầng `RuleCompiler`, khối Cycle Timer được tự động mở rộng thành 4 luật nguyên thủy kết hợp cơ chế kiểm soát `Guard`.
   * Bộ thẩm định `WriteConflictValidator` công nhận mã định danh `CYCLE`, đảm bảo các luật chuyển trạng thái $Q = 1$ và $Q = 0$ không bao giờ xung đột hay ghi đè mù quáng.
2. **Cơ chế Ngắt An Toàn Tuyệt Đối (Zero Latency Shutdown):**
   * Luật $R_3$ (`CYCLE_STOP`) luôn theo dõi sườn xuống của chân `In`. Ngay khi `In` về `0`, ngõ ra $Q$ được ép về mức `0` trong đúng chu kỳ quét hiện tại, triệt tiêu nguy cơ thiết bị chạy quá trớn ngoài ý muốn.
3. **Mô phỏng Trực quan (Live Telemetry):**
   * Trong lúc chạy mô phỏng hoặc kết nối thiết bị thật, Status Pill hiển thị trạng thái động tức thời: `● ON (4s / 10s)` hoặc `● OFF (12s / 30s)`.
