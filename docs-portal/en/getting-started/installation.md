# System Requirements & Installation

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-GS-001</span>
  <span><strong>Chapter:</strong> 02. Installation &amp; Quickstart</span>
  <span><strong>Target OS:</strong> Windows 10 / Windows 11 (x64)</span>
  <span><strong>Standard:</strong> IEC/IEEE 82079-1 &bull; ANSI/ISA-101</span>
</div>

This guide covers host workstation hardware/software prerequisites, USB CDC serial driver verification, enterprise firewall port rules, initial setup of **SynaptiX IDE**, and keeping your engineering workstation current with the built-in **Software Update Manager**.

---

## Host Workstation System Requirements

**SynaptiX IDE** is a hardware-accelerated Windows desktop engineering suite built on WPF and .NET 8. Verify that your engineering laptop or control-room workstation meets the specifications below before installation:

| Component | Minimum Specification | Recommended Commissioning Specification |
| :--- | :--- | :--- |
| **Operating System** | Windows 10 (64-bit, Version 1909 or newer) | Windows 11 Pro / Enterprise (64-bit, 22H2+) |
| **Runtime Framework** | .NET 8.0 Desktop Runtime (`win-x64`) *(bundled in standard setup)* | Latest .NET 8.0 LTS Servicing Release |
| **Processor (CPU)** | Dual-Core x64 @ 2.0 GHz | Quad-Core Intel Core i5 / AMD Ryzen 5 or higher |
| **System Memory (RAM)** | 4 GB RAM | 8 GB – 16 GB RAM (for multi-trace Live Watch & AI Copilot) |
| **Disk Space** | 250 MB free SSD/HDD space | 1 GB free NVMe SSD space (for project archives & CSV logs) |
| **Display & Ergonomics** | `1366 x 768` @ 100% DPI | **`1920 x 1080` (Full HD)** or `2560 x 1440` IPS anti-glare panel (ANSI/ISA-101 8–12 hr shift ergonomics) |
| **Local Interfaces** | 1x USB 2.0/3.0 Type-A or Type-C port | USB port + 100/1000 Mbps Ethernet / Wi-Fi NIC |

::: tip Self-Contained Win-x64 Installer Package
Official **SynaptiX IDE** `win-x64 setup` release packages include all required dependencies so field commissioning laptops inside air-gapped industrial facilities can install and run without internet access.
:::

---

## Installing & Launching SynaptiX IDE

1. **Obtain the Official Setup Package:** Download the latest signed `SynaptiX-IDE-Setup-win-x64.exe` installer from your organization's engineering portal or the official SynaptiX Releases channel.
2. **Execute Installer:** Double-click the installer package. Accept the standard Windows User Account Control (UAC) prompt if installing machine-wide, and choose the destination directory.
3. **Default Project Workspace Folder:** On first launch, **SynaptiX IDE** automatically initializes your default user project directory at:

```text
%USERPROFILE%\Documents\SynaptiX\Projects
```
4. **Select Interface Language:** Switch seamlessly between **English** and **Tiếng Việt** at any time via **Tools -> Language** on the top menu bar without restarting the IDE or closing open projects.

---

## Transport Drivers & Network Firewall Configuration

**SynaptiX IDE** communicates with **SynaptiX Edge Controllers** and **DTU Gateways** over two primary commissioning transports: **USB CDC Virtual Serial** (direct cable) and **Modbus TCP** (Ethernet / Wi-Fi LAN).

### 1. USB CDC Virtual COM Port Verification

Modern Windows 10 and Windows 11 installations include the native Microsoft USB Communications Device Class (`usbser.sys`) driver out of the box — no third-party driver installer is required on standard OS builds.

To verify physical enumeration when plugging a powered **SynaptiX Device** into your PC via USB:
1. Press `Win + X` and open **Device Manager**.
2. Expand the **Ports (COM & LPT)** category.
3. Confirm that a **USB Serial Device (`COMx`)** entry (for example, `COM3` or `COM7`) appears without a yellow warning triangle.
4. In **SynaptiX IDE**, ensure the top-right transport switch is set to **`USB CDC`**, click the **Refresh Ports** icon next to the dropdown, and select the matching `COMx` port.

