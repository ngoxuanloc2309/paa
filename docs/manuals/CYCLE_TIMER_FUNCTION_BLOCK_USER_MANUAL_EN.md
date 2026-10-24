# CYCLE TIMER FUNCTION BLOCK USER MANUAL & ENGINEERING GUIDE
## (REPEAT CYCLE / ASYMMETRICAL PERIODIC TIMER)

**Document Code:** `MAN-SPLC-CYC-2026-V2.0-EN`  
**Applicable Versions:** `SimplePLC / SynaptiX IDE v2.6.0+`  
**Referenced International Standards:**
- **IEC 61131-3:2013** (Programmable controllers — Programming languages — Timers & Function Blocks)
- **ISA-101.01-2015** (Human-Machine Interfaces for Process Automation Systems)
- **IEC 60073:2002** (Basic and safety principles for man-machine interface — Coding principles for indicators and actuators)

---

## 1. OVERVIEW & INDUSTRIAL DESIGN PHILOSOPHY

In industrial automation and smart infrastructure systems, repeating an **asymmetrical periodic ON / OFF cycle** is one of the most frequent requirements:
* **Short Pulse Oscillations:** Alarm beacon flasher alternating 1s ON / 1s OFF; acoustic error buzzer pulsing 200ms ON / 800ms OFF.
* **Long Period Machine Cycles:** Irrigation pump running 20 minutes and resting 40 minutes; compressed air tank condensate drain valve opening for 5 seconds every 30 minutes; poultry farm ventilation fan running 3 minutes and pausing 10 minutes continuously day and night.

Unlike traditional PLC platforms that require engineers to manually daisy-chain two cross-coupled `TON` timers with inverted feedback contacts or assemble custom latch logic, **SimplePLC Studio (SynaptiX IDE)** provides a dedicated **`Cycle Timer`** (`REPEAT_CYCLE`) block. It features an intuitive, ergonomic interface suitable for both field operators and senior automation engineers.

```text
       +------------------------------------+
       | #1 🕒 Cycle Timer         [CYCLE]  |
       |                                    |
       |   T_ON : 10 s                      |  (ON Duration)
       |   T_OFF: 30 s                      |  (OFF Duration)
       |                                    |
In ───►| [>]       ● ON (4s / 10s)      [>] |───► Q (Beacon / Siren / Pump)
       +------------------------------------+
```

---

## 2. PARAMETERS & INTERFACE SPECIFICATION

### 2.1. Connectors & Pins

| Pin Name | Direction | Category | Data Type | Function & Fail-Safe Behavior |
| :---: | :---: | :---: | :---: | :--- |
| **`In`** | Input | Event / Logic | `Boolean` | **Enable / Run Trigger Signal:**<br>• When `In = 1`: The block executes its periodic cycle ($T_{ON} \leftrightarrow T_{OFF}$).<br>• When `In = 0`: **Instant Fail-Safe Cutoff**, output $Q$ immediately drops to `0`, and the elapsed dwell timer resets to `0`. |
| **`Q`** | Output | State / Signal | `Boolean` | **Control Output:**<br>• Asserts high logic level `1` (ON) during the $T_{ON}$ phase.<br>• Asserts low logic level `0` (OFF) during the $T_{OFF}$ phase.<br>• Connects directly to `Action` nodes (`DO0..DOn`, `VFLAG`, etc.). |

---

### 2.2. Inspector Properties

| Parameter | Symbol | Supported Units | Default | Description |
| :--- | :---: | :---: | :---: | :--- |
| **ON Duration** | $T_{ON}$ | `ms`, `s`, `min`, `h` | `1 s` | Duration during which output $Q$ remains high `1` (ON) in each period. |
| **OFF Duration** | $T_{OFF}$ | `ms`, `s`, `min`, `h` | `1 s` | Duration during which output $Q$ remains low `0` (OFF) in each period. |
| **Initial Phase** | `StartPhase` | Enum | `ON First` | **State when input `In` transitions to 1:**<br>• `Start with ON phase (Default)`: When $In = 1$, $Q$ immediately turns ON for $T_{ON}$, then pauses for $T_{OFF}$.<br>• `Start with OFF phase`: When $In = 1$, $Q$ stays OFF for $T_{OFF}$, then turns ON for $T_{ON}$. |
| **Custom Label** | `CustomLabel` | String | Empty | User-friendly mnemonic displayed above the block (e.g., *"Duty Pump 1"*, *"Fault Tower Red"*). |

