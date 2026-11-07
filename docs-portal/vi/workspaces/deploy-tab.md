# 6. Triển khai Nguyên tử — Atomic Deploy (`Ctrl+6`)

<div class="doc-meta-bar">
  <span><strong>Mã tài liệu:</strong> SYNX-DOC-WS-006</span>
  <span><strong>Không gian làm việc:</strong> Tab 6 &bull; Phím tắt <code>Ctrl+6</code> &bull; Thực thi <code>Ctrl+F5</code></span>
  <span><strong>Phạm vi:</strong> Tiền Kiểm tra Tĩnh, Nạp Nguyên tử Hai Pha An toàn Mất điện, Đọc Ngược (F6) &amp; Đóng gói Nhị phân (.bin)</span>
  <span><strong>Tiêu chuẩn:</strong> IEC 61508-3 &bull; IEC/IEEE 82079-1 &bull; ANSI/ISA-101</span>
</div>

---

## 1. Mục đích & Vai trò Vận hành

**Không gian Triển khai — Atomic Deploy Workspace** (`Ctrl+6`) là cổng kiểm duyệt phát hành và bộ máy nạp cấu hình bất biến của **SynaptiX IDE**. Trong môi trường công nghiệp nặng, hiện tượng sụt áp tủ điện thoáng qua (brownout), nhiễu đường truyền RS-485 hoặc tuột cáp USB giữa chừng khi đang nạp chương trình có thể khiến các bộ điều khiển truyền thống rơi vào trạng thái lỗi ghi dở dang nguy hiểm. **SynaptiX IDE** triệt tiêu hoàn toàn rủi ro này bằng giao thức **Nạp Nguyên tử Hai Pha An toàn (Two-Phase Atomic Safe Commit — `Ctrl+F5`)**.

Xuyên suốt vòng đời nghiệm thu tủ điện và sản xuất loạt lớn, **Atomic Deploy Workspace** đảm nhiệm bốn tác vụ độ tin cậy cao:

1. **Tiền Kiểm tra Tĩnh & Đối chiếu Năng lực Phần cứng (Pre-Flight Verification):** Kiểm tra toàn diện trạng thái kết nối, tính mới nhất của bản biên dịch [Sơ đồ Logic](/vi/workspaces/logic-graph-tab) (`Valid` so với `Stale`/`Invalid`), các ràng buộc miền an toàn và năng lực phần cứng mục tiêu trước khi truyền đi bất kỳ byte dữ liệu nào.
2. **Nạp Nguyên tử Hai Pha Không Gián đoạn (`Ctrl+F5`):** Truyền toàn bộ gói cấu hình vào vùng đệm cô lập và kiểm tra mã toàn vẹn (**Pha 1**) trước khi phát lệnh chuyển mạch Flash nguyên tử duy nhất tại ranh giới chu kỳ quét (**Pha 2**)—giúp dây chuyền đang chạy tiếp tục vận hành êm ái không giật cục (bumpless transfer).
3. **Đọc Ngược & Tái tạo Toàn diện từ Thiết bị (`F6` — Upload from Device):** Tải ngược toàn bộ tập luật ECA, các khối hàm Timer/Counter, kênh đọc RS-485, chủ đề MQTT và tham số DTU đang chạy thực tế trên **SynaptiX Edge Controller** về IDE, đồng thời tái dựng tự động cả [Bảng Luật](/vi/workspaces/rule-table-tab) lẫn bản vẽ [Sơ đồ Logic](/vi/workspaces/logic-graph-tab).
4. **Xuất / Nhập Ảnh Nhị phân Bảo vệ CRC (`*.bin`):** Đóng gói và nạp tệp ảnh cấu hình nhị phân chuẩn hóa phục vụ lưu trữ quản lý phiên bản và nạp hàng loạt tốc độ cao tại dây chuyền lắp ráp tủ bảng điện.

---

## 2. Bố cục Trực quan & Tham chiếu Điều khiển

![Không gian Triển khai Nguyên tử Atomic Deploy SynaptiX](/images/synaptix_deploy_gate.png)

