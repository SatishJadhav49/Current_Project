using API_LSP.Models.DTOs;
using Microsoft.Data.SqlClient;

namespace API_LSP.Data
{
    public class MM_Defect_Type_MasterDataService
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MM_Defect_Type_MasterDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }
        public async Task<bool> CreateDefectType(DefectTypeMasterCreateDto createdefecttype)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string query = @"INSERT INTO dbo.MM_Defect_Type_Master 
                        (Defect_Type_Name, SORTORDER, Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                        OUTPUT INSERTED.Defect_Type_ID
                        VALUES
                        (@Defect_Type_Name, @SORTORDER, @Inserted_Host, @Inserted_User_ID, @Inserted_Date, @Plant_Code)";

                    using var command = new SqlCommand(query, connection, transaction);

                    command.Parameters.Add(new SqlParameter("@Defect_Type_Name", createdefecttype.Defect_Type_Name));
                    command.Parameters.Add(new SqlParameter("@SORTORDER", createdefecttype.SORTORDER));
                    command.Parameters.Add(new SqlParameter("@Inserted_Host", createdefecttype.Inserted_Host ?? string.Empty));
                    command.Parameters.Add(new SqlParameter("@Inserted_User_ID", createdefecttype.Inserted_User_ID ?? (object)DBNull.Value));
                    command.Parameters.Add(new SqlParameter("@Inserted_Date", createdefecttype.Inserted_Date ?? DateTime.Now));
                    command.Parameters.Add(new SqlParameter("@Plant_Code", createdefecttype.Plant_Code ?? (object)DBNull.Value));

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

        public async Task<bool> DeleteDefectType(decimal Defect_Type_Id)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string query = "DELETE FROM dbo.MM_Defect_Type_Master WHERE Defect_Type_ID = @Defect_Type_ID";
                    using var command = new SqlCommand(query, connection, transaction);
                    command.Parameters.Add(new SqlParameter("@Defect_Type_ID", Defect_Type_Id));

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

        public async Task<List<DefectTypeMasterListDto>> GetAllDefectTypes()
        {
            var defectTypes = new List<DefectTypeMasterListDto>();

            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string query = @"
                    SELECT 
                        d.Defect_Type_ID, d.Defect_Type_Name, d.SORTORDER
                    FROM MM_Defect_Type_Master d
                    ";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                defectTypes.Add(new DefectTypeMasterListDto
                {
                    Defect_Type_ID = reader.GetDecimal(reader.GetOrdinal("Defect_Type_ID")),
                    Defect_Type_Name = reader.GetString(reader.GetOrdinal("Defect_Type_Name")),
                    SORTORDER = reader.GetInt32(reader.GetOrdinal("SORTORDER")),
                });
            }

            return defectTypes;
        }

        public async Task<bool> UpdateDefectType(decimal Defect_Type_Id, DefectTypeMasterUpdateDto updates)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string updateQuery = @"UPDATE MM_Defect_Type_Master
                        SET Defect_Type_Name = @Defect_Type_Name,
                            SORTORDER = @SORTORDER,
                            Is_Edited = 1,
                            Updated_Host = @Updated_Host,
                            Updated_User_ID = @Updated_User_ID,
                            Updated_Date = @Updated_Date
                        WHERE Defect_Type_ID = @Defect_Type_ID";

                    using var updateCommand = new SqlCommand(updateQuery, connection, transaction);
                    updateCommand.Parameters.Add(new SqlParameter("@Defect_Type_ID", updates.Defect_Type_ID));
                    updateCommand.Parameters.Add(new SqlParameter("@Defect_Type_Name", updates.Defect_Type_Name));
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
