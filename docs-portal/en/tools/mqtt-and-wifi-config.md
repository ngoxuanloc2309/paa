# MQTT TLS Security & Wi-Fi Provisioning

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-TOOL-002</span>
  <span><strong>Shortcuts:</strong> <code>Ctrl+Shift+M</code> (MQTT) &bull; <code>Ctrl+Shift+W</code> (Wi-Fi)</span>
  <span><strong>Protocols:</strong> MQTT v3.1.1 / v5.0 over TLS 1.2 &amp; 1.3 &bull; IEEE 802.11b/g/n</span>
  <span><strong>Security Standard:</strong> IEC 62443-4-2 (X.509 mTLS)</span>
</div>

SynaptiX network-enabled edge controllers, gateways, and DTU units share a centralized device-level IP connectivity configuration. Rather than duplicating broker credentials or Wi-Fi keys across individual function blocks, you configure your cloud broker profile once in the **MQTT Broker & TLS Security Dialog (`Ctrl+Shift+M`)** and provision wireless station connectivity in the **Wi-Fi Provisioning & Live Scanner Dialog (`Ctrl+Shift+W`)**.

---

## Part 1: MQTT Broker & TLS Security Dialog (`Ctrl+Shift+M`)

Press `Ctrl+Shift+M` (or navigate to **Device -> MQTT Broker Configuration...**) to configure the global MQTT client session shared by all [`MQTT_PUB`](/blocks/mqtt-pub) and [`MQTT_SUB`](/blocks/mqtt-sub) blocks in your project, as well as the MQTT uplink channel in [DTU Gateway Studio](/tools/dtu-gateway-studio).

![SynaptiX MQTT Broker & X.509 mTLS Configuration Dialog](/images/synaptix_mqtt_broker.png)

### 1.1 Core Session & Authentication Parameters

| Parameter | Default | Range / Format | Functional Description |
| :--- | :--- | :--- | :--- |
| **Enable MQTT Service** | `Disabled` | `Enabled` / `Disabled` | Master switch activating the embedded asynchronous MQTT client worker on the device. |
| **Broker Host / IP** | *(Empty)* | Up to `64` ASCII chars | FQDN hostname (e.g., `a1b2c3d4-ats.iot.ap-southeast-1.amazonaws.com`, `broker.emqx.io`) or IPv4 address (e.g., `192.168.1.200`) of the target broker. |
| **Port** | `1883` (`8883` w/ TLS) | `1 .. 65535` | Automatically toggles between `1883` (unencrypted TCP) and `8883` (encrypted MQTTS) when **Use TLS** is switched, while still allowing custom enterprise ports. |
| **Client ID** | Device Serial | Up to `32` ASCII chars | Unique MQTT client identifier. Must be globally unique on the target broker to prevent disconnect-loop storms (`CONNACK` session takeover). |
| **Username / Password** | *(Empty)* | Up to `32` ASCII chars each | Optional credentials for SASL/password-authenticated brokers (EMQX, HiveMQ Cloud, Mosquitto, RabbitMQ). Leave blank when using certificate-only mTLS (such as AWS IoT Core). |
| **Keep-Alive Interval** | `60 s` | `10 .. 3600 s` | Maximum idle interval before the edge device transmits a `PINGREQ` packet to hold NAT firewall state tables open and verify broker reachability. |
| **Clean Session** | `Enabled` (`true`) | `true` / `false` | When `true`, starts a fresh session on reconnect. Set to `false` if you want the broker to queue `QoS 1` / `QoS 2` inbound [`MQTT_SUB`](/blocks/mqtt-sub) commands while the device is temporarily offline. |

---

### 1.2 Last Will & Testament (LWT — Instant Offline Detection)

When an industrial edge node loses 24V DC power or suffers a severed WAN fiber link, it cannot transmit a graceful `DISCONNECT` packet. Enabling **Last Will & Testament (LWT)** instructs the MQTT broker at connection time to store an emergency offline payload and publish it automatically the moment the device's Keep-Alive timer expires (`1.5x KeepAliveSec`):

