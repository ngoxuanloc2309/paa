#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Generates high-resolution, pixel-perfect screenshots of the SynaptiX IDE Windows Setup Wizard
for documentation and user guides.
"""

import os
import subprocess
from PIL import Image

BASE_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DOCS_PORTAL = os.path.join(BASE_DIR, "docs-portal")
IMG_DIR = os.path.join(DOCS_PORTAL, "public", "images")
APP_ICON = os.path.join(BASE_DIR, "SimplePLC.Studio", "Assets", "app.png")
CHROME_PATH = r"C:\Program Files\Google\Chrome\Application\chrome.exe"

os.makedirs(IMG_DIR, exist_ok=True)

# Common CSS for Windows 11 Inno Setup Modern Wizard
COMMON_CSS = f"""
* {{
  box-sizing: border-box;
  margin: 0;
  padding: 0;
  font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, sans-serif;
  user-select: none;
}}

body {{
  background-color: transparent;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 660px;
  height: 480px;
  overflow: hidden;
}}

.wizard-window {{
  width: 620px;
  height: 440px;
  background-color: #ffffff;
  border-radius: 8px;
  box-shadow: 0 16px 36px rgba(0, 0, 0, 0.22), 0 2px 8px rgba(0, 0, 0, 0.12);
  border: 1px solid #cbd5e1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}}

.title-bar {{
  height: 34px;
  background-color: #f1f5f9;
  border-bottom: 1px solid #e2e8f0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 12px;
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
  font-weight: 500;
  color: #1e293b;
}}

.title-bar-controls {{
  display: flex;
  gap: 12px;
  color: #64748b;
  font-size: 12px;
}}

/* Header Banner for interior pages */
.wizard-header {{
  height: 64px;
  background-color: #ffffff;
  border-bottom: 1px solid #e2e8f0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 20px;
}}

.wizard-header-title {{
  font-size: 13px;
  font-weight: 700;
  color: #0f172a;
  margin-bottom: 2px;
}}

.wizard-header-subtitle {{
  font-size: 11.5px;
  color: #64748b;
}}

.wizard-header-icon {{
  width: 36px;
  height: 36px;
}}

/* Main body container */
.wizard-content {{
  flex: 1;
  display: flex;
  background-color: #f8fafc;
  overflow: hidden;
}}

/* Left Sidebar for Welcome & Finish pages */
.wizard-sidebar {{
  width: 175px;
  background: linear-gradient(180deg, #0f172a 0%, #00384d 60%, #006487 100%);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 20px;
  border-right: 1px solid #e2e8f0;
}}

.sidebar-logo {{
  width: 72px;
  height: 72px;
  margin-bottom: 16px;
  filter: drop-shadow(0 4px 8px rgba(0,0,0,0.3));
}}

.sidebar-brand {{
  color: #ffffff;
  font-size: 14px;
  font-weight: 700;
  letter-spacing: 0.5px;
  text-align: center;
}}

.sidebar-sub {{
  color: #94a3b8;
  font-size: 10px;
  margin-top: 4px;
  letter-spacing: 0.5px;
  text-transform: uppercase;
  text-align: center;
}}

/* Main interior container */
.wizard-main-full {{
  flex: 1;
  padding: 20px 24px;
  display: flex;
  flex-direction: column;
  justify-content: flex-start;
  font-size: 12px;
  color: #334155;
  line-height: 1.5;
}}

/* Footer */
.wizard-footer {{
  height: 52px;
  background-color: #f1f5f9;
  border-top: 1px solid #e2e8f0;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  padding: 0 18px;
  gap: 10px;
}}

.btn {{
  height: 28px;
  min-width: 80px;
  padding: 0 14px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid #cbd5e1;
  background-color: #ffffff;
  color: #1e293b;
}}

.btn-primary {{
  background-color: #006487;
  color: #ffffff;
  border-color: #00506d;
}}

.btn-disabled {{
  background-color: #f1f5f9;
  color: #94a3b8;
  border-color: #e2e8f0;
}}

/* Form elements */
.input-group {{
  margin: 12px 0;
  display: flex;
  gap: 8px;
}}

.text-input {{
  flex: 1;
  height: 30px;
  padding: 0 10px;
  border: 1px solid #cbd5e1;
  border-radius: 4px;
  font-size: 12px;
  color: #0f172a;
  background-color: #ffffff;
}}

.license-box {{
  width: 100%;
  height: 180px;
  background-color: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 4px;
  padding: 10px;
  font-size: 11px;
  color: #475569;
  font-family: Consolas, monospace;
  overflow-y: auto;
  line-height: 1.45;
  margin: 10px 0;
}}

.checkbox-item {{
  display: flex;
  align-items: flex-start;
  gap: 8px;
  margin-bottom: 10px;
  font-size: 12px;
  color: #1e293b;
}}

.radio-group {{
  margin-top: 8px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}}

.radio-item {{
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: #1e293b;
}}

.bullet-list {{
  margin-left: 18px;
  margin-top: 6px;
  line-height: 1.6;
}}
"""

icon_uri = f"file:///{APP_ICON.replace('\\', '/')}"

WIZARD_PAGES = [
    {
        "filename": "synaptix_setup_step1_welcome.png",
        "html": f"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"><style>{COMMON_CSS}</style></head>
        <body>
          <div class="wizard-window">
            <div class="title-bar">
              <div class="title-bar-left">
                <img src="{icon_uri}" class="title-bar-icon" />
                <span class="title-bar-text">Setup - SynaptiX IDE</span>
              </div>
              <div class="title-bar-controls">
                <span>―</span>
                <span style="opacity:0.4;">□</span>
                <span>✕</span>
              </div>
            </div>
            
            <div class="wizard-content">
              <div class="wizard-sidebar">
                <img src="{icon_uri}" class="sidebar-logo" />
                <div class="sidebar-brand">SynaptiX IDE</div>
                <div class="sidebar-sub">Automation Suite</div>
              </div>
              
              <div class="wizard-main-full" style="padding-top: 30px;">
                <h2 style="font-size: 18px; font-weight: 700; color: #0f172a; margin-bottom: 14px; line-height: 1.3;">
                  Welcome to the SynaptiX IDE Setup Wizard
                </h2>
                <p style="margin-bottom: 12px; color: #334155;">
                  This will install <strong>SynaptiX IDE version 2.15.0</strong> on your computer.
                </p>
                <p style="margin-bottom: 14px; color: #475569;">
                  SynaptiX IDE provides an integrated industrial engineering platform for logic configuration, device telemetry, and edge connectivity across the SynaptiX ecosystem.
                </p>
                <p style="margin-bottom: 20px; color: #64748b; font-size: 11.5px;">
                  It is recommended that you close all other applications before continuing.
                </p>
                <p style="color: #334155;">
                  Click <strong>Next</strong> to continue, or <strong>Cancel</strong> to exit Setup.
                </p>
              </div>
            </div>
            
            <div class="wizard-footer">
              <button class="btn btn-disabled">&lt; Back</button>
              <button class="btn btn-primary">Next &gt;</button>
              <button class="btn">Cancel</button>
            </div>
          </div>
        </body>
        </html>
        """
    },
    {
        "filename": "synaptix_setup_step2_license.png",
        "html": f"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"><style>{COMMON_CSS}</style></head>
        <body>
          <div class="wizard-window">
            <div class="title-bar">
              <div class="title-bar-left">
                <img src="{icon_uri}" class="title-bar-icon" />
                <span class="title-bar-text">Setup - SynaptiX IDE</span>
              </div>
              <div class="title-bar-controls">
                <span>―</span>
                <span style="opacity:0.4;">□</span>
                <span>✕</span>
              </div>
            </div>
            
            <div class="wizard-header">
              <div>
                <div class="wizard-header-title">License Agreement</div>
                <div class="wizard-header-subtitle">Please read the following important information before continuing.</div>
              </div>
              <img src="{icon_uri}" class="wizard-header-icon" />
            </div>
            
            <div class="wizard-content">
              <div class="wizard-main-full">
                <p>Please read the following License Agreement. You must accept the terms of this agreement before continuing with the installation.</p>
                
                <div class="license-box">
===================================================================<br>
SYNAPTIX AUTOMATION END-USER LICENSE AGREEMENT (EULA)<br>
Version 2.15.0 - Industrial Software License<br>
===================================================================<br><br>
1. GRANT OF LICENSE<br>
SynaptiX Corporation grants you the non-exclusive right to install and use SynaptiX IDE on your engineering workstations for the purpose of configuring and operating SynaptiX IIoT controllers and devices.<br><br>
2. INDUSTRIAL SAFETY & COMPLIANCE<br>
The software adheres to IEC 61131-3 and IEC 61508-3 SIL2 engineering doctrines. The licensee agrees to conduct standard pre-commissioning safety checks before deploying logic programs to physical machinery.<br><br>
3. PROPRIETARY RIGHTS & TELEMETRY<br>
All intellectual property rights in the software remain with SynaptiX Corporation.
                </div>
                
                <div class="radio-group">
                  <label class="radio-item">
                    <input type="radio" name="license" checked />
                    <strong>I accept the agreement</strong>
                  </label>
                  <label class="radio-item">
                    <input type="radio" name="license" />
                    <span>I do not accept the agreement</span>
                  </label>
                </div>
              </div>
            </div>
            
            <div class="wizard-footer">
              <button class="btn">&lt; Back</button>
              <button class="btn btn-primary">Next &gt;</button>
              <button class="btn">Cancel</button>
            </div>
          </div>
        </body>
        </html>
        """
    },
    {
        "filename": "synaptix_setup_step3_directory.png",
        "html": f"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"><style>{COMMON_CSS}</style></head>
        <body>
          <div class="wizard-window">
            <div class="title-bar">
              <div class="title-bar-left">
                <img src="{icon_uri}" class="title-bar-icon" />
                <span class="title-bar-text">Setup - SynaptiX IDE</span>
              </div>
              <div class="title-bar-controls">
                <span>―</span>
                <span style="opacity:0.4;">□</span>
                <span>✕</span>
              </div>
            </div>
            
            <div class="wizard-header">
              <div>
                <div class="wizard-header-title">Select Destination Location</div>
                <div class="wizard-header-subtitle">Where should SynaptiX IDE be installed?</div>
              </div>
              <img src="{icon_uri}" class="wizard-header-icon" />
            </div>
            
            <div class="wizard-content">
              <div class="wizard-main-full" style="padding-top: 24px;">
                <p style="margin-bottom: 8px;">Setup will install SynaptiX IDE into the following folder.</p>
                <p style="margin-bottom: 14px; color: #64748b;">To continue, click Next. If you would like to select a different folder, click Browse.</p>
                
                <div class="input-group">
                  <input type="text" class="text-input" value="C:\\Program Files\\Synaptix Automation\\SynaptiX IDE" readonly />
                  <button class="btn" style="min-width: 75px;">Browse...</button>
                </div>
                
                <div style="margin-top: 30px; font-size: 11.5px; color: #64748b; border-top: 1px solid #e2e8f0; padding-top: 14px;">
                  At least 250.0 MB of free disk space is required.
                </div>
              </div>
            </div>
            
            <div class="wizard-footer">
              <button class="btn">&lt; Back</button>
              <button class="btn btn-primary">Next &gt;</button>
              <button class="btn">Cancel</button>
            </div>
          </div>
        </body>
        </html>
        """
    },
    {
        "filename": "synaptix_setup_step4_tasks.png",
        "html": f"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"><style>{COMMON_CSS}</style></head>
        <body>
          <div class="wizard-window">
            <div class="title-bar">
              <div class="title-bar-left">
                <img src="{icon_uri}" class="title-bar-icon" />
                <span class="title-bar-text">Setup - SynaptiX IDE</span>
              </div>
              <div class="title-bar-controls">
                <span>―</span>
                <span style="opacity:0.4;">□</span>
                <span>✕</span>
              </div>
            </div>
            
            <div class="wizard-header">
              <div>
                <div class="wizard-header-title">Select Additional Tasks</div>
                <div class="wizard-header-subtitle">Which additional tasks should be performed?</div>
              </div>
              <img src="{icon_uri}" class="wizard-header-icon" />
            </div>
            
            <div class="wizard-content">
              <div class="wizard-main-full" style="padding-top: 20px;">
                <p style="margin-bottom: 14px;">Select the additional tasks you would like Setup to perform while installing SynaptiX IDE, then click Next.</p>
                
                <div style="font-weight: 600; color: #0f172a; margin-bottom: 8px;">Additional shortcuts:</div>
                <label class="checkbox-item">
                  <input type="checkbox" checked />
                  <span>Create a <strong>Desktop</strong> shortcut</span>
                </label>
                <label class="checkbox-item">
                  <input type="checkbox" checked />
                  <span>Create a <strong>Start Menu</strong> program group shortcut</span>
                </label>
                
                <div style="font-weight: 600; color: #0f172a; margin-top: 14px; margin-bottom: 8px;">System integration:</div>
                <label class="checkbox-item">
                  <input type="checkbox" checked />
                  <span>Associate SynaptiX Project files (<strong>.splc</strong>) with SynaptiX IDE</span>
                </label>
                <label class="checkbox-item">
                  <input type="checkbox" checked />
                  <span>Bundle <strong>McuEmulator</strong> (Hardware-in-the-Loop virtual simulator)</span>
                </label>
              </div>
            </div>
            
            <div class="wizard-footer">
              <button class="btn">&lt; Back</button>
              <button class="btn btn-primary">Next &gt;</button>
              <button class="btn">Cancel</button>
            </div>
          </div>
        </body>
        </html>
        """
    },
    {
        "filename": "synaptix_setup_step5_finish.png",
        "html": f"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"><style>{COMMON_CSS}</style></head>
        <body>
          <div class="wizard-window">
            <div class="title-bar">
              <div class="title-bar-left">
                <img src="{icon_uri}" class="title-bar-icon" />
                <span class="title-bar-text">Setup - SynaptiX IDE</span>
              </div>
              <div class="title-bar-controls">
                <span>―</span>
                <span style="opacity:0.4;">□</span>
                <span>✕</span>
              </div>
            </div>
            
            <div class="wizard-content">
              <div class="wizard-sidebar">
                <img src="{icon_uri}" class="sidebar-logo" />
                <div class="sidebar-brand">SynaptiX IDE</div>
                <div class="sidebar-sub">Installation Complete</div>
              </div>
              
              <div class="wizard-main-full" style="padding-top: 30px;">
                <h2 style="font-size: 18px; font-weight: 700; color: #0f172a; margin-bottom: 14px; line-height: 1.3;">
                  Completing the SynaptiX IDE Setup Wizard
                </h2>
                <p style="margin-bottom: 14px; color: #334155;">
                  Setup has finished installing <strong>SynaptiX IDE</strong> on your computer. The application may be launched by selecting the installed shortcuts.
                </p>
                <p style="margin-bottom: 24px; color: #475569;">
                  Click <strong>Finish</strong> to exit Setup.
                </p>
                
                <label class="checkbox-item" style="padding-top: 10px; border-top: 1px solid #e2e8f0;">
                  <input type="checkbox" checked />
                  <span style="font-weight: 600; color: #006487;">Launch SynaptiX IDE now</span>
                </label>
              </div>
            </div>
            
            <div class="wizard-footer">
              <button class="btn btn-disabled">&lt; Back</button>
              <button class="btn btn-primary" style="min-width: 90px;">Finish</button>
              <button class="btn btn-disabled">Cancel</button>
            </div>
          </div>
        </body>
        </html>
        """
    }
]

print("Rendering Setup Wizard screenshots via Chrome headless...")

temp_html = os.path.join(IMG_DIR, "_temp_wizard.html")

for page in WIZARD_PAGES:
    target_png = os.path.join(IMG_DIR, page["filename"])
    with open(temp_html, "w", encoding="utf-8") as f:
        f.write(page["html"])
        
    cmd = [
        CHROME_PATH,
        "--headless=new",
        "--disable-gpu",
        "--force-device-scale-factor=2",
        f"--window-size=660,480",
        f"--screenshot={target_png}",
        f"file:///{temp_html.replace('\\', '/')}"
    ]
    subprocess.run(cmd, check=True, capture_output=True)
    print(f"Generated: {page['filename']} ({os.path.getsize(target_png):,} bytes)")

if os.path.exists(temp_html):
    os.remove(temp_html)

print("\nAll Setup Wizard screenshots successfully created in docs-portal/public/images/!")
