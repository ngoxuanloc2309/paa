#!/usr/bin/env python3
"""
SELEC MFM383A-C Modbus RTU Slave Simulator
Phục vụ kiểm thử đọc dữ liệu công tơ điện đa năng 3 pha theo Datasheet OP INST MFM383A SERIES OP623-V01.
"""

import sys
import time
import struct
import random
import argparse
import serial

if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    except Exception:
        pass

def crc16_modbus(data: bytes) -> int:
    crc = 0xFFFF
    for byte in data:
        crc ^= byte
        for _ in range(8):
            if crc & 0x0001:
                crc = (crc >> 1) ^ 0xA001
            else:
                crc >>= 1
    return crc

def float_to_registers(val: float) -> tuple[int, int]:
    """Chuyển đổi số thực IEEE-754 sang 2 thanh ghi 16-bit Big-Endian (ABCD)"""
    packed = struct.pack(">f", val)
    reg0 = (packed[0] << 8) | packed[1]
    reg1 = (packed[2] << 8) | packed[3]
    return reg0, reg1

class SelecMfm383aSimulator:
    def __init__(self, slave_id: int = 1, jitter: bool = True):
        self.slave_id = slave_id
        self.jitter = jitter

        # Thông số gốc cơ sở (Nominal 230V L-N, 398V L-L, 5A, 50Hz)
        self.base_v = 230.0
        self.base_i = 5.2
        self.base_freq = 50.0
        self.base_pf = 0.99
        self.total_kwh = 12345.60
        self.last_update = time.time()

    def get_registers(self) -> list[int]:
        now = time.time()
        dt = now - self.last_update
        self.last_update = now

        # Thêm dao động nhẹ thực tế nếu jitter bật (+/- 0.3% điện áp, +/- 1% dòng điện)
        v_noise = (random.uniform(-0.5, 0.5)) if self.jitter else 0.0
        i_noise = (random.uniform(-0.05, 0.05)) if self.jitter else 0.0

        v1 = self.base_v - 0.2 + v_noise
        v2 = self.base_v + 0.3 + v_noise * 0.8
        v3 = self.base_v - 0.1 + v_noise * 1.1
        v_avg = (v1 + v2 + v3) / 3.0

        v12 = v1 * 1.73205
        v23 = v2 * 1.73205
        v31 = v3 * 1.73205
        v_ll_avg = (v12 + v23 + v31) / 3.0

        i1 = self.base_i - 0.05 + i_noise
        i2 = self.base_i + 0.08 + i_noise * 0.9
        i3 = self.base_i - 0.02 + i_noise * 1.2
        i_avg = (i1 + i2 + i3) / 3.0

        pf1 = max(0.8, min(1.0, self.base_pf + (random.uniform(-0.005, 0.005) if self.jitter else 0.0)))
        pf2 = max(0.8, min(1.0, self.base_pf + (random.uniform(-0.005, 0.005) if self.jitter else 0.0)))
        pf3 = max(0.8, min(1.0, self.base_pf + (random.uniform(-0.005, 0.005) if self.jitter else 0.0)))
        pf_avg = (pf1 + pf2 + pf3) / 3.0

        # Công suất P (kW) = (V * I * PF) / 1000 cho từng pha
        kw1 = (v1 * i1 * pf1) / 1000.0
        kw2 = (v2 * i2 * pf2) / 1000.0
        kw3 = (v3 * i3 * pf3) / 1000.0
        total_kw = kw1 + kw2 + kw3

        # Công suất toàn phần S (kVA)
        kva1 = (v1 * i1) / 1000.0
        kva2 = (v2 * i2) / 1000.0
        kva3 = (v3 * i3) / 1000.0
        total_kva = kva1 + kva2 + kva3

        # Công suất phản kháng Q (kVAr)
        q1 = (kva1**2 - kw1**2)**0.5 if kva1 > kw1 else 0.0
        q2 = (kva2**2 - kw2**2)**0.5 if kva2 > kw2 else 0.0
        q3 = (kva3**2 - kw3**2)**0.5 if kva3 > kw3 else 0.0
        total_kvar = q1 + q2 + q3

        # Tích lũy điện năng tác dụng (kWh += P * dt / 3600)
        self.total_kwh += (total_kw * (dt / 3600.0))
        total_kvah = self.total_kwh * 1.01
        total_kvarh = self.total_kwh * 0.1

        freq = self.base_freq + (random.uniform(-0.02, 0.02) if self.jitter else 0.0)

        # Mảng 64 thanh ghi (32 thông số Float 2 thanh ghi theo đúng Datasheet trang 5)
        params = [
            (0x00, v1),
            (0x02, v2),
            (0x04, v3),
            (0x06, v_avg),
            (0x08, v12),
            (0x0A, v23),
            (0x0C, v31),
            (0x0E, v_ll_avg),
            (0x10, i1),
            (0x12, i2),
            (0x14, i3),
            (0x16, i_avg),
            (0x18, kw1),
            (0x1A, kw2),
            (0x1C, kw3),
            (0x1E, total_kw),
            (0x20, kva1),
            (0x22, kva2),
            (0x24, kva3),
            (0x26, q1),
            (0x28, q2),
            (0x2A, q3),
            (0x2C, total_kva),
            (0x2E, total_kvar),
            (0x30, pf1),
            (0x32, pf2),
            (0x34, pf3),
            (0x36, pf_avg),
            (0x38, freq),
            (0x3A, self.total_kwh),
            (0x3C, total_kvah),
            (0x3E, total_kvarh),
        ]

        regs = [0] * 64
        for offset, val in params:
            r0, r1 = float_to_registers(val)
            regs[offset] = r0
            regs[offset + 1] = r1

        return regs

    def handle_frame(self, frame: bytes) -> bytes | None:
        if len(frame) < 4:
            return None

        # Kiểm tra CRC
        expected_crc = crc16_modbus(frame[:-2])
        actual_crc = frame[-2] | (frame[-1] << 8)
        if expected_crc != actual_crc:
            print(f"[WARN] CRC Mismatch! Rx: {actual_crc:04X}, Calc: {expected_crc:04X}")
            return None

        slave = frame[0]
        func = frame[1]

        # Kiểm tra Slave ID
        if slave != self.slave_id and slave != 0:
            return None

        # SELEC MFM383A hỗ trợ FC04 (Read Input Registers) và FC03 (Read Holding Registers)
        if func not in (0x03, 0x04):
            # Exception 0x01: Illegal Function
            resp = bytes([slave, func | 0x80, 0x01])
            c = crc16_modbus(resp)
            return resp + bytes([c & 0xFF, (c >> 8) & 0xFF])

        start_addr = (frame[2] << 8) | frame[3]
        reg_count = (frame[4] << 8) | frame[5]

        # Chuẩn Modbus cho phép đọc tối đa 125 thanh ghi
        if reg_count < 1 or reg_count > 125:
            resp = bytes([slave, func | 0x80, 0x03]) # Illegal Data Value
            c = crc16_modbus(resp)
            return resp + bytes([c & 0xFF, (c >> 8) & 0xFF])

        all_regs = self.get_registers()

        # Kiểm tra dải địa chỉ
        if start_addr + reg_count > len(all_regs):
            # Nếu vượt quá 64, trả về các thanh ghi 0 mở rộng
            ext_len = start_addr + reg_count
            all_regs.extend([0] * (ext_len - len(all_regs)))

        selected_regs = all_regs[start_addr : start_addr + reg_count]
        byte_count = len(selected_regs) * 2

        resp_payload = bytearray([slave, func, byte_count])
        for r in selected_regs:
            resp_payload.append((r >> 8) & 0xFF)
            resp_payload.append(r & 0xFF)

        c = crc16_modbus(resp_payload)
        resp_payload.append(c & 0xFF)
        resp_payload.append((c >> 8) & 0xFF)

        # Log trực quan thông số đọc được
        # Giải mã xem có thông số V_avg, I_avg, Total_kW, Total_kWh không
        log_info = []
        if start_addr <= 6 and (start_addr + reg_count) >= 8:
            v_bits = (all_regs[6] << 16) | all_regs[7]
            v = struct.unpack(">f", struct.pack(">I", v_bits))[0]
            log_info.append(f"V_avg={v:.1f}V")

        if start_addr <= 22 and (start_addr + reg_count) >= 24:
            i_bits = (all_regs[22] << 16) | all_regs[23]
            i_val = struct.unpack(">f", struct.pack(">I", i_bits))[0]
            log_info.append(f"I_avg={i_val:.2f}A")

        if start_addr <= 30 and (start_addr + reg_count) >= 32:
            p_bits = (all_regs[30] << 16) | all_regs[31]
            p = struct.unpack(">f", struct.pack(">I", p_bits))[0]
            log_info.append(f"P={p:.2f}kW")

        if start_addr <= 58 and (start_addr + reg_count) >= 60:
            e_bits = (all_regs[58] << 16) | all_regs[59]
            e = struct.unpack(">f", struct.pack(">I", e_bits))[0]
            log_info.append(f"Energy={e:.2f}kWh")

        summary = " | ".join(log_info) if log_info else f"{reg_count} regs"
        print(f"[RX] FC{func:02X} Start={start_addr} Count={reg_count} ➔ [TX] {byte_count} bytes payload ({summary})", flush=True)

        return bytes(resp_payload)

