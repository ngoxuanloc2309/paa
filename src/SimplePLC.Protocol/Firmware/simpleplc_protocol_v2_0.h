/**
 * ============================================================================
 *                       SimplePLC Embedded Ecosystem
 *             MCU FIRMWARE PROTOCOL & MEMORY CONTRACT V2.0
 * ============================================================================
 * @file    simpleplc_protocol_v2_0.h
 * @brief   Authoritative C/C++ Header for Embedded Firmware Developers
 *          (STM32, ESP32, RP2040, GD32, NXP LPC, AVR, etc.)
 * @version 2.0.0
 * @date    2026
 *
 * STANDARD: Standard Modbus RTU over RS-485 / Native USB CDC.
 * Only Function Codes FC03 (Read Holding Registers), FC06 (Write Single),
 * and FC16 (Write Multiple Holding Registers) are used.
 *
 * ENDIANNESS:
 *  - 16-bit Modbus Register words: Big-Endian over the wire.
 *  - 32-bit values (int32_t, uint32_t): Transmitted as 2 consecutive registers
 *    with HIGH WORD FIRST, LOW WORD SECOND: [Reg0: High16, Reg1: Low16].
 *  - CRC-16 Modbus (Polynomial 0xA001, Init 0xFFFF): Little-Endian [Lo, Hi]
 *    at the end of every Modbus RTU frame.
 * ============================================================================
 */

#ifndef SIMPLEPLC_PROTOCOL_V2_0_H
#define SIMPLEPLC_PROTOCOL_V2_0_H

#ifdef __cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>
#include <string.h>

#if defined(__GNUC__) || defined(__clang__)
  #define SPLC_PACKED __attribute__((packed))
#elif defined(_MSC_VER)
  #define SPLC_PACKED
#else
  #define SPLC_PACKED
#endif

#pragma pack(push, 1)

/* ========================================================================= */
/* 1. PROTOCOL CONSTANTS & SYSTEM CAPACITIES                                 */
/* ========================================================================= */

#define SPLC_PROTOCOL_VERSION             2
#define SPLC_RULE_FORMAT_VERSION          7   /* 32-byte binary rule records */
#define SPLC_WIRE_PROFILE_V2              2

#define SPLC_REGISTERS_PER_RULE           16  /* 16 registers = 32 bytes */
#define SPLC_BYTES_PER_RULE               32
#define SPLC_MAX_RULES                    100 /* Maximum capacity: 100 rules */

#define SPLC_MAX_RUNTIME_TAGS             128 /* 128 tags = 256 registers */
#define SPLC_REGISTERS_PER_TAG            2   /* 2 registers = 4 bytes per tag */
#define SPLC_BYTES_PER_TAG                4
#define SPLC_ACTIVE_TAGS_REMOTE_IO        124 /* Tags 0..123 are physically mapped */

#define SPLC_COMMIT_MAGIC                 0xA5A5 /* Magic word written to 0xA000 */

#define SPLC_GUARD_NEGATE_MASK            0x8000 /* Bit 15 of guard_tag: 1 = NEGATE (NOT) */
#define SPLC_GUARD_TAG_MASK               0x7FFF /* Bits 0..14: TagIndex (0..127) */
#define SPLC_GUARD_TAG_NONE               0x7FFF /* Sentinel value 32767 = NO GUARD */

#define SPLC_SCAN_CYCLE_NOMINAL_MS        10     /* Deterministic scan cycle: 10ms */
#define SPLC_DIAG_DEFAULT_LEASE_MS        3000   /* Default diagnostic watchdog lease */
#define SPLC_DIAG_HEARTBEAT_INTERVAL_MS   1000   /* Heartbeat interval from Studio */

/* ========================================================================= */
/* 2. MODBUS HOLDING REGISTER MAP (16-bit word addresses)                    */
/* ========================================================================= */

/* --- 2.1 Core Device & Info Space (0x0000 - 0x0029) --- */
#define SPLC_ADDR_DESCRIPTOR              0x0000  /* 10 regs (20 B): SPLC_DeviceDescriptor_t (FC03, RO) */
#define SPLC_LEN_DESCRIPTOR               10

#define SPLC_ADDR_RULE_TABLE_INFO         0x0010  /* 1 reg: Active rule count (0..100) (FC03, RO) */
#define SPLC_LEN_RULE_TABLE_INFO          1

#define SPLC_ADDR_DEVICE_RESOURCE         0x0020  /* 10 regs (20 B): SPLC_DeviceResourceInfo_t (FC03, RO) */
#define SPLC_LEN_DEVICE_RESOURCE          10

/* --- 2.1.1 Tag Data Types Registry (0x0080 - 0x00BF) --- */
#define SPLC_ADDR_TAG_DATA_TYPES          0x0080  /* 64 regs (128 B): 128 tags * 1 byte (SPLC_TagDataType_t) (FC03/FC16, RW) */
#define SPLC_LEN_TAG_DATA_TYPES           64

/* --- 2.2 Active Rule Table (0x0100 - 0x073F) --- */
#define SPLC_ADDR_ACTIVE_RULES            0x0100  /* Max 1600 regs: 100 rules * 16 (FC03, RO) */
#define SPLC_LEN_ACTIVE_RULES_MAX         1600

/* --- 2.3 Device Health & Real-Time Clock (0x0800 - 0x0813) --- */
#define SPLC_ADDR_HEALTH                  0x0800  /* 10 regs (20 B): SPLC_DeviceHealth_t (FC03, RO) */
#define SPLC_LEN_HEALTH                   10

#define SPLC_ADDR_RTC_CLOCK               0x0810  /* 4 regs (8 B): SPLC_RtcClock_t (FC03/FC16, RW) */
#define SPLC_LEN_RTC_CLOCK                4

/* RTC Status Flags bitmask */
#define SPLC_RTC_FLAG_SYNCED              0x0001  /* Bit 0: 1 = Clock has been synchronized with Host */
#define SPLC_RTC_FLAG_HW_PRESENT          0x0002  /* Bit 1: 1 = Hardware RTC chip/crystal present */
#define SPLC_RTC_FLAG_BATTERY_LOW         0x0004  /* Bit 2: 1 = RTC backup coin-cell battery low */

/* --- 2.4 Runtime Tag Memory Space (0x0900 - 0x09FF) --- */
#define SPLC_ADDR_TAGS                    0x0900  /* 256 regs: 128 tags * 2 regs (FC03/FC16, RW) */
#define SPLC_LEN_TAGS                     256

/* --- 2.5 System Commands & Diagnostics (0x0A00 - 0x0A24) --- */
#define SPLC_ADDR_SYSCOMMAND              0x0A00  /* 1 reg: SPLC_SystemCommand_t (FC06/FC16, WO) */
#define SPLC_LEN_SYSCOMMAND               1

#define SPLC_ADDR_SYSCOMMAND_RESULT       0x0A01  /* 2 regs: [0]=Status, [1]=ErrorCode (FC03, RO) */
#define SPLC_LEN_SYSCOMMAND_RESULT        2

#define SPLC_ADDR_HW_CAPABILITIES         0x0A08  /* 2 regs (4 B): SPLC_HardwareCapabilities_t (32-bit uint32, FC03, RO) */
#define SPLC_LEN_HW_CAPABILITIES          2

#define SPLC_ADDR_HW_RS485_PORT_COUNT     0x0A0A  /* 1 reg: Physical RS-485 port count (FC03, RO, uint16) */
#define SPLC_LEN_HW_RS485_PORT_COUNT      1

#define SPLC_ADDR_DIAG_COMMAND            0x0A20  /* 1 reg: SPLC_DiagCommand_t (FC06/FC16, WO) */
#define SPLC_ADDR_DIAG_STATE              0x0A21  /* 1 reg: SPLC_DiagState_t (FC03, RO) */
#define SPLC_ADDR_DIAG_FLAGS              0x0A22  /* 1 reg: SPLC_DiagFlags_t (FC03, RO) */
#define SPLC_ADDR_DIAG_LEASE_REMAINING    0x0A23  /* 1 reg: Remaining lease in ms (FC03, RO) */
#define SPLC_ADDR_DIAG_ERROR_CODE         0x0A24  /* 1 reg: SPLC_DiagErrorCode_t (FC03, RO) */
#define SPLC_LEN_DIAG_BLOCK               5

/* --- 2.6 Function Block Table Subsystem V2 (0x0B00 - 0x0B7F) --- */
#define SPLC_ADDR_FB_TIMER_TABLE          0x0B00  /* 64 regs: 8 Timers * 8 regs (FC03/FC16, R/W Config) */
#define SPLC_LEN_FB_TIMER_TABLE           64

#define SPLC_ADDR_FB_COUNTER_TABLE        0x0B40  /* 64 regs: 8 Counters * 8 regs (FC03/FC16, R/W Config) */
#define SPLC_LEN_FB_COUNTER_TABLE         64

#define SPLC_LEN_FB_TABLE_TOTAL           128     /* 128 regs total */
#define SPLC_FB_REGISTERS_PER_BLOCK       8       /* 8 regs = 16 bytes per FB */
#define SPLC_FB_MAX_TIMERS                8
#define SPLC_FB_MAX_COUNTERS              8

