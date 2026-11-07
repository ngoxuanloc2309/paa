# IDE Interface Anatomy (UI Tour)

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-GS-002</span>
  <span><strong>Chapter:</strong> 02. Installation &amp; Quickstart</span>
  <span><strong>Applies To:</strong> SynaptiX IDE v2.x Workbench</span>
  <span><strong>Standard:</strong> ANSI/ISA-101.01 &bull; Microsoft Fluent 2</span>
</div>

**SynaptiX IDE** is engineered strictly around **ANSI/ISA-101 ("The Gray Principle")** ergonomics for 8–12 hour commissioning shifts: 90% of the visual surface uses neutral glare-free slate tones, reserving high-contrast semantic colors exclusively for live operational states — **Emerald Green** (Connected / Active Wire / Run OK), **Amber** (Warning / Forced / Stale Compile), **Crimson Red** (Fault / Blocked Gate), and **Petrol Blue** (Active Selection).

![SynaptiX IDE Main Engineering Workbench Anatomy](/images/synaptix_main_anatomy.png)

---

## Six-Zone Workbench Architecture

The main **SynaptiX IDE** window is partitioned into six deterministic, non-overlapping functional zones so engineers never lose context behind floating modal clutter:

<div class="synx-ide-mockup">
  <div class="synx-ide-topbar">
    <span>Zone 1: Top Command Menu Bar (File • Edit • View • Device • Tools • Help)</span>
    <span style="color: #60a5fa;">[Unified Connection: USB CDC / Modbus TCP • F4]</span>
  </div>
  <div class="synx-ide-navtabs">
    <span class="synx-ide-tab">Ctrl+1 Device</span>
    <span class="synx-ide-tab is-active">Ctrl+2 Logic Graph</span>
    <span class="synx-ide-tab">Ctrl+3 Tag Catalog</span>
    <span class="synx-ide-tab">Ctrl+4 Rule Table</span>
    <span class="synx-ide-tab">Ctrl+5 Blueprints</span>
    <span class="synx-ide-tab">Ctrl+6 Deploy</span>
    <span class="synx-ide-tab">Ctrl+7 Live Watch</span>
  </div>
  <div class="synx-ide-body">
    <div class="synx-ide-panel synx-ide-panel-left">
      <div class="synx-ide-panel-header">Zone 3 • Block Toolbox</div>
      <p style="font-size: 0.76rem; color: var(--vp-c-text-2); margin: 0;">Logic &amp; Timers (TON, TOF, CTU...)<br/>Fieldbus &amp; IIoT (MB_READ, MQTT_PUB...)</p>
      <div style="margin-top: auto; font-size: 0.72rem; color: var(--vp-c-text-3);">Fixed 265 px width</div>
    </div>
    <div class="synx-ide-panel synx-ide-panel-center">
      <div>
        <div class="synx-ide-panel-header">Zone 4 • Infinite Logic Canvas</div>
        <p style="font-size: 0.78rem; color: var(--vp-c-text-2); margin: 0;">Drag-and-drop function blocks, 4-stage deterministic wiring, real-time pin value badges.</p>
      </div>
      <div style="background-color: var(--vp-c-bg); border: 1px dashed var(--vp-c-divider); border-radius: 4px; padding: 8px; font-size: 0.74rem; color: var(--vp-c-text-2);">
        <strong>Embedded Scan Simulator Drawer (Bottom)</strong> — Standalone 10ms hardware scan cycle simulator (F5)
      </div>
    </div>
    <div class="synx-ide-panel synx-ide-panel-right">
      <div class="synx-ide-panel-header">Zone 5 • Property Inspector &amp; AI</div>
      <p style="font-size: 0.76rem; color: var(--vp-c-text-2); margin: 0;">Pin bindings, millisecond delay presets, relational comparator thresholds.</p>
      <div style="margin-top: auto; font-size: 0.72rem; color: var(--vp-c-text-3);">Fixed 320 px + Collapsible AI Copilot</div>
    </div>
  </div>
  <div class="synx-ide-statusbar">
    <span>Zone 6: Scan Diagnostics (Cycle: 10.0ms • Jitter &lt;0.5ms • Mode: RUN)</span>
    <span>CPU: 8% • RAM: 42 KB / 64 KB</span>
  </div>
</div>

---

## Zone 1: Top Command Menu & Unified Connection Bar

Positioned across the top of the window, Zone 1 provides global project/device commands on the standard menu bar (`File`, `Edit`, `View`, `Device`, `Tools`, `Help`) paired with the **Unified Target Connection Strip** on the right:

- **Segmented Transport Switch (`USB CDC` vs. `Modbus TCP`):** Switch effortlessly between local USB serial commissioning and remote network commissioning inside a fixed-width `210 px` selector container that eliminates layout shifts:
  - **`USB CDC` Mode:** Displays detected `COMx` serial ports plus a one-click **Refresh Ports** button.
  - **`Modbus TCP` Mode:** Displays inline **`Host IP : Port`** fields (for example, `192.168.1.50 : 502`).