| LWT Field | Default Value | Recommended Production Setting |
| :--- | :--- | :--- |
| **Enable LWT (`WillEnabled`)** | `Disabled` | `Enabled` on all production telemetry gateways. |
| **Will Topic (`WillTopic`)** | `synaptix/device/status` | Dedicated device health topic, e.g., `factory/line1/node01/status`. |
| **Will Payload (`WillPayload`)** | `offline` | Plain text offline state string (max 30 characters). Plain text (`offline`, `0`, `lost`) is strongly recommended over JSON to avoid double-quote `"` syntax errors in Wi-Fi module AT parsers. |
| **Will QoS (`WillQos`)** | `QoS 1` | `QoS 1` (At Least Once) guarantees SCADA/Cloud dashboards receive the fault notification. |
| **Will Retain (`WillRetain`)** | `true` | `true` ensures newly opened HMI/Web dashboards immediately see whether the node is currently online or offline. |

---

### 1.3 X.509 Server TLS & Mutual TLS (`mTLS`) Provisioning

To comply with **IEC 62443-4-2** cryptographic transport requirements, SynaptiX IDE includes an integrated X.509 certificate inspector and chunked provisioning engine supporting both **One-Way Server TLS** and **Two-Way Mutual TLS (`mTLS`)**:

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>STEP 1</span>
      </div>
      <span class="synx-node-status">X.509 PARSE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Parse X.509</span>
        <span class="synx-node-alias">Validate Subject</span>
        <span class="synx-node-val">SHA-256 Digest</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Flash</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,13 L5,13 L5,9.5 L8,9.5 L8,6 L11,6 L11,3 L14,3" /></svg>
        <span>STEP 2</span>
      </div>
      <span class="synx-node-status">TRANSFER</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">IDE ➔ Device</span>
        <span class="synx-node-alias">Chunked Key Write</span>
        <span class="synx-node-val">CA + Client Cert + Key</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">:8883</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M10,1 L5,8.5 L8.5,8.5 L6,15 L11,7.5 L7.5,7.5 Z" /></svg>
        <span>STEP 3</span>
      </div>
      <span class="synx-node-status">TLS 1.3</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">TLS Handshake</span>
        <span class="synx-node-alias">Port 8883 MQTTS</span>
        <span class="synx-node-val">TCP SYN + ClientHello</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">mTLS</span>
  </div>

  <div class="synx-canvas-node is-step">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 L13.5,4 L13.5,9.5 Q13.5,13.5 8,15 Q2.5,13.5 2.5,9.5 L2.5,4 Z" /></svg>
        <span>STEP 4</span>
      </div>
      <span class="synx-node-status">MUTUAL</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">mTLS Verify</span>
        <span class="synx-node-alias">Mutual Authentication</span>
        <span class="synx-node-val">Verify CA &amp; Sign</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Connack</span>
  </div>

  <div class="synx-canvas-node is-step is-done">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>STEP 5</span>
      </div>
      <span class="synx-node-status is-active">● CONNECTED</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Encrypted Session</span>
        <span class="synx-node-alias">MQTT CONNACK</span>
        <span class="synx-node-val">Session OK (0x00)</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

1. **Root CA Certificate (`BrowseCaCert`):**
   - Load `.crt`, `.cer`, or `.pem` Root CA files (e.g., *Amazon Root CA 1*, *DigiCert Global Root G2*, or your internal plant PKI Root CA).
   - SynaptiX IDE automatically parses and displays the certificate **Subject**, **Issuer**, **Expiration Timestamp**, and **SHA-256 Fingerprint** so you can verify authenticity before deployment.
