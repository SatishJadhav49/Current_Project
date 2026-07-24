using API_LSP.Models.DTOs;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace API_LSP.Data
{
    public class MM_Vehicle_TrackingDataService
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MM_Vehicle_TrackingDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        /// <summary>
        /// Save a stage entry:
        ///  • Upserts MM_Vehicle_Track_Logs (one row per Vehicle×Stage by UNIQUE constraint).
        ///  • If concerns exist → bulk inserts into MM_Vehicle_Defects.
        ///  • If Is_No_Defect = true → only the Track_Logs row is written.
        /// Runs inside a single transaction.
        /// </summary>
        public async Task<StageSaveResultDto> SaveStage(StageSaveDto dto)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // 1) Upsert MM_Vehicle_Track_Logs (one row per Vehicle×Stage)
                decimal trackLogId = 0;
                const string upsertLogQuery = @"
                    IF EXISTS (SELECT 1 FROM MM_Vehicle_Track_Logs
                               WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = @Audit_Area_ID)
                    BEGIN
                        UPDATE MM_Vehicle_Track_Logs
                        SET Auditor_Name = @Auditor_Name,
                            Is_No_Defect = @Is_No_Defect,
                            Log_Date = GETDATE(),
                            Documents = @Documents,
                            Is_Edited = 1,
                            Updated_Host = @Host,
                            Updated_User_ID = @UserId,
                            Updated_Date = GETDATE(),
                            Plant_Code = @Plant_Code
                        OUTPUT INSERTED.Track_Log_ID
                        WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = @Audit_Area_ID;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO MM_Vehicle_Track_Logs
                            (Vehicle_ID, Audit_Area_ID, Auditor_Name, Is_No_Defect, Log_Date,
                             Documents, Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                        OUTPUT INSERTED.Track_Log_ID
                        VALUES (@Vehicle_ID, @Audit_Area_ID, @Auditor_Name, @Is_No_Defect, GETDATE(),
                                @Documents, @Host, @UserId, GETDATE(), @Plant_Code);
                    END";

                using (var cmd = new SqlCommand(upsertLogQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@Vehicle_ID", dto.Vehicle_ID);
                    cmd.Parameters.AddWithValue("@Audit_Area_ID", dto.Audit_Area_ID);
                    cmd.Parameters.AddWithValue("@Auditor_Name", (object?)dto.Auditor_Name ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Is_No_Defect", dto.Is_No_Defect);
                    cmd.Parameters.AddWithValue("@Documents", (object?)dto.Documents ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@UserId", (object?)dto.Inserted_User_ID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Plant_Code", (object?)dto.Plant_Code ?? DBNull.Value);

                    var scalar = await cmd.ExecuteScalarAsync();
                    if (scalar != null && scalar != DBNull.Value)
                        trackLogId = Convert.ToDecimal(scalar);
                }

                // 2) If edited stage, clear any existing defects so new list replaces
                const string deleteOldDefectsQuery = @"
                    DELETE FROM MM_Vehicle_Defects
                    WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = @Audit_Area_ID";

                using (var delCmd = new SqlCommand(deleteOldDefectsQuery, connection, transaction))
                {
                    delCmd.Parameters.AddWithValue("@Vehicle_ID", dto.Vehicle_ID);
                    delCmd.Parameters.AddWithValue("@Audit_Area_ID", dto.Audit_Area_ID);
                    await delCmd.ExecuteNonQueryAsync();
                }

                // 3) Insert concerns (only if not "no defect" and list non-empty)
                int saved = 0;
                if (!dto.Is_No_Defect && dto.Concerns != null && dto.Concerns.Count > 0)
                {
                    const string insertDefectQuery = @"
                        INSERT INTO MM_Vehicle_Defects
                            (Vehicle_ID, Audit_Area_ID, Vehicle_Area_ID, Part_ID, Defect_ID,
                             Location_ID, Severity_ID, Attribution_ID, Defect_Type_ID, Remark,
                             Area_Image_ID, Image_X, Image_Y,
                             Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                        VALUES
                            (@Vehicle_ID, @Audit_Area_ID, @Vehicle_Area_ID, @Part_ID, @Defect_ID,
                             @Location_ID, @Severity_ID, @Attribution_ID, @Defect_Type_ID, @Remark,
                             @Area_Image_ID, @Image_X, @Image_Y,
                             @Host, @UserId, GETDATE(), @Plant_Code)";

                    foreach (var c in dto.Concerns)
                    {
                        using var cmd = new SqlCommand(insertDefectQuery, connection, transaction);
                        cmd.Parameters.AddWithValue("@Vehicle_ID", dto.Vehicle_ID);
                        cmd.Parameters.AddWithValue("@Audit_Area_ID", dto.Audit_Area_ID);
                        cmd.Parameters.AddWithValue("@Vehicle_Area_ID", (object?)c.Vehicle_Area_ID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Part_ID", c.Part_ID);
                        cmd.Parameters.AddWithValue("@Defect_ID", c.Defect_ID);
                        cmd.Parameters.AddWithValue("@Location_ID", c.Location_ID);
                        cmd.Parameters.AddWithValue("@Severity_ID", (object?)c.Severity_ID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Attribution_ID", (object?)c.Attribution_ID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Defect_Type_ID", (object?)c.Defect_Type_ID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Remark", (object?)c.Remark ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Area_Image_ID", (object?)c.Area_Image_ID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Image_X", (object?)c.Image_X ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Image_Y", (object?)c.Image_Y ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@UserId", (object?)dto.Inserted_User_ID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Plant_Code", (object?)dto.Plant_Code ?? DBNull.Value);
                        await cmd.ExecuteNonQueryAsync();
                        saved++;
                    }
                }

                transaction.Commit();
                return new StageSaveResultDto
                {
                    Track_Log_ID = trackLogId,
                    Concerns_Saved = saved,
                };
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Append a single concern. Upserts Track_Logs (Is_No_Defect = 0), then
        /// inserts exactly one row into MM_Vehicle_Defects. No DELETE step —
        /// previous concerns stay. Returns the new Defect_Entry_ID + Track_Log_ID.
        /// </summary>
        public async Task<AddConcernResultDto> AddConcern(StageSaveDto dto)
        {
            if (dto.Concerns == null || dto.Concerns.Count != 1)
                throw new ArgumentException("AddConcern expects exactly one concern in the payload.");

            using var connection = await _connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // 1) Upsert Track_Logs (force Is_No_Defect = 0 since we're recording a defect)
                decimal trackLogId = 0;
                const string upsertLogQuery = @"
                    IF EXISTS (SELECT 1 FROM MM_Vehicle_Track_Logs
                               WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = @Audit_Area_ID)
                    BEGIN
                        UPDATE MM_Vehicle_Track_Logs
                        SET Auditor_Name = @Auditor_Name,
                            Is_No_Defect = 0,
                            Log_Date = GETDATE(),
                            Is_Edited = 1,
                            Updated_Host = @Host,
                            Updated_User_ID = @UserId,
                            Updated_Date = GETDATE(),
                            Plant_Code = @Plant_Code
                        OUTPUT INSERTED.Track_Log_ID
                        WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = @Audit_Area_ID;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO MM_Vehicle_Track_Logs
                            (Vehicle_ID, Audit_Area_ID, Auditor_Name, Is_No_Defect, Log_Date,
                             Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                        OUTPUT INSERTED.Track_Log_ID
                        VALUES (@Vehicle_ID, @Audit_Area_ID, @Auditor_Name, 0, GETDATE(),
                                @Host, @UserId, GETDATE(), @Plant_Code);
                    END";

                using (var cmd = new SqlCommand(upsertLogQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@Vehicle_ID", dto.Vehicle_ID);
                    cmd.Parameters.AddWithValue("@Audit_Area_ID", dto.Audit_Area_ID);
                    cmd.Parameters.AddWithValue("@Auditor_Name", (object?)dto.Auditor_Name ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@UserId", (object?)dto.Inserted_User_ID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Plant_Code", (object?)dto.Plant_Code ?? DBNull.Value);
                    var scalar = await cmd.ExecuteScalarAsync();
                    if (scalar != null && scalar != DBNull.Value)
                        trackLogId = Convert.ToDecimal(scalar);
                }

                // 2) Insert the single concern, capture the new Defect_Entry_ID
                var c = dto.Concerns[0];
                decimal defectEntryId = 0;
                const string insertDefectQuery = @"
                    INSERT INTO MM_Vehicle_Defects
                        (Vehicle_ID, Audit_Area_ID, Vehicle_Area_ID, Part_ID, Defect_ID,
                         Location_ID, Severity_ID, Attribution_ID, Defect_Type_ID, Remark,
                         Area_Image_ID, Image_X, Image_Y,
                         Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                    OUTPUT INSERTED.Defect_Entry_ID
                    VALUES
                        (@Vehicle_ID, @Audit_Area_ID, @Vehicle_Area_ID, @Part_ID, @Defect_ID,
                         @Location_ID, @Severity_ID, @Attribution_ID, @Defect_Type_ID, @Remark,
                         @Area_Image_ID, @Image_X, @Image_Y,
                         @Host, @UserId, GETDATE(), @Plant_Code)";

                using (var cmd = new SqlCommand(insertDefectQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@Vehicle_ID", dto.Vehicle_ID);
                    cmd.Parameters.AddWithValue("@Audit_Area_ID", dto.Audit_Area_ID);
                    cmd.Parameters.AddWithValue("@Vehicle_Area_ID", (object?)c.Vehicle_Area_ID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Part_ID", c.Part_ID);
                    cmd.Parameters.AddWithValue("@Defect_ID", c.Defect_ID);
                    cmd.Parameters.AddWithValue("@Location_ID", c.Location_ID);
                    cmd.Parameters.AddWithValue("@Severity_ID", (object?)c.Severity_ID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Attribution_ID", (object?)c.Attribution_ID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Defect_Type_ID", (object?)c.Defect_Type_ID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Remark", (object?)c.Remark ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Area_Image_ID", (object?)c.Area_Image_ID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Image_X", (object?)c.Image_X ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Image_Y", (object?)c.Image_Y ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@UserId", (object?)dto.Inserted_User_ID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Plant_Code", (object?)dto.Plant_Code ?? DBNull.Value);
                    var scalar = await cmd.ExecuteScalarAsync();
                    if (scalar != null && scalar != DBNull.Value)
                        defectEntryId = Convert.ToDecimal(scalar);
                }

                transaction.Commit();
                return new AddConcernResultDto
                {
                    Track_Log_ID = trackLogId,
                    Defect_Entry_ID = defectEntryId,
                };
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Computes the Physical RFD readiness for a vehicle:
        ///  • Each required stage (Road Test, Underbody, LSP Audit) has a Track_Logs row.
        ///  • DTC has a Track_Logs row AND Is_No_Defect = 1 (i.e. OK).
        ///  • Every concern has a MM_Vehicle_Defect_Rework row with Rework_Done = 1.
        /// Also returns the list of concerns still pending rework so the RFD UI
        /// can show the operator exactly what is blocking dispatch.
        /// </summary>
        public async Task<RfdReadinessDto> GetRfdReadiness(decimal vehicleId)
        {
            var result = new RfdReadinessDto();
            using var connection = await _connectionFactory.CreateConnectionAsync();

            // 1) Stage Track_Logs for every status-bearing stage:
            //    required: Road Test (1), Underbody (2), DTC (3), LSP Audit (6),
            //              VMS (7), Paint Verification (12).
            //    info only: ECOS (5).
            const string stagesQuery = @"
                SELECT Audit_Area_ID, Auditor_Name, Is_No_Defect, Log_Date
                FROM MM_Vehicle_Track_Logs
                WHERE Vehicle_ID = @Vid AND Audit_Area_ID IN (1, 2, 3, 5, 6, 7, 12)";

            using (var cmd = new SqlCommand(stagesQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Vid", vehicleId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var areaId = reader.GetDecimal(reader.GetOrdinal("Audit_Area_ID"));
                    var auditor = reader.IsDBNull(reader.GetOrdinal("Auditor_Name")) ? "" : reader.GetString(reader.GetOrdinal("Auditor_Name"));
                    var isNoDefect = !reader.IsDBNull(reader.GetOrdinal("Is_No_Defect")) && reader.GetBoolean(reader.GetOrdinal("Is_No_Defect"));
                    var logDate = reader.IsDBNull(reader.GetOrdinal("Log_Date")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("Log_Date"));

                    if (areaId == 1m)
                    {
                        result.RoadTest.Completed = true;
                        result.RoadTest.AuditorName = auditor;
                        result.RoadTest.LogDate = logDate;
                    }
                    else if (areaId == 2m)
                    {
                        result.Underbody.Completed = true;
                        result.Underbody.AuditorName = auditor;
                        result.Underbody.LogDate = logDate;
                    }
                    else if (areaId == 6m)
                    {
                        result.LspAudit.Completed = true;
                        result.LspAudit.AuditorName = auditor;
                        result.LspAudit.LogDate = logDate;
                    }
                    else if (areaId == 3m)
                    {
                        result.Dtc.Completed = true;
                        result.Dtc.AuditorName = auditor;
                        result.Dtc.LogDate = logDate;
                        result.Dtc.IsOk = isNoDefect;
                    }
                    else if (areaId == 5m)
                    {
                        result.Ecos.Completed = true;
                        result.Ecos.AuditorName = auditor;
                        result.Ecos.LogDate = logDate;
                        result.Ecos.IsOk = isNoDefect;
                    }
                    else if (areaId == 7m)
                    {
                        result.Vms.Completed = true;
                        result.Vms.AuditorName = auditor;
                        result.Vms.LogDate = logDate;
                        result.Vms.IsOk = isNoDefect;
                    }
                    else if (areaId == 12m)
                    {
                        result.Paint.Completed = true;
                        result.Paint.AuditorName = auditor;
                        result.Paint.LogDate = logDate;
                        result.Paint.IsOk = isNoDefect;
                    }
                }
            }

            // 1b) Stage-level rework — a NOK stage passes RFD if it has been
            //     reworked (Rework_Done = 1). Overlays the RfdStatusCheckDto so
            //     the UI can show "reworked" chip instead of red ✗.
            const string stageReworkQuery = @"
                SELECT Audit_Area_ID, Rework_Done, Rework_By, Remark, Rework_Date
                FROM MM_Vehicle_Stage_Rework
                WHERE Vehicle_ID = @Vid AND Audit_Area_ID IN (3, 5, 7, 12)";

            using (var cmd = new SqlCommand(stageReworkQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Vid", vehicleId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var areaId = reader.GetDecimal(reader.GetOrdinal("Audit_Area_ID"));
                    var done = !reader.IsDBNull(reader.GetOrdinal("Rework_Done")) && reader.GetBoolean(reader.GetOrdinal("Rework_Done"));
                    var by = reader.IsDBNull(reader.GetOrdinal("Rework_By")) ? null : reader.GetString(reader.GetOrdinal("Rework_By"));
                    var remark = reader.IsDBNull(reader.GetOrdinal("Remark")) ? null : reader.GetString(reader.GetOrdinal("Remark"));
                    var date = reader.IsDBNull(reader.GetOrdinal("Rework_Date")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("Rework_Date"));

                    RfdStatusCheckDto? target =
                        areaId == 3m  ? result.Dtc :
                        areaId == 5m  ? result.Ecos :
                        areaId == 7m  ? result.Vms :
                        areaId == 12m ? result.Paint : null;
                    if (target == null) continue;

                    target.StageReworkDone = done;
                    target.StageReworkBy = by;
                    target.StageReworkRemark = remark;
                    target.StageReworkDate = date;
                }
            }

            // 2) Concern counts + rework status
            const string reworkCountQuery = @"
                SELECT COUNT(d.Defect_Entry_ID) AS Total,
                       ISNULL(SUM(CASE WHEN r.Rework_Done = 1 THEN 1 ELSE 0 END), 0) AS Done
                FROM MM_Vehicle_Defects d
                LEFT JOIN MM_Vehicle_Defect_Rework r ON r.Defect_Entry_ID = d.Defect_Entry_ID
                WHERE d.Vehicle_ID = @Vid";

            using (var cmd = new SqlCommand(reworkCountQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Vid", vehicleId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result.Rework.TotalConcerns = Convert.ToInt32(reader.GetValue(0));
                    result.Rework.DoneCount = Convert.ToInt32(reader.GetValue(1));
                    result.Rework.PendingCount = result.Rework.TotalConcerns - result.Rework.DoneCount;
                }
            }

            // 3) Pending concerns (only if any) — joined with master names for UI render
            if (result.Rework.PendingCount > 0)
            {
                const string pendingQuery = @"
                    SELECT
                        d.Defect_Entry_ID,
                        d.Audit_Area_ID, aa.Audit_Area_Name,
                        va.Vehicle_Area_Name,
                        p.Part_Name,
                        df.Defect_Name,
                        l.Location_Name,
                        s.Severity_Code,
                        a.Attribution_Name,
                        dt.Defect_Type_Name,
                        d.Remark
                    FROM MM_Vehicle_Defects d
                    INNER JOIN MM_Audit_Area_Master aa ON aa.Audit_Area_ID = d.Audit_Area_ID
                    LEFT  JOIN MM_Vehicle_Area_Master va ON va.Vehicle_Area_ID = d.Vehicle_Area_ID
                    INNER JOIN MM_Part_Master p ON p.Part_ID = d.Part_ID
                    INNER JOIN MM_Defect_Master df ON df.Defect_ID = d.Defect_ID
                    INNER JOIN MM_Location_Master l ON l.Location_ID = d.Location_ID
                    LEFT  JOIN MM_Severity_Master s ON s.Severity_ID = d.Severity_ID
                    LEFT  JOIN MM_Attribution_Master a ON a.Attribution_ID = d.Attribution_ID
                    LEFT  JOIN MM_Defect_Type_Master dt ON dt.Defect_Type_ID = d.Defect_Type_ID
                    LEFT  JOIN MM_Vehicle_Defect_Rework r ON r.Defect_Entry_ID = d.Defect_Entry_ID
                    WHERE d.Vehicle_ID = @Vid
                      AND (r.Rework_Done IS NULL OR r.Rework_Done = 0)
                    ORDER BY aa.SORTORDER, d.Defect_Entry_ID";

                using var cmd = new SqlCommand(pendingQuery, connection);
                cmd.Parameters.AddWithValue("@Vid", vehicleId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.PendingConcerns.Add(new
                    {
                        Defect_Entry_ID = reader.GetDecimal(reader.GetOrdinal("Defect_Entry_ID")),
                        Audit_Area_ID = reader.GetDecimal(reader.GetOrdinal("Audit_Area_ID")),
                        Audit_Area_Name = reader.GetString(reader.GetOrdinal("Audit_Area_Name")),
                        Vehicle_Area_Name = reader.IsDBNull(reader.GetOrdinal("Vehicle_Area_Name")) ? "" : reader.GetString(reader.GetOrdinal("Vehicle_Area_Name")),
                        Part_Name = reader.GetString(reader.GetOrdinal("Part_Name")),
                        Defect_Name = reader.GetString(reader.GetOrdinal("Defect_Name")),
                        Location_Name = reader.GetString(reader.GetOrdinal("Location_Name")),
                        Severity_Code = reader.IsDBNull(reader.GetOrdinal("Severity_Code")) ? "" : reader.GetString(reader.GetOrdinal("Severity_Code")),
                        Attribution_Name = reader.IsDBNull(reader.GetOrdinal("Attribution_Name")) ? "" : reader.GetString(reader.GetOrdinal("Attribution_Name")),
                        Defect_Type_Name = reader.IsDBNull(reader.GetOrdinal("Defect_Type_Name")) ? "" : reader.GetString(reader.GetOrdinal("Defect_Type_Name")),
                        Remark = reader.IsDBNull(reader.GetOrdinal("Remark")) ? null : reader.GetString(reader.GetOrdinal("Remark")),
                    });
                }
            }

            // 4) Reworked concerns (informational — shown below the readiness
            //    check so the operator can see who reworked each concern).
            if (result.Rework.DoneCount > 0)
            {
                const string reworkedQuery = @"
                    SELECT
                        d.Defect_Entry_ID,
                        d.Audit_Area_ID, aa.Audit_Area_Name,
                        va.Vehicle_Area_Name,
                        p.Part_Name,
                        df.Defect_Name,
                        l.Location_Name,
                        s.Severity_Code,
                        a.Attribution_Name,
                        dt.Defect_Type_Name,
                        d.Remark,
                        r.Rework_By,
                        r.Rework_Date
                    FROM MM_Vehicle_Defects d
                    INNER JOIN MM_Audit_Area_Master aa ON aa.Audit_Area_ID = d.Audit_Area_ID
                    LEFT  JOIN MM_Vehicle_Area_Master va ON va.Vehicle_Area_ID = d.Vehicle_Area_ID
                    INNER JOIN MM_Part_Master p ON p.Part_ID = d.Part_ID
                    INNER JOIN MM_Defect_Master df ON df.Defect_ID = d.Defect_ID
                    INNER JOIN MM_Location_Master l ON l.Location_ID = d.Location_ID
                    LEFT  JOIN MM_Severity_Master s ON s.Severity_ID = d.Severity_ID
                    LEFT  JOIN MM_Attribution_Master a ON a.Attribution_ID = d.Attribution_ID
                    LEFT  JOIN MM_Defect_Type_Master dt ON dt.Defect_Type_ID = d.Defect_Type_ID
                    INNER JOIN MM_Vehicle_Defect_Rework r ON r.Defect_Entry_ID = d.Defect_Entry_ID
                    WHERE d.Vehicle_ID = @Vid AND r.Rework_Done = 1
                    ORDER BY aa.SORTORDER, d.Defect_Entry_ID";

                using var cmd = new SqlCommand(reworkedQuery, connection);
                cmd.Parameters.AddWithValue("@Vid", vehicleId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.ReworkedConcerns.Add(new
                    {
                        Defect_Entry_ID = reader.GetDecimal(reader.GetOrdinal("Defect_Entry_ID")),
                        Audit_Area_ID = reader.GetDecimal(reader.GetOrdinal("Audit_Area_ID")),
                        Audit_Area_Name = reader.GetString(reader.GetOrdinal("Audit_Area_Name")),
                        Vehicle_Area_Name = reader.IsDBNull(reader.GetOrdinal("Vehicle_Area_Name")) ? "" : reader.GetString(reader.GetOrdinal("Vehicle_Area_Name")),
                        Part_Name = reader.GetString(reader.GetOrdinal("Part_Name")),
                        Defect_Name = reader.GetString(reader.GetOrdinal("Defect_Name")),
                        Location_Name = reader.GetString(reader.GetOrdinal("Location_Name")),
                        Severity_Code = reader.IsDBNull(reader.GetOrdinal("Severity_Code")) ? "" : reader.GetString(reader.GetOrdinal("Severity_Code")),
                        Attribution_Name = reader.IsDBNull(reader.GetOrdinal("Attribution_Name")) ? "" : reader.GetString(reader.GetOrdinal("Attribution_Name")),
                        Defect_Type_Name = reader.IsDBNull(reader.GetOrdinal("Defect_Type_Name")) ? "" : reader.GetString(reader.GetOrdinal("Defect_Type_Name")),
                        Remark = reader.IsDBNull(reader.GetOrdinal("Remark")) ? null : reader.GetString(reader.GetOrdinal("Remark")),
                        Rework_By = reader.IsDBNull(reader.GetOrdinal("Rework_By")) ? "" : reader.GetString(reader.GetOrdinal("Rework_By")),
                        Rework_Date = reader.IsDBNull(reader.GetOrdinal("Rework_Date")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("Rework_Date")),
                    });
                }
            }

            // 5) Ready flag — ECOS is informational only; DTC and VMS must be
            //    resolved (OK, or NOK + stage rework done). Paint Verification
            //    is a defect-entry stage like Road Test: completing it is the
            //    gate, and its concerns roll into the rework pending count.
            static bool StagePassed(RfdStatusCheckDto s) =>
                s.IsOk == true || (s.IsOk == false && s.StageReworkDone);

            result.Ready = result.RoadTest.Completed
                        && result.Underbody.Completed
                        && result.LspAudit.Completed
                        && result.Paint.Completed
                        && StagePassed(result.Dtc)
                        && StagePassed(result.Vms)
                        && result.Rework.PendingCount == 0;

            return result;
        }

        /// <summary>
        /// Re-runs the RFD readiness check server-side (defence in depth), then
        /// upserts the Track_Logs row for Audit_Area_ID = 11. Returns the saved
        /// row ID so the controller can confirm.
        /// </summary>
        public async Task<(bool ok, string? reason, decimal trackLogId)> SaveRfd(SaveRfdDto dto)
        {
            var readiness = await GetRfdReadiness(dto.Vehicle_ID);
            if (!readiness.Ready)
            {
                var reasons = new List<string>();
                if (!readiness.RoadTest.Completed) reasons.Add("Road Test not saved");
                if (!readiness.Underbody.Completed) reasons.Add("Underbody not saved");
                if (!readiness.LspAudit.Completed) reasons.Add("LSP Audit not saved");
                if (readiness.Dtc.IsOk != true) reasons.Add(readiness.Dtc.Completed ? "DTC is Not OK" : "DTC not saved");
                if (readiness.Rework.PendingCount > 0)
                    reasons.Add($"Rework pending — {readiness.Rework.PendingCount} of {readiness.Rework.TotalConcerns} concern(s)");
                return (false, string.Join("; ", reasons), 0);
            }

            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string upsertQuery = @"
                IF EXISTS (SELECT 1 FROM MM_Vehicle_Track_Logs
                           WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = 11)
                BEGIN
                    UPDATE MM_Vehicle_Track_Logs
                    SET Auditor_Name = @Operator_Name,
                        Is_No_Defect = 1,
                        Log_Date     = GETDATE(),
                        Is_Edited    = 1,
                        Updated_Host = @Host,
                        Updated_User_ID = @UserId,
                        Updated_Date = GETDATE(),
                        Plant_Code   = @Plant_Code
                    OUTPUT INSERTED.Track_Log_ID
                    WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = 11;
                END
                ELSE
                BEGIN
                    INSERT INTO MM_Vehicle_Track_Logs
                        (Vehicle_ID, Audit_Area_ID, Auditor_Name, Is_No_Defect, Log_Date,
                         Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                    OUTPUT INSERTED.Track_Log_ID
                    VALUES (@Vehicle_ID, 11, @Operator_Name, 1, GETDATE(),
                            @Host, @UserId, GETDATE(), @Plant_Code);
                END";

            using var cmd = new SqlCommand(upsertQuery, connection);
            cmd.Parameters.AddWithValue("@Vehicle_ID", dto.Vehicle_ID);
            cmd.Parameters.AddWithValue("@Operator_Name", (object?)dto.Operator_Name ?? string.Empty);
            cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", (object?)dto.Inserted_User_ID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Plant_Code", (object?)dto.Plant_Code ?? DBNull.Value);

            decimal trackLogId = 0;
            var scalar = await cmd.ExecuteScalarAsync();
            if (scalar != null && scalar != DBNull.Value)
                trackLogId = Convert.ToDecimal(scalar);

            return (true, null, trackLogId);
        }

        /// <summary>
        /// Rework payload: every defect on a vehicle (joined with master + stage
        /// names) LEFT JOINed with its rework state from MM_Vehicle_Defect_Rework.
        /// Rework intentionally writes no Track_Logs row — rework may be done by
        /// multiple teams attributed per concern, so there's no single auditor.
        /// </summary>
        public async Task<object> GetReworkData(decimal vehicleId)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            // Concerns + existing rework state (LEFT JOIN — no rework row yet = NULLs)
            var concerns = new List<object>();
            const string concernsQuery = @"
                SELECT
                    d.Defect_Entry_ID,
                    d.Audit_Area_ID, aa.Audit_Area_Name,
                    d.Vehicle_Area_ID, va.Vehicle_Area_Name,
                    d.Part_ID, p.Part_Name,
                    d.Defect_ID, df.Defect_Name,
                    d.Location_ID, l.Location_Name,
                    d.Severity_ID, s.Severity_Code, s.Severity_Name,
                    d.Attribution_ID, a.Attribution_Name,
                    d.Defect_Type_ID, dt.Defect_Type_Name,
                    d.Remark,
                    r.Rework_ID,
                    r.Rework_Done,
                    r.Rework_By,
                    r.Rework_Date,
                    r.Remark AS Rework_Remark
                FROM MM_Vehicle_Defects d
                INNER JOIN MM_Audit_Area_Master aa ON aa.Audit_Area_ID = d.Audit_Area_ID
                LEFT  JOIN MM_Vehicle_Area_Master va ON va.Vehicle_Area_ID = d.Vehicle_Area_ID
                INNER JOIN MM_Part_Master p ON p.Part_ID = d.Part_ID
                INNER JOIN MM_Defect_Master df ON df.Defect_ID = d.Defect_ID
                INNER JOIN MM_Location_Master l ON l.Location_ID = d.Location_ID
                LEFT  JOIN MM_Severity_Master s ON s.Severity_ID = d.Severity_ID
                LEFT  JOIN MM_Attribution_Master a ON a.Attribution_ID = d.Attribution_ID
                LEFT  JOIN MM_Defect_Type_Master dt ON dt.Defect_Type_ID = d.Defect_Type_ID
                LEFT  JOIN MM_Vehicle_Defect_Rework r ON r.Defect_Entry_ID = d.Defect_Entry_ID
                WHERE d.Vehicle_ID = @Vehicle_ID
                ORDER BY aa.SORTORDER, d.Defect_Entry_ID";

            using (var cmd = new SqlCommand(concernsQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Vehicle_ID", vehicleId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    concerns.Add(new
                    {
                        Defect_Entry_ID = reader.GetDecimal(reader.GetOrdinal("Defect_Entry_ID")),
                        Audit_Area_ID = reader.GetDecimal(reader.GetOrdinal("Audit_Area_ID")),
                        Audit_Area_Name = reader.GetString(reader.GetOrdinal("Audit_Area_Name")),
                        Vehicle_Area_ID = reader.IsDBNull(reader.GetOrdinal("Vehicle_Area_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Vehicle_Area_ID")),
                        Vehicle_Area_Name = reader.IsDBNull(reader.GetOrdinal("Vehicle_Area_Name")) ? "" : reader.GetString(reader.GetOrdinal("Vehicle_Area_Name")),
                        Part_ID = reader.GetDecimal(reader.GetOrdinal("Part_ID")),
                        Part_Name = reader.GetString(reader.GetOrdinal("Part_Name")),
                        Defect_ID = reader.GetDecimal(reader.GetOrdinal("Defect_ID")),
                        Defect_Name = reader.GetString(reader.GetOrdinal("Defect_Name")),
                        Location_ID = reader.GetDecimal(reader.GetOrdinal("Location_ID")),
                        Location_Name = reader.GetString(reader.GetOrdinal("Location_Name")),
                        Severity_ID = reader.IsDBNull(reader.GetOrdinal("Severity_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Severity_ID")),
                        Severity_Code = reader.IsDBNull(reader.GetOrdinal("Severity_Code")) ? "" : reader.GetString(reader.GetOrdinal("Severity_Code")),
                        Attribution_ID = reader.IsDBNull(reader.GetOrdinal("Attribution_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Attribution_ID")),
                        Attribution_Name = reader.IsDBNull(reader.GetOrdinal("Attribution_Name")) ? "" : reader.GetString(reader.GetOrdinal("Attribution_Name")),
                        Defect_Type_ID = reader.IsDBNull(reader.GetOrdinal("Defect_Type_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Defect_Type_ID")),
                        Defect_Type_Name = reader.IsDBNull(reader.GetOrdinal("Defect_Type_Name")) ? "" : reader.GetString(reader.GetOrdinal("Defect_Type_Name")),
                        Remark = reader.IsDBNull(reader.GetOrdinal("Remark")) ? null : reader.GetString(reader.GetOrdinal("Remark")),
                        Rework_ID = reader.IsDBNull(reader.GetOrdinal("Rework_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Rework_ID")),
                        Rework_Done = reader.IsDBNull(reader.GetOrdinal("Rework_Done")) ? (bool?)null : reader.GetBoolean(reader.GetOrdinal("Rework_Done")),
                        Rework_By = reader.IsDBNull(reader.GetOrdinal("Rework_By")) ? "" : reader.GetString(reader.GetOrdinal("Rework_By")),
                        Rework_Date = reader.IsDBNull(reader.GetOrdinal("Rework_Date")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("Rework_Date")),
                        Rework_Remark = reader.IsDBNull(reader.GetOrdinal("Rework_Remark")) ? null : reader.GetString(reader.GetOrdinal("Rework_Remark")),
                    });
                }
            }

            // Stage-level failures — one row per NOK status stage (DTC/ECOS/VMS/Paint)
            // joined with any existing MM_Vehicle_Stage_Rework entry. This is what
            // powers the "Stage Failures" section on the Rework page.
            var stageFailures = new List<object>();
            const string stageFailuresQuery = @"
                SELECT
                    tl.Audit_Area_ID, aa.Audit_Area_Name,
                    tl.Auditor_Name, tl.Log_Date,
                    tl.Documents AS Stage_Documents,
                    sr.Stage_Rework_ID,
                    sr.Rework_Done AS Stage_Rework_Done,
                    sr.Rework_By    AS Stage_Rework_By,
                    sr.Remark       AS Stage_Rework_Remark,
                    sr.Documents    AS Stage_Rework_Documents,
                    sr.Rework_Date  AS Stage_Rework_Date
                FROM MM_Vehicle_Track_Logs tl
                INNER JOIN MM_Audit_Area_Master aa ON aa.Audit_Area_ID = tl.Audit_Area_ID
                LEFT  JOIN MM_Vehicle_Stage_Rework sr
                       ON sr.Vehicle_ID = tl.Vehicle_ID
                      AND sr.Audit_Area_ID = tl.Audit_Area_ID
                WHERE tl.Vehicle_ID = @Vehicle_ID
                  AND tl.Audit_Area_ID IN (3, 5, 7)   -- DTC, ECOS, VMS (Paint is
                                                      -- defect-entry — its concerns
                                                      -- use the normal rework path)
                  AND tl.Is_No_Defect = 0
                ORDER BY aa.SORTORDER, tl.Audit_Area_ID";

            using (var cmd = new SqlCommand(stageFailuresQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Vehicle_ID", vehicleId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    stageFailures.Add(new
                    {
                        Audit_Area_ID = reader.GetDecimal(reader.GetOrdinal("Audit_Area_ID")),
                        Audit_Area_Name = reader.GetString(reader.GetOrdinal("Audit_Area_Name")),
                        Auditor_Name = reader.IsDBNull(reader.GetOrdinal("Auditor_Name")) ? "" : reader.GetString(reader.GetOrdinal("Auditor_Name")),
                        Log_Date = reader.IsDBNull(reader.GetOrdinal("Log_Date")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("Log_Date")),
                        Stage_Documents = reader.IsDBNull(reader.GetOrdinal("Stage_Documents")) ? null : reader.GetString(reader.GetOrdinal("Stage_Documents")),
                        Stage_Rework_ID = reader.IsDBNull(reader.GetOrdinal("Stage_Rework_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Stage_Rework_ID")),
                        Stage_Rework_Done = reader.IsDBNull(reader.GetOrdinal("Stage_Rework_Done")) ? false : reader.GetBoolean(reader.GetOrdinal("Stage_Rework_Done")),
                        Stage_Rework_By = reader.IsDBNull(reader.GetOrdinal("Stage_Rework_By")) ? "" : reader.GetString(reader.GetOrdinal("Stage_Rework_By")),
                        Stage_Rework_Remark = reader.IsDBNull(reader.GetOrdinal("Stage_Rework_Remark")) ? "" : reader.GetString(reader.GetOrdinal("Stage_Rework_Remark")),
                        Stage_Rework_Documents = reader.IsDBNull(reader.GetOrdinal("Stage_Rework_Documents")) ? null : reader.GetString(reader.GetOrdinal("Stage_Rework_Documents")),
                        Stage_Rework_Date = reader.IsDBNull(reader.GetOrdinal("Stage_Rework_Date")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("Stage_Rework_Date")),
                    });
                }
            }

            return new { Concerns = concerns, StageFailures = stageFailures };
        }

        /// <summary>
        /// Upsert ONLY the Documents column of a stage's Track_Logs row.
        /// Creates the row (with Is_No_Defect untouched/NULL) when missing so
        /// attachments can be saved before the stage status is decided.
        /// </summary>
        public async Task<bool> UpdateStageDocuments(UpdateStageDocumentsDto dto)
        {
            var documentsJson = System.Text.Json.JsonSerializer.Serialize(dto.Documents ?? new List<string>());

            const string sql = @"
                IF EXISTS (SELECT 1 FROM MM_Vehicle_Track_Logs
                           WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = @Audit_Area_ID)
                BEGIN
                    UPDATE MM_Vehicle_Track_Logs
                    SET Documents = @Documents,
                        Is_Edited = 1,
                        Updated_Host = @Host,
                        Updated_User_ID = @UserId,
                        Updated_Date = GETDATE(),
                        Plant_Code = @Plant_Code
                    WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = @Audit_Area_ID;
                END
                ELSE
                BEGIN
                    INSERT INTO MM_Vehicle_Track_Logs
                        (Vehicle_ID, Audit_Area_ID, Auditor_Name, Log_Date, Documents,
                         Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                    VALUES (@Vehicle_ID, @Audit_Area_ID, '', GETDATE(), @Documents,
                            @Host, @UserId, GETDATE(), @Plant_Code);
                END";

            using var connection = await _connectionFactory.CreateConnectionAsync();
            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Vehicle_ID", dto.Vehicle_ID);
            cmd.Parameters.AddWithValue("@Audit_Area_ID", dto.Audit_Area_ID);
            cmd.Parameters.AddWithValue("@Documents", documentsJson);
            cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", (object?)dto.Inserted_User_ID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Plant_Code", (object?)dto.Plant_Code ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        /// <summary>
        /// Upsert one row in MM_Vehicle_Stage_Rework, keyed on
        /// (Vehicle_ID, Audit_Area_ID). RFD-locked vehicles are rejected by the
        /// controller before we get here.
        /// </summary>
        public async Task<StageReworkUpsertResultDto> UpsertStageRework(StageReworkUpsertDto dto)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var documentsJson = dto.Documents != null && dto.Documents.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(dto.Documents)
                : null;

            const string sql = @"
                IF EXISTS (SELECT 1 FROM MM_Vehicle_Stage_Rework
                           WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = @Audit_Area_ID)
                BEGIN
                    UPDATE MM_Vehicle_Stage_Rework
                    SET Rework_Done = @Rework_Done,
                        Rework_By = @Rework_By,
                        Remark = @Remark,
                        Documents = @Documents,
                        Rework_Date = CASE WHEN @Rework_Done = 1 THEN GETDATE() ELSE Rework_Date END,
                        Updated_Host = @Host,
                        Updated_User_ID = @UserId,
                        Updated_Date = GETDATE(),
                        Plant_Code = @Plant_Code
                    OUTPUT INSERTED.Stage_Rework_ID
                    WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = @Audit_Area_ID;
                END
                ELSE
                BEGIN
                    INSERT INTO MM_Vehicle_Stage_Rework
                        (Vehicle_ID, Audit_Area_ID, Rework_Done, Rework_By, Remark, Documents,
                         Rework_Date, Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                    OUTPUT INSERTED.Stage_Rework_ID
                    VALUES (@Vehicle_ID, @Audit_Area_ID, @Rework_Done, @Rework_By, @Remark, @Documents,
                            CASE WHEN @Rework_Done = 1 THEN GETDATE() ELSE NULL END,
                            @Host, @UserId, GETDATE(), @Plant_Code);
                END";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Vehicle_ID", dto.Vehicle_ID);
            cmd.Parameters.AddWithValue("@Audit_Area_ID", dto.Audit_Area_ID);
            cmd.Parameters.AddWithValue("@Rework_Done", dto.Rework_Done);
            cmd.Parameters.AddWithValue("@Rework_By", (object?)dto.Rework_By ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Remark", (object?)dto.Remark ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Documents", (object?)documentsJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", (object?)dto.Inserted_User_ID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Plant_Code", (object?)dto.Plant_Code ?? DBNull.Value);

            var scalar = await cmd.ExecuteScalarAsync();
            return new StageReworkUpsertResultDto
            {
                Stage_Rework_ID = scalar != null && scalar != DBNull.Value ? Convert.ToDecimal(scalar) : 0,
            };
        }

        /// <summary>
        /// Cross-vehicle pending-rework list for the standalone Rework team
        /// screen. One row per defect that has either no rework row or a row
        /// where Rework_Done is false. Optional filters:
        ///  • attributionId — restrict to one Attribution (NULL/0 = All).
        ///  • vinNo — substring match against VIN_No so the user can paste a
        ///    full VIN or just the last 8 chars.
        /// RFD-locked vehicles are excluded — rework is read-only for them.
        /// </summary>
        public async Task<object> GetPendingReworkList(decimal? attributionId, string? vinNo)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var concerns = new List<object>();
            const string sql = @"
                SELECT
                    d.Vehicle_ID, v.VIN_No,
                    d.Defect_Entry_ID,
                    d.Audit_Area_ID, aa.Audit_Area_Name,
                    d.Vehicle_Area_ID, va.Vehicle_Area_Name,
                    d.Part_ID, p.Part_Name,
                    d.Defect_ID, df.Defect_Name,
                    d.Location_ID, l.Location_Name,
                    d.Severity_ID, s.Severity_Code, s.Severity_Name,
                    d.Attribution_ID, a.Attribution_Name,
                    d.Defect_Type_ID, dt.Defect_Type_Name,
                    d.Remark,
                    r.Rework_ID,
                    r.Rework_Done,
                    r.Rework_By,
                    r.Rework_Date
                FROM MM_Vehicle_Defects d
                INNER JOIN MM_Vehicle_Details v ON v.Vehicle_ID = d.Vehicle_ID
                INNER JOIN MM_Audit_Area_Master aa ON aa.Audit_Area_ID = d.Audit_Area_ID
                LEFT  JOIN MM_Vehicle_Area_Master va ON va.Vehicle_Area_ID = d.Vehicle_Area_ID
                INNER JOIN MM_Part_Master p ON p.Part_ID = d.Part_ID
                INNER JOIN MM_Defect_Master df ON df.Defect_ID = d.Defect_ID
                INNER JOIN MM_Location_Master l ON l.Location_ID = d.Location_ID
                LEFT  JOIN MM_Severity_Master s ON s.Severity_ID = d.Severity_ID
                LEFT  JOIN MM_Attribution_Master a ON a.Attribution_ID = d.Attribution_ID
                LEFT  JOIN MM_Defect_Type_Master dt ON dt.Defect_Type_ID = d.Defect_Type_ID
                LEFT  JOIN MM_Vehicle_Defect_Rework r ON r.Defect_Entry_ID = d.Defect_Entry_ID
                WHERE (r.Rework_Done IS NULL OR r.Rework_Done = 0)
                  AND NOT EXISTS (
                        SELECT 1 FROM MM_Vehicle_Track_Logs tl
                        WHERE tl.Vehicle_ID = d.Vehicle_ID AND tl.Audit_Area_ID = 11
                      )
                  AND (@AttributionId IS NULL OR d.Attribution_ID = @AttributionId)
                  AND (@Vin IS NULL OR v.VIN_No LIKE '%' + @Vin + '%')
                ORDER BY v.VIN_No, aa.SORTORDER, d.Defect_Entry_ID";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@AttributionId",
                attributionId.HasValue && attributionId.Value > 0
                    ? (object)attributionId.Value
                    : DBNull.Value);
            cmd.Parameters.AddWithValue("@Vin",
                string.IsNullOrWhiteSpace(vinNo) ? (object)DBNull.Value : vinNo.Trim());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                concerns.Add(new
                {
                    Vehicle_ID = reader.GetDecimal(reader.GetOrdinal("Vehicle_ID")),
                    VIN_No = reader.IsDBNull(reader.GetOrdinal("VIN_No")) ? "" : reader.GetString(reader.GetOrdinal("VIN_No")),
                    Defect_Entry_ID = reader.GetDecimal(reader.GetOrdinal("Defect_Entry_ID")),
                    Audit_Area_ID = reader.GetDecimal(reader.GetOrdinal("Audit_Area_ID")),
                    Audit_Area_Name = reader.GetString(reader.GetOrdinal("Audit_Area_Name")),
                    Vehicle_Area_ID = reader.IsDBNull(reader.GetOrdinal("Vehicle_Area_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Vehicle_Area_ID")),
                    Vehicle_Area_Name = reader.IsDBNull(reader.GetOrdinal("Vehicle_Area_Name")) ? "" : reader.GetString(reader.GetOrdinal("Vehicle_Area_Name")),
                    Part_ID = reader.GetDecimal(reader.GetOrdinal("Part_ID")),
                    Part_Name = reader.GetString(reader.GetOrdinal("Part_Name")),
                    Defect_ID = reader.GetDecimal(reader.GetOrdinal("Defect_ID")),
                    Defect_Name = reader.GetString(reader.GetOrdinal("Defect_Name")),
                    Location_ID = reader.GetDecimal(reader.GetOrdinal("Location_ID")),
                    Location_Name = reader.GetString(reader.GetOrdinal("Location_Name")),
                    Severity_ID = reader.IsDBNull(reader.GetOrdinal("Severity_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Severity_ID")),
                    Severity_Code = reader.IsDBNull(reader.GetOrdinal("Severity_Code")) ? "" : reader.GetString(reader.GetOrdinal("Severity_Code")),
                    Attribution_ID = reader.IsDBNull(reader.GetOrdinal("Attribution_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Attribution_ID")),
                    Attribution_Name = reader.IsDBNull(reader.GetOrdinal("Attribution_Name")) ? "" : reader.GetString(reader.GetOrdinal("Attribution_Name")),
                    Defect_Type_ID = reader.IsDBNull(reader.GetOrdinal("Defect_Type_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Defect_Type_ID")),
                    Defect_Type_Name = reader.IsDBNull(reader.GetOrdinal("Defect_Type_Name")) ? "" : reader.GetString(reader.GetOrdinal("Defect_Type_Name")),
                    Remark = reader.IsDBNull(reader.GetOrdinal("Remark")) ? null : reader.GetString(reader.GetOrdinal("Remark")),
                    Rework_ID = reader.IsDBNull(reader.GetOrdinal("Rework_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Rework_ID")),
                    Rework_Done = reader.IsDBNull(reader.GetOrdinal("Rework_Done")) ? (bool?)null : reader.GetBoolean(reader.GetOrdinal("Rework_Done")),
                    Rework_By = reader.IsDBNull(reader.GetOrdinal("Rework_By")) ? "" : reader.GetString(reader.GetOrdinal("Rework_By")),
                    Rework_Date = reader.IsDBNull(reader.GetOrdinal("Rework_Date")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("Rework_Date")),
                });
            }

            return new { Concerns = concerns };
        }

        /// <summary>
        /// Upsert one row in MM_Vehicle_Defect_Rework keyed on Defect_Entry_ID.
        /// Fires from the Rework table whenever the operator toggles the
        /// "Rework Done" checkbox or changes the "Rework By" dropdown.
        /// </summary>
        public async Task<ReworkUpsertResultDto> UpsertRework(ReworkUpsertDto dto)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string upsertQuery = @"
                IF EXISTS (SELECT 1 FROM MM_Vehicle_Defect_Rework
                           WHERE Defect_Entry_ID = @Defect_Entry_ID)
                BEGIN
                    UPDATE MM_Vehicle_Defect_Rework
                    SET Rework_Done    = @Rework_Done,
                        Rework_By      = @Rework_By,
                        Rework_Date    = CASE WHEN @Rework_Done = 1 THEN GETDATE() ELSE Rework_Date END,
                        Remark         = @Remark,
                        Is_Edited      = 1,
                        Updated_Host   = @Host,
                        Updated_User_ID = @UserId,
                        Updated_Date   = GETDATE(),
                        Plant_Code     = @Plant_Code
                    OUTPUT INSERTED.Rework_ID
                    WHERE Defect_Entry_ID = @Defect_Entry_ID;
                END
                ELSE
                BEGIN
                    INSERT INTO MM_Vehicle_Defect_Rework
                        (Defect_Entry_ID, Rework_Done, Rework_By, Rework_Date, Remark,
                         Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                    OUTPUT INSERTED.Rework_ID
                    VALUES
                        (@Defect_Entry_ID, @Rework_Done, @Rework_By,
                         CASE WHEN @Rework_Done = 1 THEN GETDATE() ELSE NULL END,
                         @Remark, @Host, @UserId, GETDATE(), @Plant_Code);
                END";

            using var cmd = new SqlCommand(upsertQuery, connection);
            cmd.Parameters.AddWithValue("@Defect_Entry_ID", dto.Defect_Entry_ID);
            cmd.Parameters.AddWithValue("@Rework_Done", (object?)dto.Rework_Done ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Rework_By", (object?)dto.Rework_By ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Remark", (object?)dto.Remark ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", (object?)dto.Inserted_User_ID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Plant_Code", (object?)dto.Plant_Code ?? DBNull.Value);

            decimal reworkId = 0;
            var scalar = await cmd.ExecuteScalarAsync();
            if (scalar != null && scalar != DBNull.Value)
                reworkId = Convert.ToDecimal(scalar);

            return new ReworkUpsertResultDto { Rework_ID = reworkId };
        }

        /// <summary>
        /// Recheck payload: every defect on a vehicle (joined with master + stage
        /// names) plus the status-stage Track_Logs rows (DTC / ECOS / VMS) so the
        /// rechecker can see OK/NOK status and uploaded files in one screen.
        /// </summary>
        public async Task<object> GetRecheckData(decimal vehicleId)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            // 1) All defect concerns across stages + rework lock flag
            var concerns = new List<object>();
            const string concernsQuery = @"
                SELECT
                    d.Defect_Entry_ID,
                    d.Audit_Area_ID, aa.Audit_Area_Name,
                    d.Vehicle_Area_ID, va.Vehicle_Area_Name,
                    d.Part_ID, p.Part_Name,
                    d.Defect_ID, df.Defect_Name,
                    d.Location_ID, l.Location_Name,
                    d.Severity_ID, s.Severity_Code, s.Severity_Name,
                    d.Attribution_ID, a.Attribution_Name,
                    d.Defect_Type_ID, dt.Defect_Type_Name,
                    d.Remark,
                    ISNULL(r.Rework_Done, 0) AS Rework_Done
                FROM MM_Vehicle_Defects d
                INNER JOIN MM_Audit_Area_Master aa ON aa.Audit_Area_ID = d.Audit_Area_ID
                LEFT  JOIN MM_Vehicle_Area_Master va ON va.Vehicle_Area_ID = d.Vehicle_Area_ID
                INNER JOIN MM_Part_Master p ON p.Part_ID = d.Part_ID
                INNER JOIN MM_Defect_Master df ON df.Defect_ID = d.Defect_ID
                INNER JOIN MM_Location_Master l ON l.Location_ID = d.Location_ID
                LEFT  JOIN MM_Severity_Master s ON s.Severity_ID = d.Severity_ID
                LEFT  JOIN MM_Attribution_Master a ON a.Attribution_ID = d.Attribution_ID
                LEFT  JOIN MM_Defect_Type_Master dt ON dt.Defect_Type_ID = d.Defect_Type_ID
                LEFT  JOIN MM_Vehicle_Defect_Rework r ON r.Defect_Entry_ID = d.Defect_Entry_ID
                WHERE d.Vehicle_ID = @Vehicle_ID
                ORDER BY aa.SORTORDER, d.Defect_Entry_ID";

            using (var cmd = new SqlCommand(concernsQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Vehicle_ID", vehicleId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    concerns.Add(new
                    {
                        Defect_Entry_ID = reader.GetDecimal(reader.GetOrdinal("Defect_Entry_ID")),
                        Audit_Area_ID = reader.GetDecimal(reader.GetOrdinal("Audit_Area_ID")),
                        Audit_Area_Name = reader.GetString(reader.GetOrdinal("Audit_Area_Name")),
                        Vehicle_Area_ID = reader.IsDBNull(reader.GetOrdinal("Vehicle_Area_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Vehicle_Area_ID")),
                        Vehicle_Area_Name = reader.IsDBNull(reader.GetOrdinal("Vehicle_Area_Name")) ? "" : reader.GetString(reader.GetOrdinal("Vehicle_Area_Name")),
                        Part_ID = reader.GetDecimal(reader.GetOrdinal("Part_ID")),
                        Part_Name = reader.GetString(reader.GetOrdinal("Part_Name")),
                        Defect_ID = reader.GetDecimal(reader.GetOrdinal("Defect_ID")),
                        Defect_Name = reader.GetString(reader.GetOrdinal("Defect_Name")),
                        Location_ID = reader.GetDecimal(reader.GetOrdinal("Location_ID")),
                        Location_Name = reader.GetString(reader.GetOrdinal("Location_Name")),
                        Severity_ID = reader.IsDBNull(reader.GetOrdinal("Severity_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Severity_ID")),
                        Severity_Code = reader.IsDBNull(reader.GetOrdinal("Severity_Code")) ? "" : reader.GetString(reader.GetOrdinal("Severity_Code")),
                        Attribution_ID = reader.IsDBNull(reader.GetOrdinal("Attribution_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Attribution_ID")),
                        Attribution_Name = reader.IsDBNull(reader.GetOrdinal("Attribution_Name")) ? "" : reader.GetString(reader.GetOrdinal("Attribution_Name")),
                        Defect_Type_ID = reader.IsDBNull(reader.GetOrdinal("Defect_Type_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Defect_Type_ID")),
                        Defect_Type_Name = reader.IsDBNull(reader.GetOrdinal("Defect_Type_Name")) ? "" : reader.GetString(reader.GetOrdinal("Defect_Type_Name")),
                        Remark = reader.IsDBNull(reader.GetOrdinal("Remark")) ? null : reader.GetString(reader.GetOrdinal("Remark")),
                        Rework_Done = !reader.IsDBNull(reader.GetOrdinal("Rework_Done")) && reader.GetBoolean(reader.GetOrdinal("Rework_Done")),
                    });
                }
            }

            // 2) All audited stages — Road Test (1), Underbody (2), DTC (3),
            //    ECOS (5), Static Audit (6), VMS (7), Paint Verification (12).
            //    LEFT JOIN so a stage row appears even when not yet saved.
            var statusStages = new List<object>();
            const string statusQuery = @"
                SELECT
                    aa.Audit_Area_ID, aa.Audit_Area_Name, aa.SORTORDER,
                    tl.Track_Log_ID, tl.Auditor_Name, tl.Is_No_Defect,
                    tl.Log_Date, tl.Documents
                FROM MM_Audit_Area_Master aa
                LEFT JOIN MM_Vehicle_Track_Logs tl
                       ON tl.Audit_Area_ID = aa.Audit_Area_ID
                      AND tl.Vehicle_ID = @Vehicle_ID
                WHERE aa.Audit_Area_ID IN (1, 2, 3, 5, 6, 7, 12)
                ORDER BY aa.SORTORDER";

            using (var cmd = new SqlCommand(statusQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Vehicle_ID", vehicleId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    statusStages.Add(new
                    {
                        Audit_Area_ID = reader.GetDecimal(reader.GetOrdinal("Audit_Area_ID")),
                        Audit_Area_Name = reader.GetString(reader.GetOrdinal("Audit_Area_Name")),
                        Track_Log_ID = reader.IsDBNull(reader.GetOrdinal("Track_Log_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Track_Log_ID")),
                        Auditor_Name = reader.IsDBNull(reader.GetOrdinal("Auditor_Name")) ? "" : reader.GetString(reader.GetOrdinal("Auditor_Name")),
                        Is_No_Defect = reader.IsDBNull(reader.GetOrdinal("Is_No_Defect")) ? (bool?)null : reader.GetBoolean(reader.GetOrdinal("Is_No_Defect")),
                        Log_Date = reader.IsDBNull(reader.GetOrdinal("Log_Date")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("Log_Date")),
                        Documents = reader.IsDBNull(reader.GetOrdinal("Documents")) ? null : reader.GetString(reader.GetOrdinal("Documents")),
                    });
                }
            }

            return new { Concerns = concerns, StatusStages = statusStages };
        }

        /// <summary>
        /// Fetch the saved state for a specific (Vehicle, Stage):
        ///  • The MM_Vehicle_Track_Logs row (auditor, no-defect flag, log date)
        ///  • All MM_Vehicle_Defects rows joined with master names for direct UI render
        /// </summary>
        public async Task<object> GetStageData(decimal vehicleId, decimal auditAreaId)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            // 1) Track log
            object? trackLog = null;
            const string logQuery = @"
                SELECT Track_Log_ID, Auditor_Name, Is_No_Defect, Log_Date, Documents,
                       Start_KM, End_KM
                FROM MM_Vehicle_Track_Logs
                WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = @Audit_Area_ID";

            using (var cmd = new SqlCommand(logQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Vehicle_ID", vehicleId);
                cmd.Parameters.AddWithValue("@Audit_Area_ID", auditAreaId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    trackLog = new
                    {
                        Track_Log_ID = reader.GetDecimal(reader.GetOrdinal("Track_Log_ID")),
                        Auditor_Name = reader.IsDBNull(reader.GetOrdinal("Auditor_Name")) ? "" : reader.GetString(reader.GetOrdinal("Auditor_Name")),
                        Is_No_Defect = !reader.IsDBNull(reader.GetOrdinal("Is_No_Defect")) && reader.GetBoolean(reader.GetOrdinal("Is_No_Defect")),
                        Log_Date = reader.IsDBNull(reader.GetOrdinal("Log_Date")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("Log_Date")),
                        Documents = reader.IsDBNull(reader.GetOrdinal("Documents")) ? null : reader.GetString(reader.GetOrdinal("Documents")),
                        Start_KM = reader.IsDBNull(reader.GetOrdinal("Start_KM")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Start_KM")),
                        End_KM = reader.IsDBNull(reader.GetOrdinal("End_KM")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("End_KM")),
                    };
                }
            }

            // 2) Concerns joined with master names + rework lock flag
            var concerns = new List<object>();
            const string concernsQuery = @"
                SELECT
                    d.Defect_Entry_ID,
                    d.Vehicle_Area_ID, va.Vehicle_Area_Name,
                    d.Part_ID, p.Part_Name,
                    d.Defect_ID, df.Defect_Name,
                    d.Location_ID, l.Location_Name,
                    d.Severity_ID, s.Severity_Code, s.Severity_Name,
                    d.Attribution_ID, a.Attribution_Name,
                    d.Defect_Type_ID, dt.Defect_Type_Name,
                    d.Remark,
                    d.Area_Image_ID, d.Image_X, d.Image_Y,
                    ISNULL(r.Rework_Done, 0) AS Rework_Done
                FROM MM_Vehicle_Defects d
                LEFT JOIN MM_Vehicle_Area_Master va ON va.Vehicle_Area_ID = d.Vehicle_Area_ID
                INNER JOIN MM_Part_Master p ON p.Part_ID = d.Part_ID
                INNER JOIN MM_Defect_Master df ON df.Defect_ID = d.Defect_ID
                INNER JOIN MM_Location_Master l ON l.Location_ID = d.Location_ID
                LEFT JOIN MM_Severity_Master s ON s.Severity_ID = d.Severity_ID
                LEFT JOIN MM_Attribution_Master a ON a.Attribution_ID = d.Attribution_ID
                LEFT JOIN MM_Defect_Type_Master dt ON dt.Defect_Type_ID = d.Defect_Type_ID
                LEFT JOIN MM_Vehicle_Defect_Rework r ON r.Defect_Entry_ID = d.Defect_Entry_ID
                WHERE d.Vehicle_ID = @Vehicle_ID AND d.Audit_Area_ID = @Audit_Area_ID
                ORDER BY d.Defect_Entry_ID";

            using (var cmd = new SqlCommand(concernsQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Vehicle_ID", vehicleId);
                cmd.Parameters.AddWithValue("@Audit_Area_ID", auditAreaId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    concerns.Add(new
                    {
                        Defect_Entry_ID = reader.GetDecimal(reader.GetOrdinal("Defect_Entry_ID")),
                        Vehicle_Area_ID = reader.IsDBNull(reader.GetOrdinal("Vehicle_Area_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Vehicle_Area_ID")),
                        Vehicle_Area_Name = reader.IsDBNull(reader.GetOrdinal("Vehicle_Area_Name")) ? "" : reader.GetString(reader.GetOrdinal("Vehicle_Area_Name")),
                        Part_ID = reader.GetDecimal(reader.GetOrdinal("Part_ID")),
                        Part_Name = reader.GetString(reader.GetOrdinal("Part_Name")),
                        Defect_ID = reader.GetDecimal(reader.GetOrdinal("Defect_ID")),
                        Defect_Name = reader.GetString(reader.GetOrdinal("Defect_Name")),
                        Location_ID = reader.GetDecimal(reader.GetOrdinal("Location_ID")),
                        Location_Name = reader.GetString(reader.GetOrdinal("Location_Name")),
                        Severity_ID = reader.IsDBNull(reader.GetOrdinal("Severity_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Severity_ID")),
                        Severity_Code = reader.IsDBNull(reader.GetOrdinal("Severity_Code")) ? "" : reader.GetString(reader.GetOrdinal("Severity_Code")),
                        Attribution_ID = reader.IsDBNull(reader.GetOrdinal("Attribution_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Attribution_ID")),
                        Attribution_Name = reader.IsDBNull(reader.GetOrdinal("Attribution_Name")) ? "" : reader.GetString(reader.GetOrdinal("Attribution_Name")),
                        Defect_Type_ID = reader.IsDBNull(reader.GetOrdinal("Defect_Type_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Defect_Type_ID")),
                        Defect_Type_Name = reader.IsDBNull(reader.GetOrdinal("Defect_Type_Name")) ? "" : reader.GetString(reader.GetOrdinal("Defect_Type_Name")),
                        Remark = reader.IsDBNull(reader.GetOrdinal("Remark")) ? null : reader.GetString(reader.GetOrdinal("Remark")),
                        Area_Image_ID = reader.IsDBNull(reader.GetOrdinal("Area_Image_ID")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Area_Image_ID")),
                        Image_X = reader.IsDBNull(reader.GetOrdinal("Image_X")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Image_X")),
                        Image_Y = reader.IsDBNull(reader.GetOrdinal("Image_Y")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Image_Y")),
                        Rework_Done = !reader.IsDBNull(reader.GetOrdinal("Rework_Done")) && reader.GetBoolean(reader.GetOrdinal("Rework_Done")),
                    });
                }
            }

            return new { TrackLog = trackLog, Concerns = concerns };
        }

        /// <summary>
        /// Returns true when the vehicle has a Physical RFD row in Track_Logs
        /// (Audit_Area_ID = 11). Once true, every write touching that vehicle
        /// must be rejected — the dispatch lock is total.
        /// </summary>
        public async Task<bool> IsVehicleRfdDone(decimal vehicleId)
        {
            if (vehicleId <= 0) return false;

            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string query = @"
                SELECT TOP 1 1
                FROM MM_Vehicle_Track_Logs
                WHERE Vehicle_ID = @Vid
                  AND Audit_Area_ID = 11
                  AND (Is_Deleted IS NULL OR Is_Deleted = 0)";
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Vid", vehicleId);
            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value;
        }

        /// <summary>
        /// Same lock probe, but reached via a Defect_Entry_ID — used by
        /// UpdateConcern / DeleteConcern / UpsertRework where the request
        /// only carries the defect id, not the vehicle id.
        /// </summary>
        public async Task<bool> IsVehicleRfdDoneByDefectEntry(decimal defectEntryId)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string query = @"
                SELECT TOP 1 1
                FROM MM_Vehicle_Defects d
                INNER JOIN MM_Vehicle_Track_Logs tl
                       ON tl.Vehicle_ID = d.Vehicle_ID
                      AND tl.Audit_Area_ID = 11
                      AND (tl.Is_Deleted IS NULL OR tl.Is_Deleted = 0)
                WHERE d.Defect_Entry_ID = @Id";
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", defectEntryId);
            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value;
        }

        /// <summary>
        /// Returns true when this concern's rework has been marked done — the
        /// concern is then locked: edits and deletes must be rejected by callers.
        /// </summary>
        public async Task<bool> IsConcernLockedByRework(decimal defectEntryId)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string query = @"
                SELECT TOP 1 ISNULL(Rework_Done, 0)
                FROM MM_Vehicle_Defect_Rework
                WHERE Defect_Entry_ID = @Id";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", defectEntryId);
            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value && Convert.ToBoolean(result);
        }

        /// <summary>
        /// Hard-delete a single concern row by its Defect_Entry_ID.
        /// If it was the last concern for that (Vehicle, Stage), the matching
        /// MM_Vehicle_Track_Logs row is flipped to Is_No_Defect = 1.
        /// </summary>
        public async Task<bool> DeleteConcern(decimal defectEntryId)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // 1) Capture the row's (Vehicle_ID, Audit_Area_ID) before deleting.
                decimal vehicleId = 0;
                decimal auditAreaId = 0;

                using (var lookupCmd = new SqlCommand(
                    "SELECT Vehicle_ID, Audit_Area_ID FROM MM_Vehicle_Defects WHERE Defect_Entry_ID = @Id",
                    connection, transaction))
                {
                    lookupCmd.Parameters.AddWithValue("@Id", defectEntryId);
                    using var reader = await lookupCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        vehicleId = reader.GetDecimal(0);
                        auditAreaId = reader.GetDecimal(1);
                    }
                }

                if (vehicleId == 0)
                {
                    transaction.Rollback();
                    return false;
                }

                // 2) Delete the concern.
                int rows;
                using (var delCmd = new SqlCommand(
                    "DELETE FROM MM_Vehicle_Defects WHERE Defect_Entry_ID = @Id",
                    connection, transaction))
                {
                    delCmd.Parameters.AddWithValue("@Id", defectEntryId);
                    rows = await delCmd.ExecuteNonQueryAsync();
                }

                // 3) If no concerns remain for this (Vehicle, Stage), flip Track_Logs.Is_No_Defect = 1.
                int remaining;
                using (var countCmd = new SqlCommand(
                    "SELECT COUNT(1) FROM MM_Vehicle_Defects WHERE Vehicle_ID = @Vid AND Audit_Area_ID = @Aid",
                    connection, transaction))
                {
                    countCmd.Parameters.AddWithValue("@Vid", vehicleId);
                    countCmd.Parameters.AddWithValue("@Aid", auditAreaId);
                    remaining = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                }

                if (remaining == 0)
                {
                    const string flipQuery = @"
                        UPDATE MM_Vehicle_Track_Logs
                        SET Is_No_Defect = 1,
                            Is_Edited = 1,
                            Updated_Date = GETDATE()
                        WHERE Vehicle_ID = @Vid AND Audit_Area_ID = @Aid";

                    using var flipCmd = new SqlCommand(flipQuery, connection, transaction);
                    flipCmd.Parameters.AddWithValue("@Vid", vehicleId);
                    flipCmd.Parameters.AddWithValue("@Aid", auditAreaId);
                    await flipCmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                return rows > 0;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Save an OK/NOK-style stage (DTC / ECOS / VMS): upsert Track_Logs
        /// with auditor, Is_No_Defect, and the Documents JSON array.
        /// No concerns table rows are written for these stages.
        /// </summary>
        public async Task<bool> SaveStageStatus(StageStatusSaveDto dto)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            // Serialize the file-name list as JSON for the Documents column.
            string? documentsJson = (dto.Documents != null && dto.Documents.Count > 0)
                ? JsonSerializer.Serialize(dto.Documents)
                : null;

            const string upsertLogQuery = @"
                IF EXISTS (SELECT 1 FROM MM_Vehicle_Track_Logs
                           WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = @Audit_Area_ID)
                BEGIN
                    UPDATE MM_Vehicle_Track_Logs
                    SET Auditor_Name = @Auditor_Name,
                        Is_No_Defect = @Is_No_Defect,
                        Log_Date     = GETDATE(),
                        Documents    = @Documents,
                        Is_Edited    = 1,
                        Updated_Host = @Host,
                        Updated_User_ID = @UserId,
                        Updated_Date = GETDATE(),
                        Plant_Code   = @Plant_Code
                    WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = @Audit_Area_ID;
                END
                ELSE
                BEGIN
                    INSERT INTO MM_Vehicle_Track_Logs
                        (Vehicle_ID, Audit_Area_ID, Auditor_Name, Is_No_Defect, Log_Date,
                         Documents, Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                    VALUES
                        (@Vehicle_ID, @Audit_Area_ID, @Auditor_Name, @Is_No_Defect, GETDATE(),
                         @Documents, @Host, @UserId, GETDATE(), @Plant_Code);
                END";

            using var cmd = new SqlCommand(upsertLogQuery, connection);
            cmd.Parameters.AddWithValue("@Vehicle_ID", dto.Vehicle_ID);
            cmd.Parameters.AddWithValue("@Audit_Area_ID", dto.Audit_Area_ID);
            cmd.Parameters.AddWithValue("@Auditor_Name", (object?)dto.Auditor_Name ?? string.Empty);
            cmd.Parameters.AddWithValue("@Is_No_Defect", dto.Is_No_Defect);
            cmd.Parameters.AddWithValue("@Documents", (object?)documentsJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", (object?)dto.Inserted_User_ID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Plant_Code", (object?)dto.Plant_Code ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        /// <summary>
        /// Upsert the Road Test Track_Logs row's Start_KM / End_KM values.
        /// Auto-creates the row (with empty Auditor_Name) if the operator types
        /// KM before doing anything else; auditor + concerns slot in later.
        /// </summary>
        public async Task<bool> UpdateRoadTestKm(RoadTestKmDto dto)
        {
            const decimal RoadTestAreaId = 1m;

            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string upsertQuery = @"
                IF EXISTS (SELECT 1 FROM MM_Vehicle_Track_Logs
                           WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = @Audit_Area_ID)
                BEGIN
                    UPDATE MM_Vehicle_Track_Logs
                    SET Start_KM       = @Start_KM,
                        End_KM         = @End_KM,
                        Is_Edited      = 1,
                        Updated_Host   = @Host,
                        Updated_User_ID = @UserId,
                        Updated_Date   = GETDATE()
                    WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = @Audit_Area_ID;
                END
                ELSE
                BEGIN
                    INSERT INTO MM_Vehicle_Track_Logs
                        (Vehicle_ID, Audit_Area_ID, Auditor_Name, Is_No_Defect, Log_Date,
                         Start_KM, End_KM,
                         Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                    VALUES
                        (@Vehicle_ID, @Audit_Area_ID, N'', 0, GETDATE(),
                         @Start_KM, @End_KM,
                         @Host, @UserId, GETDATE(), @Plant_Code);
                END";

            using var cmd = new SqlCommand(upsertQuery, connection);
            cmd.Parameters.AddWithValue("@Vehicle_ID", dto.Vehicle_ID);
            cmd.Parameters.AddWithValue("@Audit_Area_ID", RoadTestAreaId);
            cmd.Parameters.AddWithValue("@Start_KM", (object?)dto.Start_KM ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@End_KM", (object?)dto.End_KM ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", (object?)dto.Inserted_User_ID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Plant_Code", (object?)dto.Plant_Code ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        /// <summary>
        /// Update only the Auditor_Name on an existing Track_Logs row.
        /// If no row exists for (Vehicle, Stage), returns false silently — the
        /// auditor will be persisted when the first concern is added.
        /// </summary>
        public async Task<bool> UpdateAuditor(decimal vehicleId, decimal auditAreaId, string auditorName,
                                              string? host, decimal? userId)
        {
            if (string.IsNullOrWhiteSpace(auditorName)) return false;

            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string query = @"
                UPDATE MM_Vehicle_Track_Logs
                SET Auditor_Name = @Name,
                    Is_Edited = 1,
                    Updated_Host = @Host,
                    Updated_User_ID = @UserId,
                    Updated_Date = GETDATE()
                WHERE Vehicle_ID = @Vid AND Audit_Area_ID = @Aid";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Vid", vehicleId);
            cmd.Parameters.AddWithValue("@Aid", auditAreaId);
            cmd.Parameters.AddWithValue("@Name", auditorName);
            cmd.Parameters.AddWithValue("@Host", (object?)host ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        /// <summary>
        /// Overwrite the fields of an existing MM_Vehicle_Defects row.
        /// Track_Logs gets its audit columns refreshed; Is_No_Defect is forced to 0.
        /// </summary>
        public async Task<bool> UpdateConcern(decimal defectEntryId, StageSaveDto dto)
        {
            if (dto.Concerns == null || dto.Concerns.Count != 1)
                throw new ArgumentException("UpdateConcern expects exactly one concern in the payload.");

            var c = dto.Concerns[0];

            using var connection = await _connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Image coords: only overwrite when the caller sends a click
                // point — an edit without a re-click keeps the original point.
                const string updateQuery = @"
                    UPDATE MM_Vehicle_Defects
                    SET Vehicle_Area_ID = @Vehicle_Area_ID,
                        Part_ID         = @Part_ID,
                        Defect_ID       = @Defect_ID,
                        Location_ID     = @Location_ID,
                        Severity_ID     = @Severity_ID,
                        Attribution_ID  = @Attribution_ID,
                        Defect_Type_ID  = @Defect_Type_ID,
                        Remark          = @Remark,
                        Area_Image_ID   = COALESCE(@Area_Image_ID, Area_Image_ID),
                        Image_X         = COALESCE(@Image_X, Image_X),
                        Image_Y         = COALESCE(@Image_Y, Image_Y),
                        Is_Edited       = 1,
                        Updated_Host    = @Host,
                        Updated_User_ID = @UserId,
                        Updated_Date    = GETDATE()
                    WHERE Defect_Entry_ID = @Id";

                int rows;
                using (var cmd = new SqlCommand(updateQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", defectEntryId);
                    cmd.Parameters.AddWithValue("@Vehicle_Area_ID", (object?)c.Vehicle_Area_ID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Part_ID", c.Part_ID);
                    cmd.Parameters.AddWithValue("@Defect_ID", c.Defect_ID);
                    cmd.Parameters.AddWithValue("@Location_ID", c.Location_ID);
                    cmd.Parameters.AddWithValue("@Severity_ID", (object?)c.Severity_ID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Attribution_ID", (object?)c.Attribution_ID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Defect_Type_ID", (object?)c.Defect_Type_ID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Remark", (object?)c.Remark ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Area_Image_ID", (object?)c.Area_Image_ID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Image_X", (object?)c.Image_X ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Image_Y", (object?)c.Image_Y ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@UserId", (object?)dto.Inserted_User_ID ?? DBNull.Value);
                    rows = await cmd.ExecuteNonQueryAsync();
                }

                // Refresh Track_Logs audit columns + ensure Is_No_Defect = 0
                const string logUpdate = @"
                    UPDATE MM_Vehicle_Track_Logs
                    SET Is_No_Defect    = 0,
                        Is_Edited       = 1,
                        Updated_Host    = @Host,
                        Updated_User_ID = @UserId,
                        Updated_Date    = GETDATE()
                    WHERE Vehicle_ID = @Vid AND Audit_Area_ID = @Aid";

                using (var logCmd = new SqlCommand(logUpdate, connection, transaction))
                {
                    logCmd.Parameters.AddWithValue("@Vid", dto.Vehicle_ID);
                    logCmd.Parameters.AddWithValue("@Aid", dto.Audit_Area_ID);
                    logCmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
                    logCmd.Parameters.AddWithValue("@UserId", (object?)dto.Inserted_User_ID ?? DBNull.Value);
                    await logCmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                return rows > 0;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Fetch the saved stage state for a vehicle — used to render progress bar
        /// and to load existing concerns on re-entry / edit.
        /// </summary>
        public async Task<List<object>> GetStageLogsByVehicle(decimal vehicleId)
        {
            var result = new List<object>();
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string query = @"
                SELECT
                    tl.Track_Log_ID, tl.Vehicle_ID, tl.Audit_Area_ID,
                    aa.Audit_Area_Name, tl.Auditor_Name, tl.Is_No_Defect,
                    tl.Log_Date, tl.Documents
                FROM MM_Vehicle_Track_Logs tl
                INNER JOIN MM_Audit_Area_Master aa ON aa.Audit_Area_ID = tl.Audit_Area_ID
                WHERE tl.Vehicle_ID = @Vehicle_ID
                  AND (tl.Is_Deleted IS NULL OR tl.Is_Deleted = 0)
                ORDER BY aa.SORTORDER";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Vehicle_ID", vehicleId);
            using var reader = await cmd.ExecuteReaderAsync();

            bool hasReworkRow = false;
            while (await reader.ReadAsync())
            {
                var areaId = reader.GetDecimal(reader.GetOrdinal("Audit_Area_ID"));
                if (areaId == 9m) hasReworkRow = true;

                result.Add(new
                {
                    Track_Log_ID = reader.GetDecimal(reader.GetOrdinal("Track_Log_ID")),
                    Vehicle_ID = reader.GetDecimal(reader.GetOrdinal("Vehicle_ID")),
                    Audit_Area_ID = areaId,
                    Audit_Area_Name = reader.GetString(reader.GetOrdinal("Audit_Area_Name")),
                    Auditor_Name = reader.GetString(reader.GetOrdinal("Auditor_Name")),
                    Is_No_Defect = !reader.IsDBNull(reader.GetOrdinal("Is_No_Defect")) && reader.GetBoolean(reader.GetOrdinal("Is_No_Defect")),
                    Log_Date = reader.IsDBNull(reader.GetOrdinal("Log_Date")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("Log_Date")),
                    Documents = reader.IsDBNull(reader.GetOrdinal("Documents")) ? null : reader.GetString(reader.GetOrdinal("Documents")),
                });
            }
            reader.Close();

            // Synthetic Rework completion entry: Rework intentionally writes no
            // Track_Logs row (multiple teams may work on a vehicle's concerns).
            // If every defect on the vehicle has Rework_Done = 1, surface a
            // virtual entry so the progress bar marks Rework as completed.
            if (!hasReworkRow)
            {
                const string reworkCheckQuery = @"
                    SELECT
                        COUNT(d.Defect_Entry_ID) AS Total,
                        ISNULL(SUM(CASE WHEN r.Rework_Done = 1 THEN 1 ELSE 0 END), 0) AS Done,
                        MAX(r.Rework_Date) AS LastDate
                    FROM MM_Vehicle_Defects d
                    LEFT JOIN MM_Vehicle_Defect_Rework r ON r.Defect_Entry_ID = d.Defect_Entry_ID
                    WHERE d.Vehicle_ID = @Vehicle_ID";

                using var reworkCmd = new SqlCommand(reworkCheckQuery, connection);
                reworkCmd.Parameters.AddWithValue("@Vehicle_ID", vehicleId);
                using var reworkReader = await reworkCmd.ExecuteReaderAsync();
                if (await reworkReader.ReadAsync())
                {
                    int total = Convert.ToInt32(reworkReader.GetValue(0));
                    int done = Convert.ToInt32(reworkReader.GetValue(1));
                    DateTime? lastDate = reworkReader.IsDBNull(2) ? (DateTime?)null : reworkReader.GetDateTime(2);

                    if (total > 0 && total == done)
                    {
                        result.Add(new
                        {
                            Track_Log_ID = 0m,
                            Vehicle_ID = vehicleId,
                            Audit_Area_ID = 9m,
                            Audit_Area_Name = "Rework",
                            Auditor_Name = "",
                            Is_No_Defect = true,
                            Log_Date = lastDate,
                            Documents = (string?)null,
                        });
                    }
                }
            }

            return result;
        }
    }
}
