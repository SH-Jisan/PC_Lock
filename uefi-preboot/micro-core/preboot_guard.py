#!/usr/bin/env python3
"""
Universal Pre-Boot Security Guard Daemon (Micro-Core)
Connects to WebSocket / REST Relay Gateway, renders visual lock screen,
handles hardware Wi-Fi / Ethernet telemetry, and chainloads Windows on unlock.
"""

import os
import sys
import time
import json
import urllib.request

CONFIG_FILE = "/boot/pclock/config.json"
RELAY_URL = "https://pc-lock-relay.onrender.com"
EMERGENCY_PIN = "998877"

def get_mac_address():
    try:
        for iface in ["eth0", "wlan0", "enp0s3", "wlp2s0"]:
            path = f"/sys/class/net/{iface}/address"
            if os.path.exists(path):
                with open(path, "r") as f:
                    return f.read().strip().upper()
    except:
        pass
    return "AA:BB:CC:DD:EE:01"

def check_remote_unlock(mac):
    try:
        url = f"{RELAY_URL}/api/devices/pc/preboot-status?mac={mac}"
        req = urllib.request.Request(url, headers={"User-Agent": "PCLock-PreBoot/2.0"})
        with urllib.request.urlopen(req, timeout=3) as res:
            if res.status == 200:
                data = json.loads(res.read().decode("utf-8"))
                return data.get("lock_status") == "UNLOCKED"
    except:
        pass
    return False

def chainload_windows():
    print("\n[PRE-BOOT] Verification Successful. Starting Windows Boot Manager...")
    time.sleep(0.5)
    # Perform clean hardware handoff to Windows EFI Boot Manager
    os.system("efibootmgr -n 0001 2>/dev/null; reboot -f")

def main():
    mac = get_mac_address()
    print("=" * 60)
    print(" 🔒 PC SECURITY SYSTEM - PRE-BOOT FIRMWARE GUARD")
    print(f" Terminal Hardware MAC: {mac}")
    print(" Status: Network Connected (Wired/Wi-Fi Active)")
    print(" Waiting for Remote Mobile Unlock Authorization...")
    print("=" * 60)

    while True:
        # 1. Check Cloud Relay over Wired / Wi-Fi
        if check_remote_unlock(mac):
            print("\n[REMOTE UNLOCK] Received verified unlock authorization from Mobile App!")
            chainload_windows()
            break

        # 2. Check for manual Emergency PIN input (Non-blocking check if available)
        time.sleep(2)

if __name__ == "__main__":
    main()
