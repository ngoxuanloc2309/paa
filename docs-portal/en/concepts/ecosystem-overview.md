# Ecosystem Overview & Device Presets

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-CON-001</span>
  <span><strong>Chapter:</strong> 01. Introduction &amp; Core Concepts</span>
  <span><strong>Applies To:</strong> SynaptiX IDE v2.x &bull; Contract Profile V2</span>
  <span><strong>Standard:</strong> IEC/IEEE 82079-1 &bull; ANSI/ISA-101</span>
</div>

**SynaptiX IDE** is a software-first industrial engineering environment built on .NET 8 for designing, simulating, commissioning, and monitoring deterministic edge control and IIoT telemetry applications across the **SynaptiX Edge Automation & IIoT Ecosystem**.

Rather than forcing automation engineers to juggle separate tools for discrete/analog control, RS-485 Modbus RTU polling, and cloud MQTT/HTTP payload scripting, **SynaptiX IDE** unifies visual logic authoring, strict physical memory contracts, cycle-accurate offline simulation, atomic two-phase deployment, and 20 FPS live telemetry diagnostics inside a single desktop workbench.

---

## System Topology & Architectural Role

Within an industrial plant or distributed infrastructure deployment, a **SynaptiX Edge Device** operates at the boundary between physical field instrumentation (sensors, relays, VFDs, power meters) and supervisory IT/OT platforms (**SynaptiX IDE**, local SCADA masters, MQTT brokers, and REST webhooks).

<div class="synx-arch-grid">
  <!-- Card 1: Field -->
  <div class="synx-arch-card">
    <div class="synx-arch-header">
      <div class="synx-arch-icon">1</div>
      <div class="synx-arch-title-wrap">
        <h4 class="synx-arch-title">Field Instrumentation Layer</h4>
        <span class="synx-arch-subtitle">Sensors & Actuators</span>
      </div>
    </div>
    <div class="synx-arch-items">
      <div class="synx-arch-item">
        <strong>Direct Physical I/O (Discrete & Analog)</strong>
        <span>DI0..7 (Optocoupler), DO0..7 (Transistor), AI0..3 (4-20mA / 0-10V)</span>
        <span class="synx-arch-badge">IEC 61131-2</span>
      </div>
      <div class="synx-arch-item">
        <strong>RS-485 Fieldbus Slaves</strong>
        <span>VFD drives, digital power meters, environmental transmitters</span>
        <span class="synx-arch-badge">Modbus RTU • 9600..115200 bps</span>
      </div>
    </div>
  </div>

  <!-- Card 2: Edge -->
  <div class="synx-arch-card">
    <div class="synx-arch-header">
      <div class="synx-arch-icon">2</div>
      <div class="synx-arch-title-wrap">
        <h4 class="synx-arch-title">SynaptiX Edge Runtime</h4>
        <span class="synx-arch-subtitle">Deterministic Core Engine</span>
      </div>
    </div>
    <div class="synx-arch-items">
      <div class="synx-arch-item">
        <strong>Unified Tag Memory</strong>
        <span>Up to 128 global slots: DI, DO, AI, VREG, VREG_RETAIN, COUNTER</span>
        <span class="synx-arch-badge">Zero-Allocation</span>
      </div>
      <div class="synx-arch-item">
        <strong>Deterministic Scan Engine</strong>
        <span>Rigid 10ms cycle: Input sampling → FBD logic execution → Output commit</span>
        <span class="synx-arch-badge">Hard Real-time</span>
      </div>
      <div class="synx-arch-item">
        <strong>Async Fieldbus & IIoT Workers</strong>
        <span>Modbus RTU Master Poller, MQTT Client, HTTP Webhooks, TCP Client</span>
        <span class="synx-arch-badge">Non-blocking DMA</span>
      </div>
    </div>
  </div>

  <!-- Card 3: Supervisory -->
  <div class="synx-arch-card">
    <div class="synx-arch-header">
      <div class="synx-arch-icon">3</div>
      <div class="synx-arch-title-wrap">
        <h4 class="synx-arch-title">Engineering, SCADA & Cloud</h4>
        <span class="synx-arch-subtitle">Supervisory IT / OT Layer</span>
      </div>
    </div>
    <div class="synx-arch-items">
      <div class="synx-arch-item">
        <strong>SynaptiX IDE Engineering Workbench</strong>
        <span>Two-phase atomic commit deploy, 20 FPS live telemetry diagnostics</span>
        <span class="synx-arch-badge">USB CDC / Modbus TCP :502</span>
      </div>
      <div class="synx-arch-item">
        <strong>Local SCADA / HMI Stations</strong>
        <span>Ignition, WinCC, Kepware polling the Fixed SCADA Slave Register Map</span>
        <span class="synx-arch-badge">FC01..04 / FC05..16</span>
      </div>
      <div class="synx-arch-item">
        <strong>Enterprise IIoT Cloud Platforms</strong>
        <span>Publishing periodic telemetry JSON uplinks to AWS IoT, EMQX, HiveMQ</span>
        <span class="synx-arch-badge">MQTT TLS 1.2/1.3</span>
      </div>
    </div>
  </div>
