# DTU Gateway Studio (`Ctrl+Shift+D`)

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-TOOL-003</span>
  <span><strong>Shortcut:</strong> <code>Ctrl+Shift+D</code> (or <code>Ctrl+1</code> on DTU Preset)</span>
  <span><strong>Target Hardware:</strong> <code>SYNX-DTU-485</code> &bull; <code>SYNX-GW-220</code> &bull; Hybrid Edge Controllers</span>
  <span><strong>Uplinks:</strong> MQTT(s) &bull; HTTP(s) REST &bull; Raw TCP Socket</span>
</div>

**DTU Gateway Studio** is a dedicated no-code fieldbus-to-cloud commissioning workbench built directly into **SynaptiX IDE**. When you create a project using the **SynaptiX DTU (`SYNX-DTU-485`)** preset, DTU Gateway Studio opens automatically as your primary workspace. On hybrid **SynaptiX Controller** or **SynaptiX Gateway** projects, you can launch it at any time by pressing `Ctrl+Shift+D` (or selecting **Device -> DTU Gateway Configuration...**).

::: tip Quick 4-Step DTU Gateway Commissioning Guide
Follow the four guided sections in the left sidebar to quickly provision your gateway:
1. **Section 0 (RS-485):** Define downstream serial slave registers to poll (Slave ID, registers, interval) or enable serial passthrough mode.
2. **Section 1 (Wi-Fi):** Configure network credentials (SSID & Password) with DHCP or static IPv4 addressing.
3. **Section 2 (Cloud Uplink):** Select your target cloud protocol: **MQTT** (AWS IoT, EMQX, HiveMQ), **HTTP REST Webhook**, or **TCP Socket**.
4. **Section 3 (Payload / JSON):** Customize the telemetry JSON structure and publishing rate (e.g., every `5 seconds`).
:::

---

## 1. The 4 Guided Sidebar Sections

The left navigation rail of DTU Gateway Studio organizes gateway provisioning into four sequential engineering sections, each displaying a live status pill (`regs` count, active `SSID`, protocol badge, and publish interval):

### Section `0`: RS-485 Channel Poller & Serial Mode
Configures the physical RS-485 port (`RS-485 #1 .. #4`), framing (`1200 .. 115200 bps`, `8N1` / `8E1` / `8O1` / `8N2`), turnaround timing, and operating mode:
- **Modbus RTU Master (`ModbusMaster`):** Define cyclic sensor channels (`Slave ID`, `FC01..FC04` / `FC05..FC16`, 1-based address, `INT16`/`UINT16`/`INT32`/`REAL`/`BOOL`, endianness `ABCD`/`CDAB`/`BADC`/`DCBA`, linear scale factor, and target tag alias).
- **Modbus RTU Slave (`ModbusSlave`):** Exposes the [Fixed SCADA Slave Register Map](/tools/rs485-and-scada-slave) to external masters on the selected Slave ID (`1..247`).
- **Transparent Serial Passthrough (`Transference`):** Captures arbitrary raw byte or ASCII frames (scales, barcode scanners, legacy printers) delimited by silent `FrameTimeoutDelayMs` (default `20 ms`, max packet `512..2048 bytes`) and forwards them directly to the cloud uplink.

### Section `1`: Wi-Fi & IPv4 Network Provisioning
Configures onboard 2.4 GHz wireless station connectivity (`WifiEnabled`, `WifiAutoReconnect`, `SSID`, `Password`, nearby network scan list with `dBm` RSSI indicators) alongside **DHCP** or **Static IPv4** (`StaticIp`, `SubnetMask`, `Gateway`, `Primary/Secondary DNS`) and hardware MAC inspection.

### Section `2`: Cloud Uplink Protocol (`MQTT` / `HTTP REST` / `TCP Socket`)
Routes acquired serial data to one of three industrial IP uplink engines:

