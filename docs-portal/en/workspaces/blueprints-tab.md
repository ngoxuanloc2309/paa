# 5. Blueprints Library (`Ctrl+5`)

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-WS-005</span>
  <span><strong>Workspace:</strong> Tab 5 &bull; Shortcut <code>Ctrl+5</code></span>
  <span><strong>Scope:</strong> Pre-Validated Industrial Automation &amp; IIoT Telemetry Patterns, Instant Canvas Instantiation &amp; Customization</span>
  <span><strong>Standard:</strong> IEC 61131-3 &bull; IEC/IEEE 82079-1 &bull; ANSI/ISA-101</span>
</div>

---

## 1. Purpose & Operational Role

The **Blueprints Library** (`Ctrl+5`) is a curated engineering catalog of **27 pre-validated industrial automation, instrumentation, fieldbus, and IIoT telemetry architectures** built directly into **SynaptiX IDE**. Rather than assembling common control circuits from scratch—such as cross-midnight shift lighting, 4–20 mA linear transmitter scaling, E-Stop safety interlocks, or RS-485 energy meter-to-MQTT pipelines—engineers can instantiate production-grade reference networks onto the [Logic Graph Canvas](/workspaces/logic-graph-tab) with a single click.

In industrial systems integration, the **Blueprints Library** accelerates project delivery across three dimensions:

1. **Standardized Reference Architectures:** Providing peer-reviewed topologies that already incorporate proper edge detection, dwell anti-chatter filtering (`ForMs`), safety `GUARD` gates, retentive memory selection (`VREG_RETAIN`), and IEEE-754 `REAL` precision.
2. **Automatic Tag & Grid Provisioning:** Binding canonical physical and virtual tags in the [Tag Catalog](/workspaces/tag-catalog-tab), laying out blocks along clean orthogonal grid columns (`X = 80, 340, 600, 860`), and routing all **Value** and **Event** connectors automatically.
3. **Interactive Learning & Rapid Simulation (`F5`):** Enabling commissioning engineers to load any industrial pattern, press `F5` to observe animated cycle-by-cycle signal propagation in the [Simulator](/tools/simulator-and-live-debug), and tailor setpoints or I/O channels to their exact cabinet wiring.

---

## 2. Visual Layout & Controls Reference

![SynaptiX Industrial Blueprints Library](/images/synaptix_blueprints.png)

The **Blueprints Library** presents a clean, neutral-slate card gallery organized by engineering domain filters and instant keyword search:

### 2.1. Gallery Controls & Blueprint Card Anatomy

| UI Element | Location | Function & Operational Behavior |
| :--- | :--- | :--- |
| **Domain Category Filter Bar** | Top Filter Strip | Filters the 27 blueprints across `All` or any of the **7 specialized industrial engineering categories**. |
| **Keyword Search Input** | Top Toolbar Right | Searches blueprint titles, engineering descriptions, block types (`TON`, `SCALE`, `MQTT_PUB`, `MB_READ`), and bound tag names. |
| **Domain Classification Badge** | Card Top-Left | Displays the functional discipline of the pattern (for example, `Analog & Instrumentation` or `IIoT Cloud & MQTT`). |
| **Title & Operational Subtitle** | Card Header | Summarizes the target industrial machine or telemetry use case and its primary trigger/actuation mechanism. |
| **Engineering Specification Summary** | Card Body | Explains the physical sensor behavior, dwell/hysteresis logic, interlock polarity, and runtime execution characteristics. |
| **`Tags Used` Inset Manifest** | Card Footer Inset | Lists every `DI`, `DO`, `AI`, `VFLAG`, `VREG`, `VREG_RETAIN`, or `COUNTER` tag that the blueprint binds upon instantiation. |
| **`Apply Blueprint` Action Button** | Card Bottom-Right | Instantiates the complete block network, configures tag aliases/types, wires all pins, and switches focus to the [Logic Graph Canvas](/workspaces/logic-graph-tab) (`Ctrl+2`). |

### 2.2. Complete Catalog of the 7 Engineering Domains (27 Patterns)