---

## 3. TIMING DIAGRAMS

### 3.1. Mode 1: "Start With ON Phase" (Default)
Suitable for 90% of field applications where immediate activation is required upon receipt of a command (emergency siren sounds immediately, warning beacon flashes right away).

```text
Input In   : ───┌───────────────────────────────────────────────┐──────
                │                                               │
Output Q   : ───┌───────────┐           ┌───────────┐           └───0──
                │   T_ON    │   T_OFF   │   T_ON    │   T_OFF   │ (Instant cutoff
             ───┘           └───────────┘           └───────────┘  when In=0)
```

---

### 3.2. Mode 2: "Start With OFF Phase"
Suitable for scheduled sampling, condensate purging, or delayed startup where a rest or settling interval must elapse before the first action.

```text
Input In   : ───┌───────────────────────────────────────────────┐──────
                │                                               │
Output Q   : ───│           ┌───────────┐           ┌───────────└───0──
                │   T_OFF   │   T_ON    │   T_OFF   │   T_ON    │
             ───└───────────┘           └───────────┘           └──────
```

---

## 4. THREE PRACTICAL INDUSTRIAL APPLICATION SCENARIOS

### Scenario 1: Tower Alarm Beacon Flasher
* **Requirement:** When a machine fault occurs (`DI0_ALARM = 1`), the red beacon `DO0_RED` flashes with a 1-second period (500ms ON / 500ms OFF). When the alarm is cleared (`DI0_ALARM = 0`), the beacon turns OFF immediately.
* **Configuration:**
  * Mode: `CYCLE`
  * $T_{ON} = 500 \text{ ms}$
  * $T_{OFF} = 500 \text{ ms}$
  * StartPhase: `ON First`
  * Connections: `DI0` $\rightarrow$ `Cycle Timer.In`, `Cycle Timer.Q` $\rightarrow$ `DO0`.

---

### Scenario 2: Agricultural Aeration / Duty Pump Alternation
* **Requirement:** Shrimp pond aeration pump runs for 20 minutes, pauses for 40 minutes, and repeats automatically while auto mode selector `DI1_AUTO` is active.
* **Configuration:**
  * Mode: `CYCLE`
  * $T_{ON} = 20 \text{ min}$
  * $T_{OFF} = 40 \text{ min}$
  * StartPhase: `ON First`
  * Connections: `DI1` $\rightarrow$ `Cycle Timer.In`, `Cycle Timer.Q` $\rightarrow$ Pump contactor `DO1`.

---

### Scenario 3: Air Compressor Condensate Purge Valve
* **Requirement:** An air compressor receiver tank must purge moisture: Every 30 minutes, open the drain solenoid valve for exactly 5 seconds, then close it.
* **Configuration:**
  * Mode: `CYCLE`
  * $T_{ON} = 5 \text{ s}$
  * $T_{OFF} = 30 \text{ min}$
  * StartPhase: `OFF First` (Waits 30 minutes before first purge).
  * Connections: `DI2_COMP_RUN` $\rightarrow$ `Cycle Timer.In`, `Cycle Timer.Q` $\rightarrow$ Drain solenoid `DO2`.

---

## 5. COMPILER & FIRMWARE SAFETY GUARANTEES

1. **Controlled Multi-Writer Group Interlock:**
   * Under `RuleCompiler`, the Cycle Timer is macro-expanded into 4 deterministic atomic candidate rules gated by internal guards ($R_0$ `CYCLE_START`, $R_1$ `CYCLE_ON_EXPIRE`, $R_2$ `CYCLE_OFF_EXPIRE`, $R_3$ `CYCLE_STOP`).
   * The `WriteConflictValidator` registers the `CYCLE` identifier into the controlled multi-writer group, ensuring no spurious write conflicts occur during graph compilation.
2. **Instant Fail-Safe Cutoff (Zero Latency Shutdown):**
   * Rule $R_3$ (`CYCLE_STOP`) monitors the falling edge and low state of pin `In`. When `In = 0`, output $Q$ is forced to `0` within the active scan cycle, eliminating overshoot or runaway equipment.
3. **Live Telemetry & Diagnostics:**
   * During real-time simulation or hardware online monitoring, the Status Pill displays dynamic progress: `● ON (4s / 10s)` or `● OFF (12s / 30s)`.