Giao diện **Atomic Deploy Workspace** chia thành hai phân khu rõ rệt: **Cột Trái — Bảng Kê khai & Kiểm tra Tiền Triển khai** và **Cột Phải — Tiến trình Nạp 5 Bước & Nhật ký Console Chẩn đoán**:

### 2.1. Tham chiếu Bảng Kê khai & Điều khiển Tiến trình Nạp

| Khung Thông tin / Điều khiển | Phím tắt / Vị trí | Mô tả & Hành vi Vận hành |
| :--- | :--- | :--- |
| **Biểu ngữ Sẵn sàng Tiền Kiểm tra** | Đỉnh cột trái | Hiển thị trạng thái đủ điều kiện nạp (`Ready to Deploy`, `Compile Stale`, `Validation Error`, hoặc `Device Offline`) cùng chỉ báo đồng bộ **Mã băm Ngữ nghĩa (Semantic Hash)**. |
| **Thẻ Kê khai Triển khai (Deployment Manifest)** | Thân cột trái | Thống kê chi tiết số lượng thành phần sẽ nạp xuống thiết bị: số luật `Logic Rules` kích hoạt, khối `Function Blocks` (Timer/Counter), kênh `RS-485 Modbus`, dịch vụ `MQTT Services`, chế độ xuất `Direct Output`, cấu hình `DTU Gateway` và bảng `Tag Data Type Registry` (`BOOL` / `INT32` / `REAL`). |
| **Nút Commit / Atomic Deploy** | `Ctrl+F5` (Thanh tác vụ trái) | Khởi chạy quy trình Nạp Nguyên tử Hai Pha 5 bước xuống **SynaptiX Device** (hoặc bộ mô phỏng ảo). |
| **Nút Upload from Device** | `F6` (Thanh lệnh trên / trái) | Đọc ngược cấu hình đang chạy trên bộ điều khiển về **SynaptiX IDE** (có hộp thoại cảnh báo bảo vệ nếu dự án trên máy đang có thay đổi chưa lưu). |
| **Thanh Tiến trình Nguyên tử 5 Bước** | Nửa trên cột phải | Trực quan hóa trực tiếp từng pha thực thi: **1. Kiểm tra Tiền triển khai**, **2. Truyền Vùng đệm Pha 1**, **3. Xác thực Toàn vẹn Checksum**, **4. Chuyển mạch Flash Nguyên tử Pha 2**, và **5. Bắt tay Xác nhận Phiên bản**. |
| **Nhật ký Console Triển khai** | Nửa dưới cột phải | Dòng nhật ký gắn nhãn thời gian phông Monospace ghi lại từng phân hệ đã kiểm duyệt, tổng thời gian nạp (`ms`) và số phiên bản cấu hình đang chạy, đi kèm nút **Copy Log** và **Clear Log**. |
| **Xuất / Nhập Ảnh Nhị phân (`.bin`)** | `File → Export / Import → Binary (*.bin)` | Kết xuất hoặc đọc tệp ảnh cấu hình nhị phân có gắn khung kiểm tra `CRC-16` phục vụ lưu trữ ngoại tuyến và nạp sản xuất hàng loạt. |

### 2.2. Kiến trúc Nạp Nguyên tử Hai Pha An toàn (`Ctrl+F5`)

