# Keyboard Shortcuts & Canvas Hotkeys

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-APPX-001</span>
  <span><strong>Scope:</strong> Global Window Bindings &bull; Logic Graph Canvas &bull; Mouse Navigation</span>
  <span><strong>Platform:</strong> Windows 10 / 11 Desktop (<code>SynaptiX IDE</code>)</span>
  <span><strong>Standard:</strong> High-Velocity Industrial Engineering Ergonomics</span>
</div>

**SynaptiX IDE** is engineered for keyboard-first speed during control panel design, factory commissioning, and live fault diagnosis. All global shortcuts below are active across the main workbench (automatically yielding when typing inside a text input field).

---

## 1. Workspace Tab Navigation (`Ctrl+1` .. `Ctrl+7`)

Switch instantaneously between the 7 primary engineering workspaces using either the top number row (`1..7`) or the numeric keypad (`NumPad1..NumPad7`):

| Shortcut | Alternate Key | Target Workspace | Primary Engineering Function |
| :--- | :--- | :--- | :--- |
| `Ctrl+1` | `Ctrl+NumPad1` | **[1. Device Workspace](/workspaces/device-tab)** *(or [DTU Studio](/tools/dtu-gateway-studio) on DTU Preset)* | Hardware preset selection, USB/TCP connection setup, RTC clock sync & health telemetry. |
| `Ctrl+2` | `Ctrl+NumPad2` | **[2. Logic Graph Canvas](/workspaces/logic-graph-tab)** | Visual FBD authoring, multi-node wiring, `F8` live canvas debug & `F5` simulation drawer. |
| `Ctrl+3` | `Ctrl+NumPad3` | **[3. Tag Catalog](/workspaces/tag-catalog-tab)** | Physical & virtual tag allocation (`BOOL`, `INT32`, `REAL`), aliases, units & CSV import/export. |
| `Ctrl+4` | `Ctrl+NumPad4` | **[4. Rule Table](/workspaces/rule-table-tab)** | Tabular inspection of compiled 4-stage `Trigger -> Condition -> Guard -> Action` rules. |
| `Ctrl+5` | `Ctrl+NumPad5` | **[5. Blueprints Library](/workspaces/blueprints-tab)** | Pre-validated industrial control templates ready for one-click instantiation. |
| `Ctrl+6` | `Ctrl+NumPad6` | **[6. Atomic Deploy](/workspaces/deploy-tab)** | Pre-flight check, CRC-16 verification & two-phase non-volatile memory commit. |
| `Ctrl+7` | `Ctrl+NumPad7` | **[7. Live Watch & Trends](/workspaces/live-watch-tab)** | High-density real-time tag table, multi-pen strip charts & guarded online tag forcing. |

---

## 2. Runtime, Compilation & Commissioning (`F4` .. `F11`)

| Shortcut | Command Action | Operational Behavior |
| :--- | :--- | :--- |
| `F4` | **Connect / Disconnect Hardware** | Toggles online transport session (USB CDC Virtual COM or Modbus TCP `:502`) with the target SynaptiX device. |
| `F5` | **Toggle Offline Simulator** | Starts or closes the desktop [Cycle-Accurate Offline Simulator](/tools/simulator-and-live-debug) (requires valid compilation). |
| `Ctrl+F5` | **Deploy to Device** | Compiles and launches the two-phase **Atomic Deploy** sequence to commit logic and configurations to hardware. |
| `F6` | **Upload from Device** | Reads the active compiled program, tag catalog, fieldbus ports, and DTU settings from connected hardware back into the IDE. |
| `F7` | **Compile Rules & Blocks** | Runs structural grammar validation, physical type verification, write-conflict detection, and bytecode lowering. |
| `F8` | **Toggle Online Live Debug** | Overlays 20 FPS energized boolean wires, live pin readouts, timer `ET/PT`, and `[STALE]` badges on the Logic Graph. |
| `F11` | **Toggle Full-Screen Mode** | Maximizes canvas viewport estate on compact field-service laptops by hiding window chrome. |

---

## 3. Protocol Configuration & AI Copilot Dialogs

| Shortcut | Dialog / Panel Launched | Reference Guide |
| :--- | :--- | :--- |
| `Ctrl+Space` | **Toggle SynaptiX AI Copilot Panel** | Slide-out natural-language graph synthesis & compiler diagnostic assistant ([Guide](/tools/ai-copilot)). |
| `Ctrl+Shift+B` | **RS-485 Bus & SCADA Slave Configuration** | Configure `RS-485 #1..#4` Master/Slave roles, framing, channels & export Fixed Slave Map ([Guide](/tools/rs485-and-scada-slave)). |
| `Ctrl+Shift+M` | **MQTT Broker & X.509 TLS Security** | Configure broker host, credentials, LWT offline payload & Root CA / `mTLS` certificates ([Guide](/tools/mqtt-and-wifi-config)). |
| `Ctrl+Shift+W` | **Wi-Fi Provisioning & Live AP Scanner** | Configure 2.4 GHz `STA` credentials, live cabinet RSSI survey & Static/DHCP IPv4 ([Guide](/tools/mqtt-and-wifi-config)). |
| `Ctrl+Shift+D` | **DTU Gateway Studio Workbench** | Launch the guided RS-485-to-Cloud (`MQTT` / `HTTP` / `TCP`) configurator & wire frame inspector ([Guide](/tools/dtu-gateway-studio)). |

