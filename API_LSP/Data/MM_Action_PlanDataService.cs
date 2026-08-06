using API_LSP.Models.DTOs;
using Microsoft.Data.SqlClient;

namespace API_LSP.Data
{
    public class MM_Action_PlanDataService
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MM_Action_PlanDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        /// <summary>
        /// R by default / before cutoff / after a recurrence; Y once past cutoff
        /// with no recurrence yet (within the 30-day window); G once past
        /// cutoff + 30 days with zero concerns after cutoff.
        /// </summary>
        private static string ComputeStatus(DateTime? cutoff, int afterCutoff)
        {
            if (cutoff == null) return "R";
            var now = DateTime.Now;
            if (now <= cutoff.Value) return "R";
            if (afterCutoff > 0) return "R";
            if (now > cutoff.Value.AddDays(30)) return "G";
            return "Y";
        }

        private static string Key(decimal part, decimal defect, decimal attribution, decimal severity)
            => $"{part}|{defect}|{attribution}|{severity}";

        /// <summary>
        /// Problem rows for a model: distinct Part+Defect+Attribution+Severity
        /// combos from reported concerns, with frequency, cutoff (latest PCA
        /// target), concerns-after-cutoff, and computed status. Attribution /
        /// severity / status are filtered on the client.
        /// </summary>
        public async Task<List<ActionPlanProblemDto>> GetProblems(decimal modelId)
        {
            var problems = new List<ActionPlanProblemDto>();
            using var connection = await _connectionFactory.CreateConnectionAsync();

            // One round trip, three result sets: (A) grouped problems + frequency,
            // (B) existing plans + cutoff, (C) concerns after each plan's cutoff.
            const string query = @"
                SELECT d.Part_ID, p.Part_Name, d.Defect_ID, df.Defect_Name,
                       d.Attribution_ID, a.Attribution_Name,
                       ISNULL(d.Severity_ID, 0) AS Severity_ID,
                       ISNULL(s.Severity_Code, '') AS Severity_Code,
                       COUNT(*) AS Frequency
                FROM MM_Vehicle_Defects d
                INNER JOIN MM_Vehicle_Details v    ON v.Vehicle_ID = d.Vehicle_ID
                INNER JOIN MM_Part_Master p        ON p.Part_ID = d.Part_ID
                INNER JOIN MM_Defect_Master df     ON df.Defect_ID = d.Defect_ID
                INNER JOIN MM_Attribution_Master a ON a.Attribution_ID = d.Attribution_ID
                LEFT  JOIN MM_Severity_Master s    ON s.Severity_ID = d.Severity_ID
                WHERE v.Model_ID = @Model AND (v.Is_Deleted IS NULL OR v.Is_Deleted = 0)
                GROUP BY d.Part_ID, p.Part_Name, d.Defect_ID, df.Defect_Name,
                         d.Attribution_ID, a.Attribution_Name, d.Severity_ID, s.Severity_Code
                ORDER BY COUNT(*) DESC;

                SELECT ap.Action_Plan_ID, ap.Part_ID, ap.Defect_ID, ap.Attribution_ID, ap.Severity_ID, ap.Is_Submitted,
                       (SELECT MAX(Target_Date) FROM MM_Action_Plan_PCA WHERE Action_Plan_ID = ap.Action_Plan_ID) AS Cutoff
                FROM MM_Action_Plan ap
                WHERE ap.Model_ID = @Model;

                SELECT d.Part_ID, d.Defect_ID, d.Attribution_ID, ISNULL(d.Severity_ID, 0) AS Severity_ID, COUNT(*) AS Cnt
                FROM MM_Vehicle_Defects d
                INNER JOIN MM_Vehicle_Details v ON v.Vehicle_ID = d.Vehicle_ID
                INNER JOIN MM_Action_Plan ap
                    ON ap.Model_ID = v.Model_ID AND ap.Part_ID = d.Part_ID AND ap.Defect_ID = d.Defect_ID
                   AND ap.Attribution_ID = d.Attribution_ID AND ap.Severity_ID = ISNULL(d.Severity_ID, 0)
                CROSS APPLY (
                    SELECT MAX(Target_Date) AS Cutoff FROM MM_Action_Plan_PCA WHERE Action_Plan_ID = ap.Action_Plan_ID
                ) c
                WHERE v.Model_ID = @Model AND c.Cutoff IS NOT NULL AND d.Inserted_Date > c.Cutoff
                GROUP BY d.Part_ID, d.Defect_ID, d.Attribution_ID, ISNULL(d.Severity_ID, 0);";

            var plans = new Dictionary<string, (decimal Id, DateTime? Cutoff, bool Submitted)>();
            var afterCutoff = new Dictionary<string, int>();

            using (var cmd = new SqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@Model", modelId);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    problems.Add(new ActionPlanProblemDto
                    {
                        Model_ID = modelId,
                        Part_ID = reader.GetDecimal(0),
                        Part_Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Defect_ID = reader.GetDecimal(2),
                        Defect_Name = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Attribution_ID = reader.GetDecimal(4),
                        Attribution_Name = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        Severity_ID = reader.GetDecimal(6),
                        Severity_Code = reader.IsDBNull(7) ? "" : reader.GetString(7),
                        Frequency = Convert.ToInt32(reader.GetValue(8)),
                    });
                }

                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    var key = Key(reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4));
                    plans[key] = (
                        reader.GetDecimal(0),
                        reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                        !reader.IsDBNull(5) && reader.GetBoolean(5));
                }

                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                    afterCutoff[Key(reader.GetDecimal(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3))] =
                        Convert.ToInt32(reader.GetValue(4));
            }

            // Merge + compute status.
            foreach (var pr in problems)
            {
                var key = Key(pr.Part_ID, pr.Defect_ID, pr.Attribution_ID, pr.Severity_ID);
                int after = afterCutoff.TryGetValue(key, out var c) ? c : 0;
                pr.Concerns_After_Cutoff = after;

                if (plans.TryGetValue(key, out var plan))
                {
                    pr.Has_Plan = true;
                    pr.Action_Plan_ID = plan.Id;
                    pr.Is_Submitted = plan.Submitted;
                    pr.Cutoff_Date = plan.Cutoff;
                }
                pr.Status = ComputeStatus(pr.Cutoff_Date, after);
            }

            return problems;
        }

        /// <summary>Full plan detail for a problem combo, or an empty shell if none exists.</summary>
        public async Task<ActionPlanDetailDto> GetPlanByProblem(
            decimal modelId, decimal partId, decimal defectId, decimal attributionId, decimal severityId)
        {
            var detail = new ActionPlanDetailDto
            {
                Model_ID = modelId,
                Part_ID = partId,
                Defect_ID = defectId,
                Attribution_ID = attributionId,
                Severity_ID = severityId,
            };

            using var connection = await _connectionFactory.CreateConnectionAsync();

            // One round trip, four result sets: header, root causes, ICAs, PCAs.
            const string query = @"
                DECLARE @Id numeric(18,0) = (
                    SELECT Action_Plan_ID FROM MM_Action_Plan
                    WHERE Model_ID = @M AND Part_ID = @P AND Defect_ID = @D
                      AND Attribution_ID = @A AND Severity_ID = @S);

                SELECT Action_Plan_ID, Is_Submitted FROM MM_Action_Plan WHERE Action_Plan_ID = @Id;
                SELECT r.Root_Cause_ID, ISNULL(r.Root_Cause,''),
                       ISNULL(e.Employee_Name, ISNULL(r.Inserted_Host,'')) AS Added_By, r.Inserted_Date
                FROM MM_Action_Plan_Root_Cause r
                LEFT JOIN MM_Employee e ON e.Employee_ID = r.Inserted_User_ID
                WHERE r.Action_Plan_ID = @Id ORDER BY r.Root_Cause_ID;
                SELECT i.ICA_ID, ISNULL(i.Action,''), ISNULL(i.Responsibility,''), i.ICA_Date,
                       ISNULL(e.Employee_Name, ISNULL(i.Inserted_Host,'')) AS Added_By, i.Inserted_Date
                FROM MM_Action_Plan_ICA i
                LEFT JOIN MM_Employee e ON e.Employee_ID = i.Inserted_User_ID
                WHERE i.Action_Plan_ID = @Id ORDER BY i.ICA_ID;
                SELECT p.PCA_ID, ISNULL(p.Action,''), ISNULL(p.Responsibility,''), p.Target_Date, ISNULL(p.Status,'R'),
                       ISNULL(e.Employee_Name, ISNULL(p.Inserted_Host,'')) AS Added_By, p.Inserted_Date
                FROM MM_Action_Plan_PCA p
                LEFT JOIN MM_Employee e ON e.Employee_ID = p.Inserted_User_ID
                WHERE p.Action_Plan_ID = @Id ORDER BY p.PCA_ID;";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@M", modelId);
            cmd.Parameters.AddWithValue("@P", partId);
            cmd.Parameters.AddWithValue("@D", defectId);
            cmd.Parameters.AddWithValue("@A", attributionId);
            cmd.Parameters.AddWithValue("@S", severityId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                detail.Action_Plan_ID = reader.GetDecimal(0);
                detail.Is_Submitted = !reader.IsDBNull(1) && reader.GetBoolean(1);
            }

            await reader.NextResultAsync();
            while (await reader.ReadAsync())
                detail.RootCauses.Add(new ActionPlanRootCauseDto
                {
                    Root_Cause_ID = reader.GetDecimal(0),
                    Root_Cause = reader.GetString(1),
                    Added_By = reader.GetString(2),
                    Added_On = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                });

            await reader.NextResultAsync();
            while (await reader.ReadAsync())
                detail.Icas.Add(new ActionPlanIcaDto
                {
                    ICA_ID = reader.GetDecimal(0),
                    Action = reader.GetString(1),
                    Responsibility = reader.GetString(2),
                    ICA_Date = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                    Added_By = reader.GetString(4),
                    Added_On = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                });

            await reader.NextResultAsync();
            while (await reader.ReadAsync())
                detail.Pcas.Add(new ActionPlanPcaDto
                {
                    PCA_ID = reader.GetDecimal(0),
                    Action = reader.GetString(1),
                    Responsibility = reader.GetString(2),
                    Target_Date = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                    Status = reader.GetString(4),
                    Added_By = reader.GetString(5),
                    Added_On = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                });

            return detail;
        }

        /// <summary>
        /// Submit a new plan (creates header + rows) or append rows to an existing
        /// one. On a brand-new plan all three sections must have at least one row.
        /// Never deletes existing rows. Returns (ok, reason, planId).
        /// </summary>
        public async Task<(bool ok, string? reason, decimal planId)> SubmitPlan(ActionPlanSubmitDto dto)
        {
            // Keep only non-empty rows.
            var roots = dto.RootCauses.Where(r => !string.IsNullOrWhiteSpace(r.Root_Cause)).ToList();
            var icas = dto.Icas.Where(i => !string.IsNullOrWhiteSpace(i.Action)
                || !string.IsNullOrWhiteSpace(i.Responsibility) || i.ICA_Date.HasValue).ToList();
            var pcas = dto.Pcas.Where(p => !string.IsNullOrWhiteSpace(p.Action)
                || !string.IsNullOrWhiteSpace(p.Responsibility) || p.Target_Date.HasValue).ToList();

            using var connection = await _connectionFactory.CreateConnectionAsync();

            // Existing plan?
            decimal planId = 0;
            bool isNew;
            using (var cmd = new SqlCommand(@"
                SELECT Action_Plan_ID FROM MM_Action_Plan
                WHERE Model_ID = @M AND Part_ID = @P AND Defect_ID = @D AND Attribution_ID = @A AND Severity_ID = @S", connection))
            {
                cmd.Parameters.AddWithValue("@M", dto.Model_ID);
                cmd.Parameters.AddWithValue("@P", dto.Part_ID);
                cmd.Parameters.AddWithValue("@D", dto.Defect_ID);
                cmd.Parameters.AddWithValue("@A", dto.Attribution_ID);
                cmd.Parameters.AddWithValue("@S", dto.Severity_ID);
                var existing = await cmd.ExecuteScalarAsync();
                if (existing != null && existing != DBNull.Value) planId = Convert.ToDecimal(existing);
            }
            isNew = planId == 0;

            // First submit requires all three sections; append allows any subset.
            if (isNew && (roots.Count == 0 || icas.Count == 0 || pcas.Count == 0))
                return (false, "Root Cause, ICA and PCA each need at least one entry.", 0);
            if (roots.Count == 0 && icas.Count == 0 && pcas.Count == 0)
                return (false, "Nothing to save.", planId);

            using var transaction = connection.BeginTransaction();
            try
            {
                if (isNew)
                {
                    using var cmd = new SqlCommand(@"
                        INSERT INTO MM_Action_Plan
                            (Model_ID, Part_ID, Defect_ID, Attribution_ID, Severity_ID, Is_Submitted,
                             Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                        OUTPUT INSERTED.Action_Plan_ID
                        VALUES (@M, @P, @D, @A, @S, 1, @Host, @User, GETDATE(), @Plant)",
                        connection, transaction);
                    cmd.Parameters.AddWithValue("@M", dto.Model_ID);
                    cmd.Parameters.AddWithValue("@P", dto.Part_ID);
                    cmd.Parameters.AddWithValue("@D", dto.Defect_ID);
                    cmd.Parameters.AddWithValue("@A", dto.Attribution_ID);
                    cmd.Parameters.AddWithValue("@S", dto.Severity_ID);
                    cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@User", (object?)dto.Inserted_User_ID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Plant", (object?)dto.Plant_Code ?? DBNull.Value);
                    planId = Convert.ToDecimal(await cmd.ExecuteScalarAsync());
                }
                else
                {
                    using var cmd = new SqlCommand(
                        "UPDATE MM_Action_Plan SET Is_Submitted = 1 WHERE Action_Plan_ID = @Id", connection, transaction);
                    cmd.Parameters.AddWithValue("@Id", planId);
                    await cmd.ExecuteNonQueryAsync();
                }

                foreach (var r in roots)
                {
                    using var cmd = new SqlCommand(@"
                        INSERT INTO MM_Action_Plan_Root_Cause (Action_Plan_ID, Root_Cause, Inserted_Host, Inserted_User_ID, Inserted_Date)
                        VALUES (@Id, @Text, @Host, @User, GETDATE())", connection, transaction);
                    cmd.Parameters.AddWithValue("@Id", planId);
                    cmd.Parameters.AddWithValue("@Text", r.Root_Cause);
                    cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@User", (object?)dto.Inserted_User_ID ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                foreach (var i in icas)
                {
                    using var cmd = new SqlCommand(@"
                        INSERT INTO MM_Action_Plan_ICA (Action_Plan_ID, Action, Responsibility, ICA_Date, Inserted_Host, Inserted_User_ID, Inserted_Date)
                        VALUES (@Id, @Action, @Resp, @Date, @Host, @User, GETDATE())", connection, transaction);
                    cmd.Parameters.AddWithValue("@Id", planId);
                    cmd.Parameters.AddWithValue("@Action", i.Action ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Resp", i.Responsibility ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Date", (object?)i.ICA_Date ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@User", (object?)dto.Inserted_User_ID ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                foreach (var p in pcas)
                {
                    using var cmd = new SqlCommand(@"
                        INSERT INTO MM_Action_Plan_PCA (Action_Plan_ID, Action, Responsibility, Target_Date, Status, Inserted_Host, Inserted_User_ID, Inserted_Date)
                        VALUES (@Id, @Action, @Resp, @Date, @Status, @Host, @User, GETDATE())", connection, transaction);
                    cmd.Parameters.AddWithValue("@Id", planId);
                    cmd.Parameters.AddWithValue("@Action", p.Action ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Resp", p.Responsibility ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Date", (object?)p.Target_Date ?? DBNull.Value);
                    var st = string.IsNullOrWhiteSpace(p.Status) ? "R" : p.Status.Trim().ToUpper().Substring(0, 1);
                    cmd.Parameters.AddWithValue("@Status", st);
                    cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@User", (object?)dto.Inserted_User_ID ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                return (true, null, planId);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}
