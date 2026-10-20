import argparse
import csv
import glob
import json
import math
import os
import re
import subprocess
import sys
import shutil
import tempfile
from datetime import datetime, timezone
from collections import defaultdict
from multiprocessing import Pool


def check_requirements():
    missing = []
    try:
        import numpy
    except ImportError:
        missing.append("numpy")
    try:
        import h5py
    except ImportError:
        missing.append("h5py")
    if not missing:
        return
    print(f"Required modules are missing: {', '.join(missing)}")
    answer = input("Install them now? (y/n) [default: y]: ") or "y"
    if answer.lower() != "y":
        print(f"Run: {sys.executable} -m pip install {' '.join(missing)}")
        sys.exit(1)
    subprocess.check_call([sys.executable, "-m", "pip", "install", *missing])


check_requirements()

import h5py
import numpy as np


FLOAT_PATTERN = r"[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[EeDd][+-]?\d+)?"
TIME_PATTERN = re.compile(rf"\bTIME\b\s*(?:=\s*)?({FLOAT_PATTERN})", re.IGNORECASE)
SCALING_PATTERN = re.compile(
    rf"PHYSICAL SCALING:\s+R\*\s*=\s*({FLOAT_PATTERN})\s+"
    rf"M\*\s*=\s*({FLOAT_PATTERN})\s+"
    rf"V\*\s*=\s*({FLOAT_PATTERN})\s+"
    rf"T\*\s*=\s*({FLOAT_PATTERN})"
)
COAL_PREFIX_PATTERN = re.compile(r"\bCOAL\s+IQ\s*=\s*[+-]?\d+\s+NAME\s*=", re.IGNORECASE)
G_PC_KMS2_PER_MSUN = 4.30091e-3
PC_TO_AU = 206264.80624709636
HISTORY_COLUMNS = [
    "time_myr",
    "id",
    "mass",
    "vx",
    "vy",
    "vz",
    "x",
    "y",
    "z",
    "representation",
    "source_id",
]
EVENT_COLUMNS = [
    "time_myr",
    "event_list",
    "id1_list",
    "id2_list",
    "id3_list",
    "id4_list",
    "temporary_ids",
]
CLUSTER_COLUMNS = [
    "snapshot_key",
    "time_nbody",
    "time_myr",
    "rtide_pc",
    "half_mass_radius_pc",
    "com_x_pc",
    "com_y_pc",
    "com_z_pc",
    "vcom_x_kms",
    "vcom_y_kms",
    "vcom_z_kms",
    "vrms_3d_number_kms",
    "sigma_1d_number_kms",
    "analysis_quality_ok",
    "analysis_quality_reason",
]
EXCHANGE_COLUMNS = [
    "exchange_index",
    "event_type",
    "event_time_nbody",
    "event_time_myr",
    "diagnostic_start_time_myr",
    "diagnostic_end_time_myr",
    "raw_diagnostic_count",
    "raw_transitions",
    "temporary_ids",
    "before_snapshot_key",
    "after_snapshot_key",
    "before_snapshot_time_myr",
    "after_snapshot_time_myr",
    "before_snapshot_offset_myr",
    "after_snapshot_offset_myr",
    "before_pair",
    "after_pair",
    "incoming_id",
    "outgoing_id",
    "sigma_1d_before_kms",
    "sigma_1d_after_kms",
    "binary_speed_before_kms",
    "binary_speed_after_kms",
    "incoming_speed_before_kms",
    "outgoing_speed_after_kms",
    "binary_single_relative_speed_before_kms",
    "binary_single_relative_speed_after_kms",
    "binary_vesc_before_kms",
    "binary_vesc_after_kms",
    "incoming_vesc_before_kms",
    "outgoing_vesc_after_kms",
    "binary_v_over_sigma_after",
    "outgoing_v_over_sigma_after",
    "binary_v_over_vesc_after",
    "outgoing_v_over_vesc_after",
    "e_before_log",
    "e_after_log",
    "a_before_au_log",
    "a_after_au_log",
    "pericenter_before_au",
    "pericenter_after_au",
    "before_m1_msun",
    "before_m2_msun",
    "incoming_mass_msun",
    "after_m1_msun",
    "after_m2_msun",
    "outgoing_mass_msun",
    "binding_energy_before_msun_kms2",
    "binding_energy_after_msun_kms2",
    "binding_energy_release_msun_kms2",
    "binding_energy_release_positive",
    "binding_energy_log_before_msun_kms2",
    "binding_energy_log_after_msun_kms2",
    "binding_energy_primary_source",
    "binding_energy_fractional_change",
    "interaction_com_radius_before_pc",
    "interaction_com_radius_after_pc",
    "cluster_half_mass_radius_before_pc",
    "cluster_half_mass_radius_after_pc",
    "interaction_com_r_over_rh_before",
    "interaction_com_r_over_rh_after",
    "binary_r_over_rtide_before",
    "binary_r_over_rtide_after",
    "single_r_over_rtide_before",
    "single_r_over_rtide_after",
    "binary_recoil_after_triple_com_kms",
    "outgoing_speed_after_triple_com_kms",
    "predicted_binary_recoil_kms",
    "predicted_outgoing_recoil_kms",
    "measured_over_predicted_binary",
    "measured_over_predicted_outgoing",
    "kinetic_change_over_binding_release",
    "chain_escape_vf_log_kms",
    "chain_binary_vcm_log_kms",
    "outgoing_prompt_cluster_escape",
    "binary_prompt_cluster_escape",
    "outgoing_first_cluster_escape_lag_myr",
    "binary_first_exact_escape_lag_myr",
    "outgoing_escape_recorded",
    "binary_exact_escape_recorded",
    "outgoing_first_escape_lag_myr",
    "binary_first_escape_lag_myr",
    "escape_log_search_end_myr",
    "coal_id1",
    "coal_id2",
    "merge_survivor_id",
    "merge_consumed_id",
    "merge_identification_status",
    "merge_pre_mass1_msun",
    "merge_pre_mass2_msun",
    "merge_pre_total_mass_msun",
    "merge_remnant_mass_msun",
    "merge_mass_change_msun",
    "merge_mass_change_fraction",
    "merge_pre_com_speed_kms",
    "merge_remnant_speed_kms",
    "merge_velocity_change_kms",
    "merge_remnant_vesc_kms",
    "merge_remnant_v_over_sigma",
    "merge_remnant_v_over_vesc",
    "merge_radius_before_pc",
    "merge_radius_after_pc",
    "merge_r_over_rh_before",
    "merge_r_over_rh_after",
    "merge_remnant_escape_recorded",
    "merge_remnant_first_escape_lag_myr",
]


def ffloat(value):
    return float(str(value).replace("D", "E").replace("d", "e"))


def finite_number(value):
    try:
        number = float(value)
    except (TypeError, ValueError):
        return None
    return number if math.isfinite(number) else None


def natural_key(path):
    return [int(item) if item.isdigit() else item.lower() for item in re.split(r"(\d+)", path)]


def snapshot_number(key):
    match = re.search(r"(\d+)$", key)
    return int(match.group(1)) if match else -1


def parse_time_line(line):
    match = TIME_PATTERN.search(line)
    if match is None:
        return None
    try:
        return ffloat(match.group(1))
    except ValueError:
        return None


def parse_coal_line(line):
    match = COAL_PREFIX_PATTERN.search(line)
    if match is None:
        return None
    fields = line[match.end():match.end() + 12]
    if len(fields) == 12:
        try:
            return int(fields[:6].strip()), int(fields[6:].strip())
        except ValueError:
            pass
    fallback = re.match(r"\s*([+-]?\d+)\s+([+-]?\d+)", line[match.end():])
    if fallback is None:
        return None
    return int(fallback.group(1)), int(fallback.group(2))


def parse_exchange_line(line):
    try:
        ids = tuple(int(line[start:end].strip()) for start, end in [(45, 51), (51, 57), (57, 63), (63, 69)])
    except ValueError:
        return None
    values = []
    for item in line[69:].split()[:6]:
        try:
            values.append(ffloat(item))
        except ValueError:
            values.append(None)
    while len(values) < 6:
        values.append(None)
    return {
        "id1": ids[0],
        "id2": ids[1],
        "id3": ids[2],
        "id4": ids[3],
        "e_before": values[0],
        "e_after": values[1],
        "a_before_nbody": values[2],
        "a_after_nbody": values[3],
        "eb_before_nbody": values[4],
        "eb_after_nbody": values[5],
    }


def parse_scaling(output_files):
    for path in output_files:
        with open(path, "r", errors="replace") as stream:
            for line in stream:
                match = SCALING_PATTERN.search(line)
                if match:
                    rstar, mstar, vstar, tstar = map(ffloat, match.groups())
                    return {
                        "length": rstar,
                        "mass": mstar,
                        "velocity": vstar,
                        "time": tstar,
                        "source": os.path.basename(path),
                    }
    return {
        "length": 3.10,
        "mass": 127918.2,
        "velocity": 13.325,
        "time": 0.227,
        "source": "defaults",
    }


def parse_chain_binary(line):
    numbers = re.findall(FLOAT_PATTERN, line.split("PB", 1)[-1])
    if len(numbers) < 5:
        return None
    try:
        return ffloat(numbers[4])
    except ValueError:
        return None


def parse_chain_escape(line):
    values = line.split()
    if not values:
        return None
    try:
        return ffloat(values[-1])
    except ValueError:
        return None


def find_output_files(log_dir):
    output_files = sorted(
        (
            path
            for path in glob.glob(os.path.join(log_dir, "*_output.dat"))
            if os.path.isfile(path)
        ),
        key=natural_key,
    )
    if output_files:
        return output_files
    fallback = os.path.join(log_dir, "output.dat")
    if os.path.isfile(fallback):
        return [fallback]
    raise FileNotFoundError(
        f"No *_output.dat or output.dat files were found in {log_dir}"
    )


