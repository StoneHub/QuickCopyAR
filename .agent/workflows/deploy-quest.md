---
description: Build and Deploy to Meta Quest
---

# Deploy to Meta Quest

This workflow guides you through building and deploying the standalone APK to a connected Meta Quest headset.

## Prerequisites

- Meta Quest connected via USB with Developer Mode enabled.
- ADB installed and accessible in PATH.
- Unity Android Build Support installed.

## Workflow

1. **Verify Connection**
   Ensure your device is listed.

   ```powershell
   adb devices
   ```

2. **Build APK**
   - Open Unity.
   - Go to **File > Build Settings**.
   - Ensure Platform is **Android**.
   - Texture Compression: **ASTC**.
   - Click **Build**. Save as `Builds/QuickCopyAR.apk`.

3. **Install to Device**
   // turbo

   ```powershell
   adb install -r Builds/QuickCopyAR.apk
   ```

4. **Launch App**
   // turbo

   ```powershell
   adb shell monkey -p com.DefaultCompany.QuickCopyAR_Standalone -c android.intent.category.LAUNCHER 1
   ```

## Troubleshooting

- If `adb` is not found, add your Android SDK platform-tools to PATH.
- If deployment fails, try uninstalling the previous version first: `adb uninstall com.DefaultCompany.QuickCopyAR_Standalone`.
