# 3. Tag Catalog (`Ctrl+3`)

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-WS-003</span>
  <span><strong>Workspace:</strong> Tab 3 &bull; Shortcut <code>Ctrl+3</code></span>
  <span><strong>Scope:</strong> Physical &amp; Virtual Tag Architecture, IEC 61131-3 Data Types, Bad-Data Defense &amp; CSV Bulk Provisioning</span>
  <span><strong>Standard:</strong> IEC 61131-3 &bull; IEC/IEEE 82079-1 &bull; ANSI/ISA-101</span>
</div>

---

## 1. Purpose & Operational Role

The **Tag Catalog** (`Ctrl+3`) is the single source of truth for memory allocation, signal naming, data-type contracts, and SCADA register mapping across a **SynaptiX IDE** project. Every physical terminal, internal relay flag, volatile calculation register, non-volatile Flash retentive setpoint, and event counter referenced by the [Logic Graph Canvas](/workspaces/logic-graph-tab), [Rule Table](/workspaces/rule-table-tab), and [Live Watch](/workspaces/live-watch-tab) originates in this workspace.

Architecturally, the **Tag Catalog** manages **up to 128 user-configurable runtime tags** (supplemented by **8 read-only deterministic system tags**) and governs four core engineering responsibilities:

1. **Hardware-Synchronized Memory Partitioning:** Organizing physical I/O banks (`DI`, `DO`, `AI`), volatile internal memory (`VFLAG`, `VREG`), Flash-backed retentive storage (`VREG_RETAIN`), and hardware-assisted counters (`COUNTER`) according to the active **SynaptiX Edge Controller** profile.
2. **Strict IEC 61131-3 Data-Type Contracts:** Assigning deterministic memory representations (`BOOL`, `INT32`, or IEEE-754 `REAL`) to multi-word registers while enforcing **Bad-Data Defense** on every initial/preset value input.
3. **Human-Readable Aliasing & SCADA Address Transparency:** Documenting plant P&ID instrument tags via inline editable **Aliases** alongside automatically calculated **Modbus Slave Map** addresses for seamless HMI/SCADA integration.
4. **High-Speed Bulk Engineering via UTF-8 CSV:** Exporting and importing complete tag dictionaries (`*.csv` with UTF-8 BOM) so electrical and instrumentation teams can maintain hundreds of I/O descriptions in Microsoft Excel or version control.

---

## 2. Visual Layout & Controls Reference

![SynaptiX Tag Catalog Workspace](/images/synaptix_tag_catalog.png)

Designed per IBM Carbon industrial data-table density guidelines (`28–32 px` compact rows, left-aligned identifiers, monospace right-aligned addresses/values, center-aligned badges), the **Tag Catalog** consists of a filter toolbar, a collapsible **Tag Group Reference Card**, and the primary **Tag DataGrid**.

### 2.1. Tag Memory Categories & Hardware Capacities

Filtering by any category pill updates the table and reveals an inline engineering card detailing field wiring meaning, rule role, and hardware behavior:

| Category Pill | Memory Persistence | Access | Supported Data Types | Field & Runtime Engineering Role |
| :--- | :--- | :--- | :--- | :--- |
| **`DI`** *(Discrete Input)* | Physical Terminal | Read-Only | `BOOL` | `24 VDC` opto-isolated digital field sensors (limit switches, proximity sensors, pushbuttons, E-Stop contacts). |
| **`DO`** *(Discrete Output)* | Physical Terminal | Read / Write | `BOOL` | Relay or high-speed transistor actuator outputs driving contactors, solenoid valves, beacons, or pulse trains. |
| **`AI`** *(Analog Input)* | Physical Terminal | Read-Only | `REAL` / `INT32` | `4–20 mA` or `0–10 V` 12-bit ADC instrumentation channels for pressure, temperature, level, and flow transmitters. |
| **`VFLAG`** *(Virtual Flag)* | Volatile SRAM | Read / Write | `BOOL` | Internal auxiliary relay coils for latching states, cascade permissions, alarm flags, and MQTT command bits. |
| **`VREG`** *(Virtual Register)* | Volatile SRAM | Read / Write | `INT32` or `REAL` | 32-bit internal registers for scaled engineering values, RS-485 polled telemetry, and timer/math intermediates. |
| **`VREG_RETAIN`** *(Retentive)* | Non-Volatile Flash | Read / Write | `INT32` or `REAL` | Power-fail-safe Flash registers (rated for over 100,000 write cycles) preserving recipe setpoints, calibration offsets, and shift totals across blackouts. |
| **`COUNTER`** *(Event Counter)* | SRAM / Retentive Sync | Read / Write | `INT32` / `REAL` | Dedicated high-integrity accumulators driven by `INC_COUNTER`, `CTU`, and `CTD` blocks for batch and part counting. |
| **`SYSTEM`** *(System Tags)* | Firmware Internal (`1000..1007`) | Read-Only | `BOOL` / `INT32` | 8 built-in deterministic runtime signals: `SYS_FIRST_SCAN`, `SYS_ON`, `SYS_OFF`, `SYS_CLOCK_1S`, `SYS_CLOCK_100MS`, `SYS_RTC_HOUR`, `SYS_RTC_MINUTE`, and `SYS_COMM_OK`. |

