# 2. Sơ đồ Logic — Logic Graph (`Ctrl+2`)

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-WS-002</span>
  <span><strong>Không gian làm việc:</strong> Tab 2 &bull; Phím tắt <code>Ctrl+2</code></span>
  <span><strong>Phạm vi:</strong> Thiết kế Khối hàm Trực quan, Đi dây Tín hiệu, Căn chỉnh Đa khối &amp; Biên dịch Tĩnh</span>
  <span><strong>Tiêu chuẩn:</strong> IEC 61131-3 &bull; IEC/IEEE 82079-1 &bull; ANSI/ISA-101</span>
</div>

---

## 1. Mục đích & Vai trò Vận hành

**Sơ đồ Logic — Logic Graph Canvas** (`Ctrl+2`) là không gian kỹ thuật thiết kế điều khiển trực quan trung tâm của **SynaptiX IDE**. Kết hợp công thái học sơ đồ khối hàm chuẩn IEC 61131-3 (FBD) với kiến trúc thực thi tất định Sự kiện – Điều kiện – Hành động (ECA), không gian này giúp kỹ sư tự động hóa xây dựng, kiểm tra, mô phỏng và gỡ lỗi trực tuyến các thuật toán điều khiển công nghiệp lẫn điện toán biên IIoT mà không cần viết mã nhúng thủ công.

Trong quy trình tích hợp hệ thống, **Logic Graph Canvas** đảm nhiệm bốn vai trò trọng yếu:

1. **Thiết kế Luồng Tín hiệu Tất định:** Kéo thả các khối hàm từ hộp công cụ chuẩn thanh ray DIN sang mặt phẳng lưới kỹ thuật `20 px` và nối dây giữa các chân **Giá trị (Value)** và **Sự kiện (Event)** theo chiều chuẩn từ trái sang phải (`INPUT → TRIGGER / COMPARATOR → GUARD → ACTION`).
2. **Tổ chức Bản vẽ Kỹ thuật Chuẩn Hồ sơ Nghiệm thu:** Duy trì bản vẽ mạch lạc nhờ đường dây vuông góc 90 độ (orthogonal step routing), bắt điểm lưới (`Alt+G`), quét chọn đa khối, lịch sử Hoàn tác/Làm lại (`Undo/Redo`) và bộ phím tắt căn hàng – phân bố khoảng cách chuyên nghiệp.
3. **Tham số hóa An toàn Kiểu & Đối chiếu Ngôn ngữ Tự nhiên:** Thiết lập ngưỡng so sánh, bộ định thời, kênh truyền thông RS-485 và mẫu bản tin JSON ngay tại **Bảng Thuộc tính Bên phải (Right Inspector)**—được bảo vệ bởi cơ chế chống nhập sai kiểu (Bad-Data Defense) cùng ô **Diễn giải Câu lệnh (Narrative Preview)** theo thời gian thực.
4. **Biên dịch (`F7`), Mô phỏng Chu kỳ (`F5`) & Gỡ lỗi Trực tuyến (`F8`):** Hạ cấp sơ đồ trực quan thành tập luật ECA tất định có kiểm tra tĩnh, chạy mô phỏng ngoại tuyến chính xác theo chu kỳ quét hoặc phủ dữ liệu phần cứng thực tế ngay trên từng chân nối và đường dây.

---

## 2. Bố cục Trực quan & Tham chiếu Điều khiển

![Không gian Thiết kế Sơ đồ Logic SynaptiX](/images/synaptix_logic_editor.png)

**Logic Graph Canvas** được thiết kế theo chuẩn màu xám trung tính chống mỏi mắt công nghiệp (ANSI/ISA-101) với bố cục 3 cột chuyên sâu và thanh trạng thái biên dịch – kiểm thử ở cạnh dưới:

### 2.1. Tham chiếu Kiến trúc Bàn làm việc 3 Cột