def run_server(port: str, baud: int, slave_id: int):
    print("=" * 68)
    print(f"   SELEC MFM383A-C 3-Phase Multi-Function Meter Simulator")
    print("=" * 68)
    print(f" Port:         {port}")
    print(f" Baudrate:     {baud} (8-N-1)")
    print(f" Slave ID:     {slave_id}")
    print(f" Function:     FC04 (Read Input Regs 30000) & FC03")
    print(f" Data Format:  IEEE-754 Single-Precision Float 32-bit (Big-Endian ABCD)")
    print(f" Range:        0..63 (64 registers = 32 electrical parameters)")
    print("-" * 68)
    print(f" Khởi động cổng {port}... Nhấn Ctrl+C để dừng.")
    print("=" * 68)

    sim = SelecMfm383aSimulator(slave_id=slave_id, jitter=True)

    try:
        ser = serial.Serial(
            port=port,
            baudrate=baud,
            bytesize=8,
            parity=serial.PARITY_NONE,
            stopbits=serial.STOPBITS_ONE,
            timeout=0.05
        )
    except Exception as ex:
        print(f"[ERROR] Không thể mở cổng {port}: {ex}")
        sys.exit(1)

    print(f"[STATUS] Cổng {port} đã mở thành công! Đang lắng nghe yêu cầu Modbus RTU...", flush=True)

    buffer = bytearray()
    last_rx_time = time.time()

    try:
        while True:
            waiting = ser.in_waiting
            if waiting > 0:
                chunk = ser.read(waiting)
                buffer.extend(chunk)
                last_rx_time = time.time()
            else:
                # Modbus RTU framing: Sau khoảng nghỉ 3.5 ký tự (~3.5ms ở 9600), coi như hết khung
                if len(buffer) >= 8 and (time.time() - last_rx_time) > 0.015:
                    resp = sim.handle_frame(bytes(buffer))
                    if resp:
                        ser.write(resp)
                        ser.flush()
                    buffer.clear()
                time.sleep(0.005)

    except KeyboardInterrupt:
        print("\n[INFO] Đã dừng giả lập.")
    finally:
        ser.close()

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="SELEC MFM383A-C Modbus RTU Simulator")
    parser.add_argument("--port", "-p", default="COM9", help="Cổng COM nối tiếp (Mặc định: COM9)")
    parser.add_argument("--baud", "-b", type=int, default=9600, help="Baudrate (Mặc định: 9600)")
    parser.add_argument("--slave-id", "-s", type=int, default=1, help="Địa chỉ Modbus Slave ID (Mặc định: 1)")
    args = parser.parse_args()

    run_server(args.port, args.baud, args.slave_id)
