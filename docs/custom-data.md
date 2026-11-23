# Use your own NBODY6 data

[Back to the README](../README.md) · [Installation](installation.md) · [Unity development](development.md) · [Science Mode](science-mode.md)

The converter reads one NBODY6 run and builds a self-contained BH-VR dataset folder.

## 1. Prepare the input directory

Prepare HDF5 snapshots and event logs from the same simulation. They may be in one directory or in separate directories:

- **Snapshots:** `snapdata.hdf5`. The script also recognizes `snapsdata.hdf5`; use `--hdf5` for another filename.
- **Event logs:** one or more files named `*_output.dat`. If no such files exist in the selected log directory, the script uses `output.dat` instead. When both naming styles are present, it reads the `*_output.dat` files only.

For a simple example, place `nbody_bh_data.py` beside a folder named `NBODY6` containing the HDF5 file and logs. In the guided prompts below, enter `NBODY6` for both input folders. The input logs must correspond to the selected HDF5 run; the structure check does not independently establish their common provenance.

The converter validates that the HDF5 file contains `snap_*` groups. Each snapshot group must provide `id`, `m`, `kw`, `x`, `y`, `z`, `vx`, `vy`, `vz`, and a scalar `t`. If a valid `N` value is present, rows `0:N` are treated as physical particles; otherwise the array length is used.

By default, the converter selects `kw == 14` as black holes. Use `--bh-type` only if the stellar-type convention in your data is different.

## 2. Prepare Python

The converter requires Python 3, NumPy, and h5py:

```bash
python -m pip install numpy h5py
```

If either module is missing, the script offers to install it before conversion. `adb` is optional and is needed only for the script's direct-to-Quest transfer step.

## 3. Run the guided conversion

The simplest route is the interactive mode:

```bash
python nbody_bh_data.py
```

The script asks for the snapshot directory, event-log directory, and output folder separately. For the example above:

```text
Enter snapshot directory (.hdf5) [default: .]: NBODY6
Enter event log directory (*_output.dat or output.dat) [default: .]: NBODY6
Dataset folder [default: NBODYVR_Data]: MyCluster

Choose what to do:
  [Y] Create the visualization dataset
  [N] Check the input data format only
Enter Y or N [default: Y]: y
```

The script then lists the HDF5 file and logs it will read. Inspect them before answering:

```text
Are these files correct? (y/n) [default: n]: y
```

Pressing Enter at this confirmation cancels the operation. At the earlier choice, **N** checks snapshot structure and log readability without writing or transferring a visualization dataset; it does not check every particle value or prove that all event measurements can be resolved.

The default `.` and all relative input/output paths are resolved from the directory containing `nbody_bh_data.py`, not the terminal's current working directory. Each input prompt has its own default: the log folder does not automatically inherit the snapshot folder. Absolute paths and `~` paths are also supported.

After a successful build, the script asks whether to transfer the dataset folder to the connected Quest. The default answer is **yes**. Choose **n** if you will use SideQuest or transfer later. If the destination already exists locally or on the headset, the script asks before retaining the old folder as a timestamped backup and replacing it.

Input files are read but never modified.

## 4. Optional command-line settings

You can also supply paths and conversion settings as command-line options. This remains a guided operation: the current script still asks you to confirm the input-file list.

For example, to build locally without the transfer prompt:

```bash
python nbody_bh_data.py \
  --data-dir /path/to/NBODY6/run \
  --log-dir /path/to/NBODY6/run \
  --output-dir MyCluster \
  --no-transfer
```

Relative input and output paths are resolved relative to the directory containing `nbody_bh_data.py`. The output folder's basename may contain letters, numbers, underscores, and hyphens. It must not contain the input directory.

Useful options are:

| Option | Purpose |
|---|---|
| `--data-dir DIR` | Select the HDF5 snapshot directory. |
| `--log-dir DIR` | Select the event-log directory. Its default is the script directory, including when `--data-dir` points elsewhere. |
| `--output-dir DIR` | Name the generated dataset folder. |
| `--hdf5 FILE` | Select a particular HDF5 filename inside `--data-dir`. Without it, the script looks for `snapdata.hdf5` and then `snapsdata.hdf5`. |
| `--check-only` | Validate the HDF5 structure and parse the logs without writing or transferring a dataset. |
| `--no-transfer` | Build locally without offering the ADB transfer step. |
| `--force` | Skip the local dataset-replacement confirmation; the previous folder is still retained as a timestamped backup. The input-file confirmation still applies. |
| `--max-time-myr VALUE` | Include only snapshots and parsed log records at or before this physical time. |
| `--workers N` | Set the number of snapshot-processing worker processes. The automatic default is at most four. |
| `--bh-type N` | Select the stellar type to export; the default is `14`. |
| `--mass-conv VALUE` | Override the detected mass conversion to solar masses. |
| `--length-conv VALUE` | Override the detected length conversion to parsecs. |
| `--velocity-conv VALUE` | Override the detected velocity conversion to kilometres per second. |
| `--time-conv VALUE` | Override the detected time conversion to Myr. |
| `--science-search-window VALUE` | Set the maximum PRE/POST event-search window in Myr; the default is `50`. |
| `--exchange-group-tolerance VALUE` | Set the time tolerance used to group exchange diagnostics; the default is `0.001` Myr. |
| `--composite-mass-tolerance VALUE` | Set the relative mass tolerance for conservative KS/CHAIN proxy reconstruction; the default is `0.02`. |
| `--package-name NAME` | Override the Android package used for ADB transfer; the default is `com.kawashima.bhvisualizer`. |