### 2.2. Tag Catalog DataGrid Columns & Toolbar Reference

| Control / Column | Alignment & Widget | Operational Behavior |
| :--- | :--- | :--- |
| **Category Filter Pills** | Top Filter Bar | Filters the 128-tag catalog by `ALL`, `DI`, `DO`, `AI`, `VFLAG`, `VREG`, `VREG_RETAIN`, `COUNTER`, or `SYSTEM` with live tag counts per bank. |
| **Quick Search Box** | Top Toolbar | Filters rows instantaneously by `Tag Name`, `Alias`, `Group`, `Modbus Address`, or `Data Type`. |
| **Import CSV / Export CSV** | Top Toolbar & `File` Menu | Reads or writes UTF-8 (with BOM) CSV tag tables for spreadsheet bulk editing and cross-project migration. |
| **`Index`** | Right-Aligned Monospace | Zero-based deterministic runtime tag index (`0..127` for user tags; `1000..1007` for `SYSTEM` tags). |
| **`Tag Name`** | Left-Aligned Code Pill | Immutable canonical hardware/memory identifier (for example, `DI0`, `DO3`, `AI1`, `VREG0`, `VREG_RETAIN0`). |
| **`Modbus Address`** | Right-Aligned Monospace Pill | Displays the exact SCADA Slave Map coil/register address exposed over RS-485 / Modbus TCP. |
| **`Data Type`** | Center-Aligned Badge / Dropdown | Fixed `BOOL` badge on digital banks (`DI`, `DO`, `VFLAG`); interactive `INT32` / `REAL` selector on configurable register banks (`VREG`, `VREG_RETAIN`). |
| **`Group`** | Editable Category Cell | Logical plant subsystem label (for example, `Boiler_Zone_1`, `Conveyor_A`, `HVAC`) for structured organization. |
| **`Alias`** | Inline Editable Text Cell | Descriptive engineering tag name (for example, `Pump_1_Overload_OK` or `Tank_Level_Meters`) propagated across all workspaces. |
| **`Value`** | Monospace Input + `0↔1` Toggle | Displays the committed initial/simulation value with an LED state dot (`BOOL` tags include a type-safe `0↔1` toggle button; numeric cells enforce Bad-Data Defense). |

---

## 3. Data-Type Contracts, Bad-Data Defense & CSV Specification

### 3.1. Physical Memory Contracts & Auto-Normalization Lifecycle

In **SynaptiX IDE**, data types represent physical microcontroller memory contracts rather than cosmetic display labels. Every editable cell in the **Tag Catalog** implements a deterministic two-phase input state machine (**Drafting** while typing; **Committed** upon `Enter` or `LostFocus`):

| Data Type | Physical Representation | Nominal Input | Auto-Normalization (`Enter` / `LostFocus`) | Adversarial / Invalid Input Defense |
| :--- | :--- | :--- | :--- | :--- |
| **`BOOL`** | 1-bit discrete state (`0` or `1`) | `0` or `1` (or `0↔1` toggle button) | `"true"`, `"on"` → `"1"`<br/>`"false"`, `"off"` → `"0"` | Unrecognized strings (`"2"`, `"abc"`) are rejected and **auto-revert** to the prior committed `0` or `1`. |
| **`INT32`** | 32-bit signed 2's complement integer (`-2,147,483,648` .. `2,147,483,647`) | `-150`, `0`, `4095` | `"100.0"` or `"100,0"` → `"100"` | Fractional values (`"25.5"`) or text (`"abc"`) cannot exist in an integer register—rejected and **auto-reverted** immediately. |
| **`REAL`** | 32-bit IEEE-754 single-precision float | `25.5`, `-0.75`, `101.325` | `"25"` or `"25,0"` → `"25.0"` | Non-numeric text (`"NaN"`, `"err"`) is rejected and **auto-reverted** to the last valid float value. |

::: danger VISUAL TRUTH GUARANTEE (ISA-101)
**SynaptiX IDE** strictly prohibits silent truncation. Typing `"25.5"` into an `INT32` tag never displays `"25.5"` on screen while storing `25` in memory, nor does it silently round. It deterministically reverts to the previously committed integer so the operator's screen always matches controller memory 1:1.
:::

### 3.2. CSV Import & Export Format Specification

For bulk I/O scheduling via **File → Import / Export → Tags CSV**, **SynaptiX IDE** generates and parses RFC-4180 compliant comma-separated files encoded in **UTF-8 with BOM** (preserving international and Vietnamese diacritics cleanly in Microsoft Excel).

