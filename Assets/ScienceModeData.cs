using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public sealed class ScienceExchangeRecord
{
    public int ExchangeIndex;
    public string EventType;
    public double EventTimeNbody;
    public double EventTimeMyr;
    public double? DiagnosticStartTimeMyr;
    public double? DiagnosticEndTimeMyr;
    public int RawDiagnosticCount;
    public string RawTransitions;
    public int[] TemporaryIds = new int[0];
    public string BeforeSnapshotKey;
    public string AfterSnapshotKey;
    public double? BeforeSnapshotTimeMyr;
    public double? AfterSnapshotTimeMyr;
    public double? BeforeSnapshotOffsetMyr;
    public double? AfterSnapshotOffsetMyr;
    public int BeforeId1;
    public int BeforeId2;
    public int AfterId1;
    public int AfterId2;
    public int IncomingId;
    public int OutgoingId;
    public double? SigmaBefore;
    public double? SigmaAfter;
    public double? BinarySpeedBefore;
    public double? BinarySpeedAfter;
    public double? SingleSpeedBefore;
    public double? SingleSpeedAfter;
    public double? BinarySingleRelativeSpeedBefore;
    public double? BinarySingleRelativeSpeedAfter;
    public double? BinaryEscapeBefore;
    public double? BinaryEscapeAfter;
    public double? SingleEscapeBefore;
    public double? SingleEscapeAfter;
    public double? BinarySigmaRatioAfter;
    public double? SingleSigmaRatioAfter;
    public double? BinaryEscapeRatioAfter;
    public double? SingleEscapeRatioAfter;
    public double? EccentricityBefore;
    public double? EccentricityAfter;
    public double? SemimajorAxisBeforeAu;
    public double? SemimajorAxisAfterAu;
    public double? PericenterBeforeAu;
    public double? PericenterAfterAu;
    public double? BeforeMass1;
    public double? BeforeMass2;
    public double? IncomingMass;
    public double? AfterMass1;
    public double? AfterMass2;
    public double? OutgoingMass;
    public double? BindingEnergyBefore;
    public double? BindingEnergyAfter;
    public double? BindingEnergyRelease;
    public bool BindingEnergyReleasePositive;
    public double? BindingEnergyLogBefore;
    public double? BindingEnergyLogAfter;
    public string BindingEnergyPrimarySource;
    public double? BindingEnergyFractionalChange;
    public double? InteractionComRadiusBeforePc;
    public double? InteractionComRadiusAfterPc;
    public double? ClusterHalfMassRadiusBeforePc;
    public double? ClusterHalfMassRadiusAfterPc;
    public double? InteractionComHalfMassRatioBefore;
    public double? InteractionComHalfMassRatioAfter;
    public double? BinaryRadiusTidalRatioBefore;
    public double? BinaryRadiusTidalRatioAfter;
    public double? SingleRadiusTidalRatioBefore;
    public double? SingleRadiusTidalRatioAfter;
    public double? MeasuredBinaryRecoil;
    public double? MeasuredOutgoingRecoil;
    public double? PredictedBinaryRecoil;
    public double? PredictedOutgoingRecoil;
    public double? MeasuredPredictedBinaryRatio;
    public double? MeasuredPredictedOutgoingRatio;
    public double? KineticBindingRatio;
    public double? ChainEscapeVelocity;
    public double? ChainBinaryVelocity;
    public bool OutgoingPromptEscape;
    public bool BinaryPromptEscape;
    public double? OutgoingEscapeLagMyr;
    public double? BinaryEscapeLagMyr;
    public bool OutgoingEscapeRecorded;
    public bool BinaryExactEscapeRecorded;
    public double? OutgoingFirstEscapeLagMyr;
    public double? BinaryFirstEscapeLagMyr;
    public double? EscapeLogSearchEndMyr;
    public int CoalId1;
    public int CoalId2;
    public int MergeSurvivorId;
    public int MergeConsumedId;
    public string MergeIdentificationStatus;
    public double? MergePreMass1;
    public double? MergePreMass2;
    public double? MergePreTotalMass;
    public double? MergeRemnantMass;
    public double? MergeMassChange;
    public double? MergeMassChangeFraction;
    public double? MergePreComSpeed;
    public double? MergeRemnantSpeed;
    public double? MergeVelocityChange;
    public double? MergeRemnantEscape;
    public double? MergeRemnantSigmaRatio;
    public double? MergeRemnantEscapeRatio;
    public double? MergeRadiusBeforePc;
    public double? MergeRadiusAfterPc;
    public double? MergeHalfMassRatioBefore;
    public double? MergeHalfMassRatioAfter;
    public bool MergeRemnantEscapeRecorded;
    public double? MergeRemnantFirstEscapeLagMyr;

    public bool BeforePairMatches(int id1, int id2)
    {
        return PairMatches(BeforeId1, BeforeId2, id1, id2);
    }

    public bool AfterPairMatches(int id1, int id2)
    {
        return PairMatches(AfterId1, AfterId2, id1, id2);
    }

    static bool PairMatches(int a1, int a2, int b1, int b2)
    {
        return (a1 == b1 && a2 == b2) || (a1 == b2 && a2 == b1);
    }
}

