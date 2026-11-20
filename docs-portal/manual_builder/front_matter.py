# -*- coding: utf-8 -*-
"""
SynaptiX Industrial User Manual Front Matter & Back Matter Generator
Clean, minimalist, corporate engineering layout adhering to ISO/IEC/IEEE 26514 & ANSI Z535.6.
Official Corporate Identity: SYNAPTIX TECHNOLOGY JSC (synaptix-io.com)
"""

import os

PUBLIC_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "public"))

def get_logo_path() -> str:
    logo_file = os.path.join(PUBLIC_DIR, "synaptix_logo.png")
    return f"file:///{os.path.abspath(logo_file).replace('\\', '/')}"

def generate_cover(lang: str) -> str:
    logo_url = get_logo_path()
    
    if lang == "vi":
        category = "HỆ SINH THÁI THIẾT BỊ TỰ ĐỘNG HÓA SYNAPTIX IIOT"
        title = "SynaptiX IDE"
        subtitle = "HƯỚNG DẪN SỬ DỤNG & VẬN HÀNH KỸ THUẬT"
        lead = "Môi trường Kỹ thuật Lập trình Logic FBD, Giám sát Thời gian thực & Quản trị Toàn diện Hệ sinh thái Thiết bị SynaptiX IIoT."
        badges = ["PHIÊN BẢN 2.15 LTS", "CHUẨN IEC 61131-3", "SIL 2 / SIL 3 CAPABLE"]
        footer_brand = "SynaptiX &ndash; Industrial IoT"
        footer_meta = "Phiên bản 2.15 &bull; Tháng 11 / 2026"
        corp_tag = "SYNAPTIX TECHNOLOGY JSC"
    else:
        category = "SYNAPTIX IIOT AUTOMATION ECOSYSTEM"
        title = "SynaptiX IDE"
        subtitle = "SYSTEM OPERATING & USER MANUAL"
        lead = "Industrial Engineering Environment for FBD Logic Programming, Real-time Telemetry & Full-lifecycle SynaptiX IIoT Device Management."
        badges = ["VERSION 2.15 LTS", "IEC 61131-3 COMPLIANT", "SIL 2 / SIL 3 CAPABLE"]
        footer_brand = "SynaptiX &ndash; Industrial IoT"
        footer_meta = "Version 2.15 &bull; November 2026"
        corp_tag = "SYNAPTIX TECHNOLOGY JSC"

    badges_html = "".join([f'<span class="cover-badge">{b}</span>' for b in badges])

    return f"""
    <div class="page-cover">
      <div class="cover-header">
        <img src="{logo_url}" alt="SynaptiX Logo" class="cover-logo">
        <span class="cover-corp-tag">{corp_tag}</span>
      </div>

      <div class="cover-body">
        <div class="cover-category">{category}</div>
        <h1 class="cover-title">{title}</h1>
        <div class="cover-subtitle">{subtitle}</div>
        <div class="cover-divider"></div>
        <p class="cover-lead">{lead}</p>
      </div>

      <div class="cover-footer">
        <span class="cover-footer-left">{footer_brand}</span>
        <span class="cover-footer-right">{footer_meta}</span>
      </div>
    </div>
    """

