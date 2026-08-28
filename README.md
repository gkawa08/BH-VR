# NBody-VR Visualizer

NBody-VR Visualizer is a standalone VR application for exploring black-hole dynamics in NBODY6 simulations on Meta Quest 2 and Meta Quest 3. It displays black-hole trajectories, highlights `EXCHANGE` and `MERGE` events, and provides a Science Mode for inspecting the dynamics and energetics of binary-single interactions.

## Key features

- Interactive 3D playback of black-hole positions and velocities
- Event selection for `EXCHANGE` and `MERGE` events
- Raw, cluster center-of-mass, followed-BH, and binary center-of-mass views
- Optional velocity arrows and participant labels
- Science Mode with fixed pre-event/post-event comparison
- Standalone playback on a Quest headset without a continuous PC connection
- A single Python pipeline that converts NBODY6 HDF5 and text output into lightweight CSV files
- Direct CSV transfer to the headset through ADB or SideQuest

## Science Mode

Select an event from the event list and turn **Science: ON**. The Science panel appears above the main UI. For an `EXCHANGE` event, the panel separates live playback information from fixed measurements made on the resolved HDF5 snapshots immediately before and after the encounter.

The fixed comparison can be represented as

$$
(A+B)+C \longrightarrow (A+C)+B,
$$

where $A+B$ is the pre-event binary, $C$ is the incoming object, $A+C$ is the post-event binary, and $B$ is the outgoing object.

### Event header

The top of the panel shows:

```text
SCIENCE MODE — EXCHANGE #...
Event: t = ... Myr
Current playback: t = ... Myr
View frame: ...; reported speeds: cluster CoM
```

- **Event** is the event time parsed from the NBODY6 text output.
- **Current playback** is the time of the currently displayed trajectory frame.
- **View frame** describes the spatial/visual frame selected in VR.
- Quantities marked $v_{\rm cl}$ are always reported relative to the cluster center-of-mass velocity, independently of the selected visual frame.

The event time does not normally coincide with an HDF5 snapshot. Science Mode therefore displays the event time and the two measured snapshot times separately.

### Current playback

`CURRENT PLAYBACK` follows the displayed trajectory time. It reports the nearest available cluster-dynamics record and the current participant kinematics.

The cluster center-of-mass velocity is mass weighted:

$$
\mathbf{V}_{\rm cl}=\frac{\sum_i m_i\mathbf{v}_i}{\sum_i m_i}.
$$

The displayed three-dimensional RMS speed and one-dimensional dispersion are

$$
v_{\rm rms,3D}=\sqrt{\frac{1}{N}\sum_i
\left|\mathbf{v}_i-\mathbf{V}_{\rm cl}\right|^2},
\qquad
\sigma_{\rm 1D}=\frac{v_{\rm rms,3D}}{\sqrt{3}}.
$$

The sum is evaluated over valid physical particles inside the tidal-radius selection. Particle velocities are number weighted after subtracting the mass-weighted cluster center-of-mass velocity.

For a binary with component masses $m_1$ and $m_2$,

$$
\mathbf{V}_{\rm bin}=\frac{m_1\mathbf{v}_1+m_2\mathbf{v}_2}{m_1+m_2},
\qquad
v_{\rm cl,bin}=\left|\mathbf{V}_{\rm bin}-\mathbf{V}_{\rm cl}\right|.
$$

Participant labels show the BH ID, mass, cluster-frame speed, and $v/\sigma$. Masses use M<sub>⊙</sub> when the active TextMeshPro font contains the solar-symbol glyph; otherwise the panel falls back to M<sub>sun</sub>. World-space labels use `M☉`.

### Fixed pre-event and post-event snapshots

The magenta `PRE-EVENT SNAPSHOT — FIXED` and cyan `POST-EVENT SNAPSHOT — FIXED` sections do not switch as playback crosses the event time. They remain visible together so that the exchange can be compared directly.

Each section reports:

- Binary component IDs and incoming/outgoing ID
- Component masses
- HDF5 snapshot time
- Cluster one-dimensional velocity dispersion
- Binary center-of-mass speed relative to the cluster
- Incoming or outgoing single-object speed relative to the cluster
- $v/\sigma$
- $v/v_{\rm esc}$

The local escape speed is a spherical approximation evaluated from the cluster particles:

$$
v_{\rm esc}(r)=
\sqrt{2G\sum_i\frac{m_i}{\max(r,r_i)}}.
$$

