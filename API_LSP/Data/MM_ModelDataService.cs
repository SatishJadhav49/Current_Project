using API_LSP.Models;
using API_LSP.Models.DTOs;
using Microsoft.Data.SqlClient;
using System.Data;

namespace API_LSP.Data
{
    public class MM_ModelDataService
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MM_ModelDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        // GetModelList
        public async Task<List<MM_ModelListDto>> GetModelList()
        {
            try
            {
                const string query = @"
                    SELECT 
                    Model_ID,
                    Model_Name,
                    Is_LSP_Active
                    FROM MM_Model";

                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                var modelList = new List<MM_ModelListDto>();

                while (await reader.ReadAsync())
                {
                    modelList.Add(new MM_ModelListDto
                    {
                        Model_ID = reader.GetDecimal(reader.GetOrdinal("Model_ID")),
                        Model_Name = reader.GetString(reader.GetOrdinal("Model_Name")),
                        Is_LSP_Active = reader.IsDBNull(reader.GetOrdinal("Is_LSP_Active")) ? (bool?)null : reader.GetBoolean(reader.GetOrdinal("Is_LSP_Active"))
                    });
                }
                return modelList;
            }
            catch (Exception)
            {
                throw;
            }
        }
        // UpdateModelStatus
        public async  Task<bool>  UpdateModelStatus(MM_ModelListDto model)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string query = @"UPDATE MM_Model
                        SET Is_LSP_Active = @Is_LSP_Active , 
                            Updated_Host = @Updated_Host,
                            Updated_User_ID = @Updated_User_ID,
                            Updated_Date = GETDATE()
                        WHERE Model_ID = @Model_ID";

                    using var command = new SqlCommand(query, connection, transaction);
                    command.Parameters.Add(new SqlParameter("@Is_LSP_Active", model.Is_LSP_Active ?? (object)DBNull.Value));
                    command.Parameters.Add(new SqlParameter("@Updated_Host", model.Updated_Host ?? (object)DBNull.Value));
                    command.Parameters.Add(new SqlParameter("@Updated_User_ID", model.Updated_User_ID ?? (object)DBNull.Value));
                    command.Parameters.Add(new SqlParameter("@Model_ID", model.Model_ID));

                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    await transaction.CommitAsync();
                    return rowsAffected > 0;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