/* FB Timer Status Bitmask */
#define SPLC_FB_TIMER_BIT_IN              0x0001  /* Bit 0: Input active */
#define SPLC_FB_TIMER_BIT_Q               0x0002  /* Bit 1: Output Q active */
#define SPLC_FB_TIMER_BIT_RESET           0x0004  /* Bit 2: Reset active */
#define SPLC_FB_TIMER_BIT_RUNNING         0x0008  /* Bit 3: Timer is currently counting (ET < PT) */

/* FB Counter Status Bitmask */
#define SPLC_FB_COUNTER_BIT_CU            0x0001  /* Bit 0: Count Up pulse active */
#define SPLC_FB_COUNTER_BIT_CD            0x0002  /* Bit 1: Count Down pulse active */
#define SPLC_FB_COUNTER_BIT_RESET         0x0004  /* Bit 2: Reset active */
#define SPLC_FB_COUNTER_BIT_Q             0x0008  /* Bit 3: Output Q active (CTU: CV>=PV, CTD: CV<=0) */

#define SPLC_FB_COUNTER_RETAIN_NONE       0xFFFF  /* Sentinel: Counter not bound to retain tag */

/* --- 2.7 MQTT IIoT Telemetry & Broker Subsystem V2 (0x0C00 - 0x0E5F) --- */
#define SPLC_ADDR_MQTT_TELEMETRY          0x0C00  /* 384 regs: 4 Blocks * 96 regs (FC03/FC16, RW) */
#define SPLC_LEN_MQTT_TELEMETRY_TOTAL     384
#define SPLC_MQTT_MAX_TELEMETRY_BLOCKS    4
#define SPLC_MQTT_REGS_PER_BLOCK          96      /* 96 regs = 192 bytes per block */
#define SPLC_MQTT_BYTES_PER_BLOCK         192
#define SPLC_MQTT_MAX_TOPIC_CHARS         32
#define SPLC_MQTT_MAX_TEMPLATE_CHARS      128
#define SPLC_MQTT_MAX_TAGS_PER_BLOCK      8

/* Telemetry Control Flags Bitmask (Offset +0 in Block) */
#define SPLC_MQTT_CTRL_ENABLED_BIT        0x0001
#define SPLC_MQTT_CTRL_MODE_MASK          0x0006
#define SPLC_MQTT_CTRL_MODE_SHIFT         1
#define SPLC_MQTT_CTRL_QOS_MASK           0x0018
#define SPLC_MQTT_CTRL_QOS_SHIFT          3
#define SPLC_MQTT_CTRL_RETAIN_BIT         0x0020
#define SPLC_MQTT_CTRL_PAYLOAD_MASK       0x00C0
#define SPLC_MQTT_CTRL_PAYLOAD_SHIFT      6

#define SPLC_ADDR_MQTT_SUBSCRIBE          0x0D80  /* 128 regs: 2 Blocks * 64 regs (FC03/FC16, RW) */
#define SPLC_LEN_MQTT_SUBSCRIBE_TOTAL     128
#define SPLC_MQTT_MAX_SUBSCRIBE_BLOCKS    2
#define SPLC_MQTT_REGS_PER_SUB_BLOCK      64
#define SPLC_MQTT_BYTES_PER_SUB_BLOCK     128
#define SPLC_MQTT_MAX_PINS_PER_SUB_BLOCK  4
#define SPLC_MQTT_MAX_JSON_KEY_CHARS      12

/* Subscribe Control Flags Bitmask (Offset +0 in Block) */
#define SPLC_MQTT_SUB_CTRL_ENABLED_BIT       0x0001
#define SPLC_MQTT_SUB_CTRL_QOS_MASK          0x0006
#define SPLC_MQTT_SUB_CTRL_QOS_SHIFT         1
#define SPLC_MQTT_SUB_CTRL_QUEUE_FIFO_BIT    0x0008
#define SPLC_MQTT_SUB_CTRL_IGNORE_RETAIN_BIT 0x0010
#define SPLC_MQTT_SUB_CTRL_FAILSAFE_MASK     0x0060
#define SPLC_MQTT_SUB_CTRL_FAILSAFE_SHIFT    5

#define SPLC_ADDR_MQTT_BROKER             0x0E00  /* 96 regs (192 B): SPLC_MqttBrokerConfig_t (FC03/FC16, RW) */
#define SPLC_LEN_MQTT_BROKER              96
#define SPLC_MQTT_BROKER_HOST_MAX_CHARS   64
#define SPLC_MQTT_BROKER_CRED_MAX_CHARS   32

/* Broker Control Flags Bitmask (Offset +0 at 0x0E00) */
#define SPLC_BROKER_CTRL_ENABLED_BIT      0x0001
#define SPLC_BROKER_CTRL_USE_TLS_BIT      0x0002
#define SPLC_BROKER_CTRL_CLEAN_SESSION_BIT 0x0004
#define SPLC_BROKER_CTRL_VERSION_MASK     0x0018
#define SPLC_BROKER_CTRL_VERSION_SHIFT    3

/* --- 2.8 MQTT TLS Certificate Staging Buffer (0x0F00 - 0x0F43, SPLC-SPEC-MQTT-003) --- */
#define SPLC_ADDR_MQTT_TLS_STAGING        0x0F00  /* 64 regs (128 B): SPLC_CertChunk_t (FC16, WO) */
#define SPLC_LEN_MQTT_TLS_STAGING_CHUNK   64
#define SPLC_ADDR_MQTT_TLS_ACK            0x0F40  /* 1 reg: ACK chunk index (FC03, RO) */
#define SPLC_ADDR_MQTT_TLS_ERROR          0x0F41  /* 1 reg: Error Code (FC03, RO) */
#define SPLC_ADDR_MQTT_TLS_COMMIT         0x0F42  /* 1 reg: Commit 0xA5A5 (FC06/FC16, WO) */
#define SPLC_ADDR_MQTT_TLS_ACTIVE_MASK    0x0F43  /* 1 reg: Active Cert Type Mask (FC03, RO) */
#define SPLC_MQTT_CERT_PAYLOAD_MAX_BYTES  120

/* --- 2.9 Modbus RTU Master Poller Space (0x1000 - 0x10FF) --- */
#define SPLC_ADDR_MODBUS_MASTER           0x1000  /* 256 regs: 4 Blocks * 64 regs (FC03/FC16, RW) */
#define SPLC_LEN_MODBUS_MASTER_TOTAL      256
#define SPLC_MBM_MAX_BLOCKS               4
#define SPLC_MBM_REGS_PER_BLOCK           64      /* 64 regs = 128 bytes per block */
#define SPLC_MBM_BYTES_PER_BLOCK          128
#define SPLC_MBM_MAX_PINS_PER_BLOCK       4
#define SPLC_MBM_MAX_LABEL_CHARS          12

/* Master Control Flags Bitmask (Offset +0 in Block) */
#define SPLC_MBM_CTRL_ENABLED_BIT         0x0001

/* Data Types */
#define SPLC_MBM_TYPE_BOOL                1
#define SPLC_MBM_TYPE_INT16               2
#define SPLC_MBM_TYPE_UINT16              3
#define SPLC_MBM_TYPE_INT32               4
#define SPLC_MBM_TYPE_UINT32              5
#define SPLC_MBM_TYPE_REAL                6

/* Endianness */
#define SPLC_MBM_ENDIAN_ABCD              0
#define SPLC_MBM_ENDIAN_CDAB              1
#define SPLC_MBM_ENDIAN_BADC              2
#define SPLC_MBM_ENDIAN_DCBA              3

/* Read Modes */
#define SPLC_MBM_READ_MODE_CYCLIC         0
#define SPLC_MBM_READ_MODE_ON_TRIGGER     1

/* --- 2.10 Modbus RTU Master Writer Space (0x1100 - 0x11FF) --- */
#define SPLC_ADDR_MODBUS_MASTER_WRITE     0x1100  /* 256 regs: 4 Blocks * 64 regs (FC03/FC16, RW) */
#define SPLC_LEN_MODBUS_MASTER_WRITE      256

/* --- 2.11 Direct Hardware Output / Pulse Output Space (0x1200 - 0x12FF) --- */
#define SPLC_ADDR_DIRECT_WRITE            0x1200  /* 256 regs: 8 Blocks * 16 regs (FC03/FC16, RW) */
#define SPLC_LEN_DIRECT_WRITE             256
#define SPLC_ADDR_PULSE_OUT               SPLC_ADDR_DIRECT_WRITE
#define SPLC_LEN_PULSE_OUT                SPLC_LEN_DIRECT_WRITE

/* --- 2.12 Wi-Fi Subsystem (ST67 Module) Space (0x1300 - 0x13FF) --- */
#define SPLC_ADDR_WIFI_CONFIG             0x1300  /* 64 regs (128 B): SPLC_WifiConfig_t (FC03/FC16, RW) */
#define SPLC_LEN_WIFI_CONFIG              64
#define SPLC_ADDR_WIFI_CONTROL            0x1340  /* 16 regs (32 B): SPLC_WifiScanControl_t (FC03/FC06/FC16, RW) */
#define SPLC_LEN_WIFI_CONTROL             16
#define SPLC_ADDR_WIFI_SCAN_TABLE         0x1350  /* 176 regs (352 B): 8 APs * 22 regs (FC03, RO) */
#define SPLC_LEN_WIFI_SCAN_TABLE          176
#define SPLC_WIFI_MAX_SCAN_APS            8
#define SPLC_WIFI_REGS_PER_AP             22
#define SPLC_WIFI_BYTES_PER_AP            44