The target binary or single object is excluded from the potential sum. This is a tidal-radius-truncated, spherically averaged estimate; it is not a full Jacobi-energy escape criterion in an external Galactic tidal field. A red $v/v_{\rm esc}>1$ indicates that the instantaneous speed exceeds this estimate, not that permanent escape has already been demonstrated.

### Event comparison

`EVENT COMPARISON` displays the NBODY6 orbital diagnostics and derived binary binding energies:

$$
|E_b|=\frac{Gm_1m_2}{2a},
\qquad
\Delta E_b=|E_{b,\rm after}|-|E_{b,\rm before}|.
$$

- $a$: binary semimajor axis in AU
- $e$: orbital eccentricity
- $|E_b|$: magnitude of the binary binding energy
- $\Delta E_b>0$: the binary hardened and released binding energy
- $\Delta E_b\leq0$: the simple recoil prediction is not evaluated

### Simple recoil model

When $\Delta E_b>0$, Science Mode compares the measured recoil with an idealized three-body energy-partition model. For final binary mass $M_b$, outgoing mass $m_3$, and $M_{123}=M_b+m_3$,

$$
v_{b,\rm pred}=\sqrt{\frac{2\Delta E_bm_3}{M_bM_{123}}},
\qquad
v_{3,\rm pred}=\sqrt{\frac{2\Delta E_bM_b}{m_3M_{123}}}.
$$

The panel reports predicted and measured velocities in the final three-body center-of-mass frame, their ratios, and

$$
\frac{\Delta K_{\rm measured}}{\Delta E_b}.
$$

This model assumes that the released binding energy is converted into translational recoil of the final binary and outgoing single. It neglects the incoming kinetic energy, the cluster potential during the encounter, unresolved intermediate multiples, and the finite snapshot interval. Agreement with unity is therefore not required.

### NBODY6 chain diagnostics and prompt escape

When present in the NBODY6 text output, the panel also displays:

- `CHAIN BINARY VCM`
- `CHAIN ESCAPE VF`

These are direct log values and can refer to a different chain frame or epoch from the HDF5 measurements.

`Prompt cluster escape` links a later `BH ESCAPE` record to the selected exchange when it occurs within 100 N-body time units. For the dataset currently supplied with this project, this corresponds to 22.7 Myr. `Not linked` means that no escape record met this prompt criterion; it does not prove that the object never escaped later.

### Pair markers in the 3D view

Science Mode can draw both binaries simultaneously:

- Magenta: pre-event binary
- Cyan: post-event binary

The displayed component separation is the separation in the visualization snapshot. It is not the orbital semimajor axis $a$. During NBODY6 regularization, binary components may be represented at a common center-of-mass coordinate, so zero displayed separation does not imply physical contact.

### KS/CHAIN composite representation

The pipeline reads physical HDF5 particles from the `0:N` slice and excludes internal pseudo-particles stored beyond `N`. During a regularized encounter, NBODY6 may temporarily store one zero-mass component together with another record carrying the combined binary mass.

When this pattern is identified consistently with an exchange diagnostic, the pipeline reconstructs two component proxies at the stored binary center of mass. Science Mode then displays:

```text
KS/CHAIN BINARY COMPOSITE
IDs ... are displayed at one CoM proxy
This is not classified as a MERGE
```

The component masses and IDs remain available, but separate component positions and velocities cannot be recovered from that snapshot. A `MERGE` is created only from a parsed NBODY6 `COAL` record.

### Missing detailed measurements

If no complete resolved pre/post HDF5 frames are available inside the configured search window, the panel falls back to complete frames reconstructed from `bh_history.csv`. The fallback can show participant masses, velocities, pair separation, and relative velocity, but orbital $a/e$, binding energy, and local escape speed remain unavailable.

The generated `nbody_pipeline_report.json` lists every event omitted from `science_exchange_events.csv` and the corresponding reason.

## View frames

The position/follow control cycles through the available display frames:

- **Raw**: simulation coordinates and velocities
- **Cluster CoM**: cluster center-of-mass position and velocity removed
- **Follow BH**: view centered on a selected BH
- **Follow PRE binary CoM**: view centered on the pre-event binary
- **Follow POST binary CoM**: view centered on the post-event binary

Changing the view frame changes world-space motion and label velocities. Fixed Science Mode values marked $v_{\rm cl}$ remain in the cluster center-of-mass frame.

## Requirements

- Meta Quest 2 or Meta Quest 3
- SideQuest or Android platform tools (`adb`)
- Python 3
- `numpy`
- `h5py`
- NBODY6 `snapdata.hdf5` or `snapsdata.hdf5`
- One or more `*_output.dat` files

