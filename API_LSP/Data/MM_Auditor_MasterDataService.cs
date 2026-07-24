using System.Data;
using API_LSP.Models.DTOs;
using Microsoft.Data.SqlClient;

namespace API_LSP.Data
{
    public class MM_Auditor_MasterDataService
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MM_Auditor_MasterDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }
        public async Task<bool> CreateAuditor(AuditorMasterCreateDto createItem)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string query = @"INSERT INTO MM_Auditor_Master 
                        (Audit_Area_ID, Auditor_Name, Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                        OUTPUT INSERTED.Auditor_ID
                        VALUES
                        (@Audit_Area_ID, @Auditor_Name, @Inserted_Host, @Inserted_User_ID, @Inserted_Date, @Plant_Code)";

                    using var command = new SqlCommand(query, connection, transaction);

                    command.Parameters.Add(new SqlParameter("@Audit_Area_ID", createItem.Audit_Area_ID));
                    command.Parameters.Add(new SqlParameter("@Auditor_Name", createItem.Auditor_Name));
                    command.Parameters.Add(new SqlParameter("@Inserted_Host", createItem.Inserted_Host ?? string.Empty));
                    command.Parameters.Add(new SqlParameter("@Inserted_User_ID", createItem.Inserted_User_ID ?? (object)DBNull.Value));
                    command.Parameters.Add(new SqlParameter("@Inserted_Date", createItem.Inserted_Date ?? DateTime.Now));
                    command.Parameters.Add(new SqlParameter("@Plant_Code", createItem.Plant_Code ?? (object)DBNull.Value));

                    await command.ExecuteNonQueryAsync();
                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch
            {
                throw;
            }
        }

        public async Task<bool> DeleteAuditor(decimal Auditor_Id)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string query = "DELETE FROM MM_Auditor_Master WHERE Auditor_ID = @Auditor_ID";
                    using var command = new SqlCommand(query, connection, transaction);
                    command.Parameters.Add(new SqlParameter("@Auditor_ID", Auditor_Id));

                    var rowsAffected = await command.ExecuteNonQueryAsync();

                    // Commit the transaction
                    transaction.Commit();

                    return rowsAffected > 0;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch
            {
                throw;
            }
        }

        public async Task<List<AuditorMasterListDto>> GetAllAuditors()
        {
            var auditor = new List<AuditorMasterListDto>();

            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string query = @"
                    SELECT 
                        a.Audit_Area_ID, a.Auditor_ID, a.Auditor_Name, area.Audit_Area_Name
                    FROM MM_Auditor_Master a 
					inner join MM_Audit_Area_Master area on area.Audit_Area_ID = a.Audit_Area_ID
                    ";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                auditor.Add(new AuditorMasterListDto
                {
                    Auditor_ID = reader.GetDecimal(reader.GetOrdinal("Auditor_ID")),
                    Auditor_Name = reader.GetString(reader.GetOrdinal("Auditor_Name")),
                    Audit_Area_ID = reader.GetDecimal(reader.GetOrdinal("Audit_Area_ID")),
                    Audit_Area_Name = reader.GetString(reader.GetOrdinal("Audit_Area_Name"))
                });
            }

            return auditor;
        }

        public async Task<List<AuditorMasterListDto>> GetAuditorsByArea(decimal auditAreaId)
        {
            var auditors = new List<AuditorMasterListDto>();

            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string query = @"
                SELECT
                    a.Audit_Area_ID, a.Auditor_ID, a.Auditor_Name, area.Audit_Area_Name
                FROM MM_Auditor_Master a
                INNER JOIN MM_Audit_Area_Master area ON area.Audit_Area_ID = a.Audit_Area_ID
                WHERE a.Audit_Area_ID = @AuditAreaId
                  AND (a.Is_Deleted IS NULL OR a.Is_Deleted = 0)
                ORDER BY a.Auditor_Name";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@AuditAreaId", auditAreaId);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                auditors.Add(new AuditorMasterListDto
                {
                    Auditor_ID = reader.GetDecimal(reader.GetOrdinal("Auditor_ID")),
                    Auditor_Name = reader.GetString(reader.GetOrdinal("Auditor_Name")),
                    Audit_Area_ID = reader.GetDecimal(reader.GetOrdinal("Audit_Area_ID")),
                    Audit_Area_Name = reader.GetString(reader.GetOrdinal("Audit_Area_Name"))
                });
            }

            return auditors;
        }

        public async Task<bool> UpdateAuditor(decimal Auditor_Id, AuditorMasterUpdateDto updates)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string updateQuery = @"UPDATE MM_Auditor_Master
                        SET Auditor_Name = @Auditor_Name,
                            Audit_Area_ID = @Audit_Area_ID,
                            Is_Edited = 1,
                            Updated_Host = @Updated_Host,
                            Updated_User_ID = @Updated_User_ID,
                            Updated_Date = @Updated_Date
                        WHERE Auditor_ID = @Auditor_ID";

                    using var updateCommand = new SqlCommand(updateQuery, connection, transaction);
                    updateCommand.Parameters.Add(new SqlParameter("@Auditor_ID", updates.Auditor_ID));
                    updateCommand.Parameters.Add(new SqlParameter("@Auditor_Name", updates.Auditor_Name));
                    updateCommand.Parameters.Add(new SqlParameter("@Audit_Area_ID", updates.Audit_Area_ID));
                    updateCommand.Parameters.Add(new SqlParameter("@Updated_Host", updates.Updated_Host));
                    updateCommand.Parameters.Add(new SqlParameter("@Updated_User_ID", updates.Updated_User_ID));
                    updateCommand.Parameters.Add(new SqlParameter("@Updated_Date", DateTime.Now));

                    await updateCommand.ExecuteNonQueryAsync();
                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch
            {
                throw;
            }
        }

    }
}