def generate_doc_control(lang: str) -> str:
    if lang == "vi":
        title = "KIỂM SOÁT TÀI LIỆU & THÔNG TIN PHÁP LÝ"
        sec1_title = "1. Thông tin Nhận diện Tài liệu & Bản quyền"
        sec2_title = "2. Lịch sử Soát xét & Phiên bản Phần mềm"
        sec3_title = "3. Tiêu chuẩn Quốc tế & Quy chuẩn Kỹ thuật Tuân thủ"
        
        info_rows = [
            ("Mã tài liệu / Số hiệu", "<strong>DOC-SPX-UM-001</strong> (User Manual)"),
            ("Phiên bản phần mềm áp dụng", "SynaptiX IDE &amp; Runtime Engine v2.15.0 LTS (.NET 8 64-bit)"),
            ("Ngày phát hành", "Tháng 11 Năm 2026 (Xuất bản chính thức)"),
            ("Đơn vị sở hữu & Phát hành", "<strong>CÔNG TY CỔ PHẦN CÔNG NGHỆ SYNAPTIX</strong> (SYNAPTIX TECHNOLOGY JSC)"),
            ("Mã số thuế / Đăng ký KD", "0111000485 &bull; Sở Kế hoạch &amp; Đầu tư TP. Hà Nội cấp"),
            ("Địa chỉ trụ sở", "P109, Tầng 2, Tòa PZ4, Lô đất F4-CCTP1, KĐT Vinhomes Smart City, Nam Từ Liêm, Hà Nội"),
            ("Hotline Kỹ thuật / CSKH", "<strong>(+84) 972 141 491</strong> &bull; Hỗ trợ 24/7"),
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
            ("Document ID / Number", "<strong>DOC-SPX-UM-001</strong> (User Operating Manual)"),
            ("Software Target Release", "SynaptiX IDE &amp; Runtime Engine v2.15.0 LTS (.NET 8 64-bit)"),
            ("Publication Date", "November 2026 (Official Release)"),
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
    <div class="page-doc-control">
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

def generate_safety_conventions(lang: str) -> str:
    if lang == "vi":
        title = "QUY ƯỚC AN TOÀN CÔNG NGHIỆP & LƯU Ý VẬN HÀNH"
        lead = "Để bảo vệ tính mạng kỹ sư vận hành, ngăn ngừa hư hỏng cơ cấu chấp hành và duy trì tính ổn định của dây chuyền sản xuất, mọi kỹ sư lập trình SynaptiX IDE bắt buộc phải thấu hiểu và tuân thủ các quy chuẩn an toàn quốc tế theo tiêu chuẩn <strong>ANSI Z535.6</strong> và <strong>ISO 7010</strong> được sử dụng xuyên suốt tài liệu này:"
        
        d_head = "🛑 NGUY HIỂM (DANGER)"
        d_text = "Chỉ thị tình huống cực kỳ nguy hiểm cận kề. Nếu không tuân thủ nghiêm ngặt, tai nạn điện giật, kẹp cơ khí hoặc nổ áp lực <strong>CHẮC CHẮN sẽ dẫn đến tử vong hoặc thương tật vĩnh viễn</strong>. Luôn ngắt cầu dao tổng và khóa thẻ an toàn (LOTO) trước khi can thiệp đấu nối tủ điện."
        
        w_head = "⚠️ CẢNH BÁO (WARNING)"
        w_text = "Chỉ thị tình huống nguy hiểm tiềm tàng. Nếu bỏ qua, có thể dẫn đến <strong>thương tích nghiêm trọng cho người vận hành hoặc hư hỏng nặng cơ cấu máy móc</strong>. Tuyệt đối không nạp chương trình logic mới trực tiếp vào thiết bị khi máy đang hoạt động."
        
        c_head = "⚡ THẬN TRỌNG (CAUTION)"
        c_text = "Chỉ thị rủi ro kỹ thuật có thể gây <strong>hỏng hóc phần cứng, mất dữ liệu cấu hình hoặc gián đoạn chu trình sản xuất</strong>. Thường liên quan đến cắm ngược cực nguồn 24V DC, sai phân cực RS-485 A/B hoặc cấp quá điện áp vào ngõ vào tương tự AI."
        
        n_head = "ℹ️ LƯU Ý KỸ THUẬT (NOTICE)"
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
        
        d_head = "🛑 DANGER"
        d_text = "Indicates an imminently hazardous situation which, if not avoided, <strong>WILL result in death or catastrophic permanent injury</strong>. Always de-energize and execute Lockout/Tagout (LOTO) procedures on main industrial switchgear prior to accessing control terminals."
        
        w_head = "⚠️ WARNING"
        w_text = "Indicates a potentially hazardous situation which, if not avoided, <strong>COULD result in severe personal injury or serious mechanical equipment destruction</strong>. Never deploy unverified logic programs directly to actuators while plant equipment is actively moving."
        
        c_head = "⚡ CAUTION"
        c_text = "Indicates a technical condition that may result in <strong>equipment damage, firmware corruption, or costly line downtime</strong>. Typically involves reverse 24V DC supply polarity, inverted RS-485 A/B signal wiring, or over-voltage on AI analog pins."
        
        n_head = "ℹ️ NOTICE"
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
    <div class="page-safety">
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

      <div style="margin-top: 5mm; border-top: 1pt solid #004f71; padding-top: 3mm;">
        <div style="font-size: 8.5pt; font-weight: 800; color: #004f71; text-transform: uppercase; margin-bottom: 2mm;">{rule_h}</div>
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

def generate_toc(lang: str, chapters_data: list, page_map: dict = None) -> str:
    page_map = page_map or {}
    title = "MỤC LỤC TÀI LIỆU (TABLE OF CONTENTS)" if lang == "vi" else "TABLE OF CONTENTS"
    
    rows_html = []
    for ch in chapters_data:
        ch_title = ch["title"]
        ch_page = page_map.get(ch_title, 5)
        
        rows_html.append(f"""
        <div class="toc-row-ch">
          <span class="toc-name">{ch_title}</span>
          <span class="toc-dots"></span>
          <span class="toc-page-num">{ch_page}</span>
        </div>
        """)
        
        for sec_num, sec_title in ch["sections"]:
            sec_key = f"{sec_num} {sec_title}"
            sec_page = page_map.get(sec_key, ch_page)
            rows_html.append(f"""
            <div class="toc-row-sub">
              <span class="toc-name">{sec_num} {sec_title}</span>
              <span class="toc-dots"></span>
              <span class="toc-page-num">{sec_page}</span>
            </div>
            """)
            
    return f"""
    <div class="page-toc">
      <h1 class="toc-title">{title}</h1>
      <div class="toc-table">
        {''.join(rows_html)}
      </div>
    </div>
    """

def generate_back_cover(lang: str) -> str:
    logo_url = get_logo_path()
    
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
    <div class="page-back-cover">
      <div class="back-cover-top">
        <img src="{logo_url}" alt="SynaptiX Logo" class="back-cover-logo">
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
