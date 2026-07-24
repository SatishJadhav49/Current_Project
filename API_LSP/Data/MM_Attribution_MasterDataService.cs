using Microsoft.Data.SqlClient;
using API_LSP.Models.DTOs;

namespace API_LSP.Data
{
    public class MM_Attribution_MasterDataService
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MM_Attribution_MasterDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<bool> CreateAttribution(AttributionMasterCreateDto createAttribution)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string query = @"INSERT INTO dbo.MM_Attribution_Master 
                        (Attribution_Name, Sortorder, Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                        OUTPUT INSERTED.Attribution_ID
                        VALUES
                        (@Attribution_Name, @Sortorder,  @Inserted_Host, @Inserted_User_ID, @Inserted_Date, @Plant_Code)";

                    using var command = new SqlCommand(query, connection, transaction);

                    command.Parameters.Add(new SqlParameter("@Attribution_Name", createAttribution.Attribution_Name));
                    command.Parameters.Add(new SqlParameter("@Sortorder", createAttribution.SORTORDER));
                    command.Parameters.Add(new SqlParameter("@Inserted_Host", createAttribution.Inserted_Host ?? string.Empty));
                    command.Parameters.Add(new SqlParameter("@Inserted_User_ID", createAttribution.Inserted_User_ID ?? (object)DBNull.Value));
                    command.Parameters.Add(new SqlParameter("@Inserted_Date", createAttribution.Inserted_Date ?? DateTime.Now));
                    command.Parameters.Add(new SqlParameter("@Plant_Code", createAttribution.Plant_Code ?? (object)DBNull.Value));

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

        public async Task<bool> DeleteAttribution(decimal AttributionId)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string query = "DELETE FROM dbo.MM_Attribution_Master WHERE Attribution_ID = @Attribution_ID";
                    using var command = new SqlCommand(query, connection, transaction);
                    command.Parameters.Add(new SqlParameter("@Attribution_ID", AttributionId));

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

        public async Task<IEnumerable<AttributionMasterListDto>> GetAllAttributions()
        {
            var Attributions = new List<AttributionMasterListDto>();

            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string query = @"
                SELECT Attribution_ID, Attribution_Name, SORTORDER
                FROM MM_Attribution_Master";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                Attributions.Add(new AttributionMasterListDto
                {
                    Attribution_ID = reader.GetDecimal(reader.GetOrdinal("Attribution_ID")),
                    Attribution_Name = reader.GetString(reader.GetOrdinal("Attribution_Name")),
                    SORTORDER = reader.GetInt32(reader.GetOrdinal("SORTORDER")),
                });
            }

            return Attributions;
        }

        public async Task<bool> UpdateAttributionsMaster(decimal AttributionId, AttributionMasterUpdateDto updates)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string updateQuery = @"UPDATE MM_Attribution_Master
                        SET Attribution_Name = @Attribution_Name,
                            SORTORDER = @SORTORDER,
                            Plant_Code = @Plant_Code,
                            Updated_Host = @Updated_Host,
                            Updated_User_ID = @Updated_User_ID,
                            Updated_Date = @Updated_Date
                        WHERE Attribution_ID = @Attribution_ID";

                    using var updateCommand = new SqlCommand(updateQuery, connection, transaction);
                    updateCommand.Parameters.Add(new SqlParameter("@Attribution_ID", updates.Attribution_ID));
                    updateCommand.Parameters.Add(new SqlParameter("@Attribution_Name", updates.Attribution_Name));
                    updateCommand.Parameters.Add(new SqlParameter("@SORTORDER", updates.SORTORDER));
                    updateCommand.Parameters.Add(new SqlParameter("@Plant_Code", updates.Plant_Code));
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