| Phân vùng Giao diện | Kích thước / Vị trí | Điều khiển Cốt lõi & Hành vi Vận hành |
| :--- | :--- | :--- |
| **Hộp Công cụ Khối hàm (Left Toolbox)** | Neo trái `265 px` | Tích hợp ô **Tìm kiếm Nhanh** và 4 nhóm khối hàm thu gọn kiểu DIN-rail: **Core Rule Pipeline**, **Timing, Counting & Scaling**, **Fieldbus & Hardware Actuation**, và **IIoT Cloud & IP Network**. Các khối không được phần cứng mục tiêu hỗ trợ sẽ hiển thị biểu tượng khóa kèm chú giải rõ lý do. |
| **Mặt phẳng Vẽ Tương tác (Center Canvas)** | Vùng trung tâm co giãn | Không gian bản vẽ vô tận hỗ trợ kéo trượt (Pan) và phóng đại (Zoom) trên nền lưới kỹ thuật `20 px` (`Alt+G`), dây nối vuông góc 90 độ có vùng bắt chuột rộng `12 px`, tự động bắt dính chân hợp lệ và khung quét chọn hàng loạt. |
| **Bảng Thuộc tính (Right Inspector)** | Neo phải `320 px` | Biểu mẫu tham số động theo khối đang chọn. Cung cấp bộ chọn Tag, ô nhập số tích hợp Bad-Data Defense, nút mở hộp thoại cấu hình chuyên sâu (cho `MQTT_PUB`, `MQTT_SUB`, `MB_READ`, `MB_WRITE`, `DIRECT_WRITE`, `HTTP_CLIENT`, `TCP_CLIENT`), khung **Narrative Preview** và danh sách chẩn đoán lỗi có hỗ trợ nhấp để nhảy tới khối lỗi. |
| **Thanh Biên dịch & Gỡ lỗi (Bottom Bar)** | Dải trạng thái dưới cùng | Hiển thị huy hiệu **Trạng thái Biên dịch** (`Valid` xanh lá, `Stale` vàng hổ phách, `Invalid` đỏ), nút **Compile Rules (`F7`)**, **Offline Simulator (`F5`)**, **Online Live Debug (`F8`)** và trợ lý **SynaptiX AI Copilot (`Ctrl+Space`)**. |

### 2.2. Quy ước Chân Kết nối (Pin) & Đi dây Vuông góc

Mỗi khối hàm trên **Logic Graph Canvas** tách biệt nghiêm ngặt giữa **luồng dữ liệu giá trị** và **luồng xung sự kiện**, ngăn chặn từ gốc việc nối sai cấu trúc mạch ngay trong lúc kéo chuột:

<div class="synx-node-chain">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>INPUT</span>
      </div>
      <span class="synx-node-status is-live">● VALUE PIN</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Chân Giá Trị</span>
        <span class="synx-node-alias">NGUỒN DỮ LIỆU</span>
        <span class="synx-node-val">Truyền Tag Vật Lý / Ảo</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Dữ Liệu Tag</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>CMP / TRIGGER</span>
      </div>
      <span class="synx-node-status is-true">● EVAL PIN</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Chân Đánh Giá</span>
        <span class="synx-node-alias">KIỂM TRA ĐIỀU KIỆN</span>
        <span class="synx-node-val">So Sánh Ngưỡng / Sườn Xung</span>
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
      <span class="synx-node-status is-pass">● GATE PIN</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Chân Sự Kiện</span>
        <span class="synx-node-alias">KHÓA LIÊN ĐỘNG</span>
        <span class="synx-node-val">Điều Kiện Cho Phép AND</span>
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
      <span class="synx-node-status is-active">● ACTUATOR</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Chân Chấp Hành</span>
        <span class="synx-node-alias">THỰC THI TÁC VỤ</span>
        <span class="synx-node-val">SET, RESET, DIRECT OUT</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