</div>

### Software-First Engineering Workflow

Every project in **SynaptiX IDE** is governed by an explicit **Product Resource Profile**. Selecting or detecting a device profile automatically configures:

1. **Tag Memory Allocation:** Generates exact hardware and virtual tag slots (`DI`, `DO`, `AI`, `VFLAG`, `VREG`, `VREG_RETAIN`, `COUNTER`) in the [Tag Catalog](/workspaces/tag-catalog-tab).
2. **Capability-Aware Block Toolbox:** Dynamically enables or disables fieldbus and cloud blocks (`MB_READ`, `MB_WRITE`, `MQTT_PUB`, `MQTT_SUB`, `HTTP_CLIENT`, `TCP_CLIENT`) on the [Logic Graph Canvas](/workspaces/logic-graph-tab) — or switches the workbench into [DTU Gateway Studio](/tools/dtu-gateway-studio) when targeting a dedicated Data Transfer Unit.
3. **Pre-Flight Compile & Deploy Gates:** Enforces maximum rule counts and hardware capability flags during compilation (`F7`) and [Atomic Deploy](/workspaces/deploy-tab) (`Ctrl+F5`), preventing incompatible configurations from ever reaching field hardware.

---

## Built-In Device Presets Matrix

**SynaptiX IDE** ships with **7 standardized commercial device presets** covering distributed remote I/O, standalone edge control, sensor datalogging, protocol gateways, and transparent RS-485 DTUs.

### Resource Capacity Comparison

| Preset Name | Device Family | Local Physical I/O (`DI / DO / AI`) | Virtual & Retentive Slots (`VFLAG / VREG / RETAIN / CNT`) | Total Tags (Ceiling: 128) | Max Rules (Ceiling: 100) |
| :--- | :--- | :---: | :---: | :---: | :---: |
| **SynaptiX Remote I/O** | Remote I/O (Default) | `8 / 8 / 4` | `32 / 32 / 32 / 8` | `124` | `100` |
| **SynaptiX Compact I/O** | Remote I/O (Compact) | `4 / 4 / 0` | `16 / 16 / 16 / 4` | `60` | `50` |
| **SynaptiX Relay Unit** | Remote I/O (Discrete) | `8 / 8 / 0` | `32 / 32 / 32 / 8` | `120` | `100` |
| **SynaptiX Data Logger** | Data Logger | `4 / 2 / 4` | `16 / 32 / 32 / 4` | `94` | `50` |
| **SynaptiX Gateway** | Protocol Gateway | `2 / 2 / 0` | `32 / 32 / 32 / 4` | `104` | `50` |
| **SynaptiX Controller** | Edge Controller | `8 / 8 / 4` | `32 / 32 / 32 / 8` | `124` | `100` |
| **SynaptiX DTU** | RS-485 Network DTU | `0 / 0 / 0` | `32 / 32 / 32 / 4` | `100` | `50` |

### Enabled Hardware & Subsystem Capabilities

Each preset declares a bitmask of hardware subsystem flags that **SynaptiX IDE** validates at design time and pre-deployment:

| Preset Name | Base Logic & Retentive | Hardware RTC | SD Datalog | Modbus RTU Master | Modbus TCP | Ethernet / Wi-Fi | MQTT Pub/Sub | HTTP Client | Transparent TCP Stream |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **SynaptiX Remote I/O** | Yes | — | — | Yes | — | — | — | — | — |
| **SynaptiX Compact I/O** | Yes | — | — | Yes | — | — | — | — | — |
| **SynaptiX Relay Unit** | Yes | — | — | Yes | — | — | — | — | — |
| **SynaptiX Data Logger** | Yes | Yes | Yes | — | — | — | — | — | — |
| **SynaptiX Gateway** | Yes | — | — | Yes | Yes | Ethernet | Yes | Yes | Yes |
| **SynaptiX Controller** | Yes | Yes | Yes | Yes | — | Ethernet + Wi-Fi | Yes | Yes | Yes |
| **SynaptiX DTU** | DTU Engine | — | — | Yes | Yes | Ethernet + Wi-Fi | Yes | Yes | Yes |

::: info Selecting Between SynaptiX Gateway, SynaptiX Controller, and SynaptiX DTU
- Choose **`SynaptiX Controller`** when your application requires full local I/O (`8 DI / 8 DO / 4 AI`), RTC calendar scheduling, SD card logging, RS-485 master polling, and simultaneous Wi-Fi/Ethernet IIoT cloud telemetry.
- Choose **`SynaptiX Gateway`** when bridging RS-485 Modbus RTU field buses to Ethernet (`Modbus TCP`, `MQTT`, `HTTP`) with minimal local discrete interlocks (`2 DI / 2 DO`).
- Choose **`SynaptiX DTU`** when commissioning a pure RS-485-to-Cloud Data Transfer Unit (`0` local physical I/O). Selecting this preset automatically activates the specialized [DTU Gateway Studio](/tools/dtu-gateway-studio) interface instead of the node-wiring canvas.
:::

---

## Project Provisioning Modes in New Project Dialog

