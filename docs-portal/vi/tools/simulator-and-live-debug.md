# Mô phỏng Ngoại tuyến (`F5`) & Gỡ lỗi Trực tuyến (`F8`)

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-TOOL-004</span>
  <span><strong>Phím tắt:</strong> <code>F5</code> (Mô phỏng Offline) &bull; <code>F8</code> (Gỡ lỗi Trực tuyến) &bull; <code>Ctrl+7</code> (Live Watch)</span>
  <span><strong>Nhịp Thực thi:</strong> Chu kỳ Quét Ảo Tất định &bull; Đồ họa Trực tiếp 20 FPS (<code>50 ms</code>)</span>
  <span><strong>Tiêu chuẩn:</strong> Kiểm chứng Trước Triển khai theo IEC 61508-3</span>
</div>

Mọi sai sót logic khi nạp thẳng xuống tủ điện máy móc đang vận hành đều tiềm ẩn nguy cơ hỏng cơ cấu chấp hành, nhảy aptomat hoặc dừng chuyền sản xuất tốn kém. **SynaptiX IDE** loại bỏ hoàn toàn rủi ro "nạp mù" thông qua quy trình kiểm chứng 2 giai đoạn khép kín:
1. **Bộ Mô phỏng Chu kỳ Chính xác Ngoại tuyến (`F5`):** Kiểm thử 100% luật điều khiển, bộ định thời, bộ đếm, hàm quy đổi tuyến tính và bộ giải mã JSON ngay trên máy tính **mà không cần bất kỳ bo mạch phần cứng nào**.
2. **Gỡ lỗi Sơ đồ Trực tuyến (`F8`):** Kết nối trực tiếp vào thiết bị SynaptiX thực tế (`F4`) và phủ lớp trạng thái dây dẫn mang điện, giá trị biến, bộ đếm ngược thời gian thực tốc độ 20 FPS cùng cảnh báo đứt cáp `[STALE]` ngay trên Sơ đồ Logic.

---

## Phần 1: Bộ Mô phỏng Chu kỳ Chính xác Ngoại tuyến (`F5`)

Nhấn phím `F5` (hoặc bấm nút **Chạy Mô phỏng** trên thanh công cụ) để mở ngăn **Offline Simulator**. Bên dưới hậu trường, bộ mô phỏng khởi chạy cùng một nhân thực thi tất định (`RuntimeEngine` kết hợp đồng hồ ảo `SimulatedClock`) tương đồng hoàn toàn với chu trình quét trên vi điều khiển biên SynaptiX.

![Giao diện Bộ Mô phỏng Chu kỳ Chính xác Ngoại tuyến SynaptiX](/images/synaptix_simulator.png)

### 1.1 Khóa An toàn Trạng thái Biên dịch (`Pre-Flight Compile Gate`)

Tuân thủ nghiêm ngặt học thuyết **Chân lý Thị giác (Visual Truth)**, bộ mô phỏng từ chối chạy trên các sơ đồ chưa đồng bộ hoặc đang có lỗi:
- Trước khi chạy chu kỳ quét, Simulator kiểm tra điều kiện `CompileState == Valid`.
- Nếu bạn đang chạy mô phỏng mà thay đổi tham số khối, kéo lại dây nối hoặc làm phát sinh lỗi biên dịch, Simulator sẽ **tự động tạm dừng ngay lập tức** kèm thông báo cảnh báo (*"Tạm dừng mô phỏng: Logic đã thay đổi hoặc có lỗi. Vui lòng biên dịch thành công (`F7`) để tiếp tục."*), đảm bảo kết quả mô phỏng không bao giờ sai khác so với sơ đồ trên màn hình.

### 1.2 Bảng Điều khiển Tín hiệu Ảo & Đồng hồ Mô phỏng

