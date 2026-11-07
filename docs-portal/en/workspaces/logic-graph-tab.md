# 2. Logic Graph Canvas (`Ctrl+2`)

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-WS-002</span>
  <span><strong>Workspace:</strong> Tab 2 &bull; Shortcut <code>Ctrl+2</code></span>
  <span><strong>Scope:</strong> Visual Function Block Authoring, Pin Routing, Multi-Node Alignment &amp; Static Compilation</span>
  <span><strong>Standard:</strong> IEC 61131-3 &bull; IEC/IEEE 82079-1 &bull; ANSI/ISA-101</span>
</div>

---

## 1. Purpose & Operational Role

The **Logic Graph Canvas** (`Ctrl+2`) is the primary visual control engineering environment in **SynaptiX IDE**. Modeled on IEC 61131-3 Function Block Diagram (FBD) ergonomics combined with a deterministic Event-Condition-Action (ECA) execution pipeline, it enables control engineers to build, inspect, simulate, and live-debug industrial automation and IIoT edge logic without writing boilerplate firmware code.

Within the engineering workflow, the **Logic Graph Canvas** fulfills four mission-critical roles:

1. **Deterministic Signal-Flow Authoring:** Dragging function blocks from the hardware-aware DIN-rail toolbox onto a `20 px` precision engineering grid and wiring typed **Value** and **Event** connector pins left-to-right (`INPUT → TRIGGER / COMPARATOR → GUARD → ACTION`).
2. **Precision Schematic Organization:** Maintaining clean, audit-ready plant schematics using orthogonal 90-degree step routing, snap-to-grid placement (`Alt+G`), marquee multi-selection, undo/redo history, and single-keystroke multi-node alignment and distribution commands.
3. **Type-Safe Parameterization & Narrative Verification:** Editing block thresholds, timers, fieldbus channels, and payload templates in the **Right Property Inspector**—protected by Bad-Data Defense (`LostFocus`/`Enter` normalization and auto-revert) and a real-time natural-language **Narrative Preview**.
4. **Integrated Compilation (`F7`), Simulation (`F5`) & Live Debug (`F8`):** Lowering visual networks into deterministic rules verified by static domain checks, executing cycle-accurate offline scans, or superimposing live hardware telemetry directly onto block pins and wires.

---

## 2. Visual Layout & Controls Reference

![SynaptiX Logic Graph Canvas Workbench](/images/synaptix_logic_editor.png)

The **Logic Graph Canvas** is organized as a high-density, neutral-slate (ANSI/ISA-101) 3-column workbench flanked by a bottom authoring and verification status bar:

### 2.1. Workbench Architecture Reference

| Workbench Zone | Width / Position | Core Controls & Operational Behavior |
| :--- | :--- | :--- |
| **Left Function Block Toolbox** | `265 px` Left Dock | Contains an instant **Quick Search** filter and 4 collapsible DIN-rail categories: **Core Rule Pipeline**, **Timing, Counting & Scaling**, **Fieldbus & Hardware Actuation**, and **IIoT Cloud & IP Network**. Blocks unsupported by the active hardware preset display a capability lock badge and explanatory tooltip. |
| **Center Interactive Nodify Canvas** | Fluid Center Viewport | Infinite pan-and-zoom schematic surface rendered with a `20 px` engineering grid (`Alt+G`), orthogonal 90-degree step wires with a generous `12 px` hit-test target, connector-snapping wire creation, and marquee selection box. |
| **Right Property Inspector** | `320 px` Right Dock | Context-sensitive parameter sheet for the selected block. Provides tag pickers, Bad-Data-Defended numeric inputs, dedicated modal configuration launchers (for `MQTT_PUB`, `MQTT_SUB`, `MB_READ`, `MB_WRITE`, `DIRECT_WRITE`, `HTTP_CLIENT`, `TCP_CLIENT`), live **Narrative Preview**, and clickable compiler diagnostics. |
| **Bottom Verification & Debug Bar** | Bottom Status Strip | Displays the real-time **Compile State Badge** (`Valid` green, `Stale` amber, `Invalid` red), **Compile Rules (`F7`)**, **Offline Simulator (`F5`)**, **Online Live Debug (`F8`)**, and **SynaptiX AI Copilot (`Ctrl+Space`)** triggers. |

### 2.2. Connector Pin Semantics & Orthogonal Wire Routing

Every node on the **Logic Graph Canvas** enforces strict visual and electrical separation between **data values** and **execution events** so invalid topologies are prevented during wire dragging:

