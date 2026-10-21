# SynaptiX IDE — Technical User Manual
**Deterministic Logic Programming, Canvas Simulation & Edge IIoT Engineering Environment**

*Document Identifier:* **MAN-SPX-IDE-2026-V2.5**  
*Applicable Software Release:* **SynaptiX IDE v2.5.0 or later**  
*Hardware Platforms:* **SynaptiX SYNX-RIO-884 / Modbus RTU & TCP Slaves**  
*Normative Standards:* **IEC/IEEE 82079-1:2019, IEC 61131-3, SPLC-AF-003, ANSI Z535.6**  
*Publication Date:* **October 2026 / Commercial Release**  
*Classification:* **Commercial / Engineering Standard**

---

## Document Control & Revision History

This technical manual adheres to IEC/IEEE 82079-1:2019 principles. Any changes in software behavior, protocol schemas, or safety interfaces trigger formal engineering document revisions.

| Rev | Release Date | Author / Department | Approved By | Scope of Revision |
| :---: | :---: | :---: | :---: | :--- |
| **v1.0.0** | Sep 2026 | SynaptiX Software Group | Engineering Director | Initial commercial draft; 4-stage logic canvas and basic tag management. |
| **v2.0.0** | Oct 2026 | Industrial Systems Team | Lead Systems Architect | Integrated MQTT IIoT Gateway (PUB/SUB), IEC Timers/Counters, and 2-Phase Staged Deployment. |
| **v2.5.0** | Oct 2026 | AI & Automation Group | VP of Engineering | 6-Module workflow architecture, step-by-step procedures, UI callout tables, and pure popup dialogs. |
| **v2.6.0** | Oct 2026 | Systems Engineering Team | Engineering Director | ISA-101/IEC 60073 industrial color refactoring, MB_WRITE (Modbus RTU) & DIRECT_WRITE (Terminals DO0..DO7 for UCS16703 LED strip, PWM, Pulse Train) blocks, and SPX Logistics Example Library. |
| **v2.8.0** | Oct 2026 | Systems Engineering Team | Engineering Director | Added Wi-Fi Subsystem (0x1300..0x13FF, SPLC-SPEC-WIFI-004-ICD), Unified Industrial Wi-Fi Configuration Dialog (Ctrl+Shift+W) and Live Telemetry RSSI/IP/MAC. |


> [!NOTICE]
> **PROPRIETARY NOTICE & SOFTWARE LICENSING**  
> SynaptiX IDE is proprietary software protected under international copyright and industrial intellectual property treaties. Reproduction, reverse engineering, decompilation, or unauthorized dissemination of binary executables, byte-level ABI specifications, or technical documentation without explicit written consent from SynaptiX Automation Systems Group is strictly prohibited.

---

## List of Technical Figures

| Figure ID | Technical Caption & Functional Context | Reference |
| :---: | :--- | :---: |
| **Figure 1-1** | Over-the-Air (OTA) Automated Software Update Dialog | Section 1.4 |
| **Figure 2-1** | SynaptiX IDE Primary Workbench Anatomy and Ribbon Navigation Topography | Section 2.1 |
| **Figure 2-2** | New Project Configuration Wizard and Controller Target Profile Selection | Section 2.2 |
| **Figure 2-3** | Centralized 128-Tag Engineering Catalog — Physical I/O, Modbus & Retain Flash | Section 2.3 |
| **Figure 3-1** | 4-Stage Orthogonal Logic Canvas with 90-Degree Manhattan Wire Routing | Section 3.1 |
| **Figure 3-2** | Dual Rule Table — Human-Readable Logic Narratives and Compiled Bytecode Grid | Section 3.2 |
| **Figure 3-3** | Industrial Blueprints Gallery and Pre-Engineered Application Templates | Section 3.3 |
| **Figure 4-1** | Canvas-Centric Simulation Testbench — Floating I/O Board, Force Table & Cycle Trace | Section 4.1 |
| **Figure 4-2** | Industrial AI Copilot Assistant — Natural Language Synthesis & Ghost Diff Verification | Section 4.2 |
| **Figure 5-1** | Target Controller Hardware Inventory and Firmware Diagnostics Dialog | Section 5.1 |
| **Figure 5-2** | Onboard Real-Time Clock (RTC) Timezone Synchronization Dialog | Section 5.1 |
| **Figure 5-3** | SPLC-AF-003 Two-Phase Safe Deploy Gate with CRC-16 Staging Commit | Section 5.2 |
| **Figure 5-4** | Real-Time Telemetry Live Watch Grid, Signal Quality Badges & Sparklines | Section 5.3 |
| **Figure 6-1** | System MQTT Broker TCP/TLS Connection Configuration Dialog | Section 6.1 |
| **Figure 6-2** | Outbound Telemetry Publishing (MQTT PUB) Visual Field Builder Dialog | Section 6.2 |
| **Figure 6-3** | Inbound Supervisory Command Ingestion (MQTT SUB) and Fail-Safe Schema Dialog | Section 6.3 |
| **Figure 6-4** | Modbus RTU Master Sensor Poller (MB_READ) & Live Frame Inspector Dialog | Section 6.4 |

---

## Table of Contents