def parse_logs(output_files, bh_type, time_conv):
    raw_events = []
    cluster_log = []
    coal_lines_found = 0
    coal_lines_parsed = 0
    sequence = 0
    maximum_time_nbody = 0.0
    for path in output_files:
        current_time = 0.0
        expect_summary = False
        last_exchange = None
        with open(path, "r", errors="replace") as stream:
            for line in stream:
                parsed_time = parse_time_line(line)
                if parsed_time is not None:
                    current_time = parsed_time
                    maximum_time_nbody = max(maximum_time_nbody, current_time)
                if "<R>  RTIDE" in line and "VRMS" in line:
                    expect_summary = True
                    continue
                if expect_summary and line.lstrip().startswith("#1"):
                    values = line.split()[1:]
                    if len(values) >= 2:
                        try:
                            cluster_log.append(
                                {
                                    "time_nbody": current_time,
                                    "time_myr": current_time * time_conv,
                                    "rtide_pc": ffloat(values[1]),
                                }
                            )
                        except ValueError:
                            pass
                    expect_summary = False
                if line[1:9] == "EXCHANGE":
                    parsed = parse_exchange_line(line)
                    if parsed is not None:
                        parsed.update(
                            {
                                "time_nbody": current_time,
                                "time_myr": current_time * time_conv,
                                "event_list": "EXCHANGE",
                                "source": os.path.basename(path),
                                "sequence": sequence,
                                "chain_binary_vcm": None,
                                "chain_escape_vf": None,
                            }
                        )
                        sequence += 1
                        raw_events.append(parsed)
                        last_exchange = parsed
                    continue
                if "CHAIN BINARY" in line and "VCM" in line and last_exchange is not None:
                    value = parse_chain_binary(line)
                    if value is not None:
                        last_exchange["chain_binary_vcm"] = value
                if "CHAIN ESCAPE:" in line and last_exchange is not None:
                    value = parse_chain_escape(line)
                    if value is not None:
                        last_exchange["chain_escape_vf"] = value
                if (
                    len(line) >= 57
                    and line[8:14] == "ESCAPE"
                    and line[52:54].strip() == str(bh_type)
                    and line[55:57].strip() == str(bh_type)
                ):
                    try:
                        id1 = int(line[33:39].strip())
                        id2 = int(line[39:46].strip())
                    except ValueError:
                        continue
                    raw_events.append(
                        {
                            "time_nbody": current_time,
                            "time_myr": current_time * time_conv,
                            "event_list": "BH ESCAPE",
                            "id1": id1,
                            "id2": id2,
                            "id3": None,
                            "id4": None,
                            "source": os.path.basename(path),
                            "sequence": sequence,
                        }
                    )
                    sequence += 1
                if COAL_PREFIX_PATTERN.search(line):
                    coal_lines_found += 1
                    ids = parse_coal_line(line)
                    if ids is not None:
                        coal_lines_parsed += 1
                        raw_events.append(
                            {
                                "time_nbody": current_time,
                                "time_myr": current_time * time_conv,
                                "event_list": "MERGE",
                                "id1": ids[0],
                                "id2": ids[1],
                                "id3": None,
                                "id4": None,
                                "source": os.path.basename(path),
                                "sequence": sequence,
                            }
                        )
                        sequence += 1
    cluster_by_time = {}
    for row in cluster_log:
        cluster_by_time[row["time_nbody"]] = row
    return {
        "output_files": output_files,
        "raw_events": sorted(raw_events, key=lambda row: (row["time_myr"], row["sequence"])),
        "cluster_log": sorted(cluster_by_time.values(), key=lambda row: row["time_myr"]),
        "coal_lines_found": coal_lines_found,
        "coal_lines_parsed": coal_lines_parsed,
        "maximum_time_nbody": maximum_time_nbody,
        "maximum_time_myr": maximum_time_nbody * time_conv,
    }


def exchange_ids(event):
    return {int(event[name]) for name in ["id1", "id2", "id3", "id4"] if event.get(name) is not None}


def group_exchange_events(raw_events, tolerance):
    groups = []
    for event in raw_events:
        if event["event_list"] != "EXCHANGE":
            continue
        event_ids = exchange_ids(event)
        matches = [
            group
            for group in groups
            if abs(group["time_myr"] - event["time_myr"]) <= tolerance
            and group["participant_ids"] & event_ids
        ]
        if not matches:
            groups.append(
                {
                    "time_nbody": event["time_nbody"],
                    "time_myr": event["time_myr"],
                    "participant_ids": set(event_ids),
                    "configurations": [event],
                }
            )
            continue
        target = matches[0]
        target["participant_ids"].update(event_ids)
        target["configurations"].append(event)
        for extra in matches[1:]:
            target["participant_ids"].update(extra["participant_ids"])
            target["configurations"].extend(extra["configurations"])
            groups.remove(extra)
    return sorted(groups, key=lambda row: row["time_myr"])


def get_physical_count(data):
    raw_count = int(data["id"].shape[0])
    if "N" not in data:
        return raw_count, "array_length"
    value = float(np.asarray(data["N"][()]).reshape(-1)[0])
    if math.isfinite(value) and abs(value - round(value)) < 1e-6 and 1 < round(value) <= raw_count:
        return int(round(value)), "N"
    return raw_count, "array_length"


def nearest_cluster_value(cluster_log, time_myr):
    if not cluster_log:
        return math.nan
    return min(cluster_log, key=lambda row: abs(row["time_myr"] - time_myr))["rtide_pc"]


def weighted_mean(values, weights):
    return np.average(values, axis=0, weights=weights)


def cluster_kinematics(masses, positions, velocities, radius_limit):
    if len(masses) == 0:
        raise ValueError("Snapshot contains no valid positive-mass physical particles.")
    first_center = weighted_mean(positions, masses)
    if math.isfinite(radius_limit) and radius_limit > 0:
        radii = np.linalg.norm(positions - first_center, axis=1)
        mask = radii <= radius_limit
        if np.count_nonzero(mask) >= 2:
            masses = masses[mask]
            positions = positions[mask]
            velocities = velocities[mask]
    center = weighted_mean(positions, masses)
    velocity_center = weighted_mean(velocities, masses)
    residual = velocities - velocity_center
    vrms = float(np.sqrt(np.mean(np.sum(residual * residual, axis=1))))
    radii = np.linalg.norm(positions - center, axis=1)
    order = np.argsort(radii)
    cumulative_mass = np.cumsum(masses[order])
    half_mass_index = int(np.searchsorted(cumulative_mass, 0.5 * cumulative_mass[-1], side="left"))
    half_mass_radius = float(radii[order[min(half_mass_index, len(order) - 1)]])
    return center, velocity_center, vrms, half_mass_radius


_WORKER_HDF5 = None
_WORKER_SCALING = None
_WORKER_BH_TYPE = 14
_WORKER_CLUSTER_LOG = None


def initialize_worker(hdf5_path, scaling, bh_type, cluster_log):
    global _WORKER_HDF5, _WORKER_SCALING, _WORKER_BH_TYPE, _WORKER_CLUSTER_LOG
    _WORKER_HDF5 = h5py.File(hdf5_path, "r")
    _WORKER_SCALING = scaling
    _WORKER_BH_TYPE = bh_type
    _WORKER_CLUSTER_LOG = cluster_log


def process_snapshot_worker(key):
    data = _WORKER_HDF5[key]
    physical_count, count_source = get_physical_count(data)
    ids = np.asarray(data["id"][:physical_count], dtype=float).reshape(-1)
    masses = np.asarray(data["m"][:physical_count], dtype=float).reshape(-1)
    kw = np.asarray(data["kw"][:physical_count], dtype=float).reshape(-1)
    positions = np.column_stack(
        [np.asarray(data[name][:physical_count], dtype=float).reshape(-1) for name in ["x", "y", "z"]]
    )
    velocities = np.column_stack(
        [np.asarray(data[name][:physical_count], dtype=float).reshape(-1) for name in ["vx", "vy", "vz"]]
    )
    time_nbody = float(np.asarray(data["t"][()]).reshape(-1)[0])
    time_myr = time_nbody * _WORKER_SCALING["time"]
    finite_identity = np.isfinite(ids) & np.isfinite(kw)
    valid_id = finite_identity & (ids > 0) & (np.abs(ids - np.rint(ids)) < 1e-6)
    valid_kw = finite_identity & (kw >= 0) & (kw <= 15) & (np.abs(kw - np.rint(kw)) < 1e-6)
    finite_phase = np.all(np.isfinite(positions), axis=1) & np.all(np.isfinite(velocities), axis=1)
    safe_phase = finite_phase & np.all(np.abs(positions) < 1e100, axis=1) & np.all(np.abs(velocities) < 1e100, axis=1)
    valid_mass = np.isfinite(masses) & (masses > 0) & (masses <= 1.0 + 1e-8)
    valid = valid_id & valid_kw & safe_phase & valid_mass
    converted_masses = masses[valid] * _WORKER_SCALING["mass"]
    converted_positions = positions[valid] * _WORKER_SCALING["length"]
    converted_velocities = velocities[valid] * _WORKER_SCALING["velocity"]
    rtide = nearest_cluster_value(_WORKER_CLUSTER_LOG, time_myr)
    center, center_velocity, vrms, half_mass_radius = cluster_kinematics(
        converted_masses,
        converted_positions,
        converted_velocities,
        rtide,
    )
    bh_mask = valid_id & safe_phase & np.isfinite(masses) & (masses >= 0) & (masses <= 1.0 + 1e-8) & (np.rint(kw) == _WORKER_BH_TYPE)
    bh_indices = np.flatnonzero(bh_mask)
    records = []
    zero_ids = []
    for index in bh_indices:
        object_id = int(round(ids[index]))
        mass = float(masses[index] * _WORKER_SCALING["mass"])
        if mass <= 0:
            zero_ids.append(object_id)
            continue
        records.append(
            {
                "time_myr": time_myr,
                "id": object_id,
                "mass": mass,
                "vx": float(velocities[index, 0] * _WORKER_SCALING["velocity"]),
                "vy": float(velocities[index, 1] * _WORKER_SCALING["velocity"]),
                "vz": float(velocities[index, 2] * _WORKER_SCALING["velocity"]),
                "x": float(positions[index, 0] * _WORKER_SCALING["length"]),
                "y": float(positions[index, 1] * _WORKER_SCALING["length"]),
                "z": float(positions[index, 2] * _WORKER_SCALING["length"]),
                "representation": "physical",
                "source_id": object_id,
                "_proxy": False,
            }
        )
    cluster_row = {
        "snapshot_key": key,
        "time_nbody": time_nbody,
        "time_myr": time_myr,
        "rtide_pc": rtide,
        "half_mass_radius_pc": half_mass_radius,
        "com_x_pc": float(center[0]),
        "com_y_pc": float(center[1]),
        "com_z_pc": float(center[2]),
        "vcom_x_kms": float(center_velocity[0]),
        "vcom_y_kms": float(center_velocity[1]),
        "vcom_z_kms": float(center_velocity[2]),
        "vrms_3d_number_kms": vrms,
        "sigma_1d_number_kms": vrms / math.sqrt(3.0),
        "analysis_quality_ok": True,
        "analysis_quality_reason": "",
        "_physical_count": physical_count,
        "_raw_count": int(data["id"].shape[0]),
        "_count_source": count_source,
        "_excluded_count": int(np.count_nonzero(~valid)),
    }
    return {
        "key": key,
        "time_nbody": time_nbody,
        "time_myr": time_myr,
        "records": records,
        "zero_ids": zero_ids,
        "cluster": cluster_row,
    }


