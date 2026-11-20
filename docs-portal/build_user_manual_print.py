#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
SynaptiX Industrial Documentation Multi-Pass Print Compiler
Builds international corporate-grade User Manuals for SynaptiX IDE with 100% complete technical depth.
Standards: ISO/IEC/IEEE 26514:2022, IEC/IEEE 82079-1:2019, ANSI Z535.6, ANSI/ISA-101
Official Corporate Identity: SYNAPTIX TECHNOLOGY JSC (synaptix-io.com)
Typography: Pure Segoe UI (Regular 400 & Bold 700) - Zero Font Switching Artifacts
"""

import os
import re
import sys
import subprocess
from markdown_it import MarkdownIt
import fitz

BASE_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DOCS_PORTAL = os.path.join(BASE_DIR, "docs-portal")
PUBLIC_DIR = os.path.join(DOCS_PORTAL, "public")
DOWNLOADS_DIR = os.path.join(PUBLIC_DIR, "downloads")
CHROME_PATH = r"C:\Program Files\Google\Chrome\Application\chrome.exe"

os.makedirs(DOWNLOADS_DIR, exist_ok=True)

# ---------------------------------------------------------------------------
# Chapter & File Mapping (All 40 Technical Sections - 100% Information Depth)
# ---------------------------------------------------------------------------
CHAPTER_MAPPINGS = {
    "vi": [
        {
            "id": "ch1",
            "num": 1,
            "title": "Chương 1: Giới thiệu Hệ sinh thái SynaptiX IIoT & Kiến trúc Cốt lõi",
            "files": [
                ("1.1", "Tổng quan Nền tảng & Danh mục Thiết bị SynaptiX IIoT", "concepts/ecosystem-overview.md"),
                ("1.2", "Kiến trúc Bộ nhớ Tag & Chu trình Quét Thực thi Tất định", "concepts/data-model-and-types.md"),
                ("1.3", "Động cơ Quét Logic & Pipeline Thực thi Tuyến tính", "concepts/execution-pipeline.md"),
            ]
        },
        {
            "id": "ch2",
            "num": 2,
            "title": "Chương 2: Cài đặt Phần mềm & Khởi tạo Nhanh",
            "files": [
                ("2.1", "Cài đặt SynaptiX IDE & Thiết lập Môi trường Vận hành", "getting-started/installation.md"),
                ("2.2", "Tổng quan Giải phẫu Giao diện Người dùng IDE", "getting-started/ui-tour.md"),
                ("2.3", "Khởi tạo Dự án Đầu tiên trong 10 Phút", "getting-started/quickstart-first-project.md"),
            ]
        },
        {
            "id": "ch3",
            "num": 3,
            "title": "Chương 3: 7 Phân hệ Không gian Làm việc (Workspaces)",
            "files": [
                ("3.1", "Không gian Thiết bị (Device Workspace)", "workspaces/device-tab.md"),
                ("3.2", "Không gian Sơ đồ Logic (Logic Graph Canvas)", "workspaces/logic-graph-tab.md"),
                ("3.3", "Danh mục Biến (Tag Catalog)", "workspaces/tag-catalog-tab.md"),
                ("3.4", "Bảng Luật Logic (Rule Table)", "workspaces/rule-table-tab.md"),
                ("3.5", "Thư viện Bản mẫu (Blueprints)", "workspaces/blueprints-tab.md"),
                ("3.6", "Phân hệ Triển khai Nạp (Deploy Gate)", "workspaces/deploy-tab.md"),
                ("3.7", "Giám sát & Đồ thị Thời gian thực (Live Watch)", "workspaces/live-watch-tab.md"),
            ]
        },
        {
            "id": "ch4",
            "num": 4,
            "title": "Chương 4: Thiết kế Sơ đồ Logic Khối hàm FBD Canvas",
            "files": [
                ("4.1", "Tổng quan Danh mục Khối & Quy ước Ghép nối", "blocks/overview.md"),
                ("4.2", "INPUT — Khối Nguồn Tín hiệu Tag", "blocks/input.md"),
                ("4.3", "TRIGGER — Phát hiện Sườn Xung & Lịch RTC", "blocks/trigger.md"),
                ("4.4", "COMPARATOR — Bộ So sánh Quan hệ & Ngưỡng", "blocks/comparator.md"),
                ("4.5", "GUARD — Cổng Khóa Liên động An toàn", "blocks/guard.md"),
                ("4.6", "ACTION — Khối Thực thi Lệnh Chấp hành", "blocks/action.md"),
            ]
        },
        {
            "id": "ch5",
            "num": 5,
            "title": "Chương 5: Thư viện Khối hàm Điều khiển & Toán học",
            "files": [
                ("5.1", "Khối TON — Hẹn giờ Trễ Đóng (On-Delay Timer)", "blocks/ton.md"),
                ("5.2", "Khối TOF — Hẹn giờ Trễ Ngắt (Off-Delay Timer)", "blocks/tof.md"),
                ("5.3", "Khối TP — Phát Xung Đơn ổn (Monostable Pulse)", "blocks/tp.md"),
                ("5.4", "Khối CYCLE_TIMER — Tạo Chuỗi Xung Chu kỳ Bất đối xứng", "blocks/cycle-timer.md"),
                ("5.5", "Khối CTU & CTD — Bộ Đếm Tiến và Đếm Lùi", "blocks/ctu.md"),
                ("5.6", "Khối SCALE — Tuyến tính hóa & Hiệu chuẩn Cảm biến", "blocks/scale.md"),
                ("5.7", "Khối MATH — Khối Tính toán Số học (ALU)", "blocks/math.md"),
            ]
        },
        {
            "id": "ch6",
            "num": 6,
            "title": "Chương 6: Truyền thông Công nghiệp & Điện toán Biên IIoT",
            "files": [
                ("6.1", "Khối MB_READ & MB_WRITE — Đọc Ghi Thiết bị Modbus RTU", "blocks/mb-read.md"),
                ("6.2", "Khối DIRECT_WRITE — Xuất Tín hiệu DO, PWM & Xung PTO", "blocks/direct-write.md"),
                ("6.3", "Cấu hình Mạng RS-485 & Bản đồ SCADA Slave", "tools/rs485-and-scada-slave.md"),
                ("6.4", "Khối MQTT_PUB & MQTT_SUB — Viễn trắc Đám mây & Điều khiển JSON", "blocks/mqtt-pub.md"),
                ("6.5", "Khối HTTP_CLIENT & TCP_CLIENT — REST Webhook & Raw Socket", "blocks/http-client.md"),
                ("6.6", "Cấu hình Chứng chỉ MQTT TLS X.509 & Mạng Wi-Fi", "tools/mqtt-and-wifi-config.md"),
            ]
        },
        {
            "id": "ch7",
            "num": 7,
            "title": "Chương 7: DTU Gateway Studio & Mạng Không dây Zigbee Mesh",
            "files": [
                ("7.1", "DTU Gateway Studio & Trình Cấu hình Nhanh", "tools/dtu-gateway-studio.md"),
                ("7.2", "Mạng Không dây Zigbee Mesh & Thu thập Cảm biến Thông minh", "tools/zigbee-mesh-config.md"),
            ]
        },
        {
            "id": "ch8",
            "num": 8,
            "title": "Chương 8: Mô phỏng Offline & Nạp An toàn Xuống Thiết bị",
            "files": [
                ("8.1", "Chế độ Mô phỏng Logic Offline & Gỡ lỗi Điểm dừng", "tools/simulator-and-live-debug.md"),
            ]
        },
        {
            "id": "ch9",
            "num": 9,
            "title": "Chương 9: Giám sát Trực tiếp (Live Watch) & AI Copilot",
            "files": [
                ("9.1", "Trợ lý Kỹ thuật SynaptiX AI Copilot Hỗ trợ Thiết kế", "tools/ai-copilot.md"),
            ]
        },
        {
            "id": "ch10",
            "num": 10,
            "title": "Chương 10: Kịch bản Ứng dụng Thực tế (Application Notes)",
            "files": [
                ("10.1", "Kịch bản Vận hành Công nghiệp: Trạm Bơm & Giám sát Môi trường", "tools/application-notes.md"),
            ]
        },
        {
            "id": "app_a",
            "num": "A",
            "title": "Phụ lục A: Bảng Phím tắt Thao tác & Phím Nóng trên Canvas",
            "files": [
                ("A.1", "Danh mục Phím tắt & Thao tác Chuột Nhanh", "appendices/keyboard-shortcuts.md"),
            ]
        },
        {
            "id": "app_b",
            "num": "B",
            "title": "Phụ lục B: Bảng Mã lỗi Hệ thống, Cảnh báo & Xử lý Sự cố",
            "files": [
                ("B.1", "Bảng Mã Lỗi & Chẩn đoán Sự cố Vận hành", "appendices/error-codes-and-troubleshooting.md"),
                ("B.2", "Từ điển Macro Payload & Giới hạn Tài nguyên Bộ nhớ", "appendices/macro-tokens-and-limits.md"),
            ]
        },
    ],
    "en": [
        {
            "id": "ch1",
            "num": 1,
            "title": "Chapter 1: SynaptiX IIoT Ecosystem & Core Architecture",
            "files": [
                ("1.1", "Platform Overview & SynaptiX IIoT Device Portfolio", "concepts/ecosystem-overview.md"),
                ("1.2", "Unified Tag Memory Architecture & Deterministic Scan", "concepts/data-model-and-types.md"),
                ("1.3", "Deterministic Execution Pipeline & Runtime Engine", "concepts/execution-pipeline.md"),
            ]
        },
        {
            "id": "ch2",
            "num": 2,
            "title": "Chapter 2: Software Setup & Quickstart Guide",
            "files": [
                ("2.1", "System Requirements & SynaptiX IDE Installation", "getting-started/installation.md"),
                ("2.2", "IDE User Interface Anatomy & Workspace Tour", "getting-started/ui-tour.md"),
                ("2.3", "10-Minute First Project Quickstart", "getting-started/quickstart-first-project.md"),
            ]
        },
        {
            "id": "ch3",
            "num": 3,
            "title": "Chapter 3: The 7 Engineering Workspaces",
            "files": [
                ("3.1", "Device Hardware Configuration Workspace", "workspaces/device-tab.md"),
                ("3.2", "Visual Logic Graph Design Canvas", "workspaces/logic-graph-tab.md"),
                ("3.3", "Global Tag Catalog Management", "workspaces/tag-catalog-tab.md"),
                ("3.4", "Declarative Rule Table Workspace", "workspaces/rule-table-tab.md"),
                ("3.5", "Reusable Blueprints Template Library", "workspaces/blueprints-tab.md"),
                ("3.6", "Atomic Deploy & Verification Gate", "workspaces/deploy-tab.md"),
                ("3.7", "Live Watch, Telemetry & Real-time Trends", "workspaces/live-watch-tab.md"),
            ]
        },
        {
            "id": "ch4",
            "num": 4,
            "title": "Chapter 4: Visual FBD Logic Canvas Design",
            "files": [
                ("4.1", "Block Dictionary Overview & Wiring Conventions", "blocks/overview.md"),
                ("4.2", "INPUT — Tag Source Signal Block", "blocks/input.md"),
                ("4.3", "TRIGGER — Edge Detection & RTC Scheduling", "blocks/trigger.md"),
                ("4.4", "COMPARATOR — Relational & Threshold Evaluator", "blocks/comparator.md"),
                ("4.5", "GUARD — Safety Interlock Gate", "blocks/guard.md"),
                ("4.6", "ACTION — Target Command Executor Block", "blocks/action.md"),
            ]
        },
        {
            "id": "ch5",
            "num": 5,
            "title": "Chapter 5: Control & Math Function Block Library",
            "files": [
                ("5.1", "TON — On-Delay Timer Block", "blocks/ton.md"),
                ("5.2", "TOF — Off-Delay Timer Block", "blocks/tof.md"),
                ("5.3", "TP — Monostable Pulse Timer Block", "blocks/tp.md"),
                ("5.4", "CYCLE_TIMER — Asymmetric Oscillator Block", "blocks/cycle-timer.md"),
                ("5.5", "CTU & CTD — Up and Down Counters", "blocks/ctu.md"),
                ("5.6", "SCALE — Linear Sensor Calibration & Conversion", "blocks/scale.md"),
                ("5.7", "MATH — Combinational Arithmetic ALU Block", "blocks/math.md"),
            ]
        },
        {
            "id": "ch6",
            "num": 6,
            "title": "Chapter 6: Industrial Fieldbus & IIoT Edge Computing",
            "files": [
                ("6.1", "MB_READ & MB_WRITE — Modbus RTU Communication", "blocks/mb-read.md"),
                ("6.2", "DIRECT_WRITE — Hardware DO, PWM & PTO Pulse Output", "blocks/direct-write.md"),
                ("6.3", "Multi-Port RS-485 Setup & SCADA Slave Mapping", "tools/rs485-and-scada-slave.md"),
                ("6.4", "MQTT_PUB & MQTT_SUB — Cloud Telemetry & JSON Commands", "blocks/mqtt-pub.md"),
                ("6.5", "HTTP_CLIENT & TCP_CLIENT — REST Webhook & Raw Socket", "blocks/http-client.md"),
                ("6.6", "MQTT TLS X.509 Certificate Provisioning & Wi-Fi", "tools/mqtt-and-wifi-config.md"),
            ]
        },
        {
            "id": "ch7",
            "num": 7,
            "title": "Chapter 7: DTU Gateway Studio & Wireless Zigbee Mesh",
            "files": [
                ("7.1", "DTU Gateway Studio & Quick Setup Wizard", "tools/dtu-gateway-studio.md"),
                ("7.2", "Industrial Zigbee Mesh Network & Smart Sensor Acquisition", "tools/zigbee-mesh-config.md"),
            ]
        },
        {
            "id": "ch8",
            "num": 8,
            "title": "Chapter 8: Offline Simulation & Safe Atomic Deploy",
            "files": [
                ("8.1", "Offline Logic Simulation & Interactive Breakpoint Debugging", "tools/simulator-and-live-debug.md"),
            ]
        },
        {
            "id": "ch9",
            "num": 9,
            "title": "Chapter 9: Real-time Live Watch & SynaptiX AI Copilot",
            "files": [
                ("9.1", "SynaptiX AI Copilot Engineering Assistant", "tools/ai-copilot.md"),
            ]
        },
        {
            "id": "ch10",
            "num": 10,
            "title": "Chapter 10: Real-world Industrial Application Notes",
            "files": [
                ("10.1", "Industrial Integration Notes: Pump Alternation & Water Monitoring", "tools/application-notes.md"),
            ]
        },
        {
            "id": "app_a",
            "num": "A",
            "title": "Appendix A: IDE Keyboard Shortcuts & Canvas Hotkeys",
            "files": [
                ("A.1", "Comprehensive Keyboard Shortcut & Mouse Gesture Map", "appendices/keyboard-shortcuts.md"),
            ]
        },
        {
            "id": "app_b",
            "num": "B",
            "title": "Appendix B: Diagnostic Error Codes & Troubleshooting",
            "files": [
                ("B.1", "System Fault Code Dictionary & Corrective Procedures", "appendices/error-codes-and-troubleshooting.md"),
                ("B.2", "Payload Macro Tokens & Memory Capacity Limits", "appendices/macro-tokens-and-limits.md"),
            ]
        },
    ]
}

# ---------------------------------------------------------------------------
# Master CSS (Pure Segoe UI, Zero Font Switching, International Industrial Palette)
# ---------------------------------------------------------------------------
MASTER_CSS = """
    @page {
      size: A4 portrait;
      margin: 18mm 14mm 18mm 16mm;
    }

    * {
      box-sizing: border-box;
      -webkit-print-color-adjust: exact;
      print-color-adjust: exact;
    }

    html, body {
      margin: 0;
      padding: 0;
      font-family: "Segoe UI", -apple-system, BlinkMacSystemFont, "Helvetica Neue", Arial, sans-serif;
      font-size: 8.8pt;
      line-height: 1.40;
      color: #1e293b;
      background-color: #ffffff;
    }

    /* PRINT LINKS: In an industrial manual, print links must NEVER be bright blue web hyperlinks */
    a {
      color: inherit !important;
      text-decoration: none !important;
      font-weight: 600;
    }

    /* TYPOGRAPHY HIERARCHY: Balanced Semibold (600) for inline emphasis to eliminate harsh ink-blot contrast */
    strong, b {
      font-family: inherit;
      font-weight: 600;
      color: #0f172a;
    }

    h1, h2, h3, h4, h5 {
      font-family: inherit;
      color: #0f172a;
      font-weight: 700;
      line-height: 1.25;
      margin: 0;
    }

    /* TRANG BÌA (FRONT COVER) - TỐI GIẢN & SANG TRỌNG */
    .page-sheet-cover {
      width: 180mm;
      height: 258mm;
      max-height: 258mm;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      page-break-after: always;
      padding: 4mm 0 2mm 0;
    }

    .cover-top {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-bottom: 5mm;
      border: none !important;
    }

    .cover-logo {
      height: 14mm;
      width: auto;
      object-fit: contain;
      border: none !important;
      border-radius: 0 !important;
      box-shadow: none !important;
      background: transparent !important;
      margin: 0 !important;
      padding: 0 !important;
    }

    .cover-corp-tag {
      font-size: 8pt;
      font-weight: 700;
      letter-spacing: 1px;
      color: #004f71;
      text-transform: uppercase;
    }

    .cover-content {
      flex: 1 1 auto;
      display: flex;
      flex-direction: column;
      justify-content: center;
      padding-top: 10mm;
      padding-bottom: 15mm;
    }

    .cover-doc-category {
      font-size: 9pt;
      font-weight: 700;
      letter-spacing: 1.5px;
      color: #1e6091;
      text-transform: uppercase;
      margin-bottom: 4mm;
    }

    .cover-product-title {
      font-size: 32pt;
      font-weight: 700;
      color: #004f71;
      line-height: 1.05;
      letter-spacing: -0.5px;
      margin: 0 0 3mm 0;
    }

    .cover-product-subtitle {
      font-size: 15pt;
      font-weight: 700;
      color: #0f172a;
      line-height: 1.30;
      margin: 0 0 5mm 0;
    }

    .cover-title-divider {
      width: 45mm;
      height: 2.5pt;
      background-color: #004f71;
      margin: 0 0 5mm 0;
      border-radius: 1px;
    }

    .cover-product-lead {
      font-size: 9.5pt;
      color: #475569;
      line-height: 1.50;
      max-width: 160mm;
      margin: 0;
    }

    .cover-footer {
      border-top: 0.75pt solid #cbd5e1;
      padding-top: 3.5mm;
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 8.5pt;
      color: #64748b;
    }

    .cover-footer-left {
      font-weight: 700;
      color: #004f71;
    }

    .cover-footer-right {
      color: #475569;
      font-weight: 600;
    }

    /* TRANG 2: KIỂM SOÁT TÀI LIỆU & PHÁP LÝ (DOCUMENT CONTROL) */
    .page-sheet-doc-control {
      width: 180mm;
      height: 258mm;
      page-break-after: always;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      padding-top: 2mm;
    }

    .doc-control-title {
      font-size: 14pt;
      font-weight: 700;
      color: #0f172a;
      border-bottom: none !important;
      padding-bottom: 0;
      margin: 0 0 5mm 0;
    }

    .doc-control-section {
      margin-bottom: 4mm;
    }

    .doc-control-heading {
      font-size: 8.5pt;
      font-weight: 700;
      color: #0f172a;
      text-transform: uppercase;
      letter-spacing: 0.6px;
      margin: 0 0 1.5mm 0;
      border-bottom: 0.5pt solid #e2e8f0;
      padding-bottom: 0.8mm;
    }

    .doc-control-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 8.2pt;
      margin-bottom: 1mm;
    }

    .doc-control-table td, .doc-control-table th {
      padding: 1.6mm 2mm;
      border-bottom: 0.5pt solid #e2e8f0;
      vertical-align: top;
      line-height: 1.35;
    }

    .doc-control-table th {
      background-color: #f1f5f9;
      color: #0f172a;
      font-weight: 700;
      text-align: left;
    }

    .doc-control-key {
      width: 28%;
      font-weight: 600;
      color: #475569;
    }

    .doc-control-val {
      width: 72%;
      color: #1e293b;
    }

    .doc-control-legal-text {
      font-size: 7.8pt;
      color: #64748b;
      line-height: 1.40;
      text-align: justify;
      margin-top: 2mm;
    }

    /* TRANG 3: QUY ƯỚC AN TOÀN CÔNG NGHIỆP (ANSI Z535.6) */
    .page-sheet-safety {
      width: 180mm;
      height: 258mm;
      page-break-after: always;
      padding-top: 2mm;
    }

    .safety-title {
      font-size: 14pt;
      font-weight: 700;
      color: #0f172a;
      border-bottom: none !important;
      padding-bottom: 0;
      margin: 0 0 5mm 0;
    }

    .safety-lead {
      font-size: 8.8pt;
      color: #334155;
      line-height: 1.45;
      margin-bottom: 4mm;
    }

    .safety-box {
      margin-bottom: 3.5mm;
      padding: 2.5mm 3.5mm;
      border-radius: 3px;
      page-break-inside: avoid;
      font-size: 8.2pt;
      line-height: 1.40;
    }

    .safety-box-danger {
      background-color: #fef2f2;
      border-left: 3pt solid #dc2626;
      border-top: 0.5pt solid #fecaca;
      border-right: 0.5pt solid #fecaca;
      border-bottom: 0.5pt solid #fecaca;
    }

    .safety-box-warning {
      background-color: #fffbeb;
      border-left: 3pt solid #d97706;
      border-top: 0.5pt solid #fde68a;
      border-right: 0.5pt solid #fde68a;
      border-bottom: 0.5pt solid #fde68a;
    }

    .safety-box-caution {
      background-color: #fff7ed;
      border-left: 3pt solid #ea580c;
      border-top: 0.5pt solid #fed7aa;
      border-right: 0.5pt solid #fed7aa;
      border-bottom: 0.5pt solid #fed7aa;
    }

    .safety-box-notice {
      background-color: #eff6ff;
      border-left: 3pt solid #0284c7;
      border-top: 0.5pt solid #bfdbfe;
      border-right: 0.5pt solid #bfdbfe;
      border-bottom: 0.5pt solid #bfdbfe;
    }

    .safety-box-header {
      display: flex;
      align-items: center;
      gap: 2mm;
      margin-bottom: 1mm;
      font-weight: 700;
      font-size: 8.5pt;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }

    .safety-box-danger .safety-box-header { color: #991b1b; }
    .safety-box-warning .safety-box-header { color: #92400e; }
    .safety-box-caution .safety-box-header { color: #9a3412; }
    .safety-box-notice .safety-box-header { color: #075985; }

    .safety-rules-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 3mm;
      margin-top: 4mm;
    }

    .safety-rule-card {
      background-color: #f8fafc;
      border: 0.5pt solid #cbd5e1;
      border-radius: 3px;
      padding: 2.5mm 3mm;
      font-size: 8pt;
      line-height: 1.35;
    }

    .safety-rule-card strong {
      display: block;
      color: #0f172a;
      font-size: 8.5pt;
      margin-bottom: 1mm;
    }

    /* TRANG MỤC LỤC CHUẨN XÁC (TABLE OF CONTENTS) */
    .page-sheet-toc {
      width: 180mm;
      height: 258mm;
      page-break-after: always;
      display: flex;
      flex-direction: column;
      justify-content: flex-start;
      padding-top: 2mm;
    }

    .toc-heading {
      font-size: 14pt;
      font-weight: 700;
      color: #0f172a;
      margin: 0 0 4.5mm 0;
      padding-bottom: 0;
      border-bottom: none !important;
    }

    .toc-list {
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .toc-item-ch {
      display: flex;
      align-items: baseline;
      font-size: 8.8pt;
      font-weight: 700;
      color: #0f172a;
      margin-top: 2.2mm;
      margin-bottom: 0.6mm;
    }

    .toc-item-sub {
      display: flex;
      align-items: baseline;
      font-size: 8pt;
      font-weight: 400;
      color: #334155;
      padding-left: 4.5mm;
      margin-bottom: 0.5mm;
    }

    .toc-title { flex-shrink: 0; }
    .toc-dots {
      flex-grow: 1;
      border-bottom: 0.5pt dotted #94a3b8;
      margin: 0 2mm;
      position: relative;
      top: -2px;
    }
    .toc-page {
      flex-shrink: 0;
      font-weight: 700;
      color: #0f172a;
    }

    /* BODY CHAPTERS & SECTIONS */
    .chapter-start {
      page-break-before: always;
      padding-top: 2mm;
    }

    .chapter-main-title {
      font-size: 15pt;
      font-weight: 700;
      color: #0f172a;
      margin: 0 0 5mm 0;
      padding-bottom: 0;
      border-bottom: none !important;
    }

    .section-container {
      margin-bottom: 5.5mm;
    }

    .section-title {
      font-size: 11pt;
      font-weight: 700;
      color: #0f172a;
      margin: 4.5mm 0 2mm 0;
      padding-bottom: 0.8mm;
      border-bottom: none !important;
      break-after: avoid !important;
      page-break-after: avoid !important;
      break-inside: avoid !important;
      page-break-inside: avoid !important;
    }

    /* HORIZONTAL RULES: Industrial print manuals must NEVER have web divider lines */
    hr {
      display: none !important;
      height: 0 !important;
      border: none !important;
      margin: 0 !important;
      padding: 0 !important;
    }

    .section-title .sec-num {
      color: inherit;
      margin-right: 1.5mm;
    }

    .subsection-title {
      font-size: 9.8pt;
      font-weight: 700;
      color: #0f172a;
      margin: 3.5mm 0 1.5mm 0;
      break-after: avoid !important;
      page-break-after: avoid !important;
      break-inside: avoid !important;
      page-break-inside: avoid !important;
    }

    h4.sub-subsection-title, h4, h5, h6 {
      font-size: 9pt;
      font-weight: 700;
      color: #0f172a;
      margin: 2.5mm 0 1.2mm 0;
      break-after: avoid !important;
      page-break-after: avoid !important;
      break-inside: avoid !important;
      page-break-inside: avoid !important;
    }

    p {
      margin: 0 0 2mm 0;
      text-align: justify;
      line-height: 1.40;
      orphans: 3 !important;
      widows: 3 !important;
    }

    /* Keep introductory text together with following lists, tables, diagrams */
    p:has(+ ol), p:has(+ ul), p:has(+ table), p:has(+ pre),
    p:has(+ .synx-node-chain), p:has(+ .synx-pipeline-wrap), p:has(+ .synx-ide-mockup),
    h3:has(+ ol), h4:has(+ ol), h5:has(+ ol),
    h3:has(+ ul), h4:has(+ ul), h5:has(+ ul) {
      break-after: avoid !important;
      page-break-after: avoid !important;
    }

    ul, ol {
      margin: 0 0 2.5mm 0;
      padding-left: 5mm;
    }

    /* Short lists and step-by-step procedures (<= 6 steps) are kept together to prevent page splitting mid-procedure */
    ol:not(:has(> li:nth-child(7))),
    ul:not(:has(> li:nth-child(7))) {
      break-inside: avoid !important;
      page-break-inside: avoid !important;
    }

    li {
      break-inside: avoid !important;
      page-break-inside: avoid !important;
      margin-bottom: 1mm;
      line-height: 1.35;
      orphans: 3 !important;
      widows: 3 !important;
    }

    /* BẢNG BIỂU CHUẨN IBM CARBON DATA TABLES */
    table {
      width: 100%;
      border-collapse: collapse;
      margin: 2.5mm 0 3.5mm 0;
      font-size: 8pt;
      break-inside: avoid !important;
      page-break-inside: avoid !important;
    }

    tr {
      break-inside: avoid !important;
      page-break-inside: avoid !important;
    }

    th {
      background-color: #f1f5f9;
      color: #0f172a;
      font-weight: 700;
      text-align: left;
      padding: 1.8mm 2mm;
      border-top: 1pt solid #475569;
      border-bottom: 0.75pt solid #cbd5e1;
    }

    td {
      padding: 1.6mm 2mm;
      border-bottom: 0.5pt solid #e2e8f0;
      vertical-align: top;
      color: #334155;
      line-height: 1.30;
    }

    tr:nth-child(even) td {
      background-color: #f8fafc;
    }

    /* BẢNG THÔNG SỐ 2 CỘT COMPACT (DATASHEET STYLE) */
    .spec-table-2col {
      width: 100%;
      border-collapse: collapse;
      margin: 2.5mm 0 3.5mm 0;
      font-size: 8pt;
      page-break-inside: avoid;
    }

    .spec-table-2col th {
      background-color: #004f71;
      color: #ffffff;
      padding: 1.5mm 2mm;
      font-size: 8pt;
      font-weight: 700;
      border: none;
    }

    .spec-table-2col td {
      padding: 1.5mm 2mm;
      border-bottom: 0.5pt solid #e2e8f0;
      line-height: 1.30;
    }

    .spec-table-2col .col-pin {
      width: 25%;
      font-weight: 700;
      color: #004f71;
      background-color: #f8fafc;
      border-right: 0.5pt solid #e2e8f0;
    }

    .spec-table-2col .col-desc {
      width: 75%;
      color: #1e293b;
    }

    /* CALLOUT BOXES (ANSI Z535.6) */
    .callout {
      margin: 2.5mm 0 3.5mm 0;
      padding: 2.2mm 3.2mm;
      border-radius: 2px;
      break-inside: avoid !important;
      page-break-inside: avoid !important;
      font-size: 8pt;
      line-height: 1.35;
      background-color: #f8fafc;
      border: 0.5pt solid #cbd5e1;
    }

    .callout-tip {
      border-left: 2pt solid #004f71;
    }

    .callout-warning {
      border-left: 2pt solid #c2410c;
    }

    .callout-danger {
      border-left: 2pt solid #b91c1c;
    }

    .callout-info {
      border-left: 2pt solid #475569;
    }

    .callout-header {
      font-weight: 700;
      margin-bottom: 1mm;
      display: flex;
      align-items: center;
      gap: 1.5mm;
      font-size: 8pt;
      letter-spacing: 0.3px;
      text-transform: uppercase;
    }

    .callout-tip .callout-header { color: #004f71; }
    .callout-warning .callout-header { color: #9a3412; }
    .callout-danger .callout-header { color: #991b1b; }
    .callout-info .callout-header { color: #334155; }

    /* CODE, KEYBOARD & BLOCKS: Pure 100% Segoe UI for Zero Font Switching */
    pre, code, kbd, samp {
      font-family: "Segoe UI", -apple-system, BlinkMacSystemFont, "Helvetica Neue", Arial, sans-serif !important;
    }

    code {
      font-family: "Segoe UI", -apple-system, BlinkMacSystemFont, "Helvetica Neue", Arial, sans-serif !important;
      font-size: inherit;
      background: transparent !important;
      padding: 0 !important;
      border: none !important;
      border-radius: 0 !important;
      color: #0f172a !important;
      font-weight: 400 !important;
    }

    kbd {
      font-family: "Segoe UI", -apple-system, BlinkMacSystemFont, "Helvetica Neue", Arial, sans-serif !important;
      font-size: 7.8pt !important;
      font-weight: 500 !important;
      padding: 0.5mm 1.5mm !important;
      border: 0.5pt solid #cbd5e1 !important;
      border-radius: 2px !important;
      background-color: #f1f5f9 !important;
      color: #0f172a !important;
      display: inline-block !important;
      line-height: 1.2 !important;
    }

    pre {
      background-color: #f8fafc;
      border: 0.5pt solid #cbd5e1;
      border-radius: 2px;
      padding: 2.5mm 3.2mm;
      margin: 2.5mm 0 3.5mm 0;
      overflow-x: auto;
      break-inside: avoid !important;
      page-break-inside: avoid !important;
      font-family: "Segoe UI", -apple-system, BlinkMacSystemFont, "Helvetica Neue", Arial, sans-serif !important;
    }

    pre code {
      background: transparent !important;
      padding: 0 !important;
      border: none !important;
      font-family: "Segoe UI", -apple-system, BlinkMacSystemFont, "Helvetica Neue", Arial, sans-serif !important;
      font-size: 8.3pt !important;
      line-height: 1.45 !important;
      color: #1e293b !important;
      font-weight: 500 !important;
      white-space: pre-wrap !important;
      word-break: break-word !important;
    }

    /* HÌNH ẢNH & ĐỊNH DANH CAPTION */
    img {
      max-width: 96%;
      max-height: 100mm;
      height: auto;
      display: block;
      margin: 3mm auto;
      border: 0.5pt solid #cbd5e1;
      border-radius: 2px;
      break-inside: avoid !important;
      page-break-inside: avoid !important;
    }

    .figure-caption {
      font-size: 7.8pt;
      font-style: italic;
      color: #64748b;
      margin-top: 1.5mm;
      text-align: center;
    }

    .table-caption {
      font-size: 8pt;
      font-weight: 700;
      color: #004f71;
      margin-bottom: 1.2mm;
      text-align: left;
    }

    /* INDUSTRIAL SCHEMATIC CIRCUITS (.synx-node-chain) */
    .synx-node-chain {
      display: flex;
      align-items: stretch;
      justify-content: space-between;
      gap: 1.5mm;
      margin: 3mm 0;
      padding: 2mm 1.5mm;
      background-color: #f8fafc;
      border: 0.5pt solid #cbd5e1;
      border-radius: 2px;
      break-inside: avoid !important;
      page-break-inside: avoid !important;
    }

    /* Hide oversized icons, duplicate pins and badges */
    .synx-node-icon,
    .synx-node-header svg,
    .synx-node-status,
    .synx-node-alias,
    .synx-node-pin,
    .synx-wire-badge {
      display: none !important;
    }

    .synx-canvas-node {
      flex: 1 1 0;
      background-color: #ffffff;
      border: 0.75pt solid #cbd5e1;
      border-radius: 2px;
      box-shadow: none;
      display: flex;
      flex-direction: column;
      min-width: 0;
    }

    .synx-node-header {
      padding: 1.5mm 1.5mm;
      font-size: 7.2pt;
      font-weight: 700;
      color: #0f172a;
      background-color: #f1f5f9;
      border-bottom: 0.5pt solid #cbd5e1;
      text-align: center;
      text-transform: uppercase;
      letter-spacing: 0.3px;
    }

    .synx-node-body {
      padding: 2mm 1.5mm;
      font-size: 7pt;
      line-height: 1.30;
      color: #334155;
      text-align: center;
      background-color: #ffffff;
      flex: 1 1 auto;
      display: flex;
      flex-direction: column;
      justify-content: center;
    }

    .synx-node-tag {
      font-weight: 700;
      color: #004f71;
      display: block;
      font-family: inherit;
      font-size: 7.2pt;
      margin-bottom: 0.5mm;
    }

    .synx-node-val {
      font-weight: 400;
      color: #64748b;
      display: block;
      font-family: inherit;
      font-size: 6.8pt;
    }

    .synx-node-wire {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 4mm;
      flex-shrink: 0;
    }

    .synx-wire-line {
      width: 100%;
      height: 0.75pt;
      background-color: #94a3b8;
    }

    /* 3-TIER ARCHITECTURE GRID (Equal 3-column layout) */
    .synx-arch-grid {
      display: grid;
      grid-template-columns: 1fr 1fr 1fr;
      gap: 3mm;
      margin: 3mm 0;
      break-inside: avoid !important;
      page-break-inside: avoid !important;
    }

    .synx-arch-card {
      background-color: #f8fafc;
      border: 0.5pt solid #cbd5e1;
      border-radius: 2px;
      padding: 2.2mm 2.8mm;
      display: flex;
      flex-direction: column;
    }

    .synx-arch-header {
      font-weight: 700;
      color: #004f71;
      margin-bottom: 1.5mm;
      border-bottom: 0.5pt solid #cbd5e1;
      padding-bottom: 0.8mm;
      font-size: 8pt;
      display: flex;
      align-items: center;
      gap: 1.5mm;
    }

    .synx-arch-icon {
      font-weight: 700;
      font-size: 7.5pt;
      color: #004f71;
      background-color: #e2e8f0;
      padding: 0.2mm 1.5mm;
      border-radius: 2px;
    }

    .synx-arch-title {
      font-size: 8pt;
      margin: 0;
      color: #004f71;
    }

    .synx-arch-subtitle {
      font-size: 6.8pt;
      color: #64748b;
      display: block;
    }

    .synx-arch-item {
      font-size: 7.2pt;
      margin-bottom: 1.5mm;
      line-height: 1.25;
    }

    .synx-arch-item strong {
      display: block;
      color: #1e293b;
      font-size: 7.2pt;
    }

    .synx-arch-badge {
      display: block;
      font-size: 6.2pt;
      color: #64748b;
      background: transparent !important;
      border: none !important;
      padding: 0 !important;
      margin-top: 0.4mm;
      font-style: italic;
    }

    /* SYNAPTIX IDE 6-ZONE WORKBENCH MOCKUP (Print View) */
    .synx-ide-mockup {
      border: 0.75pt solid #cbd5e1;
      border-radius: 3px;
      margin: 3.5mm 0;
      background-color: #f8fafc;
      break-inside: avoid !important;
      page-break-inside: avoid !important;
      box-sizing: border-box;
    }

    .synx-ide-topbar {
      display: flex;
      justify-content: space-between;
      padding: 1.8mm 3mm;
      background-color: #1e293b;
      color: #f8fafc;
      font-size: 7.5pt;
      font-weight: 700;
      border-top-left-radius: 2px;
      border-top-right-radius: 2px;
    }

    .synx-ide-navtabs {
      display: flex;
      flex-wrap: wrap;
      gap: 1.2mm;
      padding: 1.2mm 2.5mm;
      background-color: #f1f5f9;
      border-bottom: 0.5pt solid #cbd5e1;
      font-size: 7pt;
    }

    .synx-ide-tab {
      padding: 0.8mm 2mm;
      border-radius: 2px;
      color: #475569;
      background-color: #e2e8f0;
      font-size: 6.8pt;
    }

    .synx-ide-tab.is-active {
      background-color: #004f71;
      color: #ffffff;
      font-weight: 700;
    }

    .synx-ide-body {
      display: grid;
      grid-template-columns: 28% 44% 28%;
      min-height: 28mm;
      border-bottom: 0.5pt solid #cbd5e1;
      background-color: #ffffff;
    }

    .synx-ide-panel {
      padding: 2mm 2.5mm;
      border-right: 0.5pt solid #e2e8f0;
      font-size: 7.2pt;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
    }

    .synx-ide-panel:last-child {
      border-right: none;
    }

    .synx-ide-panel-header {
      font-weight: 700;
      color: #004f71;
      font-size: 7.5pt;
      margin-bottom: 1.5mm;
      border-bottom: 0.5pt solid #e2e8f0;
      padding-bottom: 0.8mm;
    }

    .synx-ide-statusbar {
      display: flex;
      justify-content: space-between;
      padding: 1.2mm 3mm;
      background-color: #0f172a;
      color: #94a3b8;
      font-size: 6.8pt;
      border-bottom-left-radius: 2px;
      border-bottom-right-radius: 2px;
    }

    /* 4-STEP SCAN CYCLE PIPELINE (Print Grid) */
    .synx-pipeline-wrap {
      break-inside: avoid !important;
      page-break-inside: avoid !important;
      margin: 3mm 0;
      background-color: #f8fafc;
      border: 0.5pt solid #cbd5e1;
      border-radius: 2px;
      padding: 2.5mm;
    }

    .synx-pipeline-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 2mm;
    }

    .synx-pipeline-card {
      background-color: #ffffff;
      border: 0.5pt solid #cbd5e1;
      border-radius: 2px;
      padding: 2mm 1.8mm;
      font-size: 7pt;
      display: flex;
      flex-direction: column;
    }

    .synx-pipeline-badge {
      font-size: 6.2pt;
      font-weight: 700;
      color: #004f71;
      text-transform: uppercase;
      margin-bottom: 1mm;
      border-bottom: 0.5pt solid #e2e8f0;
      padding-bottom: 0.5mm;
    }

    .synx-pipeline-title {
      font-size: 7.2pt;
      font-weight: 700;
      color: #0f172a;
      margin: 0 0 1mm 0;
    }

    .synx-pipeline-desc {
      font-size: 6.8pt;
      color: #475569;
      line-height: 1.25;
      margin: 0;
    }

    .synx-pipeline-footer {
      margin-top: 2mm;
      padding-top: 1.5mm;
      border-top: 0.5pt solid #e2e8f0;
      text-align: center;
      font-size: 6.8pt;
      color: #64748b;
      font-style: italic;
    }

    /* ==========================================================================
       INDUSTRIAL PROTOCOL & WIRING SCHEMATICS (Strict ANSI/ISA-101 Slate Palette)
       ========================================================================== */

    /* 1. PROTOCOL FRAME ANALYZER (.synx-proto-card) */
    .synx-proto-card {
      background-color: #ffffff;
      border: 0.75pt solid #cbd5e1;
      border-radius: 3px;
      margin: 3mm 0;
      break-inside: avoid !important;
      page-break-inside: avoid !important;
      font-family: "Segoe UI", system-ui, -apple-system, sans-serif;
      overflow: hidden;
    }

    .synx-proto-header {
      background-color: #f1f5f9;
      color: #0f172a;
      padding: 1.5mm 3mm;
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 7.5pt;
      font-weight: 700;
      border-bottom: 0.75pt solid #cbd5e1;
    }

    .synx-proto-status {
      background-color: #e2e8f0;
      color: #334155;
      padding: 0.3mm 2mm;
      border-radius: 2px;
      font-size: 6.8pt;
      font-weight: 600;
      font-family: "Consolas", monospace;
      border: 0.5pt solid #cbd5e1;
    }

    .synx-proto-body {
      padding: 2mm 3mm;
    }

    .synx-proto-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1.2mm 4mm;
      font-size: 7.5pt;
      line-height: 1.35;
      margin-bottom: 1.8mm;
    }

    .synx-proto-item {
      display: flex;
      align-items: baseline;
      gap: 1.5mm;
    }

    .synx-proto-label {
      color: #64748b;
      font-size: 7pt;
      white-space: nowrap;
    }

    .synx-proto-val {
      color: #0f172a;
      font-weight: 600;
    }

    .synx-proto-val.font-mono {
      font-family: "Consolas", monospace;
    }

    .synx-proto-badge-ok {
      background-color: #f1f5f9;
      color: #334155;
      padding: 0.2mm 1.5mm;
      border-radius: 2px;
      font-size: 6.5pt;
      font-weight: 700;
      border: 0.5pt solid #cbd5e1;
      margin-left: 1mm;
    }

    .synx-proto-divider {
      height: 0.5pt;
      background-color: #e2e8f0;
      margin: 1.5mm 0;
    }

    .synx-proto-stream {
      display: flex;
      flex-direction: column;
      gap: 1.5mm;
    }

    .synx-stream-row {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 1.5mm;
      font-size: 7.2pt;
    }

    .synx-stream-tag {
      color: #334155;
      font-weight: 700;
      min-width: 26mm;
      font-size: 7pt;
    }

    .synx-byte-badges {
      display: inline-flex;
      align-items: center;
      gap: 1.2mm;
      flex-wrap: wrap;
    }

    .synx-byte-group {
      display: inline-flex;
      flex-direction: column;
      align-items: center;
      gap: 0.4mm;
    }

    .synx-byte {
      background-color: #f8fafc;
      color: #0f172a;
      padding: 0.4mm 1.8mm;
      border-radius: 2px;
      font-size: 7pt;
      font-weight: 700;
      font-family: "Consolas", monospace;
      border: 0.5pt solid #94a3b8;
    }

    .synx-byte-sub {
      font-size: 5.6pt;
      color: #64748b;
      font-weight: 600;
    }

    /* 2. RS-485 BUS SCHEMATIC (.synx-rs485-schematic) */
    .synx-rs485-schematic {
      background-color: #ffffff;
      border: 0.75pt solid #cbd5e1;
      border-radius: 3px;
      margin: 3mm 0;
      padding: 2.5mm 3mm;
      break-inside: avoid !important;
      page-break-inside: avoid !important;
      font-family: "Segoe UI", system-ui, -apple-system, sans-serif;
    }

    .synx-rs485-nodes {
      display: flex;
      justify-content: space-between;
      align-items: stretch;
      gap: 2mm;
    }

    .synx-rs485-node {
      background-color: #ffffff;
      border: 0.75pt solid #475569;
      border-radius: 2px;
      flex: 1;
      max-width: 52mm;
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }

    .synx-rs485-node-header {
      background-color: #f1f5f9;
      color: #0f172a;
      font-size: 7pt;
      font-weight: 700;
      padding: 1mm 1.5mm;
      text-align: center;
      border-bottom: 0.5pt solid #cbd5e1;
      white-space: nowrap;
    }

    .synx-rs485-terminals {
      padding: 1.2mm 1.5mm;
      display: flex;
      flex-direction: column;
      gap: 1mm;
    }

    .synx-rs485-term {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0.4mm 1.2mm;
      border-radius: 1px;
      font-size: 6.8pt;
      font-weight: 600;
      background-color: #f8fafc;
      color: #1e293b;
      border: 0.5pt solid #cbd5e1;
      font-family: "Consolas", monospace;
    }

    .synx-rs485-resistor {
      margin-top: 1.5mm;
      text-align: center;
    }

    .synx-resistor-badge {
      display: inline-block;
      padding: 0.3mm 1.5mm;
      border-radius: 2px;
      font-size: 6.5pt;
      font-weight: 600;
      background-color: #f1f5f9;
      color: #334155;
      border: 0.5pt solid #cbd5e1;
    }

    .synx-rs485-trunk {
      display: flex;
      flex-direction: column;
      justify-content: center;
      align-items: center;
      padding: 0 0.5mm;
      flex: 0.35;
      gap: 2.2mm;
    }

    .synx-trunk-line {
      height: 0.75pt;
      width: 100%;
      background-color: #334155;
    }

    .synx-trunk-line.is-gnd {
      border-top: 0.75pt dashed #64748b;
      background: transparent;
    }

    /* 3. ZIGBEE MESH TOPOLOGY (.synx-mesh-schematic) */
    .synx-mesh-schematic {
      background-color: #ffffff;
      border: 0.75pt solid #cbd5e1;
      border-radius: 3px;
      margin: 3mm 0;
      padding: 2.5mm 3mm;
      break-inside: avoid !important;
      page-break-inside: avoid !important;
      font-family: "Segoe UI", system-ui, -apple-system, sans-serif;
    }

    .synx-mesh-level {
      display: flex;
      justify-content: center;
      align-items: center;
      gap: 3.5mm;
      margin: 1.2mm 0;
    }

    .synx-mesh-node {
      background-color: #ffffff;
      border: 0.75pt solid #64748b;
      border-radius: 2px;
      padding: 1.2mm 2mm;
      display: flex;
      align-items: center;
      gap: 1.5mm;
    }

    .synx-mesh-node.is-coord {
      border: 1pt solid #0f172a;
      background-color: #f8fafc;
    }

    .synx-mesh-node.is-router {
      border: 0.75pt solid #475569;
    }

    .synx-mesh-node.is-end {
      border: 0.5pt solid #94a3b8;
    }

    .synx-mesh-role {
      padding: 0.2mm 1.2mm;
      border-radius: 1px;
      font-size: 6.2pt;
      font-weight: 700;
      text-transform: uppercase;
      background-color: #334155;
      color: #ffffff;
    }

    .synx-mesh-node.is-router .synx-mesh-role {
      background-color: #64748b;
      color: #ffffff;
    }

    .synx-mesh-node.is-end .synx-mesh-role {
      background-color: #e2e8f0;
      color: #334155;
      border: 0.5pt solid #cbd5e1;
    }

    .synx-mesh-name {
      font-size: 7pt;
      font-weight: 600;
      color: #0f172a;
    }

    .synx-mesh-connector {
      display: flex;
      justify-content: center;
      align-items: center;
      color: #64748b;
      font-size: 6.5pt;
      margin: 0.6mm 0;
      gap: 1.5mm;
    }

    .synx-mesh-connector::before,
    .synx-mesh-connector::after {
      content: "";
      height: 0.5pt;
      width: 12mm;
      background-color: #94a3b8;
    }

    /* TRANG BÌA SAU (BACK COVER) */
    .page-sheet-back-cover {
      width: 180mm;
      height: 258mm;
      page-break-before: always;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      padding: 10mm 0 4mm 0;
    }

    .back-cover-top {
      text-align: center;
      padding-top: 15mm;
    }

    .back-cover-logo {
      height: 18mm;
      width: auto;
      margin-bottom: 5mm;
      border: none !important;
      border-radius: 0 !important;
      box-shadow: none !important;
      background: transparent !important;
    }

    .back-cover-slogan {
      font-size: 11pt;
      font-weight: 700;
      color: #004f71;
      letter-spacing: 0.8px;
      margin-bottom: 2mm;
    }

    .back-cover-sub {
      font-size: 8.8pt;
      color: #64748b;
      max-width: 130mm;
      margin: 0 auto;
      line-height: 1.45;
    }

    .back-cover-info-card {
      background-color: #f8fafc;
      border: 0.75pt solid #cbd5e1;
      border-radius: 4px;
      padding: 4mm 5mm;
      margin: 10mm auto 0 auto;
      max-width: 140mm;
      font-size: 8.2pt;
      line-height: 1.45;
      color: #334155;
    }

    .back-cover-info-title {
      font-weight: 700;
      color: #004f71;
      font-size: 9pt;
      margin-bottom: 2mm;
      border-bottom: 0.5pt solid #e2e8f0;
      padding-bottom: 1mm;
      text-align: center;
    }

    .back-cover-footer {
      border-top: 0.5pt solid #cbd5e1;
      padding-top: 3mm;
      text-align: center;
      font-size: 7.8pt;
      color: #94a3b8;
    }
"""

# ---------------------------------------------------------------------------
# Pre-processing & Markdown Conversion
# ---------------------------------------------------------------------------
def preprocess_markdown(content: str, lang: str = "vi") -> str:
    # 1. Strip web doc-meta-bar
    content = re.sub(r'<div class="doc-meta-bar">.*?</div>', '', content, flags=re.DOTALL)
    
    # 2. Strip "Tài liệu Liên quan" (VI) and "Related Documentation" (EN) sections for the print PDF manual
    # (These web navigation links are preserved on the online VitePress wiki, but omitted in formal print manuals)
    related_pattern = r'##\s*(?:Tài liệu Liên quan|Related Documentation|Related Topics|Xem thêm|See Also).*?(?=\n##|\Z)'
    content = re.sub(related_pattern, '', content, flags=re.DOTALL | re.IGNORECASE)

    # 3. Fix image paths to file:/// absolute paths
    abs_img_dir = os.path.abspath(os.path.join(PUBLIC_DIR, "images")).replace("\\", "/")
    content = content.replace('src="/images/', f'src="file:///{abs_img_dir}/')
    content = re.sub(r'\]\(/images/(.*?)\)', rf'](file:///{abs_img_dir}/\1)', content)
    
    # 3. Clean up web marketing terms (e.g. "Pillar 1", "Trụ cột 1") to clean section headings without stutter
    content = re.sub(r'##\s*(?:Pillar|Trụ cột)\s*\d+\s*:\s*', '## ', content, flags=re.IGNORECASE)

    # 4. Convert VitePress ::: containers to print callouts (Strict Industrial ANSI Z535.6 - NO Emojis)
    def replace_container(match):
        ctype = match.group(1).lower()
        title = match.group(2).strip() if match.group(2) else ""
        body = match.group(3).strip()
        
        default_titles_vi = {
            "tip": "MẸO KỸ THUẬT",
            "warning": "CẢNH BÁO AN TOÀN",
            "danger": "NGUY HIỂM",
            "info": "LƯU Ý KỸ THUẬT"
        }
        default_titles_en = {
            "tip": "TECHNICAL TIP",
            "warning": "SAFETY WARNING",
            "danger": "DANGER",
            "info": "TECHNICAL NOTE"
        }
        
        if not title:
            mapping = default_titles_vi if lang == "vi" else default_titles_en
            title = mapping.get(ctype, ctype.upper())
            
        return f'<div class="callout callout-{ctype}"><div class="callout-header"><span class="callout-prefix">[ {title} ]</span></div><div class="callout-body">\n\n{body}\n\n</div></div>'

    pattern = r':::\s*(\w+)(?:[ \t]+([^\n\r]+))?\r?\n(.*?)\r?\n:::'
    content = re.sub(pattern, replace_container, content, flags=re.DOTALL)
    
    # 5. Remove all standalone horizontal divider lines (---, ***, ___)
    # These create unwanted web <hr> separator lines cutting across the page
    content = re.sub(r'^\s*[-*_]{3,}\s*$', '', content, flags=re.MULTILINE)
    
    # 6. Normalize non-standard Unicode symbols and emojis to pure Segoe UI glyphs
    # Prevents Chromium from falling back to Type3 bitmap fonts, CambriaMath, or oversized emoji fonts
    if lang == "vi":
        symbol_map = {
            "👑": "[Trạm Chủ]",
            "🔁": "[Trạm Lặp]",
            "📡": "[Đầu Cuối]",
            "🟢": "[BẬT]",
            "⚫": "[TẮT]",
            "🔢": "[Giá trị]",
            "⏱️": "[Thời gian]",
            "⏱": "[Thời gian]",
            "⚠️": "[CẢNH BÁO]",
            "⚠": "[CẢNH BÁO]",
            "🛑": "[NGUY HIỂM]",
            "⚡": "[THẬN TRỌNG]",
            "ℹ️": "[LƯU Ý]",
            "★": "[Ghim]",
            "✕": "[x]",
            "✓": "[OK]",
            "🔗": "•",
            "➔": "→",
            "↺": "•",
        }
    else:
        symbol_map = {
            "👑": "[Coordinator]",
            "🔁": "[Router]",
            "📡": "[End Device]",
            "🟢": "[ON]",
            "⚫": "[OFF]",
            "🔢": "[Value]",
            "⏱️": "[Timer]",
            "⏱": "[Timer]",
            "⚠️": "[WARNING]",
            "⚠": "[WARNING]",
            "🛑": "[DANGER]",
            "⚡": "[CAUTION]",
            "ℹ️": "[NOTICE]",
            "★": "[Pin]",
            "✕": "[x]",
            "✓": "[OK]",
            "🔗": "•",
            "➔": "→",
            "↺": "•",
        }
    for orig, rep in symbol_map.items():
        content = content.replace(orig, rep)
        
    return content

def render_section_content(md_parser: MarkdownIt, sec_num: str, sec_title: str, file_rel_path: str, lang: str) -> str:
    full_path = os.path.join(DOCS_PORTAL, lang, file_rel_path)
    if not os.path.exists(full_path):
        return f'<div class="missing-file">File not found: {file_rel_path}</div>'
        
    with open(full_path, "r", encoding="utf-8") as fp:
        raw_md = fp.read()
        
    # Strip the first H1 header since we generate a standardized section title
    raw_md = re.sub(r'^#\s+.*?\r?\n', '', raw_md)
    processed_md = preprocess_markdown(raw_md, lang)
    
    # Convert markdown to HTML
    body_html = md_parser.render(processed_md)
    
    # Demote internal headers (## -> h3, ### -> h4, etc.)
    body_html = re.sub(r'<h4\b', '<h5', body_html)
    body_html = re.sub(r'</h4\b', '</h5', body_html)
    body_html = re.sub(r'<h3\b', '<h4', body_html)
    body_html = re.sub(r'</h3\b', '</h4', body_html)
    body_html = re.sub(r'<h2\b', '<h3 class="subsection-title"', body_html)
    body_html = re.sub(r'</h2\b', '</h3', body_html)
    
    return f'''
    <section class="section-container" id="sec-{sec_num.replace('.', '-')}">
      <h2 class="section-title"><span class="sec-num">{sec_num}</span> {sec_title}</h2>
      <div class="section-body">
        {body_html}
      </div>
    </section>
    '''

def generate_cover_html(lang: str) -> str:
    logo_path = f"file:///{os.path.abspath(os.path.join(PUBLIC_DIR, 'synaptix_logo.png')).replace('\\', '/')}"
    
    if lang == "vi":
        corp_tag = "SYNAPTIX TECHNOLOGY JSC"
        cat = "HỆ SINH THÁI THIẾT BỊ TỰ ĐỘNG HÓA SYNAPTIX IIOT"
        title = "SynaptiX IDE"
        sub = "HƯỚNG DẪN SỬ DỤNG & VẬN HÀNH KỸ THUẬT"
        lead = "Môi trường Kỹ thuật Lập trình Logic Khối hàm FBD, Giám sát Thời gian thực & Quản trị Hệ sinh thái Thiết bị SynaptiX IIoT."
        footer_brand = "SynaptiX &ndash; Industrial IoT"
        footer_meta = "Phiên bản 2.15 LTS &bull; Tháng 11 / 2026"
    else:
        corp_tag = "SYNAPTIX TECHNOLOGY JSC"
        cat = "SYNAPTIX IIOT AUTOMATION ECOSYSTEM"
        title = "SynaptiX IDE"
        sub = "SYSTEM OPERATING & USER MANUAL"
        lead = "Industrial Engineering Environment for FBD Logic Programming, Real-time Telemetry & SynaptiX IIoT Device Ecosystem Management."
        footer_brand = "SynaptiX &ndash; Industrial IoT"
        footer_meta = "Version 2.15 LTS &bull; November 2026"

    return f"""
    <div class="page-sheet-cover">
      <div class="cover-top">
        <img src="{logo_path}" alt="SynaptiX Logo" class="cover-logo">
        <span class="cover-corp-tag">{corp_tag}</span>
      </div>

      <div class="cover-content">
        <div class="cover-doc-category">{cat}</div>
        <h1 class="cover-product-title">{title}</h1>
        <div class="cover-product-subtitle">{sub}</div>
        <div class="cover-title-divider"></div>
        <p class="cover-product-lead">{lead}</p>
      </div>

      <div class="cover-footer">
        <span class="cover-footer-left">{footer_brand}</span>
        <span class="cover-footer-right">{footer_meta}</span>
      </div>
    </div>
    """

def generate_doc_control_html(lang: str) -> str:
    if lang == "vi":
        title = "KIỂM SOÁT TÀI LIỆU & THÔNG TIN PHÁP LÝ"
        sec1_title = "1. Thông tin Nhận diện Tài liệu & Bản quyền"
        sec2_title = "2. Lịch sử Soát xét & Phiên bản Phần mềm"
        sec3_title = "3. Tiêu chuẩn Quốc tế & Quy chuẩn Kỹ thuật Tuân thủ"
        
        info_rows = [
            ("Mã tài liệu / Số hiệu", "<strong>DOC-SPX-UM-001</strong> (Official Industrial User Manual)"),
            ("Phiên bản phần mềm áp dụng", "SynaptiX IDE &amp; Runtime Engine v2.15.0 LTS (.NET 8 64-bit)"),
            ("Ngày phát hành", "Tháng 11 Năm 2026 (Xuất bản chính thức toàn cầu)"),
            ("Đơn vị sở hữu & Phát hành", "<strong>CÔNG TY CỔ PHẦN CÔNG NGHỆ SYNAPTIX</strong> (SYNAPTIX TECHNOLOGY JSC)"),
            ("Mã số thuế / Đăng ký KD", "0111000485 &bull; Sở Kế hoạch &amp; Đầu tư TP. Hà Nội cấp"),
            ("Địa chỉ trụ sở chính", "P109, Tầng 2, Tòa PZ4, Lô đất F4-CCTP1, KĐT Vinhomes Smart City, Nam Từ Liêm, Hà Nội"),
            ("Hotline Kỹ thuật / CSKH", "<strong>(+84) 972 141 491</strong> &bull; Trực tổng đài hỗ trợ 24/7"),
            ("Cổng thông tin & Thư viện", "Website: <code>https://synaptix-io.com</code> &bull; Email: <code>info@synaptix-io.com</code>"),
        ]

        rev_headers = ["Phiên bản", "Ngày ban hành", "Mô tả Tóm tắt Thay đổi", "Trạng thái"]
        rev_rows = [
            ("v1.0.0", "15/03/2025", "Phát hành bản đầu tiên: Lập trình FBD cơ bản, kết nối cáp USB CDC, nạp cấu hình.", "Hết hiệu lực"),
            ("v2.0.0", "20/08/2025", "Bổ sung Modbus RTU/TCP Master/Slave, phân hệ Live Watch đồ thị 20 FPS, bộ mô phỏng F5.", "Lưu trữ"),
            ("v2.15.0", "15/11/2026", "Bổ sung cụm khối MATH (ALU), bộ cài đặt Inno Setup, bảo mật MQTT TLS X.509, tối ưu nạp an toàn.", "<strong>Hiện hành (LTS)</strong>"),
        ]

        std_headers = ["Bộ Tiêu chuẩn", "Tên Tiêu chuẩn & Phạm vi Áp dụng", "Cấp độ Tuân thủ"]
        std_rows = [
            ("IEC 61131-3", "Tiêu chuẩn quốc tế về ngôn ngữ lập trình cho bộ điều khiển khả trình (FBD).", "Tuân thủ Hoàn toàn"),
            ("IEC 61508-3", "An toàn chức năng hệ thống điện/điện tử/lập trình liên quan đến an toàn (SIL2/SIL3).", "Thiết kế Tương thích"),
            ("IEC 62443-4-1", "An ninh mạng công nghiệp cho các hệ thống tự động hóa và điều khiển (IACS).", "Áp dụng Thực tế"),
            ("ANSI/ISA-101", "Công thái học giao diện người-máy (HMI) trong môi trường nhà máy sản xuất.", "Chuẩn Nhận diện"),
            ("ISO/IEC/IEEE 26514", "Quy trình thiết kế và phát triển tài liệu kỹ thuật dành cho người dùng phần mềm.", "Chuẩn Xuất bản"),
        ]

        legal_text = """
        <strong>TUYÊN BỐ BẢN QUYỀN &amp; TỪ CHỐI TRÁCH NHIỆM:</strong> Toàn bộ nội dung, hình ảnh, sơ đồ mạch logic và đặc tả kỹ thuật trong tài liệu này thuộc quyền sở hữu trí tuệ hợp pháp của <strong>Công ty Cổ phần Công nghệ Synaptix</strong>. Nghiêm cấm mọi hành vi sao chép, trích xuất hoặc phân phối lại dưới mọi hình thức khi chưa có sự đồng ý bằng văn bản. Thông tin trong tài liệu được đối soát chính xác tại thời điểm xuất bản; Synaptix bảo lưu quyền cải tiến và thay đổi thông số phần mềm nhằm nâng cao độ an toàn và hiệu năng mà không cần thông báo trước.
        """
    else:
        title = "DOCUMENT CONTROL & LEGAL NOTICES"
        sec1_title = "1. Document Identification & Corporate Ownership"
        sec2_title = "2. Revision History & Software Release Lifecycle"
        sec3_title = "3. International Technical Standards Compliance"
        
        info_rows = [
            ("Document ID / Number", "<strong>DOC-SPX-UM-001</strong> (Official Industrial User Manual)"),
            ("Software Target Release", "SynaptiX IDE &amp; Runtime Engine v2.15.0 LTS (.NET 8 64-bit)"),
            ("Publication Date", "November 2026 (Official Global Release)"),
            ("Legal Entity &amp; Publisher", "<strong>SYNAPTIX TECHNOLOGY JOINT STOCK COMPANY</strong> (SYNAPTIX TECHNOLOGY JSC)"),
            ("Enterprise Tax Code", "0111000485 &bull; Licensed by Hanoi Department of Planning &amp; Investment"),
            ("Headquarters Address", "P109, 2nd Fl, PZ4 Building, Vinhomes Smart City, Nam Tu Liem, Hanoi, Vietnam"),
            ("Technical Support Hotline", "<strong>(+84) 972 141 491</strong> &bull; Industrial 24/7 Support"),
            ("Enterprise Web &amp; Inquiries", "Website: <code>https://synaptix-io.com</code> &bull; Email: <code>info@synaptix-io.com</code>"),
        ]

        rev_headers = ["Version", "Release Date", "Change Summary", "Lifecycle Status"]
        rev_rows = [
            ("v1.0.0", "Mar 15, 2025", "Initial system release: Core FBD logic, USB CDC serial communication, baseline deploy.", "Superseded"),
            ("v2.0.0", "Aug 20, 2025", "Added Modbus RTU/TCP Master/Slave, 20 FPS Live Watch trend charts, F5 simulator.", "Archived"),
            ("v2.15.0", "Nov 15, 2026", "Added MATH ALU block suite, Inno Setup engine, MQTT TLS X.509, atomic deploy hardening.", "<strong>Active LTS</strong>"),
        ]

        std_headers = ["Standard Code", "Title & International Regulatory Scope", "Compliance Level"]
        std_rows = [
            ("IEC 61131-3", "Programmable controllers &bull; Part 3: Programming languages (Function Block Diagram).", "Fully Compliant"),
            ("IEC 61508-3", "Functional safety of electrical/electronic/programmable electronic safety-related systems (SIL2/SIL3).", "Design-Aligned"),
            ("IEC 62443-4-1", "Security for industrial automation and control systems (IACS) &bull; Secure development lifecycle.", "Implemented"),
            ("ANSI/ISA-101", "Human-Machine Interfaces (HMI) for Process Automation Systems &bull; Ergonomics.", "Aesthetic Target"),
            ("ISO/IEC/IEEE 26514", "Systems and software engineering &bull; Requirements for designers and developers of user documentation.", "Publishing Standard"),
        ]

        legal_text = """
        <strong>COPYRIGHT &amp; DISCLAIMER NOTICE:</strong> All materials, vector schematics, screen captures, and architectural specifications contained within this manual are the proprietary intellectual property of <strong>Synaptix Technology Joint Stock Company</strong>. Unauthorized duplication, reproduction, or redistribution is strictly prohibited. Information contained herein is verified accurate at time of publication; Synaptix reserves the right to modify technical parameters without prior notice in pursuit of continuous safety and performance engineering.
        """

    info_table_html = "".join([f"<tr><td class='doc-control-key'>{k}</td><td class='doc-control-val'>{v}</td></tr>" for k, v in info_rows])
    rev_head_html = "".join([f"<th>{h}</th>" for h in rev_headers])
    rev_body_html = "".join([f"<tr>{''.join([f'<td>{cell}</td>' for cell in row])}</tr>" for row in rev_rows])
    std_head_html = "".join([f"<th>{h}</th>" for h in std_headers])
    std_body_html = "".join([f"<tr>{''.join([f'<td>{cell}</td>' for cell in row])}</tr>" for row in std_rows])

    return f"""
    <div class="page-sheet-doc-control">
      <div>
        <h1 class="doc-control-title">{title}</h1>

        <div class="doc-control-section">
          <div class="doc-control-heading">{sec1_title}</div>
          <table class="doc-control-table">
            {info_table_html}
          </table>
        </div>

        <div class="doc-control-section">
          <div class="doc-control-heading">{sec2_title}</div>
          <table class="doc-control-table">
            <thead><tr>{rev_head_html}</tr></thead>
            <tbody>{rev_body_html}</tbody>
          </table>
        </div>

        <div class="doc-control-section">
          <div class="doc-control-heading">{sec3_title}</div>
          <table class="doc-control-table">
            <thead><tr>{std_head_html}</tr></thead>
            <tbody>{std_body_html}</tbody>
          </table>
        </div>
      </div>

      <div class="doc-control-legal-text">
        {legal_text}
      </div>
    </div>
    """

def generate_safety_html(lang: str) -> str:
    if lang == "vi":
        title = "QUY ƯỚC AN TOÀN CÔNG NGHIỆP & LƯU Ý VẬN HÀNH"
        lead = "Để bảo vệ tính mạng kỹ sư vận hành, ngăn ngừa hư hỏng cơ cấu chấp hành và duy trì tính ổn định của dây chuyền sản xuất, mọi kỹ sư lập trình SynaptiX IDE bắt buộc phải thấu hiểu và tuân thủ các quy chuẩn an toàn quốc tế theo tiêu chuẩn <strong>ANSI Z535.6</strong> và <strong>ISO 7010</strong> được sử dụng xuyên suốt tài liệu này:"
        
        d_head = "NGUY HIỂM (DANGER)"
        d_text = "Chỉ thị tình huống cực kỳ nguy hiểm cận kề. Nếu không tuân thủ nghiêm ngặt, tai nạn điện giật, kẹp cơ khí hoặc nổ áp lực <strong>CHẮC CHẮN sẽ dẫn đến tử vong hoặc thương tật vĩnh viễn</strong>. Luôn ngắt cầu dao tổng và khóa thẻ an toàn (LOTO) trước khi can thiệp đấu nối tủ điện."
        
        w_head = "CẢNH BÁO (WARNING)"
        w_text = "Chỉ thị tình huống nguy hiểm tiềm tàng. Nếu bỏ qua, có thể dẫn đến <strong>thương tích nghiêm trọng cho người vận hành hoặc hư hỏng nặng cơ cấu máy móc</strong>. Tuyệt đối không nạp chương trình logic mới trực tiếp vào thiết bị khi máy đang hoạt động."
        
        c_head = "THẬN TRỌNG (CAUTION)"
        c_text = "Chỉ thị rủi ro kỹ thuật có thể gây <strong>hỏng hóc phần cứng, mất dữ liệu cấu hình hoặc gián đoạn chu trình sản xuất</strong>. Thường liên quan đến cắm ngược cực nguồn 24V DC, sai phân cực RS-485 A/B hoặc cấp quá điện áp vào ngõ vào tương tự AI."
        
        n_head = "LƯU Ý KỸ THUẬT (NOTICE)"
        n_text = "Chỉ dẫn các quy tắc thiết kế tối ưu, nguyên tắc lập trình sạch và khuyến nghị của nhà sản xuất nhằm kéo dài tuổi thọ thiết bị, tối ưu chu kỳ quét và ngăn ngừa nhiễu đường truyền."

        rule_h = "4 NGUYÊN TẮC VÀNG TRƯỚC KHI NẠP LOGIC XUỐNG THIẾT BỊ"
        r1_t = "1. Luôn Mô phỏng Ngoại tuyến (F5)"
        r1_d = "Kiểm chứng 100% thuật toán trên Offline Simulator trước khi cắm cáp nạp vào thiết bị vật lý."
        r2_t = "2. Trang bị E-Stop Phần cứng Độc lập"
        r2_d = "Mạch Dừng khẩn cấp bắt buộc phải đấu cắt trực tiếp tiếp điểm công-tắc-tơ nguồn, không phụ thuộc vào phần mềm."
        r3_t = "3. Cô lập Nguồn Động lực Chấp hành"
        r3_d = "Ngắt cầu dao cấp nguồn cho biến tần, động cơ, van điện từ trong suốt quá trình nạp (Deploy) chương trình mới."
        r4_t = "4. Kiểm tra Tiếp địa & Vỏ Bọc kim"
        r4_d = "Luôn nối đất vỏ bọc kim cáp RS-485 tại 1 điểm duy nhất ở tủ điện chính để triệt tiêu nhiễu vòng lặp mass (Ground Loop)."
    else:
        title = "INDUSTRIAL SAFETY CONVENTIONS & PRECAUTIONS"
        lead = "To protect personnel from injury, safeguard expensive industrial actuators, and preserve factory uptime, all engineers operating SynaptiX IDE must strictly understand and adhere to the international safety definitions established by <strong>ANSI Z535.6</strong> and <strong>ISO 7010</strong>:"
        
        d_head = "DANGER"
        d_text = "Indicates an imminently hazardous situation which, if not avoided, <strong>WILL result in death or catastrophic permanent injury</strong>. Always de-energize and execute Lockout/Tagout (LOTO) procedures on main industrial switchgear prior to accessing control terminals."
        
        w_head = "WARNING"
        w_text = "Indicates a potentially hazardous situation which, if not avoided, <strong>COULD result in severe personal injury or serious mechanical equipment destruction</strong>. Never deploy unverified logic programs directly to actuators while plant equipment is actively moving."
        
        c_head = "CAUTION"
        c_text = "Indicates a technical condition that may result in <strong>equipment damage, firmware corruption, or costly line downtime</strong>. Typically involves reverse 24V DC supply polarity, inverted RS-485 A/B signal wiring, or over-voltage on AI analog pins."
        
        n_head = "NOTICE"
        n_text = "Highlights engineering best practices, deterministic execution guidelines, and manufacturer recommendations designed to maximize hardware lifespan, optimize scan jitter, and eliminate fieldbus communication drops."

        rule_h = "THE 4 GOLDEN RULES BEFORE FIELD DEPLOYMENT"
        r1_t = "1. Always Run Offline Simulation (F5)"
        r1_d = "Verify 100% of logic branches, timer roll-overs, and counter limits on the virtual simulator before connecting to physical hardware."
        r2_t = "2. Hardware Emergency Stop (E-Stop)"
        r2_d = "The Emergency Stop loop must physically de-energize master contactors and must NEVER rely exclusively on software logic."
        r3_t = "3. Isolate High-Voltage Actuator Power"
        r3_d = "Disconnect inverter, servo, and solenoid 3-phase power during logic flashing and preliminary input calibration."
        r4_t = "4. Verify Industrial Grounding & Shielding"
        r4_d = "Ground RS-485 / Ethernet shielded cabling at exactly one central star point to eliminate damaging ground loop currents."

    return f"""
    <div class="page-sheet-safety">
      <h1 class="safety-title">{title}</h1>
      <p class="safety-lead">{lead}</p>

      <div class="safety-box safety-box-danger">
        <div class="safety-box-header">{d_head}</div>
        <div>{d_text}</div>
      </div>

      <div class="safety-box safety-box-warning">
        <div class="safety-box-header">{w_head}</div>
        <div>{w_text}</div>
      </div>

      <div class="safety-box safety-box-caution">
        <div class="safety-box-header">{c_head}</div>
        <div>{c_text}</div>
      </div>

      <div class="safety-box safety-box-notice">
        <div class="safety-box-header">{n_head}</div>
        <div>{n_text}</div>
      </div>

      <div style="margin-top: 4mm; border-top: 1pt solid #0f172a; padding-top: 3mm;">
        <div style="font-size: 8.5pt; font-weight: 700; color: #0f172a; text-transform: uppercase; margin-bottom: 2mm;">{rule_h}</div>
        <div class="safety-rules-grid">
          <div class="safety-rule-card">
            <strong>{r1_t}</strong>
            {r1_d}
          </div>
          <div class="safety-rule-card">
            <strong>{r2_t}</strong>
            {r2_d}
          </div>
          <div class="safety-rule-card">
            <strong>{r3_t}</strong>
            {r3_d}
          </div>
          <div class="safety-rule-card">
            <strong>{r4_t}</strong>
            {r4_d}
          </div>
        </div>
      </div>
    </div>
    """

def generate_toc_html(lang: str, page_map: dict = None) -> str:
    chapters = CHAPTER_MAPPINGS[lang]
    page_map = page_map or {}
    
    # Split into 2 balanced TOC pages
    group1 = chapters[:5]
    group2 = chapters[5:]
    
    def render_group(ch_list, part_title):
        items_html = []
        for ch in ch_list:
            ch_title = ch["title"]
            ch_p = page_map.get(ch_title, 6)
            items_html.append(f"""
            <div class="toc-item-ch">
              <span class="toc-title">{ch_title}</span>
              <span class="toc-dots"></span>
              <span class="toc-page">{ch_p}</span>
            </div>
            """)
            
            for num, stitle, _ in ch["files"]:
                sub_key = f"{num} {stitle}"
                sub_p = page_map.get(sub_key, ch_p)
                items_html.append(f"""
                <div class="toc-item-sub">
                  <span class="toc-title">{num} {stitle}</span>
                  <span class="toc-dots"></span>
                  <span class="toc-page">{sub_p}</span>
                </div>
                """)
        return f"""
        <div class="page-sheet-toc">
          <h1 class="toc-heading">{part_title}</h1>
          <div class="toc-list">
            {''.join(items_html)}
          </div>
        </div>
        """
        
    if lang == "vi":
        toc1 = render_group(group1, "Mục lục — Phần 1: Kiến trúc, Cài đặt & Khối hàm FBD")
        toc2 = render_group(group2, "Mục lục — Phần 2: Truyền thông IIoT, Mô phỏng & Phụ lục")
    else:
        toc1 = render_group(group1, "Table of Contents — Part 1: Architecture, Setup & FBD Logic")
        toc2 = render_group(group2, "Table of Contents — Part 2: Industrial IIoT, Simulation & Appendices")
        
    return f"{toc1}\n{toc2}"

def generate_back_cover_html(lang: str) -> str:
    logo_path = f"file:///{os.path.abspath(os.path.join(PUBLIC_DIR, 'synaptix_logo.png')).replace('\\', '/')}"
    
    if lang == "vi":
        slogan = "EDGE &bull; GATEWAY &bull; CLOUD &bull; AI"
        sub = "Giải pháp Kết nối Thông minh cho Nhà máy Thông minh &amp; Giám sát Máy móc Thời gian thực."
        card_title = "TRUNG TÂM HỖ TRỢ KỸ THUẬT &amp; BẢO HÀNH CHÍNH HÃNG"
        c_corp = "CÔNG TY CỔ PHẦN CÔNG NGHỆ SYNAPTIX"
        c_hotline = "Hotline Hỗ trợ Kỹ thuật: <strong>(+84) 972 141 491</strong> (24/7)"
        c_email = "Email Tiếp nhận Sự cố: <strong>support@synaptix-io.com</strong>"
        c_web = "Cổng thông tin &amp; Tra cứu: <strong>https://synaptix-io.com</strong>"
        c_addr = "Trụ sở: Tòa nhà PZ4, KĐT Vinhomes Smart City, Tây Mỗ, Nam Từ Liêm, TP. Hà Nội, Việt Nam"
        c_warranty = "Chính sách Bảo hành: 24 Tháng 1-đổi-1 đối với toàn bộ phần cứng SynaptiX Controller, miniLog MLV2 và Gateway."
        footer = "Bản quyền &copy; 2026 Synaptix Technology Joint Stock Company. Bảo lưu mọi quyền."
    else:
        slogan = "EDGE &bull; GATEWAY &bull; CLOUD &bull; AI"
        sub = "Smart Industrial Connectivity Solutions for Intelligent Manufacturing &amp; Predictive Maintenance."
        card_title = "SYNAPTIX INDUSTRIAL SUPPORT &amp; WARRANTY CENTER"
        c_corp = "SYNAPTIX TECHNOLOGY JOINT STOCK COMPANY"
        c_hotline = "Technical Hotline: <strong>(+84) 972 141 491</strong> (24/7 OT Support)"
        c_email = "Engineering Desk: <strong>support@synaptix-io.com</strong>"
        c_web = "Documentation &amp; Portal: <strong>https://synaptix-io.com</strong>"
        c_addr = "HQ: PZ4 Building, Vinhomes Smart City, Tay Mo, Nam Tu Liem, Hanoi, Vietnam"
        c_warranty = "Warranty Policy: 24-Month Advance Replacement Guarantee across all SynaptiX Controllers, miniLog MLV2 and Gateways."
        footer = "Copyright &copy; 2026 Synaptix Technology Joint Stock Company. All rights reserved."

    return f"""
    <div class="page-sheet-back-cover">
      <div class="back-cover-top">
        <img src="{logo_path}" alt="SynaptiX Logo" class="back-cover-logo">
        <div class="back-cover-slogan">{slogan}</div>
        <div class="back-cover-sub">{sub}</div>

        <div class="back-cover-info-card">
          <div class="back-cover-info-title">{card_title}</div>
          <div style="font-weight: 700; color: #004f71; margin-bottom: 2mm;">{c_corp}</div>
          <div style="margin-bottom: 1.5mm;">{c_hotline}</div>
          <div style="margin-bottom: 1.5mm;">{c_email}</div>
          <div style="margin-bottom: 1.5mm;">{c_web}</div>
          <div style="margin-bottom: 2mm;">{c_addr}</div>
          <div style="border-top: 0.5pt solid #cbd5e1; padding-top: 1.5mm; font-size: 7.8pt; color: #64748b;">{c_warranty}</div>
        </div>
      </div>

      <div class="back-cover-footer">
        {footer}
      </div>
    </div>
    """

def build_html(lang: str, page_map: dict = None) -> str:
    md = MarkdownIt("commonmark").enable("table")
    md.validateLink = lambda url: True
    cover_html = generate_cover_html(lang)
    safety_html = generate_safety_html(lang)
    toc_html = generate_toc_html(lang, page_map)
    back_cover_html = generate_back_cover_html(lang)
    
    chapters_html = []
    for ch in CHAPTER_MAPPINGS[lang]:
        ch_html = [f'<div class="chapter-start" id="{ch["id"]}"><h1 class="chapter-main-title">{ch["title"]}</h1>']
        for sec_num, sec_title, file_rel in ch["files"]:
            sec_html = render_section_content(md, sec_num, sec_title, file_rel, lang)
            ch_html.append(sec_html)
        ch_html.append('</div>')
        chapters_html.append("\n".join(ch_html))
        
    doc_title = "SynaptiX IDE V2.15.0 - Hướng dẫn Sử dụng Hệ thống" if lang == "vi" else "SynaptiX IDE V2.15.0 - System User Manual"
    
    return f"""<!DOCTYPE html>
<html lang="{lang}">
<head>
  <meta charset="UTF-8">
  <title>{doc_title}</title>
  <style>
{MASTER_CSS}
  </style>
</head>
<body>
{cover_html}
{safety_html}
{toc_html}
{"".join(chapters_html)}
{back_cover_html}
</body>
</html>
"""

def compile_pdf(html_path: str, pdf_path: str) -> bool:
    cmd = [
        CHROME_PATH,
        "--headless=new",
        "--disable-gpu",
        "--no-pdf-header-footer",
        f"--print-to-pdf={pdf_path}",
        f"file:///{os.path.abspath(html_path).replace('\\', '/')}"
    ]
    res = subprocess.run(cmd, capture_output=True, text=True)
    return res.returncode == 0 and os.path.exists(pdf_path)

def extract_page_map(pdf_path: str, lang: str) -> dict:
    doc = fitz.open(pdf_path)
    page_map = {}
    
    # We skip Page 1 (Cover), Page 2 (Safety), Page 3-4 (TOC Part 1 & 2) -> start at index 4 (Physical Page 5)
    start_idx = 4
    chapters = CHAPTER_MAPPINGS[lang]
    for ch in chapters:
        ch_text = ch["title"].split(":")[-1].strip().lower()
        found_p = None
        for p_idx in range(start_idx, len(doc)):
            text = doc[p_idx].get_text().lower()
            if ch_text in text or ch["title"].lower() in text:
                found_p = p_idx + 1
                break
        if found_p:
            page_map[ch["title"]] = found_p
            
        for num, stitle, _ in ch["files"]:
            short_str = stitle[:22].lower()
            found_sub = None
            for p_idx in range(start_idx, len(doc)):
                text = doc[p_idx].get_text().lower()
                if num.lower() in text and short_str in text:
                    found_sub = p_idx + 1
                    break
            if found_sub:
                page_map[f"{num} {stitle}"] = found_sub
                
    doc.close()
    return page_map

def stamp_running_headers_and_footers(pdf_path: str, lang: str):
    doc = fitz.open(pdf_path)
    total_pages = len(doc)
    
    book_title = "SynaptiX IDE • Hướng dẫn Sử dụng & Vận hành" if lang == "vi" else "SynaptiX IDE • System Operating Manual"
    footer_brand = "SynaptiX – Industrial IoT • synaptix-io.com"
    
    font_italic_path = "C:/Windows/Fonts/segoeuii.ttf"
    font = fitz.Font(fontfile=font_italic_path)
    
    # Body pages are from index 4 (Physical Page 5) to index total_pages - 2 (before Back Cover)
    body_start_idx = 4
    body_end_idx = total_pages - 1 # exclude back cover
    
    last_known_ch = "Kỹ thuật Vận hành Hệ thống" if lang == "vi" else "Industrial Engineering Operations"
    
    for p_idx in range(body_start_idx, body_end_idx):
        page = doc[p_idx]
        current_page_num = p_idx + 1
        page_str = f"Trang {current_page_num} / {total_pages}" if lang == "vi" else f"Page {current_page_num} of {total_pages}"
        
        # Determine current chapter by inspecting page text
        page_text = page.get_text()
        for ch in CHAPTER_MAPPINGS[lang]:
            ch_num_str = f"Chương {ch['num']}" if lang == "vi" else f"Chapter {ch['num']}"
            if ch["num"] in ["A", "B", "C"]:
                ch_num_str = f"Phụ lục {ch['num']}" if lang == "vi" else f"Appendix {ch['num']}"
            if ch_num_str.lower() in page_text.lower():
                last_known_ch = ch["title"].split(":")[-1].strip()
                break
        curr_ch = last_known_ch
            
        # Running Header (Top Line at Y=33 pt, Margins: X=45.35 to 552.75 pt)
        # Muted subtle slate italic ("làm mờ và chữ chéo")
        page.insert_text(fitz.Point(45.35, 27), book_title, fontname="segoeuii", fontfile=font_italic_path, fontsize=7.2, color=(0.50, 0.55, 0.62))
        
        # Elegant chapter title: prevent ugly mid-word cutoffs
        if len(curr_ch) > 60:
            trimmed = curr_ch[:58]
            last_space = trimmed.rfind(" ")
            if last_space != -1:
                trimmed = trimmed[:last_space]
            ch_short = trimmed + "…"
        else:
            ch_short = curr_ch

        ch_len = font.text_length(ch_short, fontsize=7.2)
        page.insert_text(fitz.Point(552.75 - ch_len, 27), ch_short, fontname="segoeuii", fontfile=font_italic_path, fontsize=7.2, color=(0.50, 0.55, 0.62))
        page.draw_line(fitz.Point(45.35, 33), fitz.Point(552.75, 33), color=(0.88, 0.90, 0.93), width=0.35)
        
        # Running Footer (Bottom Line at Y=808 pt)
        page.draw_line(fitz.Point(45.35, 808), fitz.Point(552.75, 808), color=(0.88, 0.90, 0.93), width=0.35)
        page.insert_text(fitz.Point(45.35, 819), footer_brand, fontname="segoeuii", fontfile=font_italic_path, fontsize=7.2, color=(0.52, 0.58, 0.64))
        
        p_len = font.text_length(page_str, fontsize=7.2)
        page.insert_text(fitz.Point(552.75 - p_len, 819), page_str, fontname="segoeuii", fontfile=font_italic_path, fontsize=7.2, color=(0.40, 0.45, 0.52))
        
    tmp_path = pdf_path + ".stamped.tmp"
    doc.save(tmp_path)
    doc.close()
    if os.path.exists(pdf_path):
        os.remove(pdf_path)
    os.rename(tmp_path, pdf_path)
    print(f"[{lang.upper()}] Successfully stamped running headers & footers on {body_end_idx - body_start_idx} body pages.")

def build_full_manual(lang: str):
    print(f"\n=======================================================")
    print(f" BUILDING COMPLETE INDUSTRIAL USER MANUAL: {lang.upper()}")
    print(f"=======================================================")
    
    html_filename = f"SynaptiX_User_Manual_{lang.upper()}.html"
    pdf_filename = f"SynaptiX_User_Manual_{lang.upper()}.pdf"
    
    html_path = os.path.join(DOWNLOADS_DIR, html_filename)
    pdf_path = os.path.join(DOWNLOADS_DIR, pdf_filename)
    
    # Pass 1: Build preliminary HTML and PDF
    print(f"[Pass 1] Generating preliminary HTML: {html_filename}...")
    initial_html = build_html(lang, page_map={})
    with open(html_path, "w", encoding="utf-8") as f:
        f.write(initial_html)
        
    print(f"[Pass 1] Compiling preliminary PDF with Chrome Headless...")
    success = compile_pdf(html_path, pdf_path)
    if not success:
        print(f"ERROR: Chrome failed to compile preliminary PDF {pdf_path}")
        return False
        
    # Pass 2: Extract exact physical page numbers
    print(f"[Pass 2] Extracting exact physical page coordinates...")
    page_map = extract_page_map(pdf_path, lang)
    print(f"[Pass 2] Discovered {len(page_map)} chapter/section locations.")
    
    # Pass 3: Re-render HTML with synchronized TOC
    print(f"[Pass 3] Re-generating HTML with synchronized Table of Contents...")
    final_html = build_html(lang, page_map=page_map)
    with open(html_path, "w", encoding="utf-8") as f:
        f.write(final_html)
        
    print(f"[Pass 3] Compiling finalized PDF with Chrome Headless...")
    success = compile_pdf(html_path, pdf_path)
    if not success:
        print(f"ERROR: Chrome failed to compile finalized PDF {pdf_path}")
        return False
        
    # Pass 4: Stamp Running Headers & Footers
    print(f"[Pass 4] Stamping running headers and footers via PyMuPDF...")
    stamp_running_headers_and_footers(pdf_path, lang)
    
    doc = fitz.open(pdf_path)
    total_pages = len(doc)
    doc.close()
    
    size_mb = os.path.getsize(pdf_path) / (1024 * 1024)
    print(f"SUCCESS: {pdf_filename} successfully generated! Total Pages: {total_pages} | Size: {size_mb:.2f} MB")
    return True

if __name__ == "__main__":
    vi_ok = build_full_manual("vi")
    en_ok = build_full_manual("en")
    
    if vi_ok and en_ok:
        print("\n[ALL COMPLETE] Both VI and EN Complete Industrial User Manuals generated cleanly!")
        sys.exit(0)
    else:
        print("\n[FAILED] Manual generation encountered errors.")
        sys.exit(1)
