import type { DefaultTheme, LocaleSpecificConfig } from 'vitepress'

export const viConfig: LocaleSpecificConfig<DefaultTheme.Config> = {
  title: 'Hướng dẫn Lập trình SynaptiX',
  description: 'Tài liệu Kỹ thuật & Hướng dẫn Lập trình cho Hệ sinh thái Tự động hóa SynaptiX',
  themeConfig: {
    siteTitle: 'SynaptiX Programming Guide',
    nav: [
      { text: 'Trang chủ', link: '/vi/' },
      { text: 'Bắt đầu', link: '/vi/getting-started/installation', activeMatch: '/vi/(concepts|getting-started)/' },
      { text: 'Không gian Làm việc', link: '/vi/workspaces/device-tab', activeMatch: '/vi/workspaces/' },
      { text: 'Từ điển Khối hàm', link: '/vi/blocks/overview', activeMatch: '/vi/blocks/' },
      { text: 'Công cụ & Tích hợp', link: '/vi/tools/rs485-and-scada-slave', activeMatch: '/vi/tools/' },
      { text: 'Phụ lục Tra cứu', link: '/vi/appendices/keyboard-shortcuts', activeMatch: '/vi/appendices/' }
    ],

    sidebar: [
      {
        text: '01. Tổng quan & Kiến trúc Cốt lõi',
        collapsed: false,
        items: [
          { text: 'Hệ sinh thái & Cấu hình Thiết bị', link: '/vi/concepts/ecosystem-overview' },
          { text: 'Kiến trúc Bộ nhớ Tag & Kiểu Dữ liệu', link: '/vi/concepts/data-model-and-types' },
          { text: 'Chu trình Thực thi Tất định', link: '/vi/concepts/execution-pipeline' }
        ]
      },
      {
        text: '02. Cài đặt & Khởi tạo Nhanh',
        collapsed: false,
        items: [
          { text: 'Yêu cầu Hệ thống & Cài đặt', link: '/vi/getting-started/installation' },
          { text: 'Giải phẫu Giao diện SynaptiX IDE', link: '/vi/getting-started/ui-tour' },
          { text: 'Dự án Đầu tiên trong 10 Phút', link: '/vi/getting-started/quickstart-first-project' }
        ]
      },
      {
        text: '03. Không gian Làm việc (7 Tab)',
        collapsed: false,
        items: [
          { text: '1. Thiết bị — Device (Ctrl+1)', link: '/vi/workspaces/device-tab' },
          { text: '2. Sơ đồ Logic — Logic Graph (Ctrl+2)', link: '/vi/workspaces/logic-graph-tab' },
          { text: '3. Danh mục Biến — Tag Catalog (Ctrl+3)', link: '/vi/workspaces/tag-catalog-tab' },
          { text: '4. Bảng Luật — Rule Table (Ctrl+4)', link: '/vi/workspaces/rule-table-tab' },
          { text: '5. Thư viện Mẫu — Blueprints (Ctrl+5)', link: '/vi/workspaces/blueprints-tab' },
          { text: '6. Triển khai — Deploy (Ctrl+6)', link: '/vi/workspaces/deploy-tab' },
          { text: '7. Giám sát — Live Watch (Ctrl+7)', link: '/vi/workspaces/live-watch-tab' }
        ]
      },
      {
        text: '04. Từ điển Khối hàm (Blocks)',
        collapsed: false,
        items: [
          { text: 'Tổng quan Danh mục & Quy ước Chân', link: '/vi/blocks/overview' },
          {
            text: 'Chuỗi Luật Cốt lõi (Core Pipeline)',
            collapsed: false,
            items: [
              { text: 'INPUT — Khối Nguồn Tín hiệu Tag', link: '/vi/blocks/input' },
              { text: 'TRIGGER — Phát hiện Sườn & Lịch RTC', link: '/vi/blocks/trigger' },
              { text: 'COMPARATOR — Bộ So sánh Quan hệ', link: '/vi/blocks/comparator' },
              { text: 'GUARD — Cổng Khóa Liên động An toàn', link: '/vi/blocks/guard' },
              { text: 'ACTION — Khối Thực thi Lệnh Đích', link: '/vi/blocks/action' }
            ]
          },
          {
            text: 'Định thời, Bộ đếm & Chuẩn hóa',
            collapsed: false,
            items: [
              { text: 'TON — Hẹn giờ Trễ Đóng (On-Delay)', link: '/vi/blocks/ton' },
              { text: 'TOF — Hẹn giờ Trễ Ngắt (Off-Delay)', link: '/vi/blocks/tof' },
              { text: 'TP — Phát Xung Đơn ổn (Pulse)', link: '/vi/blocks/tp' },
              { text: 'CYCLE_TIMER — Tạo Xung Chu kỳ Bất đối xứng', link: '/vi/blocks/cycle-timer' },
              { text: 'CTU — Bộ Đếm Tiến (Count Up)', link: '/vi/blocks/ctu' },
              { text: 'CTD — Bộ Đếm Lùi (Count Down)', link: '/vi/blocks/ctd' },
              { text: 'SCALE — Tuyến tính hóa & Hiệu chuẩn 2 Điểm', link: '/vi/blocks/scale' }
            ]
          },
          {
            text: 'Truyền thông Fieldbus & Xuất Phần cứng',
            collapsed: false,
            items: [
              { text: 'MB_READ — Đọc Thiết bị Modbus RTU', link: '/vi/blocks/mb-read' },
              { text: 'MB_WRITE — Ghi Chấp hành Modbus RTU', link: '/vi/blocks/mb-write' },
              { text: 'DIRECT_WRITE — Xuất DO / PWM / PTO / Bus', link: '/vi/blocks/direct-write' }
            ]
          },
          {
            text: 'Điện toán Biên IIoT & Mạng IP',
            collapsed: false,
            items: [
              { text: 'MQTT_PUB — Đẩy Telemetry lên Broker', link: '/vi/blocks/mqtt-pub' },
              { text: 'MQTT_SUB — Nhận Lệnh Điều khiển JSON', link: '/vi/blocks/mqtt-sub' },
              { text: 'HTTP_CLIENT — Giao tiếp REST Webhook', link: '/vi/blocks/http-client' },
              { text: 'TCP_CLIENT — Truyền nhận Raw Socket', link: '/vi/blocks/tcp-client' }
            ]
          }
        ]
      },
      {
        text: '05. Công Cụ Kỹ Thuật & Giao Thức',
        collapsed: false,
        items: [
          { text: 'Cấu hình RS-485 & Bản đồ SCADA Slave', link: '/vi/tools/rs485-and-scada-slave' },
          { text: 'Cấu hình MQTT TLS & Mạng Wi-Fi', link: '/vi/tools/mqtt-and-wifi-config' },
          { text: 'Mạng Không dây Zigbee Mesh', link: '/vi/tools/zigbee-mesh-config' },
          { text: 'Không gian DTU Gateway Studio', link: '/vi/tools/dtu-gateway-studio' },
          { text: 'Mô phỏng Offline & Gỡ lỗi Trực tuyến', link: '/vi/tools/simulator-and-live-debug' },
          { text: 'Trợ lý Kỹ thuật SynaptiX AI Copilot', link: '/vi/tools/ai-copilot' },
          { text: 'Kịch bản Tích hợp Thực tế (App Notes)', link: '/vi/tools/application-notes' }
        ]
      },
      {
        text: '06. Phụ lục Tra cứu Nhanh',
        collapsed: false,
        items: [
          { text: 'Bảng Phím tắt & Thao tác Canvas', link: '/vi/appendices/keyboard-shortcuts' },
          { text: 'Từ điển Macro & Giới hạn Tài nguyên', link: '/vi/appendices/macro-tokens-and-limits' },
          { text: 'Bảng Mã lỗi & Chẩn đoán Sự cố', link: '/vi/appendices/error-codes-and-troubleshooting' }
        ]
      }
    ],

    outline: {
      level: [2, 3],
      label: 'Mục lục trang này'
    },
    docFooter: {
      prev: 'Mục trước',
      next: 'Mục tiếp theo'
    },
    darkModeSwitchLabel: 'Giao diện',
    lightModeSwitchTitle: 'Chuyển sang giao diện sáng',
    darkModeSwitchTitle: 'Chuyển sang giao diện tối công nghiệp',
    sidebarMenuLabel: 'Cây danh mục Tài liệu',
    returnToTopLabel: 'Lên đầu trang',
    langMenuLabel: 'Ngôn ngữ',
    footer: {
      message: 'Biên soạn theo Tiêu chuẩn Tài liệu Kỹ thuật Quốc tế IEC/IEEE 82079-1:2019, ISO/IEC/IEEE 26514 & ANSI Z535.6',
      copyright: 'Bản quyền © 2026 SynaptiX Automation Systems Group. Bảo lưu mọi quyền.'
    }
  }
}
