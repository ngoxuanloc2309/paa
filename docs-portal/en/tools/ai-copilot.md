# SynaptiX AI Copilot (`Ctrl+Space`)

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-TOOL-005</span>
  <span><strong>Shortcut:</strong> <code>Ctrl+Space</code></span>
  <span><strong>Capabilities:</strong> Context-Aware Tag, Rule, Timer, Modbus &amp; MQTT Graph Synthesis</span>
  <span><strong>Safety Model:</strong> Preview-Before-Commit &bull; Mandatory Human-in-the-Loop Verification</span>
</div>

**SynaptiX AI Copilot** is an integrated industrial engineering assistant docked inside **SynaptiX IDE**. Press `Ctrl+Space` from any workspace to slide open the 380 px Copilot side panel, describe your automation requirement in plain English or Vietnamese, and let Copilot synthesize typed tags, 4-stage control rules, IEC timers/counters, linear calibrators, RS-485 Modbus pollers, and MQTT JSON telemetry pipelines directly onto your project.

![SynaptiX AI Copilot Side Panel & Graph Synthesis](/images/synaptix_ai_copilot.png)

---

## 1. Initial Setup & Workspace Context Awareness

### 1.1 API Key & Model Configuration
1. Press `Ctrl+Space` to open the **SynaptiX AI** panel and click the **[Settings]** gear button in the panel header.
2. Enter your **Gemini API Key**, choose a low-latency model (`gemini-3.8-flash` default, `gemini-3.7-flash`, or `gemini-3.6-flash`), and click **Save Settings**. Credentials are stored locally in your user profile—never embedded into shared project files.

### 1.2 Live Project Context Bar & Suggestion Chips
Unlike generic web chatbots that have no visibility into your hardware I/O layout, SynaptiX AI Copilot continuously inspects your live workspace context:
- **Device Resource Profile:** Respects the exact `DI`, `DO`, `AI`, `VREG`, `VREG_RETAIN`, and `COUNTER` limits of your active [Device Preset](/concepts/ecosystem-overview).
- **Live Context Telemetry:** Displays active **Node Count**, **Tag Catalog Count**, **Compiler Status** (including any active diagnostic error codes so Copilot can explain and fix them), and **Currently Selected Nodes** on the canvas.
- **Dynamic Suggestion Chips:** Offers one-click contextual prompts tailored to whether your canvas is empty, contains uncompiled errors, or has specific blocks selected.

---

## 2. What AI Copilot Can Synthesize

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>STEP 1</span>
      </div>
      <span class="synx-node-status">PROMPT</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Natural Spec</span>
        <span class="synx-node-alias">Functional Prompt</span>
        <span class="synx-node-val">English / Vietnamese</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Infer</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>STEP 2</span>
      </div>
      <span class="synx-node-status">REASONING</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">AI Inference</span>
        <span class="synx-node-alias">Context &amp; Budget</span>
        <span class="synx-node-val">Hardware Target Limits</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Generate</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 L13.5,4 L13.5,9.5 Q13.5,13.5 8,15 Q2.5,13.5 2.5,9.5 L2.5,4 Z" /></svg>
        <span>STEP 3</span>
      </div>
      <span class="synx-node-status">SYNTHESIS</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Project Synthesis</span>
        <span class="synx-node-alias">FBD Logic Network</span>
        <span class="synx-node-val">Tags • Rules • Blocks</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Preview</span>
  </div>

  <div class="synx-canvas-node is-step is-done">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>STEP 4</span>
      </div>
      <span class="synx-node-status is-active">● DIFF VIEW</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Proposal Preview</span>
        <span class="synx-node-alias">Diff Inspection</span>
        <span class="synx-node-val">Apply • Ctrl+Z Undo</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

| Synthesis Domain | Generated Artifacts |
| :--- | :--- |
| **Tag Catalog Provisioning** | Allocates unused `DI`, `DO`, `AI`, `VFLAG`, `VREG`, or `VREG_RETAIN` slots, enforces `BOOL` / `INT32` / `REAL` physical types, and populates descriptive aliases and units. |
| **Interlocked Control Loops** | Builds complete `INPUT -> TRIGGER -> COMPARATOR -> GUARD -> ACTION` chains with proper `Set` / `Reset` latching and E-Stop permissive gating. |
| **Timing, Counting & Scaling** | Wires [`TON`](/blocks/ton) debounce delays, [`TOF`](/blocks/tof) cooling overruns, [`TP`](/blocks/tp) fixed pulses, [`CTU`](/blocks/ctu) batch counters, and [`SCALE`](/blocks/scale) `4..20 mA` two-point linearizers. |
| **RS-485 & IIoT Telemetry** | Generates [`MB_READ`](/blocks/mb-read) register polling pins, [`MQTT_PUB`](/blocks/mqtt-pub) multi-field JSON builders, and [`MQTT_SUB`](/blocks/mqtt-sub) inbound command decoders. |
| **Compiler Error Remediation** | Explains structural grammar or write-conflict diagnostics in plain language and proposes corrected wiring. |

