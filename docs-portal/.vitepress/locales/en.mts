import type { DefaultTheme, LocaleSpecificConfig } from 'vitepress'

export const enConfig: LocaleSpecificConfig<DefaultTheme.Config> = {
  title: 'SynaptiX Programming Guide',
  description: 'Official Technical Reference Manual & Programming Guide for SynaptiX Ecosystem',
  themeConfig: {
    siteTitle: 'SynaptiX Programming Guide',
    nav: [
      { text: 'Guide Home', link: '/' },
      { text: 'Getting Started', link: '/getting-started/installation', activeMatch: '/(concepts|getting-started)/' },
      { text: 'Workspaces', link: '/workspaces/device-tab', activeMatch: '/workspaces/' },
      { text: 'Blocks Reference', link: '/blocks/overview', activeMatch: '/blocks/' },
      { text: 'Tools & Integration', link: '/tools/rs485-and-scada-slave', activeMatch: '/tools/' },
      { text: 'Appendices', link: '/appendices/keyboard-shortcuts', activeMatch: '/appendices/' }
    ],

    sidebar: [
      {
        text: '01. Introduction & Core Concepts',
        collapsed: false,
        items: [
          { text: 'Ecosystem & Device Presets', link: '/concepts/ecosystem-overview' },
          { text: 'Tag Architecture & Data Types', link: '/concepts/data-model-and-types' },
          { text: 'Deterministic Execution Pipeline', link: '/concepts/execution-pipeline' }
        ]
      },
      {
        text: '02. Installation & Quickstart',
        collapsed: false,
        items: [
          { text: 'System Requirements & Setup', link: '/getting-started/installation' },
          { text: 'IDE Interface Anatomy', link: '/getting-started/ui-tour' },
          { text: '10-Minute First Project', link: '/getting-started/quickstart-first-project' }
        ]
      },
      {
        text: '03. IDE Workspaces (7 Tabs)',
        collapsed: false,
        items: [
          { text: '1. Device Workspace (Ctrl+1)', link: '/workspaces/device-tab' },
          { text: '2. Logic Graph Canvas (Ctrl+2)', link: '/workspaces/logic-graph-tab' },
          { text: '3. Tag Catalog (Ctrl+3)', link: '/workspaces/tag-catalog-tab' },
          { text: '4. Rule Table (Ctrl+4)', link: '/workspaces/rule-table-tab' },
          { text: '5. Blueprints Library (Ctrl+5)', link: '/workspaces/blueprints-tab' },
          { text: '6. Atomic Deploy (Ctrl+6)', link: '/workspaces/deploy-tab' },
          { text: '7. Live Watch & Trends (Ctrl+7)', link: '/workspaces/live-watch-tab' }
        ]
      },
      {
        text: '04. Function Blocks Reference',
        collapsed: false,
        items: [
          { text: 'Blocks Catalog & Pin Legend', link: '/blocks/overview' },
          {
            text: 'Core Rule Pipeline',
            collapsed: false,
            items: [
              { text: 'INPUT — Tag Source Node', link: '/blocks/input' },
              { text: 'TRIGGER — Edge & RTC Detector', link: '/blocks/trigger' },
              { text: 'COMPARATOR — Relational Logic', link: '/blocks/comparator' },
              { text: 'GUARD — Safety Interlock Gate', link: '/blocks/guard' },
              { text: 'ACTION — Rule Output Executor', link: '/blocks/action' }
            ]
          },
          {
            text: 'Timing, Counting & Scaling',
            collapsed: false,
            items: [
              { text: 'TON — On-Delay Timer', link: '/blocks/ton' },
              { text: 'TOF — Off-Delay Timer', link: '/blocks/tof' },
              { text: 'TP — Monostable Pulse Timer', link: '/blocks/tp' },
              { text: 'CYCLE_TIMER — Asymmetric Oscillator', link: '/blocks/cycle-timer' },
              { text: 'CTU — Count Up Counter', link: '/blocks/ctu' },
              { text: 'CTD — Count Down Counter', link: '/blocks/ctd' },
              { text: 'SCALE — Linear Calibration', link: '/blocks/scale' }
            ]
          },
          {
            text: 'Fieldbus & Hardware Actuation',
            collapsed: false,
            items: [
              { text: 'MB_READ — Modbus RTU Poller', link: '/blocks/mb-read' },
              { text: 'MB_WRITE — Modbus RTU Writer', link: '/blocks/mb-write' },
              { text: 'DIRECT_WRITE — DO / PWM / PTO / Bus', link: '/blocks/direct-write' }
            ]
          },
          {
            text: 'IIoT Cloud & IP Network',
            collapsed: false,
            items: [
              { text: 'MQTT_PUB — Telemetry Publisher', link: '/blocks/mqtt-pub' },
              { text: 'MQTT_SUB — Command Subscriber', link: '/blocks/mqtt-sub' },
              { text: 'HTTP_CLIENT — REST Webhook', link: '/blocks/http-client' },
              { text: 'TCP_CLIENT — Raw Socket Stream', link: '/blocks/tcp-client' }
            ]
          }
        ]
      },
      {
        text: '05. Engineering Tools & Protocols',
        collapsed: false,
        items: [
          { text: 'RS-485 Config & SCADA Slave Map', link: '/tools/rs485-and-scada-slave' },
          { text: 'MQTT TLS & Wi-Fi Provisioning', link: '/tools/mqtt-and-wifi-config' },
          { text: 'Industrial Zigbee Mesh', link: '/tools/zigbee-mesh-config' },
          { text: 'DTU Gateway Studio', link: '/tools/dtu-gateway-studio' },
          { text: 'Simulator & Online Live Debug', link: '/tools/simulator-and-live-debug' },
          { text: 'SynaptiX AI Copilot', link: '/tools/ai-copilot' },
          { text: 'Integration Application Notes', link: '/tools/application-notes' }
        ]
      },
      {
        text: '06. Quick Reference Appendices',
        collapsed: false,
        items: [
          { text: 'Keyboard Shortcuts & Hotkeys', link: '/appendices/keyboard-shortcuts' },
          { text: 'Payload Macros & Capacity Limits', link: '/appendices/macro-tokens-and-limits' },
          { text: 'Error Codes & Troubleshooting', link: '/appendices/error-codes-and-troubleshooting' }
        ]
      }
    ],

    outline: {
      level: [2, 3],
      label: 'On this page'
    },
    docFooter: {
      prev: 'Previous section',
      next: 'Next section'
    },
    footer: {
      message: 'Structured per IEC/IEEE 82079-1:2019, ISO/IEC/IEEE 26514 & ANSI Z535.6 Technical Documentation Standards',
      copyright: 'Copyright © 2026 SynaptiX Automation Systems Group. All rights reserved.'
    }
  }
}
