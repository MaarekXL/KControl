import sys
import time

import serial


def main() -> int:
    sys.path.insert(0, ".pio-core/packages/tool-esptoolpy")
    import esptool

    device = esptool.get_default_connected_device(
        ["COM5"], "COM5", 7, 115200, "esp32s3", False, "default-reset"
    )
    if device is None:
        print("Unable to connect to COM5", file=sys.stderr)
        return 1

    device._port.reset_input_buffer()
    device.watchdog_reset()
    try:
        device._port.close()
    except Exception:
        pass

    deadline = time.time() + 20
    captured = bytearray()
    while time.time() < deadline and not captured:
        port = None
        try:
            port = serial.Serial()
            port.port = "COM5"
            port.baudrate = 115200
            port.timeout = 0.25
            port.dtr = False
            port.rts = False
            port.open()
            connected_until = min(deadline, time.time() + 4)
            while time.time() < connected_until:
                try:
                    captured.extend(port.read(4096))
                except serial.SerialException:
                    break
        except (OSError, serial.SerialException):
            time.sleep(0.2)
        finally:
            if port is not None:
                try:
                    port.close()
                except Exception:
                    pass
        if not captured:
            time.sleep(0.2)

    sys.stdout.buffer.write(captured)
    return 0 if captured else 2


if __name__ == "__main__":
    raise SystemExit(main())
