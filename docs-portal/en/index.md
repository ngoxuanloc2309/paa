# SynaptiX Programming Guide

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-PROG-000</span>
  <span><strong>Software Platform:</strong> SynaptiX IDE (.NET 8 Engineering Environment)</span>
  <span><strong>Audience:</strong> System Integrators, Automation &amp; Embedded Engineers</span>
  <span><strong>Standard:</strong> IEC/IEEE 82079-1 &bull; ANSI/ISA-101</span>
</div>

Welcome to the official **SynaptiX Programming Guide** — the comprehensive engineering reference and system integration manual for the **SynaptiX Edge Automation & IIoT Ecosystem**.

Unlike fragmented configuration utilities that separate local control logic from fieldbus polling and cloud telemetry, **SynaptiX IDE** integrates visual graph authoring, strict physical type checking, cycle-accurate offline simulation, atomic flash deployment, and live 20 FPS telemetry diagnostics inside a single deterministic workbench.

![SynaptiX IDE Main Engineering Workbench](/images/synaptix_main_anatomy.png)

---

## Documentation Reading Path

This manual is organized sequentially into **six progressive chapters** in the left navigation tree, taking you from foundational architecture through installation, workspace mastery, block-by-block specifications, and system integration:

<div class="wiki-chapter-grid">
  <a class="wiki-card" href="./concepts/ecosystem-overview">
    <span class="wiki-card-step">CHAPTER 01</span>
    <h3>Introduction &amp; Core Concepts</h3>
    <p>Understand the 7 built-in SynaptiX device presets, physical memory tag model (BOOL, INT32, REAL), Bad-Data Defense, and deterministic rule-plus-block execution pipeline.</p>
  </a>
  <a class="wiki-card" href="./getting-started/installation">
    <span class="wiki-card-step">CHAPTER 02</span>
    <h3>Installation &amp; Quickstart</h3>
    <p>Install SynaptiX IDE on Windows 10/11, configure USB CDC &amp; TCP transports, tour the user interface, and commission your first project in 10 minutes.</p>
  </a>
  <a class="wiki-card" href="./workspaces/device-tab">
    <span class="wiki-card-step">CHAPTER 03</span>
    <h3>IDE Workspaces (7 Tabs)</h3>
    <p>In-depth operational reference for every main tab (Ctrl+1 to Ctrl+7): Device, Logic Graph, Tag Catalog, Rule Table, Blueprints, Deploy, and Live Watch.</p>
  </a>
  <a class="wiki-card" href="./blocks/overview">
    <span class="wiki-card-step">CHAPTER 04</span>
    <h3>Function Blocks Reference</h3>
    <p>Dedicated datasheet specifications for all 18 visual blocks: Core Rule Pipeline, Timers, Counters, Linear Scale, Modbus RTU Master, Direct Hardware Output, MQTT, HTTP, and TCP.</p>
  </a>
  <a class="wiki-card" href="./tools/rs485-and-scada-slave">
    <span class="wiki-card-step">CHAPTER 05</span>
    <h3>Connectivity, DTU &amp; Tools</h3>
    <p>Configure RS-485 buses, poll the Fixed SCADA Modbus Slave Map, provision MQTT TLS &amp; Wi-Fi, operate DTU Gateway Studio, simulate offline, and leverage AI Copilot.</p>
  </a>
  <a class="wiki-card" href="./appendices/keyboard-shortcuts">
    <span class="wiki-card-step">CHAPTER 06</span>
    <h3>Quick Reference Appendices</h3>
    <p>Instant lookup tables for global &amp; canvas keyboard shortcuts, payload template macro tokens ({val1:N}, {ts}, {device}), resource limits, and runtime error codes.</p>
  </a>
</div>

---

## End-to-End Engineering Workflow

