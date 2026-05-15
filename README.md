# NBody-VR Visualizer

A VR visualization tool for N-body astrophysical simulations, designed specifically for Meta Quest headsets. 
This tool allows researchers to intuitively explore and analyze complex black hole (BH) interactions, such as binary formations, exchanges, and mergers, in an immersive 3D space.

* **Event Detection & Navigation**: Automatically detects `MERGE`, `EXCHANGE` events from simulation data. Jump directly to specific events using the in-app VR UI.
* **Automated Data Pipeline**: A single Python script parses raw HDF5/DAT outputs and automatically transfers the processed data to your Quest headset via ADB.

* ## Package Contents
End-users do not need to download the source code or install Unity. Please download the following files from the [Releases](https://github.com/gkawa08/nbody-vr-visualizer-unity/releases) page:

* `NBodyVR_v1.0.apk`: The VR application for Meta Quest.
* `nbody_bh_data.py`: The data processing and transfer script.
* `sample_data/`: (Optional) Sample HDF5 and DAT files to test the pipeline.


## Prerequisites
* **Hardware**: Meta Quest 2 (Developer Mode must be enabled).
* **Software (PC)**:
  * Python 3.8+
  * ADB (Android Debug Bridge) installed and added to your system PATH.
  * SideQuest (or similar tool for installing APKs).

### Python Dependencies
pip install numpy h5py


## Quick Start Guide
1\. Install the App (First time only)
  1\. Connect your Meta Quest to your PC via USB.
  2\. Install NBodyVR_v1.0.apk using SideQuest or the ADB command:
    Bash
    adb install NBodyVR_v1.0.apk


2\. Prepare Your Data
Place your N-body simulation output files in the same directory as the Python script:
* snapdata.hdf5
* *_output.dat

3\. Run the Pipeline
Run the Python script from your terminal:
Bash
python BH_data_mp.py


Follow the interactive prompts. The script will:
* Extract BH data from snapdata.hdf5 into bh_history.csv.
* Parse .dat files for events into bh_events.csv.
* Automatically transfer files to your Quest headset.

4\. Experience in VR
Launch NBody-VR from your "Unknown Sources" library.

## License