| Uplink Mode | Key Configuration Parameters | Reliability & Security Features |
| :--- | :--- | :--- |
| **`MQTT` (`MQTT / MQTTS`)** | Broker Host, Port (`1883`/`8883`), Client ID, Username/Password, Publish Topic, Subscribe Topic, `QoS 0/1/2`, `Retain`. | Full X.509 Root CA & Mutual TLS (`mTLS`), SNI override, plus **Last Will & Testament (LWT)** offline detection. |
| **`HTTP REST` (`POST` / `PUT`)** | Endpoint URL, Method (`POST`/`PUT`), Custom Headers (`Authorization: Bearer ...`, `Content-Type`), Timeout (`s`), Field Mapper. | Supports **Event-Driven (`OnDataReceived`)** instant forwarding or **Periodic (`IntervalSec`)** batching per RFC 9110. |
| **`TCP Socket` (`Client` / `Server`)** | Socket Mode (`Client` to `RemoteHost:RemotePort`, or `Server` on `LocalPort`). | Automatic `ReconnectDelaySec`, keep-alive `HeartbeatPayload` (`PING` every `30 s`), and **IEC 62443 Station Registration Packet** (`None`, `OnConnect`, or `PrependToEveryPacket`). |

### Section `3`: Timing, JSON Payload Formatter & Bandwidth Estimator
Controls polling/uplink cadence (`PollingIntervalMs`, `PublishIntervalSec`, `SendOnChange` COV optimization) and formats outbound payloads across three modes:
1. **Standard IEC Telemetry (No-Code):** Toggle checkboxes for `Device ID`, `UTC Timestamp`, `Port Index`, `Sequence Counter`, `Hex Stream`, and `Payload Length` with live JSON preview.
2. **Custom JSON Template:** Author platform-specific schemas (AWS IoT Shadow, ThingsBoard, Azure IoT) using DTU macro placeholders (`{{DEVICE_ID}}`, `{{TIMESTAMP}}`, `{{PORT}}`, `{{BAUDRATE}}`, `{{SERIAL_PAYLOAD}}`, `{{HEX_STREAM}}`, `{{PAYLOAD_LEN}}`, `{{PACKET_SEQ}}`) validated in real time by the built-in JSON syntax checker.
3. **Raw Transparent Stream:** Bypasses JSON wrapping entirely to stream raw binary/Modbus/ASCII bytes over TCP sockets.

::: info Cellular / LTE Monthly Data Estimator
Section `3` continuously calculates **Estimated Bus Load (`%`)** and **Estimated Monthly Data (`MB/month`)** from your active register count, payload byte length, and `PublishIntervalSec`—helping engineers size industrial 4G/LTE M2M SIM data plans accurately before field deployment.
:::

---

## 2. The 1-2-3 Quick Wizard for Rapid Field Commissioning

When commissioning standard Modbus sensors under tight plant-floor schedules, click **Quick 1-2-3 Wizard** in the top action bar to launch a streamlined 3-step guided flow:

1. **Step 1 — RS-485 Sensor Registers (`FastRegisters`):** Pick serial speed (`9600 8N1`), enter Slave ID, register address, data type, scale factor, and friendly tag name (e.g., `Temp_C`, `ActivePower_kW`). Step validation unlocks **Next** automatically once at least one valid channel is defined.
2. **Step 2 — Target Uplink Endpoint:** Select `MQTT`, `HTTP REST`, or `TCP Socket` and enter the target host/URL and topic/headers.
3. **Step 3 — Cadence, Live Preview & Finish:** Set `PublishIntervalSec` (default `10 s`), verify the rendered `LivePayloadPreview` JSON card, and click **Apply & Generate** to commit the entire pipeline in under 60 seconds.

---

## 3. Moxa-Style Wire Frame Inspector & Live Packet Sniffer

The collapsible right-hand **Device Sensor Diagnostics & Wire Frame Inspector** panel gives field engineers hardware-level visibility into every RS-485 transaction without external USB-to-RS485 dongles or third-party serial terminals:

```text
+-------------------------------------------------------------------------+
| MOXA-STYLE MODBUS WIRE FRAME INSPECTOR          [● COM4 · SYNX-DTU-485] |
+-------------------------------------------------------------------------+
| Target: Slave 1 · FC03 · 40001 (Temp_C)          Latency: 14 ms [GOOD]  |
| Live Scaled Value: 28.4 °C                       Raw Register: 284      |
| ----------------------------------------------------------------------- |
| TX Request (8B):  [ 01 ] [ 03 ] [ 00 00 ] [ 00 01 ] [ 84 0A ] (CRC OK)  |
| RX Response (7B): [ 01 ] [ 03 ] [ 02 ] [ 01 1C ] [ B8 49 ]    (CRC OK)  |
+-------------------------------------------------------------------------+
```

