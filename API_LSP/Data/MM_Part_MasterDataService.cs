using System.Data;
using API_LSP.Models.DTOs;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;


namespace API_LSP.Data
{
    public class MM_Part_MasterDataService
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MM_Part_MasterDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }
        public async Task<bool> CreatePart(PartMasterCreateDto createpart)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string query = @"INSERT INTO dbo.MM_Part_Master 
                        (Part_Name, SORTORDER, Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                        OUTPUT INSERTED.Part_ID
                        VALUES
                        (@Part_Name, @SORTORDER, @Inserted_Host, @Inserted_User_ID, @Inserted_Date, @Plant_Code)";

                    using var command = new SqlCommand(query, connection, transaction);

                    command.Parameters.Add(new SqlParameter("@Part_Name", createpart.Part_Name));
                    command.Parameters.Add(new SqlParameter("@SORTORDER", createpart.SORTORDER));
                    command.Parameters.Add(new SqlParameter("@Inserted_Host", createpart.Inserted_Host ?? string.Empty));
                    command.Parameters.Add(new SqlParameter("@Inserted_User_ID", createpart.Inserted_User_ID ?? (object)DBNull.Value));
                    command.Parameters.Add(new SqlParameter("@Inserted_Date", createpart.Inserted_Date ?? DateTime.Now));
                    command.Parameters.Add(new SqlParameter("@Plant_Code", createpart.Plant_Code ?? (object)DBNull.Value));

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

        public async Task<bool> DeletePart(decimal Part_Id)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string query = "DELETE FROM dbo.MM_Part_Master WHERE Part_ID = @Part_ID";
                    using var command = new SqlCommand(query, connection, transaction);
                    command.Parameters.Add(new SqlParameter("@Part_ID", Part_Id));

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

        public async Task<List<PartMasterListDto>> GetAllParts()
        {
            try
            {
                var part = new List<PartMasterListDto>();

                using var connection = await _connectionFactory.CreateConnectionAsync();
                const string query = @"
                    SELECT 
                        p.Part_ID, p.Part_Name, p.SORTORDER
                    FROM MM_Part_Master p
                    ";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    part.Add(new PartMasterListDto
                    {
                        Part_ID = reader.GetDecimal(reader.GetOrdinal("Part_ID")),
                        Part_Name = reader.GetString(reader.GetOrdinal("Part_Name")),
                        SORTORDER = reader.GetInt32(reader.GetOrdinal("SORTORDER")),
                    });
                }

                return part;
            }
            catch
            {
                throw;
            }
        }

        public async Task<bool> UpdatePart(decimal Part_Id, PartMasterUpdateDto updates)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    const string updateQuery = @"UPDATE MM_Part_Master
                        SET Part_Name = @Part_Name,
                            SORTORDER = @SORTORDER,
                            Is_Edited = 1,
                            Updated_Host = @Updated_Host,
                            Updated_User_ID = @Updated_User_ID,
                            Updated_Date = @Updated_Date
                        WHERE Part_ID = @Part_ID";

                    using var updateCommand = new SqlCommand(updateQuery, connection, transaction);
                    updateCommand.Parameters.Add(new SqlParameter("@Part_ID", updates.Part_ID));
                    updateCommand.Parameters.Add(new SqlParameter("@Part_Name", updates.Part_Name));
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


        public async Task<(List<PartMasterCreateDto> InsertedLines, List<string> Errors)> UploadLineDataAsync(IFormFile file, PartMasterCreateDto dto)
        {
            try
            {
                var insertedLines = new List<PartMasterCreateDto>();
                var errors = new List<string>();

                // var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", file.Name);
                string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/MasterTemplates/Parts/");
                if (!Directory.Exists(uploadPath))
                {
                    Directory.CreateDirectory(uploadPath);
                }

                string fileName = $"{Guid.NewGuid()}_{file.FileName}";
                string filePath = Path.Combine(uploadPath, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);

                ExcelPackage.License.SetNonCommercialOrganization("MMMMMMMMMM");
                using var package = new ExcelPackage(new FileInfo(filePath));
                var worksheet = package.Workbook.Worksheets[0];

                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var transaction = connection.BeginTransaction();

                for (int row = 2; row <= worksheet.Dimension.Rows; row++)
                {
                    var PartName = worksheet.Cells[row, 1].Text.Trim();
                    var sortOrderText = worksheet.Cells[row, 2].Text.Trim();

                    if (string.IsNullOrWhiteSpace(PartName))
                        errors.Add($"Row {row}: Part Name is required.");

                    if (!int.TryParse(sortOrderText, out int SORTORDER))
                        errors.Add($"Row {row}: SORTORDER must be a valid number.");

                    if (errors.Any())
                        continue;

                    var checkCmd = new SqlCommand(@"
                SELECT COUNT(*) FROM MM_Part_Master 
                WHERE LOWER(Part_Name) = @Part_Name AND Plant_Code = @PlantCode", connection, transaction);

                    checkCmd.Parameters.AddWithValue("@Part_Name", PartName.ToLower());
                    //checkCmd.Parameters.AddWithValue("@SORTORDER", dto.SORTORDER);
                    checkCmd.Parameters.AddWithValue("@PlantCode", dto.Plant_Code);

                    int exists = (int)await checkCmd.ExecuteScalarAsync();

                    if (exists == 0)
                    {
                        var insertCmd = new SqlCommand(@"
                    INSERT INTO MM_Part_Master 
                    (Part_Name, SORTORDER,  Plant_Code, Inserted_Host, Inserted_User_ID, Inserted_Date)
                    VALUES (@Part_Name, @SORTORDER,  @PlantCode, @InsertedHost, @InsertedUserID, @InsertedDate)", connection, transaction);

                        insertCmd.Parameters.AddWithValue("@Part_Name", PartName);
                        insertCmd.Parameters.AddWithValue("@SORTORDER", SORTORDER);
                        insertCmd.Parameters.AddWithValue("@PlantCode", dto.Plant_Code);
                        insertCmd.Parameters.AddWithValue("@InsertedHost", dto.Inserted_Host);
                        insertCmd.Parameters.AddWithValue("@InsertedUserID", dto.Inserted_User_ID);
                        insertCmd.Parameters.AddWithValue("@InsertedDate", DateTime.Now);

                        await insertCmd.ExecuteNonQueryAsync();
                        insertedLines.Add(dto);
                    }
                    else
                    {
                        errors.Add($"Row {row}: Duplicate line '{PartName}' already exists.");
                    }

                }

                transaction.Commit();
                return (insertedLines, errors);
            }
            catch (System.Exception)
            {

                throw;
            }
        }

        public async Task<List<PartExcelExportDataDto>> ExportExcelDataAsync()
        {
            var partDataList = new List<PartExcelExportDataDto>();

            const string query = @"
                                    SELECT 
                                        pm.Part_ID,
                                        pm.Part_Name, 
                                        pm.SORTORDER, 
                                    FROM MM_Part_Master pm";
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var partDto = new PartExcelExportDataDto
                    {
                        Part_ID = reader.IsDBNull("Part_ID") ? 0 : reader.GetDecimal("Part_ID"), //reader["Part_ID"] is decimal partId ? partId : 0,
                        Part_Name = reader["Part_Name"]?.ToString() ?? string.Empty,
                        SORTORDER = reader.IsDBNull("SORTORDER") ? 0 : reader.GetInt32("SORTORDER"),
                    };

                    partDataList.Add(partDto);
                }
            }
            catch (SqlException sqlEx)
            {
                throw new Exception("Database error occurred while exporting Excel data.", sqlEx);
            }
            catch (Exception ex)
            {
                throw new Exception("Unexpected error occurred while exporting Excel data." + ex.Message, ex);
            }

            return partDataList;
        }

    }
}
