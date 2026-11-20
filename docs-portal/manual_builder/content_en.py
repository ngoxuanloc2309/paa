# -*- coding: utf-8 -*-
"""
SynaptiX Industrial User Manual - English Curated Content
Curated for high technical density, concise industrial readability, and strict IEC/IEEE standards.
Target page count: ~55-60 pages A4.
"""

import os

PUBLIC_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "public"))
IMG_DIR = os.path.abspath(os.path.join(PUBLIC_DIR, "images")).replace("\\", "/")

def img(name: str) -> str:
    return f"file:///{IMG_DIR}/{name}"

CHAPTERS_EN = [
    {
        "id": "ch1",
        "num": 1,
        "title": "Chapter 1: SynaptiX IIoT Ecosystem & Core Architecture",
        "sections": [
            ("1.1", "Platform Overview & SynaptiX IIoT Device Portfolio"),
            ("1.2", "Unified Tag Memory Architecture & Industrial Data Types"),
            ("1.3", "Deterministic Execution Pipeline & Zero-Allocation Hot Path"),
        ],
        "content": f"""
        <section class="section-container" id="sec-1-1">
          <h2 class="section-title"><span class="sec-num">1.1</span> Platform Overview &amp; SynaptiX IIoT Device Portfolio</h2>
          <p><strong>SynaptiX IDE</strong> is the unified engineering software suite engineered by <strong>Synaptix Technology Joint Stock Company</strong> (<code>synaptix-io.com</code>) for Function Block Diagram (FBD) logic programming, industrial fieldbus provisioning, and real-time edge management. Fully compliant with <strong>IEC 61131-3</strong>, the platform bridges real-time microcontroller determinism with enterprise cloud analytics under the overarching corporate doctrine: <em>"Edge &bull; Gateway &bull; Cloud &bull; AI"</em>.</p>

          <div class="table-caption">Table 1.1: SynaptiX IIoT Hardware Portfolio &amp; Engineering Specifications</div>
          <table>
            <thead>
              <tr>
                <th style="width: 22%;">Device Family</th>
                <th style="width: 20%;">Model Code</th>
                <th style="width: 33%;">Connectivity &amp; Processing</th>
                <th style="width: 25%;">Target Industrial Mission</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>Data Logger</strong></td>
                <td><code>miniLog MLV2</code></td>
                <td>Non-volatile onboard Flash storage, 4G LTE Cat-1, RS-485 Modbus, battery RTC.</td>
                <td>Environmental monitoring, water flow totalization, weather telemetry stations.</td>
              </tr>
              <tr>
                <td><strong>Remote I/O</strong></td>
                <td><code>XL-WFIO-44</code></td>
                <td>4 Digital Inputs, 4 Relay Outputs, 2.4GHz Industrial Wi-Fi, Modbus TCP.</td>
                <td>Remote pump/fan sequencing, electrical cabinet auxiliary feedback monitoring.</td>
              </tr>
              <tr>
                <td><strong>Industrial Gateway</strong></td>
                <td><code>XLink Gateway</code></td>
                <td>Multiprotocol: LoRa 433/868MHz, Zigbee 3.0 Mesh, RS-485, 100M Ethernet LAN.</td>
                <td>Wireless plant sensor mesh collection, Modbus RTU-to-MQTT cloud translation.</td>
              </tr>
              <tr>
                <td><strong>HMI Smart Display</strong></td>
                <td><code>HMI 7-inch CM4</code></td>
                <td>7-inch industrial touchscreen, Raspberry Pi CM4 Edge AI co-processor, dual LAN.</td>
                <td>On-machine CNC dashboard, packaging line OEE tracking and predictive alerts.</td>
              </tr>
              <tr>
                <td><strong>Edge Controller</strong></td>
                <td><code>SYNX-CTRL-884</code></td>
                <td>8 DI, 8 DO, 4 AI (4-20mA), dual isolated RS-485 ports, ARM Cortex-M4 core.</td>
                <td>Factory automation logic, deterministic multi-channel interlock safety circuits.</td>
              </tr>
            </tbody>
          </table>

          <div class="callout callout-info">
            <div class="callout-header">ℹ️ UNIFIED OPEN ECOSYSTEM</div>
            All physical hardware devices in the portfolio share the exact same instruction set, identical memory layouts, and are programmed directly from the single <strong>SynaptiX IDE</strong> engineering suite.
          </div>
        </section>

        <section class="section-container" id="sec-1-2">
          <h2 class="section-title"><span class="sec-num">1.2</span> Unified Tag Memory Architecture &amp; Industrial Data Types</h2>
          <p>Adhering to the foundational <strong>Hardware Ground Truth Doctrine</strong>, data representations in SynaptiX are not cosmetic labels; they are <strong>immutable physical memory contracts</strong> directly mirroring hardware registers in MCU RAM and Flash, completely eliminating memory fragmentation:</p>

          <div class="table-caption">Table 1.2: Physical Tag Memory Spaces &amp; Register Allocation</div>
          <table>
            <thead>
              <tr>
                <th style="width: 15%;">Tag Partition</th>
                <th style="width: 15%;">Data Type</th>
                <th style="width: 12%;">Access</th>
                <th style="width: 28%;">Capacity &amp; Boundary Limits</th>
                <th style="width: 30%;">Engineering Functionality</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><code>DI0 .. DI7</code></td>
                <td><code>BOOL</code> (1-bit)</td>
                <td>Read-Only (RO)</td>
                <td>8 Opto-isolated digital inputs</td>
                <td>Pushbuttons, limit switches, optical sensors, safety E-Stop contacts.</td>
              </tr>
              <tr>
                <td><code>DO0 .. DO7</code></td>
                <td><code>BOOL</code> (1-bit)</td>
                <td>Read/Write (RW)</td>
                <td>8 Relay or NPN transistor outputs</td>
                <td>Motor contactors, pneumatic solenoid valves, tower beacons, sirens.</td>
              </tr>
              <tr>
                <td><code>AI0 .. AI3</code></td>
                <td><code>REAL</code> (Float32)</td>
                <td>Read-Only (RO)</td>
                <td>4 isolated 12-bit ADC channels (0-10V / 4-20mA)</td>
                <td>Pressure transmitters, flow meters, PT100 temperature probes, tank levels.</td>
              </tr>
              <tr>
                <td><code>VFLAG0 .. 31</code></td>
                <td><code>BOOL</code> (1-bit)</td>
                <td>Read/Write (RW)</td>
                <td>32 internal RAM virtual flags</td>
                <td>Intermediate logic interlocks, Auto/Manual supervisory flags.</td>
              </tr>
              <tr>
                <td><code>VREG0 .. 31</code></td>
                <td><code>INT32 / REAL</code></td>
                <td>Read/Write (RW)</td>
                <td>32 general-purpose 32-bit registers</td>
                <td>Threshold values, timer setpoints, arithmetic accumulator results.</td>
              </tr>
              <tr>
                <td><code>VREG_RETAIN</code></td>
                <td><code>INT32 / REAL</code></td>
                <td>Read/Write (RW)</td>
                <td>16 non-volatile Flash-backed registers</td>
                <td>Calibration offsets, shift production totals, cumulative volume records.</td>
              </tr>
              <tr>
                <td><code>COUNTER0 .. 7</code></td>
                <td><code>INT32</code> (32-bit)</td>
                <td>Read/Write (RW)</td>
                <td>8 dedicated 32-bit production counters</td>
                <td>Packaging output counters, stamping stroke logs, high-speed pulse inputs.</td>
              </tr>
            </tbody>
          </table>

          <div class="callout callout-warning">
            <div class="callout-header">⚠️ PROHIBITION OF WRITES TO READ-ONLY MEMORY</div>
            Partitions <code>DI</code> and <code>AI</code> are physical read-only inputs. Any write action (via ACTION blocks or Modbus SCADA Write commands) targeting these addresses is rejected at compilation time with error <code>ERR_ACTION_READONLY_TARGET</code>.
          </div>
        </section>

        <section class="section-container" id="sec-1-3">
          <h2 class="section-title"><span class="sec-num">1.3</span> Deterministic Execution Pipeline &amp; Zero-Allocation Hot Path</h2>
          <p>Every scan cycle within the SynaptiX runtime operates under a <strong>100% deterministic 4-phase linear pipeline</strong>, eliminating race conditions and ensuring zero heap allocation during the recurring scan loop:</p>

          <ol>
            <li><strong>Phase 1 &mdash; Input Image Snapshot:</strong> The controller samples physical pins <code>DI0..DI7</code>, samples 12-bit ADCs <code>AI0..AI3</code>, and latches queued Modbus/Ethernet buffers into memory image tables.</li>
            <li><strong>Phase 2 &mdash; Topological Logic Evaluation:</strong> FBD blocks and declarative rules are evaluated in strict topological order. <code>GUARD</code> gates verify interlock permissions before downstream <code>ACTION</code> blocks can trigger.</li>
            <li><strong>Phase 3 &mdash; Memory State Commit:</strong> Intermediate arithmetic results from <code>MATH</code> blocks, elapsed timer values <code>TON</code>, and counter states are atomically committed into RAM.</li>
            <li><strong>Phase 4 &mdash; Output Flush &amp; Telemetry:</strong> Physical output latches <code>DO0..DO7</code> are updated synchronously, and scheduled MQTT/Modbus telemetry frames are dispatched.</li>
          </ol>
        </section>
        """
    },
    {
        "id": "ch2",
        "num": 2,
        "title": "Chapter 2: Software Setup & Device Connectivity",
        "sections": [
            ("2.1", "System Requirements & 5-Step Inno Setup Installation"),
            ("2.2", "USB CDC Cable Connection & Virtual COM Port Setup"),
            ("2.3", "Industrial Ethernet / Modbus TCP Networking & Static IP"),
        ],
        "content": f"""
        <section class="section-container" id="sec-2-1">
          <h2 class="section-title"><span class="sec-num">2.1</span> System Requirements &amp; 5-Step Inno Setup Installation</h2>
          <p>SynaptiX IDE is distributed as a hardened corporate installer engineered with <strong>Inno Setup 6</strong> and <strong>LZMA2 ultra solid compression</strong>. It bundles a self-contained Microsoft .NET 8 desktop runtime and the hardware emulator utility <code>Tools/SimplePLC.McuEmulator.exe</code>.</p>

          <div class="table-caption">Table 2.1: Workstation Hardware &amp; OS Requirements</div>
          <table>
            <thead>
              <tr>
                <th style="width: 25%;">Hardware Component</th>
                <th style="width: 35%;">Minimum Specification</th>
                <th style="width: 40%;">Recommended Industrial Workstation</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>Operating System</strong></td>
                <td>Windows 10 64-bit (Build 1809 or higher)</td>
                <td>Windows 11 Pro 64-bit / Windows Server 2022</td>
              </tr>
              <tr>
                <td><strong>Processor (CPU)</strong></td>
                <td>Intel Core i3 / AMD Ryzen 3 (2.0 GHz)</td>
                <td>Intel Core i5 / i7 11th Gen+ (4 cores, 3.2 GHz+)</td>
              </tr>
              <tr>
                <td><strong>System RAM</strong></td>
                <td>4 GB available RAM</td>
                <td>8 GB &ndash; 16 GB DDR4/DDR5</td>
              </tr>
              <tr>
                <td><strong>Disk Storage</strong></td>
                <td>1.5 GB free disk space (SSD)</td>
                <td>NVMe High-Speed SSD (&lt; 2s project load latency)</td>
              </tr>
              <tr>
                <td><strong>Field Interfaces</strong></td>
                <td>1x USB 2.0 / USB-C data port</td>
                <td>2x USB-C ports, 1x RJ45 100/1000M Ethernet port</td>
              </tr>
            </tbody>
          </table>

          <div class="figure-container">
            <img src="{img('synaptix_setup_step1_welcome.png')}" alt="SynaptiX IDE Setup Welcome Wizard">
            <div class="figure-caption">Figure 2.1: Step 1 &ndash; Standard Inno Setup 6 Welcome Screen</div>
          </div>

          <div class="figure-container">
            <img src="{img('synaptix_setup_step2_license.png')}" alt="Software License Agreement EULA">
            <div class="figure-caption">Figure 2.2: Step 2 &ndash; End-User Software License Agreement (EULA)</div>
          </div>

          <div class="figure-container">
            <img src="{img('synaptix_setup_step3_directory.png')}" alt="Select Installation Directory">
            <div class="figure-caption">Figure 2.3: Step 3 &ndash; Selecting Protected Enterprise Directory (%ProgramFiles%\\Synaptix Automation)</div>
          </div>

          <div class="figure-container">
            <img src="{img('synaptix_setup_step4_tasks.png')}" alt="Select Additional Tasks">
            <div class="figure-caption">Figure 2.4: Step 4 &ndash; Project File Association (.splc) &amp; McuEmulator Registration</div>
          </div>

          <div class="figure-container">
            <img src="{img('synaptix_setup_step5_finish.png')}" alt="Complete Setup Installation">
            <div class="figure-caption">Figure 2.5: Step 5 &ndash; Installation Complete with Launch Option</div>
          </div>
        </section>

        <section class="section-container" id="sec-2-2">
          <h2 class="section-title"><span class="sec-num">2.2</span> USB CDC Cable Connection &amp; Virtual COM Port Setup</h2>
          <p>To configure and flash logic directly from your engineering workstation, connect the controller's USB-C port to your computer:</p>
          <ol>
            <li>Insert the USB-C cable. The device initializes in <strong>USB CDC (Communications Device Class)</strong> mode. Windows automatically binds the native Virtual COM Port driver without requiring third-party installation.</li>
            <li>Press <code>F4</code> (or click the <strong>Connect Device</strong> toolbar button).</li>
            <li>Select the identified device port (e.g., <code>COM3 &bull; SynaptiX Controller (STMicroelectronics Virtual COM Port)</code>) with default baud rate <code>115200 bps, 8N1</code>.</li>
            <li>Click <strong>Connect</strong>. The status bar immediately transitions to solid teal: <code>CONNECTED [COM3 &bull; 115200]</code>.</li>
          </ol>

          <div class="callout callout-danger">
            <div class="callout-header">🛑 2-WIRE CHARGING CABLE HAZARD</div>
            Over 60% of field connection failures result from utilizing consumer smartphone charging cables that lack the differential data pair (<code>D+/D-</code>). Always utilize the verified high-grade USB-C data cable provided in the official hardware packaging.
          </div>
        </section>

        <section class="section-container" id="sec-2-3">
          <h2 class="section-title"><span class="sec-num">2.3</span> Industrial Ethernet / Modbus TCP Networking &amp; Static IP</h2>
          <p>For permanent panel installations involving <code>miniLog MLV2</code>, <code>XLink Gateway</code>, or <code>SYNX-CTRL-884</code>, direct RJ45 Ethernet communication provides superior industrial noise immunity:</p>
          <ul>
            <li>Default factory static IP: <code>192.168.1.200</code>, Subnet: <code>255.255.255.0</code>, Modbus TCP Port: <code>502</code>.</li>
            <li>Assign your engineering workstation an address within the same subnet (e.g., <code>192.168.1.50</code>).</li>
            <li>Open the <code>F4</code> connection dialog, select the <strong>Ethernet / Modbus TCP</strong> tab, enter IP <code>192.168.1.200</code> and Port <code>502</code>, then click <strong>Connect</strong>.</li>
          </ul>
        </section>
        """
    },
    {
        "id": "ch3",
        "num": 3,
        "title": "Chapter 3: The 7 Engineering Workspaces",
        "sections": [
            ("3.1", "Workspace Architecture & Navigation Hotkeys"),
            ("3.2", "Device Workspace (Ctrl+1) & Visual Logic Graph Canvas (Ctrl+2)"),
            ("3.3", "Tag Catalog (Ctrl+3), Declarative Rules (Ctrl+4) & Deploy Gate (Ctrl+6)"),
        ],
        "content": f"""
        <section class="section-container" id="sec-3-1">
          <h2 class="section-title"><span class="sec-num">3.1</span> Workspace Architecture &amp; Navigation Hotkeys</h2>
          <p>SynaptiX IDE adheres strictly to the <strong>ANSI/ISA-101 HMI Ergonomics Standard</strong>, employing the <em>"90/10 Gray Principle"</em>: 90% neutral slate backgrounds to prevent eye fatigue across 12-hour shifts, reserving vivid colors strictly for operational states and safety hazards.</p>

          <div class="table-caption">Table 3.1: The 7 Core Engineering Workspaces &amp; Navigation Shortcuts</div>
          <table>
            <thead>
              <tr>
                <th style="width: 18%;">Workspace Tab</th>
                <th style="width: 14%;">Hotkey</th>
                <th style="width: 38%;">Core Engineering Capabilities</th>
                <th style="width: 30%;">Integrated Standards &amp; Tools</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>1. Device</strong></td>
                <td><code>Ctrl + 1</code></td>
                <td>Hardware diagnostic telemetry, MCU health, RTC clock synchronization, RS-485 port and Wi-Fi provisioning.</td>
                <td>Device Info Dialog, Wi-Fi RF Scanner, RTC Sync Tool.</td>
              </tr>
              <tr>
                <td><strong>2. Logic Graph</strong></td>
                <td><code>Ctrl + 2</code></td>
                <td>Visual FBD canvas, orthogonal wire routing, real-time wire energizing debug overlay (<code>F8</code>).</td>
                <td>IEC 61131-3 Canvas, Snap-to-Grid, Mini-map Navigator.</td>
              </tr>
              <tr>
                <td><strong>3. Tag Catalog</strong></td>
                <td><code>Ctrl + 3</code></td>
                <td>Global tag aliasing, physical engineering units, data typing, and Modbus holding register cross-mapping.</td>
                <td>IBM Carbon High-Density Table, Instant Filter.</td>
              </tr>
              <tr>
                <td><strong>4. Rule Table</strong></td>
                <td><code>Ctrl + 4</code></td>
                <td>Declarative spreadsheet view of all interlock rules (Trigger &rarr; Condition &rarr; Guard &rarr; Action).</td>
                <td>Logic auditability, active state rule filtering.</td>
              </tr>
              <tr>
                <td><strong>5. Blueprints</strong></td>
                <td><code>Ctrl + 5</code></td>
                <td>Industrial circuit library: Star-Delta starter, Duty-Standby pumps, 4-20mA scaling, Energy totalization.</td>
                <td>Pre-validated 1-click circuit drag-and-drop.</td>
              </tr>
              <tr>
                <td><strong>6. Deploy Gate</strong></td>
                <td><code>Ctrl + 6</code></td>
                <td>Pre-flight verification gate (<code>F7</code>), bytecode diff viewer, and atomic two-phase commit flash flashing.</td>
                <td>Two-Phase Commit fail-safe microcontroller deploy.</td>
              </tr>
              <tr>
                <td><strong>7. Live Watch</strong></td>
                <td><code>Ctrl + 7</code></td>
                <td>High-speed 20 FPS telemetry grid, multi-channel real-time trend charts, and secure timed variable forcing.</td>
                <td>Interactive Trend Charts, Safe Forcing Lease Timers.</td>
              </tr>
            </tbody>
          </table>

          <div class="figure-container">
            <img src="{img('synaptix_main_anatomy.png')}" alt="SynaptiX IDE Main Interface Anatomy">
            <div class="figure-caption">Figure 3.1: Complete Architectural Anatomy of SynaptiX IDE Workspaces</div>
          </div>
        </section>

        <section class="section-container" id="sec-3-2">
          <h2 class="section-title"><span class="sec-num">3.2</span> Device Workspace (Ctrl+1) &amp; Visual Logic Graph Canvas (Ctrl+2)</h2>
          <p>The <strong>Device Workspace (Ctrl+1)</strong> provides comprehensive hardware health diagnostics: active firmware build, 24V bus rail voltage, MCU core junction temperature, and 1-click RTC clock synchronization with network time.</p>
          <p>The <strong>Logic Graph Canvas (Ctrl+2)</strong> serves as the central design workspace. Drag function blocks from the left-hand palette onto the canvas and drag connecting wires between input and output pins. All wiring conforms to crisp orthogonal routing lines.</p>

          <div class="figure-container">
            <img src="{img('synaptix_logic_editor.png')}" alt="Logic Graph Visual Canvas">
            <div class="figure-caption">Figure 3.2: FBD Visual Design Canvas with Orthogonal Wire Routing</div>
          </div>
        </section>

        <section class="section-container" id="sec-3-3">
          <h2 class="section-title"><span class="sec-num">3.3</span> Tag Catalog (Ctrl+3), Declarative Rules (Ctrl+4) &amp; Deploy Gate (Ctrl+6)</h2>
          <p>In the <strong>Tag Catalog (Ctrl+3)</strong>, all physical I/O and virtual variables are centrally configured. Engineers can assign human-readable aliases (e.g., renaming <code>AI0</code> to <code>Tank_Temperature</code> with unit <code>°C</code>) which automatically updates across all canvas nodes.</p>
          <p>Prior to committing logic to hardware, the <strong>Deploy Gate (Ctrl+6)</strong> executes comprehensive pre-flight verification. If any disconnected inputs, read-only write violations, or potential zero-division errors are detected, deployment is strictly blocked.</p>

          <div class="figure-container">
            <img src="{img('synaptix_deploy_gate.png')}" alt="Deploy Gate Pre-flight Verification">
            <div class="figure-caption">Figure 3.3: Two-Phase Atomic Deployment Verification Gate</div>
          </div>
        </section>
        """
    },
    {
        "id": "ch4",
        "num": 4,
        "title": "Chapter 4: Visual FBD Logic Canvas Design",
        "sections": [
            ("4.1", "Drag-and-Drop Editing, Wire Routing & Type Safety"),
            ("4.2", "Core Interlock Pipeline: INPUT &rarr; TRIGGER &rarr; COMPARATOR &rarr; GUARD &rarr; ACTION"),
            ("4.3", "10-Minute Walkthrough: Tank Thermal Protection & Safety Interlock"),
        ],
        "content": f"""
        <section class="section-container" id="sec-4-1">
          <h2 class="section-title"><span class="sec-num">4.1</span> Drag-and-Drop Editing, Wire Routing &amp; Type Safety</h2>
          <p>Editing logic on the SynaptiX canvas conforms to three foundational principles:</p>
          <ul>
            <li><strong>Canvas Gestures:</strong> Click and drag from an output pin (orange/green) to an input pin. Hold <code>Space</code> + Left Drag to pan the canvas viewport. Scroll the mouse wheel to smoothly zoom from 25% to 200%.</li>
            <li><strong>Type-Safe Pin Binding:</strong> Discrete binary pins (<code>BOOL</code>) only connect to <code>BOOL</code> sockets; integer (<code>INT32</code>) and analog (<code>REAL</code>) pins cannot be accidentally attached to boolean gates without an intermediate <code>COMPARATOR</code> or <code>SCALE</code> block.</li>
            <li><strong>Editing:</strong> Select wires or blocks and press <code>Delete</code> to remove them. Double-click any node to open its dedicated parameter configuration dialog.</li>
          </ul>
        </section>

        <section class="section-container" id="sec-4-2">
          <h2 class="section-title"><span class="sec-num">4.2</span> Core Interlock Pipeline: INPUT &rarr; TRIGGER &rarr; COMPARATOR &rarr; GUARD &rarr; ACTION</h2>
          <p>All control sequences are organized into a clean <strong>5-Stage Pipeline</strong> that enforces complete architectural separation between signal acquisition, evaluation, safety gating, and actuation:</p>

          <div class="synx-node-chain">
            <div class="synx-canvas-node is-input">
              <div class="synx-node-header"><span>INPUT</span><span style="font-size: 5pt;">● SOURCE</span></div>
              <div class="synx-node-body">
                <span class="synx-node-tag">AI0 &bull; REAL</span>
                <span class="synx-node-alias">TankTemp</span>
                <span class="synx-node-val">72.4 °C</span>
              </div>
            </div>

            <div class="synx-node-wire">
              <div class="synx-wire-line"></div>
              <span class="synx-wire-badge">Analog</span>
            </div>

            <div class="synx-canvas-node is-comparator">
              <div class="synx-node-header"><span>COMPARATOR</span><span style="font-size: 5pt;">● EVAL</span></div>
              <div class="synx-node-body">
                <span class="synx-node-tag">&gt; 65.0 °C</span>
                <span class="synx-node-alias">High Limit</span>
                <span class="synx-node-val">TRUE</span>
              </div>
            </div>

            <div class="synx-node-wire">
              <div class="synx-wire-line"></div>
              <span class="synx-wire-badge">BOOL</span>
            </div>

            <div class="synx-canvas-node is-guard">
              <div class="synx-node-header"><span>GUARD</span><span style="font-size: 5pt;">● INTERLOCK</span></div>
              <div class="synx-node-body">
                <span class="synx-node-tag">DI0 == 1</span>
                <span class="synx-node-alias">Door Closed</span>
                <span class="synx-node-val">PASS</span>
              </div>
            </div>

            <div class="synx-node-wire">
              <div class="synx-wire-line"></div>
              <span class="synx-wire-badge">PERMIT</span>
            </div>

            <div class="synx-canvas-node is-action">
              <div class="synx-node-header"><span>ACTION</span><span style="font-size: 5pt;">● EXEC</span></div>
              <div class="synx-node-body">
                <span class="synx-node-tag">DO0 = 1</span>
                <span class="synx-node-alias">Cooling Fan</span>
                <span class="synx-node-val">ACTIVE</span>
              </div>
            </div>
          </div>
          <div class="figure-caption">Figure 4.1: Standard Industrial 5-Stage Interlock Chain Architecture</div>
        </section>

        <section class="section-container" id="sec-4-3">
          <h2 class="section-title"><span class="sec-num">4.3</span> 10-Minute Walkthrough: Tank Thermal Protection &amp; Safety Interlock</h2>
          <p>Mission: Activate cooling fan <code>DO0</code> when tank temperature <code>AI0</code> exceeds <code>65.0 °C</code> AND the safety door contact <code>DI0</code> is securely latched. Turn off the fan when temperature drops below <code>60.0 °C</code> (5°C hysteresis band) or if the safety door is breached (<code>DI0 == 0</code>).</p>

          <ol>
            <li><strong>Create Project:</strong> Press <code>Ctrl+N</code>, name the project <code>Tank_Cooling_Safety</code>, target hardware <code>SYNX-CTRL-884</code>.</li>
            <li><strong>Place INPUT Block:</strong> Drop an <code>INPUT</code> block, select <code>AI0</code>, set alias <code>Tank_Temp</code>.</li>
            <li><strong>Place COMPARATOR Block:</strong> Select operator <code>&gt; Greater Than</code> with threshold <code>65.0</code>. Connect the <code>INPUT</code> output to the <code>COMPARATOR</code> input.</li>
            <li><strong>Place GUARD Block:</strong> Set interlock tag to <code>DI0</code> (Safety Door). Connect <code>COMPARATOR</code> output to <code>GUARD</code> input.</li>
            <li><strong>Place ACTION Block:</strong> Set command to <code>Set 1</code> targeting <code>DO0</code> (Cooling Fan). Connect <code>GUARD</code> output to <code>ACTION</code> exec pin.</li>
            <li><strong>Simulate:</strong> Press <code>F7</code> to build, then press <code>F5</code> to open the offline simulator and test input combinations!</li>
          </ol>
        </section>
        """
    },
    {
        "id": "ch5",
        "num": 5,
        "title": "Chapter 5: Control & Math Function Block Library",
        "sections": [
            ("5.1", "Standard Timers: TON, TOF, TP & CYCLE_TIMER"),
            ("5.2", "Production Counters: CTU & CTD"),
            ("5.3", "Linear Sensor Calibration: SCALE"),
            ("5.4", "Arithmetic ALU Operations: MATH Unit"),
        ],
        "content": f"""
        <section class="section-container" id="sec-5-1">
          <h2 class="section-title"><span class="sec-num">5.1</span> Standard Timers: TON, TOF, TP &amp; CYCLE_TIMER</h2>
          <p>Timers in SynaptiX execute on high-precision monotonic clock counters with 1-millisecond resolution, immune to system processing load:</p>

          <div class="table-caption">Table 5.1: IEC 61131-3 Industrial Timer Block Specifications</div>
          <table class="spec-table-2col">
            <thead><tr><th style="width: 25%;">Function Block</th><th>Operating Behavior &amp; Pin Architecture</th></tr></thead>
            <tbody>
              <tr>
                <td class="col-pin"><strong>TON</strong><br/>(On-Delay Timer)</td>
                <td class="col-desc">
                  <strong>On-Delay:</strong> When input <code>IN = 1</code>, elapsed timer <code>ET</code> increments from 0. When <code>ET &gt;= PT</code> (preset time), output <code>Q</code> asserts high (<code>1</code>). If <code>IN</code> drops to <code>0</code> at any moment, <code>ET</code> resets immediately to 0 and <code>Q = 0</code>.<br/>
                  <em>Applications:</em> Staggered pump starting to prevent grid inrush current, contact debounce.
                </td>
              </tr>
              <tr>
                <td class="col-pin"><strong>TOF</strong><br/>(Off-Delay Timer)</td>
                <td class="col-desc">
                  <strong>Off-Delay:</strong> When <code>IN = 1</code>, <code>Q = 1</code> immediately. When <code>IN</code> transitions to <code>0</code>, <code>Q</code> remains held high for duration <code>PT</code> before de-asserting to <code>0</code>.<br/>
                  <em>Applications:</em> Industrial exhaust fan purge running 60 seconds after furnace shutdown.
                </td>
              </tr>
              <tr>
                <td class="col-pin"><strong>TP</strong><br/>(Pulse Timer)</td>
                <td class="col-desc">
                  <strong>Monostable Pulse:</strong> On rising edge of <code>IN</code>, output <code>Q</code> asserts high for exact duration <code>PT</code> regardless of whether <code>IN</code> remains held or drops prematurely.<br/>
                  <em>Applications:</em> Baghouse dust collector reverse-pulse jet firing, pneumatic ejectors.
                </td>
              </tr>
              <tr>
                <td class="col-pin"><strong>CYCLE_TIMER</strong><br/>(Asymmetric Osc)</td>
                <td class="col-desc">
                  <strong>Asymmetric Flasher:</strong> Generates continuous periodic pulses with independently configured on-time (<code>T_ON</code>) and off-time (<code>T_OFF</code>) without requiring recursive dual-timer loops.<br/>
                  <em>Applications:</em> Warning strobe beacons (500ms ON / 1500ms OFF), aeration cycles.
                </td>
              </tr>
            </tbody>
          </table>
        </section>

        <section class="section-container" id="sec-5-2">
          <h2 class="section-title"><span class="sec-num">5.2</span> Production Counters: CTU &amp; CTD</h2>
          <p>Counter blocks detect false-to-true transitions (<code>0 &rarr; 1</code>) on clock inputs <code>CU</code> or <code>CD</code>:</p>
          <ul>
            <li><strong>CTU (Count Up):</strong> Increments current count <code>CV</code> by 1 on each rising edge of <code>CU</code>. When <code>CV &gt;= PV</code> (target preset), output <code>Q = 1</code>. Pulsing <code>Reset = 1</code> clears <code>CV</code> back to 0.</li>
            <li><strong>CTD (Count Down):</strong> Decrements <code>CV</code> by 1 on each rising edge of <code>CD</code>. When <code>CV &lt;= 0</code>, output <code>Q = 1</code>. Pulsing <code>Load = 1</code> reloads preset <code>PV</code>.</li>
          </ul>
        </section>

        <section class="section-container" id="sec-5-3">
          <h2 class="section-title"><span class="sec-num">5.3</span> Linear Sensor Calibration: SCALE</h2>
          <p>The <strong>SCALE</strong> block performs deterministic 2-point linear scaling from raw sensor measurements (ADC counts or 4-20mA values) to physical engineering units:</p>
          <div style="text-align: center; margin: 2mm 0; font-weight: bold; color: #004f71;">
            OUT = OutMin + [ (IN - InMin) / (InMax - InMin) ] &times; (OutMax - OutMin)
          </div>
          <p>Built-in clamp protection guarantees that if the input signal exceeds <code>[InMin, InMax]</code> due to wiring faults, the output <code>OUT</code> clamps safely at <code>OutMin</code> or <code>OutMax</code> rather than causing mathematical overflow.</p>
        </section>

        <section class="section-container" id="sec-5-4">
          <h2 class="section-title"><span class="sec-num">5.4</span> Arithmetic ALU Operations: MATH Unit</h2>
          <p>The <strong>MATH</strong> block executes combinational arithmetic on inputs <code>IN1</code> and <code>IN2</code> supporting both 2's complement integers (<code>INT32</code>) and IEEE 754 single-precision floats (<code>REAL</code>):</p>

          <div class="table-caption">Table 5.2: MATH Block ALU Operators &amp; Overflow Safeguards</div>
          <table>
            <thead>
              <tr>
                <th style="width: 15%;">Operator</th>
                <th style="width: 25%;">Mathematical Operation</th>
                <th style="width: 30%;">Overflow &amp; Zero-Divide Defense</th>
                <th style="width: 30%;">Real-World Field Example</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><code>ADD</code></td>
                <td><code>OUT = IN1 + IN2</code></td>
                <td>Clamped at INT_MAX / REAL_MAX</td>
                <td>Totalizing flow rates from two parallel boiler feed lines.</td>
              </tr>
              <tr>
                <td><code>SUB</code></td>
                <td><code>OUT = IN1 - IN2</code></td>
                <td>Clamped at INT_MIN / REAL_MIN</td>
                <td>Tare weight deduction on bulk conveyor bagging scales.</td>
              </tr>
              <tr>
                <td><code>MUL</code></td>
                <td><code>OUT = IN1 * IN2</code></td>
                <td>Internal 64-bit precision accumulator</td>
                <td>Instantaneous active power calculation (<code>P = V * I</code>).</td>
              </tr>
              <tr>
                <td><code>DIV</code></td>
                <td><code>OUT = IN1 / IN2</code></td>
                <td>Asserts <code>0xE001</code> flag on zero denominator</td>
                <td>Average production line velocity (Units / Elapsed Time).</td>
              </tr>
              <tr>
                <td><code>MOD</code></td>
                <td><code>OUT = IN1 % IN2</code></td>
                <td>Integer modulus remainder (INT32 only)</td>
                <td>Carton batching indexing (12 units per packing box).</td>
              </tr>
              <tr>
                <td><code>ABS</code></td>
                <td><code>OUT = |IN1|</code></td>
                <td>Absolute non-negative magnitude</td>
                <td>Differential pressure magnitude between cleanroom airlocks.</td>
              </tr>
            </tbody>
          </table>

          <div class="figure-container">
            <img src="{img('synaptix_math_config_dialog.png')}" alt="MATH Configuration Dialog">
            <div class="figure-caption">Figure 5.1: MATH Block Configuration Dialog (Operator, Data Type, and Pin Sockets)</div>
          </div>

          <h3 class="subsection-title">Industrial Example 1: Boiler Feedwater Multi-Line Flow Totalizer (Operator ADD)</h3>
          <p>Collect instantaneous volumetric flow rates from two parallel boiler feedwater headers via 4-20mA analog transmitters: Line 1 connects to <code>AI0</code> (FlowLine1, m3/h) and Line 2 connects to <code>AI1</code> (FlowLine2, m3/h). The MATH block computes the total instantaneous consumption <code>TotalFlow = AI0 + AI1</code>, writing the sum to register <code>VREG0</code>. When cumulative flow exceeds <code>120.0 m3/h</code>, a relief valve <code>DO0</code> is automatically tripped.</p>

          <div class="synx-node-chain">
            <div class="synx-canvas-node is-input">
              <div class="synx-node-header"><span>INPUT</span><span style="font-size: 5pt;">● LINE 1</span></div>
              <div class="synx-node-body">
                <span class="synx-node-tag">AI0 &bull; REAL</span>
                <span class="synx-node-alias">FlowLine1</span>
                <span class="synx-node-val">58.4 m3/h</span>
              </div>
            </div>
            <div class="synx-node-wire"><div class="synx-wire-line"></div><span class="synx-wire-badge">IN1</span></div>
            <div class="synx-canvas-node is-math">
              <div class="synx-node-header"><span>MATH (ADD)</span><span style="font-size: 5pt;">● SUM</span></div>
              <div class="synx-node-body">
                <span class="synx-node-tag">IN1 + IN2</span>
                <span class="synx-node-alias">Totalizer</span>
                <span class="synx-node-val">122.9 m3/h</span>
              </div>
            </div>
            <div class="synx-node-wire"><div class="synx-wire-line"></div><span class="synx-wire-badge">OUT</span></div>
            <div class="synx-canvas-node is-comparator">
              <div class="synx-node-header"><span>COMPARATOR</span><span style="font-size: 5pt;">● THRESHOLD</span></div>
              <div class="synx-node-body">
                <span class="synx-node-tag">&gt; 120.0</span>
                <span class="synx-node-alias">High Limit</span>
                <span class="synx-node-val">TRUE</span>
              </div>
            </div>
            <div class="synx-node-wire"><div class="synx-wire-line"></div><span class="synx-wire-badge">EXEC</span></div>
            <div class="synx-canvas-node is-action">
              <div class="synx-node-header"><span>ACTION</span><span style="font-size: 5pt;">● VALVE</span></div>
              <div class="synx-node-body">
                <span class="synx-node-tag">DO0 = 1</span>
                <span class="synx-node-alias">Relief Valve</span>
                <span class="synx-node-val">OPEN</span>
              </div>
            </div>
          </div>
          <div class="figure-caption">Figure 5.2: Multi-Line Flow Totalization Circuit Schematic (MATH ADD Operator)</div>

          <h3 class="subsection-title">Industrial Example 2: Silo Bulk Bagging Tare Deduction &amp; Net Weight Cutoff (Operator SUB)</h3>
          <p>On an automated bulk powder bagging line, an analog loadcell returns total gross weight (bag plus material) at <code>VREG0</code> (GrossWeight, kg). Bag tare is statically configured in <code>VREG1</code> (TareWeight = 1.25 kg). The MATH block computes net powder weight <code>NetWeight = GrossWeight - TareWeight</code> with lower clamp protection, outputting to <code>VREG2</code>. When net weight reaches <code>50.0 kg</code>, an ACTION block de-energizes the pneumatic silo discharge gate <code>DO1</code>.</p>
        </section>
        """
    },
    {
        "id": "ch6",
        "num": 6,
        "title": "Chapter 6: Offline Simulation, Secure Deploy & Live Watch",
        "sections": [
            ("6.1", "Offline Simulation (F5) & Step-by-Step Scan (StepScan)"),
            ("6.2", "Pre-Flight Compiler Gate & Two-Phase Atomic Commit"),
            ("6.3", "Online Wire-Energizing Debug (F8) & [STALE] Fault Badges"),
            ("6.4", "Live Watch High-Density Grid & Timed Variable Forcing (Ctrl+7)"),
        ],
        "content": f"""
        <section class="section-container" id="sec-6-1">
          <h2 class="section-title"><span class="sec-num">6.1</span> Offline Simulation (F5) &amp; Step-by-Step Scan (StepScan)</h2>
          <p>Pressing <code>F5</code> activates the <strong>Offline Simulator</strong>. Executing on the exact same deterministic engine as the target ARM microcontroller, it allows 100% logic verification without physical hardware:</p>

          <div class="figure-container">
            <img src="{img('synaptix_simulator.png')}" alt="Cycle-Accurate Offline Simulator">
            <div class="figure-caption">Figure 6.1: Cycle-Accurate Offline Simulator (Virtual Signals, Clock Speed &amp; Trace)</div>
          </div>

          <ul>
            <li><strong>Continuous Execution / Pause:</strong> Run the virtual scan loop continuously or freeze time to inspect variable states.</li>
            <li><strong>Step-by-Step Scan (StepScan):</strong> Increments simulated time by exactly <strong>one single scan cycle</strong> (Scan #N &rarr; Scan #N+1). Indispensable for catching sub-second rising-edge pulses!</li>
            <li><strong>Virtual Input Injection:</strong> Toggle switches <code>[ 0 ] / [ 1 ]</code> for digital inputs <code>DI0..DI7</code> or adjust analog sliders for <code>AI0..AI3</code>.</li>
            <li><strong>Simulated Speed Acceleration:</strong> Speed up execution to <code>5x</code> or <code>10x</code> to verify multi-hour timers in seconds.</li>
          </ul>
        </section>

        <section class="section-container" id="sec-6-2">
          <h2 class="section-title"><span class="sec-num">6.2</span> Pre-Flight Compiler Gate &amp; Two-Phase Atomic Commit</h2>
          <p>To eliminate operational downtime during software updates, SynaptiX IDE executes a robust Two-Phase Commit protocol:</p>
          <ol>
            <li><strong>Phase 1 (Verification &amp; Diff):</strong> Pressing <code>Ctrl+F5</code> builds the graph and presents the Diff Viewer, displaying exact parameter deltas against current device memory.</li>
            <li><strong>Phase 2 (Atomic Flash Commit):</strong> Compiled bytecode is staged to a secondary Flash bank. Upon full CRC-32 verification, the MCU swaps runtime pointers in under <strong>2 milliseconds</strong>. If a cable disconnection occurs during transmission, the controller safely rolls back to previous logic.</li>
          </ol>
        </section>

        <section class="section-container" id="sec-6-3">
          <h2 class="section-title"><span class="sec-num">6.3</span> Online Wire-Energizing Debug (F8) &amp; [STALE] Fault Badges</h2>
          <p>Once deployed to connected hardware (<code>F4</code>), open the Logic Graph (<code>Ctrl+2</code>) and press <code>F8</code> to engage <strong>Online Live Debug</strong>:</p>
          <ul>
            <li>🟢 <strong>Glowing Green Wires:</strong> Active high logic state (<code>1</code> / TRUE).</li>
            <li>⚫ <strong>Muted Gray Wires:</strong> Inactive logic state (<code>0</code> / FALSE).</li>
            <li>🔢 <strong>Live Pin Badges:</strong> Display engineering values and timer <code>ET/PT</code> progress bars in real-time at 20 FPS.</li>
            <li>⚠️ <strong>[STALE] Badges:</strong> If an RS-485 sensor cable breaks or times out, the corresponding <code>MB_READ</code> node immediately flags <strong>[STALE]</strong> to pinpoint physical cable breaks.</li>
          </ul>
        </section>

        <section class="section-container" id="sec-6-4">
          <h2 class="section-title"><span class="sec-num">6.4</span> Live Watch High-Density Grid &amp; Timed Variable Forcing (Ctrl+7)</h2>
          <p>The <strong>Live Watch (Ctrl+7)</strong> workspace features a high-density IBM Carbon data grid and multi-channel real-time trend charts. Commissioning engineers can force outputs or override variables under strict lease protection:</p>

          <div class="figure-container">
            <img src="{img('synaptix_live_watch.png')}" alt="Live Watch High-Density Telemetry Grid">
            <div class="figure-caption">Figure 6.2: Live Watch High-Density Telemetry &amp; Multi-Channel Trend Charts (Ctrl+7)</div>
          </div>

          <div class="callout callout-warning">
            <div class="callout-header">⚠️ TIMED FORCE LEASE PROTECTION</div>
            All forced variables require a defined expiration lease (e.g., 30 seconds or 5 minutes). When the lease expires, the system automatically releases the override back to logic control, preventing forgotten manual overrides from causing factory line incidents.
          </div>
        </section>
        """
    },
    {
        "id": "ch7",
        "num": 7,
        "title": "Chapter 7: Industrial Fieldbus & IIoT Edge Computing",
        "sections": [
            ("7.1", "Modbus RTU / TCP Master & Slave Architecture"),
            ("7.2", "Cloud Telemetry & JSON Command Parsing: MQTT & HTTP"),
            ("7.3", "MQTT TLS X.509 Certificate Security & Industrial Wi-Fi"),
            ("7.4", "DTU Gateway Studio & Wireless Zigbee Mesh Networking"),
        ],
        "content": f"""
        <section class="section-container" id="sec-7-1">
          <h2 class="section-title"><span class="sec-num">7.1</span> Modbus RTU / TCP Master &amp; Slave Architecture</h2>
          <p>SynaptiX Controllers provide dual optically isolated RS-485 interfaces capable of operating simultaneously as a <strong>Master</strong> collecting field sensor data and as a <strong>Slave</strong> serving enterprise SCADA systems:</p>
          <ul>
            <li><strong>MB_READ (Master Read):</strong> Periodically polls holding and input registers from field instruments (energy meters, temperature probes, VFD inverters) via function codes <code>FC01, FC02, FC03, FC04</code>. Supports block-read optimization.</li>
            <li><strong>MB_WRITE (Master Write):</strong> Writes control commands (Run/Stop, speed setpoints, limit registers) to field actuators via <code>FC05, FC06, FC16</code>.</li>
            <li><strong>Fixed SCADA Slave Memory Map:</strong> In slave mode (via Port 2 or Ethernet Port 502), provides a deterministic register map (see Appendix B).</li>
          </ul>
        </section>

        <section class="section-container" id="sec-7-2">
          <h2 class="section-title"><span class="sec-num">7.2</span> Cloud Telemetry &amp; JSON Command Parsing: MQTT &amp; HTTP</h2>
          <p>The <strong>MQTT_PUB</strong> block packages up to 8 tag variables into standard JSON telemetry frames and publishes them to enterprise cloud brokers:</p>
          <pre><code>// Standard telemetry JSON published by MQTT_PUB to Topic: factory/cell1/telemetry
{{
  "device_id": "SYNX-CTRL-884-01",
  "timestamp": 1731998400,
  "temperature": 72.4,
  "pressure": 4.82,
  "fan_status": 1,
  "error_code": 0
}}</code></pre>
          <p>Conversely, <strong>MQTT_SUB</strong> subscribes to remote commands. It enforces an <strong>Atomic All-or-Nothing</strong> policy: if incoming JSON packets have malformed schemas or missing keys, the entire packet is rejected and an <code>Err</code> pulse is asserted rather than applying partial corrupted setpoints.</p>
        </section>

        <section class="section-container" id="sec-7-3">
          <h2 class="section-title"><span class="sec-num">7.3</span> MQTT TLS X.509 Certificate Security &amp; Industrial Wi-Fi</h2>
          <p>To defend industrial plants against eavesdropping and command spoofing, communication channels support <strong>TLS 1.3 with mutual X.509 certificate authentication</strong>:</p>
          <ul>
            <li>Secure onboard storage of Root CA certificates (<code>.pem / .crt</code>).</li>
            <li>Automated broker Subject Alternative Name (SAN) validation and expiration checking via hardware RTC clock.</li>
            <li>Industrial Wi-Fi modules support WPA2-Personal and WPA3-Enterprise with automated reconnection algorithms and continuous RSSI link quality telemetry.</li>
          </ul>
        </section>

        <section class="section-container" id="sec-7-4">
          <h2 class="section-title"><span class="sec-num">7.4</span> DTU Gateway Studio &amp; Wireless Zigbee Mesh Networking</h2>
          <p>In large manufacturing facilities with distributed wireless sensing, the <strong>XLink Gateway</strong> acts as an industrial <strong>Zigbee 3.0 Mesh Coordinator</strong>:</p>
          <ul>
            <li>Wireless sensor nodes dynamically mesh through router devices back to the central gateway.</li>
            <li>The integrated <strong>DTU Gateway Studio</strong> displays interactive network topologies, Link Quality Indicator (LQI) heatmaps, and battery voltage levels.</li>
          </ul>
        </section>
        """
    },
    {
        "id": "ch8",
        "num": 8,
        "title": "Chapter 8: Real-World Industrial Application Notes",
        "sections": [
            ("8.1", "AN-001: Automated Duty-Standby Pump Alternation & Thermal Cutoff"),
            ("8.2", "AN-002: Substation Electrical Monitoring & Cloud Telemetry"),
        ],
        "content": f"""
        <section class="section-container" id="sec-8-1">
          <h2 class="section-title"><span class="sec-num">8.1</span> AN-001: Automated Duty-Standby Pump Alternation &amp; Thermal Cutoff</h2>
          <p><strong>Mission:</strong> Alternate two industrial pumps (Pump 1 - <code>DO0</code>, Pump 2 - <code>DO1</code>) every 6 operating hours to equalize mechanical wear, monitoring winding temperatures via <code>AI0</code> and <code>AI1</code>:</p>
          <ul>
            <li><strong>Sequencing Logic:</strong> Sump low-level switch <code>DI0 == 1</code> (water present) and tank high-level switch <code>DI1 == 0</code> (water demand) engage pumping. A <code>CYCLE_TIMER</code> rotates active pump relays every 21,600 seconds (6 hours).</li>
            <li><strong>Thermal Protection:</strong> If the winding temperature of the operating pump exceeds <code>85.0 °C</code>, a <code>COMPARATOR</code> immediately cuts power via <code>GUARD</code>, asserts alarm flag <code>VFLAG10 = 1</code>, and transfers load to the standby pump without interrupting factory water supply.</li>
          </ul>
        </section>

        <section class="section-container" id="sec-8-2">
          <h2 class="section-title"><span class="sec-num">8.2</span> AN-002: Substation Electrical Monitoring &amp; Cloud Telemetry</h2>
          <p><strong>Mission:</strong> Aggregate 3-phase voltage, current, total active energy kWh from an electrical power meter over RS-485 alongside transformer oil temperature via <code>AI0</code> (4-20mA):</p>
          <ul>
            <li>An <code>MB_READ</code> block polls energy registers every 2 seconds over RS-485 #1.</li>
            <li>A <code>SCALE</code> block maps the 4-20mA signal to <code>-20.0 .. +80.0 °C</code>.</li>
            <li>An <code>MQTT_PUB</code> block publishes the consolidated telemetry packet to <code>synaptix-io.com</code> every 10 seconds over secure 4G LTE with TLS X.509 encryption.</li>
          </ul>
        </section>
        """
    },
    {
        "id": "app_a",
        "num": "A",
        "title": "Appendix A: IDE Keyboard Shortcuts & Navigation Cheatsheet",
        "sections": [
            ("A.1", "Complete Hotkey Map & Engineering Mouse Gestures"),
        ],
        "content": f"""
        <section class="section-container" id="sec-a-1">
          <h2 class="section-title"><span class="sec-num">A.1</span> Complete Hotkey Map &amp; Engineering Mouse Gestures</h2>
          <div class="table-caption">Table A.1: System Hotkeys &amp; Visual Design Shortcuts</div>
          <table>
            <thead>
              <tr>
                <th style="width: 25%;">Shortcut / Gesture</th>
                <th style="width: 35%;">Action Performed</th>
                <th style="width: 40%;">Field Engineering Efficiency Tip</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><code>F4</code></td>
                <td>Open Device Connection Dialog (USB CDC / Modbus TCP)</td>
                <td>Instant scan for online COM ports and network IPs.</td>
              </tr>
              <tr>
                <td><code>F5</code></td>
                <td>Launch Offline Simulator Window</td>
                <td>Zero-hardware cycle-accurate logic and input testing.</td>
              </tr>
              <tr>
                <td><code>F7</code></td>
                <td>Compile Project &amp; Run Pre-Flight Integrity Check</td>
                <td>Scans for unassigned pins, type mismatches, and read-only writes.</td>
              </tr>
              <tr>
                <td><code>F8</code></td>
                <td>Toggle Online Live Debug Overlays</td>
                <td>Energizes green active wires and displays live numeric badges.</td>
              </tr>
              <tr>
                <td><code>Ctrl + F5</code></td>
                <td>Fast Compile &amp; Two-Phase Atomic Deploy</td>
                <td>Engages safety commit gate to flash hardware safely.</td>
              </tr>
              <tr>
                <td><code>Ctrl + 1 .. 7</code></td>
                <td>Instant Switch between 7 Workspaces</td>
                <td><code>Ctrl+2</code> opens logic graph; <code>Ctrl+7</code> opens live watch.</td>
              </tr>
              <tr>
                <td><code>Space + Drag</code></td>
                <td>Pan Canvas Viewport</td>
                <td>Smooth navigation across complex multi-rung schematics.</td>
              </tr>
              <tr>
                <td><code>Mouse Wheel</code></td>
                <td>Zoom In / Zoom Out (25% to 200%)</td>
                <td>Zoom in for pin wiring; zoom out for complete system topology.</td>
              </tr>
              <tr>
                <td><code>Delete</code></td>
                <td>Remove Selected Block or Wire Segment</td>
                <td>Press <code>Ctrl+Z</code> to instantly undo accidental deletions.</td>
              </tr>
              <tr>
                <td><code>Ctrl + Space</code></td>
                <td>Activate SynaptiX AI Copilot Assistant</td>
                <td>Instant contextual design advice and technical manual lookup.</td>
              </tr>
            </tbody>
          </table>
        </section>
        """
    },
    {
        "id": "app_b",
        "num": "B",
        "title": "Appendix B: Fixed Modbus SCADA Slave Memory Map",
        "sections": [
            ("B.1", "Standard Holding Register Map (0-based & 1-based)"),
        ],
        "content": f"""
        <section class="section-container" id="sec-b-1">
          <h2 class="section-title"><span class="sec-num">B.1</span> Standard Holding Register Map (0-based &amp; 1-based)</h2>
          <p>When operating in Modbus Slave mode serving supervisory SCADA/HMI platforms via RS-485 or Ethernet Modbus TCP (Port 502), the memory space is mapped deterministically according to the following layout:</p>

          <div class="table-caption">Table B.1: Fixed Holding Register Mapping (Function Codes 03, 04, 06, 16)</div>
          <table>
            <thead>
              <tr>
                <th style="width: 18%;">Address (0-based)</th>
                <th style="width: 18%;">SCADA (1-based)</th>
                <th style="width: 18%;">Tag Partition</th>
                <th style="width: 16%;">Access</th>
                <th style="width: 30%;">Byte Organization &amp; Format</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><code>0x0000 .. 0x0007</code></td>
                <td><code>40001 .. 40008</code></td>
                <td><code>DI0 .. DI7</code></td>
                <td>Read-Only (RO)</td>
                <td>Bit packed (0 = Inactive, 1 = Active)</td>
              </tr>
              <tr>
                <td><code>0x0010 .. 0x0017</code></td>
                <td><code>40017 .. 40024</code></td>
                <td><code>DO0 .. DO7</code></td>
                <td>Read/Write (RW)</td>
                <td>Bit packed (0 = De-energized, 1 = Energized)</td>
              </tr>
              <tr>
                <td><code>0x0064 .. 0x006B</code></td>
                <td><code>40101 .. 40108</code></td>
                <td><code>AI0 .. AI3</code></td>
                <td>Read-Only (RO)</td>
                <td>2 registers per channel (IEEE 754 Float32, Big-Endian)</td>
              </tr>
              <tr>
                <td><code>0x00C8 .. 0x00DF</code></td>
                <td><code>40201 .. 40224</code></td>
                <td><code>VFLAG0 .. 31</code></td>
                <td>Read/Write (RW)</td>
                <td>Binary supervisory flags</td>
              </tr>
              <tr>
                <td><code>0x012C .. 0x016B</code></td>
                <td><code>40301 .. 40364</code></td>
                <td><code>VREG0 .. 31</code></td>
                <td>Read/Write (RW)</td>
                <td>32-bit Float32 or signed INT32 registers</td>
              </tr>
              <tr>
                <td><code>0x0190 .. 0x01CF</code></td>
                <td><code>40401 .. 40464</code></td>
                <td><code>VREG_RETAIN</code></td>
                <td>Read/Write (RW)</td>
                <td>Non-volatile Flash parameters (Setpoints, thresholds)</td>
              </tr>
              <tr>
                <td><code>0x01F4 .. 0x0203</code></td>
                <td><code>40501 .. 40516</code></td>
                <td><code>COUNTER0 .. 7</code></td>
                <td>Read/Write (RW)</td>
                <td>32-bit production totals (INT32)</td>
              </tr>
              <tr>
                <td><code>0x03E8 .. 0x03EB</code></td>
                <td><code>41001 .. 41004</code></td>
                <td><code>SYS_DIAG</code></td>
                <td>Read-Only (RO)</td>
                <td>Fault status codes, CPU load (%), scan cycle jitter (ms)</td>
              </tr>
            </tbody>
          </table>
        </section>
        """
    },
    {
        "id": "app_c",
        "num": "C",
        "title": "Appendix C: System Diagnostic Fault Codes & Troubleshooting",
        "sections": [
            ("C.1", "Operational Fault Codes & Corrective Field Actions"),
        ],
        "content": f"""
        <section class="section-container" id="sec-c-1">
          <h2 class="section-title"><span class="sec-num">C.1</span> Operational Fault Codes &amp; Corrective Field Actions</h2>
          <div class="table-caption">Table C.1: Common Fault Codes &amp; Rapid Field Resolution Procedures</div>
          <table>
            <thead>
              <tr>
                <th style="width: 18%;">Fault Code / Symptom</th>
                <th style="width: 25%;">Technical Description</th>
                <th style="width: 27%;">Typical Physical Root Cause</th>
                <th style="width: 30%;">3-Step Field Corrective Procedure</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>[STALE] Badge</strong></td>
                <td>Fieldbus Loss of Telemetry</td>
                <td>Severed A/B wires, sensor lost 24V power, or mismatched baud rate.</td>
                <td>1. Verify 24V power at sensor terminals.<br/>2. Use Wire Inspector (Ctrl+Shift+D) to test read once.<br/>3. Swap RS-485 differential wires A(+) and B(-).</td>
              </tr>
              <tr>
                <td><strong>Error 0x02</strong></td>
                <td>Modbus Illegal Data Address</td>
                <td>SCADA client polling beyond registered boundaries or cross-partition read.</td>
                <td>1. Confirm 0-based vs 1-based offset (+1).<br/>2. Split polling into two non-overlapping requests.</td>
              </tr>
              <tr>
                <td><strong>Error 0xE001</strong></td>
                <td>Arithmetic Division by Zero</td>
                <td>MATH block operand <code>IN2</code> reached zero or sensor measured 0.</td>
                <td>1. Place <code>COMPARATOR</code> verifying <code>IN2 != 0</code>.<br/>2. Gating execution via <code>GUARD</code> prior to division.</td>
              </tr>
              <tr>
                <td><strong>Error 0xE002</strong></td>
                <td>ALU Arithmetic Overflow</td>
                <td>Multiplication product exceeded maximum signed <code>INT32</code> limits.</td>
                <td>1. Switch block data type to <code>REAL</code> (Float32).<br/>2. Insert <code>SCALE</code> block to normalize ranges.</td>
              </tr>
              <tr>
                <td><strong>COM Port Missing</strong></td>
                <td>USB CDC Device Not Enumerated</td>
                <td>Using a 2-wire charging cable, or port locked exclusively by PuTTY/ModScan.</td>
                <td>1. Replace with genuine data-capable USB cable.<br/>2. Terminate competing terminal instances.<br/>3. Press F4 to re-enumerate bus.</td>
              </tr>
            </tbody>
          </table>
        </section>
        """
    }
]