---

## 4. Project File & Clipboard Editing Shortcuts

| Shortcut | Alternate Key | Action | Scope & Notes |
| :--- | :--- | :--- | :--- |
| `Ctrl+N` | — | **New Project** | Opens the Device Preset selector dialog to initialize a clean project. |
| `Ctrl+O` | — | **Open Project** | Loads an existing SynaptiX project file from disk. |
| `Ctrl+S` | — | **Save Project** | Saves project file (and triggers `SaveAndCompile` when focused on the Logic Graph). |
| `Ctrl+Shift+S` | — | **Save Project As...** | Clones the current project to a new filename or directory. |
| `Ctrl+Z` | — | **Undo** | Reverts the last node placement, wiring edit, parameter change, alignment, or AI Copilot insertion. |
| `Ctrl+Y` | `Ctrl+Shift+Z` | **Redo** | Re-applies the most recently undone graph transaction. |
| `Ctrl+X` | — | **Cut Selection** | Copies selected nodes to the internal graph clipboard and removes them cleanly. |
| `Ctrl+C` | — | **Copy Selection** | Copies selected nodes (preserving internal parameters and interconnections) to clipboard. |
| `Ctrl+V` | — | **Paste** | Pastes clipboard nodes onto the canvas with automatic cascading coordinate offset. |
| `Ctrl+D` | — | **Duplicate Selection** | Clones selected nodes in-place in a single keystroke. |
| `Ctrl+A` | — | **Select All Nodes** | Selects every function block on the Logic Graph canvas. |
| `Delete` | — | **Delete Selected** | Deletes all currently selected function blocks and/or highlighted connection wires. |

---

## 5. Multi-Node Canvas Alignment & Grid Snapping

Select two or more blocks on the [Logic Graph Canvas (`Ctrl+2`)](/workspaces/logic-graph-tab) to tidy complex control schematics instantaneously:

| Primary Shortcut | Canvas Alternate | Layout Action | Geometric Behavior |
| :--- | :--- | :--- | :--- |
| `Alt+Left` | `Ctrl+Shift+Left` | **Align Left Edges** | Aligns left `X` coordinates of selected nodes to the leftmost node. |
| `Alt+Right` | `Ctrl+Shift+Right` | **Align Right Edges** | Aligns right edges of selected nodes to the rightmost node. |
| `Alt+Up` | `Ctrl+Shift+Up` | **Align Top Edges** | Aligns top `Y` coordinates of selected nodes to the topmost node. |
| `Alt+Down` | `Ctrl+Shift+Down` | **Align Bottom Edges** | Aligns bottom edges of selected nodes to the bottommost node. |
| `Alt+C` | `Ctrl+Shift+C` | **Align Horizontal Centers** | Centers selected nodes vertically along a shared `X` axis column. |
| `Alt+M` | `Ctrl+Shift+M` *(canvas)* | **Align Vertical Middles** | Aligns vertical midpoints along a clean horizontal `Y` pipeline row. |
| `Alt+Shift+H` | `Ctrl+Shift+H` | **Distribute Horizontally** | Spaces `3+` selected nodes evenly left-to-right across their bounding span. |
| `Alt+Shift+V` | `Ctrl+Shift+V` | **Distribute Vertically** | Spaces `3+` selected nodes evenly top-to-bottom across their bounding span. |
| `Alt+G` | `Ctrl+Shift+G` | **Snap All Nodes to Grid** | Quantizes all node coordinates onto the crisp `20 px` schematic grid. |

---

## 6. Mouse & Viewport Gestures on Logic Graph

| Mouse / Modifier Gesture | Canvas Interaction |
| :--- | :--- |
| **Mouse Wheel Scroll** | Smooth zoom in / zoom out centered around the cursor position. |
| **Middle-Click Drag** *(or Right-Click Drag on empty canvas)* | Pan viewport freely across large multi-network schematics. |
| **Left-Click Drag on Empty Canvas** | Marquee rubber-band rectangle selection around multiple blocks. |
| **`Ctrl + Left-Click`** *(or `Shift + Left-Click`)* | Add or toggle individual blocks and connection wires in the current multi-selection. |
| **Double-Click Toolbox Block** | Automatically inserts the block and connects it downstream of the currently selected node (`InsertNextConnectedNode`). |
| **Drag Output Pin -> Input Pin** | Creates a type-checked signal connection wire; right-click or select wire + `Delete` to disconnect. |
