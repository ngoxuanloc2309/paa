# Offline Simulator (`F5`) & Online Live Debug (`F8`)

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-TOOL-004</span>
  <span><strong>Shortcuts:</strong> <code>F5</code> (Offline Simulator) &bull; <code>F8</code> (Online Live Debug) &bull; <code>Ctrl+7</code> (Live Watch)</span>
  <span><strong>Engine Cadence:</strong> Deterministic Virtual Scan &bull; 20 FPS (<code>50 ms</code>) Live Canvas Telemetry</span>
  <span><strong>Standard:</strong> IEC 61508-3 Pre-Deployment Verification</span>
</div>

Industrial commissioning mistakes on live machinery risk damaged actuators, tripped breakers, and costly plant downtime. **SynaptiX IDE** eliminates blind deployment by providing a two-stage verification workflow:
1. **Cycle-Accurate Offline Simulator (`F5`):** Validate 100% of your rules, timers, counters, scaling equations, and inbound JSON parsing on your desktop with **zero physical hardware attached**.
2. **Online Live Canvas Debug (`F8`):** Connect to a physical SynaptiX controller (`F4`) and overlay real-time 20 FPS wire states, tag values, timer countdowns, and sensor quality flags directly onto your visual Logic Graph.

---

## Part 1: Cycle-Accurate Offline Simulator (`F5`)

Press `F5` (or click **Run Simulation** in the top toolbar) to open the **Offline Simulator** drawer/view. Internally, the desktop simulator instantiates the exact same deterministic compiled program representation (`RuntimeEngine` + `SimulatedClock`) executed by SynaptiX edge firmware.

![SynaptiX Cycle-Accurate Offline Simulator](/images/synaptix_simulator.png)

### 1.1 Pre-Flight Compile Gate Protection

To uphold **Visual Truth** (Axiom 2), the Offline Simulator refuses to execute outdated or invalid graphs:
- Before starting, the simulator verifies `CompileState == Valid`.
- If you modify a node, rewire a pin, or introduce a type/grammar error while simulation is running, the simulator **automatically pauses** and displays a clear gate banner (*"Simulation paused: Logic has changed or contains errors. Recompile (`F7`) to resume."*), ensuring you never observe stale rule behavior that disagrees with the canvas on screen.

### 1.2 Interactive Hardware & Clock Controls

| Simulator Control | Action & Engineering Capability |
| :--- | :--- |
| **Run / Pause Engine (`ToggleEngine`)** | Starts or freezes continuous deterministic scan cycles (`100 ms` virtual tick base scaled by `SimSpeed`). |
| **Single-Cycle Step (`StepScan`)** | Pauses continuous execution and advances `SimulatedClock` by exactly **one single scan tick** (`Scan #N -> Scan #N+1`). Essential for proving edge-triggered (`Rising` / `Falling`) rules and multi-stage latch ordering! |
| **Digital Input Toggles (`DI0..DI7`)** | Binary `[ 0 ]` / `[ 1 ]` tactile switches that simulate limit switches, E-Stop contacts, and proximity sensors, immediately logging state transitions into the **Event Stream**. |
| **Analog & Register Sliders / Inputs (`AI0..AI3`, `VREG`)** | Inject exact voltage/current raw counts or engineering floats (e.g., `4.0 mA`, `12.0 mA`, `20.0 mA`, or out-of-range fault values) to test [`SCALE`](/blocks/scale) clamping and [`COMPARATOR`](/blocks/comparator) hysteresis deadbands. |
| **Virtual RTC Clock (`SimClock` & `SimSpeed`)** | Override simulated wall-clock time (e.g., jump instantly to `08:30` or `22:00`) and accelerate time multiplier (`1x`, `5x`, `10x`) to test shift schedules and multi-hour [`TON`](/blocks/ton) / [`CYCLE_TIMER`](/blocks/cycle-timer) sequences in seconds. |
| **Inbound JSON Payload Injector ([`MQTT_SUB`](/blocks/mqtt-sub))** | Paste sample cloud JSON strings (e.g., `{"grid": 5, "color": 1}` or malformed/missing-key adversarial payloads) directly into an `MQTT_SUB` node's simulation box to verify **Atomic All-or-Nothing** parsing, `Valid` / `Err` pulses, and downstream actuation! |

### 1.3 Execution Trace Feed & Rule Evaluation Matrix

While running or single-stepping, the Simulator displays two synchronized diagnostic panes:
- **Rule Status Matrix (`RuleStatusList`):** Shows every compiled rule's real-time `Trigger`, `Condition`, `Guard`, and `Action` state (`FilterActiveOnly` isolates rules firing on the current scan).
- **Execution Trace Feed & Event Stream:** Timestamped chronological log (`[HH:mm:ss.fff]`) recording every `DI` toggle, rule firing event, timer expiration, and counter preset hit.

---

## Part 2: Online Live Canvas Debug (`F8`)

Once your logic is verified offline and deployed (`Ctrl+F5`) to a physical SynaptiX unit connected via **USB CDC** or **Modbus TCP (`F4`)**, switch to the [Logic Graph (`Ctrl+2`)](/workspaces/logic-graph-tab) and press `F8` to activate **Online Live Debug**.