Biểu đồ trình tự dưới đây minh họa cơ chế phối hợp giữa **SynaptiX IDE** và **SynaptiX Runtime** nhằm đảm bảo tuyệt đối không bao giờ xảy ra lỗi ghi dở dang ngay cả khi sập nguồn tủ điện hoặc đứt cáp đột ngột:

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>PHA 1</span>
      </div>
      <span class="synx-node-status">PRE-FLIGHT</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Tiền Kiểm Tra</span>
        <span class="synx-node-alias">Hợp Lệ Biên Dịch</span>
        <span class="synx-node-val">Check Limits &amp; Tags</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Stream</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,13 L5,13 L5,9.5 L8,9.5 L8,6 L11,6 L11,3 L14,3" /></svg>
        <span>PHA 2</span>
      </div>
      <span class="synx-node-status">STAGING</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Vùng Đệm Cô Lập</span>
        <span class="synx-node-alias">Truyền Gói Nhị Phân</span>
        <span class="synx-node-val">Logic Cũ Vẫn Chạy</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Verify</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M10,1 L5,8.5 L8.5,8.5 L6,15 L11,7.5 L7.5,7.5 Z" /></svg>
        <span>PHA 3</span>
      </div>
      <span class="synx-node-status">CRC-16</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Đối Chiếu Toàn Vẹn</span>
        <span class="synx-node-alias">Kiểm Tra Checksum</span>
        <span class="synx-node-val">Xác Thực Mã CRC</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Swap</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 L13.5,4 L13.5,9.5 Q13.5,13.5 8,15 Q2.5,13.5 2.5,9.5 L2.5,4 Z" /></svg>
        <span>PHA 4</span>
      </div>
      <span class="synx-node-status">ATOMIC</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Chuyển Mạch Êm</span>
        <span class="synx-node-alias">Ranh Giới Scan 10ms</span>
        <span class="synx-node-val">Zero Machine Jitter</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Active</span>
  </div>

  <div class="synx-canvas-node is-step is-done">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>PHA 5</span>
      </div>
      <span class="synx-node-status is-active">● COMMIT</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Bắt Tay Xác Nhận</span>
        <span class="synx-node-alias">Ghi Nhận Phiên Bản</span>
        <span class="synx-node-val">Vận Hành Chính Thức</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

::: tip TRÍ TUỆ MÃ BĂM NGỮ NGHĨA (SEMANTIC PROGRAM HASH)
**SynaptiX IDE** tính toán **Mã băm Ngữ nghĩa** thuần túy dựa trên tập luật sau biên dịch, kiểu dữ liệu biến và cấu hình truyền thông. Mọi thao tác chỉnh sửa thẩm mỹ trên [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab)—như kéo trượt bản vẽ, căn hàng (`Alt+M`) hay dàn đều khoảng cách (`Alt+Shift+H`)—đều **không** làm sai lệch trạng thái đã đồng bộ với thiết bị.
:::

---

## 3. Quy trình Thao tác Chuẩn từng Bước

### Quy trình 3.1: Thực hiện Nạp Nguyên tử Hai Pha Xuống Thiết bị (`Ctrl+F5`)

1. Kiểm tra thanh kết nối phía trên (`F4`) để chắc chắn **SynaptiX Edge Controller** (hoặc `SIMULATOR (VIRTUAL)`) đang ở trạng thái `Connected`.
2. Nhấn `F7` để đảm bảo huy hiệu biên dịch trên [Sơ đồ Logic](/vi/workspaces/logic-graph-tab) đạt trạng thái xanh lá (`Valid`), sau đó nhấn `Ctrl+6` mở **Atomic Deploy Workspace**.
3. Rà soát **Thẻ Kê khai Triển khai (Deployment Manifest)** ở cột trái nhằm xác nhận đúng số lượng luật ECA đang bật, số bộ Timer/Counter, kênh đọc RS-485 và chủ đề MQTT.
4. Nhấn nút **Atomic Deploy** (hoặc bấm tổ hợp phím `Ctrl+F5` từ bất kỳ tab nào).
5. Quan sát **Thanh Tiến trình Nguyên tử 5 Bước** lần lượt hoàn tất qua các chặng: Tiền kiểm tra, Truyền Vùng đệm Pha 1, Đối chiếu Checksum, Chuyển mạch Flash Pha 2 và Bắt tay Phiên bản.
6. Kiểm tra dòng xác nhận `[SUCCESS]` cuối cùng trong **Nhật ký Console**, ghi nhận mã phiên bản cấu hình mới và tổng thời gian thực thi tính bằng mili-giây.

### Quy trình 3.2: Đọc Ngược & Khôi phục Dự án từ Thiết bị Hiện trường (`F6`)