| Phần tử Chân / Dây nối | Hợp đồng Kiểu & Trực quan | Quy tắc Đấu nối trên Bản vẽ |
| :--- | :--- | :--- |
| **Chân Giá trị (Value Pins)** | Truyền giá trị biến thực tế hoặc tín hiệu đo lường đã hiệu chuẩn (`BOOL`, `INT32`, `REAL`). | Nối từ ngõ ra của khối **INPUT** hoặc **SCALE** sang ngõ vào dữ liệu của **TRIGGER**, **COMPARATOR**, **GUARD** hoặc các khối chức năng tiêu thụ dữ liệu. |
| **Chân Sự kiện (Event Pins)** | Truyền xung kích hoạt thực thi tất định trong chu kỳ quét (`Event`). | Nối tuần tự qua từng tầng của chuỗi luật (`TRIGGER → COMPARATOR → GUARD → ACTION` hoặc kích hoạt các khối Timer/Counter có trạng thái). |
| **Dây Vuông góc 90 Độ** | Đường mạch công nghiệp gấp khúc vuông góc (`IndustrialStepConnection`) với vùng nhận diện nhấp chuột ẩn rộng `12 px`. | Hỗ trợ nhấp chọn trực tiếp, nhấn `Del` để cắt dây và đổi màu động truyền sóng tín hiệu khi bật Mô phỏng (`F5`) hoặc Gỡ lỗi Trực tuyến (`F8`). |
| **Bắt dính Chân Hợp lệ** | Đầu dây đang kéo chỉ bắt dính vào đúng chân ngõ vào/ngõ ra tương thích. | Dây thả lơ lửng giữa khoảng trống sẽ tự động hủy bỏ khi nhả chuột, giữ bản vẽ luôn sạch và không phát sinh nhánh cụt. |

### 2.3. Bảng Phím tắt Điều hướng, Biên tập & Căn chỉnh Đa khối

Để trình bày các bản vẽ nhiều nhánh đạt chuẩn hồ sơ kỹ thuật tủ bảng điện, **SynaptiX IDE** trang bị hệ thống phím tắt thao tác nhanh chuẩn phần mềm CAD công nghiệp:

| Thao tác / Lệnh Kỹ thuật | Phím tắt / Cử chỉ Chuột | Tác dụng trên Mặt phẳng Bản vẽ |
| :--- | :--- | :--- |
| **Trượt Khung nhìn (Pan)** | Kéo Chuột giữa / Kéo Chuột phải | Di chuyển mượt mà qua các sơ đồ điều khiển diện rộng nhiều nhánh. |
| **Thu phóng Bản vẽ (Zoom)** | `Ctrl + Con lăn chuột` (hoặc Con lăn) | Phóng cận cảnh để kiểm tra tên chân pin hoặc thu nhỏ để bao quát toàn bộ kiến trúc hệ thống. |
| **Quét Chọn Nhiều Khối** | Kéo Chuột trái trên vùng trống (`Ctrl+Click` chọn thêm) | Chọn đồng thời nhiều khối và dây nối để di chuyển, sao chép hoặc căn chỉnh hàng loạt. |
| **Căn Trái / Giữa dọc / Phải** | `Alt+Left` / `Alt+C` / `Alt+Right` | Căn thẳng tọa độ ngang `X` của từ 2 khối trở lên theo mép trái, trục tâm dọc hoặc mép phải. |
| **Căn Đỉnh / Giữa ngang / Đáy** | `Alt+Up` / `Alt+M` / `Alt+Down` | Căn thẳng tọa độ dọc `Y` của từ 2 khối trở lên theo mép trên, trục tâm ngang hoặc mép dưới. |
| **Phân bố Đều Ngang / Dọc** | `Alt+Shift+H` / `Alt+Shift+V` | Chia đều khoảng cách theo phương ngang hoặc phương dọc cho từ 3 khối được chọn trở lên. |
| **Bắt điểm Lưới / Bật tắt Lưới** | `Alt+G` | Bật/tắt lưới kỹ thuật `20 px` (bước nhảy `20 px` khi bật lưới; đặt tự do `1 px` khi ẩn lưới) và kéo toàn bộ khối về đúng giao điểm lưới. |
| **Hoàn tác / Làm lại (Undo / Redo)** | `Ctrl+Z` / `Ctrl+Y` (`Ctrl+Shift+Z`) | Quay lui hoặc tiến tới qua lịch sử thao tác đặt khối, xóa khối, nối dây và dịch chuyển tọa độ. |
| **Cắt / Sao chép / Dán / Nhân bản** | `Ctrl+X` / `Ctrl+C` / `Ctrl+V` / `Ctrl+D` | Nhân bản nhanh cụm nhánh mạch cùng toàn bộ dây nối nội bộ với độ lệch tọa độ trực quan. |
| **Chọn Tất cả / Xóa Phần tử** | `Ctrl+A` / `Del` | Chọn toàn bộ khối trên bản vẽ hoặc xóa sạch các khối đang chọn cùng dây nối liên đới. |
| **Biên dịch / Lưu & Biên dịch** | `F7` / `Ctrl+S` | Hạ cấp sơ đồ trực quan thành tập luật ECA tất định (`0..99`), kiểm tra hợp lệ tĩnh và cập nhật [Bảng Luật — Rule Table](/vi/workspaces/rule-table-tab). |

