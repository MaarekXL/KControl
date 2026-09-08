Import("env")

# Espressif's unified Xtensa toolchain needs the selected core profile during
# the final link as well as compilation so it picks the ESP32-S3 multilibs.
env.Append(LINKFLAGS=["-mdynconfig=xtensa_esp32s3.so"])