When creating a project via **File -> New Project** (`Ctrl+N`), **SynaptiX IDE** presents the **New Project Provisioning Dialog** with real-time resource matrix preview on the right-hand specification card.

![SynaptiX IDE New Project Dialog & Hardware Resource Matrix](/images/synaptix_new_project.png)

Engineers can provision a project's target resource contract using three distinct workflows:

### 1. Standard Commercial Preset Selection
Select one of the **7 Built-in Presets** from the **Target Hardware Preset** dropdown. The right-hand **Hardware Specification Preview** immediately updates the exact index spans (for example, `8 (DI0 - DI7)`, `32 (RETAIN0 - RETAIN31)`) and total tag budget (`124 / 128 tags`).

### 2. Detect from Connected Device (Hardware-First Discovery)
If a physical **SynaptiX Edge Device** is already connected via **USB CDC** or **Modbus TCP** (`F4`), click **Detect from Connected Device** inside the New Project dialog:
- **SynaptiX IDE** queries the live device descriptor and runtime resource profile directly from the connected controller.
- Hardware revision, firmware version, exact I/O channel counts, retentive capacity, and maximum rule limits are imported automatically.
- The microcontroller acts as the **Single Source of Truth**, eliminating manual preset mismatch errors during field commissioning.

### Hardware Variant Identification (`DeviceClass` & `DeviceVariant`) & Safety Gate

Inside the microcontroller firmware, every device exposes an immutable binary descriptor pair:
- **`DeviceClass` (Category):** Defines the primary role: Remote I/O (`1`), Datalogger (`2`), Gateway (`3`), Controller (`4`), DTU (`5`).
- **`DeviceVariant` (Hardware Variant):** Identifies the specific model SKU and physical I/O layout:
  - *Remote I/O:* Variant `1` (SYNX-RIO-884: `8DI / 8DO / 4AI`), Variant `2` (SYNX-RIO-440-WF: `4DI / 4DO Relay Wi-Fi`), Variant `3` (SYNX-RIO-440: `4DI / 4DO Compact`), Variant `4` (SYNX-RIO-880: `8DI / 8DO Relay`), Variant `5` (SYNX-RIO-440-ZB: `4DI / 4DO Relay Zigbee`).
  - *Datalogger:* Variant `1` (`8AI` instrumentation — SYNX-DL-8AI).
  - *Gateway:* Variant `1` (RS-485 to Ethernet bridge — SYNX-GW-485-ETH).
  - *Controller:* Variant `1` (Programmable real-time logic controller).
  - *DTU:* Variant `1` (RS-485 to Cellular/Network MQTT/HTTP/TCP bridge).

::: tip Safety Gate Against Incompatible Hardware Deploys
When deploying bytecode to a field panel (`Ctrl+F5`), **SynaptiX IDE** automatically verifies the target `DeviceVariant`. If the active project is configured for a 16 DI/DO variant but you attempt to deploy to a 4 DI/DO controller, the software triggers an `UnsupportedDeviceVariant` safety interlock and blocks the deploy to prevent electrical or logical damage.
:::

::: warning Active Session Required for Auto-Detect
If no active communication session is open when clicking **Detect from Connected Device**, the dialog displays a non-blocking status warning prompting you to connect via the top connection bar (`F4`) first, or continue offline using a standard preset.
:::

### 3. Custom Resource Mode (Parameterized Allocation)
For OEM board variants or tailored memory budgets within the SynaptiX Wire Profile V2 architectural ceiling, custom resource allocation allows fine-grained adjustment of individual resource pools up to hard architectural limits:

| Resource Pool | Tag Prefix | Configurable Range | Architectural Ceiling |
| :--- | :--- | :---: | :---: |
| **Digital Inputs** | `DI` | `0 .. 8` | `8` |
| **Digital Outputs** | `DO` | `0 .. 8` | `8` |
| **Analog Inputs** | `AI` | `0 .. 4` | `4` |
| **Virtual Flags** | `VFLAG` | `0 .. 32` | `32` |
| **Virtual Registers** | `VREG` | `0 .. 32` | `32` |
| **Retentive Registers** | `VREG_RETAIN` | `0 .. 32` | `32` |
| **High-Range Counters** | `COUNTER` | `0 .. 8` | `8` |
| **Compiled Logic Rules** | `Rule #` | `1 .. 100` | `100` |

::: danger Hard Architectural Ceiling Enforcement
The sum of all declared runtime tags across all seven pools can never exceed **128 tags**, and maximum rules can never exceed **100 rules**. Both the domain profile builder and the deployment gate strictly reject any profile that violates these ceilings.
:::

---

## Related Documentation

- [Tag Architecture & Physical Data Types](/concepts/data-model-and-types) — Deep dive into the 7 tag families, `BOOL`/`INT32`/`REAL` memory contracts, and Bad-Data Defense.
- [Deterministic Execution Pipeline](/concepts/execution-pipeline) — How the 4-stage rule engine, stateful function blocks, and async network workers execute each scan.
- [10-Minute First Project](/getting-started/quickstart-first-project) — Step-by-step walkthrough using the `SynaptiX Controller` preset.
