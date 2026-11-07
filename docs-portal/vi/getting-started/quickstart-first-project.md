# Dự án Đầu tiên trong 10 Phút (Quickstart)

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-GS-003</span>
  <span><strong>Chương:</strong> 02. Cài đặt &amp; Khởi tạo Nhanh</span>
  <span><strong>Thời lượng Thực hành:</strong> 10 Phút (Mô phỏng Offline + Nạp Thiết bị Tùy chọn)</span>
  <span><strong>Cấu hình Mục tiêu:</strong> SynaptiX Controller</span>
</div>

Trong bài hướng dẫn thực hành đầu-cuối này, bạn sẽ trực tiếp khởi tạo dự án, định nghĩa Tag chuẩn vật lý, thiết kế sơ đồ khối, mô phỏng kiểm thử, biên dịch, nạp nguyên tử hai pha và giám sát đồ thị trực tuyến cho bài toán công nghiệp thực tế: **Mạch Khóa Liên động Quạt Làm mát Bảo vệ Quá nhiệt Bồn Khuấy** chỉ trong 10 phút.

### Đặc tả Kỹ thuật Bài toán Thực hành
- **Cảm biến Nhiệt độ (`AI0` -> `TankTemp`, kiểu `REAL`):** Đo liên tục nhiệt độ thực của bồn khuấy theo đơn vị `°C`.
- **Công tắc Cửa An toàn (`DI0` -> `SafetyDoorOk`, kiểu `BOOL`):** Đóng kín ở mức `1` khi nắp chắn lồng quạt đã cài chốt an toàn; mở ra mức `0` khi nhân viên bảo trì mở cửa kiểm tra.
- **Ngõ ra Quạt Làm mát (`DO0` -> `CoolingFan`, kiểu `BOOL`):**
  - **Luật Khởi động:** Đóng rơ-le `CoolingFan = 1` khi và chỉ khi `TankTemp > 65.0` **VÀ** cửa an toàn đang đóng kín (`SafetyDoorOk == 1`).
  - **Luật Dừng Trễ Nhiệt & Cắt Khẩn cấp:** Ngắt `CoolingFan = 0` khi nhiệt độ hạ xuống `TankTemp <= 60.0` (tạo vùng trễ nhiệt Hysteresis `5 °C` chống đóng cắt nhấp nhả khởi động từ) **HOẶC** ngắt lập tức ngay khi cửa bảo vệ bị mở (`SafetyDoorOk == 0`).

<div class="synx-node-chain">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>INPUT</span>
      </div>
      <span class="synx-node-status is-live">● LIVE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">AI0 • REAL</span>
        <span class="synx-node-alias">TankTemp</span>
        <span class="synx-node-val">Nhiệt Độ Bồn</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">72.4 °C</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● TRUE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">&gt; 65.0 °C</span>
        <span class="synx-node-alias">High Temp Check</span>
        <span class="synx-node-val">So Sánh Quá Nhiệt</span>
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
        <span class="synx-node-tag">DI0 == 1</span>
        <span class="synx-node-alias">Safety Door OK</span>
        <span class="synx-node-val">Khóa Cửa An Toàn</span>
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
        <span class="synx-node-alias">SET CoolingFan</span>
        <span class="synx-node-val">Khởi Động Quạt</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

---

## Bước 1: Khởi tạo Dự án `SynaptiX Controller` (`Ctrl+N`)

1. Mở phần mềm **SynaptiX IDE** và nhấn tổ hợp phím **`Ctrl+N`** (hoặc vào menu **Tệp -> Dự án mới**).
2. Tại khung **Thông tin Dự án (Project Information)**:
   - **Tên dự án (Project Name):** Nhập `Tank_Cooling_Interlock`.
   - **Thư mục lưu (Save Location):** Giữ nguyên đường dẫn mặc định `Documents\SynaptiX\Projects` hoặc nhấn **Duyệt (Browse)** để chọn thư mục riêng.
3. Tại khung **Phần cứng Mục tiêu (Target Hardware)**:
   - Chọn mẫu **`SynaptiX Controller`** trong danh sách cấu hình định sẵn (hoặc nhấn **Detect from Connected Device** nếu bạn đang cắm sẵn thiết bị `SynaptiX Controller` thực tế).
   - Đối chiếu nhanh thẻ **Xem trước Ma trận Phần cứng** bên phải: dự án cấp phát `8 DI`, `8 DO`, `4 AI`, `32 VFLAG`, `32 VREG`, `32 RETAIN`, `8 COUNTER` (`124 / 128 tags`) và tối đa `100 rules`.
