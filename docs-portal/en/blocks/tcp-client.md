# TCP_CLIENT — Raw Socket Stream & DTU Client

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-BLK-TCP</span>
  <span><strong>Block Category:</strong> IIoT Cloud &amp; IP Network</span>
  <span><strong>Execution Class:</strong> Persistent Bidirectional TCP Socket Client</span>
  <span><strong>Standard:</strong> IETF RFC 9293 TCP &bull; Industrial ASCII / Raw Socket Streaming</span>
</div>

## 1. Synopsis & Execution Semantics

The **`TCP_CLIENT`** (**Raw TCP Socket & Stream Client**) function block establishes and maintains a persistent outbound TCP socket connection to a remote IPv4/FQDN server (`RemoteHost:RemotePort`) for low-overhead industrial protocols such as barcode/vision camera servers, industrial label printers (ZPL/TSPL), custom SCADA socket collectors, and cellular DTU passthrough gateways.

Core runtime capabilities:
- **Self-Healing Persistent Connection:** Automatically manages socket connection, detects peer disconnects, and executes non-blocking reconnection every `ReconnectDelaySec` seconds.
- **Gateway Registration & NAT Heartbeat:** Supports automatic authentication/identification frame injection (`OnConnect` or `PrependToEveryPacket`) and periodic keep-alive probes (`HeartbeatIntervalSec` + `HeartbeatPayload`) so cellular NAT tables never drop idle links.
- **Deterministic Line Framing:** Appends configurable frame terminators (`CRLF \r\n`, `LF \n`, `Custom` delimiter, or `None` for binary streams) to either **Raw** data or **Custom** macro-formatted strings (`<= 128 chars`).

---

## 2. Pinout & Signal Contract

| Pin | Direction | Signal Kind | Data Type | Visibility | Functional Contract |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **`Send`** | Input | Event (`0 → 1`) | `BOOL` | Always visible | **Dispatch Trigger:** Transmits one framed TCP payload over the active socket on rising edge (`0 → 1`). |
| **`DataIn`** | Input | Value | `INT32` / `REAL` / `BOOL` | Always visible | **Outbound Stream Data:** Supplies raw value or string payload when `DataTag` is set to *DataIn Pin (FBD Auto-link)*. |
| **`Conn`** (`Connected`) | Output | Value | `BOOL` | Always visible | **Socket Established Flag:** High (`1`) while the TCP 3-way handshake is active and healthy; drops to `0` upon disconnect. |
| **`Done`** | Output | Event (Pulse) | `BOOL` | Always visible | **Packet Sent Pulse:** Pulses `1` for one scan cycle when the outbound packet is written to the TCP transmit buffer. |
| **`Err`** (`Error`) | Output | Event / Flag | `BOOL` | Always visible | **Socket Fault Flag:** Asserts `1` when connection fails, peer resets (`RST`), or `Send` triggers while disconnected. |
| **`DataOut`** | Output | Value | `INT32` / `STRING` | Always visible | **Inbound Socket Data:** Emits data frames pushed back from the remote TCP server to the controller. |

---

## 3. Inspector Parameters & Constraints

Click **`[ Configure TCP Client... ]`** (or double-click the node) to open the **TCP Stream Configuration Dialog** with live `[CONNECT]` / `[TX AUTH]` / `[TX PING]` / `[TX DATA]` wire preview:

| Parameter | Property Key | Type & Domain | Default | Firmware Limit & Operational Meaning |
| :--- | :--- | :--- | :---: | :--- |
| **Remote Host** | `RemoteHost` | `String` (**`<= 48 chars`**) | `"192.168.1.100"` | Target server IPv4 address or hostname (`<= 48` characters; non-empty validation enforced via `HostValidationError`). |
| **Remote Port** | `RemotePort` | `UINT16` (`1..65535`) | `8888` | Target TCP listening port (`0` is automatically clamped to `1`). |
| **Reconnect Delay** | `ReconnectDelaySec` | `UINT16` (`s`) | `5` s | Wait interval in seconds before attempting a fresh TCP handshake after socket loss. |
| **Heartbeat Interval** | `HeartbeatIntervalSec` | `UINT16` (`s`, `0` = Off) | `30` s | Idle keep-alive period in seconds. Set to `0` to disable periodic heartbeat packets. |
| **Heartbeat Payload** | `HeartbeatPayload` | `String` | `"PING"` | ASCII keep-alive token transmitted every `HeartbeatIntervalSec` when `HeartbeatIntervalSec > 0`. |
| **Registration Mode** | `RegistrationType` | `"None"` \| `"OnConnect"` \| `"PrependToEveryPacket"` | `"None"` | `None`: disabled · `OnConnect`: sends `RegistrationPayload` once after TCP connect · `PrependToEveryPacket`: prefixes `RegistrationPayload` ahead of every `Send` packet. |
| **Registration Packet** | `RegistrationPayload` | `String` | `""` | Gateway ID / token string used when `RegistrationType ≠ None` (e.g., `"ID:SYNX-NODE-01;"`). |
| **Payload Mode** | `PayloadMode` | `"Raw"` \| `"Custom"` | `"Raw"` | `Raw` sends direct tag/pin data; `Custom` formats an ASCII/JSON string via `CustomTemplate`. |
| **Custom Template** | `CustomTemplate` | `String` (**`<= 128 chars`**) | `"{val}"` | Active when `PayloadMode = Custom` (`<= 128 chars`). Supports `` `{val}` ``, `` `{tag}` ``, `` `{device}` ``, and `` `{ts}` ``. |
| **Framing Delimiter** | `FramingDelimiter` | `"CRLF"` \| `"LF"` \| `"None"` \| `"Custom"` | `"CRLF"` | Line termination appended to outgoing packets: `CRLF` (`\r\n`), `LF` (`\n`), `None` (raw binary), or `Custom`. |
| **Custom Delimiter** | `CustomDelimiterText` | `String` | `";"` | Custom end-of-frame character(s) appended when `FramingDelimiter = Custom`. |

