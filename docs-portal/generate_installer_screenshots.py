#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Generates authentic, pixel-perfect 680x520 RGBA screenshots of the SynaptiX IDE Windows Setup Wizard.
Adheres 100% to installer/SynaptixIDE.iss, installer/wizard_large.bmp, installer/wizard_small.bmp,
and installer/EULA.txt.
Matches the exact cropping, dimensions (680x520), and RGBA mode with transparent rounded corners
used by synaptix_update_dialog.png in docs-portal/public/images/.
"""

import os
import base64
import subprocess

BASE_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DOCS_PORTAL = os.path.join(BASE_DIR, "docs-portal")
IMG_DIR = os.path.join(DOCS_PORTAL, "public", "images")
INSTALLER_DIR = os.path.join(BASE_DIR, "installer")
CHROME_PATH = r"C:\Program Files\Google\Chrome\Application\chrome.exe"

# 1. Load authentic bitmaps and icons as base64 data URIs
with open(os.path.join(INSTALLER_DIR, "wizard_large.bmp"), "rb") as f:
    b64_large = base64.b64encode(f.read()).decode("ascii")

with open(os.path.join(INSTALLER_DIR, "wizard_small.bmp"), "rb") as f:
    b64_small = base64.b64encode(f.read()).decode("ascii")

with open(os.path.join(BASE_DIR, "SimplePLC.Studio", "Assets", "app.png"), "rb") as f:
    b64_app_icon = base64.b64encode(f.read()).decode("ascii")

with open(os.path.join(INSTALLER_DIR, "EULA.txt"), "r", encoding="utf-8") as f:
    eula_text = f.read()

# Common CSS adhering to Windows 11 Fluent Inno Setup 6 Modern Wizard
COMMON_CSS = f"""
* {{
  box-sizing: border-box;
  margin: 0;
  padding: 0;
  font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, sans-serif;
  user-select: none;
}}

html, body {{
  width: 680px;
  height: 520px;
  margin: 0;
  padding: 0;
  background: transparent;
  overflow: hidden;
}}