- **Matching Rules on Import:** Rows are matched against the active hardware catalog first by canonical `Name` (case-insensitive, such as `VREG_RETAIN0`) and secondarily by `Index`. Unknown hardware channels outside the target device's profile are safely skipped without corrupting valid tags.
- **Comma & Quote Escaping:** Aliases containing commas or quotation marks are enclosed in standard double quotes (`"..."`).

Example `synaptix_tags.csv` file:

```csv
Index,Name,Alias,Kind,Channel,Group,Value
0,DI0,"E_Stop_Chain_OK",DI,0,Safety_Circuit,1
1,DI1,"Start_Pushbutton",DI,1,Operator_Panel,0
8,DO0,"Main_Feed_Pump_Contactor",DO,0,Pump_Skid,0
16,AI0,"Header_Pressure_Bar",AI,0,Instrumentation,6.25
24,VFLAG0,"Auto_Cycle_Permissive",VFLAG,0,Control_State,1
40,VREG0,"Scaled_Flow_LPM",VREG,0,Process_Math,120.0
56,VREG_RETAIN0,"High_Pressure_Alarm_Setpoint",VREG_RETAIN,0,Setpoints,8.5
64,COUNTER0,"Completed_Pallet_Count",COUNTER,0,Packaging_Line,0
```

---

## 4. Step-by-Step Operational Procedures

### Procedure 4.1: Defining Engineering Aliases & Configuring `REAL` Setpoints

1. Press `Ctrl+3` to open the **Tag Catalog**.
2. Click the **`VREG_RETAIN`** filter pill to isolate non-volatile Flash registers suitable for operator setpoints that must survive cabinet power cycles.
3. On row `VREG_RETAIN0`, open the **`Data Type`** dropdown and switch from `INT32` to `REAL` if your setpoint requires decimal precision (for example, `75.5` °C).
4. Click the **`Group`** cell to assign a functional area (such as `Thermal_Zone_1`) and click the **`Alias`** cell to enter `Overtemp_Trip_Setpoint_C`.
5. Enter `75.5` in the **`Value`** cell and press `Enter`. Your alias and `REAL` data-type contract are now immediately selectable inside [Logic Graph Canvas](/workspaces/logic-graph-tab) tag pickers and [Live Watch](/workspaces/live-watch-tab).

### Procedure 4.2: Bulk Provisioning Plant I/O Schedules via CSV

1. Export the baseline hardware template via **File → Export → Tags CSV** (or the toolbar **Export CSV** button) and save it as `synaptix_tags.csv`.
2. Open `synaptix_tags.csv` in Microsoft Excel or VS Code, populate the `Alias`, `Group`, and initial `Value` columns from your electrical wiring schedule, and save the file (retaining UTF-8 encoding).
3. Return to **SynaptiX IDE**, select **File → Import → Tags CSV**, and choose `synaptix_tags.csv`.
4. Verify the updated aliases across the **Tag Catalog** and press `F7` to re-verify all bound rules against any updated data types.

---

## 5. Engineering Best Practices & Edge Cases

- **Flash Endurance Discipline on `VREG_RETAIN`:** Although `VREG_RETAIN` registers are backed by industrial wear-leveled non-volatile memory (rated above `100,000` write cycles), avoid writing high-frequency oscillating telemetry (such as `100 ms` noisy ADC readings) into `VREG_RETAIN` on every scan cycle. Use volatile **`VREG`** for sub-second calculations and reserve **`VREG_RETAIN`** for setpoints, calibration constants, and batch accumulators.
- **Changing Data Types on Bound Tags:** Switching a `VREG` or `VREG_RETAIN` tag between `INT32` and `REAL` updates the physical wire encoding and comparator evaluation mode (`FloatCompare`). Always press `F7` after changing a tag's `Data Type` so the static verifier checks all downstream `COMPARATOR`, `SCALE`, and `MB_READ` blocks for type alignment.
- **Using Deterministic `SYSTEM` Tags:** Instead of building dedicated oscillators for standard status lamps or initialization pulses, bind directly to `SYS_FIRST_SCAN` (one-shot initialization pulse on boot), `SYS_CLOCK_1S` (`1 Hz` blink clock), or `SYS_COMM_OK` (fieldbus health permissive).

---

## Related Documentation

- [Tag Architecture & Data Types](/concepts/data-model-and-types)
- [2. Logic Graph Canvas (`Ctrl+2`)](/workspaces/logic-graph-tab)
- [7. Live Watch & Trends (`Ctrl+7`)](/workspaces/live-watch-tab)
- [RS-485 Config & SCADA Slave Map](/tools/rs485-and-scada-slave)
- [Payload Macros & Capacity Limits](/appendices/macro-tokens-and-limits)
