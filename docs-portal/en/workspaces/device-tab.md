# 1. Device Workspace (`Ctrl+1`)

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-WS-001</span>
  <span><strong>Workspace:</strong> Tab 1 &bull; Shortcut <code>Ctrl+1</code></span>
  <span><strong>Scope:</strong> Hardware Identity, Transport Handshake, RTC Sync &amp; Peripheral Bus Setup</span>
  <span><strong>Standard:</strong> IEC/IEEE 82079-1 &bull; ANSI/ISA-101</span>
</div>

---

## 1. Purpose & Operational Role

The **Device Workspace** (`Ctrl+1`) is the hardware commissioning and transport management center of **SynaptiX IDE**. Before authoring deterministic control rules or commissioning live field signals, engineers use this workspace to establish physical or virtual communication links, inspect target hardware capabilities, synchronize the controller's Real-Time Clock (RTC), and provision onboard peripheral subsystems.

From an operational lifecycle perspective, the **Device Workspace** governs four foundational commissioning duties:

1. **Transport Selection & Session Lifecycle (`F4`):** Switching seamlessly between direct local serial commissioning (**USB CDC**), networked industrial Ethernet (**Modbus TCP**), or the built-in cycle-accurate virtual target (`SIMULATOR (VIRTUAL)`), complete with automatic link-drop recovery.
2. **Hardware Identity & Capability Discovery:** Querying the connected **SynaptiX Edge Controller** for its device class, physical I/O variant, firmware revision, wire protocol profile, and resource limits—automatically aligning the project's [Tag Catalog](/workspaces/tag-catalog-tab) and [Logic Graph](/workspaces/logic-graph-tab) block palette to the physical hardware.
3. **Real-Time Clock (RTC) & Timezone Alignment:** Reading the onboard battery-backed RTC IC (or software `SysTick` clock), diagnosing backup battery health, and committing UTC epoch timestamps alongside local timezone offsets without disturbing existing schedules during passive connection handshakes.
4. **Integrated Peripheral & DTU Provisioning:** Hosting the 3-column **Device Configuration Studio** for configuring RS-485 fieldbus parameters, Wi-Fi station credentials, MQTT/TLS cloud uplinks, and cyclic telemetry framing in a unified view.

::: tip OFFLINE HARDWARE PRESET EMULATION
When working offline without physical hardware attached, you can select a target **Device Preset** (`Remote I/O`, `IIoT Gateway`, `Datalogger`, or `Edge Controller`) from the IDE toolbar or connect to `SIMULATOR (VIRTUAL)`. **SynaptiX IDE** immediately applies the corresponding I/O capacities and function block capability gates so your offline engineering matches the field hardware 1:1.
:::

---

## 2. Visual Layout & Controls Reference

The Device commissioning workflow spans the persistent **Top Quick-Connect Bar**, the **Device Identity & Hardware Profile** inspector, the **RTC Clock Synchronization** dialog, and the **3-Column Device Configuration Studio** inside Tab 1.

### 2.1. Device Identity & Hardware Profile Inspector

![SynaptiX Device Identity and Hardware Profile](/images/synaptix_device_info.png)

When a session handshake succeeds via `F4` (or when opening **Device → Device Info**), **SynaptiX IDE** interrogates the target **SynaptiX Device** and presents its physical contract across four structured telemetry groups:

| Inspector Field / Control | Category | Description & Operational Behavior |
| :--- | :--- | :--- |
| **Connection Mode Switch** (`USB CDC` / `Modbus TCP`) | Top Quick-Connect Bar | Segmented transport selector. Choosing `USB CDC` exposes the COM port dropdown and refresh action; choosing `Modbus TCP` exposes the `Host : Port` endpoint inputs (default `127.0.0.1 : 502`). |
| **Target Port / Endpoint Selector** | Top Quick-Connect Bar | Lists enumerated Windows USB CDC serial ports plus `SIMULATOR (VIRTUAL)` in serial mode, or IPv4 address and TCP port fields in Ethernet mode. |
| **Connect / Disconnect (`F4`)** | Top Quick-Connect Bar | Initiates or terminates the transport session. Displays deterministic connection states: `Disconnected`, `Connecting`, `Connected`, `Reconnecting` (auto-retry on transient cable drop), and `Restarting`. |
| **Device Model & Class Badge** | Identity Header | Displays the hardware family classification (`Remote I/O`, `Gateway`, `Datalogger`, or `Controller`) and physical I/O topology (for example, `8DI / 8DO / 4AI` or `16DI / 16DO`). |
| **Hardware & Firmware Revision** | Identity Metrics | Reports etched PCB revision (`HW v...`), active **SynaptiX Runtime** firmware build (`FW v...`), Wire Profile (`V2`), and deterministic Rule Engine format (`v2.0`). |
| **Hardware Resource Matrix** | Capacity Contract | Displays exact per-bank tag allocations (`DI`, `DO`, `AI`, `VFLAG`, `VREG`, `VREG_RETAIN`, `COUNTER`) and maximum deterministic ECA rule capacity (`MaxRules = 100`). |
| **Transport & Slave Diagnostics** | Link Telemetry | Reports active port identifier, UART framing or TCP socket status, Modbus Unit/Slave ID, and live round-trip communication health. |

