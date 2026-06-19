# NBody-VR Visualizer

A VR visualization tool for N-body astrophysical simulations (specifically focusing on black hole interactions) designed for Meta Quest 2 and Meta Quest 3 headsets. This tool allows researchers to intuitively explore and analyze complex black hole (BH) dynamics, such as binary formations, exchange interactions (EXCHANGE) and mergers (MERGE) in an immersive 3D space.

## 🎬 Demo Video


---

## 🌟 Key Features

* **3D Orbit Visualization:** Renders the spatial orbits and interactive dynamics of black holes.
* **Event Detection & Highlighting:** Automatically detects and highlights key astrophysical events ("MERGE", "EXCHANGE") directly within the VR environment.
* **Custom Data Pipeline:** Includes a Python data pipeline script to parse raw simulation outputs (".hdf5" and "*_output.dat" from NBody6) into lightweight CSV files optimized for mobile VR performance.
* **Standalone Playback:** Run simulations standalone on your Quest headset without needing a constant PC connection.

---

## 🛠️ Requirements

* **Hardware:** Meta Quest 2 or Meta Quest 3
* **Software (for transfer/installation):**
  * SideQuest or ADB ("android-platform-tools")
* **Software (for data conversion):**
  * Python 3.x
  * Required Python packages: "numpy", "h5py"

---

## 📥 Installation Guide

Follow these steps to deploy the application (".apk") and initialize its configurations.

### 1. Download the APK
Navigate to the [Releases](../../releases) page of this repository and download the latest version of "NBodyVR_vX.X.apk". 
*(Note: Due to GitHub file size limitations, the application binary is managed via GitHub Releases rather than directly in the source tree).*

### 2. Install onto Meta Quest
Connect your Quest headset to your computer via USB and install using one of the following methods:
* **Via SideQuest:** Click the "Install APK file from folder" icon (top-right menu) and select the downloaded APK.
* **Via Terminal (ADB):**
  ```bash
  adb install NBodyVR_v1.0.apk
  ```

### 3. First-Time Launch (⚠️ CRITICAL)
After installing the APK, **you must launch the application at least once on the headset before transferring your data files.**
1. Put on your Quest headset, navigate to your App Library, change the filter to **"Unknown Sources"**, and open the app.
2. The screen will display a "Data missing!" warning. This is expected. Exit the application.
   *(This process forces the Android system to generate the specific data directory: "com.kawashima.bhvisualizer")*

---

## 📊 Data Preparation & Transfer

Use the included "nbody_bh_data.py" script to extract and transfer simulation data.

### 1. Execute the Script
Run the script within the directory containing your simulation data (HDF5/DAT). On its first run, it will automatically check for and offer to install any missing dependencies ("numpy", "h5py").

```bash
python nbody_bh_data.py
```

### 2. Enter Parameters
The script will prompt you for setup paths and constants. Press "Enter" to accept the optimized default values:

```text
Enter data directory [default: .]: 
Enter CSV output directory [default: .]:
Enter Quest package name [default: com.kawashima.bhvisualizer]:
Enter BH keyword type [default: 14]:
Enter mass conversion factor [default: 127918.2]:
Transfer to Quest via adb? (y/n) [default: y]:
```

### 3. Automated Data Deployment
Upon completion, the pipeline automatically writes and pushes the parsed "bh_history.csv" and "bh_events.csv" files directly to your Quest headset via ADB at:
"/sdcard/Android/data/com.kawashima.bhvisualizer/files/"

*(Alternatively, you can manually upload these generated CSV files into the directory above using SideQuest's File Manager).*

---

## 🥽 In-VR Navigation

Once files are transferred, open the application from "Unknown Sources" again. The environment will automatically render the star cluster and black hole trajectories based on your custom datasets.

* To swap datasets or test different simulation scenarios, you do **not** need to reinstall the APK. Simply re-run "nbody_bh_data.py" with your new data to overwrite the CSV files on the headset.

---

## 📝 For Developers

This repository contains the complete Unity project structure. To modify the UI, shader configurations, or interactive mechanics:
1. Open the project folder in Unity Editor.
2. Ensure your active build platform is set to **Android**.
3. If you change the **Package Name** in "Project Settings > Player", remember to update the destination package name when running the Python data pipeline.
"""
