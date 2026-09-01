#!/bin/sh
# Universal Pre-Boot Micro-Core Fast Init Script (Memory-Only 1.5s Boot)
export PATH=/bin:/sbin:/usr/bin:/usr/sbin

echo "[PRE-BOOT] Initializing Hardware Subsystems..."
mount -t proc proc /proc
mount -t sysfs sys /sys
mount -t devtmpfs dev /dev
mount -t tmpfs tmp /run

# 1. Start Wired Ethernet Network (DHCP Auto-Negotiate)
echo "[PRE-BOOT] Probing Wired Network (eth0)..."
ip link set eth0 up 2>/dev/null
udhcpc -i eth0 -n -t 2 -q 2>/dev/null &

# 2. Start Wireless (Wi-Fi) Network using Synced Credentials
if [ -f /boot/pclock/wifi_config.json ]; then
    SSID=$(grep -o '"ssid": *"[^"]*"' /boot/pclock/wifi_config.json | cut -d'"' -f4)
    PSK=$(grep -o '"psk": *"[^"]*"' /boot/pclock/wifi_config.json | cut -d'"' -f4)
    
    if [ -n "$SSID" ]; then
        echo "[PRE-BOOT] Connecting to Wi-Fi SSID: $SSID..."
        cat <<EOF > /run/wpa.conf
network={
    ssid="$SSID"
    psk="$PSK"
}
EOF
        ip link set wlan0 up 2>/dev/null
        wpa_supplicant -B -i wlan0 -c /run/wpa.conf 2>/dev/null
        udhcpc -i wlan0 -n -t 4 -q 2>/dev/null &
    fi
fi

# 3. Launch Pre-Boot Security Guard UI & Relay Client
exec /usr/bin/preboot_guard