| Domain Category | Included Pre-Validated Blueprints | Key Blocks Demonstrated |
| :--- | :--- | :--- |
| **1. Discrete Logic & Safety Interlocks** | &bull; Andon Station Call Alert (`15 s` dwell)<br/>&bull; Retentive Production Line Counter<br/>&bull; Material Call Pushbutton Latch Toggle<br/>&bull; High-Level Automatic Sump Pump<br/>&bull; Inverted Safety Door Interlock<br/>&bull; Main Power Cutoff Interlock<br/>&bull; Machine Status Tower Light<br/>&bull; Dual-Condition E-Stop Safety Circuit | `INPUT`, `TRIGGER` (`ON_RISE`, `ForMs`), `GUARD` (normal & inverted), `ACTION` (`SET_TAG`, `TOGGLE_TAG`, `INC_COUNTER`) |
| **2. Analog & Instrumentation** | &bull; Thermal Overheat Cutoff Protection<br/>&bull; Pneumatic / Hydraulic Pressure Monitor<br/>&bull; Safe Operating Pressure Window (`BETWEEN`)<br/>&bull; `0–10 V` Pressure Transmitter Scaling (`Bar`)<br/>&bull; `4–20 mA` RTD Temperature Scaling (`°C`)<br/>&bull; Ultrasonic Tank Level Calibration (`Meters`)<br/>&bull; IEEE-754 Float32 `75.5 °C` Precision Alarm<br/>&bull; Emergency Overpressure Alarm Broadcast | `INPUT` (`AI`), `SCALE` (2-point linear calibration), `COMPARATOR` (`>`, `<`, `BETWEEN`, `REAL`), `ACTION` (`SEND_ALARM`) |
| **3. Cyclic Timing & IEC 61131-3 Timers** | &bull; Periodic Bearing Lubrication (`INTERVAL`)<br/>&bull; `TON` On-Delay Booster Pump Startup<br/>&bull; `TOF` Off-Delay Spindle Cooling Fan Overrun<br/>&bull; `TP` Monostable Pneumatic Part Ejector Pulse | `TRIGGER` (`INTERVAL`), `TON`, `TOF`, `TP`, `ACTION` |
| **4. Batch Production Counters** | &bull; `CTU` Carton Boxing Batch Counter<br/>&bull; `CTD` Magazine Feeder Blank Countdown | `CTU`, `CTD`, `COUNTER`, `VREG_RETAIN` |
| **5. Real-Time Clock (RTC) Scheduling** | &bull; Morning Shift `07:00` Plant Illumination<br/>&bull; Cross-Midnight `18:00–06:00` Perimeter Security | `TRIGGER` (`TIME_WINDOW` intra-day & overnight wrap-around), `ACTION` |
| **6. IIoT Cloud & MQTT** | &bull; Event-Driven `MQTT_PUB` JSON Telemetry<br/>&bull; Inbound `MQTT_SUB` Cloud Command Dispatcher | `MQTT_PUB` (JSON macro tokens), `MQTT_SUB` (JSON key extraction), `GUARD` |
| **7. RS-485 Fieldbus & Smart Logistics** | &bull; Cyclic `MB_READ` RS-485 Sensor Poller<br/>&bull; `MB_WRITE` Inverter / VFD Speed Setpoint<br/>&bull; 18-Bin Sort-to-Light `DIRECT_WRITE` Pulse Bus<br/>&bull; 3-Phase Energy Meter (`MFM383A → MQTT`) Pipeline | `MB_READ`, `MB_WRITE`, `DIRECT_WRITE`, `SCALE`, `MQTT_PUB` |

---

## 3. Step-by-Step Operational Procedures

### Procedure 3.1: Instantiating & Simulating an Industrial Blueprint