---

## 4. Persistent Socket & Heartbeat Rules

The `TCP_CLIENT` block maintains an active bidirectional socket stream to remote industrial endpoints:
- **Automatic Reconnection:** Automatically attempts a TCP handshake to `RemoteHost:RemotePort`. If refused or disconnected, it asserts `Err = 1` and retries after `ReconnectDelaySec`.
- **On-Connect Registration:** Optionally transmits an initial device identification frame as soon as the socket opens.
- **Heartbeat Keep-Alive:** Transmits periodic `HeartbeatPayload` frames every `HeartbeatIntervalSec` seconds to maintain NAT table bindings and detect half-open sockets.
- **Immediate Data Transmission:** When pin `Send` transitions high, the block serializes the outbound payload (with framing delimiters) and pushes it out over the network.

---

## 5. Validation, Error Handling & Live Diagnostics

- **Host & Port Safety Guards:** `RemoteHost` rejects blank strings and automatically truncates paste inputs beyond **`48` characters** (`HostValidationError`), while `RemotePort` prevents invalid port `0` bindings.
- **Gating Sends with `Conn`:** In high-integrity designs, wire **`Conn`** into a [`GUARD`](/blocks/guard) gate upstream of `Send` (or monitor `Err`) if you want upstream sequencing to pause until the remote socket collector is confirmed online.
- **Interactive Wire Stream Preview (`PreviewStreamText`):** Displays the exact chronological byte stream (`[CONNECT]`, `[TX AUTH]`, `[TX PING]`, `[TX DATA]`) live inside the dialog as you toggle registration and framing options.
- **Live Canvas Status Pill (`F8`):** Shows **`DISCONNECTED`**, **`CONNECTED`**, **`CONN (TX:N)`**, or **`ERR: <SocketFault>`**.

*(For dedicated transparent RS-485-over-TCP/MQTT bridge topologies without FBD wiring, also see [DTU Gateway Studio](/tools/dtu-gateway-studio)).*

---

## 6. Practical Industrial Wiring Example

### Industrial Checkweigher ASCII Telemetry to Factory Socket Collector

**Objective:** Maintain a persistent TCP connection to `192.168.1.200:9100` with an `OnConnect` station registration banner `"REG,SYNX_LINE2"`, send `"PING"` every `30 s`, and transmit a `CRLF`-terminated ASCII weight record `"WT,{device},{val},KG\r\n"` whenever [`MB_READ`](/blocks/mb-read) signals a `Valid` reading.

<div class="synx-node-chain">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,5 L14,5 M14,5 L10,2 M14,5 L10,8 M14,11 L2,11 M2,11 L6,8 M2,11 L6,14" /></svg>
        <span>MB_READ</span>
      </div>
      <span class="synx-node-status is-live">● RS485 #1</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Slave 5 • FC 03</span>
        <span class="synx-node-alias">Checkweigher</span>
        <span class="synx-node-val">Weight_Kg REAL</span>
      </div>
      <div class="synx-node-pin is-out">Valid ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">SEND</span>
  </div>

  <div class="synx-canvas-node is-trigger">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,5 L14,5 M14,5 L10,2 M14,5 L10,8 M14,11 L2,11 M2,11 L6,8 M2,11 L6,14" /></svg>
        <span>TCP_CLIENT</span>
      </div>
      <span class="synx-node-status is-active">● PORT 9100</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● Send</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">192.168.1.200:9100</span>
        <span class="synx-node-alias">ASCII Record CRLF</span>
        <span class="synx-node-val">WT,{dev},{val},KG</span>
      </div>
      <div class="synx-node-pin is-out">Conn ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">CONNECTED</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-pass">● DO1 = 1</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">DO1 • Link LED</span>
        <span class="synx-node-alias">Server Link LED</span>
        <span class="synx-node-val">TCP Channel Healthy</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

**Configuration Steps:**
1. Drop **`TCP_CLIENT`** on the canvas and configure:
   - `RemoteHost = "192.168.1.200"`, `RemotePort = 9100`, `ReconnectDelaySec = 5`
   - `HeartbeatIntervalSec = 30`, `HeartbeatPayload = "PING"`
   - `RegistrationType = "OnConnect"`, `RegistrationPayload = "REG,SYNX_LINE2"`
   - `PayloadMode = "Custom"`, `CustomTemplate = "WT,{device},{val},KG"`, `FramingDelimiter = "CRLF"`
2. Wire `MB_READ.Valid` → `TCP_CLIENT.Send`, `MB_READ.Weight_Kg` → `TCP_CLIENT.DataIn`, and `TCP_CLIENT.Conn` → `DO1_ServerLinkLed`.