def nearest_positive_mass(frames, object_id, event_time, before):
    candidates = []
    for frame in frames:
        if before and frame["time_myr"] >= event_time:
            continue
        if not before and frame["time_myr"] <= event_time:
            continue
        for record in frame["records"]:
            if record["id"] == object_id and record["mass"] > 0:
                candidates.append((abs(frame["time_myr"] - event_time), record["mass"]))
    if not candidates:
        return None
    return min(candidates, key=lambda item: item[0])[1]


def reference_masses(frames, group):
    result = {}
    for object_id in group["participant_ids"]:
        before = nearest_positive_mass(frames, object_id, group["time_myr"], True)
        after = nearest_positive_mass(frames, object_id, group["time_myr"], False)
        values = [value for value in [before, after] if value is not None and value > 0]
        if values:
            result[object_id] = min(values)
    return result


def exchange_pairs(group):
    pairs = set()
    for config in group["configurations"]:
        pairs.add(frozenset([config["id1"], config["id2"]]))
        pairs.add(frozenset([config["id3"], config["id4"]]))
    return pairs


def repair_regularized_history(frames, groups, tolerance):
    repair_count = 0
    for group in groups:
        references = reference_masses(frames, group)
        group["reference_masses"] = references
        allowed_pairs = exchange_pairs(group)
        if len(references) < 2:
            continue
        for frame in frames:
            missing_ids = set(frame["zero_ids"]) & group["participant_ids"]
            if not missing_ids:
                continue
            records_by_id = {record["id"]: record for record in frame["records"]}
            for missing_id in sorted(missing_ids):
                if missing_id not in references:
                    continue
                for composite_id in sorted(group["participant_ids"] - {missing_id}):
                    if frozenset([missing_id, composite_id]) not in allowed_pairs:
                        continue
                    if composite_id not in references or composite_id not in records_by_id:
                        continue
                    composite = records_by_id[composite_id]
                    expected = references[missing_id] + references[composite_id]
                    if expected <= 0:
                        continue
                    relative = abs(composite["mass"] - expected) / expected
                    if relative > tolerance or composite["mass"] <= references[composite_id] * (1.0 + tolerance):
                        continue
                    stored_mass = composite["mass"]
                    composite["mass"] = references[composite_id]
                    composite["representation"] = "ks_chain_component_proxy"
                    proxy = dict(composite)
                    proxy["id"] = missing_id
                    proxy["mass"] = references[missing_id]
                    proxy["representation"] = "ks_chain_component_proxy"
                    proxy["source_id"] = composite_id
                    proxy["_proxy"] = True
                    proxy["_stored_composite_mass"] = stored_mass
                    composite["_proxy"] = True
                    composite["_stored_composite_mass"] = stored_mass
                    frame["records"].append(proxy)
                    records_by_id[missing_id] = proxy
                    repair_count += 1
                    break
    for frame in frames:
        unique = {}
        for record in frame["records"]:
            current = unique.get(record["id"])
            if current is None or (current.get("_proxy", False) and not record.get("_proxy", False)):
                unique[record["id"]] = record
        frame["records"] = sorted(unique.values(), key=lambda row: row["id"])
    return repair_count


def frame_record_map(frame):
    return {record["id"]: record for record in frame["records"]}


def is_complete_resolved(frame, ids, references, mass_tolerance):
    records = frame_record_map(frame)
    for object_id in ids:
        record = records.get(object_id)
        if record is None or record.get("_proxy", False) or record["mass"] <= 0:
            return False
        reference = references.get(object_id)
        if reference is not None and abs(record["mass"] - reference) / max(reference, 1e-30) > mass_tolerance:
            return False
    return True


def find_resolved_frame_for_ids(frames, group, ids, before, search_window, mass_tolerance):
    event_time = group["time_myr"]
    references = group.get("reference_masses", {})
    candidates = [
        frame
        for frame in frames
        if ((frame["time_myr"] < event_time) if before else (frame["time_myr"] > event_time))
        and abs(frame["time_myr"] - event_time) <= search_window
    ]
    candidates.sort(key=lambda frame: abs(frame["time_myr"] - event_time))
    for frame in candidates:
        if is_complete_resolved(frame, ids, references, mass_tolerance):
            return frame
    return None


def resolved_frame_failure_reason(frames, group, ids, before, search_window, mass_tolerance):
    side = "pre" if before else "post"
    event_time = group["time_myr"]
    references = group.get("reference_masses", {})
    candidates = [
        frame
        for frame in frames
        if ((frame["time_myr"] < event_time) if before else (frame["time_myr"] > event_time))
        and abs(frame["time_myr"] - event_time) <= search_window
    ]
    if not candidates:
        return f"{side}: no snapshot within {search_window:g} Myr of the event"
    failures = []
    for object_id in sorted(ids):
        records = [frame_record_map(frame).get(object_id) for frame in candidates]
        present = [record for record in records if record is not None]
        if not present:
            failures.append(f"ID {object_id} missing from all {len(candidates)} candidate snapshots")
            continue
        non_proxy = [record for record in present if not record.get("_proxy", False) and record["mass"] > 0]
        if not non_proxy:
            failures.append(f"ID {object_id} available only as an unresolved proxy")
            continue
        reference = references.get(object_id)
        if reference is not None:
            mass_consistent = [
                record
                for record in non_proxy
                if abs(record["mass"] - reference) / max(reference, 1e-30) <= mass_tolerance
            ]
            if not mass_consistent:
                failures.append(
                    f"ID {object_id} mass differs from its reference by more than {100.0 * mass_tolerance:g}%"
                )
    if failures:
        return f"{side}: " + "; ".join(failures)
    return f"{side}: required participants were not simultaneously resolved in one snapshot"


def pair_for_side(config, before):
    if before:
        return config["id1"], config["id2"]
    return config["id3"], config["id4"]


def normalized_pair(pair):
    return tuple(sorted(int(object_id) for object_id in pair))


def exchange_raw_transitions(group):
    configurations = sorted(group["configurations"], key=lambda row: row["sequence"])
    return [
        {
            "before_pair": normalized_pair(pair_for_side(config, True)),
            "after_pair": normalized_pair(pair_for_side(config, False)),
        }
        for config in configurations
    ]


def format_pair(pair):
    return "+".join(str(object_id) for object_id in pair)


def format_transition_sequence(transitions):
    return "|".join(
        f"{format_pair(transition['before_pair'])}>{format_pair(transition['after_pair'])}"
        for transition in transitions
    )


def classify_exchange_group(group):
    configurations = sorted(group["configurations"], key=lambda row: row["sequence"])
    initial_pair = normalized_pair(pair_for_side(configurations[0], True))
    final_pair = normalized_pair(pair_for_side(configurations[-1], False))
    transitions = exchange_raw_transitions(group)
    all_recorded_ids = set()
    for transition in transitions:
        all_recorded_ids.update(transition["before_pair"])
        all_recorded_ids.update(transition["after_pair"])
    event_type = "TRANSIENT EXCHANGE" if initial_pair == final_pair else "EXCHANGE"
    return {
        "event_type": event_type,
        "initial_pair": initial_pair,
        "final_pair": final_pair,
        "raw_transitions": transitions,
        "raw_diagnostic_count": len(configurations),
        "temporary_ids": sorted(all_recorded_ids - set(initial_pair) - set(final_pair)),
    }


def net_exchange_configuration(group):
    configurations = sorted(group["configurations"], key=lambda row: row["sequence"])
    first = configurations[0]
    last = configurations[-1]
    initial_pair = normalized_pair(pair_for_side(first, True))
    final_pair = normalized_pair(pair_for_side(last, False))
    result = dict(last)
    result.update(
        {
            "id1": initial_pair[0],
            "id2": initial_pair[1],
            "id3": final_pair[0],
            "id4": final_pair[1],
            "e_before": first.get("e_before"),
            "a_before_nbody": first.get("a_before_nbody"),
            "eb_before_nbody": first.get("eb_before_nbody"),
        }
    )
    for field in ["chain_binary_vcm", "chain_escape_vf"]:
        values = [config for config in configurations if config.get(field) is not None]
        if values:
            result[field] = values[-1][field]
    return result


def prepare_exchange_groups(frames, groups, search_window, mass_tolerance):
    for group in groups:
        classification = classify_exchange_group(group)
        selected = net_exchange_configuration(group)
        selected_ids = (
            set(group["participant_ids"])
            if classification["event_type"] == "TRANSIENT EXCHANGE"
            else {selected["id1"], selected["id2"], selected["id3"], selected["id4"]}
        )
        before = find_resolved_frame_for_ids(frames, group, selected_ids, True, search_window, mass_tolerance)
        after = find_resolved_frame_for_ids(frames, group, selected_ids, False, search_window, mass_tolerance)
        group["before_frame"] = before
        group["after_frame"] = after
        group["selected"] = selected
        group.update(classification)
        group["before_failure_reason"] = (
            ""
            if before is not None
            else resolved_frame_failure_reason(
                frames,
                group,
                selected_ids,
                True,
                search_window,
                mass_tolerance,
            )
        )
        group["after_failure_reason"] = (
            ""
            if after is not None
            else resolved_frame_failure_reason(
                frames,
                group,
                selected_ids,
                False,
                search_window,
                mass_tolerance,
            )
        )


def public_event_rows(raw_events, groups):
    rows = []
    for event in raw_events:
        if event["event_list"] == "EXCHANGE":
            continue
        rows.append(
            {
                "time_myr": event["time_myr"],
                "event_list": event["event_list"],
                "id1_list": event.get("id1", ""),
                "id2_list": event.get("id2", ""),
                "id3_list": event.get("id3", "") or "",
                "id4_list": event.get("id4", "") or "",
                "temporary_ids": "",
            }
        )
    for group in groups:
        event = group["selected"]
        rows.append(
            {
                "time_myr": group["time_myr"],
                "event_list": group["event_type"],
                "id1_list": event["id1"],
                "id2_list": event["id2"],
                "id3_list": event["id3"],
                "id4_list": event["id4"],
                "temporary_ids": ";".join(str(object_id) for object_id in group["temporary_ids"]),
            }
        )
    return sorted(rows, key=lambda row: (row["time_myr"], row["event_list"]))