### 2.2. Real-Time Clock (RTC) Synchronization Dialog

![SynaptiX Real-Time Clock Synchronization](/images/synaptix_rtc_sync.png)

Accessible via **Device → Sync Clock**, the RTC Synchronization tool ensures deterministic time-of-day scheduling (`TIME_WINDOW` triggers, shift counters, and ISO-8601 MQTT timestamps) across distributed controllers:

| Control / Indicator | Function & Engineering Behavior |
| :--- | :--- |
| **Clock Source Badge** (`Hardware RTC IC` vs `Software Clock`) | Indicates whether the connected **SynaptiX Edge Controller** maintains time via a dedicated battery-backed crystal RTC IC or a volatile MCU `SysTick` counter. |
| **Battery / Power Loss Warning** | Highlights if the backup coin-cell voltage is low or if power was interrupted on a non-battery-backed variant, alerting the engineer before relying on time-window schedules. |
| **Controller Current Time Readout** | Displays the live local timestamp and configured UTC offset currently running inside the **SynaptiX Runtime** without overwriting device settings upon connect. |
| **Timezone Selector** | Supports `[Default] Current PC Time` (inherits host workstation UTC offset) or explicit selection of any standard global timezone from `UTC-12:00` to `UTC+14:00`. |
| **Manual Date/Time Override** | Optional `YYYY-MM-DD HH:mm:ss` input field with Bad-Data Defense validation for testing shift transitions, midnight rollovers, or isolated air-gapped plant clocks. |
| **Live Commit Preview (1 Hz)** | Updates every second to show the exact target local time and UTC offset that will be written to hardware when **Sync Clock** is clicked. |

### 2.3. Integrated 3-Column Device Configuration Studio

Inside the main body of Tab 1 (`Ctrl+1`), the **Device Workspace** embeds a 3-column peripheral configuration studio:

| Workspace Region | Shortcuts & Sub-Sections | Operational Purpose |
| :--- | :--- | :--- |
| **Left Subsystem Navigation** | `Ctrl+Shift+B` (`RS-485 Modbus`)<br/>`Ctrl+Shift+W` (`Wi-Fi Network`)<br/>`Ctrl+Shift+M` (`Cloud Uplink`)<br/>`Timing & Payload` &bull; `Quick Wizard` | Displays live readiness badges for each onboard subsystem, launches the 1-2-3 guided **Quick Wizard**, and provides the **Save Configuration** commit trigger. |
| **Center Configuration Form** | Context-sensitive parameter cards | Provides structured, type-safe editors for UART baud rate/parity (`9600`–`115200`), Wi-Fi SSID/WPA2 credentials, MQTT broker/TLS certificates, and JSON telemetry cadence. |
| **Right Context Inspector** | Live Intelligence & Readiness Panel | Displays field wiring notes, active Modbus slave register summaries, payload size estimates, and pre-flight validation warnings. |
| **Device Menu Lifecycle Actions** | `F6` (Upload from Device)<br/>`Ctrl+F5` (Atomic Deploy)<br/>`Reboot Device` &bull; `Factory Reset` | Executes full configuration readback from hardware, two-phase atomic deployment, warm controller restart, or confirmation-guarded factory reset. |

---

## 3. Step-by-Step Operational Procedures

### Procedure 3.1: Connecting to a Physical Controller or Virtual Simulator

1. Press `Ctrl+1` to activate the **Device Workspace**.
2. In the **Top Quick-Connect Bar**, choose the transport medium:
   - **Local USB Commissioning:** Select `USB CDC`, click the **Refresh Ports** icon if the cable was just attached, and choose the assigned `COMx` port (or select `SIMULATOR (VIRTUAL)` for hardware-free testing).
   - **Ethernet / Gateway Commissioning:** Select `Modbus TCP` and enter the target IPv4 address and TCP port (standard port `502`).
3. Press `F4` (or click **Connect**).
4. Verify that the status pill transitions from `Connecting` to `Connected` (green indicator) and inspect the **Device Identity** summary to confirm firmware version and I/O channel counts.
5. Observe that **SynaptiX IDE** automatically synchronizes the [Tag Catalog](/workspaces/tag-catalog-tab) hardware profile to match the connected device's physical `DI`, `DO`, and `AI` banks.

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>STEP 1</span>
      </div>
      <span class="synx-node-status">MEDIUM</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Select Medium</span>
        <span class="synx-node-alias">Port Configuration</span>
        <span class="synx-node-val">USB CDC / TCP :502</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Connect</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>STEP 2</span>
      </div>
      <span class="synx-node-status">HANDSHAKE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Initiate Link</span>
        <span class="synx-node-alias">Shortcut Key F4</span>
        <span class="synx-node-val">Connected Badge</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Read FW</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 L13.5,4 L13.5,9.5 Q13.5,13.5 8,15 Q2.5,13.5 2.5,9.5 L2.5,4 Z" /></svg>
        <span>STEP 3</span>
      </div>
      <span class="synx-node-status">IDENTIFY</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Identify Target</span>
        <span class="synx-node-alias">Hardware Profile</span>
        <span class="synx-node-val">FW Version &amp; I/O Map</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Sync</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,13 L5,13 L5,9.5 L8,9.5 L8,6 L11,6 L11,3 L14,3" /></svg>
        <span>STEP 4</span>
      </div>
      <span class="synx-node-status">SYNC</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Auto-Align</span>
        <span class="synx-node-alias">Catalog Alignment</span>
        <span class="synx-node-val">Tag Catalog &amp; Toolbox</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Ready</span>
  </div>

  <div class="synx-canvas-node is-step is-done">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>STEP 5</span>
      </div>
      <span class="synx-node-status is-active">● READY</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Session Ready</span>
        <span class="synx-node-alias">Live Diagnostics</span>
        <span class="synx-node-val">Live Watch / Deploy</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

