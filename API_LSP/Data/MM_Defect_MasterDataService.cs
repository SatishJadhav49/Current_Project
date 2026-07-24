using API_LSP.Models.DTOs;
using Microsoft.Data.SqlClient;

namespace API_LSP.Data
{
    public class MM_Defect_MasterDataService
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MM_Defect_MasterDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }
        public async Task<bool> CreateDefect(DefectMasterCreateDto createdefect)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string query = @"INSERT INTO dbo.MM_Defect_Master 
                        (Defect_Name, SORTORDER, Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                        OUTPUT INSERTED.Defect_ID
                        VALUES
                        (@Defect_Name, @SORTORDER, @Inserted_Host, @Inserted_User_ID, @Inserted_Date, @Plant_Code)";

                    using var command = new SqlCommand(query, connection, transaction);

                    command.Parameters.Add(new SqlParameter("@Defect_Name", createdefect.Defect_Name));
                    command.Parameters.Add(new SqlParameter("@SORTORDER", createdefect.SORTORDER));
                    command.Parameters.Add(new SqlParameter("@Inserted_Host", createdefect.Inserted_Host ?? string.Empty));
                    command.Parameters.Add(new SqlParameter("@Inserted_User_ID", createdefect.Inserted_User_ID ?? (object)DBNull.Value));
                    command.Parameters.Add(new SqlParameter("@Inserted_Date", createdefect.Inserted_Date ?? DateTime.Now));
                    command.Parameters.Add(new SqlParameter("@Plant_Code", createdefect.Plant_Code ?? (object)DBNull.Value));

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

        public async Task<bool> DeleteDefect(decimal Defect_Id)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string query = "DELETE FROM dbo.MM_Defect_Master WHERE Defect_ID = @Defect_ID";
                    using var command = new SqlCommand(query, connection, transaction);
                    command.Parameters.Add(new SqlParameter("@Defect_ID", Defect_Id));

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

        public async Task<List<DefectMasterListDto>> GetAllDefects()
        {
            var defect = new List<DefectMasterListDto>();

            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string query = @"
                    SELECT 
                        d.Defect_ID, d.Defect_Name, d.SORTORDER
                    FROM MM_Defect_Master d
                    ";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                defect.Add(new DefectMasterListDto
                {
                    Defect_ID = reader.GetDecimal(reader.GetOrdinal("Defect_ID")),
                    Defect_Name = reader.GetString(reader.GetOrdinal("Defect_Name")),
                    SORTORDER = reader.GetInt32(reader.GetOrdinal("SORTORDER")),
                });
            }

            return defect;
        }

        public async Task<bool> UpdateDefect(decimal Defect_Id, DefectMasterUpdateDto updates)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string updateQuery = @"UPDATE MM_Defect_Master
                        SET Defect_Name = @Defect_Name,
                            SORTORDER = @SORTORDER,
                            Is_Edited = 1,
                            Updated_Host = @Updated_Host,
                            Updated_User_ID = @Updated_User_ID,
                            Updated_Date = @Updated_Date
                        WHERE Defect_ID = @Defect_ID";

                    using var updateCommand = new SqlCommand(updateQuery, connection, transaction);
                    updateCommand.Parameters.Add(new SqlParameter("@Defect_ID", updates.Defect_ID));
                    updateCommand.Parameters.Add(new SqlParameter("@Defect_Name", updates.Defect_Name));
                    updateCommand.Parameters.Add(new SqlParameter("@SORTORDER", updates.SORTORDER));
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
