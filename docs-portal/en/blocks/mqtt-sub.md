# MQTT_SUB — Cloud Command & Setpoint Subscriber

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-BLK-MQTTSUB</span>
  <span><strong>Block Category:</strong> IIoT Cloud &amp; IP Network</span>
  <span><strong>Execution Class:</strong> Pure Background Inbound Listener &amp; Atomic JSON Parser</span>
  <span><strong>Standard:</strong> OASIS MQTT v3.1.1 &bull; IEC 62443 Bad-Data Defense</span>
</div>

## 1. Synopsis & Execution Semantics

The **`MQTT_SUB`** (**IIoT MQTT Inbound Subscriber**) function block is a **pure background source node** that subscribes to cloud/MES command topics, validates incoming JSON payloads against strict schema and range rules, and extracts typed fields directly onto FBD output pins and local tags.

![SynaptiX MQTT Inbound Subscriber Configuration & JSON Tester](/images/synaptix_mqtt_sub.png)

Safety-critical industrial design guarantees:
- **Zero-Input Pure Listener Topology:** `MQTT_SUB` has **no `EN` or `Trig` input pins** — it listens continuously in the background whenever the broker transport is online, keeping downstream FBD sheets clean.
- **Atomic All-or-Nothing JSON Commit:** Every inbound message is validated across **all** configured dynamic `Pins` before touching runtime memory. If even one required field is missing, has a mismatched data type, or falls outside `[MinValue .. MaxValue]`, **zero** pins are modified, `DroppedCount` increments, and `Err` asserts with a specific diagnostic code.
- **Boot Retain Protection & Watchdog Fail-Safe:** `IgnoreRetainedOnBoot` prevents stale retained broker commands from unexpectedly starting machinery on power-up, while `FailSafeTimeoutMs` handles cloud silence deterministically.

---

## 2. Pinout & Signal Contract

`MQTT_SUB` exposes zero input pins on its left side and `2 + N` output pins on its right side:

| Pin | Direction | Signal Kind | Data Type | Visibility | Functional Contract |
| :--- | :---: | :---: | :---: | :---: | :--- |
| *(None)* | Input | — | — | Always empty | Pure background listener; requires no upstream enable wire. |
| **`Valid`** | Output | Event (1-Scan Pulse) | `BOOL` | Always visible | **Atomic Commit Pulse:** Pulses `1` for one scan cycle (`10 ms`) immediately after an inbound JSON packet passes all pin checks and updates all outputs simultaneously. |
| **`Err`** | Output | Value (Latched) | `BOOL` | Always visible | **Inbound Fault Flag:** Asserts `1` on JSON syntax error, missing field, type mismatch, out-of-range value, FIFO overflow, broker drop, or fail-safe timeout. Clears to `0` on next valid message. |
| **`<Pin.Label>`** | Output *(Dynamic)* | Value | `BOOL`, `INT16`, `INT32`, `FLOAT`, `STRING` | 1 pin per row in `Pins` | **Extracted JSON Field Output:** Emits the parsed value from `JsonPath` (auto-resolving both flat `{"grid": 5}` and wrapped `{"values": {"grid": 5}}` envelopes) and writes to `TargetTag`. |

---

## 3. Inspector Parameters & Constraints

### 3.1. Subscription, Queueing & Fail-Safe Properties