::: tip Live Visual States on the Canvas during F8
Pressing **F8** transforms your static logic drawing into a live operational mimic:
- 🟢 **Energized Green Wires:** Active logic paths carrying `TRUE` / level `1`.
- ⚫ **Neutral Slate Wires:** Inactive logic paths (`FALSE` / level `0`).
- 🔢 **Live Pin Badges:** Direct engineering values rendered at block pins (`INT32`, `REAL`, units).
- ⏱️ **Timer Progress:** Real-time countdowns (`ET` / `PT`) on `TON`, `TOF`, and `TP` timer blocks.
- ⚠️ **`[STALE]` Badges:** Instant alerts for disconnected RS-485 cables or timed-out MQTT brokers.
:::

### 2.1 Live Visual Overlays on the Canvas

Following **ANSI/ISA-101** high-contrast situational awareness rules, `F8` transforms static function blocks into a live process mimic at a smooth, non-blocking **20 FPS (`50 ms` UI refresh)**:

1. **Energized Wire Highlighting:**
   - Boolean wires carrying `1` (`TRUE`) illuminate in crisp operational green (`SuccessBrush`), while `0` (`FALSE`) wires remain muted slate—allowing you to trace permissive chains (`INPUT -> TRIGGER -> COMPARATOR -> GUARD -> ACTION`) visually across the sheet in one glance.
2. **Real-Time Pin & Node Value Badges:**
   - Every [`INPUT`](/blocks/input), [`SCALE`](/blocks/scale), [`MB_READ`](/blocks/mb-read), [`CTU`](/blocks/ctu)/[`CTD`](/blocks/ctd), and [`MQTT_PUB`](/blocks/mqtt-pub) node displays its live committed memory value (`INT32` or formatted `REAL` with engineering units).
3. **Live Timer Countdown & Counter Progress:**
   - [`TON`](/blocks/ton), [`TOF`](/blocks/tof), [`TP`](/blocks/tp), and [`CYCLE_TIMER`](/blocks/cycle-timer) blocks render live elapsed time (`ET`) vs. preset time (`PT`) and output `Q` status as machinery operates.
4. **Hardware & Fieldbus Quality Indicators (`[STALE]`):**
   - If a downstream RS-485 sensor wired to [`MB_READ`](/blocks/mb-read) disconnects or exceeds its retry budget, or an [`MQTT_SUB`](/blocks/mqtt-sub) heartbeat window expires, affected tags immediately display an amber/red **`[STALE]`** quality badge directly on the block—pinpointing broken field cables in seconds.

::: warning Safe Read-Only Observation Mode
During **Online Live Debug (`F8`)**, structural graph edits are guarded so accidental drags do not desynchronize the visual canvas from the running firmware image. Press `F8` again to exit Live Debug, perform your logic edits, verify with `F7` / `F5`, and commit atomically via `Ctrl+F5`.
:::

---

## Comparison: Offline Simulator vs. Online Live Debug vs. Live Watch

SynaptiX IDE provides three complementary diagnostic tools tailored to each phase of an automation project lifecycle:

| Feature / Capability | Offline Simulator (`F5`) | Online Live Debug (`F8`) | Live Watch & Trends (`Ctrl+7`) |
| :--- | :--- | :--- | :--- |
| **Physical Hardware Required?** | **No** (100% desktop virtual engine) | **Yes** (`F4` USB CDC or Modbus TCP) | **Yes** (`F4` USB CDC or Modbus TCP) |
| **Primary Visual Representation** | Interactive I/O switchboard, single-step clock & rule trace log | Animated wires, pin values & timer `ET/PT` superimposed on FBD canvas | High-density IBM Carbon tag table, real-time strip charts & force panel |
| **Single-Step Scan (`StepScan`)** | **Supported** (1 tick per click) | Not applicable (real-time MCU scan) | Not applicable (real-time MCU scan) |
| **Time Acceleration & RTC Jump** | **Supported** (`SimClock` & `SimSpeed`) | Real wall-clock hardware time | Real wall-clock hardware time |
| **Signal Trend Plotting & CSV Log** | Event trace feed (`60` latest events) | Instantaneous current state on nodes | **Multi-channel real-time trend curves** & historical telemetry export |
| **Controlled Tag Write / Force** | Free virtual toggling of all `DI`/`AI`/`VREG` | Read-only canvas observation | **Guarded online tag write** (`DO`, `VFLAG`, `VREG`) with safety lease |
| **Ideal Engineering Phase** | Algorithm design, edge-case & negative testing before panel wiring | Troubleshooting interlocks, stuck timers & `[STALE]` fieldbus nodes | Loop tuning, analog stability analysis & long-duration burn-in |

---

## Related Documentation

- [Logic Graph Canvas (`Ctrl+2`)](/workspaces/logic-graph-tab)
- [Atomic Deploy Workspace (`Ctrl+6` / `Ctrl+F5`)](/workspaces/deploy-tab)
- [Live Watch & Trends (`Ctrl+7`)](/workspaces/live-watch-tab)
- [SynaptiX AI Copilot (`Ctrl+Space`)](/tools/ai-copilot)
