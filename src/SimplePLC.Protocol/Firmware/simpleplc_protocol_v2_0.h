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

#define SPLC_ADDR_DIAG_COMMAND            0x0A20  /* 1 reg: SPLC_DiagCommand_t (FC06/FC16, WO) */
#define SPLC_ADDR_DIAG_STATE              0x0A21  /* 1 reg: SPLC_DiagState_t (FC03, RO) */
#define SPLC_ADDR_DIAG_FLAGS              0x0A22  /* 1 reg: SPLC_DiagFlags_t (FC03, RO) */
#define SPLC_ADDR_DIAG_LEASE_REMAINING    0x0A23  /* 1 reg: Remaining lease in ms (FC03, RO) */
#define SPLC_ADDR_DIAG_ERROR_CODE         0x0A24  /* 1 reg: SPLC_DiagErrorCode_t (FC03, RO) */
#define SPLC_LEN_DIAG_BLOCK               5

/* --- 2.6 Function Block Table Subsystem V2 (0x0B00 - 0x0B7F) --- */
#define SPLC_ADDR_FB_TIMER_TABLE          0x0B00  /* 64 regs: 8 Timers * 8 regs (FC03, RO) */
#define SPLC_LEN_FB_TIMER_TABLE           64

#define SPLC_ADDR_FB_COUNTER_TABLE        0x0B40  /* 64 regs: 8 Counters * 8 regs (FC03, RO) */
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

/* --- 2.7 Rule Staging Buffer & Commit Space (0x9000 - 0xA001) --- */
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
/* 3. REMOTE I/O V1 TAG INDEX LAYOUT (0..127)                                */
/* ========================================================================= */

#define SPLC_TAG_DI_START                 0
#define SPLC_TAG_DI_COUNT                 8   /* Tag 0..7   : DI0..DI7 (Discrete Inputs, Read-Only) */

#define SPLC_TAG_DO_START                 8
#define SPLC_TAG_DO_COUNT                 8   /* Tag 8..15  : DO0..DO7 (Discrete Outputs, Read-Write) */

#define SPLC_TAG_AI_START                 16
#define SPLC_TAG_AI_COUNT                 4   /* Tag 16..19 : AI0..AI3 (Analog Inputs, Read-Only) */

#define SPLC_TAG_VFLAG_START              20
#define SPLC_TAG_VFLAG_COUNT              32  /* Tag 20..51 : VFLAG0..VFLAG31 (Virtual Flags 0/1, Read-Write) */

#define SPLC_TAG_VREG_START               52
#define SPLC_TAG_VREG_COUNT               32  /* Tag 52..83 : VREG0..VREG31 (Volatile int32, Read-Write) */

#define SPLC_TAG_VREG_RETAIN_START        84
#define SPLC_TAG_VREG_RETAIN_COUNT        32  /* Tag 84..115: VREG_RETAIN0..31 (Flash Retained int32, Read-Write) */

#define SPLC_TAG_COUNTER_START            116
#define SPLC_TAG_COUNTER_COUNT            8   /* Tag 116..123: COUNTER0..COUNTER7 (Int32 counters) */

#define SPLC_TAG_RESERVED_START           124
#define SPLC_TAG_RESERVED_COUNT           4   /* Tag 124..127: Reserved slots */

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
    SPLC_TIMER_MODE_TP            = 3  /* Pulse Timer */
} SPLC_TimerMode_t;

typedef enum {
    SPLC_COUNTER_MODE_NONE        = 0,
    SPLC_COUNTER_MODE_CTU         = 1, /* Count Up */
    SPLC_COUNTER_MODE_CTD         = 2  /* Count Down */
} SPLC_CounterMode_t;

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
    uint16_t reserved[2];         /* Reserved, always 0 */
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
    uint16_t retain_tag_index;    /* TagIndex of bound VREG_RETAIN (84..115) or 0xFFFF */
    uint16_t reserved;            /* Reserved, always 0 */
} SPLC_FbCounterRecord_t;

/**
 * @brief Canonical 32-Byte Rule Record V1.7 (16 registers = 32 bytes).
 * Byte-exact layout for Active Table (0x0100) and Staging Buffer (0x9010).
 */
typedef struct SPLC_PACKED {
    int32_t  threshold_lo;        /* [0..1] Lower threshold (High Word first) */
    int32_t  threshold_hi;        /* [2..3] Upper threshold (High Word first) */
    uint32_t for_ms;              /* [4..5] Dwell delay / interval duration in ms */
    int32_t  action_param;        /* [6..7] Action parameter value */
    uint16_t trigger_tag;         /* [8]    Trigger Tag Index (0..127) */
    uint16_t action_tag;          /* [9]    Action Tag Index (0..127) */
    uint16_t guard_tag;           /* [10]   Bit 15: NEGATE, Bits 0..14: Tag Index (0x7FFF=None) */
    uint8_t  enabled;             /* [11H]  1 = Enabled, 0 = Disabled */
    uint8_t  trigger_type;        /* [11L]  SPLC_TriggerType_t */
    uint8_t  compare_op;          /* [12H]  SPLC_CompareOp_t */
    uint8_t  action_type;         /* [12L]  SPLC_ActionType_t */
    uint8_t  reserved[6];         /* [13..15] Reserved, sender writes 0x00 */
} SPLC_RuleRecord_t;

#pragma pack(pop)

/* Compile-time static assertions for exact memory layout */
#if defined(__STDC_VERSION__) && __STDC_VERSION__ >= 201112L
_Static_assert(sizeof(SPLC_DeviceDescriptor_t)   == 20, "Descriptor must be 20 bytes (10 regs)");
_Static_assert(sizeof(SPLC_DeviceResourceInfo_t) == 20, "ResourceInfo must be 20 bytes (10 regs)");
_Static_assert(sizeof(SPLC_DeviceHealth_t)       == 20, "Health must be 20 bytes (10 regs)");
_Static_assert(sizeof(SPLC_RtcClock_t)           == 8,  "RTC must be 8 bytes (4 regs)");
_Static_assert(sizeof(SPLC_DiagBlock_t)          == 10, "DiagBlock must be 10 bytes (5 regs)");
_Static_assert(sizeof(SPLC_FbTimerRecord_t)      == 16, "FbTimer must be 16 bytes (8 regs)");
_Static_assert(sizeof(SPLC_FbCounterRecord_t)    == 16, "FbCounter must be 16 bytes (8 regs)");
_Static_assert(sizeof(SPLC_RuleRecord_t)         == 32, "RuleRecord must be 32 bytes (16 regs)");
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
 * @brief Unpack a 32-bit unsigned integer into two 16-bit registers (High Word first).
 */
static inline void splc_uint32_to_regs(uint32_t val, uint16_t *hi, uint16_t *lo) {
    *hi = (uint16_t)((val >> 16) & 0xFFFF);
    *lo = (uint16_t)(val & 0xFFFF);
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
