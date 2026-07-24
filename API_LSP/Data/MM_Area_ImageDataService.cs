using API_LSP.Models.DTOs;
using Microsoft.Data.SqlClient;

namespace API_LSP.Data
{
    /// <summary>
    /// CRUD for the image-based Paint Verification masters:
    ///  - MM_Area_Image_Master (uploaded area images)
    ///  - MM_Area_Image_Part_Mapping (part hotspots with normalized coords)
    /// File bytes are handled by the controller; this service persists rows only.
    /// </summary>
    public class MM_Area_ImageDataService
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MM_Area_ImageDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        // ─── Images ──────────────────────────────────────────────────────────

        public async Task<List<AreaImageListDto>> GetAllAreaImages()
        {
            var list = new List<AreaImageListDto>();
            const string sql = @"
                SELECT
                    ai.Area_Image_ID, ai.Vehicle_Area_ID, va.Vehicle_Area_Name,
                    ai.Model_ID, ISNULL(mo.Model_Name, '') AS Model_Name,
                    ai.Image_Name, ai.Image_FileName, ai.SORTORDER,
                    (SELECT COUNT(*) FROM MM_Area_Image_Part_Mapping m
                     WHERE m.Area_Image_ID = ai.Area_Image_ID) AS Mapping_Count
                FROM MM_Area_Image_Master ai
                INNER JOIN MM_Vehicle_Area_Master va ON va.Vehicle_Area_ID = ai.Vehicle_Area_ID
                LEFT  JOIN MM_Model mo ON mo.Model_ID = ai.Model_ID
                WHERE ai.Is_Deleted IS NULL OR ai.Is_Deleted = 0
                ORDER BY mo.Model_Name, va.SORTORDER, ai.SORTORDER, ai.Area_Image_ID";

            using var connection = await _connectionFactory.CreateConnectionAsync();
            using var cmd = new SqlCommand(sql, connection);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new AreaImageListDto
                {
                    Area_Image_ID = reader.GetDecimal(reader.GetOrdinal("Area_Image_ID")),
                    Vehicle_Area_ID = reader.GetDecimal(reader.GetOrdinal("Vehicle_Area_ID")),
                    Vehicle_Area_Name = reader.GetString(reader.GetOrdinal("Vehicle_Area_Name")),
                    Model_ID = reader.GetDecimal(reader.GetOrdinal("Model_ID")),
                    Model_Name = reader.GetString(reader.GetOrdinal("Model_Name")),
                    Image_Name = reader.GetString(reader.GetOrdinal("Image_Name")),
                    Image_FileName = reader.GetString(reader.GetOrdinal("Image_FileName")),
                    SORTORDER = reader.GetInt32(reader.GetOrdinal("SORTORDER")),
                    Mapping_Count = reader.GetInt32(reader.GetOrdinal("Mapping_Count")),
                });
            }
            return list;
        }

        /// <summary>Insert a new image row (file already saved to disk by the
        /// controller). Returns the new Area_Image_ID.</summary>
        public async Task<decimal> CreateAreaImage(AreaImageSaveDto dto, string savedFileName)
        {
            const string sql = @"
                INSERT INTO MM_Area_Image_Master
                    (Vehicle_Area_ID, Model_ID, Image_Name, Image_FileName, SORTORDER,
                     Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                OUTPUT INSERTED.Area_Image_ID
                VALUES (@Vehicle_Area_ID, @Model_ID, @Image_Name, @Image_FileName, @SORTORDER,
                        @Host, @UserId, GETDATE(), @Plant_Code)";

            using var connection = await _connectionFactory.CreateConnectionAsync();
            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Vehicle_Area_ID", dto.Vehicle_Area_ID);
            cmd.Parameters.AddWithValue("@Model_ID", dto.Model_ID);
            cmd.Parameters.AddWithValue("@Image_Name", dto.Image_Name);
            cmd.Parameters.AddWithValue("@Image_FileName", savedFileName);
            cmd.Parameters.AddWithValue("@SORTORDER", dto.SORTORDER);
            cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", (object?)dto.Inserted_User_ID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Plant_Code", (object?)dto.Plant_Code ?? DBNull.Value);

            var scalar = await cmd.ExecuteScalarAsync();
            return scalar != null && scalar != DBNull.Value ? Convert.ToDecimal(scalar) : 0;
        }

        /// <summary>Update metadata; optionally swap the image file (newFileName
        /// non-null when a replacement file was uploaded). Returns the OLD
        /// file name when a swap happened so the controller can delete it.</summary>
        public async Task<string?> UpdateAreaImage(AreaImageSaveDto dto, string? newFileName)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            string? oldFileName = null;
            if (newFileName != null)
            {
                const string readOld = @"
                    SELECT Image_FileName FROM MM_Area_Image_Master WHERE Area_Image_ID = @Id";
                using var readCmd = new SqlCommand(readOld, connection);
                readCmd.Parameters.AddWithValue("@Id", dto.Area_Image_ID);
                var scalar = await readCmd.ExecuteScalarAsync();
                if (scalar != null && scalar != DBNull.Value) oldFileName = (string)scalar;
            }

            const string sql = @"
                UPDATE MM_Area_Image_Master
                SET Vehicle_Area_ID = @Vehicle_Area_ID,
                    Model_ID = @Model_ID,
                    Image_Name = @Image_Name,
                    SORTORDER = @SORTORDER,
                    Image_FileName = COALESCE(@NewFileName, Image_FileName),
                    Is_Edited = 1,
                    Updated_Host = @Host,
                    Updated_User_ID = @UserId,
                    Updated_Date = GETDATE(),
                    Plant_Code = @Plant_Code
                WHERE Area_Image_ID = @Id";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", dto.Area_Image_ID);
            cmd.Parameters.AddWithValue("@Vehicle_Area_ID", dto.Vehicle_Area_ID);
            cmd.Parameters.AddWithValue("@Model_ID", dto.Model_ID);
            cmd.Parameters.AddWithValue("@Image_Name", dto.Image_Name);
            cmd.Parameters.AddWithValue("@SORTORDER", dto.SORTORDER);
            cmd.Parameters.AddWithValue("@NewFileName", (object?)newFileName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", (object?)dto.Inserted_User_ID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Plant_Code", (object?)dto.Plant_Code ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();

            return oldFileName;
        }

        /// <summary>Soft-delete an image row. Returns its file name so the
        /// controller can decide whether to remove the file from disk.
        /// Mappings stay (row is only soft-deleted; hard cleanup is manual).</summary>
        public async Task<string?> DeleteAreaImage(decimal areaImageId)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string readSql = @"
                SELECT Image_FileName FROM MM_Area_Image_Master WHERE Area_Image_ID = @Id";
            string? fileName = null;
            using (var readCmd = new SqlCommand(readSql, connection))
            {
                readCmd.Parameters.AddWithValue("@Id", areaImageId);
                var scalar = await readCmd.ExecuteScalarAsync();
                if (scalar != null && scalar != DBNull.Value) fileName = (string)scalar;
            }

            const string sql = @"
                UPDATE MM_Area_Image_Master
                SET Is_Deleted = 1, Updated_Date = GETDATE()
                WHERE Area_Image_ID = @Id";
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@Id", areaImageId);
                await cmd.ExecuteNonQueryAsync();
            }

            return fileName;
        }

        // ─── Mappings ────────────────────────────────────────────────────────

        public async Task<List<AreaImageMappingDto>> GetMappings(decimal areaImageId)
        {
            var list = new List<AreaImageMappingDto>();
            const string sql = @"
                SELECT m.Mapping_ID, m.Area_Image_ID, m.Part_ID, p.Part_Name, m.Shape
                FROM MM_Area_Image_Part_Mapping m
                INNER JOIN MM_Part_Master p ON p.Part_ID = m.Part_ID
                WHERE m.Area_Image_ID = @Id
                ORDER BY m.Mapping_ID";

            using var connection = await _connectionFactory.CreateConnectionAsync();
            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", areaImageId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new AreaImageMappingDto
                {
                    Mapping_ID = reader.GetDecimal(reader.GetOrdinal("Mapping_ID")),
                    Area_Image_ID = reader.GetDecimal(reader.GetOrdinal("Area_Image_ID")),
                    Part_ID = reader.GetDecimal(reader.GetOrdinal("Part_ID")),
                    Part_Name = reader.GetString(reader.GetOrdinal("Part_Name")),
                    Shape = reader.GetString(reader.GetOrdinal("Shape")),
                });
            }
            return list;
        }

        public async Task<decimal> SaveMapping(AreaImageMappingDto dto)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            if (dto.Mapping_ID > 0)
            {
                const string updateSql = @"
                    UPDATE MM_Area_Image_Part_Mapping
                    SET Part_ID = @Part_ID,
                        Shape = @Shape,
                        Updated_Host = @Host,
                        Updated_User_ID = @UserId,
                        Updated_Date = GETDATE(),
                        Plant_Code = @Plant_Code
                    WHERE Mapping_ID = @Id";
                using var cmd = new SqlCommand(updateSql, connection);
                cmd.Parameters.AddWithValue("@Id", dto.Mapping_ID);
                cmd.Parameters.AddWithValue("@Part_ID", dto.Part_ID);
                cmd.Parameters.AddWithValue("@Shape", dto.Shape);
                cmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UserId", (object?)dto.Inserted_User_ID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Plant_Code", (object?)dto.Plant_Code ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
                return dto.Mapping_ID;
            }

            const string insertSql = @"
                INSERT INTO MM_Area_Image_Part_Mapping
                    (Area_Image_ID, Part_ID, Shape,
                     Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                OUTPUT INSERTED.Mapping_ID
                VALUES (@Area_Image_ID, @Part_ID, @Shape, @Host, @UserId, GETDATE(), @Plant_Code)";

            using var insCmd = new SqlCommand(insertSql, connection);
            insCmd.Parameters.AddWithValue("@Area_Image_ID", dto.Area_Image_ID);
            insCmd.Parameters.AddWithValue("@Part_ID", dto.Part_ID);
            insCmd.Parameters.AddWithValue("@Shape", dto.Shape);
            insCmd.Parameters.AddWithValue("@Host", (object?)dto.Inserted_Host ?? DBNull.Value);
            insCmd.Parameters.AddWithValue("@UserId", (object?)dto.Inserted_User_ID ?? DBNull.Value);
            insCmd.Parameters.AddWithValue("@Plant_Code", (object?)dto.Plant_Code ?? DBNull.Value);

            var scalar = await insCmd.ExecuteScalarAsync();
            return scalar != null && scalar != DBNull.Value ? Convert.ToDecimal(scalar) : 0;
        }

        public async Task<bool> DeleteMapping(decimal mappingId)
        {
            const string sql = @"DELETE FROM MM_Area_Image_Part_Mapping WHERE Mapping_ID = @Id";
            using var connection = await _connectionFactory.CreateConnectionAsync();
            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", mappingId);
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        // ─── Audit-screen read ───────────────────────────────────────────────

        /// <summary>Every active image for a Vehicle Area + Model, each with its
        /// part hotspots — consumed by the Paint Verification stage. Images are
        /// model-specific (a Thar front looks nothing like a 3XO front).</summary>
        public async Task<List<AreaImageWithMappingsDto>> GetImagesByArea(decimal vehicleAreaId, decimal modelId)
        {
            var images = new List<AreaImageWithMappingsDto>();
            const string imagesSql = @"
                SELECT Area_Image_ID, Vehicle_Area_ID, Model_ID, Image_Name, Image_FileName, SORTORDER
                FROM MM_Area_Image_Master
                WHERE Vehicle_Area_ID = @AreaId
                  AND Model_ID = @ModelId
                  AND (Is_Deleted IS NULL OR Is_Deleted = 0)
                ORDER BY SORTORDER, Area_Image_ID";

            using var connection = await _connectionFactory.CreateConnectionAsync();
            using (var cmd = new SqlCommand(imagesSql, connection))
            {
                cmd.Parameters.AddWithValue("@AreaId", vehicleAreaId);
                cmd.Parameters.AddWithValue("@ModelId", modelId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    images.Add(new AreaImageWithMappingsDto
                    {
                        Area_Image_ID = reader.GetDecimal(reader.GetOrdinal("Area_Image_ID")),
                        Vehicle_Area_ID = reader.GetDecimal(reader.GetOrdinal("Vehicle_Area_ID")),
                        Model_ID = reader.GetDecimal(reader.GetOrdinal("Model_ID")),
                        Image_Name = reader.GetString(reader.GetOrdinal("Image_Name")),
                        Image_FileName = reader.GetString(reader.GetOrdinal("Image_FileName")),
                        SORTORDER = reader.GetInt32(reader.GetOrdinal("SORTORDER")),
                    });
                }
            }

            if (images.Count == 0) return images;

            // Load all mappings for these images in one query.
            var idList = string.Join(",", images.Select(i => i.Area_Image_ID));
            var mappingsSql = $@"
                SELECT m.Mapping_ID, m.Area_Image_ID, m.Part_ID, p.Part_Name, m.Shape
                FROM MM_Area_Image_Part_Mapping m
                INNER JOIN MM_Part_Master p ON p.Part_ID = m.Part_ID
                WHERE m.Area_Image_ID IN ({idList})
                ORDER BY m.Mapping_ID";

            using (var cmd = new SqlCommand(mappingsSql, connection))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var mapping = new AreaImageMappingDto
                    {
                        Mapping_ID = reader.GetDecimal(reader.GetOrdinal("Mapping_ID")),
                        Area_Image_ID = reader.GetDecimal(reader.GetOrdinal("Area_Image_ID")),
                        Part_ID = reader.GetDecimal(reader.GetOrdinal("Part_ID")),
                        Part_Name = reader.GetString(reader.GetOrdinal("Part_Name")),
                        Shape = reader.GetString(reader.GetOrdinal("Shape")),
                    };
                    images.First(i => i.Area_Image_ID == mapping.Area_Image_ID)
                          .Mappings.Add(mapping);
                }
            }

            return images;
        }
    }
}
