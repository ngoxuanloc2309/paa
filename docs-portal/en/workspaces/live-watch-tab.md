# 7. Live Watch & Trends (`Ctrl+7`)

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-WS-007</span>
  <span><strong>Workspace:</strong> Tab 7 &bull; Shortcut <code>Ctrl+7</code></span>
  <span><strong>Scope:</strong> 20 FPS Reactive Telemetry, Sparkline Trends, GOOD vs STALE Quality &amp; Commissioning Force Overrides</span>
  <span><strong>Standard:</strong> IEC 61508-3 &bull; IEC/IEEE 82079-1 &bull; ANSI/ISA-101 &bull; ANSI Z535.6</span>
</div>

---

## 1. Purpose & Operational Role

The **Live Watch & Trends Workspace** (`Ctrl+7`) is the real-time operational telemetry, signal quality diagnostic, and commissioning actuation console of **SynaptiX IDE**. Engineered around the ANSI/ISA-101 **Visual Truth Doctrine** and an event-driven reactive streaming architecture, it provides an unvarnished 1:1 window into the live memory state and scan health of a connected **SynaptiX Edge Controller** (or virtual simulator).

During field wiring checkout, loop tuning, and live plant troubleshooting, **Live Watch** fulfills four critical engineering duties:

1. **Debounced 20 FPS Reactive Telemetry Streaming:** Ingesting high-frequency hardware register updates via push-based in-process channels and coalescing UI frames (`50 ms` / `20 FPS` reactive pipeline with crisp `5 Hz` statistical summaries) so dense tag tables never stutter or freeze the workstation UI.
2. **Inline Sparkline Micro-Trends & Session Statistics:** Plotting live waveform trajectories alongside session `Min`, `Max`, `Avg`, `TransitionCount`, and raw 32-bit hexadecimal tooltips for immediate noise, spike, and contact-bounce diagnosis.
3. **Strict `GOOD` vs `STALE` Signal Quality Honesty:** Explicitly distinguishing verified live polls (`GOOD`) from disconnected or timed-out readings (`STALE` with dimmed opacity and `[Last]` suffix) so operators never mistake frozen last-known values for live plant safety states.
4. **Lease-Guarded Commissioning Write & Force Overrides:** Enabling authorized engineers in **Manual Commissioning Mode** to test physical actuators (`DO`) and internal registers (`VFLAG`, `VREG`, `VREG_RETAIN`, `COUNTER`) under a hardware **Safety Lease** watchdog and explicit retentive Flash commit control.

---

## 2. Visual Layout & Controls Reference

![SynaptiX Live Watch and Real-Time Telemetry Workspace](/images/synaptix_live_watch.png)

The **Live Watch** console combines a **Controller Health Telemetry Strip**, a **Filter & Watchlist Pinning Toolbar**, the high-density **Live Telemetry & Sparkline DataGrid**, and a safety-guarded **Manual Commissioning Control Bar**.

### 2.1. Reactive Telemetry Pipeline Architecture (`50 ms` / `20 FPS`)

Unlike legacy polling tools that block UI threads or waste serial bandwidth when backgrounded, **SynaptiX IDE** activates live hardware streaming strictly on demand when **Live Watch** (`Ctrl+7`) or **Online Live Debug** (`F8`) is active:

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>SOURCE 10ms</span>
      </div>
      <span class="synx-node-status is-live">● 100 Hz</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">SynaptiX Runtime</span>
        <span class="synx-node-alias">Hardware Scan Loop</span>
        <span class="synx-node-val">Hard Real-Time</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Push</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>BUFFER</span>
      </div>
      <span class="synx-node-status">ATOMIC</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">IRuntimeStateStore</span>
        <span class="synx-node-alias">Lock-Free Cache</span>
        <span class="synx-node-val">Snapshots 20 FPS</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Render</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>PRESENTATION</span>
      </div>
      <span class="synx-node-status">20 FPS</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Live Watch UI</span>
        <span class="synx-node-alias">WPF DataGrid</span>
        <span class="synx-node-val">Sparkline Charts</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Truth</span>
  </div>

  <div class="synx-canvas-node is-step is-done">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 L13.5,4 L13.5,9.5 Q13.5,13.5 8,15 Q2.5,13.5 2.5,9.5 L2.5,4 Z" /></svg>
        <span>INTEGRITY</span>
      </div>
      <span class="synx-node-status is-active">● VERIFIED</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Visual Truth</span>
        <span class="synx-node-alias">User Safeguard</span>
        <span class="synx-node-val">GOOD vs STALE</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

### 2.2. Controller Health Strip & Toolbar Controls Reference