<div class="synx-workflow-grid">
  <div class="synx-workflow-card">
    <div class="synx-workflow-num">1</div>
    <h4 class="synx-workflow-title">Select Target Device Preset</h4>
    <p class="synx-workflow-desc">Initialize a new project or auto-detect physical target hardware via local USB CDC or industrial Ethernet.</p>
  </div>
  <div class="synx-workflow-card">
    <div class="synx-workflow-num">2</div>
    <h4 class="synx-workflow-title">Define Physical &amp; Virtual Tags</h4>
    <p class="synx-workflow-desc">Provision the 128-tag global memory space (DI, DO, AI, VREG) inside Tag Catalog (<kbd>Ctrl+3</kbd>) with strict physical types.</p>
  </div>
  <div class="synx-workflow-card">
    <div class="synx-workflow-num">3</div>
    <h4 class="synx-workflow-title">Wire Control &amp; IIoT Blocks</h4>
    <p class="synx-workflow-desc">Drag and drop 18 visual function blocks, Modbus RTU masters, and MQTT telemetry streams on Logic Graph (<kbd>Ctrl+2</kbd>).</p>
  </div>
  <div class="synx-workflow-card">
    <div class="synx-workflow-num">4</div>
    <h4 class="synx-workflow-title">Verify Offline &amp; Compile</h4>
    <p class="synx-workflow-desc">Validate cycle timing in the cycle-accurate Simulator (<kbd>F5</kbd>) and perform pre-flight memory checks during compilation (<kbd>F7</kbd>).</p>
  </div>
  <div class="synx-workflow-card">
    <div class="synx-workflow-num">5</div>
    <h4 class="synx-workflow-title">Two-Phase Atomic Commit Deploy</h4>
    <p class="synx-workflow-desc">Commit bytecode safely via Deploy (<kbd>Ctrl+F5</kbd>) with SHA/CRC verification and glitch-free firmware flashing.</p>
  </div>
  <div class="synx-workflow-card">
    <div class="synx-workflow-num">6</div>
    <h4 class="synx-workflow-title">Observe &amp; Commission at 20 FPS</h4>
    <p class="synx-workflow-desc">Monitor real-time live waveforms inside Live Watch (<kbd>Ctrl+7</kbd>) and perform tactile tag forcing (<kbd>F8</kbd>).</p>
  </div>
</div>

## Key Architectural Pillars of SynaptiX IDE

| Pillar | Engineering Guarantee | Where to Learn More |
| :--- | :--- | :--- |
| **Visual Truth & Type Integrity** | Every input field enforces strict physical memory contracts (`BOOL`, `INT32`, IEEE-754 `REAL`) with automatic normalization and invalid-input auto-revert upon commit. | [Tag Architecture & Data Types](./concepts/data-model-and-types) |
| **Hybrid Deterministic Engine** | Combines sub-millisecond 4-stage Event-Condition-Action rules (up to 100 rules) with stateful timers, counters, linear calibrators, and asynchronous fieldbus/cloud workers. | [Deterministic Execution Pipeline](./concepts/execution-pipeline) |
| **18 Self-Contained Function Blocks** | Zero external scripting required — native blocks cover edge detection, RTC shift windows, Modbus RTU Master read/write, PWM/PTO hardware pins, MQTT JSON builders, HTTP REST, and raw TCP streams. | [Function Blocks Catalog](./blocks/overview) |
| **Zero-Hardware Offline Verification** | Cycle-accurate desktop simulation (`F5`) and inbound JSON payload injection let engineers validate 100% of logic before connecting to physical hardware. | [Simulator & Online Live Debug](./tools/simulator-and-live-debug) |
| **Safe Atomic Deployment** | Two-phase flash staging with pre-flight validation ensures power loss or cable disconnection during transfer never leaves an edge controller in a corrupted state. | [Atomic Deploy Workspace](./workspaces/deploy-tab) |

::: tip Instant Bilingual Support
Both **SynaptiX IDE** and this documentation portal support instant switching between **English** and **Tiếng Việt** (use the language menu in the top-right navigation bar).
:::
