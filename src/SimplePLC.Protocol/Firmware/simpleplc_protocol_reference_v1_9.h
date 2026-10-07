/**
 * @file simpleplc_protocol_reference_v1_9.h
 * @brief SimplePLC Wire Protocol & Embedded Contract Reference Header V1.9
 * @version 1.9.0
 * 
 * Standard Modbus RTU (FC03, FC06, FC16).
 * 
 * AUTHORITY NOTICE:
 * This C header is a Reference Artifact for Embedded MCU Developers (STM32, ESP32, RP2040).
 * If any discrepancy arises between this header and the Platform Contract / Golden Vectors:
 *   1. docs/platform/SimplePLC_Wire_Contract_V1_9.md (PRIMARY AUTHORITY)
 *   2. tests/SimplePLC.Protocol.Tests/GoldenVectors/golden_vectors_v1_9.json (CANONICAL VECTORS)
 *   3. C# Protocol Freeze Tests
 * always take precedence over this header.
 * 
 * Big-Endian 16-bit register wire order.
 */

#ifndef SIMPLEPLC_PROTOCOL_REFERENCE_V1_9_H
#define SIMPLEPLC_PROTOCOL_REFERENCE_V1_9_H

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
/* 1. PROTOCOL CONSTANTS & SIZES                                             */
/* ========================================================================= */

#define SPLC_PROTOCOL_VERSION             1
#define SPLC_RULE_FORMAT_VERSION          7  /* V1.7 (32-byte records) */

#define SPLC_REGISTERS_PER_RULE           16
#define SPLC_BYTES_PER_RULE               32
#define SPLC_MAX_RULES                    100

#define SPLC_MAX_RUNTIME_TAGS             128
#define SPLC_REGISTERS_PER_TAG            2
#define SPLC_BYTES_PER_TAG                4
#define SPLC_ACTIVE_TAGS_REMOTE_IO        124

#define SPLC_COMMIT_MAGIC                 0xA5A5
#define SPLC_GUARD_NEGATE_MASK            0x8000
#define SPLC_GUARD_TAG_MASK               0x7FFF
#define SPLC_GUARD_TAG_NONE               0x7FFF  /* Sentinel value in bits 0..14 indicating NO GUARD */

/* ========================================================================= */
/* 2. MODBUS HOLDING REGISTER MAP (16-bit word addresses)                    */
/* ========================================================================= */

/* Core & Runtime Space (0x0000 - 0x0A02) */
#define SPLC_ADDR_DESCRIPTOR              0x0000  /* 10 registers = 20 bytes (FC03) */
#define SPLC_LEN_DESCRIPTOR               10

#define SPLC_ADDR_RULE_TABLE_INFO         0x0010  /* 1 register (active rule count) */
#define SPLC_LEN_RULE_TABLE_INFO          1

#define SPLC_ADDR_DEVICE_RESOURCE         0x0020  /* 10 registers = 20 bytes (FC03, Contract V1.9) */
#define SPLC_LEN_DEVICE_RESOURCE          10

#define SPLC_ADDR_ACTIVE_RULES            0x0100  /* 1600 registers = 100 rules * 16 (FC03) */
#define SPLC_LEN_ACTIVE_RULES_MAX         1600

#define SPLC_ADDR_HEALTH                  0x0800  /* 10 registers = 20 bytes (FC03) */
#define SPLC_LEN_HEALTH                   10

#define SPLC_ADDR_RTC_CLOCK               0x0810  /* 4 registers = 8 bytes (FC03/FC16, Contract V1.9) */
#define SPLC_LEN_RTC_CLOCK                4

#define SPLC_ADDR_TAGS                    0x0900  /* 256 registers = 128 tags * 2 (FC03) */
#define SPLC_LEN_TAGS                     256

#define SPLC_ADDR_SYSCOMMAND              0x0A00  /* 1 register (FC06/FC16) */
#define SPLC_LEN_SYSCOMMAND               1

#define SPLC_ADDR_SYSCOMMAND_RESULT       0x0A01  /* 2 registers: [0]=Status, [1]=ErrorCode */
#define SPLC_LEN_SYSCOMMAND_RESULT        2

/* Staging & Commit Space (0x9000 - 0xA001) */
#define SPLC_ADDR_CONFIG_STATUS           0x9000  /* 1 register: SPLC_CommandStatus */
#define SPLC_ADDR_CONFIG_ERROR_CODE       0x9001  /* 1 register: SPLC_ErrorCode */
#define SPLC_ADDR_RULE_COUNT_STAGED       0x9002  /* 1 register: count to stage */
#define SPLC_ADDR_EXPECTED_CRC16          0x9003  /* 1 register: CRC-16 of staged payload */
#define SPLC_ADDR_ACTIVE_RULE_COUNT       0x9004  /* 1 register: active rule count */
#define SPLC_ADDR_ACTIVE_RULE_CRC16       0x9005  /* 1 register: active CRC-16 */
#define SPLC_ADDR_STAGING_RESERVED        0x9006  /* 10 registers reserved */