4. Nhấn nút **Tạo dự án (Create Project)**.

![Khởi tạo Dự án Mới với Cấu hình SynaptiX Controller](/images/synaptix_new_project.png)

---

## Bước 2: Đặt Tên Gợi nhớ & Kiểu Dữ liệu trong Tag Catalog (`Ctrl+3`)

Trước khi kéo khối vẽ mạch, hãy khai báo tên gợi nhớ (Alias) và kiểu dữ liệu chuẩn vật lý để toàn bộ sơ đồ tự giải thích rõ ràng:

1. Nhấn **`Ctrl+3`** để chuyển sang không gian **[Danh mục Biến — Tag Catalog](/vi/workspaces/tag-catalog-tab)**.
2. Tìm đến 3 biến vật lý của bài toán và cập nhật cột **Tên gợi nhớ (Alias)** cùng **Kiểu dữ liệu (Data Type)** như bảng sau:

| Mã Tag | Phân loại Kênh | Tên Gợi nhớ (Alias) | Kiểu Dữ liệu Cần chọn | Ý nghĩa Công nghệ |
| :--- | :--- | :--- | :---: | :--- |
| **`AI0`** | Analog Input | `TankTemp` | **`REAL`** | Nhiệt độ bồn khuấy số thực dấu phẩy động 32-bit IEEE-754 (`°C`). |
| **`DI0`** | Digital Input | `SafetyDoorOk` | `BOOL` *(Cố định)* | Công tắc hành trình cửa bảo vệ (`1` = Cửa đóng an toàn, `0` = Cửa mở). |
| **`DO0`** | Digital Output | `CoolingFan` | `BOOL` *(Cố định)* | Cuộn hút khởi động từ quạt làm mát (`1` = Chạy quạt, `0` = Dừng quạt). |

![Đặt Tên Gợi nhớ Alias và Chuyển Kiểu REAL trong Tag Catalog](/images/synaptix_tag_catalog.png)

::: tip Tại sao Nên Đổi `AI0` sang Kiểu `REAL` Ngay Từ Đầu?
Việc chuyển `AI0` từ `INT32` mặc định sang **`REAL`** giúp cơ chế [Chống Nhập sai Dữ liệu (Bad-Data Defense)](/vi/concepts/data-model-and-types) hiểu rằng `TankTemp` là đại lượng số thực IEEE-754. Nhờ vậy, mọi khối [`COMPARATOR`](/vi/blocks/comparator) nối với `AI0` ở bước sau sẽ cho phép nhập ngưỡng thập phân (như `65.0` hay `65.5`) và tự động chuẩn hóa khi bạn gõ `"65"` thành `"65.0"`.
:::

---

## Bước 3: Thiết kế Mạch Khóa Liên động trên Logic Graph (`Ctrl+2`)

1. Nhấn **`Ctrl+2`** để mở mặt vẽ **[Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab)**.
2. **Thiết kế Nhánh Luật 1 (Bật Quạt Khi Quá Nhiệt Có Khóa Cửa An toàn):**
   - Từ **Hộp Công cụ Khối Hàm** bên trái, kéo lần lượt 4 khối thả ra mặt lưới 20 px: **`INPUT`** (nhóm *Process I/O*), **`COMPARATOR`** (nhóm *Logic*), **`GUARD`** (nhóm *Logic*) và **`ACTION`** (nhóm *Process I/O*).
   - Kéo chuột nối chân ngõ ra bên phải của **`INPUT`** -> chân vào của **`COMPARATOR`** -> **`GUARD`** -> **`ACTION`**.
   - Nhấp chọn từng khối và thiết lập thông số tại bảng **Property Inspector** bên phải:
     - **Khối `INPUT`:** Chọn biến nguồn `[AI0] — TankTemp`.
     - **Khối `COMPARATOR`:** Chọn toán tử `` `>` ``, chế độ so sánh hằng số (`Constant`), nhập ngưỡng `65.0` *(thử gõ `65` rồi nhấn `Enter`, bạn sẽ thấy ô tự động chuẩn hóa thành `65.0`)*.
     - **Khối `GUARD`:** Chọn biến khóa `[DI0] — SafetyDoorOk`, toán tử `` `==` ``, giá trị `1`.
     - **Khối `ACTION`:** Chọn biến đích `[DO0] — CoolingFan`, lệnh tác động `SET` (đóng ngõ ra lên `1`).