| Công cụ Mô phỏng | Chức năng & Ứng dụng Kiểm thử Kỹ thuật |
| :--- | :--- |
| **Chạy / Tạm dừng (`ToggleEngine`)** | Khởi chạy hoặc đóng băng chu kỳ quét liên tục (mỗi nhịp cơ sở `100 ms` nhân với hệ số tốc độ `SimSpeed`). |
| **Quét Từng Bước Đơn (`StepScan`)** | Dừng chế độ chạy tự do và nhích đồng hồ ảo `SimulatedClock` tiến đúng **1 chu kỳ quét duy nhất** (`Scan #N -> Scan #N+1`). Cực kỳ đắc lực để soi các mạch bắt sườn xung (`Rising` / `Falling`) và thứ tự ưu tiên chốt giữ! |
| **Công tắc Ngõ vào Số (`DI0..DI7`)** | Các nút gạt nhị phân `[ 0 ]` / `[ 1 ]` giả lập công tắc hành trình, nút dừng khẩn cấp E-Stop, cảm biến quang và ghi ngay sự kiện đóng/ngắt vào luồng **Event Stream**. |
| **Thanh trượt & Ô nhập Tương tự (`AI0..AI3`, `VREG`)** | Bơm chính xác dòng/áp thô hoặc giá trị kỹ thuật (VD: `4.0 mA`, `12.0 mA`, `20.0 mA` hoặc giá trị vọt lố đứt dây) để kiểm tra kẹp biên [`SCALE`](/vi/blocks/scale) và vùng trễ chống rung [`COMPARATOR`](/vi/blocks/comparator). |
| **Đồng hồ Thực Ảo (`SimClock` & `SimSpeed`)** | Tua nhanh đồng hồ hệ thống tới mốc giờ bất kỳ (VD: nhảy tới `08:30` hoặc `22:00`) và tăng tốc thời gian (`1x`, `5x`, `10x`) để kiểm chứng lịch bật/tắt theo ca hoặc các bộ hẹn giờ [`TON`](/vi/blocks/ton) / [`CYCLE_TIMER`](/vi/blocks/cycle-timer) dài nhiều giờ chỉ trong vài giây. |
| **Khung Bơm Gói tin JSON ([`MQTT_SUB`](/vi/blocks/mqtt-sub))** | Dán trực tiếp chuỗi JSON điều khiển từ đám mây (VD: `{"grid": 5, "color": 1}` hoặc chuỗi JSON lỗi/thiếu trường) ngay trên khối `MQTT_SUB` để kiểm tra cơ chế giải mã **Nguyên tử Toàn vẹn (Atomic All-or-Nothing)** cùng xung báo `Valid` / `Err`! |

### 1.3 Bảng Trạng thái Luật & Dòng Sự kiện Thực thi

Trong suốt quá trình chạy liên tục hoặc nhích từng bước `StepScan`, cửa sổ Simulator cập nhật đồng bộ hai luồng chẩn đoán:
- **Ma trận Đánh giá Luật (`RuleStatusList`):** Hiển thị trạng thái tức thời của từng mắt xích `Trigger`, `Condition`, `Guard` và `Action` cho mọi luật đã biên dịch (bật bộ lọc `FilterActiveOnly` để chỉ lọc riêng những luật đang tác động).
- **Luồng Nhật ký Sự kiện (`EventStream` & `ExecutionTraceFeed`):** Lưu vết theo dấu thời gian thực tế (`[HH:mm:ss.fff]`) mọi thao tác gạt `DI`, thời điểm luật kích hoạt, thời điểm bộ định thời chạm ngưỡng và bộ đếm đủ sản lượng.

---

## Phần 2: Gỡ lỗi Trực tuyến Trên Sơ đồ Khối (`F8` — Online Live Debug)

Sau khi mạch logic đã vượt qua kiểm thử ngoại tuyến và được nạp nguyên tử (`Ctrl+F5`) xuống bo mạch SynaptiX thực tế đang kết nối qua **USB CDC** hoặc **Modbus TCP (`F4`)**, hãy mở [Sơ đồ Logic (`Ctrl+2`)](/vi/workspaces/logic-graph-tab) và nhấn phím `F8` để bật chế độ **Online Live Debug**.