2. **Mutual TLS Client Certificate & Private Key (`UseMtls`):**
   - Required by cloud IoT hubs such as **AWS IoT Core**, **Azure Event Grid MQTT**, and hardened **EMQX Enterprise** clusters.
   - Supports either separate PEM files (**Client Certificate** `.crt`/`.pem` + **Private Key** `.key`/`.pem`) or a password-protected **PKCS#12 bundle** (`.pfx` / `.p12`).
3. **Advanced TLS Negotiation Options:**
   - **TLS Protocol Version:** `Auto (TLS 1.2 / 1.3)` (default), `TLS 1.2 Only`, or `TLS 1.3 Only`.
   - **SNI Hostname Override (`SniHostname`):** Custom Server Name Indication string required by multi-tenant cloud load balancers (such as AWS IoT ALPN/SNI endpoints) when connecting via internal proxy IPs.
   - **Ignore Hostname Mismatch (`IgnoreHostname`):** Permits LAN IP connections (e.g., `192.168.10.50`) when the on-premise broker certificate's Subject Alternative Name (`SAN`) only lists an internal DNS name.
   - **Allow Self-Signed (`AllowSelfSigned`):** Lab/commissioning convenience flag; **disable in production** in favor of pinning your plant Root CA.

::: tip Live TLS Handshake Test (`Test Connection`)
Click **Test Connection** directly inside the `Ctrl+Shift+M` dialog before deploying to hardware. SynaptiX IDE executes a real TCP + `SslStream` X.509 handshake (including mTLS client certificate authentication and `6 s` timeout verification) from your workstation and reports the negotiated TLS version and cipher algorithm immediately!
:::

### 1.4 Automated Smart Sync During Deploy (CRC-16 Checksum Verification)

To guarantee a unified engineering experience without fragmented menus, SynaptiX IDE **avoids standalone certificate download buttons**. All credential staging is fully automated within the single **Deploy (`F5`)** workflow:
* **Hardware Checksum Verification (`0x0F43..0x0F46`):** Prior to staging, Studio reads the CRC-16 checksums of the certificates currently stored in the MCU's flash memory.
* **Deploy Speed Optimization:** If all required certificates on the device match the local project files with 100% integrity, Studio skips chunk transmission entirely. This preserves flash write endurance and ensures routine logic deploys remain blazing fast (<100ms).
* **Automated Provisioning:** When certificates are renewed or deployed onto a fresh device, Studio splits them into 128-byte `SPLC_CertChunk_t` structs (`0x0F00`), validates chunk ACKs (`0x0F40`), and executes an atomic commit (`0x0F42 = 0xA5A5`).

---

## Part 2: Wi-Fi Provisioning & Live Scanner (`Ctrl+Shift+W`)

For wireless-capable SynaptiX controllers and DTU gateways, press `Ctrl+Shift+W` (or select **Device -> Wi-Fi Network Configuration...**) to configure 2.4 GHz IEEE 802.11b/g/n **Station (`STA`)** networking and monitor live RF link quality.

### 2.1 Wireless Station & Auto-Reconnect Configuration

| Setting | Default | Limit / Behavior | Engineering Recommendation |
| :--- | :--- | :--- | :--- |
| **Enable Wi-Fi (`IsEnabled`)** | `Disabled` | `Enabled` / `Disabled` | Activates the onboard wireless subsystem in Station (`STA`) mode. |
| **Auto-Reconnect (`AutoReconnect`)** | `Enabled` (`true`) | Exponential backoff retry | Keep **Enabled** so the device automatically re-associates after plant AP reboots or temporary RF interference. |
| **Network SSID (`Ssid`)** | *(Empty)* | Up to `32` UTF-8 bytes | Type manually (for hidden industrial SSIDs) or populate with one click from the **Live Access Point Scanner**. |
| **Security Password (`Password`)** | *(Empty)* | Up to `64` ASCII chars | Supports Open, WPA-PSK, WPA2-PSK, WPA/WPA2-Mixed, and WPA3-SAE personal pre-shared keys. Includes eye-toggle visibility inspection. |

---

### 2.2 Live Access Point Scanner (`AvailableAps`)