<div class="synx-node-chain">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>INPUT</span>
      </div>
      <span class="synx-node-status is-live">● VALUE PIN</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Value Pin</span>
        <span class="synx-node-alias">SOURCE DATA</span>
        <span class="synx-node-val">Carries Tag / Register</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Tag Data</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>CMP / TRIGGER</span>
      </div>
      <span class="synx-node-status is-true">● EVAL PIN</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Evaluation</span>
        <span class="synx-node-alias">CONDITION EVAL</span>
        <span class="synx-node-val">Threshold / Edge Check</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">TRUE</span>
  </div>

  <div class="synx-canvas-node is-guard">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 L13.5,4 L13.5,9.5 Q13.5,13.5 8,15 Q2.5,13.5 2.5,9.5 L2.5,4 Z" /></svg>
        <span>GUARD</span>
      </div>
      <span class="synx-node-status is-pass">● GATE PIN</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Event Pin</span>
        <span class="synx-node-alias">INTERLOCK GATE</span>
        <span class="synx-node-val">Permissive AND Check</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">ENABLE</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● ACTUATOR</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Execution</span>
        <span class="synx-node-alias">OUTPUT EXEC</span>
        <span class="synx-node-val">SET, RESET, DIRECT OUT</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

| Pin / Wire Element | Visual & Type Contract | Wiring Rules |
| :--- | :--- | :--- |
| **Value Connector Pins** | Carries typed tag or calibrated analog/register data (`BOOL`, `INT32`, `REAL`). | Connects **INPUT** or **SCALE** outputs into **TRIGGER**, **COMPARATOR**, **GUARD**, or data-consuming function block inputs. |
| **Event Connector Pins** | Carries deterministic scan-cycle firing pulses (`Event` rising/active state). | Connects stage-to-stage along the rule execution spine (`TRIGGER → COMPARATOR → GUARD → ACTION` or stateful timers/counters). |
| **Orthogonal Step Wires** | 90-degree industrial Manhattan routing (`IndustrialStepConnection`) with `12 px` invisible click tolerance. | Supports clicking to select, `Del` to sever, and animated color-coded state propagation during `F5` Simulation and `F8` Live Debug. |
| **Connector-Only Snapping** | Pending wire cursor snaps strictly to valid target input/output pins. | Dangling mid-air wires are automatically discarded upon mouse release, preserving schematic integrity. |

### 2.3. Canvas Navigation, Editing & Multi-Node Alignment Shortcuts

To keep multi-branch industrial control sheets readable during plant audits, **SynaptiX IDE** provides full CAD-grade keyboard accelerators:

| Action / Command | Shortcut / Gesture | Operational Effect |
| :--- | :--- | :--- |
| **Pan Viewport** | Middle-Mouse Drag / Right-Mouse Drag | Scrolls smoothly across large multi-network logic diagrams. |
| **Zoom Viewport** | `Ctrl + Mouse Wheel` (or Mouse Wheel) | Zooms in for pin-level inspection or zooms out for full plant topology overview. |
| **Marquee Multi-Selection** | Left-Mouse Drag on empty canvas (`Ctrl+Click` to toggle) | Selects multiple nodes and wires simultaneously for batch movement, copying, or alignment. |
| **Align Left / Center / Right** | `Alt+Left` / `Alt+C` / `Alt+Right` | Aligns the horizontal `X` coordinates of 2 or more selected blocks to their left edge, horizontal center, or right edge. |
| **Align Top / Middle / Bottom** | `Alt+Up` / `Alt+M` / `Alt+Down` | Aligns the vertical `Y` coordinates of 2 or more selected blocks to their top edge, vertical midpoint, or bottom edge. |
| **Distribute Horizontally / Vertically** | `Alt+Shift+H` / `Alt+Shift+V` | Spaces 3 or more selected blocks evenly along the horizontal or vertical axis. |
| **Snap to Grid / Toggle Grid** | `Alt+G` | Toggles the `20 px` engineering grid (`20 px` snap stride when visible; `1 px` fine placement when hidden) and snaps selected nodes to clean grid intersections. |
| **Undo / Redo** | `Ctrl+Z` / `Ctrl+Y` (`Ctrl+Shift+Z`) | Traverses the deterministic graph command history across node placements, deletions, wire edits, and moves. |
| **Cut / Copy / Paste / Duplicate** | `Ctrl+X` / `Ctrl+C` / `Ctrl+V` / `Ctrl+D` | Clones selected sub-networks with preserved internal wiring and offset coordinates. |
| **Select All / Delete Selection** | `Ctrl+A` / `Del` | Selects every node on the active sheet or cleanly removes selected nodes and their incident wires. |
| **Compile / Save & Compile** | `F7` / `Ctrl+S` | Lowers the visual graph into deterministic ECA rules (`0..99`), runs static validation, and updates the [Rule Table](/workspaces/rule-table-tab). |

---

## 3. Step-by-Step Operational Procedures

