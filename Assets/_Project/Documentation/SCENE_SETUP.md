# QuickCopy AR - Scene Setup Guide

## Overview
This document describes how to set up the Unity scene for the QuickCopy AR application on Meta Quest 3S.

## Prerequisites

1. **Unity Version**: 2022.3 LTS
2. **Required Packages**:
   - Meta XR All-in-One SDK (via Package Manager)
   - TextMeshPro (for toast notifications)

3. **Build Settings**:
   - Platform: Android
   - Texture Compression: ASTC
   - Minimum API: 29 (Android 10)
   - Target API: 32 (Android 12)
   - Scripting Backend: IL2CPP
   - Target Architectures: ARM64 only

## Scene Hierarchy

```
MainScene
├── OVRCameraRig
│   ├── TrackingSpace
│   │   ├── LeftEyeAnchor
│   │   ├── CenterEyeAnchor
│   │   │   └── Main Camera
│   │   ├── RightEyeAnchor
│   │   ├── LeftHandAnchor
│   │   │   └── OVRHandPrefab (Left)
│   │   ├── RightHandAnchor
│   │   │   └── OVRHandPrefab (Right)
│   │   ├── LeftControllerAnchor
│   │   └── RightControllerAnchor
│   └── OVRManager (component)
│
├── PassthroughLayer
│   └── OVRPassthroughLayer (component)
│
├── Managers
│   ├── AppManager
│   │   └── AppManager.cs
│   ├── CaptureManager
│   │   └── CaptureManager.cs
│   ├── OCRProcessor
│   │   └── OCRProcessor.cs
│   ├── ClipboardManager
│   │   └── ClipboardManager.cs
│   └── InputManager
│       └── InputManager.cs
│
├── UI
│   ├── WorldSpaceCanvas
│   │   ├── FloatingButton
│   │   │   └── FloatingButton.cs
│   │   └── ToastNotification
│   │       └── ToastNotification.cs
│   └── OverlayCanvas
│       └── TextHighlighter
│           └── TextHighlighter.cs
│
└── EventSystem
```

## Step-by-Step Setup

### 1. Create New Scene
1. File > New Scene
2. Save as `MainScene` in `Assets/Scenes/`

### 2. Set Up OVRCameraRig
1. Delete the default Main Camera
2. Add OVRCameraRig prefab from `Oculus/VR/Prefabs/`
3. Configure OVRManager component:
   - Tracking Origin Type: Floor Level
   - Use Recommended MSAA Level: On
   - Enable Hand Tracking: Controllers And Hands
   - Hand Tracking Support: Controllers And Hands

### 3. Set Up Passthrough
1. Create empty GameObject named "PassthroughLayer"
2. Add OVRPassthroughLayer component
3. Configure settings:
   - Projection Surface: Reconstructed
   - Compositing: Underlay
   - Enable Passthrough: On (check the box)

4. On OVRManager, enable:
   - Passthrough Capability Enabled
   - Insight Passthrough > Enabled

### 4. Create Managers
1. Create empty GameObject "Managers"
2. Create child objects for each manager:
   - AppManager with AppManager.cs
   - CaptureManager with CaptureManager.cs
   - OCRProcessor with OCRProcessor.cs
   - ClipboardManager with ClipboardManager.cs
   - InputManager with InputManager.cs

3. Wire up references in AppManager inspector

### 5. Set Up UI Canvas

#### Floating Button
1. Create World Space Canvas
2. Set Render Mode: World Space
3. Set size: 0.1 x 0.1 (meters)
4. Position: 0.5m in front of camera, bottom-right

Create button structure:
```
FloatingButton (GameObject)
├── ButtonImage (Image)
│   └── Color: Cyan with alpha 0.8
├── IconImage (Image)
│   └── Scan icon sprite
└── SpinnerImage (Image)
    └── Loading spinner sprite (hidden by default)
```

Add components:
- Button
- FloatingButton.cs
- AudioSource (for click sounds)

#### Toast Notification
1. Create child of Canvas
2. Position at top-center

Create structure:
```
ToastNotification (GameObject)
├── Background (Image)
│   └── Color: Dark gray with alpha 0.85
└── MessageText (TextMeshProUGUI)
    └── Alignment: Center
```

Add components:
- ToastNotification.cs
- CanvasGroup (for fade animations)

### 6. Set Up Text Highlighter
1. Create Screen Space Overlay Canvas
2. Add TextHighlighter component
3. Configure highlight colors in inspector

### 7. Configure Input Manager
1. Add OVRHandPrefab to each hand anchor (optional, for hand tracking)
2. Configure InputManager:
   - Use Controller Input: On
   - Capture Button: PrimaryIndexTrigger
   - Use Hand Tracking: On
   - Pinch Threshold: 0.8

### 8. Add Audio Sources
1. Add AudioSource to FloatingButton
2. Add AudioSource to InputManager
3. Create/import audio clips:
   - capture_click.wav
   - success_ding.wav
   - error_boop.wav

## XR Plugin Management

1. Edit > Project Settings > XR Plugin Management
2. Android tab:
   - Check "Oculus" provider
3. Oculus settings:
   - Target Devices: Quest 3
   - Low Overhead Mode: On
   - Late Latching: On

## Player Settings

1. Edit > Project Settings > Player
2. Android settings:
   - Company Name: Flying Changes Farm
   - Product Name: QuickCopy AR
   - Package Name: com.flyingchangesfarm.quickcopy
   - Minimum API Level: 29
   - Target API Level: 32
   - Scripting Backend: IL2CPP
   - Target Architectures: ARM64
   - Internet Access: Auto (for initial ML Kit model download)

## Quality Settings

1. Edit > Project Settings > Quality
2. Recommended settings for Quest:
   - Pixel Light Count: 1
   - Anti Aliasing: 4x Multi Sampling
   - Shadows: Disable or Soft Shadows only
   - VSync Count: Don't Sync

## Testing

### In Editor
1. Use Oculus Link or Air Link for in-editor testing
2. Install Meta Quest Developer Hub for wireless testing

### On Device
1. Enable Developer Mode on Quest
2. Build and Run (Ctrl+B on Windows, Cmd+B on Mac)
3. First launch requires internet for ML Kit model download

## Troubleshooting

### Passthrough not working
- Ensure OVRPassthroughLayer is enabled
- Check OVRManager has passthrough enabled
- Verify Quest 3 firmware is up to date

### ML Kit not recognizing text
- Ensure first launch had internet connection
- Model downloads automatically via Google Play Services
- Check logcat for ML Kit errors

### Hand tracking not detecting pinch
- Ensure hand tracking is enabled in Quest settings
- Check OVRHand components are added to hand anchors
- Verify pinch threshold in InputManager

### Build fails with Gradle errors
- Ensure Kotlin plugin is configured in baseProjectTemplate.gradle
- Check that all dependencies are compatible
- Try: Build Settings > Clean Build

## Performance Tips

1. Keep passthrough as Underlay (not Overlay)
2. Use LOD for any 3D objects
3. Minimize UI overdraw
4. Profile with Meta Quest Developer Hub
5. Target 72Hz for stable performance
