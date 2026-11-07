# Payload Template Macros & System Capacity Limits

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-APPX-002</span>
  <span><strong>Applies To:</strong> <code>MQTT_PUB</code> &bull; <code>HTTP_CLIENT</code> &bull; <code>TCP_CLIENT</code> &bull; <code>DTU Studio</code></span>
  <span><strong>Runtime Architecture:</strong> Deterministic Static Memory Allocation (Zero Heap Fragmentation)</span>
  <span><strong>Standard:</strong> IEC 61131-3 &bull; SynaptiX Wire Contract V2.0</span>
</div>

To guarantee sub-millisecond determinism and zero heap fragmentation during multi-year continuous factory operation, SynaptiX edge firmware pre-allocates fixed static memory pools for tags, rules, function blocks, and network string buffers. Use the reference tables on this page when designing custom cloud payloads and sizing hardware presets.

---

## Part 1: Function Block Payload Template Macro Dictionary

When `PayloadMode` is set to **`Custom`** inside [`MQTT_PUB`](/blocks/mqtt-pub), [`HTTP_CLIENT`](/blocks/http-client), or [`TCP_CLIENT`](/blocks/tcp-client), the embedded runtime expands lightweight single-brace macro tokens directly from live tag memory at transmission time:

| Macro Token | Supported Blocks | Expansion Behavior | Sample Input State | Rendered Output |
| :--- | :--- | :--- | :--- | :--- |
| `{val}` | `MQTT_PUB`, `HTTP_CLIENT`, `TCP_CLIENT` | Value of the primary bound tag (`DataTag` or first wired input `Pin 1`). Integers format without decimals; `REAL` uses compact precision. | `VREG0 = 25.48` (`REAL`) | `25.48` |
| `{val1}` .. `{val8}` | `MQTT_PUB`, `HTTP_CLIENT` | Indexed value of field/pin `1` through `8` in the Visual Field Builder or dynamic input pins. | `Pin 1 = 230.4`, `Pin 2 = 12` | `230.4` / `12` |
| `{val1:0}` .. `{val8:0}` | `MQTT_PUB`, `HTTP_CLIENT` | Formats indexed `REAL` / `INT32` value rounded to **`0` decimal places** (whole integer). | `Pin 1 = 84.67` | `85` |
| `{val1:1}` .. `{val8:1}` | `MQTT_PUB`, `HTTP_CLIENT` | Formats indexed value with fixed **`1` decimal place**. | `Pin 1 = 25.0` | `25.0` |
| `{val1:2}` .. `{val8:2}` | `MQTT_PUB`, `HTTP_CLIENT` | Formats indexed value with fixed **`2` decimal places** (standard currency / energy / pressure precision). | `Pin 1 = 12.3456` | `12.35` |
| `{val1:3}` .. `{val8:3}` | `MQTT_PUB`, `HTTP_CLIENT` | Formats indexed value with fixed **`3` decimal places** (high-precision vibration / current loop). | `Pin 1 = 4.5` | `4.500` |
| `{tag}` | `MQTT_PUB`, `HTTP_CLIENT`, `TCP_CLIENT` | Canonical name or configured field key of the primary bound tag. | `DataTag = VREG0` | `VREG0` |
| `{tag1}` .. `{tag8}` | `MQTT_PUB`, `HTTP_CLIENT` | Configured JSON key or tag identifier of field `1` through `8`. | `Field 1 Key = "temp_c"` | `temp_c` |
| `{vals}` | `MQTT_PUB`, `HTTP_CLIENT` | Expands all configured fields `1..N` as comma-separated `"key":value` JSON members in a single token! | `temp_c=28.4`, `rh=61` | `"temp_c":28.4,"rh":61` |
| `{ts}` | `MQTT_PUB`, `HTTP_CLIENT`, `TCP_CLIENT` | Current UTC Unix Epoch timestamp in seconds (from synced onboard RTC / NTP). | `2026-11-06T07:00:00Z` | `1793948400` |
| `{device}` | `MQTT_PUB`, `HTTP_CLIENT`, `TCP_CLIENT` | Unique hardware station / client identifier configured on the device. | Station `SYNX-GW-01` | `SYNX-GW-01` |
| `{unit}` | `MQTT_PUB`, `HTTP_CLIENT` | Engineering unit string assigned to the primary tag or block (`bar`, `°C`, `kW`, `%`). | `Unit = "bar"` | `bar` |