### Procedure 3.1: Authoring a Guarded Threshold Control Network

1. Press `Ctrl+2` to open the **Logic Graph Canvas**. Ensure the `20 px` grid is enabled (`Alt+G`) for clean schematic alignment.
2. From the **Left Toolbox** (`Core Rule Pipeline`), drag an **INPUT**, a **TRIGGER**, a **COMPARATOR**, a **GUARD**, and an **ACTION** block onto the canvas from left to right.
3. Click the **INPUT** block and, in the **Right Property Inspector**, bind it to an analog sensor tag (for example, `AI0` — Boiler Pressure).
4. Drag a wire from the **INPUT** output pin to the **TRIGGER** input pin, configure the trigger mode (such as `ON_CHANGE` or dwell `ForMs` filter), and wire **TRIGGER → COMPARATOR**.
5. Select the **COMPARATOR** block, choose operator `>` and enter the threshold (for example, `8.5` for a `REAL` tag). Notice how typing `"8"` automatically normalizes to `"8.0"` upon pressing `Enter` or leaving the field.
6. Wire **COMPARATOR → GUARD**, bind the **GUARD** block to your emergency permissive flag (for example, `DI0` — E-Stop OK, normal state `1`), and wire **GUARD → ACTION** (`DO0` — Relief Valve `SET 1`).
7. Read the auto-generated **Narrative Preview** at the bottom of the Right Inspector to confirm the English operational sentence matches your P&ID cause-and-effect specification.

### Procedure 3.2: Cleaning Up Layout & Compiling to Deterministic Rules (`F7`)

1. Drag a selection marquee around the pipeline blocks (`INPUT`, `TRIGGER`, `COMPARATOR`, `GUARD`, `ACTION`).
2. Press `Alt+M` (**Align Middle**) followed by `Alt+Shift+H` (**Distribute Horizontally**) so all five stages sit on a crisp horizontal centerline with uniform spacing.
3. Press `F7` (**Compile Rules**).
4. Verify that the bottom **Compile State Badge** turns green (`Valid`). If any block is missing a mandatory tag binding or wire, click the warning item in the diagnostic list to automatically pan and center the canvas on the offending block.
5. *(Optional)* Press `F5` to test the network interactively inside the [Offline Cycle-Accurate Simulator](/tools/simulator-and-live-debug), or press `Ctrl+4` to inspect the lowered ECA rules in the [Rule Table](/workspaces/rule-table-tab).

---

## 4. Engineering Best Practices & Edge Cases

::: tip SEMANTIC HASH STABILITY FOR LAYOUT EDITS
Moving blocks, aligning branches (`Alt+M`, `Alt+Shift+H`), or toggling grid snap (`Alt+G`) changes only visual schematic coordinates—not the underlying control semantics. **SynaptiX IDE** computes a **Semantic Program Hash** so tidying up node positions on the canvas will **not** falsely mark a verified hardware deployment as out-of-sync.
:::

- **Bad-Data Defense on Inspector Inputs:** Every numeric threshold, timer preset (`PT`), and counter preset (`PV`) in the Right Inspector enforces physical IEC 61131-3 memory contracts. Entering `"25.5"` or `"abc"` into an `INT32` field automatically reverts to the last committed integer value on `LostFocus` or `Enter`, guaranteeing visual truth between the schematic and **SynaptiX Runtime** memory.
- **Macro Block Rule Budget Awareness:** While a standard pipeline (`INPUT → TRIGGER → COMPARATOR → GUARD → ACTION`) compiles into **1 ECA rule**, stateful IEC function blocks automatically expand into coordinated multi-rule state machines during `F7` compilation (`TON`, `TOF`, and `TP` expand into **2 rules** each; `CTU` and `CTD` expand into **4 rules** each). Monitor total utilization against the 100-rule hardware budget in the [Rule Table](/workspaces/rule-table-tab).
- **Unlocking All Blocks in Offline Sandbox Mode:** When disconnected from physical hardware and using the full **Edge Controller** preset (or Sandbox Design Mode), all 20 function blocks across RS-485 and IIoT categories are unlocked for rapid prototyping. Before field deployment, switch to your exact target hardware preset in the [Device Workspace](/workspaces/device-tab) and press `F7` to verify full hardware compatibility.

---

## Related Documentation

- [Blocks Catalog & Pin Legend](/blocks/overview)
- [Deterministic Execution Pipeline](/concepts/execution-pipeline)
- [4. Rule Table (`Ctrl+4`)](/workspaces/rule-table-tab)
- [Simulator & Online Live Debug](/tools/simulator-and-live-debug)
- [SynaptiX AI Copilot](/tools/ai-copilot)
- [Keyboard Shortcuts & Hotkeys](/appendices/keyboard-shortcuts)
