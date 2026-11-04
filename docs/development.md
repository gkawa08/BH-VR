# Edit and build the Unity project

[Back to the README](../README.md) · [Install the APK](installation.md) · [Use your own data](custom-data.md) · [Science Mode](science-mode.md)

Use this route to change the app, inspect its source, or build an APK yourself. To run the released app, use the [installation guide](installation.md); Unity is not required for that route.

## Development environment

The repository currently records:

- Unity `6000.0.57f1`
- Universal Render Pipeline `17.0.4`
- Input System `1.14.2`
- XR Interaction Toolkit `3.3.1`
- Oculus XR Plugin `4.5.4`
- Meta XR Core SDK `85.0.0`
- Android application identifier `com.kawashima.bhvisualizer`
- Android minimum SDK `32` and target SDK `34`

Confirm the recorded version in [`ProjectSettings/ProjectVersion.txt`](../ProjectSettings/ProjectVersion.txt) and package versions in [`Packages/manifest.json`](../Packages/manifest.json). Start with that editor version when testing a clean setup.

Install [Unity Hub](https://unity.com/download), sign in, and activate the Unity license appropriate to your use. From Hub, install **Unity 6000.0.57f1** and select **Android Build Support**, **Android SDK & NDK Tools**, and **OpenJDK**. If the editor is not listed, use the [Unity archive](https://unity.com/releases/editor/archive). Use Hub's Android dependencies for that editor rather than installing unrelated SDK/NDK/JDK versions. See [Unity's Android setup documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/android-sdksetup.html).

## Open the project

1. Clone this repository or choose **Code > Download ZIP** on GitHub and extract it. APK and source downloads serve different purposes.
2. In Unity Hub, add the existing project folder: the parent containing `Assets`, `Packages`, and `ProjectSettings`. Do not create a new empty project or select the `Assets` folder itself.
3. Open it with the recorded editor version and wait for package download and asset import to finish. `Library` and other generated caches do not need to be copied from the original computer.
4. Open `Assets/Scenes/SampleScene.unity` and check the Console for compilation errors.

For Git-based development:

```bash
git clone https://github.com/gkawa08/nbody-vr-visualizer-unity.git
cd nbody-vr-visualizer-unity
```

The bundled sample is part of the source project. Python and the source HDF5 are not required to open it. You do not need to attach your own CSV files in the Inspector to test the bundled sample.

If Unity reports a missing dependency or asks to upgrade the project, first compare the installed editor and package versions with the recorded files. Reproduce the existing configuration before changing versions.

`SampleScene.unity` is the only enabled scene in the current build settings.

## Main project files

| Path | Role |
|---|---|
| `Assets/Scenes/SampleScene.unity` | Main XR scene and UI wiring. |
| `Assets/CsvVisualizer.cs` | Dataset loading, playback, event visualization, UI actions, follow modes, and Science Mode display. |
| `Assets/ScienceModeData.cs` | Science CSV parsing and event/snapshot matching. |
| `Assets/TutorialController.cs` | Two-page English/Japanese tutorial, Help behaviour, layout, and legend graphics. |
| `Assets/VRExtraControls.cs` | Main-UI toggle and right-stick vertical movement. |
| `Assets/MainCanvasPlayerFacingGrab.cs` | Grabbable panel orientation. |
| `Assets/Editor/ScienceModeSetupEditor.cs` | Unity Editor command that creates or repairs the Science Mode scroll panel. |
| `Assets/StreamingAssets/NBODYVR_Data/` | Bundled fallback dataset packaged in the APK. |
| `nbody_bh_data.py` | NBODY6-to-NBody-VR conversion and optional ADB transfer. |

## Replace the bundled sample

To ship a different fallback dataset in the APK:

1. Generate a complete format-version-1 dataset folder.
2. Replace the contents of `Assets/StreamingAssets/NBODYVR_Data/` with that folder's files.
3. Keep `manifest.json` and all filenames referenced by it together.
4. Rebuild the APK.

Files in `StreamingAssets` are packaged into the application. By contrast, replacing an external compatible dataset under the Quest `files/datasets/` directory does not require an APK rebuild.

## Edit visualization behaviour

Select the object containing `CsvVisualizer` and use its Inspector sections:

| Inspector section | Examples of configurable behaviour |
|---|---|
| **Settings** | History/event filenames, sphere prefab, scale multiplier, event pre-roll, size range, and XR origin. |
| **First Event Selection Size** | Whether and how much the first event selection reduces the sphere scale. |
| **Event Selection View** | Automatic event focus distance and whether the main panel follows the rig translation. |
| **Initial Demo Layout** | Initial playback rate, allowed rate range, view padding, minimum view distance, and panel placement. |
| **Scale Bar Settings** | Visibility, length, label, color, line width, and label placement. |
| **Colors** | Default, binary, interloper, post-merge, transient-exchange, and dim colors. |
| **Label & Callout Settings** | Label size, spread, screen-size compensation, and callout lines. |
| **Orbit Settings** | Past/future frame windows, line width, and smoothing. |
| **Science Mode Settings** | Units, pair lines, science filenames, matching tolerances, and KS/CHAIN mass tolerance. |
| **UI Linking / Follow Mode UI** | Scene references for sliders, buttons, text, dropdowns, and follow controls. |

## Edit the tutorial and panel

`TutorialController` stores separate English and Japanese pages, navigation text, black-hole legend labels, font sizes, and visual layout settings. The tutorial opens automatically when the scene starts, and **Help** reopens it.

The main panel is an XR grab interactable with `MainCanvasPlayerFacingGrab`. If its hierarchy or interaction components are replaced, reconnect the serialized references in `SampleScene`.

## Configure the Science Mode UI

With `SampleScene` open, use:

```text
Tools > NBODY VR > Configure Science Mode
```

The editor command finds the scene's `CsvVisualizer` and `MainCanvas`, creates or updates `ScienceDataPanel`, adds its scroll viewport and TextMesh Pro content, reconnects the science references, and marks the scene dirty. Save the scene after running the command, then enter Play Mode to verify it.

The command sets legacy science filenames and playback defaults while configuring the scene; a loaded format-version-1 manifest overrides the runtime filenames and identifies playback time as Myr.

## Build for Quest

1. Complete the [headset connection and USB-authorization steps](installation.md#2-connect-the-headset).
2. Open **File > Build Profiles** in the recorded Unity 6 editor and select **Android**. Switch to that platform and allow any reimport to finish.
3. Confirm that `Assets/Scenes/SampleScene.unity` is enabled in the scene list.
4. Keep the package identifier `com.kawashima.bhvisualizer` consistent with the transfer path. If you change it, use the same value with the converter's `--package-name` option.
5. Select the connected Quest as the run device and choose **Build And Run**. Save build output outside the source directories, for example under an ignored `Builds` folder.
6. Alternatively, build an APK and install it with SideQuest or ADB using the [installation guide](installation.md#3-install-the-apk).

Check that Unity uses its installed Android tools under **Settings/Preferences > External Tools** if it reports SDK, NDK, or JDK errors.

Changing C# code, the scene, project settings, or the bundled sample requires a new APK. Replacing an external format-compatible dataset does not.

### Editor preview versus headset testing

On macOS, use **Build And Run** to test the Android app on Quest. Quest Link's direct Play-in-Editor VR workflow is Windows-only. On a compatible Windows PC, [Meta Quest Link](https://developers.meta.com/horizon/documentation/unity/unity-link/) can support faster iteration when the desktop XR configuration is set up. Android configuration alone does not guarantee a desktop VR preview.

A successful Editor import does not establish that headset controls, sample loading, and Android deployment all work. Test the standalone build as well.

### Signing and installed data

Android APK updates must be signed consistently. A new development computer may have a different debug signing key from the one used for an existing installation.

If installation reports a signature mismatch, back up imported datasets before uninstalling anything. For repeated releases, use the project's established signing process and retain its signing key securely; do not commit private keystores or passwords. Alternatively, use a separate development package identifier to keep the test installation distinct, remembering that its external-data path also changes.

## Validate a clean setup

Use the GitHub source and bundled sample first, without copying untracked files or caches from the original machine:

1. Import the project without unresolved package or compilation errors.
2. Build and install the APK.
3. Open the tutorial and start sample playback on the headset.
4. Select an exchange and a merger; inspect Science Mode, Follow Mode, timeline controls, and camera reset.
5. Make one small visible UI change, rebuild, and confirm the changed build is running.
6. Separately convert and transfer a supported custom dataset, then restart the app and check that it takes priority over the sample.

Record the source commit, editor version, computer OS and architecture, headset model, and any errors. Mac and Windows validation should be recorded separately; this checklist is a test procedure, not a claim that both have already passed.
