# SynaptiX Technical Documentation Portal (`docs-portal/`)

Official bilingual (**English `/` & Tiếng Việt `/vi/`**) engineering documentation portal for the **SynaptiX Edge Automation Platform**, powered by **VitePress** and **Mermaid**.

## Standards & Governance

1. **International Standards Compliance:**
   - **IEC/IEEE 82079-1:2019** — Hardware datasheets, wiring, grounding, and lifecycle safety.
   - **ISO/IEC/IEEE 26514** — Modular task-oriented software engineering documentation (`SynaptiX IDE`).
   - **Diátaxis Framework** — Strict separation of *Get Started*, *Guides*, *Technical Reference*, *Integration*, and *Application Notes*.
   - **ANSI Z535.6 / IEC 60073** — Standardized `DANGER`, `WARNING`, `CAUTION`, and `NOTICE` hazard blocks.
2. **Brand & IP Boundary Policy:**
   - Strictly target **System Integrators, SCADA/HMI Engineers, and OT/IIoT Architects**.
   - Never expose proprietary internal IDE-to-controller staging buffers or raw firmware C structs.
   - Use official **SynaptiX** product nomenclature exclusively across both `en/` and `vi/` trees.

## Local Development & Verification

```bash
cd docs-portal
npm install

# Start live local development server (http://localhost:5173)
npm run docs:dev

# Build production static bundle & verify zero dead links
npm run docs:build

# Preview production build locally
npm run docs:preview
```
