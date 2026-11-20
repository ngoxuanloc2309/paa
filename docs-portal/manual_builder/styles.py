# -*- coding: utf-8 -*-
"""
SynaptiX Industrial Documentation Master CSS & Typography Engine
Standards: ISO/IEC/IEEE 26514:2022, IEC/IEEE 82079-1:2019, ANSI Z535.6, ANSI/ISA-101
Color Palette: International Industrial Navy (Pantone 301 C #004F71), Deep Charcoal #1E293B
"""

MASTER_PRINT_CSS = """
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
      font-family: Arial, "Helvetica Neue", Helvetica, "Segoe UI", sans-serif;
      font-size: 9.2pt;
      line-height: 1.40;
      color: #1e293b;
      background-color: #ffffff;
    }

    /* ==========================================================================
       TRANG BÌA (FRONT COVER) - TỐI GIẢN & SANG TRỌNG
       ========================================================================== */
    .page-cover {
      width: 180mm;
      height: 258mm;
      max-height: 258mm;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      page-break-after: always;
      padding: 4mm 0 2mm 0;
    }

    .cover-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-bottom: 4mm;
      border-bottom: 2pt solid #004f71;
    }

    .cover-logo {
      height: 12mm;
      width: auto;
      object-fit: contain;
      border: none !important;
      border-radius: 0 !important;
      box-shadow: none !important;
      background: transparent !important;
    }

    .cover-corp-tag {
      font-size: 8pt;
      font-weight: 700;
      letter-spacing: 1px;
      color: #004f71;
      text-transform: uppercase;
    }

    .cover-body {
      flex: 1 1 auto;
      display: flex;
      flex-direction: column;
      justify-content: center;
      padding-top: 10mm;
      padding-bottom: 15mm;
    }

    .cover-category {
      font-size: 9pt;
      font-weight: 700;
      letter-spacing: 1.5px;
      color: #1e6091;
      text-transform: uppercase;
      margin-bottom: 4mm;
    }

    .cover-title {
      font-size: 32pt;
      font-weight: 900;
      color: #004f71;
      line-height: 1.05;
      letter-spacing: -0.5px;
      margin: 0 0 3mm 0;
    }

    .cover-subtitle {
      font-size: 15pt;
      font-weight: 700;
      color: #0f172a;
      line-height: 1.30;
      margin: 0 0 5mm 0;
    }

    .cover-divider {
      width: 45mm;
      height: 2.5pt;
      background-color: #004f71;
      margin: 0 0 5mm 0;
      border-radius: 1px;
    }

    .cover-lead {
      font-size: 9.5pt;
      color: #475569;
      line-height: 1.50;
      max-width: 160mm;
      margin: 0 0 8mm 0;
    }

    .cover-badges-row {
      display: flex;
      gap: 3mm;
      margin-top: 4mm;
    }

    .cover-badge {
      display: inline-block;
      font-size: 7.5pt;
      font-weight: 600;
      color: #004f71;
      background-color: #f1f5f9;
      border: 0.5pt solid #cbd5e1;
      border-radius: 3px;
      padding: 1.2mm 2.8mm;
      letter-spacing: 0.5px;
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

    /* ==========================================================================
       TRANG 2: KIỂM SOÁT TÀI LIỆU & PHÁP LÝ (DOCUMENT CONTROL)
       ========================================================================== */
    .page-doc-control {
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
      font-weight: 800;
      color: #0f172a;
      border-bottom: none !important;
      padding-bottom: 0;
      margin: 0 0 5mm 0;
    }

    .doc-control-section {
      margin-bottom: 4.5mm;
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
      margin-bottom: 2mm;
    }

    .doc-control-table td, .doc-control-table th {
      padding: 1.8mm 2mm;
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

    /* ==========================================================================
       TRANG 3: QUY ƯỚC AN TOÀN CÔNG NGHIỆP (ANSI Z535.6)
       ========================================================================== */
    .page-safety {
      width: 180mm;
      height: 258mm;
      page-break-after: always;
      padding-top: 2mm;
    }

    .safety-title {
      font-size: 14pt;
      font-weight: 800;
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
      font-weight: 800;
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
      color: #004f71;
      font-size: 8.5pt;
      margin-bottom: 1mm;
    }

    /* ==========================================================================
       TRANG 4: MỤC LỤC CHUẨN XÁC (TABLE OF CONTENTS)
       ========================================================================== */
    .page-toc {
      width: 180mm;
      height: 258mm;
      page-break-after: always;
      padding-top: 2mm;
    }

    .toc-title {
      font-size: 14pt;
      font-weight: 800;
      color: #0f172a;
      border-bottom: none !important;
      padding-bottom: 0;
      margin: 0 0 5mm 0;
    }

    .toc-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 8.5pt;
    }

    .toc-row-ch {
      display: flex;
      align-items: baseline;
      font-weight: 700;
      color: #0f172a;
      margin-top: 2mm;
      margin-bottom: 0.6mm;
    }

    .toc-row-sub {
      display: flex;
      align-items: baseline;
      font-weight: 400;
      color: #334155;
      padding-left: 4.5mm;
      margin-bottom: 0.5mm;
      font-size: 8pt;
    }

    .toc-name { flex-shrink: 0; }
    .toc-dots {
      flex-grow: 1;
      border-bottom: 0.5pt dotted #94a3b8;
      margin: 0 2mm;
      position: relative;
      top: -2px;
    }
    .toc-page-num {
      flex-shrink: 0;
      font-weight: 700;
      color: #0f172a;
    }

    /* ==========================================================================
       BODY CHAPTERS & SECTIONS
       ========================================================================== */
    .chapter-start {
      page-break-before: always;
      padding-top: 2mm;
    }

    .chapter-header-box {
      border-bottom: 1.5pt solid #004f71;
      padding-bottom: 2mm;
      margin-bottom: 4.5mm;
      display: flex;
      justify-content: space-between;
      align-items: flex-end;
    }

    .chapter-num-badge {
      font-size: 24pt;
      font-weight: 300;
      color: #94a3b8;
      line-height: 1;
      margin-right: 3mm;
    }

    .chapter-main-title {
      font-size: 15pt;
      font-weight: 800;
      color: #0f172a;
      margin: 0;
      line-height: 1.20;
    }

    .section-title {
      font-size: 11.5pt;
      font-weight: 700;
      color: #0f172a;
      margin: 4.5mm 0 2mm 0;
      padding-bottom: 0.8mm;
      border-bottom: none !important;
      page-break-after: avoid;
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
      page-break-after: avoid;
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

    /* BẢNG BIỂU DỮ LIỆU IBM CARBON DATA TABLES */
    table {
      width: 100%;
      border-collapse: collapse;
      margin: 2.5mm 0 3.5mm 0;
      font-size: 8pt;
      page-break-inside: avoid;
    }

    th {
      background-color: #f1f5f9;
      color: #0f172a;
      font-weight: 700;
      text-align: left;
      padding: 1.8mm 2mm;
      border-top: 1pt solid #004f71;
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

    /* CALLOUT BOXES IN ẤN */
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

    /* HÌNH ẢNH & ĐỊNH DANH CAPTION CHUẨN IEC/IEEE */
    .figure-container {
      margin: 3mm auto 3.5mm auto;
      text-align: center;
      page-break-inside: avoid;
    }

    .figure-container img {
      max-width: 96%;
      max-height: 110mm;
      height: auto;
      display: block;
      margin: 0 auto;
      border: 0.5pt solid #cbd5e1;
      border-radius: 3px;
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

    /* VECTOR CIRCUITS COMPACT (.synx-node-chain) */
    .synx-node-chain {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 1.5mm;
      margin: 2.5mm 0;
      padding: 2mm 1mm;
      background-color: #f8fafc;
      border: 0.5pt solid #cbd5e1;
      border-radius: 3px;
      page-break-inside: avoid;
      overflow-x: hidden;
    }

    .synx-canvas-node {
      background-color: #ffffff;
      border: 0.75pt solid #cbd5e1;
      border-radius: 3px;
      min-width: 28mm;
      max-width: 38mm;
      box-shadow: 0 0.5pt 1.5pt rgba(0,0,0,0.05);
    }

    .synx-node-header {
      padding: 1mm 1.5mm;
      font-size: 6.8pt;
      font-weight: 800;
      color: #ffffff;
      border-top-left-radius: 2px;
      border-top-right-radius: 2px;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .synx-canvas-node.is-input .synx-node-header { background-color: #0284c7; }
    .synx-canvas-node.is-trigger .synx-node-header { background-color: #7c3aed; }
    .synx-canvas-node.is-comparator .synx-node-header { background-color: #d97706; }
    .synx-canvas-node.is-guard .synx-node-header { background-color: #dc2626; }
    .synx-canvas-node.is-action .synx-node-header { background-color: #16a34a; }
    .synx-canvas-node.is-timer .synx-node-header { background-color: #059669; }
    .synx-canvas-node.is-math .synx-node-header { background-color: #0891b2; }

    .synx-node-body {
      padding: 1.5mm 1.5mm;
      font-size: 6.8pt;
      line-height: 1.25;
      color: #334155;
    }

    .synx-node-tag {
      font-weight: 700;
      color: #0f172a;
      display: block;
      font-family: 'Cascadia Code', Consolas, monospace;
    }

    .synx-node-alias {
      color: #64748b;
      font-size: 6.2pt;
      display: block;
    }

    .synx-node-val {
      font-weight: 700;
      color: #004f71;
      display: block;
      margin-top: 0.5mm;
    }

    .synx-node-wire {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      width: 7mm;
    }

    .synx-wire-line {
      width: 100%;
      height: 1pt;
      background-color: #94a3b8;
    }

    .synx-wire-badge {
      font-size: 5.5pt;
      font-weight: 700;
      background-color: #e2e8f0;
      color: #334155;
      padding: 0.2mm 0.8mm;
      border-radius: 1.5px;
      margin-top: 0.5mm;
    }

    /* TRANG BÌA SAU (BACK COVER) */
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

    .page-back-cover {
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
      font-weight: 800;
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