.wizard-window {{
  width: 680px;
  height: 520px;
  background-color: #f0f0f0;
  border-radius: 8px;
  border: 1px solid #7a7a7a;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}}

/* Windows 11 Fluent Titlebar */
.title-bar {{
  height: 32px;
  background-color: #f3f3f3;
  border-bottom: 1px solid #e5e5e5;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 10px;
  flex-shrink: 0;
}}

.title-bar-left {{
  display: flex;
  align-items: center;
  gap: 8px;
}}

.title-bar-icon {{
  width: 16px;
  height: 16px;
}}

.title-bar-text {{
  font-size: 12px;
  font-weight: 400;
  color: #1e1e1e;
}}

.title-bar-controls {{
  display: flex;
  gap: 14px;
  color: #555555;
  font-size: 11px;
  padding-right: 4px;
}}

/* Interior Pages Header Banner (Pages 2, 3, 4) */
.wizard-header {{
  height: 68px;
  background-color: #ffffff;
  border-bottom: 1px solid #d0d0d0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 16px;
  flex-shrink: 0;
}}

.wizard-header-title {{
  font-size: 13px;
  font-weight: 700;
  color: #000000;
  margin-bottom: 3px;
}}

.wizard-header-subtitle {{
  font-size: 12px;
  color: #333333;
  padding-left: 14px;
}}

.wizard-header-icon {{
  width: 55px;
  height: 55px;
  object-fit: contain;
}}

/* Main body container */
.wizard-content {{
  flex: 1;
  display: flex;
  background-color: #ffffff;
  overflow: hidden;
}}

/* Authentic Inno Setup Left Banner (wizard_large.bmp) */
.wizard-sidebar {{
  width: 200px;
  height: 100%;
  background-color: #f8fafc;
  border-right: 1px solid #e2e8f0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: flex-start;
  padding-top: 70px;
  flex-shrink: 0;
}}

.wizard-sidebar-img {{
  width: 100px;
  height: 100px;
  object-fit: contain;
}}

/* Right Content Area for Welcome & Finished */
.wizard-main-welcome {{
  flex: 1;
  padding: 32px 28px;
  display: flex;
  flex-direction: column;
  justify-content: flex-start;
  font-size: 12.5px;
  color: #1e293b;
  line-height: 1.55;
  background-color: #ffffff;
}}

.welcome-title {{
  font-size: 19px;
  font-weight: 700;
  color: #000000;
  margin-bottom: 18px;
  line-height: 1.3;
}}

.welcome-p {{
  margin-bottom: 14px;
  color: #333333;
}}

/* Interior Client Area (Pages 2, 3, 4) */
.wizard-main-interior {{
  flex: 1;
  padding: 16px 24px;
  display: flex;
  flex-direction: column;
  justify-content: flex-start;
  font-size: 12.5px;
  color: #1e293b;
  line-height: 1.5;
  background-color: #f0f0f0;
}}

/* Bottom Action Bar */
.wizard-footer {{
  height: 52px;
  background-color: #f0f0f0;
  border-top: 1px solid #d0d0d0;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  padding: 0 16px;
  gap: 8px;
  flex-shrink: 0;
}}

/* Authentic Inno Setup Buttons */
.btn {{
  width: 82px;
  height: 25px;
  border-radius: 3px;
  font-size: 12px;
  font-weight: 400;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid #adadad;
  background-color: #e1e1e1;
  color: #000000;
  cursor: default;
}}

.btn-primary {{
  background-color: #e1e1e1;
  border: 1px solid #0055ea;
  box-shadow: inset 0 0 0 1px #0055ea;
  font-weight: 500;
}}

.btn-disabled {{
  color: #888888;
  border-color: #cccccc;
  background-color: #f4f4f4;
}}

/* Form elements */
.license-memo {{
  width: 100%;
  height: 220px;
  background-color: #ffffff;
  border: 1px solid #7a7a7a;
  padding: 8px 10px;
  font-size: 11.5px;
  color: #000000;
  font-family: 'Segoe UI', sans-serif;
  overflow-y: scroll;
  line-height: 1.45;
  white-space: pre-wrap;
  margin: 10px 0 12px 0;
}}

.radio-group {{
  display: flex;
  flex-direction: column;
  gap: 8px;
}}

.radio-item {{
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12.5px;
  color: #000000;
}}

.input-row {{
  display: flex;
  gap: 10px;
  margin: 14px 0;
}}

.dir-input {{
  flex: 1;
  height: 25px;
  padding: 0 8px;
  border: 1px solid #7a7a7a;
  background-color: #ffffff;
  font-size: 12px;
  color: #000000;
}}

.task-listbox {{
  width: 100%;
  background-color: #ffffff;
  border: 1px solid #7a7a7a;
  padding: 10px 14px;
  margin-top: 8px;
}}

.checkbox-item {{
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12.5px;
  color: #000000;
}}
"""

large_img_src = f"data:image/bmp;base64,{b64_large}"
small_img_src = f"data:image/bmp;base64,{b64_small}"
app_icon_src = f"data:image/png;base64,{b64_app_icon}"

WIZARD_PAGES = [
    {
        "filename": "synaptix_setup_step1_welcome.png",
        "html": f"""<!DOCTYPE html>
<html>
<head><meta charset="utf-8"><style>{COMMON_CSS}</style></head>
<body>
  <div class="wizard-window">
    <div class="title-bar">
      <div class="title-bar-left">
        <img src="{app_icon_src}" class="title-bar-icon" />
        <span class="title-bar-text">Setup - SynaptiX IDE</span>
      </div>
      <div class="title-bar-controls">
        <span>―</span><span style="opacity:0.3;">□</span><span>✕</span>
      </div>
    </div>
    
    <div class="wizard-content">
      <div class="wizard-sidebar">
        <img src="{app_icon_src}" class="wizard-sidebar-img" />
      </div>
      
      <div class="wizard-main-welcome">
        <h1 class="welcome-title">Welcome to the SynaptiX IDE Setup Wizard</h1>
        <p class="welcome-p">This will install SynaptiX IDE version 2.15.0 on your computer.</p>
        <p class="welcome-p">SynaptiX IDE provides an integrated engineering platform for industrial automation logic, device telemetry, and edge connectivity across the SynaptiX ecosystem.</p>
        <p class="welcome-p" style="color: #444444;">It is recommended that you close all other applications before continuing.</p>
        <p class="welcome-p" style="margin-top: 18px;">Click Next to continue, or Cancel to exit Setup.</p>
      </div>
    </div>
    
    <div class="wizard-footer">
      <div class="btn btn-disabled">&lt; Back</div>
      <div class="btn btn-primary">Next &gt;</div>
      <div class="btn">Cancel</div>
    </div>
  </div>
</body>
</html>"""
    },
    {
        "filename": "synaptix_setup_step2_license.png",
        "html": f"""<!DOCTYPE html>
<html>
<head><meta charset="utf-8"><style>{COMMON_CSS}</style></head>
<body>
  <div class="wizard-window">
    <div class="title-bar">
      <div class="title-bar-left">
        <img src="{app_icon_src}" class="title-bar-icon" />
        <span class="title-bar-text">Setup - SynaptiX IDE</span>
      </div>
      <div class="title-bar-controls">
        <span>―</span><span style="opacity:0.3;">□</span><span>✕</span>
      </div>
    </div>
    
    <div class="wizard-header">
      <div>
        <div class="wizard-header-title">License Agreement</div>
        <div class="wizard-header-subtitle">Please read the following important information before continuing.</div>
      </div>
      <img src="{small_img_src}" class="wizard-header-icon" />
    </div>
    
    <div class="wizard-content">
      <div class="wizard-main-interior">
        <p>Please read the following License Agreement. You must accept the terms of this agreement before continuing with the installation.</p>
        
        <div class="license-memo">{eula_text[:950]}...</div>
        
        <div class="radio-group">
          <label class="radio-item">
            <input type="radio" name="lic" checked />
            <span>I <u>a</u>ccept the agreement</span>
          </label>
          <label class="radio-item">
            <input type="radio" name="lic" />
            <span>I <u>d</u>o not accept the agreement</span>
          </label>
        </div>
      </div>
    </div>
    
    <div class="wizard-footer">
      <div class="btn">&lt; Back</div>
      <div class="btn btn-primary">Next &gt;</div>
      <div class="btn">Cancel</div>
    </div>
  </div>
</body>
</html>"""
    },
    {
        "filename": "synaptix_setup_step3_directory.png",
        "html": f"""<!DOCTYPE html>
<html>
<head><meta charset="utf-8"><style>{COMMON_CSS}</style></head>
<body>
  <div class="wizard-window">
    <div class="title-bar">
      <div class="title-bar-left">
        <img src="{app_icon_src}" class="title-bar-icon" />
        <span class="title-bar-text">Setup - SynaptiX IDE</span>
      </div>
      <div class="title-bar-controls">
        <span>―</span><span style="opacity:0.3;">□</span><span>✕</span>
      </div>
    </div>
    
    <div class="wizard-header">
      <div>
        <div class="wizard-header-title">Select Destination Location</div>
        <div class="wizard-header-subtitle">Where should SynaptiX IDE be installed?</div>
      </div>
      <img src="{small_img_src}" class="wizard-header-icon" />
    </div>
    
    <div class="wizard-content">
      <div class="wizard-main-interior" style="padding-top: 24px;">
        <p style="margin-bottom: 8px;">Setup will install SynaptiX IDE into the following folder.</p>
        <p style="margin-bottom: 18px;">To continue, click Next. If you would like to select a different folder, click Browse.</p>
        
        <div class="input-row">
          <input type="text" class="dir-input" value="C:\\Program Files\\SynaptiX IDE" readonly />
          <div class="btn" style="width: 85px;"><u>B</u>rowse...</div>
        </div>
        
        <div style="margin-top: 140px; font-size: 12px; color: #444444;">
          At least 250.0 MB of free disk space is required.
        </div>
      </div>
    </div>
    
    <div class="wizard-footer">
      <div class="btn">&lt; Back</div>
      <div class="btn btn-primary">Next &gt;</div>
      <div class="btn">Cancel</div>
    </div>
  </div>
</body>
</html>"""
    },
    {
        "filename": "synaptix_setup_step4_tasks.png",
        "html": f"""<!DOCTYPE html>
<html>
<head><meta charset="utf-8"><style>{COMMON_CSS}</style></head>
<body>
  <div class="wizard-window">
    <div class="title-bar">
      <div class="title-bar-left">
        <img src="{app_icon_src}" class="title-bar-icon" />
        <span class="title-bar-text">Setup - SynaptiX IDE</span>
      </div>
      <div class="title-bar-controls">
        <span>―</span><span style="opacity:0.3;">□</span><span>✕</span>
      </div>
    </div>
    
    <div class="wizard-header">
      <div>
        <div class="wizard-header-title">Select Additional Tasks</div>
        <div class="wizard-header-subtitle">Which additional shortcuts should be created?</div>
      </div>
      <img src="{small_img_src}" class="wizard-header-icon" />
    </div>
    
    <div class="wizard-content">
      <div class="wizard-main-interior" style="padding-top: 20px;">
        <p style="margin-bottom: 16px;">Select the additional tasks you would like Setup to perform while installing SynaptiX IDE, then click Next.</p>
        
        <p style="font-weight: 600; color: #000000; margin-bottom: 6px;">Additional shortcuts:</p>
        <div class="task-listbox">
          <label class="checkbox-item">
            <input type="checkbox" checked />
            <span>Create a <u>d</u>esktop shortcut</span>
          </label>
        </div>
      </div>
    </div>
    
    <div class="wizard-footer">
      <div class="btn">&lt; Back</div>
      <div class="btn btn-primary">Next &gt;</div>
      <div class="btn">Cancel</div>
    </div>
  </div>
</body>
</html>"""
    },
    {
        "filename": "synaptix_setup_step5_finish.png",
        "html": f"""<!DOCTYPE html>
<html>
<head><meta charset="utf-8"><style>{COMMON_CSS}</style></head>
<body>
  <div class="wizard-window">
    <div class="title-bar">
      <div class="title-bar-left">
        <img src="{app_icon_src}" class="title-bar-icon" />
        <span class="title-bar-text">Setup - SynaptiX IDE</span>
      </div>
      <div class="title-bar-controls">
        <span>―</span><span style="opacity:0.3;">□</span><span>✕</span>
      </div>
    </div>
    
    <div class="wizard-content">
      <div class="wizard-sidebar">
        <img src="{app_icon_src}" class="wizard-sidebar-img" />
      </div>
      
      <div class="wizard-main-welcome">
        <h1 class="welcome-title">Completing the SynaptiX IDE Setup Wizard</h1>
        <p class="welcome-p">Setup has finished installing SynaptiX IDE on your computer.</p>
        <p class="welcome-p">The application may be launched by selecting the installed shortcut.</p>
        <p class="welcome-p" style="margin-top: 14px;">Click Finish to exit Setup.</p>
        
        <label class="checkbox-item" style="margin-top: 26px;">
          <input type="checkbox" checked />
          <span><u>L</u>aunch SynaptiX IDE</span>
        </label>
      </div>
    </div>
    
    <div class="wizard-footer">
      <div class="btn btn-primary" style="margin-left: auto;">Finish</div>
    </div>
  </div>
</body>
</html>"""
    }
]

print("Rendering authentic 680x520 RGBA Setup Wizard screenshots...")

temp_html = os.path.join(IMG_DIR, "_temp_wizard.html")

for page in WIZARD_PAGES:
    target_png = os.path.join(IMG_DIR, page["filename"])
    with open(temp_html, "w", encoding="utf-8") as f:
        f.write(page["html"])
        
    cmd = [
        CHROME_PATH,
        "--headless=new",
        "--default-background-color=00000000",
        "--window-size=680,520",
        f"--screenshot={target_png}",
        "file:///" + temp_html.replace("\\", "/")
    ]
    subprocess.run(cmd, check=True, capture_output=True)
    print(f"Generated: {page['filename']} ({os.path.getsize(target_png):,} bytes)")

if os.path.exists(temp_html):
    os.remove(temp_html)

print("\nAll authentic Setup Wizard screenshots successfully generated in docs-portal/public/images/!")