def read_full_frame(hdf5_path, key, scaling):
    with h5py.File(hdf5_path, "r") as handle:
        data = handle[key]
        physical_count, _ = get_physical_count(data)
        ids = np.asarray(data["id"][:physical_count], dtype=float).reshape(-1)
        masses = np.asarray(data["m"][:physical_count], dtype=float).reshape(-1)
        kw = np.asarray(data["kw"][:physical_count], dtype=float).reshape(-1)
        positions = np.column_stack(
            [np.asarray(data[name][:physical_count], dtype=float).reshape(-1) for name in ["x", "y", "z"]]
        )
        velocities = np.column_stack(
            [np.asarray(data[name][:physical_count], dtype=float).reshape(-1) for name in ["vx", "vy", "vz"]]
        )
    valid = (
        np.isfinite(ids)
        & (ids > 0)
        & (np.abs(ids - np.rint(ids)) < 1e-6)
        & np.isfinite(masses)
        & (masses > 0)
        & (masses <= 1.0 + 1e-8)
        & np.isfinite(kw)
        & (kw >= 0)
        & (kw <= 15)
        & np.all(np.isfinite(positions), axis=1)
        & np.all(np.isfinite(velocities), axis=1)
        & np.all(np.abs(positions) < 1e100, axis=1)
        & np.all(np.abs(velocities) < 1e100, axis=1)
    )
    return {
        "ids": np.rint(ids[valid]).astype(np.int64),
        "masses": masses[valid] * scaling["mass"],
        "positions": positions[valid] * scaling["length"],
        "velocities": velocities[valid] * scaling["velocity"],
        "kw": np.rint(kw[valid]).astype(np.int64),
    }


def body_from_full(frame, object_id):
    matches = np.flatnonzero(frame["ids"] == int(object_id))
    if len(matches) == 0:
        raise KeyError(object_id)
    index = int(matches[0])
    return {
        "id": int(object_id),
        "mass": float(frame["masses"][index]),
        "position": frame["positions"][index],
        "velocity": frame["velocities"][index],
    }


def pair_measurement(first, second, cluster_velocity):
    masses = np.array([first["mass"], second["mass"]], dtype=float)
    positions = np.vstack([first["position"], second["position"]])
    velocities = np.vstack([first["velocity"], second["velocity"]])
    position = weighted_mean(positions, masses)
    velocity = weighted_mean(velocities, masses)
    return {
        "mass": float(np.sum(masses)),
        "position": position,
        "velocity": velocity,
        "speed_cluster": float(np.linalg.norm(velocity - cluster_velocity)),
    }


def spherical_escape_speed(frame, target_position, center, excluded_ids):
    mask = ~np.isin(frame["ids"], list(excluded_ids))
    masses = frame["masses"][mask]
    positions = frame["positions"][mask]
    radii = np.linalg.norm(positions - center, axis=1)
    target_radius = float(np.linalg.norm(target_position - center))
    denominator = np.maximum(np.maximum(radii, target_radius), 1e-12)
    return float(np.sqrt(2.0 * G_PC_KMS2_PER_MSUN * np.sum(masses / denominator)))


def binding_energy(m1, m2, a_pc):
    if a_pc is None or not math.isfinite(a_pc) or a_pc <= 0:
        return None
    return G_PC_KMS2_PER_MSUN * m1 * m2 / (2.0 * a_pc)


def safe_ratio(numerator, denominator):
    if numerator is None or denominator is None:
        return None
    if not math.isfinite(float(numerator)) or not math.isfinite(float(denominator)):
        return None
    if float(denominator) <= 0.0:
        return None
    return float(numerator) / float(denominator)


def radius_over_tidal_radius(position, center, tidal_radius):
    return safe_ratio(float(np.linalg.norm(position - center)), tidal_radius)


def first_escape_values(raw_events, event_time, outgoing_id, after_pair):
    outgoing_lags = []
    binary_lags = []
    pair_set = set(after_pair)
    for event in raw_events:
        if event["event_list"] != "BH ESCAPE" or event["time_myr"] <= event_time:
            continue
        lag = event["time_myr"] - event_time
        ids = {value for value in [event.get("id1"), event.get("id2")] if value is not None}
        if outgoing_id in ids:
            outgoing_lags.append(lag)
        if pair_set.issubset(ids):
            binary_lags.append(lag)
    return {
        "outgoing": min(outgoing_lags) if outgoing_lags else None,
        "binary": min(binary_lags) if binary_lags else None,
    }


def prompt_escape_values(raw_events, event_time, outgoing_id, after_pair, prompt_window):
    outgoing_lags = []
    binary_lags = []
    pair_set = set(after_pair)
    for event in raw_events:
        if event["event_list"] != "BH ESCAPE" or event["time_myr"] <= event_time:
            continue
        lag = event["time_myr"] - event_time
        if lag > prompt_window:
            continue
        ids = {event.get("id1"), event.get("id2")}
        if outgoing_id in ids:
            outgoing_lags.append(lag)
        if pair_set.issubset(ids):
            binary_lags.append(lag)
    return {
        "outgoing": min(outgoing_lags) if outgoing_lags else None,
        "binary": min(binary_lags) if binary_lags else None,
    }