3. **Thiết kế Nhánh Luật 2 & Luật 3 (Dừng Quạt Theo Độ Trễ Nhiệt & Cắt Ngay Khi Mở Cửa):**
   - Kéo thêm nhánh thứ hai: **`INPUT`** (`[AI0] — TankTemp`) -> **`COMPARATOR`** (`` `<=` `` `60.0`) -> **`ACTION`** (`RESET` `[DO0] — CoolingFan`).
   - Kéo thêm nhánh cắt an toàn thứ ba: **`INPUT`** (`[DI0] — SafetyDoorOk`) -> **`COMPARATOR`** (`` `==` `` `0`) -> **`ACTION`** (`RESET` `[DO0] — CoolingFan`).
4. Nhấn **`Alt+G`** để căn thẳng hàng toàn bộ khối bám khít lưới kỹ thuật, sau đó nhấn **`Ctrl+S`** để lưu dự án.

![Thiết kế Sơ đồ Khối Bảo vệ Quá nhiệt trên Logic Graph Canvas](/images/synaptix_logic_editor.png)

---

## Bước 4: Kiểm thử trên Offline Simulator (`F5`) & Rà soát Rule Table (`Ctrl+4`)

Nguyên tắc vàng trong tự động hóa công nghiệp là kiểm chứng đầy đủ cả kịch bản chạy chuẩn lẫn kịch bản sự cố trên trình mô phỏng trước khi nạp xuống tủ điện:

1. Nhấn phím **`F5`** để mở ngăn **[Mô phỏng Offline — Simulator](/vi/tools/simulator-and-live-debug)** ngay cạnh dưới mặt vẽ Logic Graph.
2. **Kịch bản A — Kiểm tra Khóa Liên động Chặn Khi Cửa Mở:**
   - Giữ nguyên `[DI0] — SafetyDoorOk` ở mức `0` (Cửa đang mở).
   - Nhập giá trị `[AI0] — TankTemp` lên `70.0` và nhấn `Enter`.
   - Quan sát trên sơ đồ: đoạn dây nối từ `INPUT` sang `COMPARATOR` và tới đầu vào `GUARD` sáng xanh lục (`70.0 > 65.0` đã đúng), **nhưng bị chặn đứng tại cổng `GUARD`**. Ngõ ra `[DO0] — CoolingFan` giữ an toàn tuyệt đối ở mức `0`!
3. **Kịch bản B — Đóng Cửa Cho phép Khởi động Quạt:**
   - Gạt `[DI0] — SafetyDoorOk` lên mức `1` (Cửa đã đóng kín).
   - Ngay lập tức, cổng `GUARD` thông mạch, toàn bộ đường dây sáng xanh xuyên suốt tới `ACTION`, và `[DO0] — CoolingFan` bật lên `1`.
4. **Kịch bản C — Kiểm tra Vùng Trễ Nhiệt Hysteresis & Tự Ngắt Khi Mát:**
   - Hạ `[AI0] — TankTemp` xuống `62.5`. Vì `62.5` nằm trong dải trễ `(60.0 .. 65.0]`, quạt `CoolingFan` vẫn duy trì chạy êm ở mức `1` không bị đóng cắt liên tục.
   - Hạ tiếp `[AI0] — TankTemp` xuống `59.0`. Nhánh Luật 2 lập tức tác động `RESET` đưa `CoolingFan` về `0`.
5. Nhấn **`F5`** để đóng mô phỏng, nhấn **`F7`** để chạy **Biên dịch toàn cục (Compile)**, sau đó nhấn **`Ctrl+4`** mở **[Bảng Luật — Rule Table](/vi/workspaces/rule-table-tab)** để kiểm tra 3 dòng luật 4 giai đoạn đã biên dịch hoàn chỉnh (`3 / 100 rules`).

![Kiểm chứng Kịch bản Khóa Liên động trên Trình Mô phỏng Offline Simulator](/images/synaptix_simulator.png)

