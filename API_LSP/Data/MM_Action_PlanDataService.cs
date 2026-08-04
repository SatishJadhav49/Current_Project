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

        private static string Key(decimal part, decimal defect, decimal severity)
            => $"{part}|{defect}|{severity}";

        /// <summary>
        /// Problem rows for an attribution: distinct Part+Defect+Severity combos
        /// from reported concerns, with frequency, cutoff (latest PCA target),
        /// concerns-after-cutoff, and computed status.
        /// </summary>
        public async Task<List<ActionPlanProblemDto>> GetProblems(decimal attributionId)
        {
            var problems = new List<ActionPlanProblemDto>();
            using var connection = await _connectionFactory.CreateConnectionAsync();

            // A) Grouped problems + frequency.
            const string problemsQuery = @"
                SELECT d.Part_ID, p.Part_Name, d.Defect_ID, df.Defect_Name,
                       d.Attribution_ID, a.Attribution_Name,
                       ISNULL(d.Severity_ID, 0) AS Severity_ID,
                       ISNULL(s.Severity_Code, '') AS Severity_Code,
                       COUNT(*) AS Frequency
                FROM MM_Vehicle_Defects d
                INNER JOIN MM_Part_Master p        ON p.Part_ID = d.Part_ID
                INNER JOIN MM_Defect_Master df     ON df.Defect_ID = d.Defect_ID
                INNER JOIN MM_Attribution_Master a ON a.Attribution_ID = d.Attribution_ID
                LEFT  JOIN MM_Severity_Master s    ON s.Severity_ID = d.Severity_ID
                WHERE d.Attribution_ID = @Attr
                GROUP BY d.Part_ID, p.Part_Name, d.Defect_ID, df.Defect_Name,
                         d.Attribution_ID, a.Attribution_Name, d.Severity_ID, s.Severity_Code
                ORDER BY COUNT(*) DESC";

            using (var cmd = new SqlCommand(problemsQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Attr", attributionId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    problems.Add(new ActionPlanProblemDto
                    {
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
            }

            // B) Existing plans for this attribution + their cutoff (max PCA target).
            var plans = new Dictionary<string, (decimal Id, DateTime? Cutoff, bool Submitted)>();
            const string plansQuery = @"
                SELECT ap.Action_Plan_ID, ap.Part_ID, ap.Defect_ID, ap.Severity_ID, ap.Is_Submitted,
                       (SELECT MAX(Target_Date) FROM MM_Action_Plan_PCA WHERE Action_Plan_ID = ap.Action_Plan_ID) AS Cutoff
                FROM MM_Action_Plan ap
                WHERE ap.Attribution_ID = @Attr";
            using (var cmd = new SqlCommand(plansQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Attr", attributionId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var key = Key(reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3));
                    plans[key] = (
                        reader.GetDecimal(0),
                        reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                        !reader.IsDBNull(4) && reader.GetBoolean(4));
                }
            }

            // C) Concerns after each plan's cutoff (recurrence).
            var afterCutoff = new Dictionary<string, int>();
            const string afterQuery = @"
                SELECT d.Part_ID, d.Defect_ID, ISNULL(d.Severity_ID, 0) AS Severity_ID, COUNT(*) AS Cnt
                FROM MM_Vehicle_Defects d
                INNER JOIN MM_Action_Plan ap
                    ON ap.Part_ID = d.Part_ID AND ap.Defect_ID = d.Defect_ID
                   AND ap.Severity_ID = ISNULL(d.Severity_ID, 0)
                   AND ap.Attribution_ID = d.Attribution_ID
                CROSS APPLY (
                    SELECT MAX(Target_Date) AS Cutoff FROM MM_Action_Plan_PCA WHERE Action_Plan_ID = ap.Action_Plan_ID
                ) c
                WHERE d.Attribution_ID = @Attr AND c.Cutoff IS NOT NULL AND d.Inserted_Date > c.Cutoff
                GROUP BY d.Part_ID, d.Defect_ID, ISNULL(d.Severity_ID, 0)";
            using (var cmd = new SqlCommand(afterQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Attr", attributionId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    afterCutoff[Key(reader.GetDecimal(0), reader.GetDecimal(1), reader.GetDecimal(2))] =
                        Convert.ToInt32(reader.GetValue(3));
            }

            // Merge + compute status.
            foreach (var pr in problems)
            {
                var key = Key(pr.Part_ID, pr.Defect_ID, pr.Severity_ID);
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
            decimal partId, decimal defectId, decimal attributionId, decimal severityId)
        {
            var detail = new ActionPlanDetailDto
            {
                Part_ID = partId,
                Defect_ID = defectId,
                Attribution_ID = attributionId,
                Severity_ID = severityId,
            };

            using var connection = await _connectionFactory.CreateConnectionAsync();

            decimal planId = 0;
            using (var cmd = new SqlCommand(@"
                SELECT Action_Plan_ID, Is_Submitted FROM MM_Action_Plan
                WHERE Part_ID = @P AND Defect_ID = @D AND Attribution_ID = @A AND Severity_ID = @S", connection))
            {
                cmd.Parameters.AddWithValue("@P", partId);
                cmd.Parameters.AddWithValue("@D", defectId);
                cmd.Parameters.AddWithValue("@A", attributionId);
                cmd.Parameters.AddWithValue("@S", severityId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    planId = reader.GetDecimal(0);
                    detail.Is_Submitted = !reader.IsDBNull(1) && reader.GetBoolean(1);
                }
            }

            if (planId == 0) return detail; // no plan yet
            detail.Action_Plan_ID = planId;

            using (var cmd = new SqlCommand(
                "SELECT Root_Cause_ID, ISNULL(Root_Cause,'') FROM MM_Action_Plan_Root_Cause WHERE Action_Plan_ID = @Id ORDER BY Root_Cause_ID", connection))
            {
                cmd.Parameters.AddWithValue("@Id", planId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    detail.RootCauses.Add(new ActionPlanRootCauseDto
                    {
                        Root_Cause_ID = reader.GetDecimal(0),
                        Root_Cause = reader.GetString(1),
                    });
            }

            using (var cmd = new SqlCommand(
                "SELECT ICA_ID, ISNULL(Action,''), ISNULL(Responsibility,''), ICA_Date FROM MM_Action_Plan_ICA WHERE Action_Plan_ID = @Id ORDER BY ICA_ID", connection))
            {
                cmd.Parameters.AddWithValue("@Id", planId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    detail.Icas.Add(new ActionPlanIcaDto
                    {
                        ICA_ID = reader.GetDecimal(0),
                        Action = reader.GetString(1),
                        Responsibility = reader.GetString(2),
                        ICA_Date = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                    });
            }

            using (var cmd = new SqlCommand(
                "SELECT PCA_ID, ISNULL(Action,''), ISNULL(Responsibility,''), Target_Date, ISNULL(Status,'R') FROM MM_Action_Plan_PCA WHERE Action_Plan_ID = @Id ORDER BY PCA_ID", connection))
            {
                cmd.Parameters.AddWithValue("@Id", planId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    detail.Pcas.Add(new ActionPlanPcaDto
                    {
                        PCA_ID = reader.GetDecimal(0),
                        Action = reader.GetString(1),
                        Responsibility = reader.GetString(2),
                        Target_Date = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                        Status = reader.GetString(4),
                    });
            }

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
                WHERE Part_ID = @P AND Defect_ID = @D AND Attribution_ID = @A AND Severity_ID = @S", connection))
            {
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
                            (Part_ID, Defect_ID, Attribution_ID, Severity_ID, Is_Submitted,
                             Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                        OUTPUT INSERTED.Action_Plan_ID
                        VALUES (@P, @D, @A, @S, 1, @Host, @User, GETDATE(), @Plant)",
                        connection, transaction);
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