| Control / Diagnostic Metric | Location | Operational Meaning & Behavior |
| :--- | :--- | :--- |
| **Controller Health Metrics** (`Uptime`, `CPU`, `RAM`, `Scan Time`) | Top Health Strip | Displays continuous runtime uptime (`Xd Yh Zm Ws`), MCU CPU load (`%`), SRAM utilization (`%`), current/peak scan execution time (`ms`), and **Last Reset Reason** (`Power-On`, `Hardware Pin`, `Software Command`, `Watchdog Timer`, `Brownout Detector`). |
| **Online Status & Last Poll Timestamp** | Top Toolbar Right | Displays connection state (`ONLINE` / `OFFLINE`) and millisecond-precision timestamp (`HH:mm:ss.fff`) of the most recent verified controller snapshot. |
| **Instant Search Box** | Top Toolbar Left | Filters live rows immediately by `Tag Name`, `Alias`, hexadecimal `Modbus Address`, `Data Type`, or `Index`. |
| **`★ Watchlist` Pin Filter** | Top Toolbar | Isolates starred priority signals. Pin assignments (`★`) persist inside your `.synx` project file and support right-click context actions (**Pin / Unpin / Clear Watchlist**). |
| **Category Filter Chips** | Top Filter Strip | Filters live view by `ALL`, `DI`, `DO`, `AI`, `VFLAG`, `VREG`, `RETAIN` (`VREG_RETAIN`), or `COUNTER`. |
| **`Manual Commissioning Mode` Toggle** | Top Toolbar Action | Enters or exits lease-guarded commissioning override mode (requires explicit operator safety confirmation upon activation). |
| **Retentive Dirty Banner** (`Commit Retain` / `Discard Retain`) | Contextual Alert Strip | Appears automatically whenever `VREG_RETAIN` values are modified during Manual Mode, allowing explicit Flash persistence or clean rollback. |

### 2.3. Live Telemetry Table Columns, Quality States & Override Widgets

| Column | Widget & Typography | Engineering Details |
| :--- | :--- | :--- |
| **`★` (Pin)** | Interactive Star Toggle | Pins critical loop tags to your persistent `.synx` commissioning Watchlist. |
| **`#` & `Tag Name`** | Monospace Index & Code | Canonical runtime tag index and hardware identifier (`DI0`, `DO0`, `AI0`, `VREG0`). |
| **`Description / Alias`** | Left-Aligned Text | Plant P&ID alias synchronized live from the [Tag Catalog](/workspaces/tag-catalog-tab) (`Ctrl+3`). |
| **`Modbus Address` & `Data Type`** | Monospace Pill & Type Badge | Displays SCADA register offset and physical memory contract (`BOOL`, `INT32`, `REAL`). |
| **`Live Value` & `Sparkline`** | Status LED + Formatted Value + Micro-Trend | &bull; **`BOOL`:** Crisp `● 1` (active green) or `○ 0` (neutral slate) plus transition count.<br/>&bull; **`INT32` / `REAL`:** Monospace value + real-time **Sparkline** waveform; hover tooltip reveals raw 32-bit hex plus session `Min` / `Max` / `Avg`.<br/>&bull; **Badges:** Prefixes `[F]` (amber) when actively forced; appends `[Last]` (dimmed) when quality is `STALE`. |
| **`Manual Control / Force`** | Context-Aware Safety Control | &bull; **`DI` / `AI` (Read-Only):** Displays `--` (physical field inputs cannot be forced in software).<br/>&bull; **`DO` / `VFLAG`:** Type-safe industrial `0 / 1` binary slide switch + `Release` (`✕`) button.<br/>&bull; **`VREG` / `VREG_RETAIN` / `COUNTER`:** Bad-Data-Defended numeric box + `Set` + `Release` (`✕`) button. |

### 2.4. Signal Quality Truth Table (`GOOD` vs `STALE`)

| Quality State | Trigger Condition | Visual Representation in Live Watch | Operator Action Required |
| :--- | :--- | :--- | :--- |
| **`GOOD`** | Fresh, CRC-verified telemetry frame received within poll deadline. | Full high-contrast typography, active green/neutral status indicators, live advancing `HH:mm:ss.fff` poll clock, updating Sparkline. | Normal monitoring and commissioning permitted. |
| **`STALE` / `UNKNOWN`** | USB/TCP cable unplugged, controller powered down, or transport timeout (`Reconnecting`). | Row opacity immediately dims, value appends **`[Last]`**, poll clock halts on last verified timestamp, and override controls lock out. | **Do NOT trust `[Last]` values for physical safety decisions.** Restore link (`F4`) and verify `GOOD` quality first. |

---

## 3. Step-by-Step Operational Procedures

### Procedure 3.1: Building a Focused Loop-Check Watchlist & Inspecting Sparklines