### 3.1 Moxa-Style Modbus Wire Frame Inspector
- **On-Demand Frame Breakdown (`ToggleWireFrame`):** Dissects every channel's exact `TX` query and `RX` response into bracketed protocol fields: `[ Slave ID ]`, `[ Function Code ]`, `[ 0-Based Wire Offset ]`, `[ Register/Byte Count ]`, `[ Data Payload ]`, and `[ Little-Endian CRC-16 ]`.
- **Single Probe & Continuous Live Polling:** Click **Read Once** to execute an immediate diagnostic transaction through the connected SynaptiX unit, or toggle **Continuous Polling** to watch **Live Scaled Value**, **Live Raw Value**, and **Round-Trip Latency (`ms`)** update in real time.
- **Automated Fault Hints:** Color-coded ANSI/ISA-101 status banners (`Good`, `Warning`, `Fault`) translate timeouts, CRC mismatches, and Modbus exception responses (`0x01..0x0B`) into actionable wiring and addressing remedies.

### 3.2 Live Packet Sniffer & Diagnostic Terminal
Switch to the **Live Diagnostic Monitor** tab to test end-to-end serial-to-cloud forwarding interactively:
- **ASCII or Raw HEX Injection (`TerminalSendFormat`):** Inject UTF-8 strings (e.g., `TEMP:28.4,HUM:62.1`) or raw hexadecimal frames (e.g., `01 03 00 00 00 02 C4 0B`) into the pipeline.
- **Full-Duplex Sniffer Log:** Inspect timestamped `[TX RS-485]`, `[RX RS-485]` hex dumps, CRC-16 verification results, JSON template expansion, and `[UPLINK MQTT/HTTP/TCP]` broker acknowledgments (`PUBACK` / `HTTP 200 OK`).

---

## 4. Automatic Logic Graph Synchronization & Hardware Upload

DTU Gateway Studio is **bidirectionally integrated** with the core SynaptiX visual engineering model—never an isolated "black box":

### 4.1 Automatic Function Block Generation (`AutoGenerateFbd`)
When **Auto-Generate FBD Blocks on Logic Graph** is enabled (default `true`), applying your DTU configuration automatically synthesizes and wires the corresponding visual nodes onto the [Logic Graph Canvas (`Ctrl+2`)](/workspaces/logic-graph-tab):
- Allocates typed `VREG` tags in the [Tag Catalog (`Ctrl+3`)](/workspaces/tag-catalog-tab) with your exact aliases and engineering units.
- Drops and configures [`MB_READ`](/blocks/mb-read) poller blocks grouped cleanly by Slave ID and contiguous register spans.
- Wires those tag outputs directly into an [`MQTT_PUB`](/blocks/mqtt-pub), [`HTTP_CLIENT`](/blocks/http-client), or [`TCP_CLIENT`](/blocks/tcp-client) uplink block—allowing you to add local [`COMPARATOR`](/blocks/comparator) alarms, [`SCALE`](/blocks/scale) calibrators, or [`TON`](/blocks/ton) debounce timers on top of your gateway pipeline!

### 4.2 Full Hardware Upload (`F6` / `UploadFromDevice`)
Connecting to an already-deployed `SYNX-DTU-485` unit in the field and pressing `F6` (or clicking **Upload from Device** in the DTU Studio header) reads the complete active configuration image from non-volatile memory, switches automatically to **DTU Gateway Studio**, and reconstructs all RS-485 parameters, Wi-Fi settings, MQTT/HTTP/TCP endpoints, LWT topics, and FBD blocks with 100% fidelity.

---

## Related Documentation

- [RS-485 Config & SCADA Slave Map (`Ctrl+Shift+B`)](/tools/rs485-and-scada-slave)
- [MQTT TLS & Wi-Fi Provisioning (`Ctrl+Shift+M` / `W`)](/tools/mqtt-and-wifi-config)
- [Integration Application Notes (AN-001 & AN-002)](/tools/application-notes)