- **Primary Lifecycle Commands & Hotkeys:**
  - **Connect / Disconnect (`F4`):** Opens or closes an authenticated live communication session with the target **SynaptiX Edge Device**.
  - **Offline Simulation (`F5`):** Launches the cycle-accurate desktop simulator without requiring physical hardware.
  - **Upload from Device (`F6`):** Reads the active runtime configuration back from a connected controller into the IDE.
  - **Compile Logic (`F7`):** Validates all node connections, tag types, and capacity limits, lowering the graph into deterministic rules.
  - **Atomic Deploy (`Ctrl+F5`):** Executes pre-flight safety gates and commits compiled rules to non-volatile device flash.
  - **Online Live Debug (`F8`):** Streams real-time 20 FPS tag values and energized green wire states directly onto the canvas.
- **SynaptiX AI Chatbox (`Ctrl+Space`):** Toggles the resizable right-hand [SynaptiX AI Copilot](/tools/ai-copilot) assistant panel.
- **Instant Bilingual Switcher (`EN` / `VI`):** Accessible under **Tools -> Language** (`English` / `Tiếng Việt`), rebinding every label, tooltip, and validation message instantaneously without restarting.

---

## Zone 2: Primary Workspace Switcher (`Ctrl+1` .. `Ctrl+7`)

Docked on the left side of the navigation strip, seven segmented workspace tabs follow the natural left-to-right engineering lifecycle. Every workspace is reachable with a single `Ctrl + Number` shortcut (supporting both top-row digits and Numpad keys):

| Shortcut | Workspace Tab | Core Engineering Responsibility | Detailed Reference |
| :---: | :--- | :--- | :--- |
| `Ctrl+1` | **1. Device** | Inspect hardware identity, firmware version, resource profile, RS-485 bus parameters, MQTT/Wi-Fi status, and RTC clock sync. | [Device Workspace](/workspaces/device-tab) |
| `Ctrl+2` | **2. Logic Graph** | Author visual control & IIoT logic on the infinite node canvas (or configure DTU polling/uplinks on DTU targets). | [Logic Graph Canvas](/workspaces/logic-graph-tab) |
| `Ctrl+3` | **3. Tag Catalog** | Manage all up to 128 physical/virtual tags, symbolic aliases, `BOOL`/`INT32`/`REAL` types, and CSV import/export. | [Tag Catalog](/workspaces/tag-catalog-tab) |
| `Ctrl+4` | **4. Rule Table** | Audit compiled 4-stage Event-Condition-Action rules in compact tabular form and jump back to source graphs. | [Rule Table](/workspaces/rule-table-tab) |
| `Ctrl+5` | **5. Blueprints** | Instantiate pre-validated industrial automation & IIoT application templates in one click. | [Blueprints Library](/workspaces/blueprints-tab) |
| `Ctrl+6` | **6. Deploy** | Review pre-flight compatibility checks, resource utilization bars, and execute Two-Phase Atomic Flash Commit. | [Atomic Deploy](/workspaces/deploy-tab) |
| `Ctrl+7` | **7. Live Watch** | Monitor real-time 20 FPS tag tables, multi-channel trend charts, force/write values, and record CSV logs. | [Live Watch & Trends](/workspaces/live-watch-tab) |

---

## Zone 3: Left Categorized Block Toolbox & Protocol Configs

When working on the **Logic Graph** (`Ctrl+2`), the `265 px` left panel houses the **Function Block Toolbox**:

1. **Instant Filter Search:** Type any keyword (such as `TON`, `MQTT`, `SCALE`, or `Guard`) into the top search bar to filter blocks in real time.
2. **8 Expandable DIN-Rail Categories:** Organizes all [18 Function Blocks](/blocks/overview) logically:
   - *Process I/O:* [`INPUT`](/blocks/input), [`ACTION`](/blocks/action)
   - *Logic & Interlocks:* [`TRIGGER`](/blocks/trigger), [`GUARD`](/blocks/guard), [`COMPARATOR`](/blocks/comparator)
   - *Timers:* [`TON`](/blocks/ton), [`TOF`](/blocks/tof), [`TP`](/blocks/tp), [`CYCLE_TIMER`](/blocks/cycle-timer)
   - *Counters:* [`CTU`](/blocks/ctu), [`CTD`](/blocks/ctd)
   - *Analog & Actuators:* [`SCALE`](/blocks/scale), [`DIRECT_WRITE`](/blocks/direct-write)
   - *Modbus Fieldbus:* [`MB_READ`](/blocks/mb-read), [`MB_WRITE`](/blocks/mb-write)
   - *MQTT IIoT Telemetry:* [`MQTT_PUB`](/blocks/mqtt-pub), [`MQTT_SUB`](/blocks/mqtt-sub)
   - *Gateway & Egress:* [`HTTP_CLIENT`](/blocks/http-client), [`TCP_CLIENT`](/blocks/tcp-client)