/* --- 2.13 Multi-Port RS-485 Modbus Master Config Space (0x1400 - 0x143F, SPLC-SPEC-MBM-003) --- */
#define SPLC_ADDR_MODBUS_PORT_CONFIG      0x1400  /* 64 regs (128 B): 4 Ports * 16 regs (FC03/FC16, RW) */
#define SPLC_LEN_MODBUS_PORT_CONFIG_TOTAL 64
#define SPLC_MB_PORT_MAX_PORTS            4
#define SPLC_MB_PORT_REGS_PER_PORT        16      /* 16 regs = 32 bytes per port */
#define SPLC_MB_PORT_BYTES_PER_PORT       32
#define SPLC_MB_PORT_NAME_MAX_CHARS       12

/* Wi-Fi Command codes at 0x1340 */
#define SPLC_WIFI_CMD_IDLE                0x0000
#define SPLC_WIFI_CMD_START_SCAN          0x0001
#define SPLC_WIFI_CMD_CONNECT             0x0002
#define SPLC_WIFI_CMD_DISCONNECT          0x0003
#define SPLC_WIFI_CMD_FORGET              0x0004

/* Wi-Fi Scan state at 0x1341 */
#define SPLC_WIFI_SCAN_STATE_IDLE         0
#define SPLC_WIFI_SCAN_STATE_SCANNING     1
#define SPLC_WIFI_SCAN_STATE_COMPLETED    2
#define SPLC_WIFI_SCAN_STATE_FAILED       3

/* Wi-Fi Connection status at 0x133A */
#define SPLC_WIFI_CONN_DISCONNECTED       0
#define SPLC_WIFI_CONN_CONNECTING         1
#define SPLC_WIFI_CONN_CONNECTED          2
#define SPLC_WIFI_CONN_GOT_IP             3
#define SPLC_WIFI_CONN_AUTH_FAILED        4
#define SPLC_WIFI_CONN_SSID_NOT_FOUND     5
#define SPLC_WIFI_CONN_AP_MODE_ACTIVE     6

/* Wi-Fi Auth modes */
#define SPLC_WIFI_AUTH_OPEN               0
#define SPLC_WIFI_AUTH_WEP                1
#define SPLC_WIFI_AUTH_WPA_PSK            2
#define SPLC_WIFI_AUTH_WPA2_PSK           3
#define SPLC_WIFI_AUTH_WPA_WPA2_MIXED     4
#define SPLC_WIFI_AUTH_WPA2_ENTERPRISE    5
#define SPLC_WIFI_AUTH_WPA3_PSK           6

/* --- 2.13 Rule Staging Buffer & Commit Space (0x9000 - 0xA001) --- */
#define SPLC_ADDR_CONFIG_STATUS           0x9000  /* 1 reg: SPLC_CommandStatus_t (FC03, RO) */
#define SPLC_ADDR_CONFIG_ERROR_CODE       0x9001  /* 1 reg: SPLC_ErrorCode_t (FC03, RO) */
#define SPLC_ADDR_RULE_COUNT_STAGED       0x9002  /* 1 reg: Number of rules staged (FC03/FC16, RW) */
#define SPLC_ADDR_EXPECTED_CRC16          0x9003  /* 1 reg: Expected CRC-16 of payload (FC03/FC16, RW) */
#define SPLC_ADDR_ACTIVE_RULE_COUNT       0x9004  /* 1 reg: Currently active rule count (FC03, RO) */
#define SPLC_ADDR_ACTIVE_RULE_CRC16       0x9005  /* 1 reg: Currently active rule CRC-16 (FC03, RO) */
#define SPLC_ADDR_STAGING_RESERVED        0x9006  /* 10 regs reserved */

#define SPLC_ADDR_STAGING_RULES           0x9010  /* Max 1600 regs: Staging buffer for new rules (FC16, WO) */
#define SPLC_LEN_STAGING_RULES_MAX        1600

#define SPLC_ADDR_COMMIT_COMMAND          0xA000  /* 1 reg: Write 0xA5A5 to commit staging to active (FC06/FC16) */
#define SPLC_ADDR_ACTIVE_RULE_VERSION     0xA001  /* 1 reg: Monotonically increasing version counter (FC03, RO) */

/* ========================================================================= */
/* 3. TAG INDEX LAYOUT ARCHITECTURE (Dynamic & Default Layouts)              */
/* ========================================================================= */

/**
 * @brief Dynamic Tag Layout Struct (Consecutive Packing)
 * Starting with Platform V2.0, tag groups are packed consecutively based on
 * the declared pin counts in DeviceResourceInfo (0x0020).
 * DO0 starts immediately after DI (at di_count), eliminating unused dummy gaps.
 */
typedef struct {
    uint16_t di_base;           /* Always 0 */
    uint16_t di_count;
    uint16_t do_base;           /* = di_count */
    uint16_t do_count;
    uint16_t ai_base;           /* = di_count + do_count */
    uint16_t ai_count;
    uint16_t vflag_base;        /* = di_count + do_count + ai_count */
    uint16_t vflag_count;
    uint16_t vreg_base;         /* = di_count + do_count + ai_count + vflag_count */
    uint16_t vreg_count;
    uint16_t vreg_retain_base;  /* = di_count + do_count + ai_count + vflag_count + vreg_count */
    uint16_t vreg_retain_count;
    uint16_t counter_base;      /* = ... + vreg_retain_count */
    uint16_t counter_count;
    uint16_t total_tags;
} SPLC_TagLayoutMap_t;

/**
 * @brief Helper inline function to compute dynamic layout on MCU
 */
static inline SPLC_TagLayoutMap_t SPLC_ComputeTagLayout(
    uint16_t di_cnt, uint16_t do_cnt, uint16_t ai_cnt,
    uint16_t vflag_cnt, uint16_t vreg_cnt, uint16_t retain_cnt, uint16_t counter_cnt)
{
    SPLC_TagLayoutMap_t m;
    m.di_count = di_cnt;
    m.do_count = do_cnt;
    m.ai_count = ai_cnt;
    m.vflag_count = vflag_cnt;
    m.vreg_count = vreg_cnt;
    m.vreg_retain_count = retain_cnt;
    m.counter_count = counter_cnt;

    m.di_base = 0;
    m.do_base = di_cnt;
    m.ai_base = (uint16_t)(m.do_base + do_cnt);
    m.vflag_base = (uint16_t)(m.ai_base + ai_cnt);
    m.vreg_base = (uint16_t)(m.vflag_base + vflag_cnt);
    m.vreg_retain_base = (uint16_t)(m.vreg_base + vreg_cnt);
    m.counter_base = (uint16_t)(m.vreg_retain_base + retain_cnt);
    m.total_tags = (uint16_t)(m.counter_base + counter_cnt);
    return m;
}

/* Default / Reference Capacities (Standard 8DI/8DO/4AI Hardware) */
#define SPLC_DEFAULT_DI_COUNT             8
#define SPLC_DEFAULT_DO_COUNT             8
#define SPLC_DEFAULT_AI_COUNT             4
#define SPLC_DEFAULT_VFLAG_COUNT          32
#define SPLC_DEFAULT_VREG_COUNT           32
#define SPLC_DEFAULT_VREG_RETAIN_COUNT    32
#define SPLC_DEFAULT_COUNTER_COUNT        8

/* Legacy Static Base Offsets (Reference only for standard 8DI/8DO profile) */
#define SPLC_TAG_DI_START                 0
#define SPLC_TAG_DO_START                 8
#define SPLC_TAG_AI_START                 16
#define SPLC_TAG_VFLAG_START              20
#define SPLC_TAG_VREG_START               52
#define SPLC_TAG_VREG_RETAIN_START        84
#define SPLC_TAG_COUNTER_START            116

/* Modbus address calculation for TagIndex: 0x0900 + (TagIndex * 2) */
#define SPLC_TAG_TO_MODBUS_ADDR(idx)      ((uint16_t)(SPLC_ADDR_TAGS + ((idx) * SPLC_REGISTERS_PER_TAG)))


/* ========================================================================= */
/* 4. ENUMERATIONS (Type Semantics & Status Codes)                           */
/* ========================================================================= */

typedef enum {
    SPLC_TAG_KIND_NONE            = 0,
    SPLC_TAG_KIND_DI              = 1,
    SPLC_TAG_KIND_DO              = 2,
    SPLC_TAG_KIND_AI              = 3,
    SPLC_TAG_KIND_VFLAG           = 4,
    SPLC_TAG_KIND_VREG            = 5,
    SPLC_TAG_KIND_MB_COIL         = 6,
    SPLC_TAG_KIND_MB_HOLDING      = 7,
    SPLC_TAG_KIND_VREG_RETAIN     = 8,
    SPLC_TAG_KIND_COUNTER         = 9
} SPLC_TagKind_t;

/**
 * @brief IEC 61131-3 Tag Data Types Contract
 */
typedef enum {
    SPLC_DATA_TYPE_NONE           = 0,
    SPLC_DATA_TYPE_BOOL           = 1, /* IEC 61131-3 BOOL: 1-bit boolean flag (stored as 0 or 1 in 32-bit register) */
    SPLC_DATA_TYPE_INT32          = 2, /* IEC 61131-3 DINT: 32-bit signed two's complement integer (int32_t) */
    SPLC_DATA_TYPE_REAL           = 3  /* IEC 61131-3 REAL: 32-bit IEEE-754 single-precision float (float) */
} SPLC_TagDataType_t;