---

## Bước 5: Kết nối (`F4`), Nạp Nguyên tử 2 Pha (`Ctrl+F5`) & Giám sát Live Watch (`Ctrl+7`)

Sau khi sơ đồ đã vượt qua 100% bài kiểm thử mô phỏng, tiến hành nạp chương trình xuống **Bộ Điều khiển SynaptiX Controller** thực tế:

1. **Thiết lập Phiên Kết nối (`F4`):**
   - Trên Cụm Kết nối góc trên bên phải, chọn **`USB CDC`** (chọn đúng cổng `COMx`) hoặc **`Modbus TCP`** (nhập `IP : 502` của bộ điều khiển).
   - Nhấn **`F4`** (hoặc nút **Kết nối**). Xác nhận chấm trạng thái góc dưới chuyển sang màu xanh lục bảo.
2. **Thực thi Nạp Nguyên tử Hai Pha (`Ctrl+F5`):**
   - Nhấn **`Ctrl+6`** mở không gian **[Triển khai — Deploy](/vi/workspaces/deploy-tab)** (hoặc nhấn thẳng **`Ctrl+F5`** từ bất kỳ Tab nào).
   - Kiểm tra bảng **Cổng Thẩm định Tiền Triển khai (Pre-Flight Gate)** báo xanh toàn bộ các tiêu chí (Khớp dòng thiết bị, Tương thích giao thức, Dung lượng Tag & Luật nằm trong ngưỡng).
   - Xác nhận nạp chương trình. **SynaptiX IDE** truyền cấu hình vào vùng đệm cô lập, đối chiếu mã kiểm tra toàn vẹn rồi mới chuyển mạch nguyên tử sang vùng nhớ Flash vận hành — đảm bảo dù mất điện đột ngột giữa chừng thiết bị cũng không bao giờ rơi vào trạng thái lỗi dở dang.

![Cổng Thẩm định Tiền Triển khai & Nạp Nguyên tử Hai Pha](/images/synaptix_deploy_gate.png)

3. **Nghiệm thu Đồ thị Thời gian Thực trên Live Watch (`Ctrl+7`) & Live Debug (`F8`):**
   - Nhấn **`Ctrl+7`** chuyển sang **[Giám sát — Live Watch](/vi/workspaces/live-watch-tab)**. Đưa 3 biến `TankTemp`, `SafetyDoorOk`, `CoolingFan` vào khung đồ thị xu hướng để quan sát đường đặc tuyến trực tiếp ở tốc độ 20 FPS và xuất biên bản CSV nghiệm thu nếu cần.
   - Bạn cũng có thể quay lại **Sơ đồ Logic (`Ctrl+2`)** và bật **`F8` (Online Live Debug)** để theo dõi dòng điện thực tế ngoài tủ điều khiển làm sáng trực tiếp từng sợi dây nối trên màn hình máy tính.

![Giám sát Giá trị Biến 20 FPS và Đồ thị Xu hướng trong Live Watch](/images/synaptix_live_watch.png)

---

## Lộ trình Khám phá Tiếp theo

- **Làm chủ Toàn bộ 7 Không gian Làm việc:** Tra cứu chi tiết từng tính năng chuyên sâu tại [Chương 03 — Không gian Làm việc (7 Tab)](/vi/workspaces/device-tab).
- **Khai thác Đầy đủ 18 Khối Hàm Công nghiệp:** Mở rộng bài toán với khối hiệu chuẩn [`SCALE`](/vi/blocks/scale), chống nhiễu [`TON`](/vi/blocks/ton), đọc đồng hồ RS-485 [`MB_READ`](/vi/blocks/mb-read) và bắn cảnh báo đám mây [`MQTT_PUB`](/vi/blocks/mqtt-pub) tại [Chương 04 — Từ điển Khối hàm](/vi/blocks/overview).
- **Tăng tốc Thiết kế với Thư viện Mẫu & AI Copilot:** Khởi tạo mạch mẫu trong 1 cú nhấp chuột với [Blueprints (`Ctrl+5`)](/vi/workspaces/blueprints-tab) hoặc ra lệnh bằng ngôn ngữ tự nhiên cho [SynaptiX AI Copilot (`Ctrl+Space`)](/vi/tools/ai-copilot).
