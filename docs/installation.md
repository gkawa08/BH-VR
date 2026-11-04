# Install, launch, and explore

[Back to the README](../README.md) · [Use your own data](custom-data.md) · [Unity development](development.md) · [Science Mode](science-mode.md)

Use this guide to install the released APK and explore the bundled sample. Unity and Python are not required for this route. The instructions apply to installation from either a Mac or a Windows computer; they are distinct from running a VR scene directly from Unity Editor.

## 1. Prepare the headset and computer

You need:

- A Meta Quest headset with its controllers.
- A computer with an internet connection.
- A USB cable that supports data transfer; Thunderbolt is not required.
- The APK from [GitHub Releases](https://github.com/gkawa08/nbody-vr-visualizer-unity/releases).

Use the following tools to copy the APK or your own simulation dataset to the headset:

| Computer | File-transfer method |
|---|---|
| **Windows** | Use the built-in File Explorer after allowing USB file access on the headset. No additional file-transfer application is normally needed. |
| **Mac** | Use an Android file-transfer tool such as [ADB](https://developer.android.com/tools/adb), [OpenMTP](https://openmtp.ganeshrvel.com/), or [SideQuest](https://sidequestvr.com/). Finder alone does not provide this MTP file-transfer workflow. |

After copying the APK, install it inside the headset using its Files app as described below. The bundled sample needs no separate data transfer. If headset storage or a dataset destination folder is unavailable through a file browser, use ADB for the [custom-data transfer](custom-data.md#6-transfer-the-dataset-to-quest).

For ADB-based installation, enable developer mode and follow Meta's current [headset setup instructions](https://developers.meta.com/horizon/documentation/unity/unity-env-device-setup/). Windows users should also check the ADB driver requirements in that guide. For the Files-app route, follow any installation-permission prompts shown by the headset; availability and menu names depend on its OS version and settings.

## 2. Connect the headset

1. Turn on and unlock the headset.
2. Connect it to the computer with the USB data cable.
3. Put on the headset and allow USB file/data access when prompted.
4. Open the headset's internal storage in Windows File Explorer or in your Mac transfer application.

If you use ADB or SideQuest instead, enable developer mode, approve this computer's USB-debugging authorization inside the headset, and check that the application recognizes the device. With ADB, run:

   ```bash
   adb devices
   ```

An authorized device is listed with status `device`. If it is `unauthorized`, approve the request inside the headset. If no device is listed, check the cable, developer mode, and, on Windows, the device driver.

USB file access and USB-debugging authorization are separate permissions. Allow file/data access inside the headset when using Windows File Explorer or OpenMTP. USB debugging is needed for ADB and SideQuest. On macOS, the headset does not need to appear in Finder for those tools to work.

## 3. Install the APK

### Copy the APK and install it inside the headset

1. On Windows, use File Explorer to copy the downloaded APK to the headset's `Download` folder. On Mac, use an Android file-transfer tool such as **ADB**, **OpenMTP**, or **SideQuest**.
2. Put on the headset, open its **Files** app, and locate the APK in Downloads.
3. Open the APK's three-dot menu, choose **Open with**, then **Package Installer** or **Package Manager**. If tapping the APK opens the installer directly, use that prompt instead.
4. Select **Install** and follow the headset's permission prompts.
5. After installation finishes, open the app from the sideloaded/unknown-source applications section.

This route needs no additional Windows desktop software. Copying the file is followed by installation inside the headset; it is not installed by File Explorer or OpenMTP itself. The Files-app menu and availability vary with headset OS and settings. If no package-installation option is available, use the computer-based method below.

The Files-app installation route is described in [SideQuest's headset-installation instructions](https://sidequestvr.com/app/9473/sidequest-inside-); see also [AnExplorer's Meta Quest System Files instructions](https://anexplorer.io/install/apk-on-vr-headset).

### Alternative: install from the computer

Use SideQuest's APK installation option, or [Android SDK Platform Tools](https://developer.android.com/tools/releases/platform-tools), which includes ADB. These methods can install the APK from either Windows or Mac:

```bash
adb install /path/to/NBodyVis.apk
```

On Windows, use your downloaded file's actual path; PowerShell users running ADB from its extracted folder may need `./adb.exe` in place of `adb`. If Platform Tools is not on PATH on macOS, use `./adb` from its folder or its full path.

For an update signed with the same key, use:

```bash
adb install -r /path/to/NBodyVis.apk
```

If installation reports an incompatible signature, do not uninstall the existing app before backing up any imported data you want to keep. A build made on another computer may use a different signing key; see [signing and installed data](development.md#signing-and-installed-data).

## 4. Launch and try the sample

1. Open the headset's sideloaded/unknown-source applications section and launch NBody-VR.
2. Choose **EN** or **JP** in the two-page tutorial.
3. Select **Start Exploring**.
4. Press **Play**.
5. Select an event, inspect its participants, then try **Science** and **Follow Mode**.

The APK contains the fallback dataset in `Assets/StreamingAssets/NBODYVR_Data/`. If no external dataset is installed, it loads automatically. You do not need to transfer the sample separately or attach CSV files in the Unity Inspector.

## Return to the bundled sample

At startup, external data take priority over the bundled sample. Closing the app and moving your imported dataset out of the active location lets you return to the sample without deleting it.

The app checks these locations in order:

1. Dataset subfolders containing `manifest.json` under:

   ```text
   /sdcard/Android/data/com.kawashima.bhvisualizer/files/datasets/
   ```

2. The older root-level CSV layout under:

   ```text
   /sdcard/Android/data/com.kawashima.bhvisualizer/files/
   ```

3. The bundled sample inside the APK.

Move active imported datasets to a separate storage location or a computer. If you used the older root-level layout, move those CSV files together as well. Then restart the app. Folder names containing `_backup_` or ending in `_uploading` are ignored by the current dataset-folder search.

Do not rename an active dataset arbitrarily inside `datasets` and expect it to be disabled: any eligible subfolder containing `manifest.json` can be selected. An invalid external dataset is rejected rather than silently replaced with the sample.

## Troubleshooting installation and startup

| Symptom | What to check |
|---|---|
| The computer cannot see the headset | Developer mode, USB-debugging authorization, a data-capable cable, and Windows drivers where required. |
| `adb` is not found | Add Platform Tools to PATH or use the executable's full path. SideQuest is an alternative. |
| `unauthorized` in `adb devices` | Put on the headset and approve this computer. |
| APK update fails with a signature error | Use a consistently signed release or follow the backup-first instructions in the development guide. |
| Files does not offer a package installer for the APK | Menu availability varies by headset OS and settings. Use SideQuest or ADB from the computer instead. |
| The sample is not displayed | Check for an imported dataset or legacy root-level CSV files. |
| The wrong imported run loads | Keep one active dataset folder; the current app loads the first eligible folder in sorted path order. |
| A dataset-format or units error appears | Use a complete dataset generated by the matching converter; see the custom-data guide. |
| Science details are unavailable | Select an event and check that its measurements exist in the dataset. Some events can be visualized without complete PRE/POST science measurements. |

For a clean-machine test, check installation, tutorial, sample playback, event selection, Science Mode, Follow Mode, and camera reset. Record the computer OS, headset model, and APK version when reporting a problem; these instructions do not by themselves establish that every combination has been tested.

## Controls reference

| Control | What it does |
|---|---|
| **Help** | Reopens the English/Japanese tutorial. |
| **Science: ON/OFF** | Shows or hides Science Mode. The detailed panel appears when an event is selected and compatible science data are loaded. |
| **Follow Mode** | Opens a target list. Select an available black hole or a selected event's binary centre of mass. Press **Exit Follow** to leave the mode. |
| **Select Event...** | Lists exchange, transient-exchange, and merger events whose participants can be represented from the loaded history. |
| **Timeline** | Selects a stored simulation frame directly. |
| **&lt; / &gt;** | Moves backward or forward by one stored frame. |
| **Speed** | Changes playback from the configured minimum to maximum steps per second on a logarithmic scale. The middle position uses the configured initial speed. |
| **Size** | Changes only the visual scale of the black-hole spheres. |
| **Pos: CoM / Pos: Raw** | Switches between cluster centre-of-mass-relative positions and raw simulation coordinates. This button is disabled while Follow Mode is active. |
| **Reset Camera** | Restores the XR rig and the main panel to their initial poses. |
| **Play / Stop** | Starts or stops playback. Playback loops to the first stored frame after the last frame. |

Selecting an event moves playback to a short pre-event lead-in. In the bundled scene it can also move the player closer to the event, move the panel by the same translation, and reduce the black-hole display size on the first event selection. These behaviours are configurable in the `CsvVisualizer` Inspector.

### Controller and panel actions

- The controller ray and trigger operate the UI.
- The controller **Primary Button** toggles the main UI. The bundled scene labels this action as **A**.
- When the main UI is closed, the right thumbstick's vertical axis moves the XR rig vertically.
- The main panel has an XR grab interaction. While it is held, it turns to face the player and remains upright; it faces the player again when released.
- When an event is selected while the panel is hidden, a world-space event countdown or completion message remains visible.

### Colors and event graphics

The current defaults in `CsvVisualizer` are:

| Display | Default color or behaviour |
|---|---|
| Black hole with no selected event role | Black |
| Selected binary member | Magenta |
| Incoming/interloping black hole | Red |
| Post-merge remnant | Cyan |
| Transient-exchange participant | Orange |
| Other objects during a selected event | Dimmed |
| Trajectory | Smoothed line using configurable past and future frame windows |
| Scale bar | Camera-facing world-space bar; its label and length are configurable |