## Installation

1. Download the latest APK from [Releases](../../releases).
2. Connect the Quest headset by USB.
3. Install the APK with SideQuest or ADB:

```bash
adb install NBodyVR_vX.X.apk
```

4. Launch the application once from **Unknown Sources**, then close it. This creates the application data directory.

## Data preparation

Run the included unified pipeline:

```bash
python nbody_bh_data.py \
  --data-dir /path/to/NBODY6/output \
  --output-dir /path/to/generated/csv
```

The pipeline reads the physical-particle portion of every HDF5 snapshot, parses event and scaling information from the text output, groups simultaneous EXCHANGE diagnostics into net encounters, repairs recognized regularized composite representations, and writes:

| File | Purpose |
| --- | --- |
| `bh_history.csv` | BH positions, velocities, masses, IDs, and representation metadata |
| `bh_events.csv` | Grouped EXCHANGE events, COAL-derived MERGE events, and BH ESCAPE records |
| `science_exchange_events.csv` | Fixed pre/post EXCHANGE measurements and derived quantities |
| `science_cluster_dynamics.csv` | Per-snapshot cluster center, velocity, dispersion, and tidal radius |
| `nbody_pipeline_report.json` | Scaling, row counts, repair counts, and skipped-science diagnostics |

Scaling is read from the NBODY6 `PHYSICAL SCALING` line when available. It can be overridden with `--mass-conv`, `--length-conv`, `--velocity-conv`, and `--time-conv`.

Use `--no-transfer` to generate files without sending them to a headset. Run `python nbody_bh_data.py --help` for all options.

## Transfer data to Meta Quest

The application first checks `Application.persistentDataPath`, corresponding to:

```text
/sdcard/Android/data/com.kawashima.bhvisualizer/files/
```

Copy the four CSV files to that directory with SideQuest, or use ADB:

```bash
adb push bh_history.csv /sdcard/Android/data/com.kawashima.bhvisualizer/files/
adb push bh_events.csv /sdcard/Android/data/com.kawashima.bhvisualizer/files/
adb push science_exchange_events.csv /sdcard/Android/data/com.kawashima.bhvisualizer/files/
adb push science_cluster_dynamics.csv /sdcard/Android/data/com.kawashima.bhvisualizer/files/
```

Close the application before replacing the files, then restart it. Reinstalling the APK is unnecessary when only the simulation data changes.

`nbody_pipeline_report.json` is for offline diagnosis and does not need to be copied to the headset.

## Unity development

The repository contains the Unity project source. Open it in Unity `6000.0.57f1`, select Android as the active build platform, and keep the application package name synchronized with the ADB destination.

Files placed in `Assets/StreamingAssets/` provide built-in fallback data. On the headset, files in the application data directory take priority over embedded StreamingAssets.

Changing C# scripts, UI layout, scenes, shaders, or interaction logic requires rebuilding and reinstalling the APK. Replacing CSV data does not.

## Scientific scope and limitations

- Event times come from NBODY6 text diagnostics; trajectory and cluster values come from discrete HDF5 snapshots.
- Pre/post event values are sampled, not time interpolated.
- Multiple EXCHANGE diagnostics at the same encounter time can describe intermediate configurations. The pipeline selects the closest resolved pre-event and post-event pairs and stores one net exchange.
- Spherical local escape speed is an approximation and does not include the complete external tidal escape condition.
- A regularized center-of-mass proxy preserves recoverable IDs and masses but cannot reconstruct unresolved internal phase space.
- The simple recoil model is diagnostic rather than a complete scattering calculation.

## References

- Aarseth, S. J. (1999), “From NBODY1 to NBODY6,” *Publications of the Astronomical Society of the Pacific*, 111, 1333. [https://doi.org/10.1086/316455](https://doi.org/10.1086/316455)
- Heggie, D. & Hut, P. (2003), *The Gravitational Million-Body Problem*. [https://doi.org/10.1017/CBO9781139164535](https://doi.org/10.1017/CBO9781139164535)
- Spitzer, L. (1987), *Dynamical Evolution of Globular Clusters*. [https://doi.org/10.1515/9781400858736](https://doi.org/10.1515/9781400858736)
- Wang, L. et al. (2015), “NBODY6++GPU: ready for the gravitational million-body problem,” *Monthly Notices of the Royal Astronomical Society*, 450, 4070. [https://doi.org/10.1093/mnras/stv817](https://doi.org/10.1093/mnras/stv817)
