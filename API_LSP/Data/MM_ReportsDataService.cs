using System.Text.Json;
using Microsoft.Data.SqlClient;
using API_LSP.Models.DTOs;

namespace API_LSP.Data
{
    public class MM_ReportsDataService
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MM_ReportsDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        /// <summary>
        /// Daily report for one Model on one date. The whole report is scoped to
        /// vehicles whose vehicle date (MM_Vehicle_Details.Inserted_Date) falls on
        /// that day; ALL of each vehicle's data is then included regardless of when
        /// individual stages/defects were logged. Rework (stage 9, no Track_Logs
        /// row) is counted from concern-level rework completion.
        /// </summary>
        public async Task<DailyReportResponse> GetDailyReport(decimal modelId, DateTime reportDate)
        {
            var result = new DailyReportResponse { ReportDate = reportDate.Date };
            var day = reportDate.Date;

            using var connection = await _connectionFactory.CreateConnectionAsync();

            // 1) Model name (header label).
            using (var cmd = new SqlCommand(
                "SELECT ISNULL(Model_Name, '') FROM MM_Model WHERE Model_ID = @Model_ID", connection))
            {
                cmd.Parameters.AddWithValue("@Model_ID", modelId);
                var name = await cmd.ExecuteScalarAsync();
                result.Model_Name = name == null || name == DBNull.Value ? string.Empty : (string)name;
            }

            // 2) Per-stage vehicle counts for that day's vehicles (all audit areas,
            //    ordered by SORTORDER). Scoped by vehicle date, not the stage's
            //    own log date. RFD (11) becomes the headline tile; Rework (9) is
            //    overwritten below from the rework table.
            const string stageQuery = @"
                SELECT aa.Audit_Area_ID, aa.Audit_Area_Name, ISNULL(c.Cnt, 0) AS Cnt
                FROM MM_Audit_Area_Master aa
                LEFT JOIN (
                    SELECT tl.Audit_Area_ID, COUNT(DISTINCT tl.Vehicle_ID) AS Cnt
                    FROM MM_Vehicle_Track_Logs tl
                    INNER JOIN MM_Vehicle_Details v ON v.Vehicle_ID = tl.Vehicle_ID
                    WHERE v.Model_ID = @Model_ID
                      AND (v.Is_Deleted IS NULL OR v.Is_Deleted = 0)
                      AND (tl.Is_Deleted IS NULL OR tl.Is_Deleted = 0)
                      AND CAST(v.Inserted_Date AS date) = @Day
                    GROUP BY tl.Audit_Area_ID
                ) c ON c.Audit_Area_ID = aa.Audit_Area_ID
                ORDER BY aa.SORTORDER";

            const decimal REWORK_AREA = 9m;
            const decimal RFD_AREA = 11m;
            DailyStageCount? reworkSlot = null;

            using (var cmd = new SqlCommand(stageQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Model_ID", modelId);
                cmd.Parameters.AddWithValue("@Day", day);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var id = reader.GetDecimal(0);
                    var stage = new DailyStageCount
                    {
                        Audit_Area_ID = id,
                        Stage_Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        Completed_Count = Convert.ToInt32(reader.GetValue(2)),
                    };

                    if (id == RFD_AREA)
                    {
                        result.RfdDoneCount = stage.Completed_Count;
                        continue; // headline, not shown in the grid
                    }
                    if (id == REWORK_AREA) reworkSlot = stage; // count filled next
                    result.StageCounts.Add(stage);
                }
            }

            // 3) Rework done for that day's vehicles (concern-level; stage 9 has
            //    no Track_Logs row). Scoped by vehicle date, not the rework date.
            const string reworkQuery = @"
                SELECT COUNT(DISTINCT d.Vehicle_ID)
                FROM MM_Vehicle_Defect_Rework r
                INNER JOIN MM_Vehicle_Defects d ON d.Defect_Entry_ID = r.Defect_Entry_ID
                INNER JOIN MM_Vehicle_Details v ON v.Vehicle_ID = d.Vehicle_ID
                WHERE v.Model_ID = @Model_ID
                  AND (v.Is_Deleted IS NULL OR v.Is_Deleted = 0)
                  AND r.Rework_Done = 1
                  AND CAST(v.Inserted_Date AS date) = @Day";
            using (var cmd = new SqlCommand(reworkQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Model_ID", modelId);
                cmd.Parameters.AddWithValue("@Day", day);
                var cnt = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
                if (reworkSlot != null) reworkSlot.Completed_Count = cnt;
            }

            // 4) Vehicles with that vehicle date (the day's audited vehicles).
            const string activeQuery = @"
                SELECT COUNT(*)
                FROM MM_Vehicle_Details v
                WHERE v.Model_ID = @Model_ID
                  AND (v.Is_Deleted IS NULL OR v.Is_Deleted = 0)
                  AND CAST(v.Inserted_Date AS date) = @Day";
            using (var cmd = new SqlCommand(activeQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Model_ID", modelId);
                cmd.Parameters.AddWithValue("@Day", day);
                result.VehiclesActiveCount = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
            }

            // 5) Every defect of that day's vehicles (any defect date), joined to
            //    masters + stage auditor.
            const string defectQuery = @"
                SELECT
                    v.VIN_No,
                    aa.Audit_Area_Name,
                    ISNULL(va.Vehicle_Area_Name, '') AS Vehicle_Area_Name,
                    p.Part_Name,
                    df.Defect_Name,
                    l.Location_Name,
                    ISNULL(s.Severity_Code, '')      AS Severity_Code,
                    ISNULL(a.Attribution_Name, '')   AS Attribution_Name,
                    ISNULL(dt.Defect_Type_Name, '')  AS Defect_Type_Name,
                    ISNULL(tl.Auditor_Name, '')      AS Auditor_Name,
                    ISNULL(d.Remark, '')             AS Remark,
                    d.Inserted_Date,
                    CASE WHEN r.Rework_Done = 1 THEN 1 ELSE 0 END AS Rework_Done
                FROM MM_Vehicle_Defects d
                INNER JOIN MM_Vehicle_Details v   ON v.Vehicle_ID = d.Vehicle_ID
                INNER JOIN MM_Audit_Area_Master aa ON aa.Audit_Area_ID = d.Audit_Area_ID
                LEFT  JOIN MM_Vehicle_Area_Master va ON va.Vehicle_Area_ID = d.Vehicle_Area_ID
                INNER JOIN MM_Part_Master p       ON p.Part_ID = d.Part_ID
                INNER JOIN MM_Defect_Master df    ON df.Defect_ID = d.Defect_ID
                INNER JOIN MM_Location_Master l   ON l.Location_ID = d.Location_ID
                LEFT  JOIN MM_Severity_Master s   ON s.Severity_ID = d.Severity_ID
                LEFT  JOIN MM_Attribution_Master a ON a.Attribution_ID = d.Attribution_ID
                LEFT  JOIN MM_Defect_Type_Master dt ON dt.Defect_Type_ID = d.Defect_Type_ID
                LEFT  JOIN MM_Vehicle_Track_Logs tl ON tl.Vehicle_ID = d.Vehicle_ID AND tl.Audit_Area_ID = d.Audit_Area_ID
                LEFT  JOIN MM_Vehicle_Defect_Rework r ON r.Defect_Entry_ID = d.Defect_Entry_ID
                WHERE v.Model_ID = @Model_ID
                  AND (v.Is_Deleted IS NULL OR v.Is_Deleted = 0)
                  AND CAST(v.Inserted_Date AS date) = @Day
                ORDER BY v.VIN_No, aa.SORTORDER, d.Defect_Entry_ID";

            using (var cmd = new SqlCommand(defectQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Model_ID", modelId);
                cmd.Parameters.AddWithValue("@Day", day);
                using var reader = await cmd.ExecuteReaderAsync();
                int iDate = reader.GetOrdinal("Inserted_Date");
                int iRw = reader.GetOrdinal("Rework_Done");
                while (await reader.ReadAsync())
                {
                    result.Defects.Add(new DailyDefectRow
                    {
                        VIN_No = reader.GetString(reader.GetOrdinal("VIN_No")),
                        Stage_Name = reader.GetString(reader.GetOrdinal("Audit_Area_Name")),
                        Vehicle_Area_Name = reader.GetString(reader.GetOrdinal("Vehicle_Area_Name")),
                        Part_Name = reader.GetString(reader.GetOrdinal("Part_Name")),
                        Defect_Name = reader.GetString(reader.GetOrdinal("Defect_Name")),
                        Location_Name = reader.GetString(reader.GetOrdinal("Location_Name")),
                        Severity_Code = reader.GetString(reader.GetOrdinal("Severity_Code")),
                        Attribution_Name = reader.GetString(reader.GetOrdinal("Attribution_Name")),
                        Defect_Type_Name = reader.GetString(reader.GetOrdinal("Defect_Type_Name")),
                        Auditor_Name = reader.GetString(reader.GetOrdinal("Auditor_Name")),
                        Remark = reader.GetString(reader.GetOrdinal("Remark")),
                        Entered_Date = reader.IsDBNull(iDate) ? (DateTime?)null : reader.GetDateTime(iDate),
                        Rework_Status = Convert.ToInt32(reader.GetValue(iRw)) == 1 ? "Done" : "Pending",
                    });
                }
            }

            result.TotalDefectsCount = result.Defects.Count;
            return result;
        }

        /// <summary>
        /// WIP / Pending report: vehicles of the given Model with a vehicle date
        /// in [start, end] that have NOT reached Physical RFD (no area-11 log).
        /// For each, the stage it is "stuck at" is the first pending stage in the
        /// app's SORTORDER (optional Shower Test excluded), plus ageing metrics.
        /// </summary>
        public async Task<WipReportResponse> GetWipReport(
            decimal modelId, DateTime startDate, DateTime endDate)
        {
            var result = new WipReportResponse { StartDate = startDate.Date, EndDate = endDate.Date };
            var start = startDate.Date;
            var end = endDate.Date;
            var today = DateTime.Now.Date;

            using var connection = await _connectionFactory.CreateConnectionAsync();

            // Model name.
            using (var cmd = new SqlCommand(
                "SELECT ISNULL(Model_Name, '') FROM MM_Model WHERE Model_ID = @Model_ID", connection))
            {
                cmd.Parameters.AddWithValue("@Model_ID", modelId);
                var name = await cmd.ExecuteScalarAsync();
                result.Model_Name = name == null || name == DBNull.Value ? string.Empty : (string)name;
            }

            // Ordered pipeline stages (SORTORDER); Shower (4) + Rework (9) excluded.
            // RFD (11) stays last so an otherwise-complete vehicle reads "stuck at RFD".
            var stageOrder = new List<(decimal Id, string Name)>();
            const string stageQuery = @"
                SELECT Audit_Area_ID, Audit_Area_Name
                FROM MM_Audit_Area_Master
                WHERE Audit_Area_ID IN (1, 2, 3, 5, 6, 7, 8, 11, 12)
                ORDER BY SORTORDER";
            using (var cmd = new SqlCommand(stageQuery, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    stageOrder.Add((reader.GetDecimal(0), reader.IsDBNull(1) ? "" : reader.GetString(1)));
            }

            // WIP vehicles (no RFD log).
            var vehicles = new Dictionary<decimal, WipRow>();
            var vehicleDate = new Dictionary<decimal, DateTime?>();
            const string vehQuery = @"
                SELECT v.Vehicle_ID, v.VIN_No, ISNULL(m.Model_Name, '') AS Model_Name,
                       v.Inserted_Date, v.Roll_Down_Date
                FROM MM_Vehicle_Details v
                LEFT JOIN MM_Model m ON m.Model_ID = v.Model_ID
                WHERE v.Model_ID = @Model_ID
                  AND (v.Is_Deleted IS NULL OR v.Is_Deleted = 0)
                  AND CAST(v.Inserted_Date AS date) BETWEEN @Start AND @End
                  AND NOT EXISTS (
                      SELECT 1 FROM MM_Vehicle_Track_Logs tl
                      WHERE tl.Vehicle_ID = v.Vehicle_ID AND tl.Audit_Area_ID = 11
                        AND (tl.Is_Deleted IS NULL OR tl.Is_Deleted = 0))
                ORDER BY v.Inserted_Date, v.VIN_No";
            using (var cmd = new SqlCommand(vehQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Model_ID", modelId);
                cmd.Parameters.AddWithValue("@Start", start);
                cmd.Parameters.AddWithValue("@End", end);
                using var reader = await cmd.ExecuteReaderAsync();
                int iVid = reader.GetOrdinal("Vehicle_ID");
                int iVin = reader.GetOrdinal("VIN_No");
                int iModel = reader.GetOrdinal("Model_Name");
                int iIns = reader.GetOrdinal("Inserted_Date");
                int iRoll = reader.GetOrdinal("Roll_Down_Date");
                while (await reader.ReadAsync())
                {
                    var vid = reader.GetDecimal(iVid);
                    DateTime? vd = reader.IsDBNull(iIns) ? (DateTime?)null : reader.GetDateTime(iIns);
                    vehicles[vid] = new WipRow
                    {
                        VIN_No = reader.GetString(iVin),
                        Model_Name = reader.GetString(iModel),
                        Vehicle_Date = vd,
                        Roll_Down_Date = reader.IsDBNull(iRoll) ? (DateTime?)null : reader.GetDateTime(iRoll),
                        Age_Days = vd.HasValue ? Math.Max(0, (today - vd.Value.Date).Days) : 0,
                    };
                    vehicleDate[vid] = vd;
                }
            }

            // Completed stages + last activity for those vehicles.
            var completed = new Dictionary<decimal, HashSet<decimal>>();
            var lastActivity = new Dictionary<decimal, (DateTime Date, decimal StageId)>();
            if (vehicles.Count > 0)
            {
                const string logQuery = @"
                    SELECT tl.Vehicle_ID, tl.Audit_Area_ID, tl.Log_Date
                    FROM MM_Vehicle_Track_Logs tl
                    INNER JOIN MM_Vehicle_Details v ON v.Vehicle_ID = tl.Vehicle_ID
                    WHERE v.Model_ID = @Model_ID
                      AND (v.Is_Deleted IS NULL OR v.Is_Deleted = 0)
                      AND (tl.Is_Deleted IS NULL OR tl.Is_Deleted = 0)
                      AND CAST(v.Inserted_Date AS date) BETWEEN @Start AND @End";
                using var cmd = new SqlCommand(logQuery, connection);
                cmd.Parameters.AddWithValue("@Model_ID", modelId);
                cmd.Parameters.AddWithValue("@Start", start);
                cmd.Parameters.AddWithValue("@End", end);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var vid = reader.GetDecimal(0);
                    if (!vehicles.ContainsKey(vid)) continue; // RFD-done vehicles ignored
                    var areaId = reader.GetDecimal(1);
                    var logDate = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);

                    if (!completed.TryGetValue(vid, out var set))
                        completed[vid] = set = new HashSet<decimal>();
                    set.Add(areaId);

                    if (logDate.HasValue &&
                        (!lastActivity.TryGetValue(vid, out var la) || logDate.Value > la.Date))
                        lastActivity[vid] = (logDate.Value, areaId);
                }
            }

            // Pending (un-reworked) concern counts for those vehicles.
            var pendingConcerns = new Dictionary<decimal, int>();
            if (vehicles.Count > 0)
            {
                const string concernQuery = @"
                    SELECT d.Vehicle_ID, COUNT(*) AS Cnt
                    FROM MM_Vehicle_Defects d
                    INNER JOIN MM_Vehicle_Details v ON v.Vehicle_ID = d.Vehicle_ID
                    LEFT JOIN MM_Vehicle_Defect_Rework r ON r.Defect_Entry_ID = d.Defect_Entry_ID
                    WHERE v.Model_ID = @Model_ID
                      AND (v.Is_Deleted IS NULL OR v.Is_Deleted = 0)
                      AND CAST(v.Inserted_Date AS date) BETWEEN @Start AND @End
                      AND (r.Rework_Done IS NULL OR r.Rework_Done = 0)
                    GROUP BY d.Vehicle_ID";
                using var cmd = new SqlCommand(concernQuery, connection);
                cmd.Parameters.AddWithValue("@Model_ID", modelId);
                cmd.Parameters.AddWithValue("@Start", start);
                cmd.Parameters.AddWithValue("@End", end);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    pendingConcerns[reader.GetDecimal(0)] = Convert.ToInt32(reader.GetValue(1));
            }

            // Assemble rows: stuck stage = first pending stage in order.
            var stuckTally = new Dictionary<string, int>();
            foreach (var kv in vehicles)
            {
                var vid = kv.Key;
                var row = kv.Value;
                var done = completed.TryGetValue(vid, out var set) ? set : new HashSet<decimal>();

                var stuck = stageOrder.FirstOrDefault(s => !done.Contains(s.Id));
                row.Stuck_Stage_Name = string.IsNullOrEmpty(stuck.Name) ? "Physical RFD" : stuck.Name;

                if (lastActivity.TryGetValue(vid, out var la))
                {
                    row.Last_Activity_Date = la.Date;
                    row.Idle_Days = Math.Max(0, (today - la.Date.Date).Days);
                    var match = stageOrder.FirstOrDefault(s => s.Id == la.StageId);
                    row.Last_Stage_Name = match.Name ?? string.Empty;
                    row.Last_Stage_Date = la.Date;
                }
                else
                {
                    row.Last_Stage_Name = "—";
                    row.Idle_Days = row.Age_Days;
                }

                row.Pending_Concerns = pendingConcerns.TryGetValue(vid, out var pc) ? pc : 0;

                stuckTally[row.Stuck_Stage_Name] = stuckTally.GetValueOrDefault(row.Stuck_Stage_Name) + 1;
                result.Rows.Add(row);
            }

            // Order rows by longest idle first (most attention needed).
            result.Rows = result.Rows.OrderByDescending(r => r.Idle_Days).ThenBy(r => r.VIN_No).ToList();
            result.TotalWip = result.Rows.Count;

            // Stuck summary ordered by pipeline order for a readable grid.
            foreach (var s in stageOrder)
            {
                var label = s.Name;
                if (stuckTally.TryGetValue(label, out var c))
                    result.StuckCounts.Add(new WipStuckCount { Stage_Name = label, Count = c });
            }
            if (stuckTally.TryGetValue("Physical RFD", out var rfdc) &&
                !result.StuckCounts.Any(x => x.Stage_Name == "Physical RFD"))
                result.StuckCounts.Add(new WipStuckCount { Stage_Name = "Physical RFD", Count = rfdc });

            return result;
        }

        /// <summary>
        /// Stage TAT (turnaround) report for a Model over a vehicle-date range.
        /// Per-transition average/min/max hours between consecutive stages
        /// (Roll Down → first stage → … → RFD), plus the headline overall cycle
        /// time Roll Down Date → Physical RFD Date. Only positive deltas count
        /// (stages are upserts, so timings are approximate).
        /// </summary>
        public async Task<TatReportResponse> GetTatReport(
            decimal modelId, DateTime startDate, DateTime endDate)
        {
            var result = new TatReportResponse { StartDate = startDate.Date, EndDate = endDate.Date };
            var start = startDate.Date;
            var end = endDate.Date;

            using var connection = await _connectionFactory.CreateConnectionAsync();

            // Model name.
            using (var cmd = new SqlCommand(
                "SELECT ISNULL(Model_Name, '') FROM MM_Model WHERE Model_ID = @Model_ID", connection))
            {
                cmd.Parameters.AddWithValue("@Model_ID", modelId);
                var name = await cmd.ExecuteScalarAsync();
                result.Model_Name = name == null || name == DBNull.Value ? string.Empty : (string)name;
            }

            // Ordered pipeline stages (SORTORDER); Shower (4) + Rework (9) excluded.
            var stageOrder = new List<(decimal Id, string Name)>();
            const string stageQuery = @"
                SELECT Audit_Area_ID, Audit_Area_Name
                FROM MM_Audit_Area_Master
                WHERE Audit_Area_ID IN (1, 2, 3, 5, 6, 7, 8, 11, 12)
                ORDER BY SORTORDER";
            using (var cmd = new SqlCommand(stageQuery, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    stageOrder.Add((reader.GetDecimal(0), reader.IsDBNull(1) ? "" : reader.GetString(1)));
            }

            // Vehicles in range + their Roll Down date.
            var rollDown = new Dictionary<decimal, DateTime?>();
            const string vehQuery = @"
                SELECT v.Vehicle_ID, v.Roll_Down_Date
                FROM MM_Vehicle_Details v
                WHERE v.Model_ID = @Model_ID
                  AND (v.Is_Deleted IS NULL OR v.Is_Deleted = 0)
                  AND CAST(v.Inserted_Date AS date) BETWEEN @Start AND @End";
            using (var cmd = new SqlCommand(vehQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Model_ID", modelId);
                cmd.Parameters.AddWithValue("@Start", start);
                cmd.Parameters.AddWithValue("@End", end);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    rollDown[reader.GetDecimal(0)] =
                        reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
            }
            result.VehicleCount = rollDown.Count;

            // Stage log dates per vehicle.
            var stageDates = new Dictionary<decimal, Dictionary<decimal, DateTime>>();
            if (rollDown.Count > 0)
            {
                const string logQuery = @"
                    SELECT tl.Vehicle_ID, tl.Audit_Area_ID, tl.Log_Date
                    FROM MM_Vehicle_Track_Logs tl
                    INNER JOIN MM_Vehicle_Details v ON v.Vehicle_ID = tl.Vehicle_ID
                    WHERE v.Model_ID = @Model_ID
                      AND (v.Is_Deleted IS NULL OR v.Is_Deleted = 0)
                      AND (tl.Is_Deleted IS NULL OR tl.Is_Deleted = 0)
                      AND CAST(v.Inserted_Date AS date) BETWEEN @Start AND @End
                      AND tl.Log_Date IS NOT NULL";
                using var cmd = new SqlCommand(logQuery, connection);
                cmd.Parameters.AddWithValue("@Model_ID", modelId);
                cmd.Parameters.AddWithValue("@Start", start);
                cmd.Parameters.AddWithValue("@End", end);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var vid = reader.GetDecimal(0);
                    var areaId = reader.GetDecimal(1);
                    var logDate = reader.GetDateTime(2);
                    if (!stageDates.TryGetValue(vid, out var map))
                        stageDates[vid] = map = new Dictionary<decimal, DateTime>();
                    map[areaId] = logDate;
                }
            }

            // Helper to aggregate a set of hour-deltas.
            static (double avg, double min, double max, int cnt) Aggregate(List<double> deltas)
            {
                if (deltas.Count == 0) return (0, 0, 0, 0);
                return (deltas.Average(), deltas.Min(), deltas.Max(), deltas.Count);
            }

            // Build sequence: [Roll Down] → each stage in order.
            // For each consecutive pair, gather positive hour-deltas across vehicles.
            const decimal ROLLDOWN = -1m;
            var sequence = new List<(decimal Id, string Name)> { (ROLLDOWN, "Roll Down") };
            sequence.AddRange(stageOrder);

            for (int i = 1; i < sequence.Count; i++)
            {
                var prev = sequence[i - 1];
                var cur = sequence[i];
                var deltas = new List<double>();

                foreach (var vid in rollDown.Keys)
                {
                    DateTime? prevTime = prev.Id == ROLLDOWN
                        ? rollDown[vid]
                        : (stageDates.TryGetValue(vid, out var m1) && m1.TryGetValue(prev.Id, out var t1) ? t1 : (DateTime?)null);
                    DateTime? curTime =
                        stageDates.TryGetValue(vid, out var m2) && m2.TryGetValue(cur.Id, out var t2) ? t2 : (DateTime?)null;

                    if (prevTime.HasValue && curTime.HasValue)
                    {
                        var hrs = (curTime.Value - prevTime.Value).TotalHours;
                        if (hrs > 0) deltas.Add(hrs);
                    }
                }

                var agg = Aggregate(deltas);
                result.Transitions.Add(new TatTransition
                {
                    From_Stage = prev.Name,
                    To_Stage = cur.Name,
                    Avg_Hours = Math.Round(agg.avg, 2),
                    Min_Hours = Math.Round(agg.min, 2),
                    Max_Hours = Math.Round(agg.max, 2),
                    Vehicle_Count = agg.cnt,
                });
            }

            // Overall cycle: Roll Down → RFD (area 11), computed directly.
            var overall = new List<double>();
            foreach (var vid in rollDown.Keys)
            {
                var rd = rollDown[vid];
                if (rd.HasValue &&
                    stageDates.TryGetValue(vid, out var m) && m.TryGetValue(11m, out var rfd) &&
                    (rfd - rd.Value).TotalHours > 0)
                    overall.Add((rfd - rd.Value).TotalHours);
            }
            if (overall.Count > 0)
            {
                result.OverallAvgHours = Math.Round(overall.Average(), 2);
                result.OverallMinHours = Math.Round(overall.Min(), 2);
                result.OverallMaxHours = Math.Round(overall.Max(), 2);
                result.OverallCount = overall.Count;
            }

            return result;
        }

        /// <summary>
        /// Image defect heat-map for a Model + Vehicle Area over a vehicle-date
        /// range. Returns every area image with the Paint tap-points recorded on
        /// it (normalized 0..1), each mapped part region's defect tally, and the
        /// top-parts ranking across the area. Points are grouped per image and
        /// counted into hotspots using smallest-rect-wins (matching Paint).
        /// </summary>
        public async Task<ImageHeatmapResponse> GetImageHeatmapReport(
            decimal modelId, decimal vehicleAreaId, DateTime startDate, DateTime endDate)
        {
            var result = new ImageHeatmapResponse { StartDate = startDate.Date, EndDate = endDate.Date };
            var start = startDate.Date;
            var end = endDate.Date;

            using var connection = await _connectionFactory.CreateConnectionAsync();

            // Model + Vehicle Area names.
            using (var cmd = new SqlCommand(
                "SELECT ISNULL(Model_Name, '') FROM MM_Model WHERE Model_ID = @Model_ID", connection))
            {
                cmd.Parameters.AddWithValue("@Model_ID", modelId);
                var name = await cmd.ExecuteScalarAsync();
                result.Model_Name = name == null || name == DBNull.Value ? string.Empty : (string)name;
            }
            using (var cmd = new SqlCommand(
                "SELECT ISNULL(Vehicle_Area_Name, '') FROM MM_Vehicle_Area_Master WHERE Vehicle_Area_ID = @Id", connection))
            {
                cmd.Parameters.AddWithValue("@Id", vehicleAreaId);
                var name = await cmd.ExecuteScalarAsync();
                result.Vehicle_Area_Name = name == null || name == DBNull.Value ? string.Empty : (string)name;
            }

            // Images for this Model + Area (SORTORDER for stable left-to-right layout).
            var images = new List<HeatImage>();
            var byId = new Dictionary<decimal, HeatImage>();
            const string imgQuery = @"
                SELECT Area_Image_ID, Image_Name, Image_FileName
                FROM MM_Area_Image_Master
                WHERE Model_ID = @Model_ID
                  AND Vehicle_Area_ID = @Area_ID
                  AND (Is_Deleted IS NULL OR Is_Deleted = 0)
                ORDER BY SORTORDER, Area_Image_ID";
            using (var cmd = new SqlCommand(imgQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Model_ID", modelId);
                cmd.Parameters.AddWithValue("@Area_ID", vehicleAreaId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var img = new HeatImage
                    {
                        Area_Image_ID = reader.GetDecimal(0),
                        Image_Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Image_FileName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    };
                    images.Add(img);
                    byId[img.Area_Image_ID] = img;
                }
            }

            if (images.Count == 0)
            {
                result.Images = images;
                return result;
            }

            // Part-hotspot mappings for those images (with rect centre for the badge).
            var mappedPartsByImage = new Dictionary<decimal, HashSet<decimal>>();
            const string mapQuery = @"
                SELECT m.Area_Image_ID, m.Part_ID, p.Part_Name, m.Shape
                FROM MM_Area_Image_Part_Mapping m
                INNER JOIN MM_Part_Master p ON p.Part_ID = m.Part_ID
                INNER JOIN MM_Area_Image_Master ai ON ai.Area_Image_ID = m.Area_Image_ID
                WHERE ai.Model_ID = @Model_ID AND ai.Vehicle_Area_ID = @Area_ID
                  AND (ai.Is_Deleted IS NULL OR ai.Is_Deleted = 0)";
            using (var cmd = new SqlCommand(mapQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Model_ID", modelId);
                cmd.Parameters.AddWithValue("@Area_ID", vehicleAreaId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var aid = reader.GetDecimal(0);
                    if (!byId.TryGetValue(aid, out var img)) continue;
                    var partId = reader.GetDecimal(1);
                    var shape = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    var rect = ParseRect(shape);
                    img.Hotspots.Add(new HeatHotspot
                    {
                        Part_ID = partId,
                        Part_Name = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Shape = shape,
                        CenterX = rect.HasValue ? rect.Value.x + rect.Value.w / 2 : 0.5,
                        CenterY = rect.HasValue ? rect.Value.y + rect.Value.h / 2 : 0.5,
                    });
                    if (!mappedPartsByImage.TryGetValue(aid, out var set))
                        mappedPartsByImage[aid] = set = new HashSet<decimal>();
                    set.Add(partId);
                }
            }

            // Paint defects recorded on those images (full detail for the table).
            const string defectQuery = @"
                SELECT d.Defect_Entry_ID, d.Area_Image_ID, d.Part_ID,
                       v.VIN_No, p.Part_Name, df.Defect_Name, l.Location_Name,
                       ISNULL(s.Severity_Code, '')     AS Severity_Code,
                       ISNULL(a.Attribution_Name, '')  AS Attribution_Name,
                       ISNULL(dt.Defect_Type_Name, '') AS Defect_Type_Name,
                       ISNULL(tl.Auditor_Name, '')     AS Auditor_Name,
                       ISNULL(d.Remark, '')            AS Remark,
                       CASE WHEN r.Rework_Done = 1 THEN 1 ELSE 0 END AS Rework_Done
                FROM MM_Vehicle_Defects d
                INNER JOIN MM_Vehicle_Details v ON v.Vehicle_ID = d.Vehicle_ID
                INNER JOIN MM_Part_Master p     ON p.Part_ID = d.Part_ID
                INNER JOIN MM_Defect_Master df  ON df.Defect_ID = d.Defect_ID
                INNER JOIN MM_Location_Master l ON l.Location_ID = d.Location_ID
                LEFT  JOIN MM_Severity_Master s ON s.Severity_ID = d.Severity_ID
                LEFT  JOIN MM_Attribution_Master a ON a.Attribution_ID = d.Attribution_ID
                LEFT  JOIN MM_Defect_Type_Master dt ON dt.Defect_Type_ID = d.Defect_Type_ID
                INNER JOIN MM_Area_Image_Master ai ON ai.Area_Image_ID = d.Area_Image_ID
                LEFT  JOIN MM_Vehicle_Track_Logs tl ON tl.Vehicle_ID = d.Vehicle_ID AND tl.Audit_Area_ID = d.Audit_Area_ID
                LEFT  JOIN MM_Vehicle_Defect_Rework r ON r.Defect_Entry_ID = d.Defect_Entry_ID
                WHERE v.Model_ID = @Model_ID
                  AND (v.Is_Deleted IS NULL OR v.Is_Deleted = 0)
                  AND CAST(v.Inserted_Date AS date) BETWEEN @Start AND @End
                  AND ai.Vehicle_Area_ID = @Area_ID
                  AND d.Area_Image_ID IS NOT NULL
                  AND d.Image_X IS NOT NULL
                  AND d.Image_Y IS NOT NULL
                ORDER BY v.VIN_No, d.Defect_Entry_ID";
            using (var cmd = new SqlCommand(defectQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Model_ID", modelId);
                cmd.Parameters.AddWithValue("@Area_ID", vehicleAreaId);
                cmd.Parameters.AddWithValue("@Start", start);
                cmd.Parameters.AddWithValue("@End", end);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var aid = reader.GetDecimal(1);
                    var partId = reader.GetDecimal(2);
                    result.Defects.Add(new ImageDefectRow
                    {
                        Defect_Entry_ID = reader.GetDecimal(0),
                        Area_Image_ID = aid,
                        Part_ID = partId,
                        VIN_No = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Part_Name = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Defect_Name = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        Location_Name = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        Severity_Code = reader.GetString(7),
                        Attribution_Name = reader.GetString(8),
                        Defect_Type_Name = reader.GetString(9),
                        Auditor_Name = reader.GetString(10),
                        Remark = reader.GetString(11),
                        Rework_Status = Convert.ToInt32(reader.GetValue(12)) == 1 ? "Done" : "Pending",
                    });

                    if (byId.TryGetValue(aid, out var img)) img.DefectCount++;
                    result.TotalDefects++;

                    // "Not placed" = the defect's part has no mapped region on its image,
                    // so it gets no count badge (still listed in the table).
                    if (!mappedPartsByImage.TryGetValue(aid, out var parts) || !parts.Contains(partId))
                        result.UnplacedCount++;
                }
            }

            result.Images = images;
            return result;
        }

        /// <summary>Parse a normalized rect hotspot Shape JSON; null if not a rect.</summary>
        private static (double x, double y, double w, double h)? ParseRect(string shapeJson)
        {
            if (string.IsNullOrWhiteSpace(shapeJson)) return null;
            try
            {
                using var doc = JsonDocument.Parse(shapeJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var t) &&
                    !string.Equals(t.GetString(), "rect", StringComparison.OrdinalIgnoreCase))
                    return null;
                double G(string k) => root.TryGetProperty(k, out var v) ? v.GetDouble() : 0;
                return (G("x"), G("y"), G("w"), G("h"));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Vehicle Tracking report: one row per vehicle for the given Model,
        /// filtered by MM_Vehicle_Details.Inserted_Date within [start, end].
        /// Track_Logs are pivoted into per-stage date + Is_No_Defect flag;
        /// Rework status is derived from concern-level rework completion.
        /// </summary>
        public async Task<List<VehicleTrackingReportRow>> GetVehicleTrackingReport(
            decimal modelId, DateTime startDate, DateTime endDate)
        {
            var rows = new List<VehicleTrackingReportRow>();
            using var connection = await _connectionFactory.CreateConnectionAsync();

            // Stage IDs: RT=1, UB=2, DTC=3, Shower=4, StaticAudit=6, VMS=7,
            // Recheck=8, RFD=11, Paint=12. (ECOS=5 intentionally excluded.)
            const string query = @"
                SELECT
                    v.VIN_No,
                    ISNULL(v.Body_Serial_No, '')            AS Body_Serial_No,
                    ISNULL(m.Model_Name, '')                AS Model_Name,
                    ISNULL(v.Model_Desc, '')                AS Model_Desc,
                    ISNULL(v.Variant, '')                   AS Variant,
                    ISNULL(v.Colour_Desc, '')               AS Colour_Desc,
                    ISNULL(v.Engine_No, '')                 AS Engine_No,
                    ISNULL(bp.Build_Phase_Name, '')         AS Build_Phase_Name,
                    ISNULL(dr.Drive_Name, '')               AS Drive_Name,
                    ISNULL(fu.Fuel_Name, '')                AS Fuel_Name,
                    ISNULL(co.Country_Name, '')             AS Country_Name,
                    v.Roll_Down_Date,
                    v.CAI_OK_Date,
                    MAX(CASE WHEN tl.Audit_Area_ID = 1  THEN tl.Log_Date END)               AS RT_Date,
                    MAX(CASE WHEN tl.Audit_Area_ID = 1  THEN CAST(tl.Is_No_Defect AS int) END) AS RT_Flag,
                    MAX(CASE WHEN tl.Audit_Area_ID = 2  THEN tl.Log_Date END)               AS UB_Date,
                    MAX(CASE WHEN tl.Audit_Area_ID = 2  THEN CAST(tl.Is_No_Defect AS int) END) AS UB_Flag,
                    MAX(CASE WHEN tl.Audit_Area_ID = 3  THEN tl.Log_Date END)               AS DTC_Date,
                    MAX(CASE WHEN tl.Audit_Area_ID = 3  THEN CAST(tl.Is_No_Defect AS int) END) AS DTC_Flag,
                    MAX(CASE WHEN tl.Audit_Area_ID = 4  THEN tl.Log_Date END)               AS SH_Date,
                    MAX(CASE WHEN tl.Audit_Area_ID = 4  THEN CAST(tl.Is_No_Defect AS int) END) AS SH_Flag,
                    MAX(CASE WHEN tl.Audit_Area_ID = 6  THEN tl.Log_Date END)               AS SA_Date,
                    MAX(CASE WHEN tl.Audit_Area_ID = 6  THEN CAST(tl.Is_No_Defect AS int) END) AS SA_Flag,
                    MAX(CASE WHEN tl.Audit_Area_ID = 7  THEN tl.Log_Date END)               AS VMS_Date,
                    MAX(CASE WHEN tl.Audit_Area_ID = 7  THEN CAST(tl.Is_No_Defect AS int) END) AS VMS_Flag,
                    MAX(CASE WHEN tl.Audit_Area_ID = 12 THEN tl.Log_Date END)               AS PT_Date,
                    MAX(CASE WHEN tl.Audit_Area_ID = 12 THEN CAST(tl.Is_No_Defect AS int) END) AS PT_Flag,
                    MAX(CASE WHEN tl.Audit_Area_ID = 8  THEN CAST(tl.Is_No_Defect AS int) END) AS RC_Flag,
                    MAX(CASE WHEN tl.Audit_Area_ID = 8  THEN 1 ELSE 0 END)                  AS RC_Present,
                    MAX(CASE WHEN tl.Audit_Area_ID = 11 THEN tl.Log_Date END)               AS RFD_Date,
                    MAX(CASE WHEN tl.Audit_Area_ID = 11 THEN 1 ELSE 0 END)                  AS RFD_Present,
                    rw.Total AS RW_Total,
                    rw.Done  AS RW_Done
                FROM MM_Vehicle_Details v
                LEFT JOIN MM_Model m               ON m.Model_ID = v.Model_ID
                LEFT JOIN MM_Build_Phase_Master bp ON bp.Build_Phase_ID = v.Build_Phase_ID
                LEFT JOIN MM_Drive_Master dr       ON dr.Drive_ID = v.Drive_ID
                LEFT JOIN MM_Fuel_Master fu        ON fu.Fuel_ID = v.Fuel_ID
                LEFT JOIN MM_Country_Master co     ON co.Country_ID = v.Country_ID
                LEFT JOIN MM_Vehicle_Track_Logs tl
                       ON tl.Vehicle_ID = v.Vehicle_ID
                      AND (tl.Is_Deleted IS NULL OR tl.Is_Deleted = 0)
                LEFT JOIN (
                    SELECT d.Vehicle_ID,
                           COUNT(*) AS Total,
                           SUM(CASE WHEN r.Rework_Done = 1 THEN 1 ELSE 0 END) AS Done
                    FROM MM_Vehicle_Defects d
                    LEFT JOIN MM_Vehicle_Defect_Rework r ON r.Defect_Entry_ID = d.Defect_Entry_ID
                    GROUP BY d.Vehicle_ID
                ) rw ON rw.Vehicle_ID = v.Vehicle_ID
                WHERE v.Model_ID = @Model_ID
                  AND (v.Is_Deleted IS NULL OR v.Is_Deleted = 0)
                  AND v.Inserted_Date >= @StartDate
                  AND v.Inserted_Date <  @EndDate
                GROUP BY
                    v.Vehicle_ID, v.VIN_No, v.Body_Serial_No, m.Model_Name, v.Model_Desc,
                    v.Variant, v.Colour_Desc, v.Engine_No, bp.Build_Phase_Name, dr.Drive_Name,
                    fu.Fuel_Name, co.Country_Name, v.Roll_Down_Date, v.CAI_OK_Date,
                    v.Inserted_Date, rw.Total, rw.Done
                ORDER BY v.Inserted_Date, v.VIN_No";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Model_ID", modelId);
            cmd.Parameters.AddWithValue("@StartDate", startDate.Date);
            cmd.Parameters.AddWithValue("@EndDate", endDate.Date.AddDays(1)); // inclusive end day

            using var reader = await cmd.ExecuteReaderAsync();

            DateTime? Dt(string col)
            {
                var i = reader.GetOrdinal(col);
                return reader.IsDBNull(i) ? (DateTime?)null : reader.GetDateTime(i);
            }
            int? Flag(string col)
            {
                var i = reader.GetOrdinal(col);
                return reader.IsDBNull(i) ? (int?)null : Convert.ToInt32(reader.GetValue(i));
            }
            string Str(string col)
            {
                var i = reader.GetOrdinal(col);
                return reader.IsDBNull(i) ? string.Empty : reader.GetString(i);
            }

            // A Track_Logs row (any Is_No_Defect value) means the stage was
            // audited → Done; no row → Pending. Only two statuses in this report.
            static string StageStatus(int? flag) =>
                flag == null ? "Pending" : "Done";

            while (await reader.ReadAsync())
            {
                var row = new VehicleTrackingReportRow
                {
                    VIN_No = Str("VIN_No"),
                    Body_Serial_No = Str("Body_Serial_No"),
                    Model_Name = Str("Model_Name"),
                    Model_Desc = Str("Model_Desc"),
                    Variant = Str("Variant"),
                    Colour_Desc = Str("Colour_Desc"),
                    Engine_No = Str("Engine_No"),
                    Build_Phase_Name = Str("Build_Phase_Name"),
                    Drive_Name = Str("Drive_Name"),
                    Fuel_Name = Str("Fuel_Name"),
                    Country_Name = Str("Country_Name"),
                    Roll_Down_Date = Dt("Roll_Down_Date"),
                    CAI_OK_Date = Dt("CAI_OK_Date"),

                    RoadTest_Date = Dt("RT_Date"),
                    RoadTest_Status = StageStatus(Flag("RT_Flag")),
                    UnderBody_Date = Dt("UB_Date"),
                    UnderBody_Status = StageStatus(Flag("UB_Flag")),
                    Dtc_Date = Dt("DTC_Date"),
                    Dtc_Status = StageStatus(Flag("DTC_Flag")),
                    Shower_Date = Dt("SH_Date"),
                    Shower_Status = StageStatus(Flag("SH_Flag")),
                    StaticAudit_Date = Dt("SA_Date"),
                    StaticAudit_Status = StageStatus(Flag("SA_Flag")),
                    Vms_Date = Dt("VMS_Date"),
                    Vms_Status = StageStatus(Flag("VMS_Flag")),
                    Paint_Date = Dt("PT_Date"),
                    Paint_Status = StageStatus(Flag("PT_Flag")),

                    Rfd_Date = Dt("RFD_Date"),
                };

                // Recheck (8): a Track_Logs row means the recheck was done.
                row.Recheck_Status =
                    Convert.ToInt32(reader.GetValue(reader.GetOrdinal("RC_Present"))) == 1 ? "Done" : "Pending";

                // Physical RFD (11): a Track_Logs row means dispatched.
                row.Rfd_Status =
                    Convert.ToInt32(reader.GetValue(reader.GetOrdinal("RFD_Present"))) == 1 ? "Done" : "Pending";

                // Rework (9): no Track_Logs row — derive from concern rework state.
                // Pending when any un-reworked concern remains; otherwise Done
                // (a vehicle with no concerns has nothing outstanding).
                int? rwTotal = Flag("RW_Total");
                int? rwDone = Flag("RW_Done");
                row.Rework_Status = (rwTotal.HasValue && rwTotal.Value > 0 && (rwDone ?? 0) < rwTotal.Value)
                    ? "Pending" : "Done";

                rows.Add(row);
            }

            return rows;
        }
    }
}