::: tip Compact Multi-Field Custom Template Pattern (`<= 128 chars`)
When building custom schemas for AWS IoT Core or ThingsBoard within the `128`-character firmware template buffer, use `{vals}` or `{val1:2}` to maximize payload density:
```json
{"id":"{device}","t":{ts},"d":{"p_bar":{val1:2},"q_lpm":{val2:1},"ok":{val3:0}}}
```
SynaptiX IDE's live character counter (`Length / 128`) and regex token validator immediately flag any unsupported macro name or buffer overflow before compilation!
:::

### 1.1 DTU Gateway Studio Serial Passthrough Macros (Studio-Side Preview & Framing)
Inside [DTU Gateway Studio (`Ctrl+Shift+D`)](/tools/dtu-gateway-studio), the **Custom JSON Formatter** additionally supports double-brace serial stream tokens for transparent RS-485 passthrough design: `{{DEVICE_ID}}`, `{{TIMESTAMP}}`, `{{PORT}}`, `{{BAUDRATE}}`, `{{SERIAL_PAYLOAD}}`, `{{HEX_STREAM}}`, `{{PAYLOAD_LEN}}`, and `{{PACKET_SEQ}}`.

---

## Part 2: System Capacity & String Buffer Limits

Every parameter below reflects the hard deterministic ceiling enforced by both SynaptiX IDE validation and edge runtime firmware:

### 2.1 Global Runtime, Logic & Function Block Pools

| Subsystem Resource | Maximum Capacity | Per-Block / Per-Item Detail |
| :--- | :--- | :--- |
| **Total Runtime Tags (`Max Tags`)** | `128` tags | `32-bit` aligned per tag (`BOOL`, `INT32`, or IEEE-754 `REAL`), plus `8` read-only `SYS_*` system tags. |
| **Compiled Rules (`Max Rules`)** | `100` rules *(or `50` on Compact/Gateway presets)* | Deterministic top-to-bottom scan evaluation (`Trigger -> Condition -> Guard -> Action`). |
| **Hardware Timer Blocks** | `8` concurrent instances | Shared across [`TON`](/blocks/ton), [`TOF`](/blocks/tof), [`TP`](/blocks/tp), and [`CYCLE_TIMER`](/blocks/cycle-timer). |
| **Hardware Counter Blocks** | `8` concurrent instances | Shared across [`CTU`](/blocks/ctu) and [`CTD`](/blocks/ctd) (`INT32` accumulator, optional retentive backing). |
| **Modbus RTU Master Pollers (`MB_READ`)** | `4` blocks per project | Up to `4` dynamic tag-mapped output pins per `MB_READ` block (`PinLabel <= 12 chars`). |
| **Modbus RTU Master Writers (`MB_WRITE`)** | `4` blocks per project | Supports `Channel-Indexed` mode or `Direct Registers` (`FC05, FC06, FC15, FC16`). |
| **Direct Hardware Actuators (`DIRECT_WRITE`)** | `8` blocks (`DO0 .. DO7`) | `1` dedicated hardware block per physical digital output pin (`DO`, `PWM`, `PTO`, or `AddressableBus`). |
| **MQTT Telemetry Publishers (`MQTT_PUB`)** | `4` blocks per project | Up to `8` telemetry fields (`{val1}..{val8}`) per `MQTT_PUB` block. |
| **MQTT Command Subscribers (`MQTT_SUB`)** | `2` blocks per project | Up to `4` atomic JSON extraction pins per `MQTT_SUB` block; **Inbound `FIFO` Queue `<= 16` messages**. |
| **Physical RS-485 Serial Ports** | Up to `4` ports (`#1 .. #4`) | Independently configurable as `Modbus RTU Master`, `Modbus RTU Slave`, or `DTU Transference`. |

### 2.2 Fixed String & Protocol Buffer Ceilings