3. **Capability-Aware Guardrails:** If the active project preset does not support a subsystem (for example, `MQTT_PUB` on a non-networked `SynaptiX Compact I/O`), unsupported cards automatically dim with an explanatory hardware-capability tooltip.
4. **Global Quick Protocol Configuration Dialogs:** Configure underlying transport parameters from anywhere in the IDE via dedicated shortcuts under the **Device** menu:
   - `Ctrl+Shift+B` — [RS-485 Modbus Port & SCADA Slave Configuration](/tools/rs485-and-scada-slave)
   - `Ctrl+Shift+M` — [MQTT Broker & TLS Security Configuration](/tools/mqtt-and-wifi-config)
   - `Ctrl+Shift+W` — [Wi-Fi Station Provisioning](/tools/mqtt-and-wifi-config)
   - `Ctrl+Shift+D` — [DTU Gateway Configuration & Packet Sniffer](/tools/dtu-gateway-studio)

---

## Zone 4: Central Infinite Logic Canvas (or DTU Gateway Studio)

Zone 4 is the primary authoring viewport:

- **Precision 20 px Engineering Grid:** Supports smooth pan/zoom, multi-node marquee selection, orthogonal 90-degree industrial step connections with `12 px` invisible hit-testing targets, and CAD-style alignment shortcuts (`Alt + Arrow Keys` to align edges, `Alt+Shift+H/V` to distribute evenly, `Alt+G` to snap/toggle grid).
- **Docked Simulation Drawer (`F5`):** Expands cleanly from the bottom of the canvas during offline testing so you can toggle `DI` switches, sweep `AI` sliders, and inject inbound JSON payloads while watching live wire highlights above.
- **Human Affirmation Gate for AI Proposals:** When [SynaptiX AI Copilot](/tools/ai-copilot) proposes logic nodes, ghosted preview nodes appear with a floating **Accept / Reject** safety banner verifying zero write conflicts before committing changes to your graph.
- **Adaptive DTU Mode:** When the project targets the **`SynaptiX DTU`** preset, Zone 4 automatically replaces the node canvas with [DTU Gateway Studio](/tools/dtu-gateway-studio) for streamlined RS-485 channel polling and cloud uplink mapping.

---

## Zone 5: Right Context-Sensitive Property Inspector

Docked on the right (`320 px` width), the **Property Inspector** dynamically adapts to the currently selected block on the canvas:

- **Type-Safe Parameter Forms:** Displays tag selectors (`[Tag] — Alias`), relational operator pickers, preset timers, Modbus register decoders, and JSON payload template editors with live macro validation.
- **Bad-Data Defense Active Enforcement:** Every constant and setpoint field enforces the [Input State Machine](/concepts/data-model-and-types#visual-truth-input-state-machine-bad-data-defense) — automatically normalizing `"25"` to `"25.0"` on `REAL` inputs and reverting invalid entries on `LostFocus` or `Enter`.
- **Collapsible AI Copilot Sidebar (`Ctrl+Space`):** Opens beside the workspace via a draggable `5 px` splitter when conversational engineering assistance is needed, and collapses to `0 px` width when closed.

---

## Zone 6: Bottom Telemetry & Diagnostics Status Bar

Anchored across the bottom (`36 px` height), Zone 6 combines VS Code-style authoring actions on the left with real-time hardware health indicators on the right:

- **Left Segment — Authoring & Runtime Strip:**
  - Displays live **Syntax & Compile State** (`Valid` in green, `Stale` in amber, `Invalid` in red) alongside rule-editing badges.
  - Houses compact tactile action buttons for **New Rule Canvas**, **Jump to Rule Table**, **Compile (`F7`)**, **Save (`Ctrl+S`)**, **Deploy (`Ctrl+F5`)**, **Simulate (`F5`)**, **Live Debug (`F8`)** *(expands into a pulsing green `LIVE DEBUG` pill when active)*, and **Grid Toggle**.
- **Right Segment — Hardware Telemetry & System Badges:**
  - **Software Update Badge:** Appears passively only when a newer IDE release is available.
  - **RTC Battery Low Warning:** Highlights in crimson red if the connected controller reports low backup coin-cell voltage.
  - **Live Hardware RTC Clock Chip:** Displays the connected controller's real-time clock; click directly to synchronize the device RTC with workstation time.
  - **Active Device Profile Badge:** Shows the current target preset and total tag count; click to inspect full hardware descriptor details.

::: tip Full-Screen Commissioning Mode (`F11`)
Press **`F11`** at any time to toggle distraction-free Full-Screen mode, maximizing vertical canvas and trend-chart real estate on compact `1366 x 768` field laptops.
:::

---

## Next Steps

- [10-Minute First Project](/getting-started/quickstart-first-project) — Put all six zones into practice by building, simulating, deploying, and trending your first control loop.
- [Keyboard Shortcuts & Hotkeys Appendix](/appendices/keyboard-shortcuts) — Printable reference sheet of all global and canvas shortcuts.
