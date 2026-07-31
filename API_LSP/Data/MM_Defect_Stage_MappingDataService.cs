using API_LSP.Models.DTOs;
using Microsoft.Data.SqlClient;

namespace API_LSP.Data
{
    public class MM_Defect_Stage_MappingDataService
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MM_Defect_Stage_MappingDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        // Stages that use the defect-entry form: Road Test, Under Body,
        // Static Audit, Paint Verification. Ordered by SORTORDER.
        private static readonly decimal[] EntryStageIds = { 1m, 2m, 6m, 12m };

        /// <summary>Entry stages + how many defects are mapped to each (master list).</summary>
        public async Task<List<StageMappingSummaryDto>> GetStageSummaries()
        {
            var list = new List<StageMappingSummaryDto>();
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string query = @"
                SELECT aa.Audit_Area_ID, aa.Audit_Area_Name,
                       ISNULL(m.Cnt, 0) AS Mapped_Count
                FROM MM_Audit_Area_Master aa
                LEFT JOIN (
                    SELECT Audit_Area_ID, COUNT(*) AS Cnt
                    FROM MM_Defect_Stage_Mapping
                    GROUP BY Audit_Area_ID
                ) m ON m.Audit_Area_ID = aa.Audit_Area_ID
                WHERE aa.Audit_Area_ID IN (1, 2, 6, 12)
                ORDER BY aa.SORTORDER";

            using var cmd = new SqlCommand(query, connection);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new StageMappingSummaryDto
                {
                    Audit_Area_ID = reader.GetDecimal(0),
                    Audit_Area_Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Mapped_Count = Convert.ToInt32(reader.GetValue(2)),
                });
            }
            return list;
        }

        /// <summary>The Defect_IDs mapped to a stage (drives the master checklist).</summary>
        public async Task<List<decimal>> GetStageDefectIds(decimal auditAreaId)
        {
            var ids = new List<decimal>();
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string query = "SELECT Defect_ID FROM MM_Defect_Stage_Mapping WHERE Audit_Area_ID = @Area";
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Area", auditAreaId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                ids.Add(reader.GetDecimal(0));
            return ids;
        }

        /// <summary>
        /// Effective defect list for a stage used by the defect-entry form:
        /// the mapped defects, or ALL defects when the stage has no mapping.
        /// </summary>
        public async Task<List<StageDefectDto>> GetMappedDefects(decimal auditAreaId)
        {
            var list = new List<StageDefectDto>();
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string query = @"
                IF EXISTS (SELECT 1 FROM MM_Defect_Stage_Mapping WHERE Audit_Area_ID = @Area)
                    SELECT d.Defect_ID, d.Defect_Name, d.SORTORDER
                    FROM MM_Defect_Master d
                    INNER JOIN MM_Defect_Stage_Mapping m ON m.Defect_ID = d.Defect_ID
                    WHERE m.Audit_Area_ID = @Area
                    ORDER BY d.SORTORDER, d.Defect_Name
                ELSE
                    SELECT d.Defect_ID, d.Defect_Name, d.SORTORDER
                    FROM MM_Defect_Master d
                    ORDER BY d.SORTORDER, d.Defect_Name";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Area", auditAreaId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new StageDefectDto
                {
                    Defect_ID = reader.GetDecimal(0),
                    Defect_Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    SORTORDER = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                });
            }
            return list;
        }

        /// <summary>Replace the full defect set mapped to a stage (delete + insert).</summary>
        public async Task<bool> SaveStageMapping(SaveStageMappingDto dto)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                using (var del = new SqlCommand(
                    "DELETE FROM MM_Defect_Stage_Mapping WHERE Audit_Area_ID = @Area", connection, transaction))
                {
                    del.Parameters.AddWithValue("@Area", dto.Audit_Area_ID);
                    await del.ExecuteNonQueryAsync();
                }

                foreach (var defectId in dto.Defect_IDs.Distinct())
                {
                    using var ins = new SqlCommand(@"
                        INSERT INTO MM_Defect_Stage_Mapping
                            (Audit_Area_ID, Defect_ID, Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                        VALUES (@Area, @Defect, @Host, @User, GETDATE(), @Plant)",
                        connection, transaction);
                    ins.Parameters.AddWithValue("@Area", dto.Audit_Area_ID);
                    ins.Parameters.AddWithValue("@Defect", defectId);
                    ins.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
                    ins.Parameters.AddWithValue("@User", (object?)dto.Inserted_User_ID ?? DBNull.Value);
                    ins.Parameters.AddWithValue("@Plant", (object?)dto.Plant_Code ?? DBNull.Value);
                    await ins.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}