---

## 3. Quy trình Thao tác Chuẩn từng Bước

### Quy trình 3.1: Thiết kế Mạch Điều khiển Ngưỡng Áp suất Có Khóa Liên động

1. Nhấn `Ctrl+2` để mở **Sơ đồ Logic (Logic Graph Canvas)**. Đảm bảo chế độ lưới `20 px` đang bật (`Alt+G`) để các khối tự động bắt điểm thẳng hàng.
2. Từ **Hộp Công cụ Bên trái** (nhóm `Core Rule Pipeline`), lần lượt kéo các khối **INPUT**, **TRIGGER**, **COMPARATOR**, **GUARD** và **ACTION** đặt từ trái sang phải trên bản vẽ.
3. Nhấp chọn khối **INPUT**, sau đó tại **Bảng Thuộc tính Bên phải**, gán biến cảm biến tương tự (ví dụ: `AI0` — Áp suất Nồi hơi).
4. Kéo dây từ chân ngõ ra của **INPUT** sang chân ngõ vào của **TRIGGER**, chọn chế độ kích hoạt (chẳng hạn `ON_CHANGE` hoặc lọc chống dội `ForMs`), rồi nối tiếp **TRIGGER → COMPARATOR**.
5. Nhấp chọn khối **COMPARATOR**, chọn toán tử `>` và nhập ngưỡng tác động (ví dụ: `8.5` cho biến kiểu `REAL`). Lưu ý khi gõ `"8"` rồi nhấn `Enter` hoặc rời ô nhập, IDE sẽ tự động chuẩn hóa thành `"8.0"`.
6. Nối dây từ **COMPARATOR → GUARD**, gán khối **GUARD** vào tín hiệu cho phép an toàn (ví dụ: `DI0` — Nút dừng khẩn E-Stop bình thường ở mức `1`), rồi nối **GUARD → ACTION** (`DO0` — Mở van xả áp `SET 1`).
7. Đọc câu diễn giải tự động tại mục **Narrative Preview** ở cuối Bảng Thuộc tính để đối chiếu logic trên màn hình khớp 100% với thuyết minh công nghệ P&ID.

### Quy trình 3.2: Căn chỉnh Bản vẽ & Biên dịch sang Tập Luật Tất định (`F7`)

1. Quét chuột trái bao quanh cả 5 khối (`INPUT`, `TRIGGER`, `COMPARATOR`, `GUARD`, `ACTION`).
2. Nhấn `Alt+M` (**Căn Giữa Ngang**) rồi nhấn tiếp `Alt+Shift+H` (**Phân bố Đều Ngang**) để toàn bộ chuỗi khối nằm thẳng tắp trên một trục tâm với khoảng cách đồng đều.
3. Nhấn phím `F7` (**Compile Rules**).
4. Kiểm tra huy hiệu **Compile State** ở thanh dưới cùng chuyển sang màu xanh lá (`Valid`). Nếu có khối nào chưa gán Tag hoặc thiếu dây nối bắt buộc, chỉ cần nhấp vào dòng cảnh báo để khung nhìn tự động cuộn và đưa khối đó vào chính giữa màn hình.
5. *(Tùy chọn)* Nhấn `F5` để chạy thử nghiệm trực tiếp trên [Bộ Mô phỏng Offline Chu kỳ Chính xác](/vi/tools/simulator-and-live-debug), hoặc nhấn `Ctrl+4` để kiểm tra tập luật sau biên dịch tại [Bảng Luật — Rule Table](/vi/workspaces/rule-table-tab).