### Procedure 3.2: Synchronizing the Controller Real-Time Clock (RTC)

1. With an active `Connected` session, open **Device → Sync Clock** from the top command bar.
2. Inspect the **Clock Source Badge** and verify no backup battery fault is active.
3. Choose either `[Default] Current PC Time` to mirror your engineering laptop's clock and timezone, or select the plant's fixed UTC offset from the **Timezone Selector**.
4. *(Optional)* Enable **Manual Date/Time Override** and enter a specific `YYYY-MM-DD HH:mm:ss` timestamp if validating scheduled `TIME_WINDOW` rules.
5. Confirm the **Live Commit Preview** matches the expected plant time, then click **Sync Clock** to write the UTC epoch and timezone offset atomically to the **SynaptiX Edge Controller**.

### Procedure 3.3: Configuring Onboard RS-485, Wi-Fi, and Cloud Subsystems

1. In the left sidebar of the **Device Workspace** (`Ctrl+1`), select **RS-485 Modbus** (`Ctrl+Shift+B`) to set transceiver baud rate, data bits, parity, stop bits, and local SCADA Slave ID. For full register mapping details, see [RS-485 Config & SCADA Slave Map](/tools/rs485-and-scada-slave).
2. Select **Wi-Fi Network** (`Ctrl+Shift+W`) and **Cloud Uplink** (`Ctrl+Shift+M`) on network-capable models to configure station credentials, MQTT broker host, TLS security, and topic namespaces. Refer to [MQTT TLS & Wi-Fi Provisioning](/tools/mqtt-and-wifi-config) and [DTU Gateway Studio](/tools/dtu-gateway-studio).
3. Click **Save Configuration** in the left panel, then execute **Atomic Deploy** (`Ctrl+F5`) or switch to the [Atomic Deploy Workspace](/workspaces/deploy-tab) to commit all peripheral and logic changes together.

---

## 4. Engineering Best Practices & Edge Cases

::: warning PASSIVE CONNECT TIMEZONE PRESERVATION
Connecting to a **SynaptiX Edge Controller** via `F4` performs a **non-destructive read** of the controller's RTC and hardware profile. **SynaptiX IDE** intentionally never overwrites the device clock or timezone automatically on connect—preventing an engineer connecting remotely from a different timezone from accidentally shifting active plant `TIME_WINDOW` schedules. Clock updates occur strictly when you explicitly execute **Sync Clock**.
:::

- **Automatic Cable-Drop Recovery (`Reconnecting` State):** Industrial environments frequently experience transient USB EMI bursts or cabinet power cycling during commissioning. When an active link drops unexpectedly, **SynaptiX IDE** enters the `Reconnecting` state—executing fast active retries followed by non-blocking background polling—and seamlessly restores telemetry once the **SynaptiX Device** comes back online.
- **Hardware Capability Gating Consistency:** If you connect to a compact **Remote I/O** unit that lacks onboard Wi-Fi/Ethernet hardware, network function blocks (`MQTT_PUB`, `MQTT_SUB`, `HTTP_CLIENT`, `TCP_CLIENT`) are automatically gated in the [Logic Graph Canvas](/workspaces/logic-graph-tab) so you cannot accidentally author logic that the target silicon cannot execute.
- **Uploading Existing Field Configurations (`F6`):** Before modifying a controller already operating on the plant floor, always press `F6` (**Upload from Device**) first to pull its live rules, function blocks, RS-485 parameters, and MQTT topics into **SynaptiX IDE** for inspection and backup.

---

## Related Documentation

- [Ecosystem & Device Presets](/concepts/ecosystem-overview)
- [2. Logic Graph Canvas (`Ctrl+2`)](/workspaces/logic-graph-tab)
- [3. Tag Catalog (`Ctrl+3`)](/workspaces/tag-catalog-tab)
- [RS-485 Config & SCADA Slave Map](/tools/rs485-and-scada-slave)
- [MQTT TLS & Wi-Fi Provisioning](/tools/mqtt-and-wifi-config)
- [DTU Gateway Studio](/tools/dtu-gateway-studio)