public sealed class ClusterScienceRecord
{
    public string SnapshotKey;
    public double TimeNbody;
    public double TimeMyr;
    public double? TidalRadiusPc;
    public double? HalfMassRadiusPc;
    public Vector3 CenterPc;
    public Vector3 CenterVelocityKms;
    public double? Vrms3dKms;
    public double? Sigma1dKms;
    public bool QualityOk;
    public string QualityReason;
}

public sealed class ScienceModeDataStore
{
    readonly List<ScienceExchangeRecord> exchangeRecords = new List<ScienceExchangeRecord>();
    readonly List<ClusterScienceRecord> clusterByNbody = new List<ClusterScienceRecord>();
    readonly List<ClusterScienceRecord> clusterByMyr = new List<ClusterScienceRecord>();

    public int ExchangeCount { get { return exchangeRecords.Count; } }
    public int ClusterCount { get { return clusterByNbody.Count; } }

    public void LoadExchange(string[] lines)
    {
        exchangeRecords.Clear();
        if (lines == null || lines.Length < 2) return;

        Dictionary<string, int> header = BuildHeader(lines[0]);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] values = lines[i].Split(',');
            ScienceExchangeRecord record = new ScienceExchangeRecord();
            record.ExchangeIndex = ReadInt(header, values, "exchange_index");
            record.EventType = ReadString(header, values, "event_type");
            if (string.IsNullOrWhiteSpace(record.EventType)) record.EventType = "EXCHANGE";
            record.EventTimeNbody = ReadDouble(header, values, "event_time_nbody") ?? double.NaN;
            record.EventTimeMyr = ReadDouble(header, values, "event_time_myr") ?? double.NaN;
            record.DiagnosticStartTimeMyr = ReadDouble(header, values, "diagnostic_start_time_myr");
            record.DiagnosticEndTimeMyr = ReadDouble(header, values, "diagnostic_end_time_myr");
            record.RawDiagnosticCount = ReadInt(header, values, "raw_diagnostic_count");
            record.RawTransitions = ReadString(header, values, "raw_transitions");
            record.TemporaryIds = ReadIntList(ReadString(header, values, "temporary_ids"));
            record.BeforeSnapshotKey = ReadString(header, values, "before_snapshot_key");
            record.AfterSnapshotKey = ReadString(header, values, "after_snapshot_key");
            record.BeforeSnapshotTimeMyr = ReadDouble(header, values, "before_snapshot_time_myr");
            record.AfterSnapshotTimeMyr = ReadDouble(header, values, "after_snapshot_time_myr");
            record.BeforeSnapshotOffsetMyr = ReadDouble(header, values, "before_snapshot_offset_myr");
            record.AfterSnapshotOffsetMyr = ReadDouble(header, values, "after_snapshot_offset_myr");
            ReadPair(ReadString(header, values, "before_pair"), out record.BeforeId1, out record.BeforeId2);
            ReadPair(ReadString(header, values, "after_pair"), out record.AfterId1, out record.AfterId2);
            record.IncomingId = ReadInt(header, values, "incoming_id");
            record.OutgoingId = ReadInt(header, values, "outgoing_id");
            record.SigmaBefore = ReadDouble(header, values, "sigma_1d_before_kms");
            record.SigmaAfter = ReadDouble(header, values, "sigma_1d_after_kms");
            record.BinarySpeedBefore = ReadDouble(header, values, "binary_speed_before_kms");
            record.BinarySpeedAfter = ReadDouble(header, values, "binary_speed_after_kms");
            record.SingleSpeedBefore = ReadDouble(header, values, "incoming_speed_before_kms");
            record.SingleSpeedAfter = ReadDouble(header, values, "outgoing_speed_after_kms");
            record.BinarySingleRelativeSpeedBefore = ReadDouble(header, values, "binary_single_relative_speed_before_kms");
            record.BinarySingleRelativeSpeedAfter = ReadDouble(header, values, "binary_single_relative_speed_after_kms");
            record.BinaryEscapeBefore = ReadDouble(header, values, "binary_vesc_before_kms");
            record.BinaryEscapeAfter = ReadDouble(header, values, "binary_vesc_after_kms");
            record.SingleEscapeBefore = ReadDouble(header, values, "incoming_vesc_before_kms");
            record.SingleEscapeAfter = ReadDouble(header, values, "outgoing_vesc_after_kms");
            record.BinarySigmaRatioAfter = ReadDouble(header, values, "binary_v_over_sigma_after");
            record.SingleSigmaRatioAfter = ReadDouble(header, values, "outgoing_v_over_sigma_after");
            record.BinaryEscapeRatioAfter = ReadDouble(header, values, "binary_v_over_vesc_after");
            record.SingleEscapeRatioAfter = ReadDouble(header, values, "outgoing_v_over_vesc_after");
            record.EccentricityBefore = ReadDouble(header, values, "e_before_log");
            record.EccentricityAfter = ReadDouble(header, values, "e_after_log");
            record.SemimajorAxisBeforeAu = ReadDouble(header, values, "a_before_au_log");
            record.SemimajorAxisAfterAu = ReadDouble(header, values, "a_after_au_log");
            record.PericenterBeforeAu = ReadDouble(header, values, "pericenter_before_au");
            record.PericenterAfterAu = ReadDouble(header, values, "pericenter_after_au");
            record.BeforeMass1 = ReadDouble(header, values, "before_m1_msun");
            record.BeforeMass2 = ReadDouble(header, values, "before_m2_msun");
            record.IncomingMass = ReadDouble(header, values, "incoming_mass_msun");
            record.AfterMass1 = ReadDouble(header, values, "after_m1_msun");
            record.AfterMass2 = ReadDouble(header, values, "after_m2_msun");
            record.OutgoingMass = ReadDouble(header, values, "outgoing_mass_msun");
            record.BindingEnergyBefore = ReadDouble(header, values, "binding_energy_before_msun_kms2");
            record.BindingEnergyAfter = ReadDouble(header, values, "binding_energy_after_msun_kms2");
            record.BindingEnergyRelease = ReadDouble(header, values, "binding_energy_release_msun_kms2");
            record.BindingEnergyReleasePositive = ReadBool(header, values, "binding_energy_release_positive");
            record.BindingEnergyLogBefore = ReadDouble(header, values, "binding_energy_log_before_msun_kms2");
            record.BindingEnergyLogAfter = ReadDouble(header, values, "binding_energy_log_after_msun_kms2");
            record.BindingEnergyPrimarySource = ReadString(header, values, "binding_energy_primary_source");
            record.BindingEnergyFractionalChange = ReadDouble(header, values, "binding_energy_fractional_change");
            record.InteractionComRadiusBeforePc = ReadDouble(header, values, "interaction_com_radius_before_pc");
            record.InteractionComRadiusAfterPc = ReadDouble(header, values, "interaction_com_radius_after_pc");
            record.ClusterHalfMassRadiusBeforePc = ReadDouble(header, values, "cluster_half_mass_radius_before_pc");
            record.ClusterHalfMassRadiusAfterPc = ReadDouble(header, values, "cluster_half_mass_radius_after_pc");
            record.InteractionComHalfMassRatioBefore = ReadDouble(header, values, "interaction_com_r_over_rh_before");
            record.InteractionComHalfMassRatioAfter = ReadDouble(header, values, "interaction_com_r_over_rh_after");
            record.BinaryRadiusTidalRatioBefore = ReadDouble(header, values, "binary_r_over_rtide_before");
            record.BinaryRadiusTidalRatioAfter = ReadDouble(header, values, "binary_r_over_rtide_after");
            record.SingleRadiusTidalRatioBefore = ReadDouble(header, values, "single_r_over_rtide_before");
            record.SingleRadiusTidalRatioAfter = ReadDouble(header, values, "single_r_over_rtide_after");
            record.MeasuredBinaryRecoil = ReadDouble(header, values, "binary_recoil_after_triple_com_kms");
            record.MeasuredOutgoingRecoil = ReadDouble(header, values, "outgoing_speed_after_triple_com_kms");
            record.PredictedBinaryRecoil = ReadDouble(header, values, "predicted_binary_recoil_kms");
            record.PredictedOutgoingRecoil = ReadDouble(header, values, "predicted_outgoing_recoil_kms");
            record.MeasuredPredictedBinaryRatio = ReadDouble(header, values, "measured_over_predicted_binary");
            record.MeasuredPredictedOutgoingRatio = ReadDouble(header, values, "measured_over_predicted_outgoing");
            record.KineticBindingRatio = ReadDouble(header, values, "kinetic_change_over_binding_release");
            record.ChainEscapeVelocity = ReadDouble(header, values, "chain_escape_vf_log_kms");
            record.ChainBinaryVelocity = ReadDouble(header, values, "chain_binary_vcm_log_kms");
            record.OutgoingPromptEscape = ReadBool(header, values, "outgoing_prompt_cluster_escape");
            record.BinaryPromptEscape = ReadBool(header, values, "binary_prompt_cluster_escape");
            record.OutgoingEscapeLagMyr = ReadDouble(header, values, "outgoing_first_cluster_escape_lag_myr");
            record.BinaryEscapeLagMyr = ReadDouble(header, values, "binary_first_exact_escape_lag_myr");
            record.OutgoingEscapeRecorded = ReadBool(header, values, "outgoing_escape_recorded");
            record.BinaryExactEscapeRecorded = ReadBool(header, values, "binary_exact_escape_recorded");
            record.OutgoingFirstEscapeLagMyr = ReadDouble(header, values, "outgoing_first_escape_lag_myr");
            record.BinaryFirstEscapeLagMyr = ReadDouble(header, values, "binary_first_escape_lag_myr");
            record.EscapeLogSearchEndMyr = ReadDouble(header, values, "escape_log_search_end_myr");
            record.CoalId1 = ReadInt(header, values, "coal_id1");
            record.CoalId2 = ReadInt(header, values, "coal_id2");
            record.MergeSurvivorId = ReadInt(header, values, "merge_survivor_id");
            record.MergeConsumedId = ReadInt(header, values, "merge_consumed_id");
            record.MergeIdentificationStatus = ReadString(header, values, "merge_identification_status");
            record.MergePreMass1 = ReadDouble(header, values, "merge_pre_mass1_msun");
            record.MergePreMass2 = ReadDouble(header, values, "merge_pre_mass2_msun");
            record.MergePreTotalMass = ReadDouble(header, values, "merge_pre_total_mass_msun");
            record.MergeRemnantMass = ReadDouble(header, values, "merge_remnant_mass_msun");
            record.MergeMassChange = ReadDouble(header, values, "merge_mass_change_msun");
            record.MergeMassChangeFraction = ReadDouble(header, values, "merge_mass_change_fraction");
            record.MergePreComSpeed = ReadDouble(header, values, "merge_pre_com_speed_kms");
            record.MergeRemnantSpeed = ReadDouble(header, values, "merge_remnant_speed_kms");
            record.MergeVelocityChange = ReadDouble(header, values, "merge_velocity_change_kms");
            record.MergeRemnantEscape = ReadDouble(header, values, "merge_remnant_vesc_kms");
            record.MergeRemnantSigmaRatio = ReadDouble(header, values, "merge_remnant_v_over_sigma");
            record.MergeRemnantEscapeRatio = ReadDouble(header, values, "merge_remnant_v_over_vesc");
            record.MergeRadiusBeforePc = ReadDouble(header, values, "merge_radius_before_pc");
            record.MergeRadiusAfterPc = ReadDouble(header, values, "merge_radius_after_pc");
            record.MergeHalfMassRatioBefore = ReadDouble(header, values, "merge_r_over_rh_before");
            record.MergeHalfMassRatioAfter = ReadDouble(header, values, "merge_r_over_rh_after");
            record.MergeRemnantEscapeRecorded = ReadBool(header, values, "merge_remnant_escape_recorded");
            record.MergeRemnantFirstEscapeLagMyr = ReadDouble(header, values, "merge_remnant_first_escape_lag_myr");
            if (!double.IsNaN(record.EventTimeNbody)) exchangeRecords.Add(record);
        }
    }

    public void LoadCluster(string[] lines)
    {
        clusterByNbody.Clear();
        clusterByMyr.Clear();
        if (lines == null || lines.Length < 2) return;

        Dictionary<string, int> header = BuildHeader(lines[0]);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] values = lines[i].Split(',');
            double? timeNbody = ReadDouble(header, values, "time_nbody");
            double? timeMyr = ReadDouble(header, values, "time_myr");
            if (!timeNbody.HasValue || !timeMyr.HasValue) continue;

            ClusterScienceRecord record = new ClusterScienceRecord();
            record.SnapshotKey = ReadString(header, values, "snapshot_key");
            record.TimeNbody = timeNbody.Value;
            record.TimeMyr = timeMyr.Value;
            record.TidalRadiusPc = ReadDouble(header, values, "rtide_pc");
            record.HalfMassRadiusPc = ReadDouble(header, values, "half_mass_radius_pc");
            record.CenterPc = ReadVector(header, values, "com_x_pc", "com_y_pc", "com_z_pc");
            record.CenterVelocityKms = ReadVector(header, values, "vcom_x_kms", "vcom_y_kms", "vcom_z_kms");
            record.Vrms3dKms = ReadDouble(header, values, "vrms_3d_number_kms");
            record.Sigma1dKms = ReadDouble(header, values, "sigma_1d_number_kms");
            record.QualityOk = ReadBool(header, values, "analysis_quality_ok");
            record.QualityReason = ReadString(header, values, "analysis_quality_reason");
            clusterByNbody.Add(record);
            clusterByMyr.Add(record);
        }

        clusterByNbody.Sort((a, b) => a.TimeNbody.CompareTo(b.TimeNbody));
        clusterByMyr.Sort((a, b) => a.TimeMyr.CompareTo(b.TimeMyr));
    }

    public ScienceExchangeRecord FindExchange(
        InteractionEvent interaction,
        double tolerance,
        bool interactionTimeIsNbody)
    {
        if (interaction == null || interaction.type != "EXCHANGE") return null;

        ScienceExchangeRecord signatureMatch = null;
        int signatureScore = int.MinValue;
        double signatureDifference = double.MaxValue;
        foreach (ScienceExchangeRecord record in exchangeRecords)
        {
            if (!string.Equals(record.EventType, "EXCHANGE", StringComparison.OrdinalIgnoreCase)) continue;
            int score = GetExchangeMatchScore(interaction, record);
            if (score < 8) continue;

            double recordTime = interactionTimeIsNbody ? record.EventTimeNbody : record.EventTimeMyr;
            double difference = Math.Abs(recordTime - interaction.time);
            if (score > signatureScore ||
                (score == signatureScore && difference < signatureDifference))
            {
                signatureMatch = record;
                signatureScore = score;
                signatureDifference = difference;
            }
        }

        if (signatureMatch != null) return signatureMatch;

        ScienceExchangeRecord best = null;
        int bestScore = int.MinValue;
        double bestDifference = double.MaxValue;
        double effectiveTolerance = double.IsNaN(tolerance) ||
            double.IsInfinity(tolerance) ||
            tolerance <= 0.0
                ? 0.5
                : Math.Max(tolerance, 0.5);

        foreach (ScienceExchangeRecord record in exchangeRecords)
        {
            if (!string.Equals(record.EventType, "EXCHANGE", StringComparison.OrdinalIgnoreCase)) continue;
            double recordTime = interactionTimeIsNbody ? record.EventTimeNbody : record.EventTimeMyr;
            double difference = Math.Abs(recordTime - interaction.time);
            if (difference > effectiveTolerance) continue;

            int score = GetExchangeMatchScore(interaction, record);

            if (score > bestScore || (score == bestScore && difference < bestDifference))
            {
                best = record;
                bestScore = score;
                bestDifference = difference;
            }
        }
        return best;
    }

    public ScienceExchangeRecord FindTransient(InteractionEvent interaction, double tolerance, bool interactionTimeIsNbody)
    {
        if (interaction == null || interaction.type != "TRANSIENT EXCHANGE") return null;
        return FindTypedRecord(interaction, "TRANSIENT EXCHANGE", tolerance, interactionTimeIsNbody);
    }

    public ScienceExchangeRecord FindMerge(InteractionEvent interaction, double tolerance, bool interactionTimeIsNbody)
    {
        if (interaction == null || interaction.type != "MERGE") return null;
        return FindTypedRecord(interaction, "MERGE", tolerance, interactionTimeIsNbody);
    }

    ScienceExchangeRecord FindTypedRecord(
        InteractionEvent interaction,
        string eventType,
        double tolerance,
        bool interactionTimeIsNbody)
    {
        double effectiveTolerance = double.IsNaN(tolerance) || double.IsInfinity(tolerance) || tolerance <= 0.0
            ? 0.5
            : Math.Max(tolerance, 0.5);
        ScienceExchangeRecord best = null;
        int bestScore = int.MinValue;
        double bestDifference = double.MaxValue;
        foreach (ScienceExchangeRecord record in exchangeRecords)
        {
            if (!string.Equals(record.EventType, eventType, StringComparison.OrdinalIgnoreCase)) continue;
            double recordTime = interactionTimeIsNbody ? record.EventTimeNbody : record.EventTimeMyr;
            double difference = Math.Abs(recordTime - interaction.time);
            if (difference > effectiveTolerance) continue;
            int score = GetTypedMatchScore(interaction, record, eventType);
            if (score > bestScore || (score == bestScore && difference < bestDifference))
            {
                best = record;
                bestScore = score;
                bestDifference = difference;
            }
        }
        return best;
    }

    static int GetTypedMatchScore(InteractionEvent interaction, ScienceExchangeRecord record, string eventType)
    {
        if (interaction.ids == null) return 0;
        int score = 0;
        if (eventType == "MERGE")
        {
            if (interaction.ids.Contains(record.CoalId1)) score += 4;
            if (interaction.ids.Contains(record.CoalId2)) score += 4;
            return score;
        }
        if (interaction.ids.Length >= 2 && record.BeforePairMatches(interaction.ids[0], interaction.ids[1])) score += 8;
        int[] eventTemporaryIds = interaction.temporaryIds ?? new int[0];
        score += eventTemporaryIds.Intersect(record.TemporaryIds ?? new int[0]).Count();
        return score;
    }

    static int GetExchangeMatchScore(InteractionEvent interaction, ScienceExchangeRecord record)
    {
        if (interaction == null || interaction.ids == null) return 0;

        int score = 0;
        if (interaction.ids.Length >= 2 &&
            record.BeforePairMatches(interaction.ids[0], interaction.ids[1])) score += 4;
        if (interaction.ids.Length >= 4 &&
            record.AfterPairMatches(interaction.ids[2], interaction.ids[3])) score += 4;
        if (interaction.ids.Contains(record.IncomingId)) score += 1;
        if (interaction.ids.Contains(record.OutgoingId)) score += 1;
        return score;
    }

    public ClusterScienceRecord FindNearestCluster(double time, bool timeIsNbody)
    {
        List<ClusterScienceRecord> source = timeIsNbody ? clusterByNbody : clusterByMyr;
        if (source.Count == 0) return null;

        int low = 0;
        int high = source.Count - 1;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            double middleTime = timeIsNbody ? source[middle].TimeNbody : source[middle].TimeMyr;
            if (middleTime < time) low = middle + 1;
            else if (middleTime > time) high = middle - 1;
            else return source[middle];
        }

        int upper = Mathf.Clamp(low, 0, source.Count - 1);
        int lower = Mathf.Clamp(low - 1, 0, source.Count - 1);
        double lowerTime = timeIsNbody ? source[lower].TimeNbody : source[lower].TimeMyr;
        double upperTime = timeIsNbody ? source[upper].TimeNbody : source[upper].TimeMyr;
        return Math.Abs(time - lowerTime) <= Math.Abs(upperTime - time) ? source[lower] : source[upper];
    }

    public double? ConvertNbodyToMyr(double timeNbody)
    {
        if (clusterByNbody.Count == 0) return null;
        if (timeNbody <= clusterByNbody[0].TimeNbody) return clusterByNbody[0].TimeMyr;
        int last = clusterByNbody.Count - 1;
        if (timeNbody >= clusterByNbody[last].TimeNbody) return clusterByNbody[last].TimeMyr;

        int low = 0;
        int high = last;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            double middleTime = clusterByNbody[middle].TimeNbody;
            if (middleTime < timeNbody) low = middle + 1;
            else if (middleTime > timeNbody) high = middle - 1;
            else return clusterByNbody[middle].TimeMyr;
        }

        ClusterScienceRecord before = clusterByNbody[low - 1];
        ClusterScienceRecord after = clusterByNbody[low];
        double span = after.TimeNbody - before.TimeNbody;
        if (span <= 0.0) return before.TimeMyr;
        double fraction = (timeNbody - before.TimeNbody) / span;
        return before.TimeMyr + fraction * (after.TimeMyr - before.TimeMyr);
    }

    static Dictionary<string, int> BuildHeader(string line)
    {
        string[] values = line.TrimStart('\ufeff').Split(',');
        Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < values.Length; i++) result[values[i].Trim()] = i;
        return result;
    }

    static string ReadString(Dictionary<string, int> header, string[] values, string key)
    {
        int index;
        if (!header.TryGetValue(key, out index) || index < 0 || index >= values.Length) return "";
        return values[index].Trim();
    }

    static int ReadInt(Dictionary<string, int> header, string[] values, string key)
    {
        double? number = ReadDouble(header, values, key);
        return number.HasValue ? (int)Math.Round(number.Value) : 0;
    }

    static double? ReadDouble(Dictionary<string, int> header, string[] values, string key)
    {
        string value = ReadString(header, values, key);
        double parsed;
        if (string.IsNullOrWhiteSpace(value) ||
            !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
            double.IsNaN(parsed) || double.IsInfinity(parsed)) return null;
        return parsed;
    }

    static bool ReadBool(Dictionary<string, int> header, string[] values, string key)
    {
        string value = ReadString(header, values, key);
        bool parsed;
        if (bool.TryParse(value, out parsed)) return parsed;
        return value == "1";
    }

    static Vector3 ReadVector(Dictionary<string, int> header, string[] values, string x, string y, string z)
    {
        return new Vector3(
            (float)(ReadDouble(header, values, x) ?? 0.0),
            (float)(ReadDouble(header, values, y) ?? 0.0),
            (float)(ReadDouble(header, values, z) ?? 0.0)
        );
    }

    static void ReadPair(string value, out int first, out int second)
    {
        first = 0;
        second = 0;
        if (string.IsNullOrWhiteSpace(value)) return;
        string[] pieces = value.Split('+');
        if (pieces.Length < 2) return;
        int.TryParse(pieces[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out first);
        int.TryParse(pieces[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out second);
    }

    static int[] ReadIntList(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new int[0];
        return value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(piece =>
            {
                int parsed;
                return int.TryParse(piece.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                    ? (int?)parsed
                    : null;
            })
            .Where(number => number.HasValue)
            .Select(number => number.Value)
            .ToArray();
    }
}
