using API_LSP.Models.DTOs;
using Microsoft.Data.SqlClient;

namespace API_LSP.Data
{
    /// <summary>
    /// Imports shower-test data for a vehicle from the external PQ_Dock_Audit DB
    /// (via SP_Get_Shower_Defects_For_LSP) into our LSP tables:
    ///  - Upserts MM_Vehicle_Track_Logs (Audit_Area_ID = 4)
    ///  - Replaces MM_Vehicle_Defects rows for stage 4
    /// Part_Name is auto-inserted into MM_Part_Master when missing. All other
    /// master lookups (Defect, Vehicle_Area, Location, Severity, Attribution)
    /// are read-only — rows that reference a missing master are reported back
    /// in the Skipped list and not inserted.
    /// </summary>
    public class MM_ShowerTestDataService
    {
        private const decimal SHOWER_AUDIT_AREA_ID = 4;

        private readonly IDbConnectionFactory _connectionFactory;

        public MM_ShowerTestDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<ShowerImportResultDto> ImportShowerForVehicle(ShowerImportRequestDto req)
        {
            var result = new ShowerImportResultDto();

            // 1) Pull rows from the external Dock_Audit DB via the SP.
            var rows = await FetchShowerRows(req.VIN_No);
            if (rows.Count == 0)
            {
                result.Found = false;
                return result;
            }

            result.Found = true;

            // Header data comes from the first row — all rows share the same
            // VIN-level header (auditor, audit date, no-defect flag).
            var first = rows[0];
            result.Auditor_Name = first.Auditor_Name ?? string.Empty;
            result.Audit_Date = first.Audit_Date;
            result.Is_No_Defect = first.Is_No_Defect;

            // 2) Write to LSP DB inside a single transaction.
            using var conn = await _connectionFactory.CreateConnectionAsync();
            using var tx = conn.BeginTransaction();

            try
            {
                await UpsertTrackLog(conn, tx, req, result);
                await DeleteExistingShowerDefects(conn, tx, req.Vehicle_ID);

                if (!result.Is_No_Defect)
                {
                    foreach (var row in rows)
                    {
                        // Defect-bearing rows must carry the four required FKs.
                        // Skip the entire row if any required value is empty.
                        if (string.IsNullOrWhiteSpace(row.Part_Name)
                            || string.IsNullOrWhiteSpace(row.Defect_Name)
                            || string.IsNullOrWhiteSpace(row.Location_Name)
                            || string.IsNullOrWhiteSpace(row.Area_Name))
                        {
                            // Source data malformed — record and skip.
                            result.Skipped.Add(new ShowerSkippedRowDto
                            {
                                Part_Name = row.Part_Name,
                                MissingMaster = "Source Row",
                                MissingValue = "Required field is empty (Part / Defect / Location / Area)",
                            });
                            continue;
                        }

                        // Part: lookup → auto-insert if missing.
                        var partId = await GetOrCreatePart(conn, tx, row.Part_Name.Trim(), req);

                        // Other masters: lookup only. If not found, skip the row.
                        var defectId = await LookupId(conn, tx,
                            "SELECT Defect_ID FROM MM_Defect_Master WHERE LOWER(LTRIM(RTRIM(Defect_Name))) = @v",
                            row.Defect_Name);
                        if (defectId == null) { Skip(result, row, "Defect Master", row.Defect_Name); continue; }

                        var areaId = await LookupId(conn, tx,
                            "SELECT Vehicle_Area_ID FROM MM_Vehicle_Area_Master WHERE LOWER(LTRIM(RTRIM(Vehicle_Area_Name))) = @v",
                            row.Area_Name);
                        if (areaId == null) { Skip(result, row, "Vehicle Area Master", row.Area_Name); continue; }

                        var locationId = await LookupId(conn, tx,
                            "SELECT Location_ID FROM MM_Location_Master WHERE LOWER(LTRIM(RTRIM(Location_Name))) = @v",
                            row.Location_Name);
                        if (locationId == null) { Skip(result, row, "Location Master", row.Location_Name); continue; }

                        decimal? severityId = null;
                        if (!string.IsNullOrWhiteSpace(row.Severity_Name))
                        {
                            severityId = await LookupId(conn, tx,
                                "SELECT Severity_ID FROM MM_Severity_Master WHERE LOWER(LTRIM(RTRIM(Severity_Name))) = @v",
                                row.Severity_Name);
                            if (severityId == null) { Skip(result, row, "Severity Master", row.Severity_Name!); continue; }
                        }

                        decimal? attributionId = null;
                        if (!string.IsNullOrWhiteSpace(row.Attribution_Name))
                        {
                            attributionId = await LookupId(conn, tx,
                                "SELECT Attribution_ID FROM MM_Attribution_Master WHERE LOWER(LTRIM(RTRIM(Attribution_Name))) = @v",
                                row.Attribution_Name);
                            if (attributionId == null) { Skip(result, row, "Attribution Master", row.Attribution_Name!); continue; }
                        }

                        await InsertDefect(conn, tx, req, partId, defectId.Value, areaId.Value,
                            locationId.Value, severityId, attributionId, row.Remark);
                        result.Imported++;
                    }
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return result;
        }

        // ─── External DB read ────────────────────────────────────────────────

        private async Task<List<ShowerSpRowDto>> FetchShowerRows(string vinNo)
        {
            var rows = new List<ShowerSpRowDto>();

            using var conn = await _connectionFactory.CreateShowerConnectionAsync();
            using var cmd = new SqlCommand("[dbo].[SP_Get_Shower_Defects_For_LSP]", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure,
            };
            cmd.Parameters.AddWithValue("@Vin_No", vinNo ?? string.Empty);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new ShowerSpRowDto
                {
                    Vin_No = GetStr(reader, "Vin_No"),
                    Audit_Date = GetDate(reader, "Audit_Date"),
                    Auditor_Name = GetStr(reader, "Auditor_Name"),
                    Shower_Booth = GetStrOrNull(reader, "Shower_Booth"),
                    Cycle_Name = GetStrOrNull(reader, "Cycle_Name"),
                    Is_No_Defect = GetBool(reader, "Is_No_Defect"),
                    Area_Name = GetStrOrNull(reader, "Area_Name"),
                    Part_Name = GetStrOrNull(reader, "Part_Name"),
                    Defect_Name = GetStrOrNull(reader, "Defect_Name"),
                    Location_Name = GetStrOrNull(reader, "Location_Name"),
                    Severity_Name = GetStrOrNull(reader, "Severity_Name"),
                    Attribution_Name = GetStrOrNull(reader, "Attribution_Name"),
                    Zone_Name = GetStrOrNull(reader, "Zone_Name"),
                    Detection_Time = GetStrOrNull(reader, "Detection_Time"),
                    Remark = GetStrOrNull(reader, "Remark"),
                });
            }

            return rows;
        }

