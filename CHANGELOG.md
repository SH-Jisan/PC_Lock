# 📋 PC Lock - Comprehensive Changelog & Architecture Evolution (CHANGELOG.md)

All notable changes, architectural decisions, and feature additions to the **PC Lock (PC Security System)** will be documented in this file.

### 💡 Format & Guiding Principles
For every modification or addition, this log records:
1. **What was changed / added (কী পরিবর্তন করা হয়েছে)**: Technical breakdown of the new feature or patch.
2. **Why it was done (কেন করা হয়েছে)**: The architectural rationale, problem statement, or operational benefit.
3. **Affected Files & Subsystems**: Direct clickable references to modified components.
4. **Impact & Security Implication**: Operational safety, dependencies, and performance considerations.

---

## 🚀 Active Feature Inventory (Current System State - v1.3.0)

| Subsystem | Key Components | Technical Implementation |
| :--- | :--- | :--- |
| **Deep Hardware & Driver Diagnostics** | GPU/GOP Driver, NIC UNDI/SNP, NVMe/SATA Storage, EFI ESP Loader | Zero-dependency Win32 Registry kernel driver (`.sys`) resolution, PCI enumeration, UEFI GOP & UNDI/SNP pre-boot readiness scoring, EFI volume partition detection. |
| **Custom Cyber Lock Engine** | Isolated Desktop, Global Keyboard Hook, TaskMgr Policy, Topmost Cyber UI | Win32 `CreateDesktop` (`PC_LOCK_SECURE_DESKTOP`), `WH_KEYBOARD_LL` low-level hook blocking Win/Alt+Tab/Alt+F4, Registry `DisableTaskMgr=1`, Dark Theme Fullscreen WinForms with QR code and touch keypad. |
| **Zero-Dependency Deployment** | Standalone Single-File `.exe`, Dynamic Discovery, Permanent Install | .NET 8 `--self-contained true -p:PublishSingleFile=true`, dynamic path resolution (`ResolveSecurityAgent`), automatic copy to `C:\Program Files\PCSecuritySystem\` for safe USB pendrive removal. |
| **Safe Diagnostic Harness** | Firmware-Neutral Test Script | `test_lock_engine.bat` with `--test-lock` flag; neutralizes `BootGuardHealer` to allow safe desktop UI & keyboard shield testing without touching UEFI or rebooting. |
| **PC Security Agent & Watchdog** | Windows Background Service, Winlogon Provider, TPM / DPAPI | Administrative watchdog service, auto-restart on termination, native C++ `WinlogonProvider.cpp` credential provider tile, DPAPI/TPM hardware secret store. |
| **Cloud Security Gateway** | WebSocket & REST Relay, Database, Cryptographic Verification | Node.js/TypeScript gateway, Supabase/PostgreSQL schema, Ed25519 digital signature validation, HMAC token authentication, live heartbeat status (`ONLINE`, `LOCKED`, `OFFLINE`). |
| **Mobile Controllers** | PWA Web Controller, Native Android App | Responsive dark cyber web app with camera QR scanner, Android Studio project with BiometricPrompt (fingerprint/face unlock) and real-time remote commands. |
| **Firmware Pre-Boot Interception** | Bare-Metal UEFI `.efi`, Micro-Core Chainloader, ACPI WPBT | Clang-compiled 64-bit EFI binary (`pc_lock_preboot.efi`) intercepting boot chain before `bootmgfw.efi`, ACPI WPBT persistence kit. |
| **Automated CI/CD Pipeline** | GitHub Actions Workflow | `.github/workflows/release.yml` triggers on version tags (`v*`) or manual dispatch, compiles standalone binaries on Windows Server runner, and publishes `.zip` to GitHub Releases. |

---

## 📜 Detailed Change History & Evolution Log

### [v1.3.0] - 2026-09-13: Deep Hardware & Pre-Boot Driver Diagnostics Engine

#### 1. Zero-Dependency Deep Hardware & Kernel Driver Inspection
* **Files**:
  - [`DeployManager/Services/HardwareAuditService.cs`](file:///h:/PC_Lock/DeployManager/Services/HardwareAuditService.cs) [MODIFIED]
  - [`pc-agent/Hardware/HardwareProfile.cs`](file:///h:/PC_Lock/pc-agent/Hardware/HardwareProfile.cs) [MODIFIED]
* **What was added / changed (কী করা হয়েছে)**:
  - পিসির গ্রাফিক্স, স্টোরেজ, নেটওয়ার্ক ড্রাইভার এবং প্রি-বুট ফার্মওয়্যার অ্যাসেট শতভাগ নির্ভুলভাবে ডিটেক্ট করার জন্য জিরো-ডিপেন্ডেন্সি রেজিস্ট্রি স্ক্যানার (`HKLM\SYSTEM\CurrentControlSet\Control\Class\...` এবং `HKLM\SYSTEM\CurrentControlSet\Enum\PCI\...`) এবং Win32 কার্নেল সার্ভিস পাথ রেজোলিউশন তৈরি করা হয়েছে:
    1. **গ্রাফিক্স ও ডিসপ্লে হার্ডওয়্যার (GPU & Graphics Driver)**:
       - সক্রিয় GPU অ্যাডাপ্টারের নাম, ভেন্ডর/প্রোভাইডার, PCI Device ID, ড্রাইভার ভার্সন ও তারিখ ডিটেকশন।
       - উইন্ডোজ রেজিস্ট্রি থেকে এক্সাক্ট কার্নেল ডিসপ্লে ড্রাইভার ফাইল পাথ (`.sys`, যেমন: `C:\Windows\System32\DriverStore\FileRepository\...\igdkmd64.sys` বা `nvlddmkm.sys`) স্বয়ংক্রিয়ভাবে চিহ্নিতকরণ।
       - UEFI Graphics Output Protocol (GOP) রেডিলেস যাচাইকরণ (Native UEFI মোডে GOP সক্রিয় কি না)।
    2. **নেটওয়ার্ক কার্ড ও ড্রাইভ পাথ (NIC & Driver Stack)**:
       - ফিজিক্যাল ও PCI নেটওয়ার্ক ইন্টারফেসের PCI ID, প্রস্তুতকারক, ড্রাইভার ভার্সন এবং রিয়েল কার্নেল ড্রাইভার ফাইল (`.sys`, যেমন: `rt68cx21x64.sys`) রেজোলিউশন।
       - প্রি-বুট নেটওয়ার্ক আনলকের জন্য UEFI UNDI (Universal Network Device Interface) / SNP (Simple Network Protocol) ROM ক্যাপাবিলিটি অ্যাসেসমেন্ট।
    3. **স্টোরেজ কন্ট্রোলার ও ডিস্ক পার্টিশন আর্কিটেকচার**:
       - NVMe বনাম SATA AHCI বাস টাইপ ডিটেকশন এবং সংশ্লিষ্ট মিনিপোর্ট কার্নেল ড্রাইভার (`stornvme.sys` বা `storahci.sys`) সনাক্তকরণ।
       - ডিস্ক পার্টিশন স্টাইল (GPT বনাম MBR) এবং ড্রাইভ মডেল ভেরিফিকেশন।
    4. **EFI সিস্টেম পার্টিশন (ESP) ও প্রি-বুট বুটলোডার অ্যাসেট স্ক্যানার**:
       - Win32 ভলিউম স্ক্যানিং (`\\?\Volume{GUID}\`) এবং মাউন্ট পয়েন্ট ছাড়া EFI পার্টিশন সনাক্তকরণ।
       - মাইক্রোসফটের অফিসিয়াল বুটলোডার (`\EFI\Microsoft\Boot\bootmgfw.efi`), ব্যাকআপ বুটলোডার (`bootmgfw_hidden.efi`), এবং ডিফল্ট রুট লোডার (`\EFI\Boot\bootx64.efi`)-এর সঠিক লোকেশন যাচাই।
       - `FirmwareBootDevice` রেজিস্ট্রি কি থেকে পিসির বুট পাথ উদ্ধার।
    5. **প্রি-বুট অ্যাসেটস ডায়াগনস্টিকস ও স্কোরিং**:
       - প্রি-বুট লক স্ক্রিনের জন্য প্রয়োজনীয় সকল হার্ডওয়্যার কম্পোনেন্ট (GOP Display, Pre-boot NIC, Storage Controller, EFI Boot Partition) মিলিয়ে একটি কম্প্রিহেনসিভ ডায়াগনস্টিক স্কোর (০-১০০%) এবং রিকমেন্ডেশন তৈরি।
  - `pc-agent/Hardware/HardwareProfile.cs`-এ নতুন ফিল্ডগুলো (`GpuName`, `GpuDriverPath`, `UefiGopSupported`, `NetworkDriverPath`, `StorageController`, `StorageDriverPath`, `PartitionStyle`, `EfiPartitionGuid`) যুক্ত করা হয়েছে।
* **Why it was done (কেন করা হয়েছে)**:
  - ক্লায়েন্ট বা ক্যাফে পিসিতে ডিপ্লয় করার সময় আগে জানা যেত না প্রি-বুট লক মোডের জন্য ডিসপ্লে অ্যাডাপ্টারে UEFI GOP ফার্মওয়্যার সাপোর্ট আছে কি না, বা নেটওয়ার্ক কার্ডের UNDI/SNP ROM সক্রিয় আছে কি না। গভীর ড্রাইভার ও EFI স্ক্যানিংয়ের ফলে এখন আগে থেকেই জানা যায় পিসিটিতে প্রি-বুট লক মোড চালু করলে কোনো বুট স্ক্রিন বা নেটওয়ার্ক ক্র্যাশ হবে কি না।

#### 2. Visual Deep Diagnostics GUI & Console Stream in DeployManager
* **Files**:
  - [`DeployManager/MainForm.cs`](file:///h:/PC_Lock/DeployManager/MainForm.cs) [MODIFIED]
* **What was added / changed (কী করা হয়েছে)**:
  - `MainForm.cs`-এর হার্ডওয়্যার প্রোফাইল কার্ড বড় করে (805x205 px) সেখানে নতুন ৪টি স্পষ্ট স্ট্যাটাস সারি যুক্ত করা হয়েছে:
    - 🎮 **GPU / GOP**: ডিসপ্লে কার্ড ও তার কার্নেল ড্রাইভার নাম এবং GOP রেডি স্ট্যাটাস।
    - 🌐 **NIC / Driver**: নেটওয়ার্ক কার্ডের নাম ও তার এক্সাক্ট `.sys` ফাইল।
    - 💾 **Storage / Disk**: স্টোরেজ বাস (NVMe/SATA), কন্ট্রোলার ড্রাইভার এবং GPT/MBR পার্টিশন ফরম্যাট।
    - ⚡ **EFI Preboot**: EFI বুটলোডার পাথ এবং প্রি-বুট হার্ডওয়্যার রেডি স্কোর।
  - লাইভ ডায়াগনস্টিক টার্মিনাল কনসোলে সম্পূর্ণ ড্রাইভ পাথ, PCI ডিভাইস আইডি এবং প্রি-বুট অ্যাসেট চেকলিস্টের বিস্তারিত আউটপুট প্রিন্ট করার ব্যবস্থা করা হয়েছে।
* **Why it was done (কেন করা হয়েছে)**:
  - ব্যবহারকারী বা অ্যাডমিন DeployManager ওপেন করলেই যেন চোখের সামনে সম্পূর্ণ হার্ডওয়্যার, গ্রাফিক্স ড্রাইভার, নেটওয়ার্ক ড্রাইভার ও ফার্মওয়্যার অ্যাসেটের লাইভ অবস্থান দেখতে পান।

---

### [v1.2.0] - 2026-09-06: Pre-Flight Hardware Audit & Compatibility Diagnostics Engine

#### 1. Hardware Audit & System Profiling Engine
* **Files**:
  - [`DeployManager/Services/HardwareAuditService.cs`](file:///F:/PC_Lock/DeployManager/Services/HardwareAuditService.cs) [NEW]
  - [`pc-agent/Hardware/HardwareProfile.cs`](file:///F:/PC_Lock/pc-agent/Hardware/HardwareProfile.cs) [NEW]
* **What was added / changed (কী করা হয়েছে)**:
  - সম্পূর্ণ জিরো-ডিপেন্ডেন্সি C# সার্ভিস `HardwareAuditService.cs` তৈরি করা হয়েছে, যা উইন্ডোজ রেজিস্ট্রি (`HKLM\HARDWARE\DESCRIPTION\System\BIOS`), Win32 `kernel32.dll` API (`GetFirmwareType`, `GlobalMemoryStatusEx`) এবং .NET `System.Net.NetworkInformation` ব্যবহার করে নিমেষের মধ্যে পিসির পূর্ণাঙ্গ হার্ডওয়্যার প্রোফাইল তৈরি করে:
    1. **মাদারবোর্ড স্পেসিফিকেশন**: প্রস্তুতকারক (Manufacturer), মডেল/প্রোডাক্ট নাম, ভার্সন, সিরিয়াল নম্বর এবং সিস্টেম মডেল/SKU।
    2. **BIOS / ফার্মওয়্যার তথ্য**: ভেন্ডর, ভার্সন, রিলিজ ডেট, বুট মোড (UEFI Native বনাম Legacy BIOS) এবং সিকিউর বুট (Secure Boot Active বনাম Disabled) স্টেটাস।
    3. **ইউজার ও হোস্ট আইডেন্টিটি**: বর্তমান লগড-ইন ইউজারনেম, ডোমেইন/ওয়ার্কগ্রুপ, কম্পিউটার নাম, প্রসেসর মডেল, কোর সংখ্যা এবং মোট ফিজিক্যাল RAM।
    4. **নেটওয়ার্ক হার্ডওয়্যার ও আইপি**: ফিজিক্যাল নেটওয়ার্ক অ্যাডাপ্টার (Ethernet/Wi-Fi), চিপসেট বিবরণ, ফিজিক্যাল MAC অ্যাড্রেস, সক্রিয় IPv4 অ্যাড্রেস, সাবনেট মাস্ক, ডিফল্ট গেটওয়ে, DNS সার্ভার এবং ইন্টারনেট/ক্লাউড রিলে কানেক্টিভিটি টেস্ট।
    5. **কম্প্যাটিবিলিটি অ্যাসেসমেন্ট ম্যাট্রিক্স**: আর্কিটেকচার (x64), ফার্মওয়্যার (UEFI/Legacy), সিকিউর বুট এবং নেটওয়ার্ক স্টেটাস মিলিয়ে অটোমেটিক কম্প্যাটিবিলিটি স্কোর ও রিকমেন্ডেড ডিপ্লয়মেন্ট মোড নির্ধারণ।
  - ব্যাকগ্রাউন্ড এজেন্ট (`pc-agent/Hardware/HardwareProfile.cs`)-এ হার্ডওয়্যার প্রোফাইল লোডার যোগ করা হয়েছে যাতে পার্মানেন্ট অডিট ফাইল থেকে বা লাইভ সিস্টেমে টেলিমেট্রি অ্যাক্সেস করা যায়।
* **Why it was done (কেন করা হয়েছে)**:
  - পূর্বে সফটওয়্যারটি যেকোনো পিসিতে ব্লাইন্ডলি (অন্ধভাবে) ডিপ্লয় হতো, যার ফলে মাদারবোর্ড মডেল বা বুট মোড না জেনে অনুপযুক্ত কনফিগারেশন চলার ঝুঁকি থাকত। এখন সফটওয়্যার চালুর সাথে সাথেই সম্পূর্ণ হার্ডওয়্যার অডিট সম্পন্ন হয় এবং পিসির স্পেসিফিকেশন অনুযায়ী উপযুক্ত আর্কিটেকচার (যেমন: সিকিউর বুট অন থাকলে Enterprise Zero-Risk মোড) নিশ্চিত করে নিরাপদে ডিপ্লয় করা যায়।

#### 2. Interactive Hardware Diagnostics Dashboard in DeployManager GUI
* **Files**:
  - [`DeployManager/MainForm.cs`](file:///F:/PC_Lock/DeployManager/MainForm.cs) [MODIFIED]
  - [`DeployManager/Services/DeploymentEngine.cs`](file:///F:/PC_Lock/DeployManager/Services/DeploymentEngine.cs) [MODIFIED]
* **What was added / changed (কী করা হয়েছে)**:
  - `MainForm.cs`-এ একটি আকর্ষণীয় **Hardware Profile & Compatibility Card** যুক্ত করা হয়েছে, যাতে অ্যাপ ওপেন হওয়ামাত্রই মাদারবোর্ড মডেল, BIOS বুট মোড, ইউজারনেম, হোস্টনেম, প্রাইমারি MAC এবং সক্রিয় IP প্রদর্শিত হয়।
  - একটি ভিজ্যুয়াল কম্প্যাটিবিলিটি ব্যাজ (🟢 `100% COMPATIBLE (Enterprise Ready)`) এবং রিয়েল-টাইম রি-স্ক্যান বাটন ("🔄 Re-Scan") যুক্ত করা হয়েছে।
  - লাইভ ডায়াগনস্টিক কনসোলে বিস্তারিত হার্ডওয়্যার প্রোফাইল ও অডিট নোট স্ট্রিম করা হয়েছে।
  - ডিপ্লয়মেন্টের সময় স্বয়ংক্রিয়ভাবে `hardware_audit.json` ফাইলে সম্পূর্ণ প্রোফাইল `C:\Program Files\PCSecuritySystem\`-এ সংরক্ষণ করা হয়।
* **Why it was done (কেন করা হয়েছে)**:
  - অ্যাডমিনিস্ট্রেটর বা টেকনিশিয়ান ডিপ্লয় বাটনে ক্লিক করার আগেই এক পলকে দেখতে পারবেন পিসিটির মাদারবোর্ড ও নেটওয়ার্ক কনফিগারেশন কী এবং সিস্টেমটি সম্পূর্ণ প্রস্তুত কিনা।

---

### [v1.1.0] - 2026-09-03 to 2026-09-06

#### 1. Hybrid Dual-Plane Custom Cyber Lock Engine
* **Files**: 
  - [`pc-agent/LockEngine/DesktopManager.cs`](file:///F:/PC_Lock/pc-agent/LockEngine/DesktopManager.cs)
  - [`pc-agent/LockEngine/KeyboardHook.cs`](file:///F:/PC_Lock/pc-agent/LockEngine/KeyboardHook.cs)
  - [`pc-agent/LockEngine/TaskManagerPolicy.cs`](file:///F:/PC_Lock/pc-agent/LockEngine/TaskManagerPolicy.cs)
  - [`pc-agent/LockEngine/Views/LockScreenForm.cs`](file:///F:/PC_Lock/pc-agent/LockEngine/Views/LockScreenForm.cs)
  - [`pc-agent/LockEngine/LockEngineCoordinator.cs`](file:///F:/PC_Lock/pc-agent/LockEngine/LockEngineCoordinator.cs)
* **What was added / changed (কী করা হয়েছে)**:
  - তৈরি করা হয়েছে নিজস্ব ডার্ক সাইবার লক স্ক্রিন UI যাতে রয়েছে লাইভ ডিজিটাল ঘড়ি, তারিখ, ভার্চুয়াল কিপ্যাড, কিবোর্ড পিন ইনপুট, মোবাইল আনলকের জন্য ডাইনামিক QR কোড এবং মাস্টার পিন (`998877`, `SHJ`, `123456`) সাপোর্ট।
  - `DesktopManager.cs` দিয়ে Win32 আইসোলেটেড ডেস্কটপ (`PC_LOCK_SECURE_DESKTOP`) চালু করা হয়েছে।
  - `KeyboardHook.cs` দিয়ে গ্লোবাল `WH_KEYBOARD_LL` হুক যুক্ত করে Windows Key, Alt+Tab, Alt+Esc, Ctrl+Esc, Ctrl+Shift+Esc এবং Alt+F4 সম্পূর্ণরূপে ব্লক করা হয়েছে।
  - `TaskManagerPolicy.cs` দিয়ে রেজিস্ট্রি পলিসি প্রয়োগ করে টাস্ক ম্যানেজার ডিজেবল (`DisableTaskMgr = 1`) করা হয়েছে।
* **Why it was done (কেন করা হয়েছে)**:
  - সাধারণ উইন্ডোজ লক স্ক্রিন (`LockWorkStation`) অনেক ক্ষেত্রে ব্যবহারকারীকে পর্যাপ্ত ভিজ্যুয়াল ব্রান্ডিং বা ডাইনামিক কিউআর কোড স্ক্যান সুবিধা দেয় না এবং ক্যাফে/ল্যাব এনভায়রনমেন্টে বিভিন্ন থার্ড-পার্টি অ্যাপ স্ক্রিন দখল করতে পারে। নিজস্ব আইসোলেটেড ডেস্কটপ এবং লো-লেভেল কিবোর্ড ইন্টারসেপশন উইন্ডোজের যেকোনো বাইপাস টেকনিককে সম্পূর্ণরূপে প্রতিহত করে।

---

#### 2. Safe Desktop Lock Testing Harness
* **Files**:
  - [`test_lock_engine.bat`](file:///F:/PC_Lock/test_lock_engine.bat)
  - [`pc-agent/Program.cs`](file:///F:/PC_Lock/pc-agent/Program.cs)
  - [`pc-agent/Controllers/BootGuardHealer.cs`](file:///F:/PC_Lock/pc-agent/Controllers/BootGuardHealer.cs)
* **What was added / changed (কী করা হয়েছে)**:
  - `Program.cs`-এ `--test-lock` কমান্ড-লাইন ফ্ল্যাগ যুক্ত করা হয়েছে।
  - রুটে ১-ক্লিকে রানযোগ্য `test_lock_engine.bat` স্ক্রিপ্ট তৈরি করা হয়েছে।
  - টেস্ট মোড চালু থাকলে `BootGuardHealer.IsPreBootEnabled = false` করে রাখা হয়েছে।
* **Why it was done (কেন করা হয়েছে)**:
  - ডেভেলপার ও অ্যাডমিনরা যেন ফার্মওয়্যারে (UEFI/EFI) কোনো পরিবর্তন না এনে এবং পিসি রিবুট বা ব্রিকিং ঝুঁকি ছাড়া সরাসরি চলমান উইন্ডোজ ডেস্কটপে লক স্ক্রিনের UI, ভার্চুয়াল কিপ্যাড, কিবোর্ড শিল্ড এবং মাস্টার পিন পরীক্ষা করতে পারেন।

---

#### 3. Permanent Program Files Installation & Safe USB Removal
* **Files**:
  - [`DeployManager/Services/DeploymentEngine.cs`](file:///F:/PC_Lock/DeployManager/Services/DeploymentEngine.cs)
* **What was added / changed (কী করা হয়েছে)**:
  - পেনড্রাইভ বা এক্সটার্নাল ড্রাইভ থেকে `DeployManager.exe` রান করে ডিপ্লয় করার সময় `PC.SecurityAgent.exe`-কে স্বয়ংক্রিয়ভাবে `C:\Program Files\PCSecuritySystem\` ফোল্ডারে কপি করার লজিক যুক্ত করা হয়েছে।
  - ডিপ্লয়ারের আনইন্সটল রুটিনে এই ডিরেক্টরি এবং সংশ্লিষ্ট রেজিস্ট্রি কি সম্পূর্ণ মুছে ফেলার লজিক যোগ করা হয়েছে।
* **Why it was done (কেন করা হয়েছে)**:
  - পূর্বে পেনড্রাইভ থেকে ডিপ্লয় করার পর পেনড্রাইভ খুলে ফেললে ব্যাকগ্রাউন্ড এজেন্ট ফাইল না পেয়ে ক্র্যাশ করত। পার্মানেন্ট ডিরেক্টরিতে সেলফ-কপি হওয়ায় ডিপ্লয় শেষেই **পেনড্রাইভ নিরাপদে খুলে নেওয়া সম্ভব**।

---

#### 4. Zero-Dependency Standalone Compilation & Dynamic Agent Resolution
* **Files**:
  - [`build_all_standalone.bat`](file:///F:/PC_Lock/build_all_standalone.bat)
  - [`DeployManager/Services/DeploymentEngine.cs`](file:///F:/PC_Lock/DeployManager/Services/DeploymentEngine.cs)
  - [`DeployManager/publish_single_file.bat`](file:///F:/PC_Lock/DeployManager/publish_single_file.bat)
  - [`pc-agent/publish_single_file.bat`](file:///F:/PC_Lock/pc-agent/publish_single_file.bat)
  - [`.gitignore`](file:///F:/PC_Lock/.gitignore)
* **What was added / changed (কী করা হয়েছে)**:
  - `build_all_standalone.bat`-এ .NET 8-এর `--self-contained true -p:PublishSingleFile=true` ফ্ল্যাগ ব্যবহার করে সিঙ্গেল-ফাইল বাইনারি বিল্ড করা হয়েছে।
  - ডিপ্লয়মেন্ট ইঞ্জিনে থাকা হার্ডকোডেড `D:\Soft\dotnet\dotnet.exe` পাথ অপসারণ করে `ResolveSecurityAgent()` এবং `FindPrebootEfiBinary()` মেথডের মাধ্যমে রানটাইমে স্বয়ংক্রিয়ভাবে বাইনারি সনাক্তকরণের ব্যবস্থা করা হয়েছে।
  - `.gitignore`-এ `release_package/`, `bin_publish/`, এবং `*.zip` যোগ করা হয়েছে।
* **Why it was done (কেন করা হয়েছে)**:
  - ক্লায়েন্ট বা টার্গেট পিসিতে .NET 8 SDK, Runtime বা LLVM/Clang কিছুই ইনস্টল থাকার প্রয়োজন নেই। যেকোনো স্ট্যান্ডার্ড উইন্ডোজ ১০/১১ পিসিতে কোনো প্রি-রিকুইজিট ছাড়া সফটওয়্যারটি সরাসরি চলবে।

---

#### 5. Automated CI/CD GitHub Actions Release Pipeline
* **Files**:
  - [`.github/workflows/release.yml`](file:///F:/PC_Lock/.github/workflows/release.yml)
* **What was added / changed (কী করা হয়েছে)**:
  - একটি অটোমেটেড GitHub Actions ওয়ার্কফ্লো তৈরি করা হয়েছে যা নতুন কোনো ভার্সন ট্যাগ (যেমন `v1.1.0`) পুশ করলে স্বয়ংক্রিয়ভাবে উইন্ডোজ রানারে .NET 8 ও LLVM সেটআপ করে `build_all_standalone.bat` এক্সিকিউট করে সম্পূর্ণ প্যাকেজটি ZIP ফাইল আকারে GitHub Releases-এ আপলোড করে দেয়।
  - বিল্ড স্ক্রিপ্ট ও ডিপ্লয়মেন্ট ইঞ্জিনে হেডলেস CI ডেডলক পরিহারের জন্য `if not defined CI pause` এবং ক্র্যাশ গার্ড যোগ করা হয়েছে।
* **Why it was done (কেন করা হয়েছে)**:
  - ম্যানুয়াল বিল্ড ও লোকাল জিপ তৈরির ঝামেলা দূর করে ক্লাউড থেকে সরাসরি প্রোডাকশন-রেডি রিলিজ ফাইল ডাউনলোডযোগ্য করা।

---

### [v1.0.0] - Initial Enterprise Architecture

* **Files**:
  - [`uefi-preboot/src/efi_main.c`](file:///F:/PC_Lock/uefi-preboot/src/efi_main.c) (UEFI Pre-Boot EFI)
  - [`acpi-wpbt/`](file:///F:/PC_Lock/acpi-wpbt) (ACPI WPBT Persistence)
  - [`pc-agent/Services/SecurityService.cs`](file:///F:/PC_Lock/pc-agent/Services/SecurityService.cs) (Windows Background Service)
  - [`pc-agent/CredentialProvider/WinlogonProvider.cpp`](file:///F:/PC_Lock/pc-agent/CredentialProvider/WinlogonProvider.cpp) (Native C++ Logon Tile)
  - [`backend/src/index.ts`](file:///F:/PC_Lock/backend/src/index.ts) (Cloud WebSocket Relay Hub)
  - [`mobile-app/index.html`](file:///F:/PC_Lock/mobile-app/index.html) (PWA Controller)
  - [`android-app/`](file:///F:/PC_Lock/android-app) (Native Android Application)
* **What was added / changed (কী করা হয়েছে)**:
  - মাল্টি-লেয়ার সিকিউরিটি আর্কিটেকচার প্রতিষ্ঠা: বেয়ার-মেটাল ফার্মওয়্যার লেভেল থেকে ক্লাউড রিলে এবং মোবাইল অ্যাপ পর্যন্ত সম্পূর্ণ সিস্টেম ডিজাইন ও ইমপ্লিমেন্টেশন।
* **Why it was done (কেন করা হয়েছে)**:
  - সাইবার ক্যাফে ও ল্যাব ওয়ার্কস্টেশনে হার্ডডিস্ক ফরম্যাট বা উইন্ডোজ রিস্টার্টেও টিকে থাকার মতো সর্বোচ্চ স্তরের নিরাপত্তা নিশ্চিত করা।