1. Press `Ctrl+5` to open the **Blueprints Library**.
2. Select a domain filter pill (for example, **Analog & Instrumentation**) or type `4-20mA` into the search bar.
3. Review the **`Tags Used`** manifest on the desired card to see which physical inputs (`AI0`) and virtual registers (`VREG0`) the template utilizes.
4. Click **Apply Blueprint**.
5. **SynaptiX IDE** populates the schematic on clean grid coordinates (`X = 80, 340, 600, 860`) and transitions automatically to the [Logic Graph Canvas](/workspaces/logic-graph-tab) (`Ctrl+2`).
6. Press `F5` to launch the **Offline Simulator**, adjust the simulated `AI0` slider or toggle `DI0`, and watch real-time values propagate across the pre-wired blocks.

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>STEP 1</span>
      </div>
      <span class="synx-node-status">SELECT</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Select Pattern</span>
        <span class="synx-node-alias">Standard Library</span>
        <span class="synx-node-val">Ctrl+5 Blueprints</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Apply</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>STEP 2</span>
      </div>
      <span class="synx-node-status">TAGS</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Auto-Provision</span>
        <span class="synx-node-alias">Tag Registration</span>
        <span class="synx-node-val">Aliases & Types</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Spawn</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 L13.5,4 L13.5,9.5 Q13.5,13.5 8,15 Q2.5,13.5 2.5,9.5 L2.5,4 Z" /></svg>
        <span>STEP 3</span>
      </div>
      <span class="synx-node-status">CANVAS</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Layout Blocks</span>
        <span class="synx-node-alias">Snapped Grid</span>
        <span class="synx-node-val">Ctrl+2 Logic Graph</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Tune</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,13 L5,13 L5,9.5 L8,9.5 L8,6 L11,6 L11,3 L14,3" /></svg>
        <span>STEP 4</span>
      </div>
      <span class="synx-node-status">PARAMS</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Tailor Settings</span>
        <span class="synx-node-alias">Field Specifics</span>
        <span class="synx-node-val">Right Inspector</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Run</span>
  </div>

  <div class="synx-canvas-node is-step is-done">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>STEP 5</span>
      </div>
      <span class="synx-node-status is-active">● OPERATE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Verify & Run</span>
        <span class="synx-node-alias">Simulate & Deploy</span>
        <span class="synx-node-val">F7 / F5 / Ctrl+F5</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

### Procedure 3.2: Customizing a Blueprint for Field Cabinet Wiring

1. After applying a blueprint onto the [Logic Graph Canvas](/workspaces/logic-graph-tab), click any **INPUT**, **GUARD**, or **ACTION** block whose terminal assignment differs from your physical wiring diagram (for example, changing `DO0` to `DO4`).
2. Update the bound tag in the **Right Property Inspector**, and adjust process setpoints (such as changing a `TON` preset from `5000 ms` to `10000 ms` or updating an `MB_READ` Slave ID).
3. Switch to the [Tag Catalog](/workspaces/tag-catalog-tab) (`Ctrl+3`) if you wish to localize the auto-populated tag **Aliases** to match plant-specific P&ID tag numbers.
4. Press `F7` to compile and verify zero static warnings in the [Rule Table](/workspaces/rule-table-tab) (`Ctrl+4`).

---

## 4. Engineering Best Practices & Edge Cases

::: warning CANVAS REPLACEMENT ON BLUEPRINT APPLICATION
Clicking **Apply Blueprint** initializes a clean, self-contained schematic for that reference architecture on the active [Logic Graph Canvas](/workspaces/logic-graph-tab). If your current project already contains unsaved custom logic networks, save your `.synx` project first (`Ctrl+S`)—or open a second exploration session to copy (`Ctrl+C`) and paste (`Ctrl+V`) specific blueprint branches into an existing multi-network project.
:::

- **Hardware Capability Alignment After Applying IIoT / Fieldbus Blueprints:** Blueprints in Categories 6 and 7 (`MQTT_PUB`, `MQTT_SUB`, `MB_READ`, `MB_WRITE`) require target hardware equipped with Wi-Fi/Ethernet or RS-485 transceivers. Ensure your target preset in the [Device Workspace](/workspaces/device-tab) (`Ctrl+1`) supports those interfaces before final deployment (`Ctrl+F5`).
- **Cross-Midnight `TIME_WINDOW` Validation:** When adapting the *Cross-Midnight `18:00–06:00` Perimeter Security* blueprint, always synchronize the controller's hardware clock and local UTC offset via **Device → Sync Clock** (see [Device Workspace](/workspaces/device-tab)) so overnight window transitions align accurately with plant shift changes.

---

## Related Documentation

- [2. Logic Graph Canvas (`Ctrl+2`)](/workspaces/logic-graph-tab)
- [3. Tag Catalog (`Ctrl+3`)](/workspaces/tag-catalog-tab)
- [Blocks Catalog & Pin Legend](/blocks/overview)
- [Integration Application Notes](/tools/application-notes)
- [Simulator & Online Live Debug](/tools/simulator-and-live-debug)