        // ─── LSP DB writes ───────────────────────────────────────────────────

        private static async Task UpsertTrackLog(SqlConnection conn, SqlTransaction tx,
            ShowerImportRequestDto req, ShowerImportResultDto result)
        {
            const string sql = @"
                IF EXISTS (SELECT 1 FROM MM_Vehicle_Track_Logs
                           WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = @Audit_Area_ID)
                BEGIN
                    UPDATE MM_Vehicle_Track_Logs
                    SET Auditor_Name = @Auditor_Name,
                        Is_No_Defect = @Is_No_Defect,
                        Log_Date = @Log_Date,
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
                        (Vehicle_ID, Audit_Area_ID, Auditor_Name, Is_No_Defect, Log_Date,
                         Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                    VALUES (@Vehicle_ID, @Audit_Area_ID, @Auditor_Name, @Is_No_Defect, @Log_Date,
                            @Host, @UserId, GETDATE(), @Plant_Code);
                END";

            using var cmd = new SqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@Vehicle_ID", req.Vehicle_ID);
            cmd.Parameters.AddWithValue("@Audit_Area_ID", SHOWER_AUDIT_AREA_ID);
            cmd.Parameters.AddWithValue("@Auditor_Name", (object?)result.Auditor_Name ?? string.Empty);
            cmd.Parameters.AddWithValue("@Is_No_Defect", result.Is_No_Defect);
            cmd.Parameters.AddWithValue("@Log_Date", (object?)result.Audit_Date ?? DateTime.Now);
            cmd.Parameters.AddWithValue("@Host", (object?)req.Inserted_Host ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", (object?)req.Inserted_User_ID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Plant_Code", (object?)req.Plant_Code ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task DeleteExistingShowerDefects(SqlConnection conn, SqlTransaction tx, decimal vehicleId)
        {
            const string sql = @"
                DELETE FROM MM_Vehicle_Defects
                WHERE Vehicle_ID = @Vehicle_ID AND Audit_Area_ID = @Audit_Area_ID";

            using var cmd = new SqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@Vehicle_ID", vehicleId);
            cmd.Parameters.AddWithValue("@Audit_Area_ID", SHOWER_AUDIT_AREA_ID);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task InsertDefect(SqlConnection conn, SqlTransaction tx,
            ShowerImportRequestDto req, decimal partId, decimal defectId, decimal areaId,
            decimal locationId, decimal? severityId, decimal? attributionId, string? remark)
        {
            const string sql = @"
                INSERT INTO MM_Vehicle_Defects
                    (Vehicle_ID, Audit_Area_ID, Vehicle_Area_ID, Part_ID, Defect_ID,
                     Location_ID, Severity_ID, Attribution_ID, Defect_Type_ID, Remark,
                     Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                VALUES
                    (@Vehicle_ID, @Audit_Area_ID, @Vehicle_Area_ID, @Part_ID, @Defect_ID,
                     @Location_ID, @Severity_ID, @Attribution_ID, NULL, @Remark,
                     @Host, @UserId, GETDATE(), @Plant_Code)";

            using var cmd = new SqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@Vehicle_ID", req.Vehicle_ID);
            cmd.Parameters.AddWithValue("@Audit_Area_ID", SHOWER_AUDIT_AREA_ID);
            cmd.Parameters.AddWithValue("@Vehicle_Area_ID", areaId);
            cmd.Parameters.AddWithValue("@Part_ID", partId);
            cmd.Parameters.AddWithValue("@Defect_ID", defectId);
            cmd.Parameters.AddWithValue("@Location_ID", locationId);
            cmd.Parameters.AddWithValue("@Severity_ID", (object?)severityId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Attribution_ID", (object?)attributionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Remark", (object?)remark ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Host", (object?)req.Inserted_Host ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", (object?)req.Inserted_User_ID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Plant_Code", (object?)req.Plant_Code ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        // ─── Master helpers ──────────────────────────────────────────────────

        private static async Task<decimal?> LookupId(SqlConnection conn, SqlTransaction tx,
            string sql, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            using var cmd = new SqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@v", value.Trim().ToLower());
            var scalar = await cmd.ExecuteScalarAsync();
            if (scalar == null || scalar == DBNull.Value) return null;
            return Convert.ToDecimal(scalar);
        }

        /// <summary>
        /// Lookup Part_Name in MM_Part_Master. If absent, INSERT a new row and
        /// return its ID. Plant_Code / audit fields are taken from the request.
        /// </summary>
        private static async Task<decimal> GetOrCreatePart(SqlConnection conn, SqlTransaction tx,
            string partName, ShowerImportRequestDto req)
        {
            const string lookup = @"
                SELECT TOP 1 Part_ID FROM MM_Part_Master
                WHERE LOWER(LTRIM(RTRIM(Part_Name))) = @Part_Name";

            using (var cmd = new SqlCommand(lookup, conn, tx))
            {
                cmd.Parameters.AddWithValue("@Part_Name", partName.ToLower());
                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar != null && scalar != DBNull.Value)
                    return Convert.ToDecimal(scalar);
            }

            const string insert = @"
                INSERT INTO MM_Part_Master
                    (Part_Name, SORTORDER, Plant_Code, Inserted_Host, Inserted_User_ID, Inserted_Date)
                OUTPUT INSERTED.Part_ID
                VALUES (@Part_Name, @SORTORDER, @Plant_Code, @Host, @UserId, GETDATE())";

            using (var cmd = new SqlCommand(insert, conn, tx))
            {
                cmd.Parameters.AddWithValue("@Part_Name", partName);
                cmd.Parameters.AddWithValue("@SORTORDER", 0);
                cmd.Parameters.AddWithValue("@Plant_Code", (object?)req.Plant_Code ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Host", (object?)req.Inserted_Host ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UserId", (object?)req.Inserted_User_ID ?? DBNull.Value);
                var scalar = await cmd.ExecuteScalarAsync();
                return Convert.ToDecimal(scalar);
            }
        }

        private static void Skip(ShowerImportResultDto result, ShowerSpRowDto row, string master, string value)
        {
            result.Skipped.Add(new ShowerSkippedRowDto
            {
                Part_Name = row.Part_Name,
                MissingMaster = master,
                MissingValue = value,
            });
        }

        // ─── SqlDataReader helpers ───────────────────────────────────────────

        private static string GetStr(SqlDataReader r, string col)
        {
            var i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? string.Empty : r.GetString(i);
        }

        private static string? GetStrOrNull(SqlDataReader r, string col)
        {
            var i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? null : r.GetString(i);
        }

        private static DateTime? GetDate(SqlDataReader r, string col)
        {
            var i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? (DateTime?)null : r.GetDateTime(i);
        }

        private static bool GetBool(SqlDataReader r, string col)
        {
            var i = r.GetOrdinal(col);
            return !r.IsDBNull(i) && r.GetBoolean(i);
        }
    }
}