def science_exchange_row(index, group, hdf5_path, scaling, raw_events, log_end_time_myr):
    before_meta = group.get("before_frame")
    after_meta = group.get("after_frame")
    if before_meta is None or after_meta is None:
        return None
    config = group["selected"]
    before_full = read_full_frame(hdf5_path, before_meta["key"], scaling)
    after_full = read_full_frame(hdf5_path, after_meta["key"], scaling)
    before_cluster = before_meta["cluster"]
    after_cluster = after_meta["cluster"]
    before_center = np.array([before_cluster[name] for name in ["com_x_pc", "com_y_pc", "com_z_pc"]])
    after_center = np.array([after_cluster[name] for name in ["com_x_pc", "com_y_pc", "com_z_pc"]])
    before_vcom = np.array([before_cluster[name] for name in ["vcom_x_kms", "vcom_y_kms", "vcom_z_kms"]])
    after_vcom = np.array([after_cluster[name] for name in ["vcom_x_kms", "vcom_y_kms", "vcom_z_kms"]])
    try:
        b1 = body_from_full(before_full, config["id1"])
        b2 = body_from_full(before_full, config["id2"])
        a1 = body_from_full(after_full, config["id3"])
        a2 = body_from_full(after_full, config["id4"])
    except KeyError:
        return None
    before_set = {config["id1"], config["id2"]}
    after_set = {config["id3"], config["id4"]}
    incoming_ids = list(after_set - before_set)
    outgoing_ids = list(before_set - after_set)
    if len(incoming_ids) != 1 or len(outgoing_ids) != 1:
        return None
    incoming = body_from_full(before_full, incoming_ids[0])
    outgoing = body_from_full(after_full, outgoing_ids[0])
    before_binary = pair_measurement(b1, b2, before_vcom)
    after_binary = pair_measurement(a1, a2, after_vcom)
    incoming_speed = float(np.linalg.norm(incoming["velocity"] - before_vcom))
    outgoing_speed = float(np.linalg.norm(outgoing["velocity"] - after_vcom))
    relative_speed_before = float(np.linalg.norm(before_binary["velocity"] - incoming["velocity"]))
    relative_speed_after = float(np.linalg.norm(after_binary["velocity"] - outgoing["velocity"]))
    before_binary_vesc = spherical_escape_speed(before_full, before_binary["position"], before_center, before_set)
    after_binary_vesc = spherical_escape_speed(after_full, after_binary["position"], after_center, after_set)
    incoming_vesc = spherical_escape_speed(before_full, incoming["position"], before_center, [incoming["id"]])
    outgoing_vesc = spherical_escape_speed(after_full, outgoing["position"], after_center, [outgoing["id"]])
    a_before_pc = config.get("a_before_nbody")
    a_after_pc = config.get("a_after_nbody")
    a_before_pc = a_before_pc * scaling["length"] if a_before_pc is not None else None
    a_after_pc = a_after_pc * scaling["length"] if a_after_pc is not None else None
    eb_before = binding_energy(b1["mass"], b2["mass"], a_before_pc)
    eb_after = binding_energy(a1["mass"], a2["mass"], a_after_pc)
    release = eb_after - eb_before if eb_before is not None and eb_after is not None else None
    energy_scale = scaling["mass"] * scaling["velocity"] ** 2
    eb_log_before = config.get("eb_before_nbody")
    eb_log_after = config.get("eb_after_nbody")
    eb_log_before = abs(eb_log_before) * energy_scale if eb_log_before is not None else None
    eb_log_after = abs(eb_log_after) * energy_scale if eb_log_after is not None else None
    primary_eb_before = eb_log_before if eb_log_before is not None else eb_before
    primary_eb_after = eb_log_after if eb_log_after is not None else eb_after
    fractional_binding_change = (
        (primary_eb_after - primary_eb_before) / primary_eb_before
        if primary_eb_before is not None and primary_eb_after is not None and primary_eb_before > 0
        else None
    )
    after_total_mass = after_binary["mass"] + outgoing["mass"]
    after_triple_velocity = (
        after_binary["mass"] * after_binary["velocity"] + outgoing["mass"] * outgoing["velocity"]
    ) / after_total_mass
    measured_binary = float(np.linalg.norm(after_binary["velocity"] - after_triple_velocity))
    measured_outgoing = float(np.linalg.norm(outgoing["velocity"] - after_triple_velocity))
    before_total_mass = before_binary["mass"] + incoming["mass"]
    before_triple_velocity = (
        before_binary["mass"] * before_binary["velocity"] + incoming["mass"] * incoming["velocity"]
    ) / before_total_mass
    kinetic_before = 0.5 * before_binary["mass"] * np.sum((before_binary["velocity"] - before_triple_velocity) ** 2)
    kinetic_before += 0.5 * incoming["mass"] * np.sum((incoming["velocity"] - before_triple_velocity) ** 2)
    kinetic_after = 0.5 * after_binary["mass"] * measured_binary**2
    kinetic_after += 0.5 * outgoing["mass"] * measured_outgoing**2
    predicted_binary = None
    predicted_outgoing = None
    if release is not None and release > 0:
        predicted_binary = math.sqrt(2.0 * release * outgoing["mass"] / (after_binary["mass"] * after_total_mass))
        predicted_outgoing = math.sqrt(2.0 * release * after_binary["mass"] / (outgoing["mass"] * after_total_mass))
    prompt = prompt_escape_values(
        raw_events,
        group["time_myr"],
        outgoing["id"],
        (a1["id"], a2["id"]),
        100.0 * scaling["time"],
    )
    actual_escape = first_escape_values(
        raw_events,
        group["time_myr"],
        outgoing["id"],
        (a1["id"], a2["id"]),
    )
    sigma_before = before_cluster["sigma_1d_number_kms"]
    sigma_after = after_cluster["sigma_1d_number_kms"]
    before_rtide = before_cluster.get("rtide_pc")
    after_rtide = after_cluster.get("rtide_pc")
    before_half_mass_radius = before_cluster.get("half_mass_radius_pc")
    after_half_mass_radius = after_cluster.get("half_mass_radius_pc")
    before_triple_position = weighted_mean(
        np.vstack([b1["position"], b2["position"], incoming["position"]]),
        np.array([b1["mass"], b2["mass"], incoming["mass"]], dtype=float),
    )
    after_triple_position = weighted_mean(
        np.vstack([a1["position"], a2["position"], outgoing["position"]]),
        np.array([a1["mass"], a2["mass"], outgoing["mass"]], dtype=float),
    )
    interaction_radius_before = float(np.linalg.norm(before_triple_position - before_center))
    interaction_radius_after = float(np.linalg.norm(after_triple_position - after_center))
    return {
        "exchange_index": index,
        "event_type": "EXCHANGE",
        "event_time_nbody": group["time_nbody"],
        "event_time_myr": group["time_myr"],
        "diagnostic_start_time_myr": min(config["time_myr"] for config in group["configurations"]),
        "diagnostic_end_time_myr": max(config["time_myr"] for config in group["configurations"]),
        "raw_diagnostic_count": group["raw_diagnostic_count"],
        "raw_transitions": format_transition_sequence(group["raw_transitions"]),
        "temporary_ids": ";".join(str(object_id) for object_id in group["temporary_ids"]),
        "before_snapshot_key": before_meta["key"],
        "after_snapshot_key": after_meta["key"],
        "before_snapshot_time_myr": before_meta["time_myr"],
        "after_snapshot_time_myr": after_meta["time_myr"],
        "before_snapshot_offset_myr": before_meta["time_myr"] - group["time_myr"],
        "after_snapshot_offset_myr": after_meta["time_myr"] - group["time_myr"],
        "before_pair": f"{b1['id']}+{b2['id']}",
        "after_pair": f"{a1['id']}+{a2['id']}",
        "incoming_id": incoming["id"],
        "outgoing_id": outgoing["id"],
        "sigma_1d_before_kms": sigma_before,
        "sigma_1d_after_kms": sigma_after,
        "binary_speed_before_kms": before_binary["speed_cluster"],
        "binary_speed_after_kms": after_binary["speed_cluster"],
        "incoming_speed_before_kms": incoming_speed,
        "outgoing_speed_after_kms": outgoing_speed,
        "binary_single_relative_speed_before_kms": relative_speed_before,
        "binary_single_relative_speed_after_kms": relative_speed_after,
        "binary_vesc_before_kms": before_binary_vesc,
        "binary_vesc_after_kms": after_binary_vesc,
        "incoming_vesc_before_kms": incoming_vesc,
        "outgoing_vesc_after_kms": outgoing_vesc,
        "binary_v_over_sigma_after": after_binary["speed_cluster"] / sigma_after if sigma_after > 0 else None,
        "outgoing_v_over_sigma_after": outgoing_speed / sigma_after if sigma_after > 0 else None,
        "binary_v_over_vesc_after": after_binary["speed_cluster"] / after_binary_vesc if after_binary_vesc > 0 else None,
        "outgoing_v_over_vesc_after": outgoing_speed / outgoing_vesc if outgoing_vesc > 0 else None,
        "e_before_log": config.get("e_before"),
        "e_after_log": config.get("e_after"),
        "a_before_au_log": a_before_pc * PC_TO_AU if a_before_pc is not None else None,
        "a_after_au_log": a_after_pc * PC_TO_AU if a_after_pc is not None else None,
        "pericenter_before_au": a_before_pc * PC_TO_AU * (1.0 - config["e_before"]) if a_before_pc is not None and config.get("e_before") is not None else None,
        "pericenter_after_au": a_after_pc * PC_TO_AU * (1.0 - config["e_after"]) if a_after_pc is not None and config.get("e_after") is not None else None,
        "before_m1_msun": b1["mass"],
        "before_m2_msun": b2["mass"],
        "incoming_mass_msun": incoming["mass"],
        "after_m1_msun": a1["mass"],
        "after_m2_msun": a2["mass"],
        "outgoing_mass_msun": outgoing["mass"],
        "binding_energy_before_msun_kms2": eb_before,
        "binding_energy_after_msun_kms2": eb_after,
        "binding_energy_release_msun_kms2": release,
        "binding_energy_release_positive": bool(release is not None and release > 0),
        "binding_energy_log_before_msun_kms2": eb_log_before,
        "binding_energy_log_after_msun_kms2": eb_log_after,
        "binding_energy_primary_source": "NBODY LOG" if eb_log_before is not None and eb_log_after is not None else "DERIVED",
        "binding_energy_fractional_change": fractional_binding_change,
        "interaction_com_radius_before_pc": interaction_radius_before,
        "interaction_com_radius_after_pc": interaction_radius_after,
        "cluster_half_mass_radius_before_pc": before_half_mass_radius,
        "cluster_half_mass_radius_after_pc": after_half_mass_radius,
        "interaction_com_r_over_rh_before": safe_ratio(interaction_radius_before, before_half_mass_radius),
        "interaction_com_r_over_rh_after": safe_ratio(interaction_radius_after, after_half_mass_radius),
        "binary_r_over_rtide_before": radius_over_tidal_radius(before_binary["position"], before_center, before_rtide),
        "binary_r_over_rtide_after": radius_over_tidal_radius(after_binary["position"], after_center, after_rtide),
        "single_r_over_rtide_before": radius_over_tidal_radius(incoming["position"], before_center, before_rtide),
        "single_r_over_rtide_after": radius_over_tidal_radius(outgoing["position"], after_center, after_rtide),
        "binary_recoil_after_triple_com_kms": measured_binary,
        "outgoing_speed_after_triple_com_kms": measured_outgoing,
        "predicted_binary_recoil_kms": predicted_binary,
        "predicted_outgoing_recoil_kms": predicted_outgoing,
        "measured_over_predicted_binary": measured_binary / predicted_binary if predicted_binary else None,
        "measured_over_predicted_outgoing": measured_outgoing / predicted_outgoing if predicted_outgoing else None,
        "kinetic_change_over_binding_release": (kinetic_after - kinetic_before) / release if release and release > 0 else None,
        "chain_escape_vf_log_kms": config.get("chain_escape_vf"),
        "chain_binary_vcm_log_kms": config.get("chain_binary_vcm"),
        "outgoing_prompt_cluster_escape": prompt["outgoing"] is not None,
        "binary_prompt_cluster_escape": prompt["binary"] is not None,
        "outgoing_first_cluster_escape_lag_myr": prompt["outgoing"],
        "binary_first_exact_escape_lag_myr": prompt["binary"],
        "outgoing_escape_recorded": actual_escape["outgoing"] is not None,
        "binary_exact_escape_recorded": actual_escape["binary"] is not None,
        "outgoing_first_escape_lag_myr": actual_escape["outgoing"],
        "binary_first_escape_lag_myr": actual_escape["binary"],
        "escape_log_search_end_myr": log_end_time_myr,
    }


def system_measurement(bodies, cluster_velocity):
    masses = np.array([body["mass"] for body in bodies], dtype=float)
    positions = np.vstack([body["position"] for body in bodies])
    velocities = np.vstack([body["velocity"] for body in bodies])
    position = weighted_mean(positions, masses)
    velocity = weighted_mean(velocities, masses)
    return {
        "mass": float(np.sum(masses)),
        "position": position,
        "velocity": velocity,
        "speed_cluster": float(np.linalg.norm(velocity - cluster_velocity)),
    }