/**
 * @brief Universal 32-Bit Tag Value Union (IEEE-754 / Two's Complement Coexistence)
 * Each tag in the runtime Tag Store (0x0900..0x09FF) occupies exactly 4 bytes (2 Modbus holding registers).
 * Big-Endian on wire: High Word (bits 31..16) in Reg0, Low Word (bits 15..0) in Reg1.
 */
typedef union SPLC_PACKED {
    int32_t   as_int32;  /* IEC 61131-3 DINT / INT32 signed integer */
    int32_t   as_dint;   /* Alias for as_int32 */
    float     as_real;   /* IEC 61131-3 REAL / IEEE-754 32-bit float */
    uint32_t  as_raw32;  /* Raw 32-bit binary representation */
    struct {
        uint16_t high_word; /* Register N   (Bits 31..16) */
        uint16_t low_word;  /* Register N+1 (Bits 15..0) */
    } words;
} SPLC_TagValue_t;

/**
 * @brief Bitmask định nghĩa các năng lực phần cứng thực tế của MCU.
 * Đọc tại địa chỉ Holding Register 0x0A08 (32-bit uint32, Big-Endian).
 */
typedef enum {
    SPLC_CAP_NONE         = 0x0000,
    SPLC_CAP_BASE_LOGIC   = 0x0001, /* Bit 0: Hỗ trợ Logic Engine FBD cơ bản (Rule Engine) */
    SPLC_CAP_RETENTIVE    = 0x0002, /* Bit 1: Hỗ trợ lưu trữ Retentive Memory (Flash/NVRAM/FRAM) */
    SPLC_CAP_RTC          = 0x0004, /* Bit 2: Hỗ trợ phần cứng Real-Time Clock (DS3231/LSE) */
    SPLC_CAP_ETHERNET     = 0x0008, /* Bit 3: Có cổng truyền thông mạng vật lý (Ethernet/WiFi) */
    SPLC_CAP_MQTT_PUB     = 0x0010, /* Bit 4: Hỗ trợ phân hệ xuất bản viễn trắc MQTT PUB (0x0C00) */
    SPLC_CAP_MQTT_SUB     = 0x0020, /* Bit 5: Hỗ trợ phân hệ nhận bản tin điều khiển MQTT SUB (0x0D80) */
    SPLC_CAP_SD_DATALOG   = 0x0040, /* Bit 6: Hỗ trợ ghi chép dữ liệu cục bộ qua thẻ nhớ MicroSD */
    SPLC_CAP_MQTT_TLS_CERT = 0x0080, /* Bit 7: Hỗ trợ nạp và giải mã chứng chỉ TLS/SSL theo khối struct (0x0F00) */
    SPLC_CAP_MODBUS_MASTER = 0x0100, /* Bit 8: Hỗ trợ phân hệ RS-485 Modbus RTU Master Poller (0x1000) */
    SPLC_CAP_WIFI          = 0x0200, /* Bit 9: Hỗ trợ phân hệ truyền thông không dây Wi-Fi ST67 (0x1300..0x13FF) */
    SPLC_CAP_MODBUS_TCP    = 0x0400, /* Bit 10: Hỗ trợ phân hệ Modbus TCP Server qua Ethernet/Wi-Fi (Port 502) */
    SPLC_CAP_FLOAT32       = 0x0800  /* Bit 11: Hỗ trợ tính toán phần cứng số thực Float32 IEEE-754 / REAL math */
} SPLC_HardwareCapabilities_t;

typedef enum {
    SPLC_DEV_CLASS_UNKNOWN        = 0,
    SPLC_DEV_CLASS_REMOTE_IO      = 1,
    SPLC_DEV_CLASS_DATALOGGER     = 2,
    SPLC_DEV_CLASS_GATEWAY        = 3,
    SPLC_DEV_CLASS_CONTROLLER     = 4
} SPLC_DeviceClass_t;

typedef enum {
    SPLC_RIO_VARIANT_UNKNOWN      = 0,
    SPLC_RIO_VARIANT_8DI_8DO_4AI  = 1,
    SPLC_RIO_VARIANT_16DI_16DO    = 2
} SPLC_RemoteIoVariant_t;

typedef enum {
    SPLC_RESET_UNKNOWN            = 0,
    SPLC_RESET_POWER_ON           = 1,
    SPLC_RESET_SOFTWARE           = 2,
    SPLC_RESET_WATCHDOG           = 3,
    SPLC_RESET_BROWNOUT           = 4,
    SPLC_RESET_EXTERNAL           = 5
} SPLC_ResetReason_t;

typedef enum {
    SPLC_HEALTH_NONE              = 0,
    SPLC_HEALTH_CPU_HIGH          = (1 << 0),
    SPLC_HEALTH_RAM_HIGH          = (1 << 1),
    SPLC_HEALTH_SCAN_OVERRUN      = (1 << 2)
} SPLC_HealthFlags_t;

typedef enum {
    SPLC_SYS_CMD_NONE             = 0,
    SPLC_SYS_CMD_REBOOT           = 1,
    SPLC_SYS_CMD_FACTORY_RESET    = 2,
    SPLC_SYS_CMD_CLEAR_RULES      = 3,
    SPLC_SYS_CMD_CLEAR_RETAIN     = 4
} SPLC_SystemCommand_t;

typedef enum {
    SPLC_CMD_STATUS_IDLE          = 0,
    SPLC_CMD_STATUS_ACCEPTED      = 1,
    SPLC_CMD_STATUS_BUSY          = 2,
    SPLC_CMD_STATUS_DONE          = 3,
    SPLC_CMD_STATUS_ERROR         = 4
} SPLC_CommandStatus_t;

typedef enum {
    SPLC_ERR_NONE                 = 0,
    SPLC_ERR_INVALID_COMMAND      = 1,
    SPLC_ERR_INVALID_PARAMETER    = 2,
    SPLC_ERR_BUSY                 = 3,
    SPLC_ERR_CRC_MISMATCH         = 4,
    SPLC_ERR_UNSUPPORTED          = 5,
    SPLC_ERR_FLASH                = 6
} SPLC_ErrorCode_t;

typedef enum {
    SPLC_TRG_ON_CHANGE            = 0,
    SPLC_TRG_ON_RISE              = 1,
    SPLC_TRG_ON_FALL              = 2,
    SPLC_TRG_TIME_WINDOW          = 3,
    SPLC_TRG_INTERVAL             = 4
} SPLC_TriggerType_t;

typedef enum {
    SPLC_CMP_NONE                 = 0,
    SPLC_CMP_EQ                   = 1,
    SPLC_CMP_NEQ                  = 2,
    SPLC_CMP_GT                   = 3,
    SPLC_CMP_LT                   = 4,
    SPLC_CMP_GTE                  = 5,
    SPLC_CMP_LTE                  = 6,
    SPLC_CMP_BETWEEN              = 7
} SPLC_CompareOp_t;

typedef enum {
    SPLC_ACT_SET_TAG              = 0,
    SPLC_ACT_TOGGLE_TAG           = 1,
    SPLC_ACT_INC_COUNTER          = 2,
    SPLC_ACT_WRITE_REMOTE         = 3,
    SPLC_ACT_LOG_EVENT            = 4,
    SPLC_ACT_SEND_ALARM           = 5,
    SPLC_ACT_ADD_TAG              = 6,
    SPLC_ACT_SCALE_TAG            = 7
} SPLC_ActionType_t;

/* Diagnostic & Commissioning Enums (0x0A20..0x0A24) */
typedef enum {
    SPLC_DIAG_CMD_NONE            = 0,
    SPLC_DIAG_CMD_ENTER_DIAG      = 1, /* Acquire manual diagnostic ownership */
    SPLC_DIAG_CMD_HEARTBEAT       = 2, /* Reset lease timer to default duration */
    SPLC_DIAG_CMD_EXIT_DIAG       = 3, /* Release diagnostic ownership, resume Rule Engine */
    SPLC_DIAG_CMD_COMMIT_RETAIN   = 4, /* Atomic flush of RAM shadow to Flash */
    SPLC_DIAG_CMD_DISCARD_RETAIN  = 5  /* Reload RAM shadow from Flash, clear dirty flag */
} SPLC_DiagCommand_t;

typedef enum {
    SPLC_DIAG_STATE_NONE          = 0,
    SPLC_DIAG_STATE_ENGINE_RUNNING = 1, /* Normal autonomous logic execution */
    SPLC_DIAG_STATE_DIAG_CONTROL   = 2, /* Studio has lease, overrides allowed */
    SPLC_DIAG_STATE_TRANSITIONING  = 3, /* Safety settling period */
    SPLC_DIAG_STATE_FAULT          = 4  /* Lockout due to watchdog or hardware fault */
} SPLC_DiagState_t;

typedef enum {
    SPLC_DIAG_FLAG_NONE           = 0,
    SPLC_DIAG_FLAG_RETAIN_DIRTY   = (1 << 0), /* 0x0001: Flash retain has unsaved updates in RAM */
    SPLC_DIAG_FLAG_LEASE_ACTIVE   = (1 << 1), /* 0x0002: Host heartbeat lease is active */
    SPLC_DIAG_FLAG_EMERGENCY_STOP = (1 << 2)  /* 0x0004: Reserved / E-Stop active */
} SPLC_DiagFlags_t;