| Field / Buffer Parameter | Exact Character / Byte Limit | Validation Notes |
| :--- | :--- | :--- |
| **`Custom JSON Template` (`MQTT_PUB` / `HTTP`)** | `<= 128 chars` | Validated live in dialog (`CustomTemplateLength <= 128`). |
| **`MQTT Topic` (`MQTT_PUB` / `MQTT_SUB`)** | `<= 32 chars` | UTF-8 topic path; wildcards `+`/`#` permitted only in `MQTT_SUB`, forbidden in `MQTT_PUB`. |
| **`MQTT_SUB JSON Key` (`Pin.JsonKey`)** | `<= 12 chars` | Exact case-sensitive JSON property key extracted per output pin. |
| **`MQTT Broker Host`** | `<= 64 chars` | Domain name or IPv4 address in `Ctrl+Shift+M`. |
| **`MQTT Client ID / Username / Password`** | `<= 32 chars` each | ASCII authentication strings in `Ctrl+Shift+M`. |
| **`HTTP_CLIENT` Endpoint `Url`** | `<= 64 chars` | Must begin with `http://` or `https://`; auto-clamped at `64` characters. |
| **`HTTP_CLIENT` `CustomHeaders`** | `<= 56 chars` | Compact header line(s), e.g., `Authorization: Bearer <token>`. |
| **`TCP_CLIENT` `RemoteHost`** | `<= 48 chars` | IPv4 address or hostname of target socket server (`TcpClientNode` limit `<= 48 chars`). |
| **`TCP` Heartbeat / Registration Strings** | `<= 16 chars` (Ping) / `<= 32 chars` (Reg) | Keep-alive payload (e.g., `PING`) and IEC 62443 station login prefix. |
| **`Wi-Fi SSID` / `Wi-Fi Password`** | `<= 32 chars` (SSID) / `<= 64 chars` (Pass) | `Ctrl+Shift+W` wireless station credentials; Live Scanner returns top `8` nearby APs. |

---

## Part 3: Built-In Hardware Preset Resource Matrix

When creating a new project (`Ctrl+N`), SynaptiX IDE configures tag catalogs, toolbox visibility, and compiler ceilings according to the selected **Device Preset**:

| Built-In Preset Name | Target Model ID | `DI` | `DO` | `AI` | `VFLAG` | `VREG` | `VREG_RETAIN` | `COUNTER` | Total Tags | Max Rules | Supported Protocols & Capabilities |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **SynaptiX Remote I/O** | `SYNX-RIO-884` | `8` | `8` | `4` | `32` | `32` | `32` | `8` | `124` | `100` | Base Logic, Retentive, RS-485 Modbus Master/Slave |
| **SynaptiX Compact I/O** | `SYNX-RIO-440` | `4` | `4` | `0` | `16` | `16` | `16` | `4` | `60` | `50` | Base Logic, Retentive, RS-485 Modbus Master/Slave |
| **SynaptiX Relay Unit** | `SYNX-RIO-880` | `8` | `8` | `0` | `32` | `32` | `32` | `8` | `120` | `100` | Base Logic, Retentive, RS-485 Modbus Master/Slave |
| **SynaptiX Data Logger** | `SYNX-DL-424` | `4` | `2` | `4` | `16` | `32` | `32` | `4` | `94` | `50` | Base Logic, Retentive, Hardware RTC, SD Datalogging |
| **SynaptiX Gateway** | `SYNX-GW-220` | `2` | `2` | `0` | `32` | `32` | `32` | `4` | `104` | `50` | Logic, Retentive, Ethernet, Modbus Master & TCP, MQTT Pub/Sub, HTTP, TCP Stream |
| **SynaptiX Controller** | `SYNX-CTRL-884` | `8` | `8` | `4` | `32` | `32` | `32` | `8` | `124` | `100` | Full Flagship: Logic, Retentive, RTC, SD, Ethernet, Wi-Fi, Modbus, MQTT Pub/Sub, HTTP, TCP Stream |
| **SynaptiX DTU** | `SYNX-DTU-485` | `0` | `0` | `0` | `32` | `32` | `32` | `4` | `100` | `50` | Pure Gateway: Ethernet, Wi-Fi, RS-485 Master/Slave, Modbus TCP, MQTT Pub/Sub, HTTP, TCP Stream |

---

## Related Documentation

- [Ecosystem & Device Presets](/concepts/ecosystem-overview)
- [Tag Architecture & Physical Data Types](/concepts/data-model-and-types)
- [MQTT_PUB](/blocks/mqtt-pub) &bull; [HTTP_CLIENT](/blocks/http-client) &bull; [TCP_CLIENT](/blocks/tcp-client)