::: tip Nhận diện Trạng thái Trực tiếp trên Sơ đồ khi Bật F8
Khi nhấn **F8**, Sơ đồ Logic (`Logic Graph`) chuyển thành màn hình giám sát thời gian thực:
- 🟢 **Dây dẫn sáng xanh:** Đang có tín hiệu điện kích hoạt (`TRUE` / mức `1`).
- ⚫ **Dây xám:** Không có tín hiệu kích hoạt (`FALSE` / mức `0`).
- 🔢 **Huy hiệu số chân khối:** Hiển thị trực tiếp giá trị đo thực tế (`INT32`, `REAL`, đơn vị đo).
- ⏱️ **Tiến độ đếm:** Hiển thị thời gian thực trôi qua (`ET` / `PT`) của các bộ hẹn giờ `TON`, `TOF`, `TP`.
- ⚠️ **Huy hiệu `[STALE]`:** Cảnh báo đứt cáp hoặc mất tín hiệu cảm biến RS-485 / MQTT.
:::

### 2.1 Các Lớp phủ Thị giác Thời gian thực Trên Canvas

Tuân thủ tiêu chuẩn nhận thức tình huống công nghiệp **ANSI/ISA-101**, chế độ `F8` biến bản vẽ FBD tĩnh thành màn hình giám sát trực quan mượt mà ở tốc độ **20 FPS (`50 ms`/khung hình)** mà không gây giật lag giao diện:

1. **Tô sáng Dây dẫn Đang Mang Tín hiệu (`Wire Energizing`):**
   - Các đường dây logic `BOOL` đang mang mức `1` (`TRUE`) phát sáng màu xanh vận hành chuẩn xác (`SuccessBrush`), trong khi dây mức `0` (`FALSE`) giữ tông xám trung tính — giúp kỹ sư nhìn xuyên suốt chuỗi khóa liên động (`INPUT -> TRIGGER -> COMPARATOR -> GUARD -> ACTION`) chỉ trong tích tắc.
2. **Huy hiệu Giá trị Thực tế Ngay tại Chân Khối:**
   - Từng khối [`INPUT`](/vi/blocks/input), [`SCALE`](/vi/blocks/scale), [`MB_READ`](/vi/blocks/mb-read), [`CTU`](/vi/blocks/ctu)/[`CTD`](/vi/blocks/ctd) và [`MQTT_PUB`](/vi/blocks/mqtt-pub) hiển thị trực tiếp giá trị trong RAM thiết bị (`INT32` hoặc số thực `REAL` kèm đơn vị kỹ thuật).
3. **Đếm Thời gian & Tiến độ Bộ đếm Trực tiếp:**
   - Các khối [`TON`](/vi/blocks/ton), [`TOF`](/vi/blocks/tof), [`TP`](/vi/blocks/tp) và [`CYCLE_TIMER`](/vi/blocks/cycle-timer) hiển thị thời gian trôi qua (`ET`) so với thời gian đặt (`PT`) cùng trạng thái ngõ ra `Q` theo nhịp máy chạy thực tế.
4. **Huy hiệu Cảnh báo Chất lượng Dữ liệu (`[STALE]`):**
   - Nếu một cảm biến RS-485 đấu vào khối [`MB_READ`](/vi/blocks/mb-read) bị đứt dây A/B hoặc mất nguồn quá số lần thử lại, hoặc kênh [`MQTT_SUB`](/vi/blocks/mqtt-sub) quá hạn mất gói tin, khối tương ứng lập tức bật huy hiệu cảnh báo **`[STALE]`** giúp khoanh vùng đúng điểm lỗi hiện trường chỉ trong vài giây.

