using Microsoft.Data.SqlClient;
using API_LSP.Models.DTOs;

namespace API_LSP.Data
{
    public class MM_Location_MasterDataService
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MM_Location_MasterDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }
        public async Task<bool> CreateLocation(LocationMasterCreateDto createLocation)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string checkQuery = @"SELECT COUNT(1) 
                                        FROM dbo.MM_Location_Master 
                                        WHERE Location_Name = @Location_Name 
                                        AND Plant_Code = @Plant_Code";

                    using (var checkCommand = new SqlCommand(checkQuery, connection, transaction))
                    {
                        checkCommand.Parameters.Add(new SqlParameter("@Location_Name", createLocation.Location_Name));
                        checkCommand.Parameters.Add(new SqlParameter("@Plant_Code", createLocation.Plant_Code ?? (object)DBNull.Value));

                        var exists = (int)await checkCommand.ExecuteScalarAsync();
                        if (exists > 0)
                        {
                            transaction.Rollback();
                            return false; // Duplicate found
                        }
                    }

                    const string query = @"INSERT INTO dbo.MM_Location_Master 
                        (Location_Name, SORTORDER, Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                        OUTPUT INSERTED.Location_ID
                        VALUES
                        (@Location_Name, @SORTORDER,  @Inserted_Host, @Inserted_User_ID, @Inserted_Date, @Plant_Code)";

                    using var command = new SqlCommand(query, connection, transaction);

                    command.Parameters.Add(new SqlParameter("@Location_Name", createLocation.Location_Name));
                    command.Parameters.Add(new SqlParameter("@SORTORDER", createLocation.SORTORDER));
                    command.Parameters.Add(new SqlParameter("@Inserted_Host", createLocation.Inserted_Host ?? string.Empty));
                    command.Parameters.Add(new SqlParameter("@Inserted_User_ID", createLocation.Inserted_User_ID ?? (object)DBNull.Value));
                    command.Parameters.Add(new SqlParameter("@Inserted_Date", createLocation.Inserted_Date ?? DateTime.Now));
                    command.Parameters.Add(new SqlParameter("@Plant_Code", createLocation.Plant_Code ?? (object)DBNull.Value));

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

        public async Task<bool> DeleteLocation(decimal Location_ID)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string query = "DELETE FROM dbo.MM_Location_Master WHERE Location_ID = @Location_ID";
                    using var command = new SqlCommand(query, connection, transaction);
                    command.Parameters.Add(new SqlParameter("@Location_ID", Location_ID));

                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    Console.WriteLine(Location_ID);

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

        public async Task<IEnumerable<LocationMasterListDto>> GetAllLocations()
        {
            var LocationParameters = new List<LocationMasterListDto>();

            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string query = @"
                SELECT Location_ID, Location_Name, SORTORDER
                FROM MM_Location_Master";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                LocationParameters.Add(new LocationMasterListDto
                {
                    Location_ID = reader.GetDecimal(reader.GetOrdinal("Location_ID")),
                    Location_Name = reader.GetString(reader.GetOrdinal("Location_Name")),
                    SORTORDER = reader.GetInt32(reader.GetOrdinal("SORTORDER")),
                });
            }

            return LocationParameters;
        }

        public async Task<bool> UpdateLocation(decimal Location_ID, LocationMasterUpdateDto updates)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string updateQuery = @"UPDATE MM_Location_Master
                        SET Location_Name = @Location_Name,
                            SORTORDER = @SORTORDER,
                            Is_Edited = 1,
                            Updated_Host = @Updated_Host,
                            Updated_User_ID = @Updated_User_ID,
                            Updated_Date = @Updated_Date
                        WHERE Location_ID = @Location_ID";

                    using var updateCommand = new SqlCommand(updateQuery, connection, transaction);
                    updateCommand.Parameters.Add(new SqlParameter("@Location_ID", updates.Location_ID));
                    updateCommand.Parameters.Add(new SqlParameter("@Location_Name", updates.Location_Name));
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