def science_transient_row(index, group, hdf5_path, scaling):
    before_meta = group.get("before_frame")
    after_meta = group.get("after_frame")
    if before_meta is None or after_meta is None:
        return None
    temporary_ids = list(group["temporary_ids"])
    if not temporary_ids:
        return None
    before_full = read_full_frame(hdf5_path, before_meta["key"], scaling)
    after_full = read_full_frame(hdf5_path, after_meta["key"], scaling)
    before_cluster = before_meta["cluster"]
    after_cluster = after_meta["cluster"]
    before_center = np.array([before_cluster[name] for name in ["com_x_pc", "com_y_pc", "com_z_pc"]])
    after_center = np.array([after_cluster[name] for name in ["com_x_pc", "com_y_pc", "com_z_pc"]])
    before_vcom = np.array([before_cluster[name] for name in ["vcom_x_kms", "vcom_y_kms", "vcom_z_kms"]])
    after_vcom = np.array([after_cluster[name] for name in ["vcom_x_kms", "vcom_y_kms", "vcom_z_kms"]])
    stable_pair = group["initial_pair"]
    try:
        before_pair_bodies = [body_from_full(before_full, object_id) for object_id in stable_pair]
        after_pair_bodies = [body_from_full(after_full, object_id) for object_id in stable_pair]
        before_interlopers = [body_from_full(before_full, object_id) for object_id in temporary_ids]
        after_interlopers = [body_from_full(after_full, object_id) for object_id in temporary_ids]
    except KeyError:
        return None
    before_binary = system_measurement(before_pair_bodies, before_vcom)
    after_binary = system_measurement(after_pair_bodies, after_vcom)
    before_interloper = system_measurement(before_interlopers, before_vcom)
    after_interloper = system_measurement(after_interlopers, after_vcom)
    before_system = system_measurement(before_pair_bodies + before_interlopers, before_vcom)
    after_system = system_measurement(after_pair_bodies + after_interlopers, after_vcom)
    before_binary_vesc = spherical_escape_speed(before_full, before_binary["position"], before_center, stable_pair)
    after_binary_vesc = spherical_escape_speed(after_full, after_binary["position"], after_center, stable_pair)
    before_interloper_vesc = spherical_escape_speed(before_full, before_interloper["position"], before_center, temporary_ids)
    after_interloper_vesc = spherical_escape_speed(after_full, after_interloper["position"], after_center, temporary_ids)
    config = group["selected"]
    a_before_pc = config.get("a_before_nbody")
    a_after_pc = config.get("a_after_nbody")
    a_before_pc = a_before_pc * scaling["length"] if a_before_pc is not None else None
    a_after_pc = a_after_pc * scaling["length"] if a_after_pc is not None else None
    eb_before = binding_energy(before_pair_bodies[0]["mass"], before_pair_bodies[1]["mass"], a_before_pc)
    eb_after = binding_energy(after_pair_bodies[0]["mass"], after_pair_bodies[1]["mass"], a_after_pc)
    release = eb_after - eb_before if eb_before is not None and eb_after is not None else None
    energy_scale = scaling["mass"] * scaling["velocity"] ** 2
    eb_log_before = config.get("eb_before_nbody")
    eb_log_after = config.get("eb_after_nbody")
    eb_log_before = abs(eb_log_before) * energy_scale if eb_log_before is not None else None
    eb_log_after = abs(eb_log_after) * energy_scale if eb_log_after is not None else None
    primary_before = eb_log_before if eb_log_before is not None else eb_before
    primary_after = eb_log_after if eb_log_after is not None else eb_after
    fractional_change = (
        (primary_after - primary_before) / primary_before
        if primary_before is not None and primary_after is not None and primary_before > 0
        else None
    )
    before_radius = float(np.linalg.norm(before_system["position"] - before_center))
    after_radius = float(np.linalg.norm(after_system["position"] - after_center))
    sigma_before = before_cluster["sigma_1d_number_kms"]
    sigma_after = after_cluster["sigma_1d_number_kms"]
    return {
        "exchange_index": index,
        "event_type": "TRANSIENT EXCHANGE",
        "event_time_nbody": group["time_nbody"],
        "event_time_myr": group["time_myr"],
        "diagnostic_start_time_myr": min(item["time_myr"] for item in group["configurations"]),
        "diagnostic_end_time_myr": max(item["time_myr"] for item in group["configurations"]),
        "raw_diagnostic_count": group["raw_diagnostic_count"],
        "raw_transitions": format_transition_sequence(group["raw_transitions"]),
        "temporary_ids": ";".join(str(object_id) for object_id in temporary_ids),
        "before_snapshot_key": before_meta["key"],
        "after_snapshot_key": after_meta["key"],
        "before_snapshot_time_myr": before_meta["time_myr"],
        "after_snapshot_time_myr": after_meta["time_myr"],
        "before_snapshot_offset_myr": before_meta["time_myr"] - group["time_myr"],
        "after_snapshot_offset_myr": after_meta["time_myr"] - group["time_myr"],
        "before_pair": format_pair(stable_pair),
        "after_pair": format_pair(stable_pair),
        "sigma_1d_before_kms": sigma_before,
        "sigma_1d_after_kms": sigma_after,
        "binary_speed_before_kms": before_binary["speed_cluster"],
        "binary_speed_after_kms": after_binary["speed_cluster"],
        "incoming_speed_before_kms": before_interloper["speed_cluster"],
        "outgoing_speed_after_kms": after_interloper["speed_cluster"],
        "binary_single_relative_speed_before_kms": float(np.linalg.norm(before_binary["velocity"] - before_interloper["velocity"])),
        "binary_single_relative_speed_after_kms": float(np.linalg.norm(after_binary["velocity"] - after_interloper["velocity"])),
        "binary_vesc_before_kms": before_binary_vesc,
        "binary_vesc_after_kms": after_binary_vesc,
        "incoming_vesc_before_kms": before_interloper_vesc,
        "outgoing_vesc_after_kms": after_interloper_vesc,
        "binary_v_over_sigma_after": safe_ratio(after_binary["speed_cluster"], sigma_after),
        "outgoing_v_over_sigma_after": safe_ratio(after_interloper["speed_cluster"], sigma_after),
        "binary_v_over_vesc_after": safe_ratio(after_binary["speed_cluster"], after_binary_vesc),
        "outgoing_v_over_vesc_after": safe_ratio(after_interloper["speed_cluster"], after_interloper_vesc),
        "e_before_log": config.get("e_before"),
        "e_after_log": config.get("e_after"),
        "a_before_au_log": a_before_pc * PC_TO_AU if a_before_pc is not None else None,
        "a_after_au_log": a_after_pc * PC_TO_AU if a_after_pc is not None else None,
        "pericenter_before_au": a_before_pc * PC_TO_AU * (1.0 - config["e_before"]) if a_before_pc is not None and config.get("e_before") is not None else None,
        "pericenter_after_au": a_after_pc * PC_TO_AU * (1.0 - config["e_after"]) if a_after_pc is not None and config.get("e_after") is not None else None,
        "before_m1_msun": before_pair_bodies[0]["mass"],
        "before_m2_msun": before_pair_bodies[1]["mass"],
        "incoming_mass_msun": before_interloper["mass"],
        "after_m1_msun": after_pair_bodies[0]["mass"],
        "after_m2_msun": after_pair_bodies[1]["mass"],
        "outgoing_mass_msun": after_interloper["mass"],
        "binding_energy_before_msun_kms2": eb_before,
        "binding_energy_after_msun_kms2": eb_after,
        "binding_energy_release_msun_kms2": release,
        "binding_energy_release_positive": bool(release is not None and release > 0),
        "binding_energy_log_before_msun_kms2": eb_log_before,
        "binding_energy_log_after_msun_kms2": eb_log_after,
        "binding_energy_primary_source": "NBODY LOG" if eb_log_before is not None and eb_log_after is not None else "DERIVED",
        "binding_energy_fractional_change": fractional_change,
        "interaction_com_radius_before_pc": before_radius,
        "interaction_com_radius_after_pc": after_radius,
        "cluster_half_mass_radius_before_pc": before_cluster.get("half_mass_radius_pc"),
        "cluster_half_mass_radius_after_pc": after_cluster.get("half_mass_radius_pc"),
        "interaction_com_r_over_rh_before": safe_ratio(before_radius, before_cluster.get("half_mass_radius_pc")),
        "interaction_com_r_over_rh_after": safe_ratio(after_radius, after_cluster.get("half_mass_radius_pc")),
        "binary_r_over_rtide_before": radius_over_tidal_radius(before_binary["position"], before_center, before_cluster.get("rtide_pc")),
        "binary_r_over_rtide_after": radius_over_tidal_radius(after_binary["position"], after_center, after_cluster.get("rtide_pc")),
        "single_r_over_rtide_before": radius_over_tidal_radius(before_interloper["position"], before_center, before_cluster.get("rtide_pc")),
        "single_r_over_rtide_after": radius_over_tidal_radius(after_interloper["position"], after_center, after_cluster.get("rtide_pc")),
    }


def resolved_frame_record(frame, object_id):
    record = frame_record_map(frame).get(int(object_id))
    if record is None or record.get("_proxy", False) or record.get("mass", 0) <= 0:
        return None
    return record


def find_merge_frames(frames, event, search_window):
    event_time = event["time_myr"]
    coal_ids = [event["id1"], event["id2"]]
    before_candidates = [
        frame for frame in frames
        if frame["time_myr"] < event_time and event_time - frame["time_myr"] <= search_window
    ]
    before_candidates.sort(key=lambda frame: event_time - frame["time_myr"])
    before = next(
        (
            frame for frame in before_candidates
            if all(resolved_frame_record(frame, object_id) is not None for object_id in coal_ids)
        ),
        None,
    )
    after_candidates = [
        frame for frame in frames
        if frame["time_myr"] > event_time and frame["time_myr"] - event_time <= search_window
    ]
    after_candidates.sort(key=lambda frame: frame["time_myr"] - event_time)
    after = None
    survivor_id = None
    for frame in after_candidates:
        present = [object_id for object_id in coal_ids if resolved_frame_record(frame, object_id) is not None]
        if len(present) == 1:
            after = frame
            survivor_id = present[0]
            break
    return before, after, survivor_id


def first_single_escape_lag(raw_events, event_time, object_id):
    lags = []
    for event in raw_events:
        if event["event_list"] != "BH ESCAPE" or event["time_myr"] <= event_time:
            continue
        if object_id in {event.get("id1"), event.get("id2")}:
            lags.append(event["time_myr"] - event_time)
    return min(lags) if lags else None


