# MQTT_PUB — IIoT Telemetry Publisher

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-BLK-MQTTPUB</span>
  <span><strong>Block Category:</strong> IIoT Cloud &amp; IP Network</span>
  <span><strong>Execution Class:</strong> Asynchronous Edge-to-Broker Telemetry Dispatcher</span>
  <span><strong>Standard:</strong> OASIS MQTT v3.1.1 &bull; Sparkplug / JSON Edge Telemetry</span>
</div>

## 1. Synopsis & Execution Semantics

The **`MQTT_PUB`** (**IIoT MQTT Telemetry Publisher**) function block packages live process tags, scaled analog measurements, batch counters, and fault alarms into structured payloads and publishes them asynchronously over Wi-Fi / Ethernet / Cellular to the MQTT Broker configured in [MQTT TLS & Wi-Fi Provisioning](/tools/mqtt-and-wifi-config).

![SynaptiX MQTT Telemetry Publisher Configuration](/images/synaptix_mqtt_pub.png)

`MQTT_PUB` supports both **single-wire scalar streaming** and **multi-tag JSON batching** without writing a single line of C or script code:
- **3 Trigger Strategies (`PublishMode`):** Rising-edge event (`Event`), Change-of-Value with hysteresis (`COV` `Deadband`), or periodic timer (`Cyclic` `IntervalMs`).
- **3 Payload Engines (`PayloadMode`):** Zero-code **JSON Visual Field Builder** (`WrapValues`, `IncludeDevice`, `IncludeTimestamp`), **Raw** scalar string, or **Custom Template** (`<= 128 chars`) with deterministic firmware macro expansion.

---

## 2. Pinout & Signal Contract

`MQTT_PUB` dynamically adapts its left-hand input connectors (`SynchronizeConnectors`) so the block matches your exact `PublishMode` and `PayloadMode`:

| Pin | Direction | Signal Kind | Data Type | Visibility Condition | Functional Contract |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **`Trig`** | Input | Event (`0 → 1`) | `BOOL` | **Visible ONLY when `PublishMode = Event`** *(hidden in `COV` & `Cyclic`)* | **Publish Trigger:** Captures current input/tag values and queues an MQTT publish packet on rising edge (`0 → 1`). |
| **`Val`** | Input | Value | `BOOL` / `INT32` / `REAL` | Visible when `PayloadMode = Raw`, or `JSON` with `0` fields, or `Custom` with only `` `{val}` `` | **Single-Value Input:** Primary process value serialized into the outgoing message. |
| **`<Field.Key>`** | Input *(Dynamic)* | Value | `BOOL` / `INT32` / `REAL` | Visible when `PayloadMode = JSON` and `Fields.Count > 0` | **Visual Field Builder Inputs:** One named input connector per configured JSON key (replaces default `Val`). |
| **`<Token>`** | Input *(Dynamic)* | Value | `BOOL` / `INT32` / `REAL` | Visible when `PayloadMode = Custom` with `` `{val1}` ``..`` `{val8}` `` or custom tokens | **Template Token Inputs:** Auto-extracted connectors (up to `8` pins) bound to template placeholders. |
| **`Done`** | Output | Event (Pulse) | `BOOL` | Always visible | Pulses `1` when the packet is dispatched (and PUBACK confirmed for `QoS 1`/`2`). |
| **`Err`** | Output | Value | `BOOL` | Always visible | Asserts `1` if the MQTT broker session is disconnected or transmission times out. |

---

## 3. Inspector Parameters & Constraints

### 3.1. Broker Topic, QoS & Trigger Parameters

| Parameter | Property Key | Type & Domain | Default | Description & Constraints |
| :--- | :--- | :--- | :---: | :--- |
| **MQTT Topic** | `Topic` | `String` (UTF-8) | `"synaptix/telemetry/data"` | Destination topic path. Supports `` `{device}` `` runtime substitution and one-click presets (`synaptix/{device}/telemetry`, `synaptix/{device}/alarms`, `synaptix/{device}/status`). Must not contain MQTT wildcards (`+`, `#`). |
| **QoS Level** | `Qos` | `0` \| `1` \| `2` | `0` | `0`: At most once (high-rate telemetry) · `1`: At least once (alarms & batch counts) · `2`: Exactly once. |
| **Retain Flag** | `Retain` | `BOOL` | `false` | When `true`, the broker stores the last known message for newly connecting dashboards. |
| **Publish Mode** | `PublishMode` | `"Event"` \| `"COV"` \| `"Cyclic"` | `"Event"` | Selects edge-triggered, Change-of-Value deadband, or periodic interval publishing. |
| **COV Deadband** | `Deadband` | `Double` (`> 0`) | `0.5` | Minimum value change (`|x - x_last| ≥ Deadband`) required to publish in `COV` mode. |
| **Cyclic Interval** | `IntervalMs` | `UINT32` (`ms`) | `1000` | Periodic transmission interval in milliseconds when `PublishMode = Cyclic`. |
| **Engineering Unit** | `Unit` | `String` | `"bar"` | Unit string substituted when using the `` `{unit}` `` macro token. |

### 3.2. Payload Modes & Macro Token Contract