typedef enum {
    SPLC_DIAG_ERR_NONE            = 0,
    SPLC_DIAG_ERR_DENIED_FAULT    = 1, /* Denied: MCU is in Fault state */
    SPLC_DIAG_ERR_LEASE_EXPIRED   = 2, /* Lease timer ran down to 0 (watchdog trip) */
    SPLC_DIAG_ERR_FLASH_CRC_MISMATCH = 3, /* CRC mismatch during flash retain commit */
    SPLC_DIAG_ERR_INVALID_COMMAND = 4, /* Invalid command or wrong state context */
    SPLC_DIAG_ERR_RETAIN_DIRTY    = 5  /* Exit denied: unsaved retain data in RAM */
} SPLC_DiagErrorCode_t;

/* Function Block Subsystem Enums (0x0B00..0x0B7F) */
typedef enum {
    SPLC_TIMER_MODE_NONE          = 0,
    SPLC_TIMER_MODE_TON           = 1, /* On-Delay Timer */
    SPLC_TIMER_MODE_TOF           = 2, /* Off-Delay Timer */
    SPLC_TIMER_MODE_TP            = 3, /* Pulse Timer */
    SPLC_TIMER_MODE_CYCLE         = 4  /* Periodic Cycle Timer (T_ON / T_OFF) */
} SPLC_TimerMode_t;

typedef enum {
    SPLC_COUNTER_MODE_NONE        = 0,
    SPLC_COUNTER_MODE_CTU         = 1, /* Count Up */
    SPLC_COUNTER_MODE_CTD         = 2  /* Count Down */
} SPLC_CounterMode_t;

/* MQTT IIoT Subsystem Enums (0x0C00..0x0E5F) */
typedef enum {
    SPLC_MQTT_MODE_ON_CHANGE      = 0, /* Event driven on tag value change */
    SPLC_MQTT_MODE_COV            = 1, /* Change of Value with Deadband */
    SPLC_MQTT_MODE_PERIODIC       = 2  /* Periodic cyclic interval */
} SPLC_MqttPublishMode_t;

typedef enum {
    SPLC_MQTT_QOS_0               = 0, /* At most once */
    SPLC_MQTT_QOS_1               = 1, /* At least once */
    SPLC_MQTT_QOS_2               = 2  /* Exactly once */
} SPLC_MqttQos_t;

typedef enum {
    SPLC_MQTT_PAYLOAD_COMPACT_JSON = 0, /* Auto-compact JSON: {"tag":x,"val":y,"ts":z} */
    SPLC_MQTT_PAYLOAD_RAW_VALUE    = 1, /* Raw numerical string */
    SPLC_MQTT_PAYLOAD_CUSTOM       = 2  /* 128B custom template with token substitution */
} SPLC_MqttPayloadMode_t;

typedef enum {
    SPLC_MQTT_ERR_NONE            = 0,
    SPLC_MQTT_ERR_DNS_FAIL        = 1,
    SPLC_MQTT_ERR_TCP_RESET       = 2,
    SPLC_MQTT_ERR_AUTH_FAIL       = 3,
    SPLC_MQTT_ERR_TIMEOUT         = 4
} SPLC_MqttBrokerErrorCode_t;

/* ========================================================================= */
/* 5. PROTOCOL BINARY STRUCTURES (Strict Byte-Exact Wire Layouts)            */
/* ========================================================================= */

/**
 * @brief Device Descriptor (10 registers = 20 bytes at 0x0000).
 * Read-Only, immutable hardware/firmware identification.
 */
typedef struct SPLC_PACKED {
    uint16_t device_class;        /* SPLC_DeviceClass_t (1 = REMOTE_IO) */
    uint16_t device_variant;      /* SPLC_RemoteIoVariant_t (1 = 8DI_8DO_4AI) */
    uint16_t hw_version_major;    /* e.g. 1 */
    uint16_t hw_version_minor;    /* e.g. 0 */
    uint16_t hw_version_patch;    /* e.g. 0 */
    uint16_t fw_version_major;    /* e.g. 2 */
    uint16_t fw_version_minor;    /* e.g. 0 */
    uint16_t fw_version_patch;    /* e.g. 0 */
    uint16_t protocol_version;    /* Must be 2 for V2.0 */
    uint16_t rule_format_version; /* Must be 7 for V1.7 Rule format */
} SPLC_DeviceDescriptor_t;

/**
 * @brief Device Resource Info (10 registers = 20 bytes at 0x0020).
 * Self-Describing profile defining tag and rule counts.
 */
typedef struct SPLC_PACKED {
    uint16_t wire_profile;        /* Must be 2 for Wire Profile V2 */
    uint16_t max_rules;           /* Maximum rule count = 100 */
    uint16_t runtime_tag_count;   /* Total active tags = 124 */
    uint16_t di_count;            /* DI slots = 8 */
    uint16_t do_count;            /* DO slots = 8 */
    uint16_t ai_count;            /* AI slots = 4 */
    uint16_t vflag_count;         /* VFLAG slots = 32 */
    uint16_t vreg_count;          /* VREG slots = 32 */
    uint16_t vreg_retain_count;   /* VREG_RETAIN slots = 32 */
    uint16_t counter_count;       /* COUNTER slots = 8 */
} SPLC_DeviceResourceInfo_t;

/**
 * @brief Device Health (10 registers = 20 bytes at 0x0800).
 * Operational telemetry, cycle scan times, memory load.
 */
typedef struct SPLC_PACKED {
    uint32_t uptime_s;            /* Uptime in seconds since boot */
    uint16_t reset_reason;        /* SPLC_ResetReason_t */
    uint16_t health_flags;        /* SPLC_HealthFlags_t */
    uint16_t cpu_load_percent;    /* CPU load: 0..100 % */
    uint16_t ram_usage_percent;   /* RAM load: 0..100 % */
    uint32_t scan_time_ms;        /* Execution duration of last 10ms cycle */
    uint32_t max_scan_time_ms;    /* Peak scan duration observed since boot */
} SPLC_DeviceHealth_t;

/**
 * @brief Real-Time Clock Synchronization (4 registers = 8 bytes at 0x0810).
 * Synchronized by Studio; maintained autonomously by MCU.
 */
typedef struct SPLC_PACKED {
    uint32_t epoch_utc_s;         /* Unix Epoch timestamp in seconds (High Word first) */
    int16_t  tz_offset_min;       /* Timezone offset in minutes (e.g. +420 for UTC+7) */
    uint16_t status_flags;        /* Bit 0: Synced, Bit 1: HW_RTC, Bit 2: Battery_Low */
} SPLC_RtcClock_t;

/**
 * @brief Diagnostic Block (5 registers = 10 bytes at 0x0A20).
 * Commissioning, manual overrides, and watchdog lease control.
 */
typedef struct SPLC_PACKED {
    uint16_t command;             /* SPLC_DiagCommand_t */
    uint16_t state;               /* SPLC_DiagState_t */
    uint16_t flags;               /* SPLC_DiagFlags_t */
    uint16_t lease_remaining_ms;  /* Downcounter in ms; falls back to autonomous at 0 */
    uint16_t error_code;          /* SPLC_DiagErrorCode_t */
} SPLC_DiagBlock_t;

/**
 * @brief Function Block Timer Record (8 registers = 16 bytes at 0x0B00 + (idx * 8)).
 * Standard IEC 61131-3 Timer Block telemetry.
 */
typedef struct SPLC_PACKED {
    uint16_t status_bits;         /* Bit 0: IN, Bit 1: Q, Bit 2: RESET, Bit 3: RUNNING */
    uint16_t mode;                /* SPLC_TimerMode_t (1: TON, 2: TOF, 3: TP) */
    uint32_t pt_ms;               /* Preset Time in ms (High Word first) */
    uint32_t et_ms;               /* Elapsed Time in ms (High Word first) */
    uint16_t rule_ref;            /* Rule Reference Index (1-based: 1..100, 0: unlinked) */
    uint16_t reserved;            /* Reserved, always 0 */
} SPLC_FbTimerRecord_t;

/**
 * @brief Function Block Counter Record (8 registers = 16 bytes at 0x0B40 + (idx * 8)).
 * Standard IEC 61131-3 Counter Block telemetry with Retain Tag binding.
 */
typedef struct SPLC_PACKED {
    uint16_t status_bits;         /* Bit 0: CU, Bit 1: CD, Bit 2: RESET, Bit 3: Q */
    uint16_t mode;                /* SPLC_CounterMode_t (1: CTU, 2: CTD) */
    int32_t  preset_value;        /* Preset Value (PV, High Word first) */
    int32_t  current_value;       /* Current Value (CV, High Word first) */
    uint16_t retain_tag_index;    /* TagIndex of bound CV storage register (VREG, VREG_RETAIN, VFLAG, COUNTER) or 0xFFFF if unbound */
    uint16_t reserved;            /* Reserved, always 0 */
} SPLC_FbCounterRecord_t;

typedef enum {
    SPLC_RULE_FLAG_NONE          = 0x00,
    SPLC_RULE_FLAG_TRIGGER_FLOAT = 0x01,  /* Bit 0: 1 = Thresholds (threshold_lo, threshold_hi) are IEEE-754 Float32 */
    SPLC_RULE_FLAG_ACTION_FLOAT  = 0x02,  /* Bit 1: 1 = Action parameter (action_param) is IEEE-754 Float32 */
    SPLC_RULE_FLAG_FLOAT_DATA    = 0x03   /* Legacy alias: 1 = Both Trigger and Action are IEEE-754 Float32 */
} SPLC_RuleFlags_t;