---

## 4. Thực hành Kỹ thuật Tốt nhất & Xử lý Tình huống Biên

::: tip BẢO TOÀN MÃ BĂM NGỮ NGHĨA (SEMANTIC HASH) KHI CĂN CHỈNH BẢN VẼ
Các thao tác kéo dịch khối, căn hàng (`Alt+M`, `Alt+Shift+H`) hoặc bật/tắt bắt điểm lưới (`Alt+G`) chỉ thay đổi tọa độ trình bày trực quan chứ không làm thay đổi bản chất thuật toán điều khiển. **SynaptiX IDE** tính toán **Mã băm Ngữ nghĩa Chương trình (Semantic Program Hash)** nên việc dọn đẹp bố cục bản vẽ sẽ **không** làm mất trạng thái đồng bộ của chương trình đã nạp xuống phần cứng.
:::

- **Cơ chế Chống Nhập Dữ liệu Sai (Bad-Data Defense) tại Inspector:** Mọi ô nhập ngưỡng số, thời gian đặt (`PT`) và giá trị đặt bộ đếm (`PV`) trong Bảng Thuộc tính đều tuân thủ hợp đồng bộ nhớ vật lý chuẩn IEC 61131-3. Nếu kỹ sư gõ nhầm số thập phân `"25.5"` hoặc ký tự `"abc"` vào tham số kiểu nguyên `INT32`, ngay khi rời ô (`LostFocus`) hoặc nhấn `Enter`, ô nhập sẽ tự động khôi phục về giá trị nguyên hợp lệ gần nhất—đảm bảo sự trung thực tuyệt đối giữa giao diện IDE và bộ nhớ **SynaptiX Runtime**.
- **Quản lý Ngân sách Luật khi Dùng Khối Hàm Trạng thái (Macro Blocks):** Một chuỗi điều khiển tiêu chuẩn (`INPUT → TRIGGER → COMPARATOR → GUARD → ACTION`) biên dịch thành **1 luật ECA**, trong khi các khối hàm chuẩn IEC có bộ nhớ trạng thái sẽ tự động hạ cấp thành tổ hợp nhiều luật phối hợp khi nhấn `F7` (`TON`, `TOF`, `TP` sinh **2 luật**/khối; `CTU`, `CTD` sinh **4 luật**/khối). Hãy theo dõi thanh phần trăm dung lượng trên tổng ngân sách 100 luật tại [Bảng Luật — Rule Table](/vi/workspaces/rule-table-tab).
- **Mở khóa Toàn bộ Khối hàm trong Chế độ Thiết kế Thử nghiệm (Sandbox):** Khi làm việc ngoại tuyến với hồ sơ **Edge Controller** đầy đủ (hoặc chế độ Sandbox), toàn bộ 20 khối hàm bao gồm cả nhóm RS-485 và IIoT Cloud đều được mở khóa để kỹ sư phác thảo giải pháp nhanh. Trước khi mang ra hiện trường, hãy chọn đúng biến thể phần cứng thực tế tại [Không gian Thiết bị — Device](/vi/workspaces/device-tab) và nhấn `F7` để trình biên dịch kiểm chứng tính tương thích phần cứng.

---

## Tài liệu Liên quan

- [Tổng quan Danh mục & Quy ước Chân](/vi/blocks/overview)
- [Chu trình Thực thi Tất định](/vi/concepts/execution-pipeline)
- [4. Bảng Luật — Rule Table (`Ctrl+4`)](/vi/workspaces/rule-table-tab)
- [Mô phỏng Offline & Gỡ lỗi Trực tuyến](/vi/tools/simulator-and-live-debug)
- [Trợ lý Kỹ thuật SynaptiX AI Copilot](/vi/tools/ai-copilot)
- [Bảng Phím tắt & Thao tác Canvas](/vi/appendices/keyboard-shortcuts)