- [Part 1: System Overview & Getting Started](#part-1-system-overview--getting-started)
  - [1.1 System Concept & Core Architectural Pillars](#11-system-concept--core-architectural-pillars)
  - [1.2 Hardware Specifications & Workstation Prerequisites](#12-hardware-specifications--workstation-prerequisites)
  - [1.3 Portable Execution & USB Serial Driver Setup](#13-portable-execution--usb-serial-driver-setup)
  - [1.4 Over-the-Air (OTA) Automated Software Updates](#14-over-the-air-ota-automated-software-updates)
- [Part 2: Engineering Workspace & Data Management](#part-2-engineering-workspace--data-management)
  - [2.1 Main Workbench Anatomy & Ribbon Navigation](#21-main-workbench-anatomy--ribbon-navigation)
  - [2.2 Project Creation Wizard & Hardware Profiles (.synx)](#22-project-creation-wizard--hardware-profiles-synx)
  - [2.3 Centralized 128-Tag Catalog & Modbus Mapping](#23-centralized-128-tag-catalog--modbus-mapping)
- [Part 3: Control Logic Design & Instruction Library](#part-3-control-logic-design--instruction-library)
  - [3.1 4-Stage Orthogonal Logic Programming](#31-4-stage-orthogonal-logic-programming)
  - [3.2 Dual Rule Table & AST Syntax Validation](#32-dual-rule-table--ast-syntax-validation)
  - [3.3 Standard Instruction Library & Blueprints Gallery](#33-standard-instruction-library--blueprints-gallery)
- [Part 4: Verification, Simulation & Industrial AI](#part-4-verification-simulation--industrial-ai)
  - [4.1 Canvas-Centric Simulation Engine & Testbench](#41-canvas-centric-simulation-engine--testbench)
  - [4.2 Senior Industrial AI Copilot & Ghost Diff Engine](#42-senior-industrial-ai-copilot--ghost-diff-engine)
- [Part 5: Hardware Commissioning, Safe Deployment & Telemetry](#part-5-hardware-commissioning-safe-deployment--telemetry)
  - [5.1 Target Controller Hardware Diagnostics & RTC Sync](#51-target-controller-hardware-diagnostics--rtc-sync)
  - [5.2 SPLC-AF-003 Two-Phase Safe Deploy Gate](#52-splc-af-003-two-phase-safe-deploy-gate)
  - [5.3 Real-Time Telemetry & Live Watch Grid](#53-real-time-telemetry--live-watch-grid)
- [Part 6: IIoT Edge Connectivity & System Maintenance](#part-6-iiot-edge-connectivity--system-maintenance)
  - [6.1 Industrial MQTT Broker TCP/TLS Configuration](#61-industrial-mqtt-broker-tcptls-configuration)
  - [6.2 Outbound Telemetry Publishing (MQTT PUB) & Visual Field Builder](#62-outbound-telemetry-publishing-mqtt-pub--visual-field-builder)
  - [6.3 Inbound Supervisory Command Ingestion (MQTT SUB)](#63-inbound-supervisory-command-ingestion-mqtt-sub)
  - [6.4 RS-485 Modbus RTU Master Polling & Sensor Ingestion (MB_READ)](#64-rs-485-modbus-rtu-master-polling--sensor-ingestion-mb_read)
  - [6.5 RS-485 Modbus RTU Master Actuator Controller (MB_WRITE)](#65-rs-485-modbus-rtu-master-actuator-controller-mb_write)
  - [6.6 High-Speed Pulse Generator & Actuator Controller (PULSE_OUT)](#66-high-speed-pulse-generator--actuator-controller-pulse_out)
  - [6.7 Wireless Wi-Fi Communication Configuration (Wi-Fi Configuration Dialog)](#67-wireless-wi-fi-communication-configuration-wi-fi-configuration-dialog)
  - [6.8 Diagnostics Fault Directory & Safety Mandate](#68-diagnostics-fault-directory--safety-mandate)
- [Part 7: Example Projects Library & Industrial Case Studies](#part-7-example-projects-library--industrial-case-studies)
  - [7.1 SPX Express: Sort-to-Light 18-Bin Parcel Sorting Hub](#71-spx-express-sort-to-light-18-bin-parcel-sorting-hub)
  - [7.2 Closed-Loop VFD Pressure Booster Pump Station](#72-closed-loop-vfd-pressure-booster-pump-station)
  - [7.3 GSP-Compliant Pharmaceutical Cold Chain Smart Gateway](#73-gsp-compliant-pharmaceutical-cold-chain-smart-gateway)
  - [7.4 Opening and Running Simulations on Canvas](#74-opening-and-running-simulations-on-canvas)
- [Technical Appendices](#technical-appendices)
  - [Appendix A: Complete Modbus RTU / TCP Register Map](#appendix-a-complete-modbus-rtu--tcp-register-map)
  - [Appendix B: SPLC-AF-003 Binary Bytecode Specification](#appendix-b-splc-af-003-binary-bytecode-specification)
  - [Appendix C: Keyboard Shortcuts & Engineering Quick Reference](#appendix-c-keyboard-shortcuts--engineering-quick-reference)

---

# Part 1: System Overview & Getting Started
*Hardware Specifications, Portable Runtime & First Connection*

### 1.1 System Concept & Core Architectural Pillars
SynaptiX IDE is a professional soft-PLC engineering environment designed for mission-critical industrial machines and edge automation. Built on four core pillars:
- **Deterministic 10ms Engine:** Hard real-time 10ms execution loop (100 Hz) with sub-microsecond jitter for process I/O scan.
- **4-Stage Orthogonal Pipeline:** Strict unidirectional signal flow (Input -> Trigger -> Guard -> Action) with 90° Manhattan wiring.
- **Dual AST Compiler:** Simultaneous compilation of graphical visual graphs into human narratives and 32-byte binary execution records.
- **Edge IIoT Convergence:** Native MQTT Telemetry Publisher and Command Subscriber bridging OT sensors directly to IT/Cloud SCADA.

### 1.2 Hardware Specifications & Workstation Prerequisites
The reference hardware controller (SynaptiX SYNX-RIO-884) incorporates the following industrial specifications:

| Subsystem / Parameter | Technical Specification | Standard |
| :--- | :--- | :--- |
| **Discrete Inputs (DI)** | 8 Channels (DI0 - DI7), 24VDC Optocoupled, 3.5mA Sink/Source | IEC 61131-2 Type 1/3 |
| **Discrete Outputs (DO)** | 8 Channels (DO0 - DO7), Relay Contact 250VAC/5A or Solid-State 24VDC/1A | IEC 61131-2 Standard |
| **Analog Inputs (AI)** | 4 Channels (AI0 - AI3), 12-Bit ADC (0 - 10VDC or 4 - 20mA) | Calibrated Engineering Units |
| **Internal Retain Flash** | 32 Non-Volatile Retain Registers (RETAIN0 - RETAIN31), 100k Write Cycles | Ferroelectric / Flash Emulated |
| **Real-Time Clock (RTC)** | DS3231 High-Precision TCXO RTC with CR2032 Battery Backup (+/-2ppm) | I2C Hardware Bus |
| **Serial Communication** | RS-485 / USB CDC Serial, Modbus RTU Slave, Baud: 9600 - 115200 bps | EIA-485 / Modbus Spec |

### 1.3 Portable Execution & USB Serial Driver Setup
SynaptiX IDE runs as a zero-installation portable application. Follow these steps to prepare your engineering workstation:
- **Step 1: Extract Archive —** Extract `SynaptixIDE_v2.5.0_win-x64.zip` into a local non-network folder (e.g., `C:\Automation\SynaptixIDE\`).
- **Step 2: Runtime Check —** Ensure Microsoft .NET 8.0 Desktop Runtime (x64) is installed on Windows 10/11.
- **Step 3: Hardware Connection —** Connect the SYNX-RIO controller to host PC using a shielded USB Type-C cable. Windows Device Manager will enumerate `USB Serial Device (COMx)`.

### 1.4 Over-the-Air (OTA) Automated Software Updates
When connected to the internet, SynaptiX IDE periodically queries the official repository for software updates. When a release is detected, the OTA Software Update dialog opens:

![Figure 1-1: Over-the-Air (OTA) Automated Software Update Dialog](proposals/images/synaptix_update_dialog.png)
*Figure 1-1: Over-the-Air (OTA) Automated Software Update Dialog*

| UI Control / Area | Operational Function |
| :--- | :--- |
| **Current & New Version** | Displays active software release vs remote certified release tag. |
| **Release Notes Viewer** | Scrollable Markdown view detailing bug fixes, new features, and security patches. |
| **Download & Install Button** | Initiates secure binary download, validates SHA-256 hash, and restarts IDE. |
| **Skip This Version** | Suppresses notifications for the current release tag until subsequent release. |

---

# Part 2: Engineering Workspace & Data Management
*Ribbon Navigation, Project Containers (.synx) & 128-Tag Architecture*

### 2.1 Main Workbench Anatomy & Ribbon Navigation
The SynaptiX IDE interface is structured into standardized ergonomic regions designed for continuous operation in factory control rooms:

![Figure 2-1: SynaptiX IDE Primary Workbench Anatomy and Ribbon Navigation Topography](proposals/images/synaptix_main_anatomy.png)
*Figure 2-1: SynaptiX IDE Primary Workbench Anatomy and Ribbon Navigation Topography*

| Workbench Region | Description & Role |
| :--- | :--- |
| **[1] Title Bar & Port Bar** | Displays project filename, serial port selection, baud rate, and connection status. |
| **[2] Menu Bar** | Structured access to Project, Edit, View, Device, Tools, and Help operations. |
| **[3] Navigation Ribbon** | 6 dedicated tabs: Logic (Ctrl+1), Tags (Ctrl+2), Rules (Ctrl+3), Blueprints (Ctrl+4), Deploy (Ctrl+5), Live Watch (Ctrl+6). |
| **[4] Primary Work Area** | Hosts the active visual editor canvas, tag spreadsheet, or diagnostics grid. |
| **[5] Bottom Status Bar Telemetry** | Real-time scan cycle execution time (e.g. SCAN: 9.8ms), AST compile pill, and heartbeat indicator. |

#### 2.1.1 Industrial Ergonomics: ISA-101 & IEC 60073 Slate Palette
Starting with v2.6.0, SynaptiX IDE strictly adheres to international standards **ANSI/ISA-101.01-2015** (Human-Machine Interfaces for Process Automation Systems) and **IEC 60073** (Coding principles for indicators and actuators):
- **Industrial Slate Gray Base Theme:** Built upon neutral, glare-free dark slate shades (`#0F172A`, `#1E293B`, `#334155`) engineered for multi-hour control room shift operations, reducing ocular fatigue and highlighting critical process exceptions.
- **Semantic Safety & Status Color Codes:**
  - **Industrial Green (`#107C41` / `#166534`):** Strictly reserved for affirmative operating conditions (RUN, Online, Active, Normal, Logic 1). All high-saturation neon/pastel green accents have been eliminated.
  - **Emergency Red (`#B91C1C` / `#DC2626`):** Dedicated exclusively to critical alarms, trip events, communication faults, and hardware halts (FAULT, Stop, Trip, CRC Error, Offline).
  - **Industrial Amber / Orange (`#C05621` / `#B45309`):** Reserved for process warnings, stale telemetry data, and manual signal force overrides (Warning, Stale, Force Override).
  - **Industrial Blue (`#0284C7`):** Engineering information, RS-485 Modbus RTU Master functional blocks (`MB_READ`, `MB_WRITE`).
  - **Industrial Violet (`#7C3AED`):** Cloud & Edge IIoT connectivity blocks (`MQTT_PUB`, `MQTT_SUB`).

### 2.2 Project Creation Wizard & Hardware Profiles (.synx)
All project data (canvas nodes, 90° Manhattan wires, tag definitions, rules, and MQTT topics) are packaged into an atomic `.synx` JSON container. Follow these steps to initialize a new project:
- **Step 1: Open Wizard —** Select File -> New Project or press Ctrl+N to open the New Project Wizard.
- **Step 2: Enter Metadata —** Enter Project Name, Author designation, and browse to select target storage folder.
- **Step 3: Select Target Hardware —** Select target hardware profile (e.g., SYNX-RIO-884: 8 DI / 8 DO / 4 AI) and nominal scan rate (10ms).
- **Step 4: Initialize —** Click 'Create Project' to generate the project file and auto-populate default physical I/O tags.

![Figure 2-2: New Project Configuration Wizard and Controller Target Profile Selection](proposals/images/synaptix_new_project.png)
*Figure 2-2: New Project Configuration Wizard and Controller Target Profile Selection*

### 2.3 Centralized 128-Tag Catalog & Modbus Mapping
Tab 1 (I/O Tags) manages all system signals through a fixed, zero-fragmentation 128-Tag memory pool mapped directly to Modbus Holding Registers (0x0900 - 0x097F):

![Figure 2-3: Centralized 128-Tag Engineering Catalog — Physical I/O, Modbus & Retain Flash](proposals/images/synaptix_tag_catalog.png)
*Figure 2-3: Centralized 128-Tag Engineering Catalog — Physical I/O, Modbus & Retain Flash*

| Signal Kind | Capacity & Address | Data Type | Modbus Register Address |
| :--- | :--- | :--- | :--- |
| **Discrete Inputs (DI)** | 8 Tags (DI0 - DI7) | Boolean (0 / 1) | 0x0900 - 0x0907 |
| **Discrete Outputs (DO)** | 8 Tags (DO0 - DO7) | Boolean (0 / 1) | 0x0908 - 0x090F |
| **Analog Inputs (AI)** | 4 Tags (AI0 - AI3) | Float32 (IEEE 754, 2 Regs/Tag) | 0x0910 - 0x0917 |
| **Virtual Registers (VREG)** | 32 Tags (VREG0 - VREG31) | Int32 / Float32 (Volatile) | 0x0918 - 0x0957 |
| **Retain Registers (RETAIN)** | 32 Tags (RETAIN0 - RETAIN31) | Int32 / Float32 (Non-Volatile Flash) | 0x0958 - 0x0997 |
| **Virtual Flags (VFLAG)** | 32 Tags (VFLAG0 - VFLAG31) | Boolean (Internal Bits) | 0x0998 - 0x09B7 |

> [!CAUTION]
> **NON-VOLATILE RETAIN FLASH ENDURANCE LIMITS**  
> Retain Registers (RETAIN0..31) are written to physical flash upon state change. While rated for 100,000 write cycles, high-frequency cyclic logic must NOT write to Retain tags every 10ms scan. Use volatile VREG for rapid math and commit to RETAIN only upon process state changes.

---

# Part 3: Control Logic Design & Instruction Library
*4-Stage Orthogonal Canvas, Dual Rule Table & Industrial Blueprints*

### 3.1 4-Stage Orthogonal Logic Programming
Tab 0 (Logic Graph) enforces a strict 4-stage pipeline where signals flow unidirectionally from left to right through 90° Manhattan routed wires:

![Figure 3-1: 4-Stage Orthogonal Logic Canvas with 90-Degree Manhattan Wire Routing](proposals/images/synaptix_logic_editor.png)
*Figure 3-1: 4-Stage Orthogonal Logic Canvas with 90-Degree Manhattan Wire Routing*

| Pipeline Stage | Functional Role & Operational Execution |
| :--- | :--- |
| **Stage 1: Input Signal** | Selects physical inputs (DI, AI), timers, or virtual registers as primary observation signals. |
| **Stage 2: Logic Trigger** | Evaluates transitions: Rising Edge (ON_RISE), Falling Edge (ON_FALL), Level, or Numeric CMP. |
| **Stage 3: Guard Interlock** | Safety interlock chain. Evaluates secondary permissive guards (e.g. E-Stop, Door Guard, Thermal OK). |
| **Stage 4: Actuator Action** | Drives physical DO actuators (SET/RESET/TOGGLE), increments counters, or writes registers. |

#### Comprehensive 13 Function Blocks Master Catalog

SynaptiX IDE standardizes on 13 modular Function Blocks classified into 4 functional groups compliant with IEC 61131-3 and industrial IIoT:

| # | Block & Badge | Functional Group | Terminal Pins (In / Out) | Operational Role & Typical Application |
| :---: | :--- | :--- | :--- | :--- |
| **1** | **`INPUT`** `[INPUT]` | Signal Ingestion (Stage 1) | Out: `Out` (Data) | Reads physical inputs DI0..DI7, AI0..AI3, internal variables VREG0..31, non-volatile RETAIN0..31, or static constants. |
| **2** | **`TRIGGER`** `[TRIG]` | Event Detection (Stage 2) | In: `In` (Data)<br>Out: `Trig` (Event) | Detects state transitions: Rising edge (`ON_RISE`), Falling edge (`ON_FALL`), Logic level (`ON_HIGH`, `ON_LOW`), or RTC Schedule (`TIME_WINDOW`). |
| **3** | **`COMPARATOR`** `[CMP]` | Evaluation & Threshold (Stage 2) | In: `In` (Data)<br>Out: `Pass` (Logic) | Compares integer or Float32 values against threshold: `>`, `>=`, `==`, `!=`, `<=`, `<` (pressure, temperature, tank level supervision). |
| **4** | **`GUARD`** `[GUARD]` | Permissive Interlock (Stage 3) | In: `In` (Logic)<br>Out: `Permit` (Logic) | Series safety interlock chain. Passes execution signal only when permissive condition is satisfied (`E-Stop = 1`, `Door Closed = 1`, `Thermal OK = 1`). |
| **5** | **`ACTION`** `[ACTION]` | Actuator Command (Stage 4) | In: `Exec` (Event/Logic)<br>Out: Terminal DO/VREG | Executes control actions: `SET` (energize), `RESET` (de-energize), `TOGGLE` relay outputs DO0..DO7, assigns numeric value to VREG, or adjusts counters. |
| **6** | **`TIMER`** `[TON / TOF / TP]` | IEC 61131-3 Timers | In: `IN`, `PT` (ms)<br>Out: `Q` (Logic), `ET` (ms) | Standard timers: `TON` (On-delay pump spin-up), `TOF` (Off-delay cooling fan), `TP` (Monostable single pulse duration for pneumatic ejectors). |
| **7** | **`COUNTER`** `[CTU / CTD]` | IEC 61131-3 Counters | In: `CU`/`CD`, `R`/`LD`, `PV`<br>Out: `Q` (Logic), `CV` (Int) | Industrial counters: `CTU` (Up-count batch production), `CTD` (Down-count remaining blanks). Retained in non-volatile Flash. |
| **8** | **`SCALE`** `[SCALE]` | Linear Engineering Scaling | In: `In` (Raw ADC)<br>Out: `Out` (Scaled Float) | Linear transformation $y = k \cdot x + b$: Maps raw ADC 0..4095 or 4-20mA current to engineering units (0..10 bar, -20..80 °C, 0..100%). |
| **9** | **`PULSE_OUT`** `[PULSE]` | High-Speed Waveform & Actuator | In: `Trig`, `In/Chan`, `En/Param`<br>Out: `Done`, `Err` | Direct high-speed control on DO0..DO7: Discrete DO, PWM duty cycle (1..20 kHz), PTO stepper pulses, WS2812B/UCS16703 18-bin Sort-to-Light addressable LED bus. *(Backward compatible with DIRECT_WRITE)*. |
| **10** | **`MB_READ`** `[MB_READ]` | Modbus RTU Master Read | In: `En` (Enable)<br>Out: `Done`, `Err`, `Data` (VREG) | Ingests RS-485 Modbus RTU slave devices via FC01, FC02, FC03, FC04; automatically updates destination VREG registers periodically or on-demand. |
| **11** | **`MB_WRITE`** `[MB_WRITE]` | Modbus RTU Master Write | In: `Trig`, `In/Val`<br>Out: `Done`, `Err` | Commands RS-485 Modbus RTU slaves (VFD inverters, smart relays) via FC05, FC06, FC15, FC16; supports channel-indexed Sort-to-Light logistics. |
| **12** | **`MQTT_PUB`** `[MQTT_PUB]` | IIoT Telemetry Publisher | In: `Trig`, `In0..In3` (Data)<br>Out: `Done`, `Err` | Encapsulates process telemetry into structured JSON payloads published to MQTT Broker over TCP/TLS (QoS 0/1/2, Retain) cyclically or on event. |
| **13** | **`MQTT_SUB`** `[MQTT_SUB]` | IIoT Inbound Command Ingest | Out: `Out0..Out3` (Data), `NewMsg` (Event) | Subscribes to Cloud/WMS supervisory topics, extracts JSON payload key-value fields onto connector pins, and asserts 1-scan notification pulse. |

### 3.2 Dual Rule Table & AST Syntax Validation
Tab 2 (Rule Table) presents logic in two synchronized perspectives: Left view provides human-readable engineering narratives, while right view displays compiled 32-byte bytecode parameters:

![Figure 3-2: Dual Rule Table — Human-Readable Logic Narratives and Compiled Bytecode Grid](proposals/images/synaptix_rule_table.png)
*Figure 3-2: Dual Rule Table — Human-Readable Logic Narratives and Compiled Bytecode Grid*

The Abstract Syntax Tree (AST) engine continuously parses the logic network in the background. If any pin is left un-wired or a cyclic loop is created, a red diagnostic badge appears in the status bar with an immediate navigation link to the error source.

### 3.3 Standard Instruction Library & Blueprints Gallery
SynaptiX IDE provides standard IEC 61131-3 instruction blocks (TON, TOF, TP timers, CTU/CTD counters, linear SCALE, and relational CMP) alongside advanced communication blocks (MQTT PUB/SUB, Modbus Master MB_READ/MB_WRITE). In Tab 3 (Blueprints), engineers can instantiate pre-engineered application templates with 1 click:

![Figure 3-3: Industrial Blueprints Gallery and Pre-Engineered Application Templates](proposals/images/synaptix_blueprints.png)
*Figure 3-3: Industrial Blueprints Gallery and Pre-Engineered Application Templates*

#### Pre-Engineered Blueprints Catalog

| Blueprint Group | Count | Representative Templates & Industrial Scope |
| :--- | :---: | :--- |
| **Group 1: Discrete Logic & Safety Interlocks** | 5 | Andon OEE Alert, Flash Retentive Production Counter, Material Feed Call, Sump Pump, Inverted Door Guard. |
| **Group 2: Motor Interlocking & Reversing** | 3 | Direct-On-Line (DOL) starter, Forward/Reverse Interlocked starter, Star-Delta sequencer. |
| **Group 3: IEC 61131-3 Real-Time Timers (TON/TOF/TP)** | 3 | Lube pump on-delay (TON), Cooling fan run-on off-delay (TOF), Part reject air pulse (TP). |
| **Group 4: IEC 61131-3 Batch Counters (CTU/CTD)** | 3 | Automatic boxing up-counter (CTU), Blank feeder countdown (CTD), Parking garage up-down counter (CTUD). |
| **Group 5: Analog Signal Normalization & Linear Scaling** | 5 | 4-20mA pressure transmitter, PT100/NTC RTD temperature, Hydrostatic tank level, CT current transformer. |
| **Group 6: Cloud Telemetry & Industrial IIoT (MQTT)** | 2 | Cloud Telemetry Publisher (MQTT PUB), Remote Supervisory Command Dispatcher (MQTT SUB). |
| **Group 7: RS-485 Modbus RTU Master & SPX Logistics** | 3 | **RS-485 Sensor Ingestion (MB_READ)**: Cyclic polling FC04 pressure transmitter into VREG0.<br>**VFD Speed Controller (MB_WRITE)**: Direct register write 50.00 Hz speed setpoint via FC06.<br>**SPX 18-Bin Sort-to-Light Hub**: Ingest WMS put commands via MQTT SUB and drive 18-bin LEDs via PULSE_OUT (DIRECT_WRITE) (Terminal DO3) without Modbus ID. |

- **Step 1: Browse Library —** Navigate to Tab 3 (Blueprints) and browse catalog (Emergency Stop Latch, SPX Sort-to-Light, Motor Reverse, VFD Speed Control).
- **Step 2: Apply Template —** Click 'Apply Blueprint' to instantiate the complete network, automatic tags, and interlocks onto canvas without manual wiring from scratch.

---

# Part 4: Verification, Simulation & Industrial AI
*Canvas Testbench, Virtual I/O HUD & Senior Industrial AI Copilot*

### 4.1 Canvas-Centric Simulation Engine & Testbench
Simulation in SynaptiX IDE is canvas-centric. Pressing F5 transforms the editing canvas into an interactive testbench without leaving the design environment:

![Figure 4-1: Canvas-Centric Simulation Testbench — Floating I/O Board, Force Table & Cycle Trace](proposals/images/synaptix_simulator.png)
*Figure 4-1: Canvas-Centric Simulation Testbench — Floating I/O Board, Force Table & Cycle Trace*

| Simulation Element | Operational Capability |
| :--- | :--- |
| **Diagnostics Drawer (Bottom)** | Displays 10ms scan cycle timestamps, step events, execution timing, and cycle jitter traces. |
| **Virtual I/O Board HUD (Floating)** | Provides tactile DI switches, DO status LEDs, and analog sliders for interactive signal injection. |
| **I/O Watch & Force Table (Right)** | Monitors variable states in real time and allows forcing test values directly during simulation. |

- **Step 1: Start Simulation —** Press F5 or click Simulation Toggle. The canvas enters run mode with glowing active wires.
- **Step 2: Inject Discrete Inputs —** Click DI0 toggle switch on the floating Virtual I/O Board to simulate a proximity sensor trip.
- **Step 3: Inspect Execution —** Verify DO0 LED illuminates on the I/O HUD and trace log records 10ms scan execution in the bottom drawer.

### 4.2 Senior Industrial AI Copilot & Ghost Diff Engine
The Senior Industrial AI Copilot assists engineers in generating compliant automation circuits from natural language requirements while enforcing human verification via non-destructive Ghost Diff previews:

![Figure 4-2: Industrial AI Copilot Assistant — Natural Language Synthesis & Ghost Diff Verification](proposals/images/synaptix_ai_copilot.png)
*Figure 4-2: Industrial AI Copilot Assistant — Natural Language Synthesis & Ghost Diff Verification*

- **Step 1: Open Copilot Panel —** Click the AI Copilot button in the top-right ribbon to slide open the engineering assistant panel.
- **Step 2: Enter Prompt —** Type requirement (e.g. 'Design an E-Stop circuit with motor permissive under IEC 60204-1') and press Enter.
- **Step 3: Review Ghost Diff —** Observe synthesized blocks rendered as semi-transparent dashed outlines (Ghost Diff) directly on canvas.
- **Step 4: Approve Proposal —** Click 'Accept Proposal' to commit generated nodes into the project, or 'Reject' to discard safely.

---

# Part 5: Hardware Commissioning, Safe Deployment & Telemetry
*Device Diagnostics, RTC Timezone Sync & Two-Phase Safe Deploy Gate*

### 5.1 Target Controller Hardware Diagnostics & RTC Sync
Prior to deploying code to physical machinery, verify controller hardware inventory and calibrate the onboard DS3231 real-time clock:

![Figure 5-1: Target Controller Hardware Inventory and Firmware Diagnostics Dialog](proposals/images/synaptix_device_info.png)
*Figure 5-1: Target Controller Hardware Inventory and Firmware Diagnostics Dialog*

The Device Info dialog queries MCU serial number, firmware build, wire profile compliance, and active communication baud rate.

![Figure 5-2: Onboard Real-Time Clock (RTC) Timezone Synchronization Dialog](proposals/images/synaptix_rtc_sync.png)
*Figure 5-2: Onboard Real-Time Clock (RTC) Timezone Synchronization Dialog*

Click 'Sync RTC' to align the controller's onboard battery-backed DS3231 RTC chip with workstation UTC time and timezone offset, guaranteeing millisecond accuracy for telemetry logs.

### 5.2 SPLC-AF-003 Two-Phase Safe Deploy Gate & Hardware Capability Verification (0x0A08)
Direct memory writing during live machine operation risks severe relay chatter if communication drops mid-packet. SPLC-AF-003 eliminates this risk via a 2-Phase Staging Protocol combined with a Fail-Safe Hardware Capability Verification Gate:

![Figure 5-3: SPLC-AF-003 Two-Phase Safe Deploy Gate with CRC-16 Staging Commit](proposals/images/synaptix_deploy_gate.png)
*Figure 5-3: SPLC-AF-003 Two-Phase Safe Deploy Gate with CRC-16 Staging Commit*

- **Step 0: Hardware Capabilities Check (0x0A08) & Fail-Fast Gate:**
  - Prior to writing advanced configurations (MQTT PUB, MQTT SUB, MQTT Broker), the IDE issues a Modbus FC03 read for 2 holding registers at `0x0A08..0x0A09` to inspect hardware capability flags (`SPLC_CAP_MQTT_PUB = 0x0010`, `SPLC_CAP_MQTT_SUB = 0x0020`).
  - **Eliminating Silent Deployment Failures:** If the project contains MQTT blocks and reading `0x0A08` fails (Timeout, CRC, serial disconnection) or the target MCU does not assert the required capability bits, the IDE immediately **ABORTS DEPLOYMENT**, returns standard error code `UNSUPPORTED (0x0002)`, and reports Deploy FAILED. The IDE will never silently skip MQTT blocks while reporting false success.
  - **Backward Compatibility:** Legacy boards lacking register `0x0A08` (responding with Modbus Exception `0x02 Illegal Data Address`) continue to deploy base logic normally as long as no MQTT blocks are used in the project.
  - **Traceable Real-Time Logging:** The IDE logs each Modbus frame write before transmission (`[MQTT PUB] Writing 96 registers to 0x0C00...`, `[MQTT Broker] Writing 96 registers to 0x0E00...`) enabling 1-to-1 cross-audit with gateway firmware logs.
- **Step 1: Phase 1: Upload to Staging —** Transfer compiled 32-byte bytecode into an isolated microcontroller RAM staging buffer without affecting active control loops.
- **Step 2: Checksum Verification —** Controller computes CRC-16 checksum of staging buffer and verifies match with IDE transmission packet.
- **Step 3: Phase 2: Atomic Swap —** Upon confirmation, controller executes a sub-10ms atomic pointer swap precisely at scan cycle boundary.

### 5.3 Real-Time Telemetry & Live Watch Grid
Tab 5 (Live Watch & Diag) provides a real-time engineering telemetry grid polling all 128 tags at 200ms intervals with micro-sparkline trends and signal quality indicators:

![Figure 5-4: Real-Time Telemetry Live Watch Grid, Signal Quality Badges & Sparklines](proposals/images/synaptix_live_watch.png)
*Figure 5-4: Real-Time Telemetry Live Watch Grid, Signal Quality Badges & Sparklines*

| Telemetry Component | Technical Behavior |
| :--- | :--- |
| **Quality Badges (GOOD / STALE / BAD)** | Green = Fresh Modbus telemetry (<200ms); Amber = Stale data (timeout); Red = Comm error / CRC fault. |
| **Micro-Sparkline Waveform** | 60-sample historical rolling trend line displaying instantaneous signal dynamics. |
| **Manual Force Override** | Allows forcing DO outputs ON/OFF during commissioning. Indicated by prominent amber badges. |

> [!WARNING]
> **MANDATORY REMOVAL OF FORCE OVERRIDES BEFORE PRODUCTION**  
> Leaving physical outputs in a forced state bypasses safety interlocks and guard conditions. Always verify that all Forced tags are released to AUTOMATIC before enabling automated machine cycles.

---

# Part 6: IIoT Edge Connectivity & System Maintenance
*MQTT Broker Setup, Telemetry Publishing (PUB) & Fail-Safe Ingestion (SUB)*

### 6.1 Industrial MQTT Broker TCP/TLS Configuration
Select Device -> MQTT Broker Configuration (Ctrl+Shift+M) to establish communication with plant supervisory MQTT brokers:

![Figure 6-1: System MQTT Broker TCP/TLS Connection Configuration Dialog](proposals/images/synaptix_mqtt_broker.png)
*Figure 6-1: System MQTT Broker TCP/TLS Connection Configuration Dialog*

- **Step 1: Host & Service Port —** Specify Broker IP/Hostname and Port (port `1883` for plaintext TCP, or automatically toggled to port `8883` when enabling TLS encryption).
- **Step 2: TLS/SSL Encryption & X.509 Certificate Management —**
  - Checking `Use TLS / SSL Encryption (Port 8883)` activates the **3. TLS / SSL SECURITY & CERTIFICATES** section.
  - **Select Root CA Certificate (`.crt`, `.pem`, `.cer`)**: Click `Browse...` to load the enterprise or facility Root CA. The IDE decodes the ASN.1 X.509 structure and renders detailed certificate metadata:
    - *Issued by (Issuer)*: e.g., `CN=SPX-ROOT-CA-01`.
    - *Expires (Valid Until)*: Shows UTC expiration date/time.
    - *SHA-256 Fingerprint*: 32-byte colon-delimited hex digest for zero-trust certificate pinning and verification.
  - **Allow Self-Signed Certificates**: Check this option for internal LAN brokers with self-signed certificates or machines lacking a public FQDN.
  - **Chunked Struct Transfer Protocol (SPLC-SPEC-MQTT-003)**: Since ~1.8 KB PEM certificates exceed standard Modbus frame capacities, the IDE automatically slices the certificate into byte-exact **128-byte (64 Modbus registers)** structs (`SPLC_CertChunk_t`) staged at address `0x0F00`. Each chunk features independent CRC-16 Modbus verification, confirmed via ACK (`0x0F40`) before emitting an atomic commit command (`0x0F42` with magic `0xA5A5`).
- **Step 3: Identity & Authentication Credentials —** Enter the unique Client ID, Username, Password, and Keep-Alive heartbeat interval (default: 60s).
- **Step 4: Real-Time Handshake Test & Configuration Save —**
  - Click `Test Broker Connection`: Opens a live TCP socket and performs an authentic TLS handshake (`SslStream`) against the broker, returning negotiated protocol and cipher suite (e.g., `TLS handshake successful! Protocol: Tls13, Cipher: AesGcm256`).
  - Click `Save Configuration` to persist all parameters into the `.synx` project file.

### 6.2 Outbound Telemetry Publishing (MQTT PUB) & Visual Field Builder
Double-click an MQTT Publish node to open the Visual Field Builder. Map multiple internal tags into structured JSON payloads without writing code:

![Figure 6-2: Outbound Telemetry Publishing (MQTT PUB) Visual Field Builder Dialog](proposals/images/synaptix_mqtt_pub.png)
*Figure 6-2: Outbound Telemetry Publishing (MQTT PUB) Visual Field Builder Dialog*

Select publish trigger mode: Event-driven (on trigger pulse), Periodic (e.g. every 1000ms), or Change-of-Value (COV, publishes only when value exceeds deadband, conserving bandwidth).

### 6.3 Inbound Supervisory Command Ingestion (MQTT SUB)
The MQTT Subscribe block parses supervisory setpoints and commands from incoming JSON messages and maps them to dynamic output pins:

![Figure 6-3: Inbound Supervisory Command Ingestion (MQTT SUB) and Fail-Safe Schema Dialog](proposals/images/synaptix_mqtt_sub.png)
*Figure 6-3: Inbound Supervisory Command Ingestion (MQTT SUB) and Fail-Safe Schema Dialog*

The parser enforces an All-or-Nothing validation rule: if any incoming JSON field fails schema validation or is out-of-range, the entire packet is rejected without updating internal registers.

### 6.4 RS-485 Modbus RTU Master Polling & Sensor Ingestion (MB_READ)
The **`MB_READ`** block empowers SimplePLC to act as an autonomous Modbus RTU Master on the RS-485 bus, cyclically interrogating field instrumentation (temperature transmitters, power meters, variable frequency drives, level probes), parsing incoming binary frames, and presenting engineering values onto FBD logic pins:

![Figure 6-4: Modbus RTU Master Sensor Poller (MB_READ) & Live Frame Inspector Dialog](proposals/images/synaptix_modbus_read.png)
*Figure 6-4: Modbus RTU Master Sensor Poller (MB_READ) & Live Frame Inspector Dialog*

#### 1. MB_READ Canvas Node Connector Topology:
- **Input Pin `EN` (Enable):** Activation gate. When `EN` = TRUE, the PLC executes cyclic polling at the configured rate. When `EN` = FALSE, polling stops to yield the RS-485 bus.
- **Output Pin `Valid`:** Emits a single-scan 1-shot pulse upon receiving a valid response frame and successfully parsing all bound pins.
- **Output Pin `Err`:** Latches TRUE when communication fails (timeout, CRC mismatch, or slave exception).
- **Output Pin `Err_Code`:** Outputs industrial diagnostic error codes:
  - `0`: Communication healthy (`ERR_NONE`).
  - `1`: Response timeout (`ERR_TIMEOUT` — broken cable, incorrect baud rate, or wrong slave ID).
  - `2`: Integrity failure (`ERR_CRC` — electromagnetic interference on RS-485).
  - `3`: Slave exception (`ERR_EXCEPTION` — illegal address or unsupported function code).
  - `4`: Malformed payload (`ERR_MALFORMED`).
- **Dynamic Output Pins:** Up to 4 pins per block with custom technical labels (`Temp`, `Humidity`, `Current`, `Frequency`), streaming converted numerical values directly into downstream FBD blocks.

#### 2. Modbus.org Standardized Configuration Parameters:
- **Slave ID (1..247):** Target hardware station address on the RS-485 multidrop bus.
- **Function Code:** Selects the standard Modbus read function:
  - `FC 03`: Read Holding Registers (4x Table — VFDs, Process Controllers).
  - `FC 04`: Read Input Registers (3x Table — Analog Transmitters, Power Meters).
  - `FC 01`: Read Coils (0x Table — Digital Relays).
  - `FC 02`: Read Discrete Inputs (1x Table — Digital Switches).
- **Starting Address:** Register start index on the slave instrument (supports 0-based and 1-based addressing).
- **Quantity of Registers:** Number of registers/coils requested per query (1..125). Features **Smart Auto-Fit**: users can enter any custom span, and the IDE automatically expands the quantity if defined pin offsets exceed the current range.
- **Poll Interval (ms) & Timeout (ms):** Query repetition interval (default 500ms) and maximum wait duration before triggering `ERR_TIMEOUT` (default 1000ms).

#### 3. Dynamic Pin Bindings & Transformation:
- **Reg Offset:** Relative zero-based offset from `Starting Address`.
- **Data Type:** Comprehensive type support: `INT16`, `UINT16`, `REAL` (Float32 IEEE 754 — 2 registers), `INT32`, `UINT32`, and `BOOL`.
- **Endianness (Byte Order):** Full coverage for all 4 industrial packing conventions:
  - `Big-Endian (ABCD)`: Standard network byte order.
  - `Word-Swap (CDAB)`: Dominant standard for PLC Modbus sensors and energy meters.
  - `Byte-Swap (BADC)`: Internal byte swap.
  - `Little-Endian (DCBA)`: Native x86/ARM format.
- **Multiplier:** Linear scaling factor applied as $V_{\text{final}} = V_{\text{raw}} \times \text{Multiplier}$ (e.g. integer `250` scaled by `0.1` becomes $25.0^\circ\text{C}$).

#### 4. Interactive Live Frame Inspector:
- Engineers can paste real hex response frames captured from field sensors (e.g. `01 03 04 00 FA 03 E8 B9 AF`) or click **"Sample Frame"**.
- Clicking **"Test Frame Decoding"** executes the CRC-16 checksum validator, register unpacker, and endianness converters in real time, displaying instant pin readouts to verify communication settings prior to PLC deployment.

---

### 6.5 RS-485 Modbus RTU Master Actuator Controller (MB_WRITE)
The **`MB_WRITE`** block (badge `MB_WRITE`) enables SimplePLC to act as an active Modbus RTU Master, transmitting command and parameter frames over RS-485 to external field actuators (Variable Frequency Drives, Sort-to-Light smart LED indicators, multi-channel relay modules, Modbus control valves). The block complies with international standards **PLCopen TC5 Communication** and **IEC 61131-3**.

> [!TIP]
> **Detailed Engineering Manual:** Refer to the comprehensive manual at [`docs/manuals/MB_WRITE_FUNCTION_BLOCK_USER_MANUAL.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/manuals/MB_WRITE_FUNCTION_BLOCK_USER_MANUAL.md) for complete application case studies, formulas, and telegram frame breakdowns.

#### 1. MB_WRITE Canvas Connector Topology:
- **Event Input Pin `Trig`:** Execution trigger pulse (only visible on Canvas when `TriggerMode = OnTriggerRisingEdge`).
- **Data Input Pins:**
  - *Channel-Indexed Mode:* `Channel` (target index $1..N$), `Command` (action code), `Param` (24-bit RGB color code or extended parameter).
  - *Direct Registers Mode:* Dynamic data input pins `IN0`..`IN3` bound to runtime values to write directly into slave registers.
- **Handshaking Status Output Pins:**
  - **`Done`:** Emits a single-scan 1-shot pulse ($10\text{ms}$) upon receiving positive acknowledgment echo from the target slave.
  - **`Busy`:** Latches `TRUE` throughout the UART RS-485 frame dispatch and response reception window. Serves as an interlocking guard flag.
  - **`Error`:** Latches `TRUE` upon transmission failure (timeout, CRC corrupted, or slave exception).
  - **`ErrCode`:** Standard diagnostic error code:
    - `0`: Success (`ERR_NONE`).
    - `1`: Response timeout (`ERR_TIMEOUT` — actuator unpowered or cable broken).
    - `2`: Integrity failure (`ERR_CRC` — electromagnetic noise).
    - `3`: Slave exception (`ERR_EXCEPTION` — illegal register or value).
    - `4`: Bus arbitration collision (`ERR_BUS_BUSY`).
    - `5`: Channel out of bounds (`ERR_CHANNEL_OUT_OF_BOUNDS`).

#### 2. Dual Industrial Operating Modes (Write Modes):
1. **Direct Registers Mode:**
   - Supports 4 standard Modbus function codes:
     - `FC 05`: Write Single Coil (1-bit digital state).
     - `FC 06`: Write Single Register (16-bit word — VFD frequency, setpoints).
     - `FC 15`: Write Multiple Coils.
     - `FC 16`: Write Multiple Registers.
   - Configurable `Start Address` and `Point Count`, type conversion (`INT16`, `UINT16`, `REAL` Float32 IEEE 754, `INT32`, `UINT32`, `BOOL`), endianness swapping (`ABCD`, `CDAB` Word-Swap, `BADC`, `DCBA`), and inverse scaling factor (`Multiplier`).
2. **Channel-Indexed Target Mode (Optimized for E-Commerce Logistics):**
   - Engineered specifically for distributed multi-channel actuators such as **Sort-to-Light racks (1..18 bins)**, pneumatic manifold banks, or zoned LED indicators.
   - Configurable `Base Command Address` (e.g. `0x0100`) and `Base Param Address` (e.g. `0x0120` for 24-bit RGB colors).
   - Automatically dispatches **FC 06** for Command and **FC 16** for 32-bit Param.
   - **Exclusive Channel Arbiter (`ExclusiveChannel`):** When enabled, activating any single bin automatically clears/extinguishes all other bins, eliminating fulfillment operator picking errors.

#### 3. Execution Trigger Policies & Deadband Filter:
- **On Trigger Rising Edge (Event-driven):** Dispatches write PDU strictly on the $0 \rightarrow 1$ transition of the `Trig` pin (ideal for barcode scan events, drop confirmations, pushbuttons).
- **Cyclic (Periodic Heartbeat):** Dispatches periodic write telegrams at `CyclicIntervalMs` (50..60000ms) intervals, ideal for actuator watchdog heartbeats.
- **On Data Change with COV Deadband ($\Delta$):** Dispatches write PDU immediately when input values change by more than `Deadband` ($|\text{Val} - \text{Val}_{\text{last}}| \ge \text{Deadband}$). Prevents RS-485 bus saturation caused by continuous ADC/analog fractional noise.

#### 4. ISA-101 Ergonomic HMI Experience:
- **Sidebar Inspector (~230px):** Serves as a clean read-only summary card displaying Slave ID, Write Mode, Trigger Mode summary (e.g. `Cyclic (500 ms)` or `On Data Change (Δ0.50)`), pin count, and a prominent `[ ⚙ Configure... ]` button.
- **Popup Configuration Dialog:** Dedicated workspace offering an automated Actuator Channel Mapping Table and an interactive Modbus RTU Frame Inspector with CRC-16 breakdown.

---

### 6.6 High-Speed Pulse Generator & Actuator Controller (PULSE_OUT)
The **`PULSE_OUT`** block (visual badge `PULSE`) operates as a specialized hardware function block (Special Function / Pulse Generator) enabling SynaptiX IDE to command high-speed waveforms and low-level actuators directly on the controller's 8 onboard outputs (`DO0..DO7`) that exceed ordinary 10ms PLC scan cycle capabilities: high-frequency PWM (1..20 kHz), calibrated PTO pulse trains for stepping motors, addressable LED serial bus drivers (UCS16703/WS2812B), or edge-triggered digital pulse dispatches:

#### 1. PULSE_OUT Canvas Connector Topology:
- **Input Pin `Trig` (Event Input):** Execution trigger pulse (rising edge from `MQTT_SUB`, photoelectric sensors `DI`, or upstream logic).
- **Input Pin `In` / `Chan` (Data Input):** Logic value In, PWM duty percentage, PTO pulse count, or target channel index (1..18).
- **Input Pin `En` / `Param` (Data Input):** Pulse enable signal En, output frequency Freq (Hz), or 24-bit RGB color code for addressable LEDs.
- **Output Pin `Done` (Event Output):** Emits a single-scan 1-shot pulse upon completing hardware pin transmission.
- **Output Pin `Err` (Event Output):** Latches TRUE upon parameter out-of-range, channel saturation, or hardware conflict.

#### 2. Target Hardware Pin Selection (Target Pin):
The `PULSE_OUT` block maps directly to physical controller output terminals:
- **`DO0` .. `DO7`:** Physical digital output terminals on the controller chassis (Transistor/MOSFET/Relay). The platform strictly enforces standard industrial identifiers `DO0..DO7`.

#### 3. Four Operating Actuator Modes:
1. **Digital Output (Direct Logic DO):**
   - Direct ON/OFF control for relays, solenoids, indicator lamps, and acoustic alarms.
   - Fully supports configurable logic polarity (`ActiveHigh`) and fail-safe state (`FailSafeState`).
2. **Pulse-Width Modulation (PWM Duty Cycle):**
   - Continuously modulates duty cycle (0..100%) at user-defined `PwmFrequencyHz` (e.g., 1,000 Hz) for DC dimming, proportional valves, or motor speed regulation.
3. **Pulse Train Output (PTO):**
   - Emits an exact count of calibrated square pulses with microsecond duration `PulseWidthUs` ($\mu s$) for stepper motor positioning or dosing actuation.
4. **Addressable Actuator Bus:**
   - Directly drives serial addressable LED strips or 1-wire smart actuator banks (WS2812B, UCS16703).
   - Configurable channel capacity (`ChannelCount`, e.g., 18 bins in Sort-to-Light logistics).
   - **Exclusive Channel Arbiter (`ExclusiveChannel`):** When enabled, illuminating a newly selected bin automatically extinguishes all other bins.
   - **Default Color (`DefaultColorRgb`):** Fallback 24-bit RGB color (e.g., `0x00FF00` - Forest Green).

#### 4. Execution Trigger Policies:
- **On Trigger Rising Edge (Default):** Dispatches hardware waveform strictly on the $0 \rightarrow 1$ transition of the `Trig` pin.
- **On Data Change:** Dispatches hardware waveform immediately when `Chan` or `Param` values change.
- **Continuous:** Continuously drives target terminal outputs as long as `Trig` remains logic TRUE.

---

### 6.7 Wireless Wi-Fi Communication Configuration (Wi-Fi Configuration Dialog)
Select **Device -> Wi-Fi Configuration... (Ctrl+Shift+W)** from the top menu to open the unified wireless network management dialog:

- **1. Live Network Telemetry:**
  - Real-time telemetry monitoring (1500ms poll cycle): Connection Status (`DISCONNECTED`, `CONNECTING`, `CONNECTED`, `GOT_IP`, `AUTH_FAILED`), Radio Signal Strength (`RSSI dBm` with visual strength bar), Assigned IPv4 Address, and Network Interface Physical MAC Address.
- **2. AP Scanning & Network Credentials:**
  - Click **"Scan"**: Triggers an asynchronous, non-blocking background scan (FreeRTOS Worker Task) on the controller without interrupting the real-time 10ms PLC scan cycle.
  - The nearby network table displays up to 8 discovered Access Points with signal strength (dBm) and security type (WPA2/WPA3).
  - Double-click or select any discovered network row to automatically populate the **SSID** input field.
  - Enter the network security **Password** (WPA2/WPA3).
- **3. IP Addressing Mode:**
  - **Automatic IP (DHCP):** Enabled by default; the controller obtains an IPv4 lease automatically from the plant network router or DHCP server.
  - **Static IP:** Disable DHCP to configure deterministic Static IP, Subnet Mask, Default Gateway, and Primary DNS for isolated OT/SCADA automation subnets.
- **4. Operational Controls:**
  - Click **"Save & Connect"**: Writes configuration parameters to holding registers `0x1300`, then issues command `0x0002` (`CONNECT`) to `0x1340`. Upon successful link negotiation and IP acquisition, credentials are saved to non-volatile Flash/NVS memory for automatic power-loss recovery.
  - Click **"Disconnect"**: Issues command `0x0003` (`DISCONNECT`) to `0x1340` to cleanly tear down the association.

---

### 6.8 Diagnostics Fault Directory & Safety Mandate
Refer to this diagnostic directory to rapidly resolve common field issues encountered during machine commissioning:

| Fault Symptom | Probable Root Cause | Recommended Engineering Action |
| :--- | :--- | :--- |
| **COM Port Not Detected** | USB cable loose or USB CDC driver missing. | Verify cable; inspect Device Manager under Ports (COM & LPT); re-plug. |
| **COM Port Access Denied** | Serial port locked by external terminal (PuTTY). | Close background terminal utilities or conflicting serial debuggers. |
| **Deploy FAILED: Hardware does not support MQTT** | Logic diagram contains MQTT blocks but MCU firmware has not enabled `SPLC_CAP_MQTT_PUB` (`0x0010`) or `SPLC_CAP_MQTT_SUB` (`0x0020`) in register `0x0A08`. | Update gateway firmware with MQTT build enabled (`MQTT=1`), or verify register `0x0A08` implementation on the MCU. |
| **Deploy Read 0x0A08 Error: Timeout / CRC** | RS-485 line noise, disconnected wire, or gateway response exceeded timeout limit. | Check RS-485 A/B lines, 120Ω termination resistors and baud rate; check detailed `[CAPS]` entries in Deploy tab log. |
| **Red AST Error Badge on Canvas** | Unconnected input pin or cyclic feedback loop. | Locate red badge; ensure all mandatory block pins are wired orthogonally. |
| **Physical DO Fails to Energize** | Guard condition FALSE or output is Forced OFF. | Inspect Live Watch; verify series permissive guards; clear Force latches. |
| **Analog Value Jitters / Fluctuates** | Industrial EMI noise or unshielded cable. | Use twisted-pair shielded cables (STP); increase Dwell debounce time. |
| **Retained Value Cleared on Boot** | Tag was assigned to volatile VREG instead of RETAIN. | Reassign tag to RETAIN0..31 in Tag Catalog and re-deploy bytecode. |

> [!DANGER]
> **HARDWIRED SAFETY CIRCUIT MANDATE (IEC 60204-1)**  
> Under international standard IEC 60204-1, software-based controllers must NEVER be used as the sole safety mechanism for Emergency Stop functions or Personnel Protection Guards. Always wire Emergency Stop buttons through certified electromechanical safety relays.

---

# Part 7: Example Projects Library & Industrial Case Studies
*Real-World SPX Logistics Hub, Closed-Loop VFD Pressure Control & GSP Cold Chain*

Starting with v2.6.0, SynaptiX IDE ships with an industrial example library located in `examples/`, verified against 100% test coverage:

### 7.1 SPX Express: Sort-to-Light 18-Bin Parcel Sorting Hub
- **Project File:** `examples/SPX_Sort_To_Light_Logistics_Center.synx`
- **Application Scope:** High-speed parcel sorting at SPX Express Sorting & Fulfillment Centers (Hanoi SOC Hub).
- **Architecture Integration:**
  - **`MQTT_SUB` Block:** Ingests dispatch commands from warehouse WMS over `spx/hub_hn/rack_01/put_command`, unpacking `bin_id` (1..18) into `VREG0` and `color_rgb` into `VREG1`.
  - **`PULSE_OUT` Block (backward compatible with `DIRECT_WRITE`) (Terminal DO3, Addressable Bus):** Directly commands 18-bin addressable RGB LED indicator strips (UCS16703/WS2812B) from digital output terminal `DO3` without requiring Modbus Slave IDs or auxiliary driver boards, asserting `ExclusiveChannel = True` to clear all other 17 bins whenever an active bin changes.
  - **`TON` Block (15s Watchdog):** Drop timeout supervisor; illuminates red tower beacon (`DO3: TOWER_RED_ALARM`) if an operator fails to drop the parcel within 15 seconds.
  - **Photoelectric Drop Sensor (`DI1: PE_BIN_CONFIRM`):** Mounted at the chute opening to register parcel entry via rising-edge pulse.
  - **`CTU` Block (Shift Sorter Counter):** Tracks cumulative processed parcel volume per shift into non-volatile memory `VREG2`.
  - **`MQTT_PUB` Block:** Dispatches JSON completion telemetry back to the WMS on `spx/hub_hn/rack_01/feedback`.

### 7.2 Closed-Loop VFD Pressure Booster Pump Station
- **Project File:** `examples/Modbus_RTU_Master_VFD_And_Sensor_Loop.synx`
- **Application Scope:** Municipal and industrial water pressure boosting station.
- **Architecture Integration:**
  - **`MB_READ` Block:** Interrogates pipe pressure transmitter via RS-485 Modbus RTU (FC04 Input Register).
  - **`SCALE` Block:** Scales raw integer data to engineering units (`bar`, $0.0..10.0\text{ bar}$, $k = 0.01$).
  - **`MB_WRITE` Block (Direct Registers):** When pressure sags below setpoint and reservoir level is verified (`DI1: DRY_RUN_SENSOR = 1`), writes 50.00 Hz command to VFD register 40001 (FC06).

### 7.3 GSP-Compliant Pharmaceutical Cold Chain Smart Gateway
- **Project File:** `examples/IIoT_Cold_Chain_Smart_Gateway.synx`
- **Application Scope:** Real-time continuous thermal monitoring of pharmaceutical vaccine storage chambers (-20°C).
- **Architecture Integration:**
  - Reads precision PT100 temperature sensor on `AI0` and relative humidity on `AI1`.
  - **`MQTT_PUB` Block (Cyclic Mode):** Publishes 30-second ISO 8601 UTC timestamped telemetry payloads with QoS 1 and `Retain` flag to SCADA Cloud.
  - **`MQTT_SUB` Block:** Receives live high-temperature threshold overrides (`temp_high_limit`) dynamically from supervisors without halting PLC scan loops.
  - Latches emergency siren (`DO2: TEMP_ALARM_SIREN`) if temperature exceeds threshold for $> 3\text{ seconds}$.

### 7.4 Opening and Running Simulations on Canvas
1. In SynaptiX IDE, select **File** $\rightarrow$ **Open Project...** (`Ctrl + O`).
2. Navigate to `examples/` and open any `.synx` project file.
3. Observe the orthogonal 90° Manhattan wiring layout and node technical labels.
4. Press `F5` or click **Simulate** to launch real-time simulation; observe live signal propagation across connectors.

---

## Technical Appendices

### Appendix A: Complete Modbus RTU / TCP Register Map
All holding registers are accessible using Modbus FC03 (Read) and FC16 (Write Multiple Registers). Word order for 32-bit values is Big-Endian standard:

| Modbus Register | Tag Assignment | Data Format & Width | Access Mode |
| :--- | :--- | :--- | :--- |
| **0x0900 - 0x0907** | DI0 - DI7 | Discrete Inputs (Bit 0 of 16-bit word) | Read-Only (FC03) |
| **0x0908 - 0x090F** | DO0 - DO7 | Discrete Outputs (Bit 0 of 16-bit word) | Read/Write (FC03/FC16) |
| **0x0910 - 0x0917** | AI0 - AI3 | Analog Inputs (Float32, 2 registers per channel) | Read-Only (FC03) |
| **0x0918 - 0x0957** | VREG0 - VREG31 | Virtual Calculation Registers (Int32 / Float32) | Read/Write (FC03/FC16) |
| **0x0958 - 0x0997** | RETAIN0 - RETAIN31 | Non-Volatile Flash Registers (Int32 / Float32) | Read/Write (FC03/FC16) |
| **0x0998 - 0x09B7** | VFLAG0 - VFLAG31 | Internal State Flags (Bit 0 of 16-bit word) | Read/Write (FC03/FC16) |

### Appendix B: SPLC-AF-003 Binary Bytecode Specification
Each compiled rule record consists of exactly 32 contiguous bytes: Header (4 bytes), Trigger Spec (8 bytes), Guard Spec (8 bytes), Action Spec (8 bytes), and CRC-16 Checksum (2 bytes) + Reserved (2 bytes). This fixed record structure guarantees deterministic O(1) microcontroller execution without runtime interpretation.

### Appendix C: Keyboard Shortcuts & Engineering Quick Reference

| Keystroke | Engineering Function | Scope / Context |
| :--- | :--- | :--- |
| **Ctrl+N** | Create New Automation Project Wizard | Global Application |
| **Ctrl+S** | Save Current Project (.synx) | Global Application |
| **Ctrl+1 .. Ctrl+6** | Direct Navigation to Workspace Tabs (1:Canvas, 2:Tags, etc.) | Global Navigation |
| **F4** | Toggle Connect / Disconnect Target Controller | Device Connection |
| **F5** | Toggle Canvas-Centric Simulation Engine | Simulation Testbench |
| **Ctrl+F5** | Launch SPLC-AF-003 Safe Deploy Gate | Deployment Gate |
| **Alt+G** | Snap All Canvas Nodes to 20px Grid | Logic Editor Canvas |