1. Connect to your **SynaptiX Edge Controller** (`F4`) and press `Ctrl+7` to open **Live Watch & Trends**.
2. Confirm the top badge reads `ONLINE`, the `Last Poll` millisecond clock is advancing, and **Controller Health** shows normal scan times (`< 5 ms` typical) with zero watchdog resets.
3. Use the **Search** box or category chips (`AI`, `DO`, `VREG`) to locate the sensors and actuators belonging to the skid under test, and click the **`★`** icon on each row.
4. Click the **`★ Watchlist`** toolbar filter to collapse the view down to your pinned signals, then press `Ctrl+S` to persist the Watchlist inside your `.synx` project.
5. Observe the inline **Sparkline** trends on analog/register channels and hover over any **`Live Value`** cell to inspect raw 32-bit hex words alongside session `Min`, `Max`, and `Avg` readings.

### Procedure 3.2: Executing Safe Actuator & Setpoint Overrides in Manual Commissioning Mode

::: danger PHYSICAL MACHINERY MOTION & ENERGIZATION HAZARD
Forcing discrete outputs (`DO`) or writing control registers (`VFLAG`, `VREG`, `VREG_RETAIN`) on a live **SynaptiX Edge Controller** directly energizes physical relays, contactors, solenoid valves, motors, and heating elements—bypassing normal upstream sensor conditions!
- Verify all personnel are clear of moving machinery and pinch points before entering **Manual Commissioning Mode**.
- Confirm hardwired Emergency Stop (E-Stop) circuits and mechanical lockouts (LOTO) are operational and independent of software overrides.
:::

::: warning AUTOMATIC SAFETY LEASE EXPIRATION
Active commissioning overrides are governed by a continuous **Safety Lease** heartbeat between **SynaptiX IDE** and the **SynaptiX Runtime**. If your engineering laptop disconnects, sleeps, or loses USB/Ethernet communication, the controller's hardware lease timer expires automatically and releases forced overrides to prevent orphaned forced actuators.
:::

1. In **Live Watch** (`Ctrl+7`), click **`Manual Commissioning Mode`** on the toolbar and read and confirm the industrial safety interlock dialog.
2. **To Test a Digital Output (`DO`) or Virtual Flag (`VFLAG`):**
   - Click the binary **`0 / 1`** toggle switch in the **`Manual Control / Force`** column.
   - Verify that the row displays the amber **`[F]`** forced indicator and confirm physical relay/actuator response in the cabinet.
   - Click the **`Release` (`✕`)** button next to the switch to return control of that tag back to deterministic ECA scan logic.
3. **To Tune a Numeric Register (`VREG`, `VREG_RETAIN`, or `COUNTER`):**
   - Enter a valid typed number into the row's input box (protected by Bad-Data Defense: `"25.5"` is rejected on `INT32`; `"25"` normalizes to `"25.0"` on `REAL`) and click **`Set`**.
   - If you modified any **`VREG_RETAIN`** Flash setpoint and want the new value preserved permanently across power cycles, click **`Commit Retain`** on the **Retentive Dirty Banner** (or click **`Discard Retain`** to roll back temporary test values).
4. When loop checkout is complete, release all active `[F]` overrides and toggle **`Manual Commissioning Mode`** **OFF** before handing the machine back to production operators.

---

## 4. Engineering Best Practices & Edge Cases

- **Why `DI` and `AI` Cannot Be Forced in Live Watch:** Following strict hardware ground-truth principles, physical input terminals (`DI`, `AI`) reflect real electrical voltage/current on the microcontroller's input pins (`IsForceable = false`). To test downstream logic without actuating real field sensors, test offline inside the [Cycle-Accurate Simulator (`F5`)](/tools/simulator-and-live-debug) or wire a commissioning permissive `VFLAG` in parallel during bench testing.
- **Diagnosing Unexpected Field Resets:** Always check the **Last Reset Reason** badge in the Controller Health strip when investigating intermittent machine stoppages. A `Brownout Detector` report points directly to an undersized or noisy `24 VDC` cabinet power supply rather than a logic bug.
- **Co-Using Live Watch with Online Live Debug (`F8`):** While `Ctrl+7` provides high-density tabular monitoring and Sparklines, pressing `F8` on the [Logic Graph Canvas](/workspaces/logic-graph-tab) (`Ctrl+2`) overlays the exact same reactive `IRuntimeStateStore` telemetry stream directly onto visual blocks and orthogonal wires.

---

## Related Documentation

- [Tag Architecture & Data Types](/concepts/data-model-and-types)
- [3. Tag Catalog (`Ctrl+3`)](/workspaces/tag-catalog-tab)
- [6. Atomic Deploy (`Ctrl+6`)](/workspaces/deploy-tab)
- [Simulator & Online Live Debug](/tools/simulator-and-live-debug)
- [Error Codes & Troubleshooting](/appendices/error-codes-and-troubleshooting)