def science_merge_row(index, event, frames, hdf5_path, scaling, raw_events, search_window, log_end_time_myr):
    before_meta, after_meta, survivor_id = find_merge_frames(frames, event, search_window)
    if before_meta is None or after_meta is None or survivor_id is None:
        return None
    coal_ids = [event["id1"], event["id2"]]
    consumed_id = next(object_id for object_id in coal_ids if object_id != survivor_id)
    before_full = read_full_frame(hdf5_path, before_meta["key"], scaling)
    after_full = read_full_frame(hdf5_path, after_meta["key"], scaling)
    try:
        progenitors = [body_from_full(before_full, object_id) for object_id in coal_ids]
        remnant = body_from_full(after_full, survivor_id)
    except KeyError:
        return None
    before_cluster = before_meta["cluster"]
    after_cluster = after_meta["cluster"]
    before_center = np.array([before_cluster[name] for name in ["com_x_pc", "com_y_pc", "com_z_pc"]])
    after_center = np.array([after_cluster[name] for name in ["com_x_pc", "com_y_pc", "com_z_pc"]])
    before_vcom = np.array([before_cluster[name] for name in ["vcom_x_kms", "vcom_y_kms", "vcom_z_kms"]])
    after_vcom = np.array([after_cluster[name] for name in ["vcom_x_kms", "vcom_y_kms", "vcom_z_kms"]])
    progenitor_system = system_measurement(progenitors, before_vcom)
    remnant_velocity_cluster = remnant["velocity"] - after_vcom
    progenitor_velocity_cluster = progenitor_system["velocity"] - before_vcom
    remnant_speed = float(np.linalg.norm(remnant_velocity_cluster))
    velocity_change = float(np.linalg.norm(remnant_velocity_cluster - progenitor_velocity_cluster))
    remnant_vesc = spherical_escape_speed(after_full, remnant["position"], after_center, [survivor_id])
    pre_total_mass = progenitor_system["mass"]
    mass_change = remnant["mass"] - pre_total_mass
    before_radius = float(np.linalg.norm(progenitor_system["position"] - before_center))
    after_radius = float(np.linalg.norm(remnant["position"] - after_center))
    escape_lag = first_single_escape_lag(raw_events, event["time_myr"], survivor_id)
    return {
        "exchange_index": index,
        "event_type": "MERGE",
        "event_time_nbody": event["time_nbody"],
        "event_time_myr": event["time_myr"],
        "diagnostic_start_time_myr": event["time_myr"],
        "diagnostic_end_time_myr": event["time_myr"],
        "raw_diagnostic_count": 1,
        "before_snapshot_key": before_meta["key"],
        "after_snapshot_key": after_meta["key"],
        "before_snapshot_time_myr": before_meta["time_myr"],
        "after_snapshot_time_myr": after_meta["time_myr"],
        "before_snapshot_offset_myr": before_meta["time_myr"] - event["time_myr"],
        "after_snapshot_offset_myr": after_meta["time_myr"] - event["time_myr"],
        "before_pair": format_pair(coal_ids),
        "sigma_1d_before_kms": before_cluster["sigma_1d_number_kms"],
        "sigma_1d_after_kms": after_cluster["sigma_1d_number_kms"],
        "coal_id1": coal_ids[0],
        "coal_id2": coal_ids[1],
        "merge_survivor_id": survivor_id,
        "merge_consumed_id": consumed_id,
        "merge_identification_status": "resolved_by_hdf5_id_continuity",
        "merge_pre_mass1_msun": progenitors[0]["mass"],
        "merge_pre_mass2_msun": progenitors[1]["mass"],
        "merge_pre_total_mass_msun": pre_total_mass,
        "merge_remnant_mass_msun": remnant["mass"],
        "merge_mass_change_msun": mass_change,
        "merge_mass_change_fraction": safe_ratio(mass_change, pre_total_mass),
        "merge_pre_com_speed_kms": progenitor_system["speed_cluster"],
        "merge_remnant_speed_kms": remnant_speed,
        "merge_velocity_change_kms": velocity_change,
        "merge_remnant_vesc_kms": remnant_vesc,
        "merge_remnant_v_over_sigma": safe_ratio(remnant_speed, after_cluster["sigma_1d_number_kms"]),
        "merge_remnant_v_over_vesc": safe_ratio(remnant_speed, remnant_vesc),
        "merge_radius_before_pc": before_radius,
        "merge_radius_after_pc": after_radius,
        "merge_r_over_rh_before": safe_ratio(before_radius, before_cluster.get("half_mass_radius_pc")),
        "merge_r_over_rh_after": safe_ratio(after_radius, after_cluster.get("half_mass_radius_pc")),
        "cluster_half_mass_radius_before_pc": before_cluster.get("half_mass_radius_pc"),
        "cluster_half_mass_radius_after_pc": after_cluster.get("half_mass_radius_pc"),
        "merge_remnant_escape_recorded": escape_lag is not None,
        "merge_remnant_first_escape_lag_myr": escape_lag,
        "escape_log_search_end_myr": log_end_time_myr,
    }


def clean_csv_value(value):
    if value is None:
        return ""
    if isinstance(value, float) and not math.isfinite(value):
        return ""
    return value


def write_csv(path, columns, rows):
    with open(path, "w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=columns, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow({name: clean_csv_value(row.get(name, "")) for name in columns})


def transfer_to_quest(paths, package_name):
    if not re.fullmatch(r"[A-Za-z0-9_.]+", package_name):
        raise ValueError("Invalid Android package name")
    if shutil.which("adb") is None:
        raise RuntimeError("adb is unavailable. Copy the generated folder with SideQuest instead.")
    dataset_dir = os.path.dirname(paths[0])
    folder_name = os.path.basename(dataset_dir)
    target_dir = f"/sdcard/Android/data/{package_name}/files/datasets"
    subprocess.run(["adb", "shell", "mkdir", "-p", target_dir], check=True)
    destination = target_dir + "/" + folder_name
    exists = subprocess.run(["adb", "shell", "test", "-e", destination]).returncode == 0
    if exists:
        answer = input("Quest dataset already exists. Keep a backup and replace? (y/n) [default: n]: ").strip().lower()
        if answer != "y":
            print("Transfer cancelled. Local dataset is ready.")
            return
        backup = destination + "_backup_" + datetime.now().strftime("%Y%m%d_%H%M%S_%f")
    staging = destination + "_uploading"
    if subprocess.run(["adb", "shell", "test", "-e", staging]).returncode == 0:
        raise RuntimeError("An unfinished Quest upload exists: " + staging + ". Move it aside with SideQuest first.")
    subprocess.run(["adb", "push", dataset_dir, staging], check=True)
    if exists:
        subprocess.run(["adb", "shell", "mv", destination, backup], check=True)
    subprocess.run(["adb", "shell", "mv", staging, destination], check=True)
    print("Dataset transferred: " + destination)


def choose_hdf5(data_dir, explicit):
    if explicit:
        path = explicit if os.path.isabs(explicit) else os.path.join(data_dir, explicit)
        if os.path.exists(path):
            return path
        raise FileNotFoundError(path)
    for name in ["snapdata.hdf5", "snapsdata.hdf5"]:
        path = os.path.join(data_dir, name)
        if os.path.exists(path):
            return path
    raise FileNotFoundError("snapdata.hdf5 or snapsdata.hdf5 was not found")


def input_value(prompt, default, cast):
    value = input(f"{prompt} [default: {default}]: ").strip()
    return cast(value) if value else cast(default)


def ask_create_dataset():
    print("")
    print("Choose what to do:")
    print("  [Y] Create the visualization dataset")
    print("  [N] Check the input data format only")
    while True:
        answer = input("Enter Y or N [default: Y]: ").strip().lower()
        if answer in ("", "y", "yes"):
            return True
        if answer in ("n", "no"):
            return False
        print("Please enter Y or N.")


def resolve_path_from_script(value):
    expanded = os.path.expanduser(value)
    if os.path.isabs(expanded):
        return os.path.abspath(expanded)
    script_dir = os.path.dirname(os.path.abspath(__file__))
    return os.path.abspath(os.path.join(script_dir, expanded))


def resolve_arguments():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--data-dir",
        help="Snapshot directory (.hdf5); defaults to .",
    )
    parser.add_argument("--log-dir", help="Event log directory; defaults to .")
    parser.add_argument("--output-dir")
    parser.add_argument("--hdf5", default="")
    parser.add_argument("--package-name", default="com.kawashima.bhvisualizer")
    parser.add_argument("--bh-type", type=int, default=14)
    parser.add_argument("--mass-conv", type=float)
    parser.add_argument("--length-conv", type=float)
    parser.add_argument("--velocity-conv", type=float)
    parser.add_argument("--time-conv", type=float)
    parser.add_argument("--workers", type=int, default=0)
    parser.add_argument("--no-transfer", action="store_true")
    parser.add_argument("--check-only", action="store_true")
    parser.add_argument("--force", action="store_true")
    parser.add_argument("--science-search-window", type=float, default=50.0)
    parser.add_argument("--exchange-group-tolerance", type=float, default=0.001)
    parser.add_argument("--composite-mass-tolerance", type=float, default=0.02)
    parser.add_argument("--max-time-myr", type=float)
    args = parser.parse_args()
    interactive = args.data_dir is None
    if args.data_dir is None:
        args.data_dir = input("Enter snapshot directory (.hdf5) [default: .]: ").strip() or "."
    if args.log_dir is None:
        args.log_dir = (
            input("Enter event log directory (*_output.dat or output.dat) [default: .]: ").strip()
            or "."
        ) if interactive else "."
    if args.output_dir is None:
        args.output_dir = input("Dataset folder [default: NBODYVR_Data]: ").strip() or "NBODYVR_Data"
        if not args.check_only:
            args.check_only = not ask_create_dataset()
    return args


def validate_input(hdf5_path, scaling):
    if any(not math.isfinite(value) or value <= 0 for value in scaling.values()):
        raise ValueError("Scaling factors must be finite and positive.")
    with h5py.File(hdf5_path, "r") as handle:
        keys = sorted([key for key in handle if key.startswith("snap_")], key=snapshot_number)
        if not keys:
            raise ValueError("No snap_* snapshots were found in the HDF5 file.")
        for key in keys:
            data = handle[key]
            required = ["id", "m", "kw", "x", "y", "z", "vx", "vy", "vz", "t"]
            missing = [name for name in required if name not in data]
            if missing:
                raise ValueError(f"{key}: missing datasets: {', '.join(missing)}")
            count = data["id"].size
            if not count or any(data[name].size != count for name in required if name != "t"):
                raise ValueError(f"{key}: inconsistent particle array sizes")
            if data["t"].size != 1 or not math.isfinite(float(np.asarray(data["t"][()]).reshape(-1)[0])):
                raise ValueError(f"{key}: invalid snapshot time")
    print(f"Input structure OK: {len(keys)} snapshots. Particle values are checked during conversion.")
    return keys


def select_snapshot_keys(hdf5_path, keys, time_conv, max_time_myr):
    if max_time_myr is None:
        return keys
    if not math.isfinite(max_time_myr) or max_time_myr < 0:
        raise ValueError("Maximum time must be a finite, non-negative value in Myr.")
    selected = []
    with h5py.File(hdf5_path, "r") as handle:
        for key in keys:
            time_nbody = float(np.asarray(handle[key]["t"][()]).reshape(-1)[0])
            if time_nbody * time_conv <= max_time_myr + 1e-9:
                selected.append(key)
    if not selected:
        raise ValueError(f"No snapshots were found at or before {max_time_myr:g} Myr.")
    return selected


def limit_logs_to_time(logs, max_time_myr, time_conv):
    if max_time_myr is None:
        return logs
    logs["raw_events"] = [row for row in logs["raw_events"] if row["time_myr"] <= max_time_myr + 1e-9]
    logs["cluster_log"] = [row for row in logs["cluster_log"] if row["time_myr"] <= max_time_myr + 1e-9]
    logs["maximum_time_myr"] = min(logs["maximum_time_myr"], max_time_myr)
    logs["maximum_time_nbody"] = logs["maximum_time_myr"] / time_conv
    return logs