/**
 * @brief Canonical 32-Byte Rule Record V2.0 (16 registers = 32 bytes).
 * Byte-exact layout for Active Table (0x0100) and Staging Buffer (0x9010).
 */
typedef struct SPLC_PACKED {
    union {
        int32_t  threshold_lo;        /* [0..1] Lower threshold (Int32 when flags=0) */
        float    threshold_lo_f;      /* [0..1] Lower threshold (Float32 when flags=1) */
    };
    union {
        int32_t  threshold_hi;        /* [2..3] Upper threshold (Int32 when flags=0) */
        float    threshold_hi_f;      /* [2..3] Upper threshold (Float32 when flags=1) */
    };
    uint32_t for_ms;                  /* [4..5] Dwell delay / interval duration in ms */
    union {
        int32_t  action_param;        /* [6..7] Action parameter value (Int32 when flags=0) */
        float    action_param_f;      /* [6..7] Action parameter value (Float32 when flags=1) */
    };
    uint16_t trigger_tag;             /* [8]    Trigger Tag Index (0..127) */
    uint16_t action_tag;              /* [9]    Action Tag Index (0..127) */
    uint16_t guard_tag;               /* [10]   Bit 15: NEGATE, Bits 0..14: Tag Index (0x7FFF=None) */
    uint8_t  enabled;                 /* [11H]  1 = Enabled, 0 = Disabled */
    uint8_t  trigger_type;            /* [11L]  SPLC_TriggerType_t */
    uint8_t  compare_op;              /* [12H]  SPLC_CompareOp_t */
    uint8_t  action_type;             /* [12L]  SPLC_ActionType_t */
    uint8_t  flags;                   /* [13H]  SPLC_RuleFlags_t: Bit 0=TRIGGER_FLOAT (0x01), Bit 1=ACTION_FLOAT (0x02) */
    uint8_t  reserved[5];             /* [13L..15] Reserved, sender writes 0x00 */
} SPLC_RuleRecord_t;

/**
 * @brief MQTT Telemetry Block Wire Record (96 registers = 192 bytes at 0x0C00 + (idx * 96)).
 * Defines independent MQTT publication stream, topic, timing, COV deadband, tags and JSON template.
 */
typedef struct SPLC_PACKED {
    /* Offset +0: Control Flags (Bits: Enabled, Mode, QoS, Retain, PayloadMode) */
    uint16_t control_flags;

    /* Offset +1: Enable Tag Index (0xFFFF = Always enabled) */
    uint16_t en_tag_index;

    /* Offset +2..3: Periodic Interval in ms (32-bit Big-Endian, High Word first) */
    uint32_t interval_ms;

    /* Offset +4..5: COV Deadband (Float32 IEEE 754, High Word first) */
    float    deadband;

    /* Offset +6: Tag count bound to this block (1..8) */
    uint16_t tag_count;

    /* Offset +7..14: Array of 8 Tag Indices (0xFFFF if slot unused) */
    uint16_t bound_tags[SPLC_MQTT_MAX_TAGS_PER_BLOCK];

    /* Offset +15: Reserved for alignment / diagnostics */
    uint16_t reserved_header;

    /* Offset +16..31: MQTT Topic (32 bytes ASCII null-padded, 2 chars/reg Big-Endian) */
    char     topic[SPLC_MQTT_MAX_TOPIC_CHARS];

    /* Offset +32..95: Custom JSON Payload Template (128 bytes ASCII null-padded) */
    char     payload_template[SPLC_MQTT_MAX_TEMPLATE_CHARS];
} SPLC_MqttTelemetryBlock_t;

/**
 * @brief MQTT System Broker Configuration Record (96 registers = 192 bytes at 0x0E00).
 * System-wide Broker endpoint, credentials, connection parameters and MCU live status feedback.
 */
typedef struct SPLC_PACKED {
    /* Offset +0: Broker Control Flags (Bits: Enabled, TLS, CleanSession, Version) */
    uint16_t broker_control;

    /* Offset +1: Broker Port (1883 for Plain TCP, 8883 for MQTTS TLS) */
    uint16_t broker_port;

    /* Offset +2: Keep-Alive interval in seconds (default 60) */
    uint16_t keepalive_sec;

    /* Offset +3: Reserved (Always 0x0000) */
    uint16_t reserved_0;

    /* Offset +4..35: Broker Hostname or IPv4/IPv6 (64 bytes ASCII null-padded) */
    char     broker_host[SPLC_MQTT_BROKER_HOST_MAX_CHARS];

    /* Offset +36..51: MQTT Client Identifier (32 bytes ASCII null-padded) */
    char     client_id[SPLC_MQTT_BROKER_CRED_MAX_CHARS];

    /* Offset +52..67: MQTT Username (32 bytes ASCII null-padded) */
    char     username[SPLC_MQTT_BROKER_CRED_MAX_CHARS];

    /* Offset +68..83: MQTT Password (32 bytes ASCII null-padded) */
    char     password[SPLC_MQTT_BROKER_CRED_MAX_CHARS];

    /* Offset +84..95: Live Diagnostic & Status Reflection (Read-Only updated by MCU) */
    uint16_t status_flags;        /* +84: Bit 0: Connected, Bit 1: TLS Active, Bit 2: Auth OK */
    uint16_t connection_latency;  /* +85: Round-trip Ping in ms */
    uint32_t tx_message_count;    /* +86..87: Total telemetry messages sent (High Word first) */
    uint32_t tx_byte_count;       /* +88..89: Total payload bytes transmitted (High Word first) */
    uint16_t last_error_code;     /* +90: SPLC_MqttBrokerErrorCode_t */
    uint16_t reserved_status[5];  /* +91..95: Reserved */
} SPLC_MqttBrokerConfig_t;

/**
 * @brief MQTT Subscription Pin Binding Record (10 registers = 20 bytes).
 * Defines target tag index, data type, default/failsafe value, and JSON key string.
 */
typedef struct SPLC_PACKED {
    uint16_t target_tag_index;
    uint16_t data_type;         /* 1=BOOL, 2=INT32, 3=REAL */
    union {
        uint32_t default_val_raw;
        int32_t  default_val_int;
        float    default_val_real;
    };
    char     json_key[SPLC_MQTT_MAX_JSON_KEY_CHARS]; /* 12 bytes null-padded */
} SPLC_MqttSubPinBinding_t;

/**
 * @brief MQTT Subscription Block Wire Record (64 registers = 128 bytes at 0x0D80 + (idx * 64)).
 * Defines inbound MQTT topic subscription, QoS, fail-safe timeout, and up to 4 pin bindings.
 */
typedef struct SPLC_PACKED {
    uint16_t control_flags;
    uint16_t reserved;
    uint32_t failsafe_timeout_ms;
    uint16_t pin_count;
    uint16_t reserved_pins[3];
    char     topic[SPLC_MQTT_MAX_TOPIC_CHARS]; /* 32 bytes null-padded */
    SPLC_MqttSubPinBinding_t pins[SPLC_MQTT_MAX_PINS_PER_SUB_BLOCK]; /* 4 pins * 20 bytes = 80 bytes */
} SPLC_MqttSubscriptionBlock_t;

/**
 * @brief MQTT TLS Certificate Staging Chunk Wire Record (64 registers = 128 bytes at 0x0F00).
 * SPLC-SPEC-MQTT-003: Chunked transfer of X.509 CA/Client certificates.
 */
typedef struct SPLC_PACKED {
    uint8_t  cert_type;        /* 0x01: Root CA, 0x02: Client Cert, 0x03: Client Key */
    uint8_t  chunk_index;      /* Chunk sequence: 0, 1, 2, ... (total_chunks - 1) */
    uint8_t  total_chunks;     /* Total number of chunks (N <= 32) */
    uint8_t  chunk_len;        /* Valid data byte length in payload (1..120) */
    uint16_t total_cert_size;  /* Total size of full certificate in bytes */
    uint16_t chunk_crc16;      /* CRC-16/MODBUS (Polynomial 0xA001) over payload */
    uint8_t  payload[SPLC_MQTT_CERT_PAYLOAD_MAX_BYTES]; /* 120 bytes PEM/DER data */
} SPLC_CertChunk_t;

/**
 * @brief Modbus Master Pin Binding Record (12 registers = 24 bytes).
 * Defines target tag index, register offset, data type, endianness, multiplier, and pin label.
 */
typedef struct SPLC_PACKED {
    uint16_t target_tag_index;
    uint16_t reg_offset;
    uint16_t data_type;         /* 1=BOOL, 2=INT16, 3=UINT16, 4=INT32, 5=UINT32, 6=REAL */
    uint16_t endianness;        /* 0=ABCD, 1=CDAB, 2=BADC, 3=DCBA */
    float    multiplier;        /* IEEE 754 32-bit float */
    char     pin_label[SPLC_MBM_MAX_LABEL_CHARS]; /* 12 bytes null-padded */
} SPLC_ModbusMasterPinBinding_t;

/**
 * @brief Modbus Master Read Block Wire Record (64 registers = 128 bytes at 0x1000 + (idx * 64)).
 * Defines polling transaction: Slave ID, Function Code, Start Address, Reg Count, Interval, Timeout,
 * status tags, and up to 4 dynamic pin bindings.
 */