The script first searches the output logs for a `PHYSICAL SCALING` record. If none is found and no conversion overrides are supplied, it uses the fallback values defined in the script and reports `defaults` as the scaling source. Always check the printed scaling and the generated report before using the result scientifically.

## 5. Inspect the generated dataset

A successful conversion produces one folder:

```text
MyCluster/
├── manifest.json
├── bh_history.csv
├── bh_events.csv
├── science_events.csv
├── science_cluster_dynamics.csv
└── nbody_pipeline_report.json
```

| File | Purpose |
|---|---|
| `manifest.json` | Dataset format version, filenames, units, source filename, counts, and warnings. |
| `bh_history.csv` | Black-hole time, ID, mass, velocity, position, and representation. |
| `bh_events.csv` | Parsed merger, escape-log, net exchange, and transient-exchange records. The Unity event menu currently displays only merger and exchange-family records. |
| `science_events.csv` | Resolved exchange, transient-exchange, and merger measurements used by Science Mode. |
| `science_cluster_dynamics.csv` | Snapshot time, tidal radius, half-mass radius, centre, centre velocity, RMS speed, dispersion, and quality fields. |
| `nbody_pipeline_report.json` | Detailed diagnostics for the conversion; it is not a runtime dependency, but it is transferred with the dataset folder for traceability. |

The current Unity loader accepts dataset format version `1` with these exact units:

| Quantity | Manifest value |
|---|---|
| Time | `Myr` |
| Length | `pc` |
| Mass | `M_sun` |
| Velocity | `km/s` |

Review `nbody_pipeline_report.json`, especially:

- the scaling source and conversion factors;
- snapshot, BH, and event counts;
- grouped and transient exchange counts;
- repaired regularized frames;
- skipped exchange or merger science records;
- failure reasons and exchange-group diagnostics.

This folder is the visualization dataset; it does not contain the original HDF5 input. Keep the files together for transfer and retain the source simulation separately.

An event may remain available in the standard display even when detailed Science Mode measurements cannot be resolved. The converter records the reason rather than inventing a value.

## 6. Transfer the dataset to Quest

The current dataset root is:

```text
/sdcard/Android/data/com.kawashima.bhvisualizer/files/datasets/
```

Transfer the complete folder, including `manifest.json`, rather than copying individual CSV files into the application root.

The converter can perform the transfer automatically. For a manual ADB transfer:

```bash
adb shell mkdir -p /sdcard/Android/data/com.kawashima.bhvisualizer/files/datasets
adb push MyCluster /sdcard/Android/data/com.kawashima.bhvisualizer/files/datasets/
```

The resulting structure must be:

```text
/sdcard/Android/data/com.kawashima.bhvisualizer/files/datasets/MyCluster/manifest.json
```

For manual file transfer on **Windows**, use File Explorer; normally no extra transfer application is needed. On **Mac**, use an Android file-transfer tool such as **ADB**, **OpenMTP**, or **SideQuest**. Allow USB file access on the headset when using File Explorer or OpenMTP. Open the same `datasets` directory and copy the entire `MyCluster` folder into it.

The headset may be shown as internal shared storage in a file browser; its storage root corresponds to `/sdcard` in the ADB path above. If the device or destination folder is not accessible in a file browser, use ADB. The converter's automatic transfer requires ADB on either OS. The same file-transfer tools can copy an APK, which can then be installed inside the headset on supported OS versions; see the [installation guide](installation.md#copy-the-apk-and-install-it-inside-the-headset).

If Android has not created the package directory, launch the installed application once and try again. Restart BH-VR after adding or replacing a dataset.

## Dataset selection and fallback

At startup, the application:

1. looks under `files/datasets/` for folders containing `manifest.json`;
2. ignores folders whose paths contain `_backup_` or end in `_uploading`;
3. sorts the remaining paths and loads the first one;
4. loads the legacy root-level `bh_history.csv` layout only when no dataset folder exists; and
5. falls back to the bundled `StreamingAssets/NBODYVR_Data/` sample when neither external form exists.

The dataset dropdown is currently hidden, so keep only one active non-backup dataset folder on the headset when you need an unambiguous selection. A selected external dataset with an invalid manifest is rejected rather than silently mixed with bundled files.

## Troubleshooting

| Message or symptom | What to check |
|---|---|
| `No *_output.dat or output.dat files were found` | Confirm the selected event-log folder (`--log-dir`) and supported filenames. The snapshot folder is selected separately. |
| `snapdata.hdf5 or snapsdata.hdf5 was not found` | Supply the expected filename or use `--hdf5`. |
| `No snap_* snapshots were found` | Check the HDF5 group layout. |
| `missing datasets` or `inconsistent particle array sizes` | Compare the snapshot structure with the required fields listed above. |
| `No BH records found` | Check `kw`, `--bh-type`, masses, and the HDF5 format. |
| `Unsupported dataset format` | Regenerate the dataset with the converter shipped with this application version. |
| `Unsupported dataset units` | Use the units written by the current converter. |
| `adb is unavailable` | Install Android platform tools or copy the folder with SideQuest. |
| An `_uploading` folder remains | Move it aside with SideQuest before retrying; the converter will not overwrite an unfinished upload. |
| Science button is disabled | Confirm that `science_events.csv` or `science_cluster_dynamics.csv` contains readable records and that the manifest names them correctly. |

For the origin, definitions, and limitations of the generated measurements, see the [Science Mode guide](science-mode.md).
