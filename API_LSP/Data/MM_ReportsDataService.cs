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

            // OK when clean (Is_No_Defect = 1), NOK when a defect was recorded, Pending when no row.
            static string StageStatus(int? flag) =>
                flag == null ? "Pending" : (flag == 1 ? "OK" : "NOK");

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
                    Convert.ToInt32(reader.GetValue(reader.GetOrdinal("RC_Present"))) == 1 ? "OK" : "Pending";

                // Physical RFD (11): a Track_Logs row means dispatched.
                row.Rfd_Status =
                    Convert.ToInt32(reader.GetValue(reader.GetOrdinal("RFD_Present"))) == 1 ? "OK" : "Pending";

                // Rework (9): no Track_Logs row — derive from concern rework state.
                // Pending when any un-reworked concern remains; otherwise OK
                // (a vehicle with no concerns has nothing outstanding).
                int? rwTotal = Flag("RW_Total");
                int? rwDone = Flag("RW_Done");
                row.Rework_Status = (rwTotal.HasValue && rwTotal.Value > 0 && (rwDone ?? 0) < rwTotal.Value)
                    ? "Pending" : "OK";

                rows.Add(row);
            }

            return rows;
        }
    }
}