1. Kết nối máy tính kỹ thuật với **SynaptiX Edge Controller** qua cổng USB CDC hoặc mạng Modbus TCP (`F4`).
2. Nhấn phím `F6` (**Upload from Device**). Nếu dự án `.synx` đang mở có thay đổi chưa lưu, IDE sẽ hiển thị hộp thoại hỏi xác nhận lưu trước khi ghi đè không gian làm việc.
3. **SynaptiX IDE** tải ngược toàn bộ luật ECA, khối chức năng, thông số cổng RS-485, cấu hình MQTT và kiểu dữ liệu biến, sau đó tự động dựng lại đầy đủ cả [Bảng Luật — Rule Table](/vi/workspaces/rule-table-tab) (`Ctrl+4`) lẫn sơ đồ khối trực quan trên [Sơ đồ Logic — Logic Graph](/vi/workspaces/logic-graph-tab) (`Ctrl+2`).
4. Nhấn ngay `Ctrl+S` để lưu trữ bản sao lưu dự phòng `.synx` kèm ngày giờ bảo trì.

### Quy trình 3.3: Đóng gói & Nạp Tệp Ảnh Nhị phân (`.bin`) Sản xuất Hàng loạt

1. Sau khi biên dịch (`F7`) và kiểm thử hoàn chỉnh dự án chuẩn, vào menu **File → Export → Binary (`*.bin`)** để xuất tệp ảnh phát hành (ví dụ: `tram_bom_tang_ap_v2.bin`). Tệp `.bin` được nhúng sẵn khung kiểm tra toàn vẹn `CRC-16`.
2. Tại bàn xuất xưởng tủ điện hàng loạt, kỹ thuật viên chỉ cần mở **File → Import → Binary (`*.bin`)**—hệ thống tự động kiểm tra chữ ký `CRC-16` ngay khi tải tệp—rồi nhấn `Ctrl+F5` nạp tuần tự cho hàng chục bộ điều khiển giống nhau với độ đồng nhất nhị phân tuyệt đối.

---

## 4. Thực hành Kỹ thuật Tốt nhất & Xử lý Tình huống Biên

::: warning XỬ LÝ CHÊNH LỆCH NĂNG LỰC PHẦN CỨNG KHI TRIỂN KHAI
Nếu một dự án được thiết kế trên cấu hình **IIoT Edge Controller** đầy đủ (có chứa khối `MQTT_PUB` hoặc cấu hình `Wi-Fi`) nhưng khi ra hiện trường lại cắm nhầm vào biến thể **RS-485 Remote I/O** không có phần cứng mạng, **Bộ Tiền Kiểm tra (Pre-Flight Verifier)** sẽ phát hiện ngay sự khác biệt năng lực—cảnh báo hoặc cô lập phân hệ mạng không tương thích trong khi vẫn bảo vệ an toàn các luật điều khiển I/O cốt lõi. Hãy luôn đối chiếu đúng hồ sơ thiết bị tại [Không gian Thiết bị — Device](/vi/workspaces/device-tab) (`Ctrl+1`) trước khi ký nghiệm thu.
:::

- **Khôi phục Nhanh Sau Sự cố Mất điện Giữa Chừng:** Nếu nguồn điện nhà xưởng bị cúp đúng lúc đang chạy **Bước 2 (Truyền Vùng đệm Pha 1)** hoặc **Bước 3 (Kiểm tra Checksum)**, bạn chỉ cần cấp điện trở lại, chờ **SynaptiX IDE** tự động chuyển từ `Reconnecting` sang `Connected` rồi nhấn lại `Ctrl+F5`. Thiết bị luôn khởi động an toàn trên bản cấu hình hợp lệ liền trước mà không bao giờ bị treo lỗi bộ nhớ.
- **Lưu trữ Nhật ký Nạp cho Biên bản Nghiệm thu (FAT/SAT):** Nhấn nút **Copy Log** ngay sau khi hoàn tất nạp cho từng tủ điều khiển để dán bản ghi thời gian và mã phiên bản vào biên bản nghiệm thu bàn giao khách hàng.

---

## Tài liệu Liên quan

- [Chu trình Thực thi Tất định](/vi/concepts/execution-pipeline)
- [1. Thiết bị — Device (`Ctrl+1`)](/vi/workspaces/device-tab)
- [4. Bảng Luật — Rule Table (`Ctrl+4`)](/vi/workspaces/rule-table-tab)
- [7. Giám sát — Live Watch (`Ctrl+7`)](/vi/workspaces/live-watch-tab)
- [Bảng Mã lỗi & Chẩn đoán Sự cố](/vi/appendices/error-codes-and-troubleshooting)