typedef struct SPLC_PACKED {
    uint16_t control_flags;
    uint8_t  slave_id;
    uint8_t  function_code;
    uint16_t start_address;
    uint16_t reg_count;
    uint32_t poll_interval_ms;
    uint32_t timeout_ms;
    uint16_t valid_tag_index;
    uint16_t error_tag_index;
    uint16_t err_code_tag_index;
    uint16_t pin_count;
    uint16_t read_mode;          /* [12] SPLC_MBM_READ_MODE: 0 = Cyclic, 1 = OnTriggerRisingEdge */
    uint16_t trig_tag_index;     /* [13] Tag index for rising edge trigger (0xFFFF if none) */
    uint16_t reserved[2];        /* [14..15] Reserved for future expansion */
    SPLC_ModbusMasterPinBinding_t pins[SPLC_MBM_MAX_PINS_PER_BLOCK]; /* 4 pins * 24 bytes = 96 bytes */
} SPLC_ModbusMasterReadBlock_t;

/**
 * @brief Modbus Master Write Block Wire Record (64 registers = 128 bytes at 0x1100 + (idx * 64)).
 * Defines write transaction: Slave ID, Function Code, Mode (Direct vs Channel-Indexed),
 * Start Address / Base Addresses, Channel / Point Count, Trigger Mode, and payload bindings.
 */
typedef struct SPLC_PACKED {
    uint16_t control_flags;             /* Bit 0=Enabled, Bit 1=Exclusive, Bit 2..3=Mode (0=Direct, 1=ChannelIndexed) */
    uint8_t  slave_id;
    uint8_t  function_code;             /* FC05, FC06, FC15, FC16 */
    uint16_t start_or_base_cmd;         /* Direct: Start Address; Indexed: Base Command Address */
    uint16_t count_or_channels;         /* Direct: Point Count (1..16); Indexed: Channel Count (1..64) */
    uint32_t base_address_param;        /* Indexed: Base Param / Color Address */
    uint32_t cyclic_interval_ms;
    uint32_t timeout_ms;
    uint16_t trigger_mode;              /* 0=Edge, 1=Change, 2=Cyclic */
    uint16_t trig_tag_index;
    uint16_t done_tag_index;
    uint16_t busy_tag_index;
    uint16_t error_tag_index;
    uint16_t err_code_tag_index;
    union {
        struct {
            uint16_t channel_tag_index;
            uint16_t command_tag_index;
            uint16_t param_tag_index;
            uint16_t reserved[45];
        } indexed;
        SPLC_ModbusMasterPinBinding_t direct_pins[SPLC_MBM_MAX_PINS_PER_BLOCK]; /* 4 * 24 = 96 bytes */
    } payload;
} SPLC_ModbusMasterWriteBlock_t;

typedef enum {
    SPLC_DIR_PIN_DO0 = 0,
    SPLC_DIR_PIN_DO1 = 1,
    SPLC_DIR_PIN_DO2 = 2,
    SPLC_DIR_PIN_DO3 = 3,
    SPLC_DIR_PIN_DO4 = 4,
    SPLC_DIR_PIN_DO5 = 5,
    SPLC_DIR_PIN_DO6 = 6,
    SPLC_DIR_PIN_DO7 = 7
} SPLC_DirectPin_t;

typedef enum {
    SPLC_DIR_MODE_DIGITAL_OUTPUT  = 0, /* Direct logic output 0/1 (Relay / Solenoid / Lamp) */
    SPLC_DIR_MODE_PWM_DUTY        = 1, /* PWM Duty Cycle 0..100% (Dimmer / Motor Speed) */
    SPLC_DIR_MODE_PULSE_TRAIN     = 2, /* Pulse Train Output (PTO for Stepper / Servo) */
    SPLC_DIR_MODE_ADDRESSABLE_BUS = 3  /* 1-wire addressable bus (UCS16703 / WS2812 / Actuator) */
} SPLC_DirectMode_t;

typedef enum {
    SPLC_DIR_TRIG_RISING_EDGE   = 0,     /* Trigger execution on rising edge of Trig connector */
    SPLC_DIR_TRIG_CONTINUOUS    = 1,     /* Continuous cyclic execution every scan pass */
    SPLC_DIR_TRIG_COV           = 2      /* Change-of-Value: execute only when delta >= deadband_x1000/1000.0 */
} SPLC_DirectTrigger_t;

/**
 * @brief DIRECT_WRITE Block Configuration (32 bytes = 16 Modbus registers).
 * Located at 0x1200..0x12FF (Up to 8 blocks: 0x1200 + BlockIndex * 16).
 */
typedef struct {
    uint8_t  target_pin;        /* SPLC_DirectPin_t: DO0..DO7 (0..7) */
    uint8_t  mode;              /* SPLC_DirectMode_t: 0=DO, 1=PWM, 2=PTO, 3=Bus */
    uint16_t channel_count;     /* Number of channels (Bus mode) or pulses count */
    uint8_t  flags;             /* Bit 0: is_active_high, Bit 1: is_exclusive, Bit 2: fail_safe */
    uint8_t  trigger_mode;      /* SPLC_DirectTrigger_t: 0=RisingEdge, 1=Continuous, 2=COV */
    uint16_t pulse_width_us;    /* Base pulse width in microseconds */
    uint32_t pwm_freq_hz;       /* PWM Frequency (Hz) */
    uint8_t  duty_min;          /* PWM Duty Minimum % (0..100) */
    uint8_t  duty_max;          /* PWM Duty Maximum % (0..100) */
    uint16_t channel_tag_index; /* Tag index for logic in / duty % / bus channel */
    uint16_t param_tag_index;   /* Tag index for enable / frequency / bus parameter */
    uint32_t default_color_rgb; /* Default 24-bit RGB parameter (0x00RRGGBB) */
    uint16_t deadband_x1000;    /* COV deadband * 1000 (e.g. 500 = 0.5 units); 0xFFFF = not set */
    uint8_t  reserved[8];       /* Padding to align to exactly 32 bytes */
} SPLC_DirectWriteBlock_t;

/* Standardized aliases for PULSE_OUT (Pulse Output / Hardware Pulse Actuator) */
typedef SPLC_DirectPin_t        SPLC_PulsePin_t;
typedef SPLC_DirectMode_t       SPLC_PulseMode_t;
typedef SPLC_DirectTrigger_t    SPLC_PulseTrigger_t;
typedef SPLC_DirectWriteBlock_t SPLC_PulseOutBlock_t;

/**
 * @brief Wi-Fi Configuration & Live Telemetry Record (64 registers = 128 bytes at 0x1300).
 * SPLC-SPEC-WIFI-004-ICD: ST67 (ST67W611M1) Wi-Fi 6 subsystem settings & live status.
 */
typedef struct SPLC_PACKED {
    uint16_t control_flags;     /* Bit 0: Enable, Bit 1..2: Mode (0=STA, 1=AP), Bit 3: DHCP, Bit 4: Auto-reconnect */
    char     ssid[32];          /* SSID ASCII null-terminated */
    char     password[64];      /* WPA2/WPA3 password ASCII null-terminated */
    uint32_t static_ip;         /* Static IPv4 (Network byte order / Big-Endian) */
    uint32_t static_subnet;     /* Static Subnet Mask */
    uint32_t static_gateway;    /* Static Gateway */
    uint32_t static_dns;        /* Static DNS */
    uint16_t conn_status;       /* SPLC_WIFI_CONN_* (RO) */
    int16_t  rssi_dbm;          /* Live signal strength dBm (-100..0) (RO) */
    uint32_t current_ip;        /* Assigned IP from DHCP or static (RO) */
    uint8_t  mac_addr[6];       /* Hardware MAC address of ST67 (RO) */
} SPLC_WifiConfig_t;

/**
 * @brief Wi-Fi Command & Scan Control Record (16 registers = 32 bytes at 0x1340).
 * SPLC-SPEC-WIFI-004-ICD: Command handshake & background scan progress.
 */
typedef struct SPLC_PACKED {
    uint16_t command;           /* W: SPLC_WIFI_CMD_* (0x0001=Scan, 0x0002=Connect, 0x0003=Disconnect, 0x0004=Forget) */
    uint16_t scan_state;        /* R: SPLC_WIFI_SCAN_STATE_* (0=Idle, 1=Scanning, 2=Completed, 3=Failed) */
    uint16_t ap_found_count;    /* R: AP count found (0..8) */
    uint16_t error_code;        /* R: Error code (0=None, 1=Busy, 2=Timeout, 3=DriverError) */
    uint8_t  reserved[24];      /* Padding to align to exactly 32 bytes */
} SPLC_WifiScanControl_t;

/**
 * @brief Wi-Fi Access Point Info Record (22 registers = 44 bytes at 0x1350 + idx * 22).
 * SPLC-SPEC-WIFI-004-ICD: Scanned network details from ST67.
 */
typedef struct SPLC_PACKED {
    char    ssid[32];           /* SSID ASCII null-terminated */
    int16_t rssi_dbm;           /* Signal strength in dBm */
    uint8_t auth_mode;          /* SPLC_WIFI_AUTH_* (0=Open, 1=WEP, 2=WPA, 3=WPA2, 4=WPA2/3, 5=Ent, 6=WPA3) */
    uint8_t channel;            /* RF Channel (1..14) */
    uint8_t bssid[6];           /* BSSID MAC address of AP */
    uint8_t reserved[2];        /* Padding to align to exactly 44 bytes */
} SPLC_WifiApInfo_t;

