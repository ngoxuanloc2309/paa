# SPLC-SPEC-SECURITY-001: SynaptiX Secure Transport & Mutual Ecosystem Authentication Protocol (Wire Contract ICD)

**Tài liệu:** `SPLC-SPEC-SECURITY-001`  
**Phiên bản:** `1.0`  
**Trạng thái:** `OFFICIAL SPECIFICATION`  
**Tiêu chuẩn liên quan:** `IEC 62443-4-2` (An ninh mạng Hệ thống Tự động hóa Công nghiệp), `RFC 8439` (ChaCha20-Poly1305 AEAD), `NIST SP 800-56A Rev 3` (ECDH P-256), `RFC 5869` (HKDF-SHA256), `SPLC-AF-003`  
**Đối tượng:** Kỹ sư Firmware STM32H5 (C/C++), Kỹ sư Phần mềm SynaptiX Studio (.NET 8 C#)  
**Tệp Header C quy chuẩn:** `docs/firmware/splc_secure_wire.h`  

---

## 1. Mục Đích & Bối Cảnh Bảo Vệ (Threat Modeling & Objectives)

Hệ sinh thái tự động hóa **SynaptiX** bao gồm phần mềm lập trình **SynaptiX IDE (SimplePLC Studio)** và dòng điều khiển biên công nghiệp chạy trên nền vi điều khiển **ARM Cortex-M33 (STM32H5)**.

Nhằm bảo vệ tài sản trí tuệ (IP) và ngăn ngừa việc sao chép giải pháp, tiêu chuẩn **SPLC-SPEC-SECURITY-001** xác lập một cơ chế truyền thông an ninh khép kín đáp ứng **4 mục tiêu cốt lõi**:

1. **Che giấu tuyệt đối giao thức (Dark Tunnel / Anti-Sniffing):**
   - Toàn bộ dữ liệu truyền trên bus UART/RS-485 (bao gồm mã lệnh nạp rule 32-byte, địa chỉ thanh ghi, telemetry giám sát I/O) được mã hóa hoàn toàn. Người dùng bên ngoài cắm máy đo logic analyzer hoặc phần mềm bắt gói tin cổng COM sẽ chỉ thấy các chuỗi byte ngẫu nhiên vô nghĩa (High Entropy), không thể dò ra cấu trúc hay nguyên lý vận hành.
2. **Khóa chống phần cứng nhái (Anti-Hardware Cloning):**
   - Chỉ có phần cứng chính hãng sản xuất tại nhà máy của SynaptiX mới có thể kết nối với SynaptiX IDE. Thiết bị nhái (sao chép sơ đồ nguyên lý hoặc nạp firmware vào chip khác) sẽ bị phát hiện và từ chối kích hoạt ngay lập tức nhờ xác thực danh tính **96-bit Unique ID (UID)** của chip gắn liền với chữ ký xuất xưởng.
3. **Khóa chống ứng dụng lạ (Anti-Unauthorized App):**
   - Mạch điều khiển SynaptiX MCU từ chối tiếp nhận lệnh từ bất kỳ phần mềm bên thứ 3 nào (như script Python, tool Modbus công cộng). Khi gặp dữ liệu không qua được bắt tay xác thực, MCU chuyển sang **Chế độ Câm (Silent Drop)**.
4. **Chống tấn công phát lại & Toàn vẹn dữ liệu (Anti-Replay & Message Integrity):**
   - Sử dụng bộ đếm gói tin động (Monotonic Packet Counter) cùng mã chứng thực **Poly1305 Tag (16 bytes)**. Kẻ gian không thể ghi lại gói tin cũ để gửi lại, đồng thời bất kỳ hiện tượng nhiễu điện công nghiệp làm sai 1 bit trên đường truyền RS-485 đều bị loại bỏ ngay tại lớp giải mã.

---

## 2. Ngăn Xếp Mật Mã Lõi (Cryptographic Stack)

Toàn bộ hệ thống vận hành theo kiến trúc **Mã hóa phần mềm tối ưu cho hệ nhúng** kết hợp tận dụng các khối tăng tốc phần cứng không bị giới hạn xuất khẩu của STM32H5:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        SYNAPTIX CRYPTO STACK                           │
├────────────────────────┬───────────────────────────────────────────────┤
│ Giai đoạn Bắt tay      │ • Trao đổi khóa: ECDH NIST P-256              │
│ (Handshake Phase)      │ • Dẫn xuất khóa: HKDF-SHA256 (RFC 5869)       │
│                        │ • Xác thực danh tính: Token HMAC-SHA256       │
├────────────────────────┼───────────────────────────────────────────────┤
│ Giai đoạn Vận chuyển   │ • Mã hóa đối xứng: ChaCha20 (RFC 8439)        │
│ (Transport Tunnel)     │ • Xác thực toàn vẹn: Poly1305 MAC (16 bytes)  │
│                        │ • Vector khởi tạo: 12-byte Monotonic Nonce    │
├────────────────────────┼───────────────────────────────────────────────┤
│ Tối ưu phần cứng       │ • STM32H5: Tăng tốc PKA (P-256) & HASH (SHA)  │
│                        │ • Studio: Native .NET 8 Cryptography APIs     │
└────────────────────────┴───────────────────────────────────────────────┘
```

---

## 3. Quy Trình Bắt Tay Xác Thực 2 Chiều (2-Way Handshake Protocol)

Khi SynaptiX IDE mở cổng COM hoặc bấm "Kết nối", phiên làm việc bắt buộc phải trải qua 2 bước bắt tay trước khi được phép gửi bất kỳ lệnh logic nào:

```text
SynaptiX IDE (Client)                                          STM32H5 Controller (Server)
        │                                                                   │
        │ ─── 1. SPLC_MSG_HANDSHAKE_REQ (Header 0x53 0x48, 88 Bytes) ────>  │
        │      • magic_sof: 0x5348 ('S', 'H')                               │
        │      • app_ephemeral_pubkey: 64 bytes (X, Y trên NIST P-256)      │
        │      • app_random_salt: 16 bytes (TRNG sinh từ Client)            │
        │      • app_vendor_token: 8 bytes (HMAC chứng minh App chính hãng) │
        │                                                                   │
        │                                                     [MCU XỬ LÝ & XÁC MINH]
        │                                                     1. Xác minh app_vendor_token.
        │                                                        Nếu SAI ➔ SILENT DROP (Im lặng).
        │                                                     2. Sinh cặp khóa tạm thời (MCU Ephemeral).
        │                                                     3. Tính Shared Secret qua khối PKA.
        │                                                     4. Dẫn xuất SessionKey = HKDF(SharedSecret).
        │                                                     5. Ký device_proof dựa trên 96-bit UID.
        │                                                                   │
        │ <── 2. SPLC_MSG_HANDSHAKE_RESP (Header 0x53 0x53, 96 Bytes) ────  │
        │      • magic_sof: 0x5353 ('S', 'S')                               │
        │      • mcu_ephemeral_pubkey: 64 bytes                             │
        │      • mcu_random_salt: 16 bytes                                  │
        │      • device_proof: 16 bytes (HMAC(UID, RootSecret))             │
        │                                                                   │
  [APP XỬ LÝ & XÁC MINH]                                                    │
  1. Xác minh device_proof có khớp chữ ký xưởng.                            │
     Nếu SAI ➔ Ngắt kết nối, báo "Thiết bị không ủy quyền".                │
  2. Tính Shared Secret qua ECDiffieHellman.                                │
  3. Dẫn xuất SessionKey = HKDF(SharedSecret).                              │
        │                                                                   │
        ▼                                                                   ▼
  [ ĐÈN XANH KẾT NỐI ]                                              [ ACTIVE SECURE TUNNEL ]
```

---

## 4. Định Dạng Khung Bắt Tay (Handshake Frame Formats)

### 4.1. Gói Tin 1: Yêu Cầu Bắt Tay từ App (`SPLC_HandshakeReq_t`)
Kích thước cố định: **88 bytes**.

| Byte Offset | Tên Trường | Kiểu Dữ Liệu | Giá Trị / Ý Nghĩa |
| :---: | :--- | :---: | :--- |
| `0..1` | `magic_sof` | UINT16 | `0x4853` (Little-Endian của `'S'`, `'H'`: `0x53`, `0x48`). |
| `2` | `protocol_version`| UINT8 | `0x01` (Phiên bản giao thức bảo mật V1). |
| `3` | `reserved` | UINT8 | `0x00` (Dự phòng căn lề byte). |
| `4..67` | `app_pubkey_xy` | UINT8[64] | Tọa độ $X$ (32 bytes) và $Y$ (32 bytes) của Ephemeral Public Key trên đường cong NIST P-256. |
| `68..83`| `app_salt` | UINT8[16] | Chuỗi số ngẫu nhiên 128-bit do App sinh ra chống phát lại. |
| `84..91`| `vendor_token` | UINT8[8] | 64-bit HMAC tính từ `app_salt` bằng SynaptiX Vendor Secret, chứng minh App chính hãng. |

### 4.2. Gói Tin 2: Phản Hồi Bắt Tay từ MCU (`SPLC_HandshakeResp_t`)
Kích thước cố định: **96 bytes**.

| Byte Offset | Tên Trường | Kiểu Dữ Liệu | Giá Trị / Ý Nghĩa |
| :---: | :--- | :---: | :--- |
| `0..1` | `magic_sof` | UINT16 | `0x5353` (Little-Endian của `'S'`, `'S'`: `0x53`, `0x53`). |
| `2` | `status_code` | UINT8 | `0x00` = Success, `0x01` = Invalid Token, `0x02` = Crypto Busy. |
| `3` | `mcu_hw_revision` | UINT8 | Phiên bản bo mạch phần cứng (ví dụ: `0x10` = Rev 1.0). |
| `4..67` | `mcu_pubkey_xy` | UINT8[64] | Tọa độ $X$ (32 bytes) và $Y$ (32 bytes) của MCU Ephemeral Public Key. |
| `68..83`| `mcu_salt` | UINT8[16] | Chuỗi số ngẫu nhiên do bộ TRNG của STM32H5 sinh ra. |
| `84..95`| `device_proof` | UINT8[12] | Bằng chứng xác thực danh tính chip gắn liền với 96-bit Unique ID (UID). |

---

## 5. Cấu Trúc Khung Dữ Liệu Mã Hóa (Secure Data Frame Wire Layout)

Sau khi giai đoạn bắt tay hoàn tất, mọi gói tin truyền nhận tiếp theo (lệnh nạp 32-byte rules, đọc ghi I/O, telemetry) đều bắt buộc phải đóng trong **Khung Vận Chuyển An Ninh (Secure Frame)**:

```
Byte Offset:   0    1      2      3      4               15   16               16+N-1  16+N           16+N+15
             ┌──────────┬──────────────┬───────────────────┬────────────────────────┬──────────────────────┐
Field:       │ SOF (2B) │  Length (2B) │    Nonce (12B)    │  Ciphertext (N Bytes)  │  Poly1305 Tag (16B)  │
Value:       │  'S' 'X' │   0x00..0xFF │  SessionID+Count  │   ChaCha20 Encrypted   │   Message Auth Tag   │
             └──────────┴──────────────┴───────────────────┴────────────────────────┴──────────────────────┘
```

### 5.1. Bảng Đặc Tả Chi Tiết Trường Byte

| Byte Offset | Tên Trường | Kích Thước | Kiểu | Mô Tả Kỹ Thuật |
| :---: | :--- | :---: | :---: | :--- |
| `0..1` | `magic_sof` | 2 Bytes | UINT16 | `0x5853` (Little-Endian của `'S'`, `'X'`: `0x53`, `0x58`). Ký tự đồng bộ khung UART. |
| `2..3` | `payload_len` | 2 Bytes | UINT16 | Độ dài chính xác của vùng `Ciphertext` ($1 \le N \le 1024$ bytes). |
| `4..7` | `session_id` | 4 Bytes | UINT32 | Mã định danh phiên ngẫu nhiên (sinh ra từ pha Handshake). |
| `8..15`| `packet_counter`| 8 Bytes | UINT64 | Bộ đếm gói tin tăng dần đơn điệu ($0, 1, 2, ...$). Mỗi chiều truyền duy trì bộ đếm riêng biệt. |
| `16..(16+N-1)` | `ciphertext` | $N$ Bytes | BYTE[N] | Nội dung gốc (Plaintext) đã được mã hóa bằng thuật toán **ChaCha20** với khóa `SessionKey`. |
| `(16+N)..(31+N)`| `auth_tag` | 16 Bytes | BYTE[16] | Mã xác thực thông điệp 128-bit do thuật toán **Poly1305** tính trên Header + Ciphertext. |

### 5.2. Nguyên Tắc An Ninh Của Vector Khởi Tạo (Nonce Security Invariant)
* Khung truyền Nonce chuẩn RFC 8439 có độ dài **12 bytes** = `session_id (4B)` + `packet_counter (8B)`.
* Với mỗi gói tin gửi đi, `packet_counter` bắt buộc phải tăng thêm 1 đơn vị.
* **Quy tắc tuyệt đối:** Không bao giờ tái sử dụng cùng một bộ (Key, Nonce) cho hai gói tin khác nhau. Nếu `packet_counter` đạt giá trị cực đại ($2^{64}-1$), kết nối bắt buộc phải tự động đóng và tái bắt tay (Re-keying).

---

## 6. Cơ Chế Chống Hàng Giả & Khóa Hệ Sinh Thái (Anti-Clone Mechanism)

### 6.1. Quy Trình Cấp Phép Xuất Xưởng (Factory Provisioning)
1. Trong quá trình nạp nạp mạch tại xưởng sản xuất SynaptiX:
   - Tool nạp đọc mã số định danh phần cứng **96-bit Unique ID (UID)** từ thanh ghi hệ thống của STM32H5 (tại địa chỉ `0x08FFF800` / `UID_BASE`).
   - Dùng khóa bí mật chủ **SynaptiX Master Key** băm với UID qua thuật toán HMAC-SHA256 để tạo ra **Device Factory Token**.
   - Ghi Device Factory Token vào vùng Flash bảo mật (OBKeys / Flash Secure Area).
   - Kích hoạt **Read-Out Protection Level 2 (RDP2)** để khóa vĩnh viễn cổng JTAG/SWD, ngăn chặn việc dump firmware và trích xuất token.
2. Khi kết nối với SynaptiX IDE:
   - MCU chứng minh tính chính hãng bằng cách gửi `device_proof`.
   - App SynaptiX IDE xác minh tính hợp lệ của Token. Mọi bo mạch tự thiết kế ngoài luồng sẽ không thể vượt qua bước kiểm tra này.

---

## 7. Xử Lý Sự Cố, An Toàn Thất Bại & Phòng Vệ (Fail-Safe Defense Rules)

1. **Chế độ Câm (Silent Drop Policy):**
   - Nếu MCU nhận dữ liệu sai Header, sai kích thước hoặc sai Token bắt tay: MCU **tuyệt đối không phản hồi** mã lỗi hay bất kỳ byte nào. Kẻ gian dùng kỹ thuật fuzzing hoặc scan cổng COM sẽ không thu được bất kỳ manh mối phản hồi nào.
2. **Khóa phạt trễ (Penalty Lockout):**
   - Nếu nhận 3 yêu cầu Handshake sai liên tiếp trong vòng 10 giây: MCU tự động vô hiệu hóa cổng UART trong 5 giây để triệt tiêu các cuộc tấn công vét cạn (Brute-force).
3. **Tự hủy khóa trong RAM (Key Zeroization):**
   - Khi mất tín hiệu cổng COM quá 500ms hoặc khi người dùng bấm "Ngắt kết nối": Cả App và MCU đều phải gọi hàm xóa bộ nhớ (`memset(session_key, 0, 32)`) để hủy khóa phiên khỏi RAM ngay lập tức.
4. **Hủy gói tin khi Auth Tag không khớp:**
   - Nếu đường truyền bị nhiễu điện từ trường công nghiệp hoặc bị can thiệp làm sai khác 1 bit: Hàm Poly1305 verification sẽ trả về lỗi $\rightarrow$ MCU vứt bỏ gói tin ngay lập tức, không cho phép payload đi vào bộ máy thực thi PLC.

---

## 8. Cấu Trúc File Header C Quy Chuẩn (`splc_secure_wire.h`)

Kỹ sư Firmware sử dụng tệp định nghĩa header sau cho mã nguồn vi điều khiển:

```c
#ifndef SPLC_SECURE_WIRE_H
#define SPLC_SECURE_WIRE_H

#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

#define SPLC_SOF_HANDSHAKE_REQ   0x5348  /* 'S', 'H' */
#define SPLC_SOF_HANDSHAKE_RESP  0x5353  /* 'S', 'S' */
#define SPLC_SOF_SECURE_DATA     0x5853  /* 'S', 'X' */

#define SPLC_MAX_PAYLOAD_SIZE    1024
#define SPLC_AUTH_TAG_SIZE       16
#define SPLC_NONCE_SIZE          12
#define SPLC_PUBKEY_SIZE         64
#define SPLC_SALT_SIZE           16

#pragma pack(push, 1)

/**
 * @brief Gói tin yêu cầu bắt tay từ SynaptiX IDE (88 Bytes)
 */
typedef struct {
    uint16_t magic_sof;          /* 0x5348 ('S', 'H') */
    uint8_t  protocol_version;   /* 0x01 */
    uint8_t  reserved;           /* 0x00 */
    uint8_t  app_pubkey_xy[64];  /* NIST P-256 Public Key (X: 32B, Y: 32B) */
    uint8_t  app_salt[16];       /* Random Salt từ App */
    uint8_t  vendor_token[8];    /* HMAC xác thực App chính hãng */
} SPLC_HandshakeReq_t;

/**
 * @brief Gói tin phản hồi bắt tay từ STM32H5 MCU (96 Bytes)
 */
typedef struct {
    uint16_t magic_sof;          /* 0x5353 ('S', 'S') */
    uint8_t  status_code;        /* 0x00: Thành công, Khác 0: Lỗi */
    uint8_t  mcu_hw_revision;    /* Phiên bản bo mạch */
    uint8_t  mcu_pubkey_xy[64];  /* NIST P-256 Public Key của MCU */
    uint8_t  mcu_salt[16];       /* Random Salt từ TRNG của MCU */
    uint8_t  device_proof[12];   /* Bằng chứng xác thực gắn với 96-bit UID */
} SPLC_HandshakeResp_t;

/**
 * @brief Tiêu đề Khung vận chuyển dữ liệu mã hóa (16 Bytes Header)
 */
typedef struct {
    uint16_t magic_sof;          /* 0x5853 ('S', 'X') */
    uint16_t payload_len;        /* Số byte của ciphertext (1..1024) */
    uint32_t session_id;         /* ID phiên làm việc */
    uint64_t packet_counter;     /* Bộ đếm gói tin tăng dần (chống Replay) */
} SPLC_SecureHeader_t;

#pragma pack(pop)

#ifdef __cplusplus
}
#endif

#endif /* SPLC_SECURE_WIRE_H */
```