#define SPLC_ADDR_STAGING_RULES           0x9010  /* 1600 registers (FC16) */
#define SPLC_LEN_STAGING_RULES_MAX        1600

#define SPLC_ADDR_COMMIT_COMMAND          0xA000  /* 1 register: write SPLC_COMMIT_MAGIC (FC06/FC16) */
#define SPLC_ADDR_ACTIVE_RULE_VERSION     0xA001  /* 1 register: version increment on commit */

/* ========================================================================= */
/* 3. REMOTE I/O V1 TAG INDEX LAYOUT (0..127)                                */
/* ========================================================================= */

#define SPLC_TAG_DI_START                 0
#define SPLC_TAG_DI_COUNT                 8   /* Tag 0..7   : DI0..DI7 */

#define SPLC_TAG_DO_START                 8
#define SPLC_TAG_DO_COUNT                 8   /* Tag 8..15  : DO0..DO7 */

#define SPLC_TAG_AI_START                 16
#define SPLC_TAG_AI_COUNT                 4   /* Tag 16..19 : AI0..AI3 */

#define SPLC_TAG_VFLAG_START              20
#define SPLC_TAG_VFLAG_COUNT              32  /* Tag 20..51 : VFLAG0..VFLAG31 */

#define SPLC_TAG_VREG_START               52
#define SPLC_TAG_VREG_COUNT               32  /* Tag 52..83 : VREG0..VREG31 */

#define SPLC_TAG_VREG_RETAIN_START        84
#define SPLC_TAG_VREG_RETAIN_COUNT        32  /* Tag 84..115: VREG_RETAIN0..VREG_RETAIN31 */

#define SPLC_TAG_COUNTER_START            116
#define SPLC_TAG_COUNTER_COUNT            8   /* Tag 116..123: COUNTER0..COUNTER7 */

#define SPLC_TAG_RESERVED_START           124
#define SPLC_TAG_RESERVED_COUNT           4   /* Tag 124..127: Reserved */

#define SPLC_TAG_TOTAL_ACTIVE             124
#define SPLC_TAG_TOTAL_CAPACITY           128

/* ========================================================================= */
/* 4. ENUMS (Contract Semantics)                                             */
/* ========================================================================= */

/**
 * @brief Platform TagKind Contract V1 (Spec M?c 3.2).
 */
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
    SPLC_CONFIG_STATUS_IDLE       = 0,
    SPLC_CONFIG_STATUS_RECEIVING  = 1,
    SPLC_CONFIG_STATUS_VERIFYING  = 2,
    SPLC_CONFIG_STATUS_READY      = 3,
    SPLC_CONFIG_STATUS_ERROR      = 4
} SPLC_ConfigStatus_t;

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

/* ========================================================================= */
/* 5. PROTOCOL BINARY STRUCTURES (Strict Byte Exact Layouts)                 */
/* ========================================================================= */

/**
 * @brief Device Descriptor (10 registers = 20 bytes at 0x0000).
 * Big-endian 16-bit register order over Modbus.
 */
typedef struct SPLC_PACKED {
    uint16_t device_class;        /* SPLC_DeviceClass_t */
    uint16_t device_variant;      /* e.g. SPLC_RemoteIoVariant_t */
    uint16_t hw_version_major;
    uint16_t hw_version_minor;
    uint16_t hw_version_patch;
    uint16_t fw_version_major;
    uint16_t fw_version_minor;
    uint16_t fw_version_patch;
    uint16_t protocol_version;    /* Must be 1 */
    uint16_t rule_format_version; /* Must be 7 for V1.7 */
} SPLC_DeviceDescriptor_t;

/**
 * @brief Device Resource Info (10 registers = 20 bytes at 0x0020, Contract V1.9).
 * Big-endian 16-bit register order over Modbus. Self-Describing Profile.
 */
typedef struct SPLC_PACKED {
    uint16_t wire_profile;           /* Must be 1 for Wire Profile V1 */
    uint16_t max_rules;              /* Maximum rule engine capacity (e.g. 100) */
    uint16_t runtime_tag_count;      /* Total active tag slots (e.g. 124) */
    uint16_t di_count;               /* Digital inputs (0..8) */
    uint16_t do_count;               /* Digital outputs (0..8) */
    uint16_t ai_count;               /* Analog inputs (0..4) */
    uint16_t vflag_count;            /* Virtual flags (0..32) */
    uint16_t vreg_count;             /* Virtual registers (0..32) */
    uint16_t vreg_retain_count;      /* Retentive virtual registers (0..32) */
    uint16_t counter_count;          /* Counters (0..8) */
} SPLC_DeviceResourceInfo_t;

/**
 * @brief Device Health (10 registers = 20 bytes at 0x0800).
 * Uptime and scan times are in milliseconds / seconds.
 */