::: warning Chế độ Quan sát An toàn Không Chỉnh sửa Nhầm
Trong lúc đang bật **Online Live Debug (`F8`)**, thao tác sửa đổi cấu trúc sơ đồ được khóa bảo vệ để tránh vô tình kéo lệch dây làm mất đồng bộ giữa hình vẽ trên màn hình và chương trình đang chạy trong thiết bị. Hãy nhấn `F8` lần nữa để thoát chế độ gỡ lỗi khi cần chỉnh sửa mạch, sau đó biên dịch (`F7`), mô phỏng (`F5`) và nạp lại an toàn (`Ctrl+F5`).
:::

---

## Bảng So sánh: Offline Simulator (`F5`) vs. Online Live Debug (`F8`) vs. Live Watch (`Ctrl+7`)

SynaptiX IDE trang bị bộ ba công cụ chẩn đoán bổ trợ chặt chẽ cho từng giai đoạn của vòng đời dự án tự động hóa:

| Đặc tính / Năng lực Kỹ thuật | Mô phỏng Ngoại tuyến (`F5`) | Gỡ lỗi Sơ đồ Trực tuyến (`F8`) | Giám sát & Đồ thị Live Watch (`Ctrl+7`) |
| :--- | :--- | :--- | :--- |
| **Cần Phần cứng Vật lý?** | **Không** (Chạy 100% trên máy ảo Desktop) | **Có** (Kết nối `F4` qua USB CDC / Modbus TCP) | **Có** (Kết nối `F4` qua USB CDC / Modbus TCP) |
| **Giao diện Trực quan Trọng tâm** | Bảng công tắc giả lập I/O, nhích từng bước quét & nhật ký vết luật | Dây dẫn phát sáng, giá trị chân cắm & thanh `ET/PT` phủ ngay trên bản vẽ FBD | Bảng dữ liệu mật độ cao chuẩn IBM Carbon, đồ thị xu hướng & bảng cưỡng bức |
| **Nhích Từng Chu kỳ Quét (`StepScan`)** | **Hỗ trợ** (Tiến đúng 1 chu kỳ/lần bấm) | Không áp dụng (Vi điều khiển quét thời gian thực) | Không áp dụng (Vi điều khiển quét thời gian thực) |
| **Tua nhanh Thời gian & Giờ RTC** | **Hỗ trợ** (`SimClock` & hệ số `SimSpeed`) | Chạy theo thời gian thực của phần cứng | Chạy theo thời gian thực của phần cứng |
| **Vẽ Đồ thị Biến thiên & Xuất CSV** | Luồng nhật ký sự kiện (`60` bản ghi gần nhất) | Hiển thị giá trị tức thời trên từng khối | **Đồ thị đa kênh thời gian thực** & xuất lịch sử đo lường |
| **Ghi / Cưỡng bức Giá trị Biến** | Gạt/nhập tự do mọi biến ảo `DI`/`AI`/`VREG` | Quan sát chỉ đọc an toàn trên bản vẽ | **Ghi/cưỡng bức trực tuyến có khóa bảo vệ** (`DO`, `VFLAG`, `VREG`) kèm Lease an toàn |
| **Giai đoạn Sử dụng Lý tưởng** | Thiết kế thuật toán, kiểm thử biên & dữ liệu xấu trước khi đấu tủ điện | Dò tìm điều kiện khóa chéo bị kẹt, timer chưa đủ giờ & điểm đứt cáp `[STALE]` | Tinh chỉnh vòng điều khiển, đánh giá độ ổn định tín hiệu analog & chạy thử tải dài hạn |

---

## Tài liệu Liên quan

- [Sơ đồ Logic — Logic Graph (`Ctrl+2`)](/vi/workspaces/logic-graph-tab)
- [Triển khai Nguyên tử — Deploy (`Ctrl+6` / `Ctrl+F5`)](/vi/workspaces/deploy-tab)
- [Giám sát & Đồ thị — Live Watch (`Ctrl+7`)](/vi/workspaces/live-watch-tab)
- [Trợ lý Kỹ thuật SynaptiX AI Copilot (`Ctrl+Space`)](/vi/tools/ai-copilot)