| Payload Mode | Configuration Controls | Output Format & Firmware Limits |
| :--- | :--- | :--- |
| **1. `JSON`** *(Visual Field Builder)* | `Fields` collection (`Key`, `Tag`, `Unit`), `WrapValues` (`true`), `IncludeDevice` (`true`), `IncludeTimestamp` (`true`) | Generates valid JSON automatically. When `WrapValues = true`, fields nest inside `"values": { ... }`; when `false`, keys flatten at root alongside `"device"` and `"ts"`. |
| **2. `Raw`** | Single `Val` wire or `DataTag` | Publishes compact plain-text scalar value (e.g., `45.2` or `1`) for lightweight SCADA topics. |
| **3. `Custom`** *(Template)* | `CustomTemplate` (**strictly `<= 128 chars`**) | Free-form JSON/CSV/line-protocol template expanded on the MCU. Validated in real time (`0 / 128` character counter + token whitelist). |

### Supported Firmware Macro Tokens (`Custom` Mode — Max `128` Characters)

| Token Syntax | Expanded Runtime Output | Example Result |
| :--- | :--- | :--- |
| `` `{val}` `` | Primary input / `DataTag` formatted value | `12.45` |
| `` `{val1}` `` .. `` `{val8}` `` | Value of Field / Connector `1..8` in natural tag type | `248` |
| `` `{val1:0}` `` .. `` `{val8:3}` `` | Value of Field `1..8` with fixed `0..3` decimal places (`FLOAT32` / `INT32`) | `` `{val1:2}` `` → `12.40` |
| `` `{tag}` ``, `` `{tag1}` `` .. `` `{tag8}` `` | Symbolic tag name of primary tag or Field `1..8` | `"VREG0_PipeBar"` |
| `` `{vals}` `` | JSON array of all configured field values | `[12.45, 38.2, 1]` |
| `` `{ts}` `` | Monotonic / RTC Unix timestamp (milliseconds / epoch) | `1730880000125` |
| `` `{device}` `` | Provisioned SynaptiX controller hardware/station identifier | `"SynaptiX_Controller"` |
| `` `{unit}` `` | Configured `Unit` string property | `"bar"` |

*(See [Payload Macros & Capacity Limits](/appendices/macro-tokens-and-limits) for full byte-budget tables).*

---

## 4. Publish Modes & Payload Framing Rules

The `MQTT_PUB` block transforms industrial tags into cloud messages across 3 flexible mechanisms:
- **Publish Trigger Modes (`PublishMode`):** Dispatches on rising edges of pin `Trig`, periodically via `IntervalMs`, or on change of value (`Δ ≥ Deadband`).
- **Flexible Serialization:** Supports structured JSON (`Visual Field Builder`), raw single-value streams (`Raw`), or custom MES/ERP templates (`Custom <= 128 characters`).
- **Non-Blocking In-Process Queue:** Outbound frames enter an asynchronous task queue, ensuring broker network handshakes never stall local deterministic logic.

---

## 5. Validation, Error Handling & Live Diagnostics

- **Real-Time Topic Validator & Sanitizer:** If `Topic` is empty, contains spaces, or includes subscriber wildcards (`+` or `#`), `HasTopicError` highlights the input immediately. Clicking **Sanitize** (`SanitizeTopicCommand`) strips illegal characters automatically.
- **`128`-Character & Unsupported Token Guard:** In `Custom` mode, exceeding `128` characters or typing an unknown placeholder (such as `` `{foo}` `` when not mapped) triggers `CustomTemplateValidationError` prior to compilation so malformed templates never reach MCU flash.
- **Live Payload Preview (`PreviewPayloadText`):** Renders the exact expanded JSON payload in real time inside the Inspector, and tracks `PublishCount`, `LastPublishedTime`, and `LastPublishedPayload` during [Online Live Debug](/tools/simulator-and-live-debug) (`F8`).

---

## 6. Practical Industrial Wiring Example

### Multi-Sensor Pump Skid Telemetry Envelope (`JSON` Visual Field Builder + `COV`)

**Objective:** Bundle calibrated discharge pressure (`VREG0_PipeBar`), motor current (`VREG1_MotorAmp`), and run status (`DO0_PumpRun`) into a single JSON message on `synaptix/skid01/telemetry` (`QoS 1`), transmitting automatically whenever pressure shifts by `≥ 0.2 bar`.

```json
{
  "device": "SynaptiX_Skid01",
  "ts": 1730880420000,
  "values": {
    "pressure_bar": 6.45,
    "motor_amps": 14.2,
    "pump_running": 1
  }
}
```

**Configuration Steps:**
1. Place **`MQTT_PUB`** on the canvas and set `Topic = "synaptix/skid01/telemetry"`, `Qos = 1`, `Retain = false`.
2. Set `PublishMode = "COV"` and `Deadband = 0.2`. (The `Trig` connector automatically hides).
3. Select `PayloadMode = "JSON"`, keep `WrapValues = true`, `IncludeDevice = true`, `IncludeTimestamp = true`, and add three rows in **Fields**:
   - `pressure_bar` → `VREG0_PipeBar` (`REAL`)
   - `motor_amps` → `VREG1_MotorAmp` (`REAL`)
   - `pump_running` → `DO0_PumpRun` (`BOOL`)
4. Wire upstream scaled outputs directly to the generated `pressure_bar`, `motor_amps`, and `pump_running` pins on the canvas.