| Parameter | Property Key | Type & Domain | Default | Description & Industrial Safety Purpose |
| :--- | :--- | :--- | :---: | :--- |
| **Subscribe Topic** | `Topic` | `String` | `"synaptix/commands/inbound"` | MQTT topic filter (`SanitizeTopic` supported; MQTT single-level `+` and multi-level `#` wildcards permitted). |
| **QoS Level** | `Qos` | `0` \| `1` \| `2` | `1` | Defaults to `QoS 1` (at-least-once delivery) for reliable command reception. |
| **Queue Mode** | `QueueMode` | `FIFO` \| `LatestWins` | `FIFO` | **`FIFO`** (`Capacity = 16`): dequeues `1` message per scan in arrival order so back-to-back sorting commands are never lost. **`LatestWins`**: discards older queued packets and applies only the freshest setpoint. |
| **Queue Capacity** | `QueueCapacity` | `INT32` | `16` | Depth of the deterministic ring buffer when `QueueMode = FIFO`. |
| **Ignore Retained on Boot** | `IgnoreRetainedOnBoot` | `BOOL` | `true` | Discards retained packets flagged by the broker upon initial connection so old commands never trigger surprise motion. |
| **Fail-Safe Timeout** | `FailSafeTimeoutMs` (`TimeoutMs`) | `UINT32` (`ms`, `0` = Off) | `0` | Watchdog window in milliseconds. If no valid message arrives before expiration, triggers `FailSafeAction` and sets `ErrCode = TimeoutExpired`. |
| **Fail-Safe Action** | `FailSafeAction` | `KeepLastValue` \| `ResetToZero` \| `ResetToDefault` | `KeepLastValue` | Deterministic fallback applied to all dynamic pins when `FailSafeTimeoutMs` expires or broker connection drops. |

### 3.2. Dynamic JSON Pin Binding Properties (`MqttPinBindingViewModel`)

| Field | Property Key | Supported Options | Description |
| :--- | :--- | :--- | :--- |
| **Field / JSON Path** | `FieldName` / `JsonPath` | e.g., `grid`, `setpoint_temp`, `values.speed` | Key name or dot-path extracted from inbound JSON; automatically synchronizes the canvas pin `Label`. |
| **Data Type** | `DataType` | `Int16`, `Int32`, `Float`, `Bool`, `String` | Target type contract (`Bool` accepts `true`/`false`, `1`/`0`, `"TRUE"`/`"FALSE"`). |
| **Min / Max Bounds** | `MinValue`, `MaxValue` | `Double?` (optional) | Rejects the entire packet (`OutOfRange`) if the parsed number violates `[MinValue .. MaxValue]`. |
| **Default Value** | `DefaultValue` | `Double` (`0.0`) | Safe fallback value loaded when `FailSafeAction = ResetToDefault`. |
| **Target Tag** | `TargetTag` | `TagModel?` | Optional direct binding to a `VREG`, `VBIT`, or `DO` tag in [Tag Catalog](/workspaces/tag-catalog-tab). |

---

## 4. Atomic All-or-Nothing Ingestion Principle

To protect industrial machinery from malformed or partial cloud commands, `MQTT_SUB` enforces strict atomic validation:
- **Atomic All-or-Nothing:** All configured `JsonPath` keys within an incoming packet must parse successfully (syntax valid, required fields present, matching data types, and within `[MinValue .. MaxValue]` limits).
- **100% Valid:** All output pins and bound tags commit simultaneously within the same scan cycle, asserting a single-scan `Valid = 1` pulse.
- **Any Field Fails:** The entire packet is rejected atomically, asserting `Err = 1` and preserving prior memory states intact. Incomplete or half-mutated data is never committed.

---

## 5. Validation, Error Handling & Live Diagnostics

### Standardized `MqttErrorCode` Diagnostic Table