::: warning Troubleshooting Missing COM Ports on USB Connection
- **Charge-Only USB Cables:** Many consumer USB-C / Micro-USB cables omit the `D+` / `D-` data pair. Always use a shielded industrial data-rated USB cable.
- **Port Contention:** Serial COM ports on Windows are exclusive-access resources. Close any external serial terminal or Modbus scanner holding `COMx` before clicking **Connect (`F4`)** in **SynaptiX IDE**.
:::

### 2. Enterprise Firewall & Industrial VLAN Port Matrix

When commissioning devices over **Modbus TCP** or testing end-to-end cloud telemetry from plant networks, ensure local Windows Defender Firewall and industrial VLAN ACLs permit the following ports:

| Protocol / Service | Transport & Port | Traffic Direction | Operational Purpose |
| :--- | :---: | :--- | :--- |
| **Modbus TCP Commissioning & SCADA** | `TCP 502` *(configurable)* | Workstation / SCADA -> SynaptiX Device | Remote **SynaptiX IDE** Connect (`F4`), Atomic Deploy (`Ctrl+F5`), Live Watch (`Ctrl+7`), and SCADA Slave polling. |
| **MQTT Broker (Standard)** | `TCP 1883` | SynaptiX Device -> Broker | Unencrypted LAN MQTT telemetry (`MQTT_PUB`) and command subscription (`MQTT_SUB`). |
| **MQTT over TLS (Encrypted)** | `TCP 8883` | SynaptiX Device -> Cloud Broker | Production TLS-encrypted IIoT telemetry and mutual certificate authentication. |
| **HTTP / HTTPS Webhooks** | `TCP 80` / `TCP 443` | SynaptiX Device -> REST Server | Outbound `HTTP_CLIENT` REST API posts and **SynaptiX IDE** GitHub release update checks (`TCP 443`). |
| **Raw Socket / Transparent DTU** | Custom `TCP Port` | SynaptiX Device -> Host Server | User-defined TCP socket streaming (`TCP_CLIENT` and [DTU Gateway Studio](/tools/dtu-gateway-studio)). |

---

## Built-In Software Update Manager

**SynaptiX IDE** includes a non-intrusive **Software Update Manager** designed specifically for industrial engineering workflows — it never forces automatic restarts during active commissioning or live monitoring sessions.

![SynaptiX IDE Built-In Software Update Dialog](/images/synaptix_update_dialog.png)

### How Update Detection & Installation Works

1. **Passive Status Bar Notification:** When workstation internet access (`HTTPS / TCP 443`) is available, **SynaptiX IDE** checks for newer official releases in the background. If a newer version is published, a subtle slate-gray **Update Available** badge appears on the far right of the bottom status bar without interrupting your canvas work.
2. **Manual Check Anytime:** You can trigger an immediate check via **Help -> Check for Updates...** on the main menu bar (or click the status bar update badge).
3. **Inspect Release Specifications & Changelog:** The **Update Dialog** displays:
   - Your **Current Installed Version** alongside the **Target Release Version** (for example, `v2.2.1 -> v2.3.0`).
   - Exact **Package Size** (in `MB`) and target architecture chip (`win-x64 setup`).
   - Structured **Release Notes & Technical Changelog** detailing new function blocks, protocol enhancements, and bug fixes.
4. **One-Click Download & Upgrade (or Browser Fallback):**
   - Click **Update Now** to stream the installer with a real-time percentage progress bar, or click **Later** to defer until your current commissioning session is saved and disconnected.
   - Behind strict corporate proxies or GitHub API rate limits, the updater automatically falls back to web redirect resolution and provides an **Open in Browser** fallback button if direct download is restricted by IT policy.

::: danger Save Project & Disconnect Before Applying Updates
Before clicking **Update Now** to launch the new installer, always save your active project (`Ctrl+S`) and disconnect any active live hardware session (`F4`) so no uncommitted edits are interrupted when the application restarts.
:::

---

## Next Steps

- [IDE Interface Anatomy (UI Tour)](/getting-started/ui-tour) — Master the 6 ergonomic zones, connection bar, and keyboard shortcuts of **SynaptiX IDE**.
- [10-Minute First Project](/getting-started/quickstart-first-project) — Build, simulate, compile, deploy, and monitor a complete over-temperature safety interlock from scratch.