def main():
    args = resolve_arguments()
    data_dir = resolve_path_from_script(args.data_dir)
    log_dir = resolve_path_from_script(args.log_dir)
    output_dir = resolve_path_from_script(args.output_dir)
    print(f"Input directory: {data_dir}")
    print(f"Dataset output: {output_dir}")
    destination_dir = output_dir
    if not re.fullmatch(r"[A-Za-z0-9_-]+", os.path.basename(destination_dir)):
        raise ValueError("Use letters, digits, underscores or hyphens for the dataset folder name.")
    output_files = find_output_files(log_dir)
    hdf5_path = choose_hdf5(data_dir, args.hdf5)
    for input_dir in (data_dir, log_dir, os.path.dirname(hdf5_path)):
        if os.path.commonpath([os.path.realpath(destination_dir), os.path.realpath(input_dir)]) == os.path.realpath(destination_dir):
            raise ValueError("The dataset folder must not contain or equal an NBODY input folder.")
    print(f"HDF5 file: {hdf5_path}")
    print("Log files to read:")
    for path in output_files:
        print(f"  {path}")
    if input("Are these files correct? (y/n) [default: n]: ").strip().lower() not in ("y", "yes"):
        print("Cancelled.")
        return
    detected = parse_scaling(output_files)
    scaling = {
        "mass": args.mass_conv if args.mass_conv is not None else detected["mass"],
        "length": args.length_conv if args.length_conv is not None else detected["length"],
        "velocity": args.velocity_conv if args.velocity_conv is not None else detected["velocity"],
        "time": args.time_conv if args.time_conv is not None else detected["time"],
    }
    print(f"Scaling source: {detected['source']}")
    print(
        f"R*={scaling['length']} pc, M*={scaling['mass']} M_sun, "
        f"V*={scaling['velocity']} km/s, T*={scaling['time']} Myr"
    )
    keys = validate_input(hdf5_path, scaling)
    keys = select_snapshot_keys(hdf5_path, keys, scaling["time"], args.max_time_myr)
    logs = parse_logs(output_files, args.bh_type, scaling["time"])
    logs = limit_logs_to_time(logs, args.max_time_myr, scaling["time"])
    if args.check_only:
        print(f"Logs readable: {len(logs['output_files'])} files; {len(logs['raw_events'])} parsed event records.")
        print("Check complete. No CSV files were written or transferred.")
        return
    if os.path.exists(destination_dir) and not args.force:
        if input("Dataset folder exists. Keep a backup and replace? (y/n) [default: n]: ").strip().lower() != "y":
            print("Cancelled; existing files were not changed.")
            return
    os.makedirs(os.path.dirname(destination_dir), exist_ok=True)
    output_dir = tempfile.mkdtemp(prefix=".nbodyvr_build_", dir=os.path.dirname(destination_dir))
    groups = group_exchange_events(logs["raw_events"], args.exchange_group_tolerance)
    workers = args.workers if args.workers > 0 else min(4, max(1, (os.cpu_count() or 2) - 1))
    print(f"Processing {len(keys)} snapshots with {workers} workers")
    frames = []
    with Pool(
        processes=workers,
        initializer=initialize_worker,
        initargs=(hdf5_path, scaling, args.bh_type, logs["cluster_log"]),
    ) as pool:
        for index, result in enumerate(pool.imap(process_snapshot_worker, keys, chunksize=1), start=1):
            frames.append(result)
            if index % 50 == 0 or index == len(keys):
                print(f"[{index}/{len(keys)}] Completed")
    frames.sort(key=lambda frame: frame["time_myr"])
    if not any(frame["records"] for frame in frames):
        raise ValueError("No BH records found. Check the stellar type and HDF5 format.")
    repair_count = repair_regularized_history(frames, groups, args.composite_mass_tolerance)
    prepare_exchange_groups(
        frames,
        groups,
        args.science_search_window,
        args.composite_mass_tolerance,
    )
    event_rows = public_event_rows(logs["raw_events"], groups)
    science_rows = []
    skipped_science = []
    exchange_group_diagnostics = []
    for index, group in enumerate(groups):
        diagnostic = {
            "exchange_index": index,
            "event_time_nbody": group["time_nbody"],
            "event_time_myr": group["time_myr"],
            "event_type": group["event_type"],
            "initial_pair": list(group["initial_pair"]),
            "final_pair": list(group["final_pair"]),
            "raw_transitions": [
                {
                    "before_pair": list(transition["before_pair"]),
                    "after_pair": list(transition["after_pair"]),
                }
                for transition in group["raw_transitions"]
            ],
            "raw_diagnostic_count": group["raw_diagnostic_count"],
            "temporary_ids": group["temporary_ids"],
        }
        if group["event_type"] == "TRANSIENT EXCHANGE":
            row = science_transient_row(index, group, hdf5_path, scaling)
        else:
            row = science_exchange_row(
                index,
                group,
                hdf5_path,
                scaling,
                logs["raw_events"],
                logs["maximum_time_myr"],
            )
        if row is None:
            reasons = [
                reason
                for reason in [group.get("before_failure_reason"), group.get("after_failure_reason")]
                if reason
            ]
            reason = "; ".join(reasons) or "net incoming and outgoing participants could not be resolved uniquely"
            skipped_science.append(
                {
                    "exchange_index": index,
                    "event_time_myr": group["time_myr"],
                    "participant_ids": sorted(group["participant_ids"]),
                    "reason": reason,
                }
            )
            diagnostic["science_status"] = "unavailable"
            diagnostic["science_reason"] = reason
        else:
            science_rows.append(row)
            diagnostic["science_status"] = "available"
            diagnostic["science_reason"] = ""
        exchange_group_diagnostics.append(diagnostic)
    skipped_merge_science = []
    merge_events = [event for event in logs["raw_events"] if event["event_list"] == "MERGE"]
    for merge_offset, event in enumerate(merge_events):
        row = science_merge_row(
            len(groups) + merge_offset,
            event,
            frames,
            hdf5_path,
            scaling,
            logs["raw_events"],
            args.science_search_window,
            logs["maximum_time_myr"],
        )
        if row is not None:
            science_rows.append(row)
        else:
            skipped_merge_science.append(
                {
                    "event_time_myr": event["time_myr"],
                    "coal_ids": [event.get("id1"), event.get("id2")],
                    "reason": (
                        "could not resolve both COAL IDs before the event and exactly one surviving "
                        "COAL ID after it within the science search window"
                    ),
                }
            )
    science_rows.sort(key=lambda row: (row["event_time_myr"], row["event_type"]))
    history_rows = [record for frame in frames for record in frame["records"]]
    cluster_rows = [frame["cluster"] for frame in frames]
    paths = {
        "history": os.path.join(output_dir, "bh_history.csv"),
        "events": os.path.join(output_dir, "bh_events.csv"),
        "science_exchange": os.path.join(output_dir, "science_events.csv"),
        "science_cluster": os.path.join(output_dir, "science_cluster_dynamics.csv"),
        "report": os.path.join(output_dir, "nbody_pipeline_report.json"),
    }
    write_csv(paths["history"], HISTORY_COLUMNS, history_rows)
    write_csv(paths["events"], EVENT_COLUMNS, event_rows)
    write_csv(paths["science_exchange"], EXCHANGE_COLUMNS, science_rows)
    write_csv(paths["science_cluster"], CLUSTER_COLUMNS, cluster_rows)
    report = {
        "hdf5": hdf5_path,
        "output_files": [os.path.basename(path) for path in logs["output_files"]],
        "scaling": scaling,
        "snapshot_count": len(frames),
        "bh_history_rows": len(history_rows),
        "raw_exchange_diagnostics": sum(1 for row in logs["raw_events"] if row["event_list"] == "EXCHANGE"),
        "grouped_exchange_events": len(groups),
        "net_exchange_events": sum(1 for group in groups if group["event_type"] == "EXCHANGE"),
        "transient_exchange_events": sum(1 for group in groups if group["event_type"] == "TRANSIENT EXCHANGE"),
        "science_event_rows": len(science_rows),
        "science_exchange_rows": sum(1 for row in science_rows if row["event_type"] == "EXCHANGE"),
        "science_transient_exchange_rows": sum(
            1 for row in science_rows if row["event_type"] == "TRANSIENT EXCHANGE"
        ),
        "science_merge_rows": sum(1 for row in science_rows if row["event_type"] == "MERGE"),
        "regularized_frames_repaired": repair_count,
        "merge_events": sum(1 for row in event_rows if row["event_list"] == "MERGE"),
        "coal_lines_found": logs["coal_lines_found"],
        "coal_lines_parsed": logs["coal_lines_parsed"],
        "skipped_science": skipped_science,
        "skipped_merge_science": skipped_merge_science,
        "exchange_group_diagnostics": exchange_group_diagnostics,
        "outputs": paths,
    }
    with open(paths["report"], "w", encoding="utf-8") as stream:
        json.dump(report, stream, indent=2)
    manifest = {
        "format_version": 1,
        "dataset_name": os.path.basename(destination_dir),
        "created_utc": datetime.now(timezone.utc).isoformat(),
        "source_hdf5": os.path.basename(hdf5_path),
        "history_file": "bh_history.csv", "events_file": "bh_events.csv",
        "science_events_file": "science_events.csv",
        "science_cluster_file": "science_cluster_dynamics.csv",
        "time_unit": "Myr", "length_unit": "pc", "mass_unit": "M_sun", "velocity_unit": "km/s",
        "scaling": scaling, "snapshot_count": len(frames),
        "bh_count": len({record["id"] for record in history_rows}),
        "event_count": len(event_rows), "science_available": bool(science_rows),
        "unavailable_science_count": len(skipped_science) + len(skipped_merge_science),
        "warnings": [item["reason"] for item in skipped_science + skipped_merge_science]
    }
    with open(os.path.join(output_dir, "manifest.json"), "w", encoding="utf-8") as stream:
        json.dump(manifest, stream, indent=2)
    if os.path.exists(destination_dir):
        backup = destination_dir + "_backup_" + datetime.now().strftime("%Y%m%d_%H%M%S_%f")
        os.rename(destination_dir, backup)
        print("Previous dataset retained: " + backup)
    os.rename(output_dir, destination_dir)
    paths = {key: os.path.join(destination_dir, os.path.basename(path)) for key, path in paths.items()}
    report["outputs"] = paths
    with open(paths["report"], "w", encoding="utf-8") as stream:
        json.dump(report, stream, indent=2)
    print(f"Ready: {destination_dir}\n{len(frames)} snapshots; {manifest['bh_count']} BH IDs; {len(event_rows)} events; {len(science_rows)} detailed science records.")
    print(f"Unavailable detailed records: {manifest['unavailable_science_count']}. See nbody_pipeline_report.json.")
    if not args.no_transfer:
        answer = input("Transfer this dataset folder to Quest? (y/n) [default: y]: ").strip() or "y"
        if answer.lower() == "y":
            transfer_to_quest(
                [paths["history"], paths["events"], paths["science_exchange"], paths["science_cluster"]],
                args.package_name,
            )


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, RuntimeError, subprocess.CalledProcessError) as error:
        print(f"NBODYVR: {error}\nInput files are unchanged. Check the paths and input format, then retry.", file=sys.stderr)
        sys.exit(1)

