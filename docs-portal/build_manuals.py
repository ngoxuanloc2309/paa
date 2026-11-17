#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
SynaptiX Industrial Documentation Generator
Builds international manufacturer-grade PDF manuals (VI & EN) from VitePress sources.
Standards: ISO/IEC/IEEE 26514:2022, IEC/IEEE 82079-1:2019, ANSI/ISA-101
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
# Chapter & File Mapping
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
            "title": "Chương 7: DTU Gateway Studio & Mạng Zigbee Mesh",
            "files": [
                ("7.1", "Không gian Cấu hình Nhanh DTU Gateway Studio", "tools/dtu-gateway-studio.md"),
                ("7.2", "Mạng Không dây Zigbee Mesh & Cảm biến Thông minh", "tools/zigbee-mesh-config.md"),
            ]
        },
        {
            "id": "ch8",
            "num": 8,
            "title": "Chương 8: Mô phỏng Offline & Nạp An toàn Xuống Thiết bị",
            "files": [
                ("8.1", "Mô phỏng Logic Offline & Gỡ lỗi Điểm dừng", "tools/simulator-and-live-debug.md"),
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
# Pre-processing & Markdown Conversion
# ---------------------------------------------------------------------------
def preprocess_markdown(content: str) -> str:
    # 1. Strip doc-meta-bar
    content = re.sub(r'<div class="doc-meta-bar">.*?</div>', '', content, flags=re.DOTALL)
    
    # 2. Fix image paths to file:/// absolute
    abs_img_dir = os.path.abspath(os.path.join(PUBLIC_DIR, "images")).replace("\\", "/")
    content = content.replace('src="/images/', f'src="file:///{abs_img_dir}/')
    content = re.sub(r'\]\(/images/(.*?)\)', rf'](file:///{abs_img_dir}/\1)', content)
    
    # 3. Convert ::: containers to styled callouts
    def replace_container(match):
        ctype = match.group(1).lower()
        title = match.group(2).strip() if match.group(2) else ""
        body = match.group(3).strip()
        
        icons = {
            "tip": "💡",
            "warning": "⚠️",
            "danger": "🛑",
            "info": "ℹ️"
        }
        icon = icons.get(ctype, "📌")
        if not title:
            titles = {
                "tip": "MẸO KỸ THUẬT / TIP",
                "warning": "CẢNH BÁO / WARNING",
                "danger": "NGUY HIỂM / DANGER",
                "info": "THÔNG TIN / NOTICE"
            }
            title = titles.get(ctype, ctype.upper())
            
        return f'<div class="callout callout-{ctype}"><div class="callout-header"><span class="callout-icon">{icon}</span> <strong class="callout-title">{title}</strong></div><div class="callout-body">\n\n{body}\n\n</div></div>'

    # Match ::: tip [title] ... :::
    pattern = r':::\s*(\w+)(?:[ \t]+([^\n\r]+))?\r?\n(.*?)\r?\n:::'
    content = re.sub(pattern, replace_container, content, flags=re.DOTALL)
    
    return content

def render_section_content(md_parser: MarkdownIt, sec_num: str, sec_title: str, file_rel_path: str, lang: str) -> str:
    full_path = os.path.join(DOCS_PORTAL, lang, file_rel_path)
    if not os.path.exists(full_path):
        return f'<div class="missing-file">File not found: {file_rel_path}</div>'
        
    with open(full_path, "r", encoding="utf-8") as fp:
        raw_md = fp.read()
        
    # Strip the first H1 header since we generate a standardized section title
    raw_md = re.sub(r'^#\s+.*?\r?\n', '', raw_md)
    processed_md = preprocess_markdown(raw_md)
    
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

# ---------------------------------------------------------------------------
# Master CSS
# ---------------------------------------------------------------------------
MASTER_CSS = """
    @page {
      size: A4 portrait;
      margin: 18mm 14mm 18mm 14mm;
    }

    * {
      box-sizing: border-box;
      -webkit-print-color-adjust: exact;
      print-color-adjust: exact;
    }

    html, body {
      margin: 0;
      padding: 0;
      font-family: Arial, "Helvetica Neue", Helvetica, sans-serif;
      font-size: 9.5pt;
      line-height: 1.55;
      color: #0f172a;
      background-color: #ffffff;
    }

    /* ==========================================================================
       TRANG BÌA (COVER PAGE)
       ========================================================================== */
    .page-sheet-cover {
      width: 182mm;
      height: 257mm;
      max-height: 257mm;
      display: flex;
      flex-direction: column;
      justify-content: flex-start;
      page-break-after: always;
      padding: 0;
    }

    .cover-top {
      flex-shrink: 0;
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-bottom: 3.5mm;
      border-bottom: 1.5pt solid #006487;
    }

    .cover-brand {
      display: flex;
      align-items: center;
    }

    .cover-logo {
      height: 10mm;
      width: auto;
      object-fit: contain;
    }

    .cover-doc-badge {
      font-size: 8pt;
      font-weight: 600;
      color: #475569;
      background-color: #f1f5f9;
      border: 0.5pt solid #cbd5e1;
      border-radius: 4px;
      padding: 1.2mm 3.2mm;
      letter-spacing: 0.8px;
      font-family: Arial, "Helvetica Neue", Helvetica, sans-serif;
    }

    .cover-content {
      flex: 1 1 auto;
      display: flex;
      flex-direction: column;
      justify-content: flex-start;
      padding-top: 12mm;
    }

    .cover-title-group {
      margin-bottom: 8mm;
    }

    .cover-doc-category {
      font-size: 9pt;
      font-weight: 600;
      color: #006487;
      text-transform: uppercase;
      letter-spacing: 1.2px;
      margin-bottom: 2mm;
    }

    .cover-product-title {
      font-size: 30pt;
      font-weight: bold;
      color: #0f172a;
      margin: 0 0 2mm 0;
      line-height: 1.05;
      letter-spacing: -0.3px;
    }

    .cover-product-subtitle {
      font-size: 13pt;
      color: #1e293b;
      margin: 0 0 2.5mm 0;
      line-height: 1.35;
      font-weight: 600;
    }

    .cover-title-divider {
      width: 32mm;
      height: 2pt;
      background-color: #006487;
      margin: 3mm 0 4mm 0;
      border-radius: 1px;
    }

    .cover-product-lead {
      font-size: 9pt;
      color: #475569;
      line-height: 1.55;
      margin: 0;
      max-width: 165mm;
    }

    .cover-spec-section {
      margin-top: 5mm;
      margin-bottom: 4mm;
    }

    .cover-spec-heading {
      font-size: 8.5pt;
      font-weight: 700;
      color: #006487;
      text-transform: uppercase;
      letter-spacing: 0.8px;
      margin-bottom: 2mm;
    }

    .cover-spec-table {
      width: 100%;
      border-collapse: collapse;
      border-top: 1.5pt solid #006487;
      border-bottom: 1pt solid #cbd5e1;
    }

    .cover-spec-table td {
      padding: 2.2mm 2mm;
      border-bottom: 0.5pt solid #e2e8f0;
      color: #334155;
      font-size: 8.5pt;
      vertical-align: top;
      line-height: 1.45;
    }

    .cover-spec-table tr:last-child td {
      border-bottom: none;
    }

    .cover-spec-key {
      width: 26%;
      font-weight: 600;
      color: #475569;
    }

    .cover-spec-val {
      width: 74%;
      color: #1e293b;
    }

    .cover-spec-val strong {
      color: #0f172a;
    }

    .cover-precaution {
      border-left: 2pt solid #006487;
      padding: 1.8mm 0 1.8mm 3.5mm;
      font-size: 8pt;
      line-height: 1.55;
      color: #64748b;
      margin-top: 5mm;
    }

    .cover-precaution strong {
      color: #334155;
    }

    .cover-footer {
      flex-shrink: 0;
      margin-top: auto;
      border-top: 0.5pt solid #e2e8f0;
      padding-top: 3mm;
      display: flex;
      justify-content: space-between;
      align-items: flex-end;
      font-size: 8pt;
      color: #94a3b8;
      line-height: 1.5;
    }

    .cover-footer-brand {
      font-weight: 500;
      color: #64748b;
      font-size: 8pt;
      margin-bottom: 0.5mm;
    }

    .cover-footer-meta {
      font-weight: 500;
      color: #64748b;
      font-size: 8pt;
      margin-bottom: 0.5mm;
    }

    .cover-footer-sub {
      color: #94a3b8;
      font-size: 7.5pt;
    }

    /* ==========================================================================
       TRANG MỤC LỤC (TABLE OF CONTENTS)
       ========================================================================== */
    .page-sheet-toc {
      width: 182mm;
      height: 257mm;
      max-height: 257mm;
      display: flex;
      flex-direction: column;
      justify-content: flex-start;
      page-break-after: always;
      padding: 0;
    }

    h1.toc-heading {
      font-size: 15pt;
      font-weight: bold;
      color: #0f172a;
      margin: 0 0 3.5mm 0;
      padding-bottom: 1.5mm;
      border-bottom: 1.5pt solid #006487;
    }

    .toc-list {
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .toc-item-ch {
      display: flex;
      align-items: baseline;
      font-size: 9.5pt;
      font-weight: bold;
      color: #0f172a;
      margin-top: 2.2mm;
      margin-bottom: 0.5mm;
    }

    .toc-item-sub {
      display: flex;
      align-items: baseline;
      font-size: 8.5pt;
      font-weight: normal;
      color: #334155;
      padding-left: 5mm;
      margin-bottom: 0.4mm;
      font-family: Arial, "Helvetica Neue", Helvetica, sans-serif;
    }

    .toc-title {
      flex-shrink: 0;
    }

    .toc-dots {
      flex-grow: 1;
      border-bottom: 0.75pt dotted #94a3b8;
      margin: 0 2.5mm;
      position: relative;
      top: -2.5px;
    }

    .toc-page {
      flex-shrink: 0;
      font-family: Arial, "Helvetica Neue", Helvetica, sans-serif;
      font-size: 8.5pt;
      font-weight: bold;
      color: #0f172a;
    }

    /* ==========================================================================
       THÂN VĂN BẢN (BODY CHAPTERS & SECTIONS)
       ========================================================================== */
    .chapter-start {
      page-break-before: always;
      padding-top: 2mm;
    }

    .chapter-main-title {
      font-size: 16pt;
      font-weight: bold;
      color: #0f172a;
      margin: 0 0 5mm 0;
      padding-bottom: 2mm;
      border-bottom: 1.5pt solid #006487;
    }

    .section-container {
      margin-bottom: 7mm;
    }

    .section-title {
      font-size: 11pt;
      font-weight: bold;
      color: #006487;
      margin: 5mm 0 2.5mm 0;
      padding-bottom: 1mm;
      border-bottom: 0.5pt solid #e2e8f0;
      page-break-after: avoid;
    }

    .section-title .sec-num {
      color: #64748b;
      margin-right: 1.5mm;
    }

    .subsection-title {
      font-size: 10pt;
      font-weight: bold;
      color: #1e293b;
      margin: 4mm 0 2mm 0;
      page-break-after: avoid;
    }

    h4.sub-subsection-title, h4 {
      font-size: 9.5pt;
      font-weight: bold;
      color: #334155;
      margin: 3mm 0 1.5mm 0;
      page-break-after: avoid;
    }

    p {
      margin: 0 0 2.5mm 0;
      text-align: justify;
    }

    ul, ol {
      margin: 0 0 3mm 0;
      padding-left: 5mm;
    }

    li {
      margin-bottom: 1.2mm;
      line-height: 1.5;
    }

    /* BẢNG BIỂU CHUẨN IBM CARBON DATA TABLE */
    table {
      width: 100%;
      border-collapse: collapse;
      margin: 3.5mm 0 4.5mm 0;
      font-size: 8.5pt;
      page-break-inside: avoid;
    }

    th {
      background-color: #f1f5f9;
      color: #0f172a;
      font-weight: bold;
      text-align: left;
      padding: 2.2mm 2.5mm;
      border-top: 1pt solid #cbd5e1;
      border-bottom: 1pt solid #cbd5e1;
    }

    td {
      padding: 2mm 2.5mm;
      border-bottom: 0.5pt solid #e2e8f0;
      vertical-align: top;
      color: #334155;
    }

    tr:nth-child(even) td {
      background-color: #f8fafc;
    }

    /* CALLOUT BOXES (ANSI Z535.6) */
    .callout {
      margin: 3.5mm 0 4.5mm 0;
      padding: 2.5mm 3.5mm;
      border-radius: 4px;
      page-break-inside: avoid;
      font-size: 8.5pt;
      line-height: 1.5;
    }

    .callout-header {
      display: flex;
      align-items: center;
      gap: 2mm;
      margin-bottom: 1.5mm;
    }

    .callout-icon {
      font-size: 10pt;
    }

    .callout-title {
      font-size: 8.5pt;
      letter-spacing: 0.5px;
    }

    .callout-body p:last-child {
      margin-bottom: 0;
    }

    .callout-tip {
      background-color: #f0fdf4;
      border-left: 3pt solid #107c41;
      color: #14532d;
    }
    .callout-tip .callout-title { color: #107c41; }

    .callout-warning {
      background-color: #fffbeb;
      border-left: 3pt solid #c05621;
      color: #78350f;
    }
    .callout-warning .callout-title { color: #c05621; }

    .callout-danger {
      background-color: #fef2f2;
      border-left: 3pt solid #b91c1c;
      color: #7f1d1d;
    }
    .callout-danger .callout-title { color: #b91c1c; }

    .callout-info {
      background-color: #f0f9ff;
      border-left: 3pt solid #006487;
      color: #0c4a6e;
    }
    .callout-info .callout-title { color: #006487; }

    /* CODE & MONOSPACE */
    code {
      font-family: 'Cascadia Code', Consolas, monospace;
      font-size: 8pt;
      background-color: #f1f5f9;
      padding: 0.2mm 1.2mm;
      border-radius: 3px;
      color: #0f172a;
      border: 0.5pt solid #e2e8f0;
    }

    pre {
      background-color: #f8fafc;
      border: 1pt solid #e2e8f0;
      border-radius: 4px;
      padding: 2.5mm 3.5mm;
      margin: 3mm 0 4mm 0;
      overflow-x: auto;
      page-break-inside: avoid;
    }

    pre code {
      background: transparent;
      padding: 0;
      border: none;
      font-size: 8pt;
      line-height: 1.45;
    }

    /* HÌNH ẢNH MINH HỌA */
    img {
      max-width: 95%;
      height: auto;
      display: block;
      margin: 4mm auto;
      border: 1pt solid #cbd5e1;
      border-radius: 4px;
      page-break-inside: avoid;
    }

    /* CARDS & GRIDS TRONG TÀI LIỆU */
    .synx-arch-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 3.5mm;
      margin: 3.5mm 0;
      page-break-inside: avoid;
    }

    .synx-arch-card {
      background-color: #f8fafc;
      border: 1pt solid #e2e8f0;
      border-radius: 4px;
      padding: 2.5mm 3mm;
    }

    .synx-arch-header {
      font-weight: bold;
      color: #006487;
      margin-bottom: 2mm;
      border-bottom: 0.5pt solid #cbd5e1;
      padding-bottom: 1mm;
      font-size: 8.5pt;
    }

    .synx-arch-item {
      font-size: 8pt;
      margin-bottom: 1.5mm;
    }

    .synx-arch-item strong {
      display: block;
      color: #1e293b;
    }

    .synx-arch-badge {
      display: inline-block;
      font-size: 7pt;
      background-color: #e2e8f0;
      color: #475569;
      padding: 0.2mm 1.5mm;
      border-radius: 2px;
      margin-top: 0.5mm;
    }
"""

def generate_cover_html(lang: str) -> str:
    logo_path = f"file:///{os.path.abspath(os.path.join(PUBLIC_DIR, 'synaptix_logo.png')).replace('\\', '/')}"
    
    if lang == "vi":
        badge = "DOC-SPX-MAN-001"
        cat = "HỆ SINH THÁI THIẾT BỊ SYNAPTIX IIOT • HƯỚNG DẪN KỸ THUẬT VẬN HÀNH"
        title = "SynaptiX IDE"
        sub = "Môi trường Kỹ thuật Lập trình Điều khiển & Quản trị Hệ sinh thái Thiết bị SynaptiX IIoT"
        lead = "Phần mềm kỹ thuật đồng hành cùng các dòng thiết bị trong hệ sinh thái SynaptiX IIoT."
        spec_head = "THÔNG SỐ ĐỊNH DANH HỆ THỐNG"
        spec_rows = [
            ("Gói phần mềm", "<strong>SynaptiX IDE & Runtime Engine V2.15.0 LTS</strong>"),
            ("Hệ sinh thái áp dụng", "Toàn diện thiết bị SynaptiX IIoT (XLink Gateway, DTU, Remote I/O, Data Logger, Smart Sensor)"),
            ("Chức năng chính", "Lập trình Logic FBD, Quản trị Mạng IIoT, Giám sát Thời gian thực & Trợ lý AI Copilot"),
            ("Môi trường vận hành", "Hệ điều hành Windows 10 / 11 (64-bit), Microsoft .NET 8 Desktop Runtime"),
            ("Tiêu chuẩn tuân thủ", "IEC 61131-3 &bull; IEC 61508-3 SIL2 &bull; ANSI/ISA-101 &bull; IEC 62443-4-1"),
        ]
        precaution = "<strong>LƯU Ý TRƯỚC KHI VẬN HÀNH:</strong> Vui lòng đọc kỹ toàn bộ tài liệu này trước khi cài đặt phần mềm, thiết lập logic hoặc kết nối với bất kỳ thiết bị nào trong hệ sinh thái SynaptiX. Luôn đảm bảo nút Dừng Khẩn Cấp (E-Stop) sẵn sàng và nguồn động lực được cô lập an toàn trước khi nạp chương trình logic xuống thiết bị."
        brand_meta = "Tài liệu kỹ thuật chính thức &bull; Phiên bản: 2.15.0 &bull; Tháng 11 / 2026"
    else:
        badge = "DOC-SPX-MAN-001"
        cat = "SYNAPTIX IIOT ECOSYSTEM • INDUSTRIAL OPERATING MANUAL"
        title = "SynaptiX IDE"
        sub = "Engineering Environment for Control Programming & SynaptiX IIoT Device Management"
        lead = "Industrial engineering software designed for the SynaptiX IIoT device ecosystem."
        spec_head = "SYSTEM IDENTIFICATION SPECIFICATIONS"
        spec_rows = [
            ("Software Package", "<strong>SynaptiX IDE & Runtime Engine V2.15.0 LTS</strong>"),
            ("Applicable Ecosystem", "Comprehensive SynaptiX IIoT Devices (XLink Gateway, DTU, Remote I/O, Data Logger, Smart Sensor)"),
            ("Core Capabilities", "FBD Logic Programming, Industrial IIoT Provisioning, Real-time Telemetry & AI Copilot"),
            ("Runtime Environment", "Windows 10 / 11 (64-bit), Microsoft .NET 8 Desktop Runtime"),
            ("Standards Compliance", "IEC 61131-3 &bull; IEC 61508-3 SIL2 &bull; ANSI/ISA-101 &bull; IEC 62443-4-1"),
        ]
        precaution = "<strong>SAFETY NOTICE BEFORE OPERATION:</strong> Please read this entire manual thoroughly before installing the software, designing logic diagrams, or connecting to any SynaptiX hardware. Always ensure that the Emergency Stop (E-Stop) mechanism is armed and hazardous power is isolated before deploying logic programs to field devices."
        brand_meta = "Official Technical Manual &bull; Version: 2.15.0 &bull; November 2026"

    spec_table_html = "".join([f"<tr><td class='cover-spec-key'>{k}</td><td class='cover-spec-val'>{v}</td></tr>" for k, v in spec_rows])

    return f"""
    <div class="page-sheet-cover">
      <div class="cover-top">
        <div class="cover-brand">
          <img src="{logo_path}" alt="SynaptiX Logo" class="cover-logo">
        </div>
        <div class="cover-doc-badge">{badge}</div>
      </div>

      <div class="cover-content">
        <div class="cover-title-group">
          <div class="cover-doc-category">{cat}</div>
          <h1 class="cover-product-title">{title}</h1>
          <div class="cover-product-subtitle">{sub}</div>
          <div class="cover-title-divider"></div>
          <p class="cover-product-lead">{lead}</p>
        </div>

        <div class="cover-spec-section">
          <div class="cover-spec-heading">{spec_head}</div>
          <table class="cover-spec-table">
            {spec_table_html}
          </table>
        </div>

        <div class="cover-precaution">
          {precaution}
        </div>
      </div>

      <div class="cover-footer">
        <div>
          <div class="cover-footer-brand">SynaptiX &ndash; Industrial IoT</div>
          <div class="cover-footer-sub">Website: synaptix-io.com &bull; Email: support@synaptix-io.com</div>
        </div>
        <div style="text-align: right;">
          <div class="cover-footer-meta">{brand_meta}</div>
          <div class="cover-footer-sub">docs.synaptix-io.com</div>
        </div>
      </div>
    </div>
    """

def generate_toc_html(lang: str, page_map: dict = None) -> str:
    chapters = CHAPTER_MAPPINGS[lang]
    page_map = page_map or {}
    
    # Split into 2 balanced TOC pages:
    # Page 1: Chapters 1 to 5 (Core Architecture, Installation, Workspaces, Logic Blocks)
    # Page 2: Chapters 6 to 10 + Appendices (Fieldbus, DTU/Zigbee, Simulation, Live Watch, App Notes, Appendices)
    group1 = chapters[:5]
    group2 = chapters[5:]
    
    def render_group(ch_list, part_title):
        items_html = []
        for ch in ch_list:
            ch_title = ch["title"]
            ch_p = page_map.get(ch_title, 4)
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
        toc1 = render_group(group1, "Mục lục — Phần 1: Kiến trúc & Khối hàm Logic")
        toc2 = render_group(group2, "Mục lục — Phần 2: Truyền thông IIoT & Vận hành")
    else:
        toc1 = render_group(group1, "Table of Contents — Part 1: Architecture & Logic")
        toc2 = render_group(group2, "Table of Contents — Part 2: Industrial IIoT & Operations")
        
    return f"{toc1}\n{toc2}"

def build_html(lang: str, page_map: dict = None) -> str:
    md = MarkdownIt("commonmark").enable("table")
    cover_html = generate_cover_html(lang)
    toc_html = generate_toc_html(lang, page_map)
    
    chapters_html = []
    for ch in CHAPTER_MAPPINGS[lang]:
        ch_html = [f'<div class="chapter-start" id="{ch["id"]}"><h1 class="chapter-main-title">{ch["title"]}</h1>']
        for sec_num, sec_title, file_rel in ch["files"]:
            sec_html = render_section_content(md, sec_num, sec_title, file_rel, lang)
            ch_html.append(sec_html)
        ch_html.append('</div>')
        chapters_html.append("\n".join(ch_html))
        
    doc_title = "SynaptiX IDE V2.15.0 - Hướng dẫn Sử dụng Hệ thống" if lang == "vi" else "SynaptiX IDE V2.15.0 - System User Manual"
    
    full_html = f"""<!DOCTYPE html>
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
{toc_html}
{"".join(chapters_html)}
</body>
</html>
"""
    return full_html

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
    
    # We skip Page 1 (Cover), Page 2 (TOC Part 1), Page 3 (TOC Part 2) -> start search at index 3 (Page 4)
    start_idx = 3
    chapters = CHAPTER_MAPPINGS[lang]
    for ch in chapters:
        # Search chapter title
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
            short_str = stitle[:20].lower()
            found_sub = None
            for p_idx in range(start_idx, len(doc)):
                text = doc[p_idx].get_text().lower()
                if num.lower() in text and short_str in text:
                    found_sub = p_idx + 1
                    break
            if found_sub:
                page_map[f"{num} {stitle}"] = found_sub
                
    return page_map

def stamp_running_headers_and_footers(pdf_path: str, lang: str):
    doc = fitz.open(pdf_path)
    total_pages = len(doc)
    
    book_title = "SynaptiX IDE • Hướng dẫn sử dụng" if lang == "vi" else "SynaptiX IDE • User Manual"
    footer_brand = "SYNX-UM-2026 • SynaptiX – Industrial IoT"
    
    font_path = "C:/Windows/Fonts/arial.ttf"
    font = fitz.Font(fontfile=font_path)
    
    # We stamp from page index 3 (Physical Page 4) onwards
    for p_idx in range(3, total_pages):
        page = doc[p_idx]
        p_num = str(p_idx + 1)
        
        # Determine current chapter by inspecting page text or fallback
        page_text = page.get_text()
        curr_ch = ""
        for ch in CHAPTER_MAPPINGS[lang]:
            ch_num_str = f"Chương {ch['num']}" if lang == "vi" else f"Chapter {ch['num']}"
            if ch["num"] in ["A", "B"]:
                ch_num_str = f"Phụ lục {ch['num']}" if lang == "vi" else f"Appendix {ch['num']}"
            if ch_num_str.lower() in page_text.lower():
                curr_ch = ch["title"].split(":")[-1].strip()
                break
        if not curr_ch:
            curr_ch = "Industrial Engineering" if lang == "en" else "Kỹ thuật Vận hành"
            
        # Running Header: book title left, chapter title right
        page.insert_text(fitz.Point(39.69, 30), book_title, fontname="arial", fontfile=font_path, fontsize=8, color=(0.45, 0.52, 0.60))
        
        ch_short = curr_ch[:45]
        ch_len = font.text_length(ch_short, fontsize=8)
        page.insert_text(fitz.Point(555.59 - ch_len, 30), ch_short, fontname="arial", fontfile=font_path, fontsize=8, color=(0.45, 0.52, 0.60))
        page.draw_line(fitz.Point(39.69, 36), fitz.Point(555.59, 36), color=(0.89, 0.92, 0.94), width=0.5)
        
        # Running Footer: brand left, page num right-aligned (muted industrial slate)
        page.draw_line(fitz.Point(39.69, 805), fitz.Point(555.59, 805), color=(0.89, 0.92, 0.94), width=0.5)
        page.insert_text(fitz.Point(39.69, 816), footer_brand, fontname="arial", fontfile=font_path, fontsize=8, color=(0.45, 0.52, 0.60))
        
        p_len = font.text_length(p_num, fontsize=8)
        page.insert_text(fitz.Point(555.59 - p_len, 816), p_num, fontname="arial", fontfile=font_path, fontsize=8, color=(0.45, 0.52, 0.60))
        
    tmp_path = pdf_path + ".stamped.tmp"
    doc.save(tmp_path)
    doc.close()
    if os.path.exists(pdf_path):
        os.remove(pdf_path)
    os.rename(tmp_path, pdf_path)
    print(f"[{lang.upper()}] Stamped running headers & footers on {total_pages - 3} body pages.")

def build_full_manual(lang: str):
    print(f"\n=======================================================")
    print(f" BUILDING FULL MANUAL: {lang.upper()}")
    print(f"=======================================================")
    
    html_name = f"SynaptiX_User_Manual_{lang.upper()}.html"
    pdf_name = f"SynaptiX_User_Manual_{lang.upper()}.pdf"
    html_path = os.path.join(DOWNLOADS_DIR, html_name)
    pdf_path = os.path.join(DOWNLOADS_DIR, pdf_name)
    
    # Pass 1: Build preliminary HTML
    print(f"[Pass 1] Generating initial HTML: {html_name}...")
    initial_html = build_html(lang)
    with open(html_path, "w", encoding="utf-8") as fp:
        fp.write(initial_html)
        
    print(f"[Pass 1] Compiling preliminary PDF with Chrome Headless...")
    if not compile_pdf(html_path, pdf_path):
        print(f"ERROR: Failed to compile preliminary PDF for {lang}")
        return False
        
    # Pass 2: Extract exact page map
    print(f"[Pass 2] Extracting exact physical page coordinates...")
    page_map = extract_page_map(pdf_path, lang)
    print(f"[Pass 2] Discovered {len(page_map)} chapter/section locations.")
    
    # Pass 2: Re-generate HTML with accurate TOC page numbers
    print(f"[Pass 2] Re-generating HTML with synchronized Table of Contents...")
    final_html = build_html(lang, page_map)
    with open(html_path, "w", encoding="utf-8") as fp:
        fp.write(final_html)
        
    print(f"[Pass 2] Compiling finalized PDF with Chrome Headless...")
    if not compile_pdf(html_path, pdf_path):
        print(f"ERROR: Failed to compile finalized PDF for {lang}")
        return False
        
    # Pass 3: Stamp running headers and footers
    print(f"[Pass 3] Stamping running headers and footers via PyMuPDF...")
    stamp_running_headers_and_footers(pdf_path, lang)
    
    doc = fitz.open(pdf_path)
    print(f"SUCCESS: {pdf_name} successfully generated! Total Pages: {len(doc)}")
    return True

if __name__ == "__main__":
    success_vi = build_full_manual("vi")
    success_en = build_full_manual("en")
    
    if success_vi and success_en:
        print("\n[ALL COMPLETE] Both VI and EN Customer User Manuals generated cleanly!")
        sys.exit(0)
    else:
        sys.exit(1)
