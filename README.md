# NBody-VR Visualizer

NBody-VR Visualizer is a standalone Meta Quest application for exploring black-hole dynamics from NBODY6 simulations in an interactive 3D environment.

It displays black-hole trajectories, masses, velocities, binaries, exchanges, mergers, and escapes along a simulation timeline. A bundled example dataset is included for immediate use, while your own NBODY6 output can be converted with one Python script and transferred to the headset.

## Basic VR use

The main controls are:

| Control | Function |
|---|---|
| **Play / Pause** | Start or stop time evolution. |
| **Time slider** | Move directly to another simulation time. |
| **Event list** | Jump to a detected exchange, merger, or escape event. |
| **BH size** | Change the visual scale of black holes. This does not change their physical masses. |
| **Speed** | Change playback speed. |
| **Position / Follow** | Select the display frame or follow a black hole or binary centre of mass. |
| **Science** | Show the scientific information panel above the main controls. |
| **Help** | Show the in-app control guide. |

Point at a UI element with the controller ray and press the trigger to operate it.

## Quick Play

Quick Play uses the example dataset bundled with the APK. No Python conversion or manual data transfer is required.

### Requirements

- Meta Quest 2 or Quest 3
- A USB cable
- [SideQuest](https://sidequestvr.com/) or Android Debug Bridge (`adb`)

### Install and launch

1. Download the latest APK from [GitHub Releases](https://github.com/gkawa08/nbody-vr-visualizer-unity/releases).
2. Install it with SideQuest, or run:

   ```bash
   adb install NBodyVis_VR.apk
   ```

3. On the headset, open **App Library → Unknown Sources** and launch NBody-VR Visualizer.
4. Press **Play**, move the time slider, or select an item from the event list.

The bundled sample contains the trajectory and event files required by both the standard display and Science Mode.

> If custom CSV files already exist on the headset, they take priority over the bundled sample. Remove those files from the app's data directory to return to Quick Play mode.

## Use your own NBODY6 data

The normal data flow remains intentionally simple:

```text
NBODY6 HDF5 + output logs → nbody_bh_data.py → four CSV files → Meta Quest
```

`extract_exchange_physics.py` is not required for normal use. Its analysis logic has been incorporated into the main conversion path where needed.

### 1. Prepare the input

Place the following in one input directory:

- `snapdata.hdf5`
- the corresponding NBODY6 log files, normally named `*_output.dat`

The converter requires Python 3, NumPy, and h5py:

```bash
python -m pip install numpy h5py
```

### 2. Convert the data

Run the project converter:

```bash
python nbody_bh_data.py \
  --data-dir /path/to/NBODY6/output \
  --output-dir /path/to/generated/csv
```

It generates:

| File | Purpose |
|---|---|
| `bh_history.csv` | Black-hole positions, velocities, masses, and time evolution. |
| `bh_events.csv` | Event list used by the standard UI. |
| `science_exchange_events.csv` | Resolved exchange participants and fixed pre/post measurements. |
| `science_cluster_dynamics.csv` | Snapshot-level cluster quantities used by Science Mode. |
| `nbody_conversion_report.json` | Diagnostic report for checking the conversion; it is not copied to the headset. |

Inspect `nbody_conversion_report.json` before transfer. Warnings identify missing snapshots, unresolved participants, composite-particle reconstruction, and event records that could not receive detailed measurements.

### 3. Copy the CSV files to the headset

The destination is:

```text
/sdcard/Android/data/com.kawashima.bhvisualizer/files/
```

With SideQuest, open the headset file manager, navigate to that directory, and copy the four CSV files listed above.

With `adb`:

```bash
adb shell mkdir -p /sdcard/Android/data/com.kawashima.bhvisualizer/files/
adb push bh_history.csv /sdcard/Android/data/com.kawashima.bhvisualizer/files/
adb push bh_events.csv /sdcard/Android/data/com.kawashima.bhvisualizer/files/
adb push science_exchange_events.csv /sdcard/Android/data/com.kawashima.bhvisualizer/files/
adb push science_cluster_dynamics.csv /sdcard/Android/data/com.kawashima.bhvisualizer/files/
```

If Android has not yet created the application directory, launch the installed app once and try again. Restart the app after replacing the files.

The application first checks this external directory. If a file is absent, it falls back to the sample copy bundled in `StreamingAssets`. For a consistent dataset, replace all four files together.

## Science Mode at a glance

Science Mode adds an information panel above the main UI. It is designed primarily for examining how an exchange encounter changes a binary and redistributes energy and recoil.

The display is divided into three kinds of information:

1. **Current playback** — quantities associated with the current animation time.
2. **Fixed PRE and POST snapshots** — measurements from the nearest resolved snapshots before and after the selected event.
3. **Event comparison** — orbital, binding-energy, recoil, and escape diagnostics associated with that event.

PRE and POST are deliberately shown together. They are fixed measurements bracketing the event, not two states that switch when the playback cursor crosses the event time.

Masses are displayed as solar masses. World-space labels use `M☉`. TextMesh Pro panels use `M<sub>⊙</sub>` when the selected font asset contains the solar symbol; otherwise the fallback is `M<sub>sun</sub>`.

## View and follow modes

The selected view frame changes displayed positions and world-space velocity labels, but it does not change the scientific event measurements reported in the cluster centre-of-mass frame.

| Mode | Meaning |
|---|---|
| **Raw** | Original simulation coordinates. |
| **Cluster CoM** | Cluster centre-of-mass motion is removed. |
| **Follow BH** | Positions and velocities are shown relative to a selected black hole. |
| **Follow binary CoM** | The view follows the selected binary centre of mass. |

For an exchange, the pre-event and post-event binaries can have different members. Fixed PRE/POST measurements therefore remain separate even when the camera follows one selected pair.

## Appendix A: Science Mode details

### Event and snapshot times

The panel header shows the selected event time parsed from the NBODY6 event log. PRE and POST snapshot times are shown separately because an HDF5 snapshot usually does not exist at the exact event time.

- **Event: t** — selected log-event time converted to Myr.
- **Current time** — current animation time.
- **PRE snapshot / POST snapshot** — HDF5 times used for fixed measurements.

An event can therefore occur between two stored snapshots. The event time must not be replaced by the time of either neighbouring snapshot.

### Current playback quantities

For each snapshot, the cluster centre-of-mass velocity is calculated from physical particles within the adopted tidal boundary:

```math
\boldsymbol{V}_{\mathrm{cl}}
=
\frac{\sum_i m_i\boldsymbol{v}_i}{\sum_i m_i}.
```

The number-weighted three-dimensional RMS speed and corresponding one-dimensional dispersion are

```math
v_{\mathrm{rms,3D}}
=
\sqrt{\frac{1}{N}\sum_i
\left|\boldsymbol{v}_i-\boldsymbol{V}_{\mathrm{cl}}\right|^2},
\qquad
\sigma_{\mathrm{1D}}=\frac{v_{\mathrm{rms,3D}}}{\sqrt{3}}.
```

These are cluster-wide quantities, not a dispersion measured from black holes alone.

For a binary with component masses $m_1$ and $m_2$,

```math
\boldsymbol{V}_{\mathrm{bin}}
=
\frac{m_1\boldsymbol{v}_1+m_2\boldsymbol{v}_2}{m_1+m_2},
\qquad
v_{\mathrm{cl,bin}}
=
\left|\boldsymbol{V}_{\mathrm{bin}}-\boldsymbol{V}_{\mathrm{cl}}\right|.
```

The panel also reports $v/\sigma$. This measures how unusual a speed is relative to cluster random motion; it is not by itself an escape criterion.

### Fixed PRE and POST measurements

For a resolved exchange of the form

```math
(A+B)+C \longrightarrow (A+C)+B,
```

Science Mode displays both:

- **PRE:** the original binary $A+B$ and incoming object $C$.
- **POST:** the resulting binary $A+C$ and outgoing object $B$.

Each side includes participant IDs, masses, cluster-frame speeds, $v/\sigma$, an approximate local escape speed, and $v/v_{\mathrm{esc}}$ where available.

The local escape-speed estimate is

```math
v_{\mathrm{esc}}(r)
=
\sqrt{2G\sum_i\frac{m_i}{\max(r,r_i)}}.
```

The target object is excluded from the sum. The cluster is treated as spherical and truncated at the adopted tidal radius. This is a useful local diagnostic, but it is not the complete Jacobi escape condition in an external tidal field.

### Binary orbit and binding energy

When the NBODY6 log provides the binary semimajor axis $a$ and eccentricity $e$, the binding-energy magnitude is

```math
|E_b|=\frac{Gm_1m_2}{2a},
```

and the reported change is

```math
\Delta E_b=|E_{b,\mathrm{after}}|-|E_{b,\mathrm{before}}|.
```

- $\Delta E_b>0$: the binary hardened and released binding energy.
- $\Delta E_b<0$: the binary softened.

The logged $a$ is distinct from the visible distance between HDF5 coordinates. Regularized binary components can be stored at a shared centre-of-mass position, so a displayed separation of zero does not imply physical contact.

### Simple recoil model

This is a classical three-body recoil estimate, not gravitational-wave merger recoil. Let

```math
M_b=m'_1+m'_2,
\qquad
M_{123}=M_b+m'_3,
```

where $m'_3$ is the outgoing single mass. If the released binding energy becomes translational kinetic energy in the final triple centre-of-mass frame, momentum conservation gives

```math
v_{b,\mathrm{pred}}
=
\sqrt{\frac{2\Delta E_b\,m'_3}{M_bM_{123}}},
\qquad
v_{3,\mathrm{pred}}
=
\sqrt{\frac{2\Delta E_b\,M_b}{m'_3M_{123}}}.
```

The panel compares these predictions with snapshot measurements and reports measured/predicted ratios and $\Delta K_{\mathrm{measured}}/\Delta E_b$. Values need not equal one because the model omits incoming kinetic energy, the cluster potential, transient multiples, and snapshot-time offsets. If $\Delta E_b\leq0$, the prediction is shown as unavailable.

### NBODY6 chain log and prompt escape

When present, `CHAIN BINARY VCM` and `CHAIN ESCAPE VF` are values read directly from the NBODY6 chain output. They can differ from HDF5-derived measurements because their times and reference frames are not necessarily identical.

The prompt-escape field searches for a linked `BH ESCAPE` record within 100 N-body time units after the encounter. In the bundled sample this corresponds to 22.7 Myr. **Not linked** means that the prompt criterion was not satisfied; it does not prove that the object never escaped later.

### Pair markers in the 3D view

The PRE and POST pairs are both shown for comparison:

- magenta: pre-event binary
- cyan: post-event binary

Their visible separation is a snapshot-coordinate separation, not the logged orbital semimajor axis.

### KS/CHAIN composite particles

NBODY6 HDF5 snapshots may contain physical particles in rows `0:N` and regularization pseudo-particles in later rows. The converter separates these regions and excludes pseudo-particles from ordinary physical-particle statistics.

Some physical members can appear as zero-mass placeholders while a KS/CHAIN centre-of-mass row stores their combined mass. When the structure is identifiable, `nbody_bh_data.py` reconstructs proxy members at the composite centre-of-mass position so that the encounter can still be followed. Such reconstruction is recorded in `nbody_conversion_report.json`.

A composite representation is not interpreted as a merger. A merger is added only from an explicit NBODY6 `COAL` record.

### Missing measurements

Detailed Science Mode information can remain unavailable when:

- a participant is missing from the nearest suitable snapshot;
- a composite particle cannot be reconstructed unambiguously;
- the event log lacks orbital information;
- the event cannot be matched safely across PRE and POST data.

In that case the standard trajectory and event display can still work, but orbital elements, binding-energy changes, escape diagnostics, or recoil comparisons may be absent. The converter reports the reason instead of silently inventing a measurement.

## Appendix B: Data-pipeline details

`nbody_bh_data.py` performs the full application conversion workflow:

1. Reads NBODY6 scaling and time information.
2. Separates physical rows `0:N` from later KS/CHAIN pseudo-particles.
3. Extracts black-hole histories from HDF5 snapshots.
4. Parses exchange, coalescence, and escape records from output logs.
5. Groups simultaneous exchange log lines into one net encounter where appropriate.
6. Reconstructs identifiable composite members without treating them as mergers.
7. Resolves fixed PRE and POST participants and computes cluster diagnostics.
8. Writes the four runtime CSV files and a diagnostic JSON report.

The logic is based on data structure and event relationships, not on a hard-coded time such as 1087.784 Myr. Different NBODY6 runs can still require review when their log format, scaling metadata, or regularization output differs from the supported forms; consult the diagnostic report whenever warnings appear.

## Appendix C: Unity development

The project uses Unity `6000.0.57f1`.

For editor development:

1. Clone the repository.
2. Open the project in the matching Unity version.
3. Place a bundled fallback dataset in `Assets/StreamingAssets/` if required.
4. Build for Android and install the APK on the headset.

Files in `Assets/StreamingAssets/` are packaged into the APK. Files copied later to

```text
/sdcard/Android/data/com.kawashima.bhvisualizer/files/
```

override those bundled files at runtime. Changing C# scripts requires a new APK build; replacing compatible CSV data does not.

## Scientific scope and limitations

- Event times come from parsed NBODY6 logs; trajectory measurements come from discrete HDF5 snapshots.
- Nearest snapshots can be separated from the event by a finite interval.
- Escape speed is a spherical, cluster-self-gravity approximation.
- A momentary $v/v_{\mathrm{esc}}>1$ is not identical to confirmed long-term escape.
- The recoil model is an idealized energy-and-momentum estimate.
- KS/CHAIN reconstruction is conservative and can leave events unresolved.
- Visual black-hole sizes and pair-line distances are display aids, not literal horizon or binary-orbit scales.

## References

- Aarseth, S. J. (1999), “From NBODY1 to NBODY6,” *Publications of the Astronomical Society of the Pacific*, 111, 1333. [https://doi.org/10.1086/316455](https://doi.org/10.1086/316455)
- Heggie, D. & Hut, P. (2003), *The Gravitational Million-Body Problem*. [https://doi.org/10.1017/CBO9781139164535](https://doi.org/10.1017/CBO9781139164535)
- Spitzer, L. (1987), *Dynamical Evolution of Globular Clusters*. [https://doi.org/10.1515/9781400858736](https://doi.org/10.1515/9781400858736)
- Wang, L. et al. (2015), “NBODY6++GPU: Ready for the gravitational million-body problem,” *Monthly Notices of the Royal Astronomical Society*, 450, 4070. [https://doi.org/10.1093/mnras/stv817](https://doi.org/10.1093/mnras/stv817)