---

## 3. Industrial Prompt Engineering Patterns

Ambiguous prompts produce generic logic. Writing structured engineering prompts that state **1) Tag Pins & Types**, **2) Engineering Units & Thresholds**, **3) Hysteresis / Dwell Timing**, and **4) E-Stop / Fault Interlocks** yields production-grade schematics on the first pass:

| Prompt Dimension | Weak / Ambiguous Prompt | Strong Industrial Engineering Prompt |
| :--- | :--- | :--- |
| **Pump Pressure Control with Dwell & E-Stop** | *"Turn on the relief valve when pressure gets too high."* | *"Scale analog input `AI0` (`4..20 mA`) to `0.0..16.0 bar` into `VREG0` (`BoilerPressure_Bar`). If `VREG0 >= 12.5 bar` continuously for `5 s` (`TON`), energize relief valve `DO0`, gated by active-high E-Stop healthy contact `DI2 == 1`. Reset `DO0` when `VREG0 <= 10.5 bar`."* |
| **Post-Stop Cooling Fan Overrun** | *"Keep cooling fan running for a bit after the motor stops."* | *"Control main spindle `DO0` with Start pushbutton `DI0` (rising edge) and Stop pushbutton `DI1`, interlocked by thermal overload healthy `DI3 == 1`. Drive cooling fan `DO1` via a `TOF` off-delay timer so `DO1` stays ON during operation and continues running for `15 s` after `DO0` turns OFF."* |
| **RS-485 Sensor to MQTT Telemetry** | *"Read temperature from Modbus and send to MQTT."* | *"Poll Slave ID `2` on `RS-485 #1` Holding Register `40001` (`INT16`, scale `0.1`) into `VREG1` (`ChamberTemp_C`) and `40002` (`UINT16`, scale `0.1`) into `VREG2` (`ChamberHum_Pct`) every `1000 ms`. Publish both fields with device ID and UTC timestamp every `10 s` (`QoS 1`) to MQTT topic `plant/oven1/telemetry`."* |

::: tip Iterative Refinement & Full Undo (`Ctrl+Z`)
Every Copilot generation surfaces as a reviewable proposal (`HasPendingAiProposal`) with auto-layout node placement. Even after accepting a generated circuit onto the [Logic Graph (`Ctrl+2`)](/workspaces/logic-graph-tab), the entire multi-node insertion is recorded as a single atomic transaction on the undo stack—press `Ctrl+Z` at any time to roll back cleanly.
:::

---

## 4. Mandatory Human-in-the-Loop Verification Checklist

::: danger Safety-Critical Engineering Rule (IEC 61508-3)
AI-generated control logic is an **engineering draft**, never a substitute for qualified commissioning verification. You **MUST** complete every item in the 4-step verification checklist below before executing **Atomic Deploy (`Ctrl+F5`)** to physical machinery!
:::

1. **Inspect Physical Tag Types & Polarity (`Ctrl+3`):**
   - Confirm that Normally-Closed (`NC`) hardware safety circuits (E-Stop mushroom buttons, thermal overload relays, low-level float switches) match the exact `== 1` (healthy closed loop) vs. `== 0` (broken wire / tripped) polarity wired in your electrical panel!
   - Verify `INT32` vs. `REAL` (`Float32`) assignments in the [Tag Catalog](/workspaces/tag-catalog-tab) so fractional thresholds (such as `12.5 bar`) are never truncated into integer registers.
2. **Execute Deterministic Compilation (`F7`):**
   - Press `F7` and confirm **0 Errors / 0 Write Conflicts** in the compiler status bar and [Rule Table (`Ctrl+4`)](/workspaces/rule-table-tab).
3. **Run Nominal & Adversarial Offline Simulation (`F5`):**
   - Launch the [Offline Simulator (`F5`)](/tools/simulator-and-live-debug) and verify **three mandatory scenarios**:
     - *Happy Path:* Normal start/stop sequence and threshold crossing after full `TON` dwell duration.
     - *Boundary / Chatter Rejection:* Brief spikes shorter than `TON.PT` or within hysteresis deadband do **not** falsely trip actuators.
     - *Fault / E-Stop Injection:* Dropping the E-Stop or safety `GUARD` contact immediately drives actuators to their safe de-energized state (`0`).
4. **Two-Phase Atomic Deploy & Live Observation (`Ctrl+F5` -> `F8`):**
   - Deploy via [Atomic Deploy (`Ctrl+6`)](/workspaces/deploy-tab) with mechanical loads isolated first, then engage **Online Live Debug (`F8`)** to confirm real sensor polarities before enabling live actuators.

---

## Related Documentation

- [Logic Graph Canvas (`Ctrl+2`)](/workspaces/logic-graph-tab)
- [Blueprints Library (`Ctrl+5`)](/workspaces/blueprints-tab)
- [Simulator & Online Live Debug (`F5` / `F8`)](/tools/simulator-and-live-debug)
- [Integration Application Notes](/tools/application-notes)
