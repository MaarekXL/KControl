# Keryx Display for Waveshare ESP32-S3-LCD-4.3

Wi-Fi, display-only companion for Keryx Manager. This target is the original
Waveshare 800x480 `ESP32-S3-LCD-4.3` without touch support.

## First boot

1. The screen creates `KERYX-DISPLAY-XXXXXX` if it has no usable Wi-Fi profile.
2. Connect a phone or PC to that network with password `keryxdisplay`.
3. Open `http://192.168.4.1` and enter the home Wi-Fi details.
4. The screen restarts and waits for Keryx Manager telemetry.

The screen does not start or stop mining. It receives display-only local UDP
telemetry, so it keeps rendering even when a Windows Remote Desktop session is
closed.

## Build

The project uses PlatformIO with the pioarduino ESP32 platform (Arduino 3.3.11),
LVGL 9.5.0, and the Waveshare/Espressif display libraries included under `lib`.

```powershell
pio run
```

Do not upload until the exact board model and the generated binary have been
verified. Keep the factory flash backup before any first upload.