When SynaptiX IDE is connected online (`F4`) to a physical device over USB CDC or Ethernet, switch to **Tab 2: Scan Networks** (or click **Scan Wi-Fi**):

1. The IDE instructs the connected SynaptiX device's own onboard RF radio—**not** your laptop's Wi-Fi card—to perform an active site survey at the exact cabinet installation point (up to `8` strongest nearby Access Points).
2. Within `<= 5 seconds`, the `AvailableAps` table lists each discovered AP's **SSID**, **Security Mode** (`OPEN`, `WPA2-PSK`, `WPA3-PSK`), **Channel**, **RSSI (`dBm`)**, and normalized **Signal Quality (`%`)**.
3. Selecting any row in `AvailableAps` and clicking **Apply Selected AP** transfers the exact SSID into Tab 1, eliminating case-sensitive typos.

### 2.3 Signal Strength (`RSSI` & Quality `%`) Commissioning Guide

SynaptiX IDE computes normalized **Signal Quality (`0% .. 100%`)** from raw `RSSI (dBm)` using the linear industrial mapping `Quality% = clamp(2 * (RSSI + 100), 0, 100)`:

| RSSI Range (`dBm`) | Signal Quality (`%`) | RF Link Assessment & Cabinet Action |
| :--- | :--- | :--- |
| `>= -50 dBm` | `100%` | **Excellent:** Line-of-sight to industrial AP; ideal for high-frequency `MQTT_PUB` / `Modbus TCP`. |
| `-65 .. -51 dBm` | `70% .. 98%` | **Good (Recommended):** Reliable production operation with ample fade margin. |
| `-75 .. -66 dBm` | `50% .. 68%` | **Fair / Marginal:** Susceptible to packet retries near heavy VFD cabinets. Route an external SMA antenna outside metal enclosures! |
| `<= -76 dBm` | `<= 48%` | **Weak / Unstable:** High risk of TCP/TLS handshake timeouts. Add an industrial AP repeater or switch to wired Ethernet / RS-485. |

---

### 2.4 DHCP vs. Static IPv4 Addressing & Live Link Telemetry

| IPv4 Mode | Fields Configured | When to Choose |
| :--- | :--- | :--- |
| **DHCP (Automatic)** *(Default)* | Automatically acquires IP, Subnet Mask, Gateway, and DNS via router DHCP lease. | Cloud-only IIoT telemetry (`MQTT_PUB`, `HTTP_CLIENT`) where outbound connections initiate from the device. |
| **Static IPv4** | **Static IP** (`192.168.1.50`), **Subnet Mask** (`255.255.255.0`), **Default Gateway** (`192.168.1.1`), **Primary DNS** (`8.8.8.8`). | **Mandatory** whenever SCADA/HMI polls the device via **Modbus TCP (Port `502`)** or **TCP Server** mode so the device IP never drifts across DHCP renewals. |

While connected online (`IsDeviceConnected == true`), the dialog's **Live Telemetry Strip** refreshes every `1.5 seconds` to display:
- **Connection Status State:** `Disconnected`, `Connecting...`, `Connected`, `Got IP`, `Authentication Failed`, or `SSID Not Found`.
- **Active IPv4 Address (`CurrentIp`)** & **Hardware MAC Address (`MacAddress`)** (useful for IT MAC-whitelist registration on factory VLANs).
- **Instant Actions:** Test **Connect Now** or **Disconnect** interactively before clicking **Save** to persist settings into both the project metadata and non-volatile device configuration.

---

## Related Documentation

- [MQTT_PUB — Telemetry Publisher Block](/blocks/mqtt-pub)
- [MQTT_SUB — Inbound JSON Command Subscriber](/blocks/mqtt-sub)
- [DTU Gateway Studio (`Ctrl+Shift+D`)](/tools/dtu-gateway-studio)
- [Error Codes & Commissioning Troubleshooting](/appendices/error-codes-and-troubleshooting)