typedef struct SPLC_PACKED {
    uint32_t uptime_s;            /* Seconds since boot */
    uint16_t reset_reason;        /* SPLC_ResetReason_t */
    uint16_t health_flags;        /* SPLC_HealthFlags_t bitmask */
    uint16_t cpu_load_percent;    /* 0..100 % */
    uint16_t ram_usage_percent;   /* 0..100 % */
    uint32_t scan_time_ms;        /* Last cycle scan time in ms (nominal 10 ms) */
    uint32_t max_scan_time_ms;    /* Max cycle scan time since boot in ms */
} SPLC_DeviceHealth_t;

/**
 * @brief RTC Clock (4 registers = 8 bytes at 0x0810, Contract V1.9).
 * Big-endian 16-bit register order over Modbus.
 */
typedef struct SPLC_PACKED {
    uint32_t epoch_utc_s;         /* Seconds since 01/01/1970 UTC (big-endian high word first) */
    int16_t  tz_offset_min;       /* Timezone offset in minutes (e.g. +420 for UTC+7) */
    uint16_t status_flags;        /* bit 0: IS_SYNCED, bit 1: HW_RTC, bit 2: BATT_LOW */
} SPLC_RtcClock_t;

/**
 * @brief Rule Record V1.7 (16 registers = 32 bytes).
 * Byte-exact wire layout for Active (0x0100) and Staging (0x9010) tables.
 */
typedef struct SPLC_PACKED {
    int32_t  threshold_lo;        /* [0..1] Lower threshold (big-endian high word first) */
    int32_t  threshold_hi;        /* [2..3] Upper threshold (big-endian high word first) */
    uint32_t for_ms;               /* [4..5] Dwell / interval / time window in ms */
    int32_t  action_param;        /* [6..7] Action parameter value */
    uint16_t trigger_tag;         /* [8]    Tag index (0..127) */
    uint16_t action_tag;          /* [9]    Tag index (0..127) */
    uint16_t guard_tag;           /* [10]   bit 0..14: tag index; bit 15: NEGATE flag */
    uint8_t  enabled;             /* [11 High Byte] 1 = enabled, 0 = disabled */
    uint8_t  trigger_type;        /* [11 Low Byte]  SPLC_TriggerType_t */
    uint8_t  compare_op;          /* [12 High Byte] SPLC_CompareOp_t */
    uint8_t  action_type;         /* [12 Low Byte]  SPLC_ActionType_t */
    uint8_t  reserved[6];         /* [13..15] Must be set to 0x00 by sender; receiver ignores */
} SPLC_RuleRecord_v1_7_t;

#pragma pack(pop)

/* Compile-time size assertions */
#if defined(__STDC_VERSION__) && __STDC_VERSION__ >= 201112L
_Static_assert(sizeof(SPLC_DeviceDescriptor_t) == 20, "SPLC_DeviceDescriptor_t must be 20 bytes (10 registers)");
_Static_assert(sizeof(SPLC_DeviceResourceInfo_t) == 20, "SPLC_DeviceResourceInfo_t must be 20 bytes (10 registers)");
_Static_assert(sizeof(SPLC_DeviceHealth_t) == 20, "SPLC_DeviceHealth_t must be 20 bytes (10 registers)");
_Static_assert(sizeof(SPLC_RtcClock_t) == 8, "SPLC_RtcClock_t must be 8 bytes (4 registers)");
_Static_assert(sizeof(SPLC_RuleRecord_v1_7_t) == 32, "SPLC_RuleRecord_v1_7_t must be 32 bytes (16 registers)");
#endif

/* ========================================================================= */
/* 6. CONVENIENCE MACROS & HELPERS                                           */
/* ========================================================================= */

#define SPLC_GUARD_IS_NEGATED(g)    (((g) & SPLC_GUARD_NEGATE_MASK) != 0)
#define SPLC_GUARD_GET_TAG(g)       ((uint16_t)((g) & SPLC_GUARD_TAG_MASK))
#define SPLC_GUARD_HAS_GUARD(g)     (SPLC_GUARD_GET_TAG(g) != SPLC_GUARD_TAG_NONE)
#define SPLC_GUARD_MAKE(tag, neg)   ((uint16_t)(((tag) & SPLC_GUARD_TAG_MASK) | ((neg) ? SPLC_GUARD_NEGATE_MASK : 0)))

static inline uint16_t splc_pack_bytes(uint8_t high, uint8_t low) {
    return (uint16_t)(((uint16_t)high << 8) | (uint16_t)low);
}

static inline void splc_unpack_bytes(uint16_t reg, uint8_t *high, uint8_t *low) {
    if (high) *high = (uint8_t)(reg >> 8);
    if (low)  *low  = (uint8_t)(reg & 0xFF);
}

#ifdef __cplusplus
}
#endif

#endif /* SIMPLEPLC_PROTOCOL_V1_7_H */