/**
 * @brief Multi-Port RS-485 Modbus Master Port Record (16 registers = 32 bytes at 0x1400 + port_idx * 16).
 * SPLC-SPEC-MBM-003: Physical RS-485 port configuration.
 */
typedef struct SPLC_PACKED {
    uint16_t control_flags;         /* +0: Bit 8 = ENABLED (0x0100), Bit 0..7 = PORT_NUMBER (1..4) */
    uint32_t baud_rate;             /* +1..+2: Baud Rate (uint32 Big-Endian) */
    uint16_t parity;                /* +3: 0 = None, 1 = Even, 2 = Odd */
    uint16_t framing;               /* +4: High Byte: DataBits (7, 8), Low Byte: StopBits (1, 2) */
    uint16_t response_timeout_ms;   /* +5: Timeout in ms (50..5000) */
    uint16_t inter_frame_delay_ms;  /* +6: Inter-frame delay in ms (10..500) */
    uint16_t retry_count;           /* +7: Retry count (0..5) */
    char     port_name[12];         /* +8..+13: 12 bytes ASCII packed string */
    uint16_t reserved[2];           /* +14..+15: Reserved (always 0x0000) */
} SPLC_ModbusPortConfigRecord_t;

#pragma pack(pop)

/* Compile-time static assertions for exact memory layout */
#if defined(__STDC_VERSION__) && __STDC_VERSION__ >= 201112L
_Static_assert(sizeof(SPLC_DeviceDescriptor_t)      == 20,  "Descriptor must be 20 bytes (10 regs)");
_Static_assert(sizeof(SPLC_DeviceResourceInfo_t)    == 20,  "ResourceInfo must be 20 bytes (10 regs)");
_Static_assert(sizeof(SPLC_DeviceHealth_t)          == 20,  "Health must be 20 bytes (10 regs)");
_Static_assert(sizeof(SPLC_RtcClock_t)              == 8,   "RTC must be 8 bytes (4 regs)");
_Static_assert(sizeof(SPLC_DiagBlock_t)             == 10,  "DiagBlock must be 10 bytes (5 regs)");
_Static_assert(sizeof(SPLC_FbTimerRecord_t)         == 16,  "FbTimer must be 16 bytes (8 regs)");
_Static_assert(sizeof(SPLC_FbCounterRecord_t)       == 16,  "FbCounter must be 16 bytes (8 regs)");
_Static_assert(sizeof(SPLC_RuleRecord_t)            == 32,  "RuleRecord must be 32 bytes (16 regs)");
_Static_assert(sizeof(SPLC_MqttTelemetryBlock_t)    == 192, "MqttTelemetryBlock must be 192 bytes (96 regs)");
_Static_assert(sizeof(SPLC_MqttSubPinBinding_t)     == 20,  "MqttSubPinBinding must be 20 bytes (10 regs)");
_Static_assert(sizeof(SPLC_MqttSubscriptionBlock_t) == 128, "MqttSubscriptionBlock must be 128 bytes (64 regs)");
_Static_assert(sizeof(SPLC_CertChunk_t)          == 128, "CertChunk must be 128 bytes (64 regs)");
_Static_assert(sizeof(SPLC_MqttBrokerConfig_t)      == 192, "MqttBrokerConfig must be 192 bytes (96 regs)");
_Static_assert(sizeof(SPLC_ModbusMasterPinBinding_t)== 24,  "ModbusMasterPinBinding must be 24 bytes (12 regs)");
_Static_assert(sizeof(SPLC_ModbusMasterReadBlock_t) == 128, "ModbusMasterReadBlock must be 128 bytes (64 regs)");
_Static_assert(sizeof(SPLC_ModbusMasterWriteBlock_t)== 128, "ModbusMasterWriteBlock must be 128 bytes (64 regs)");
_Static_assert(sizeof(SPLC_DirectWriteBlock_t)      == 32,  "DirectWriteBlock must be 32 bytes (16 regs)");
_Static_assert(sizeof(SPLC_PulseOutBlock_t)         == 32,  "PulseOutBlock must be 32 bytes (16 regs)");
_Static_assert(sizeof(SPLC_WifiConfig_t)            == 128, "WifiConfig must be 128 bytes (64 regs)");
_Static_assert(sizeof(SPLC_WifiScanControl_t)       == 32,  "WifiScanControl must be 32 bytes (16 regs)");
_Static_assert(sizeof(SPLC_WifiApInfo_t)            == 44,  "WifiApInfo must be 44 bytes (22 regs)");
_Static_assert(sizeof(SPLC_ModbusPortConfigRecord_t) == 32, "ModbusPortConfigRecord must be 32 bytes (16 regs)");
#endif

/* ========================================================================= */
/* 6. HELPER INLINE FUNCTIONS (Endianness & Calculations)                     */
/* ========================================================================= */

/**
 * @brief Pack two 16-bit registers into a 32-bit integer (High Word first).
 */
static inline int32_t splc_regs_to_int32(uint16_t hi, uint16_t lo) {
    return (int32_t)(((uint32_t)hi << 16) | (uint32_t)lo);
}

/**
 * @brief Pack two 16-bit registers into a 32-bit unsigned integer (High Word first).
 */
static inline uint32_t splc_regs_to_uint32(uint16_t hi, uint16_t lo) {
    return (((uint32_t)hi << 16) | (uint32_t)lo);
}

/**
 * @brief Pack two 16-bit registers into an IEEE-754 32-bit float (High Word first).
 */
static inline float splc_regs_to_float(uint16_t hi, uint16_t lo) {
    uint32_t u = (((uint32_t)hi << 16) | (uint32_t)lo);
    float f;
    memcpy(&f, &u, sizeof(float));
    return f;
}

/**
 * @brief Unpack a 32-bit unsigned integer into two 16-bit registers (High Word first).
 */
static inline void splc_uint32_to_regs(uint32_t val, uint16_t *hi, uint16_t *lo) {
    *hi = (uint16_t)((val >> 16) & 0xFFFF);
    *lo = (uint16_t)(val & 0xFFFF);
}

/**
 * @brief Unpack an IEEE-754 32-bit float into two 16-bit registers (High Word first).
 */
static inline void splc_float_to_regs(float val, uint16_t *hi, uint16_t *lo) {
    uint32_t u;
    memcpy(&u, &val, sizeof(float));
    *hi = (uint16_t)((u >> 16) & 0xFFFF);
    *lo = (uint16_t)(u & 0xFFFF);
}

/**
 * @brief Compute CRC-16 Modbus (Polynomial 0xA001, Init 0xFFFF).
 */
static inline uint16_t splc_crc16_modbus(const uint8_t *buffer, uint16_t length) {
    uint16_t crc = 0xFFFF;
    for (uint16_t i = 0; i < length; i++) {
        crc ^= (uint16_t)buffer[i];
        for (uint8_t bit = 0; bit < 8; bit++) {
            if (crc & 0x0001) {
                crc = (crc >> 1) ^ 0xA001;
            } else {
                crc = crc >> 1;
            }
        }
    }
    return crc;
}

/**
 * @brief Compute current local time in HHmm format from Epoch UTC and Timezone offset.
 * @param epoch_utc_s Current Unix timestamp in seconds
 * @param tz_offset_min Timezone offset in minutes (e.g. +420 for UTC+7)
 * @return Integer HHmm (e.g. 700 for 07:00, 1830 for 18:30)
 */
static inline int splc_compute_local_hhmm(uint32_t epoch_utc_s, int16_t tz_offset_min) {
    int64_t local_epoch = (int64_t)epoch_utc_s + ((int64_t)tz_offset_min * 60);
    int32_t seconds_of_day = (int32_t)(local_epoch % 86400);
    if (seconds_of_day < 0) {
        seconds_of_day += 86400;
    }
    int hours = seconds_of_day / 3600;
    int minutes = (seconds_of_day % 3600) / 60;
    return (hours * 100) + minutes;
}

/**
 * @brief Evaluate whether the current local HHmm falls within a Rule's Time Window.
 * Handles both intra-day ranges (Lo <= Hi) and cross-midnight ranges (Lo > Hi).
 * @param current_hhmm Current time (e.g. 700, 1830)
 * @param threshold_lo Lower threshold (e.g. 700, 1800)
 * @param threshold_hi Upper threshold (e.g. 1700, 600)
 * @param op CompareOp (SPLC_CMP_EQ, SPLC_CMP_BETWEEN, etc.)
 */
static inline bool splc_is_time_window_active(int current_hhmm, int32_t threshold_lo, int32_t threshold_hi, uint8_t op) {
    if (op == SPLC_CMP_EQ) {
        return current_hhmm == threshold_lo;
    }

    if (threshold_lo <= threshold_hi) {
        /* Standard intra-day window (e.g. 07:00 to 17:00) */
        return (current_hhmm >= threshold_lo) && (current_hhmm <= threshold_hi);
    } else {
        /* Cross-midnight overnight window (e.g. 18:00 to 06:00) */
        return (current_hhmm >= threshold_lo) || (current_hhmm <= threshold_hi);
    }
}

#ifdef __cplusplus
}
#endif

#endif /* SIMPLEPLC_PROTOCOL_V2_0_H */