| Code | Enum Identifier | Fault Condition | Runtime Memory Guarantee |
| :---: | :--- | :--- | :--- |
| **`0`** | `None` | Valid JSON parsed and committed. | All pins updated; `MsgCount` incremented. |
| **`1`** | `ConnectionLost` | Broker TCP/TLS session disconnected. | Applies `FailSafeAction`; `Err = 1`. |
| **`2`** | `JsonSyntaxError` | Malformed JSON brackets, quotes, or trailing commas. | Packet dropped (`DroppedCount++`); prior pin values untouched. |
| **`3`** | `MissingField` | A configured `JsonPath` key is absent from the JSON object. | Packet dropped atomically; identifies `FaultyField` in dialog. |
| **`4`** | `TypeMismatch` | Non-numeric string (e.g., `"abc"`) sent to `Int16`/`Float` pin. | Packet dropped atomically; zero tag corruption. |
| **`5`** | `OutOfRange` | Numeric value violates `[MinValue .. MaxValue]` safety bounds. | Packet dropped atomically; prevents unsafe actuator setpoints. |
| **`6`** | `QueueOverflow` | Burst rate exceeded `16`-slot `FIFO` buffer capacity. | Oldest/excess burst flagged so engineer can tune scan/QoS. |
| **`7`** | `TimeoutExpired` | No valid packet received within `FailSafeTimeoutMs`. | Executes `KeepLastValue`, `ResetToZero`, or `ResetToDefault`. |

- **Built-In Live JSON Payload Tester (`SimInputPayload`):** Both the Inspector and the `MQTT_SUB` modal dialog include an interactive **Test JSON Payload** box (`SimulateSend`). Paste any real cloud payload and click **Test Parse** to verify path extraction, range rejection, and `Valid` pulsing offline without needing a live broker!

---

## 6. Practical Industrial Wiring Example

### Remote HVAC Chiller Setpoint Dispatch with Bounds Defense (`LatestWins` + Watchdog)

**Objective:** Accept remote chilled-water temperature setpoints (`{"temp_sp": 7.5, "enable": true}`) on `synaptix/hvac/chiller1/setpoint`, enforce strict physical limits `[4.0 °C .. 18.0 °C]` so a cloud typo like `-10` can never freeze evaporator tubes, and fall back to `12.0 °C` if cloud heartbeat stops for `60,000 ms`.

<div class="synx-node-chain">
  <div class="synx-canvas-node is-trigger">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,14 L8,8 M5,10 A4,4 0 0 1 11,10 M3,7.5 A7,7 0 0 1 13,7.5 M1.5,5 A9.5,9.5 0 0 1 14.5,5" /></svg>
        <span>MQTT_SUB</span>
      </div>
      <span class="synx-node-status is-active">● WATCHDOG</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">LatestWins • 60s</span>
        <span class="synx-node-alias">BMS Setpoint</span>
        <span class="synx-node-val">{temp_sp: 7.5, en: 1}</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">7.5 °C</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● GT SP</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">AI0 &gt; temp_sp</span>
        <span class="synx-node-alias">Water Temp Compare</span>
        <span class="synx-node-val">Chilled Water Over SP</span>
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
      <span class="synx-node-status is-pass">● PASS</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">RemoteEn == 1</span>
        <span class="synx-node-alias">Remote Permissive</span>
        <span class="synx-node-val">BMS Enable Gate</span>
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
      <span class="synx-node-status is-active">● SET 1</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">DO0 = 1</span>
        <span class="synx-node-alias">Chiller Compressor</span>
        <span class="synx-node-val">Close Power Contactor</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

**Configuration Steps:**
1. Drop **`MQTT_SUB`** on the canvas and set `Topic = "synaptix/hvac/chiller1/setpoint"`, `Qos = 1`, `QueueMode = LatestWins`, `IgnoreRetainedOnBoot = true`.
2. Set `FailSafeTimeoutMs = 60000` and `FailSafeAction = ResetToDefault`.
3. Add two dynamic pins:
   - `temp_sp`: `DataType = Float`, `MinValue = 4.0`, `MaxValue = 18.0`, `DefaultValue = 12.0`, `TargetTag = VREG_TempSp` (`REAL`).
   - `enable`: `DataType = Bool`, `DefaultValue = 0`, `TargetTag = VBIT_RemoteEn` (`BOOL`).
4. In the dialog's **Simulation** box, test `{"temp_sp": 25.0, "enable": true}` and confirm it immediately rejects with `OutOfRange` while leaving `VREG_TempSp` untouched!
