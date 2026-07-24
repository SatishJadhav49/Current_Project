using Microsoft.Data.SqlClient;
using API_LSP.Models.DTOs;
using System.Reflection.Metadata;
using System.Data;

namespace API_LSP.Data
{
    public class MM_Vehicle_DetailsDataService
    {

        private readonly IDbConnectionFactory _connectionFactory;

        public MM_Vehicle_DetailsDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        // GetVinData
        public async Task<MM_Vehicle_DetailsDto> GetVinDetails(string Vin_No)
        {
            try
            {
                MM_Vehicle_DetailsDto vehicleDetails = null;
                using var connection = await _connectionFactory.CreateConnectionAsync();

                // Step 1: Check if VIN already exists in MM_Vehicle_Details
                const string checkQuery = @"
                                           select * from MM_Vehicle_Details
                                            WHERE VIN_No LIKE @Vin_No";

                using (var command = new SqlCommand(checkQuery, connection))
                {
                    command.Parameters.AddWithValue("@Vin_No", "%" + Vin_No);

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        vehicleDetails = new MM_Vehicle_DetailsDto
                        {
                            Vehicle_ID = reader["Vehicle_ID"] != DBNull.Value ? Convert.ToInt32(reader["Vehicle_ID"]) : 0,
                            VIN_No = reader["VIN_No"].ToString() ?? "",
                            Body_Serial_No = reader["Body_Serial_No"].ToString() ?? "",
                            Model_ID = reader["Model_ID"] != DBNull.Value ? Convert.ToInt32(reader["Model_ID"]) : 0,
                            Drive_ID = reader["Drive_ID"] != DBNull.Value ? Convert.ToInt32(reader["Drive_ID"]) : 0,
                            Fuel_ID = reader["Fuel_ID"] != DBNull.Value ? Convert.ToInt32(reader["Fuel_ID"]) : 0,
                            Country_ID = reader["Country_ID"] != DBNull.Value ? Convert.ToInt32(reader["Country_ID"]) : 0,
                            Build_Phase_ID = reader["Build_Phase_ID"] != DBNull.Value ? Convert.ToInt32(reader["Build_Phase_ID"]) : 0,
                            Engine_No = reader["Engine_No"].ToString() ?? "",
                            Roll_Down_Date = reader["Roll_Down_Date"] != DBNull.Value ? Convert.ToDateTime(reader["Roll_Down_Date"]) : (DateTime?)null,
                            CAI_OK_Date = reader["CAI_OK_Date"] != DBNull.Value ? Convert.ToDateTime(reader["CAI_OK_Date"]) : (DateTime?)null,
                            Model_Code = reader["Model_Code"].ToString() ?? "",
                            Model_Desc = reader["Model_Desc"].ToString() ?? "",
                            Variant = reader["Variant"] != DBNull.Value ? reader["Variant"].ToString() : string.Empty,
                            Colour_Code = reader["Colour_Code"] != DBNull.Value ? reader["Colour_Code"].ToString() : string.Empty,
                            Colour_Desc = reader["Colour_Desc"] != DBNull.Value ? reader["Colour_Desc"].ToString() : string.Empty
                        };
                    }
                }
                // vehicleDetails.Body_Serial_No = "12345678901";
                // vehicleDetails.Engine_No = "Engine No 12345678901";
                // vehicleDetails.Model_Code = "Model Code 12345678901";
                // vehicleDetails.Model_Desc = "Model Desc 12345678901";
                // vehicleDetails.Colour_Code = "BR -2";
                // vehicleDetails.Colour_Desc = "black Red";
                // vehicleDetails.VIN_No = Vin_No;
                // vehicleDetails.Roll_Down_Date = DateTime.Now;
                // vehicleDetails.CAI_OK_Date = DateTime.Now;
                // vehicleDetails.Variant = "Variant 12345678901";
                if (vehicleDetails != null)
                    return vehicleDetails;





                using (var command = new SqlCommand("SP_GetDataFrom_Vin_Number", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@Vin_Number", Vin_No);

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        vehicleDetails = new MM_Vehicle_DetailsDto
                        {
                            VIN_No = reader["VIN_Number"].ToString() ?? "",
                            Body_Serial_No = reader["BIW_No"].ToString() ?? "",
                            Engine_No = reader["Engine_No"].ToString() ?? "",
                            Roll_Down_Date = reader["RollDown_Date"] != DBNull.Value ? Convert.ToDateTime(reader["RollDown_Date"]) : (DateTime?)null,
                            CAI_OK_Date = reader["CAIOut_Date"] != DBNull.Value ? Convert.ToDateTime(reader["CAIOut_Date"]) : (DateTime?)null,
                            Model_Code = reader["Model_Code"].ToString() ?? "",
                            Model_Desc = reader["Model_Description"].ToString() ?? "",
                            Variant = reader["Variant"] != DBNull.Value ? reader["Variant"].ToString() : string.Empty,
                            Colour_Code = reader["Color_Code"] != DBNull.Value ? reader["Color_Code"].ToString() : string.Empty,
                            Colour_Desc = reader["Colour_Desc"] != DBNull.Value ? reader["Colour_Desc"].ToString() : string.Empty
                        };

                        if (reader["Model_Name"].ToString()?.Length > 0)
                        {
                            // Get model ID based on Model_Name
                            using var modelCmd = new SqlCommand("SELECT Model_ID FROM MM_Model WHERE Model_Name = @Model_Name", connection);
                            modelCmd.Parameters.AddWithValue("@Model_Name", reader["Model_Name"].ToString());
                            var modelId = await modelCmd.ExecuteScalarAsync();
                            if (modelId != null && modelId != DBNull.Value)
                            {
                                vehicleDetails.Model_ID = Convert.ToDecimal(modelId);
                            }
                        }

                        // Get fuel ID based on Fuel_Type
                        if (reader["Fuel"].ToString()?.Length > 0)
                        {
                            using var fuelCmd = new SqlCommand("SELECT Fuel_ID FROM MM_Fuel_Master WHERE Fuel_Name = @Fuel_Type", connection);
                            fuelCmd.Parameters.AddWithValue("@Fuel_Type", reader["Fuel"].ToString());
                            var fuelId = await fuelCmd.ExecuteScalarAsync();
                            if (fuelId != null && fuelId != DBNull.Value)
                            {
                                vehicleDetails.Fuel_ID = Convert.ToDecimal(fuelId);
                            }
                        }

                        if (reader["Drive_Type"].ToString()?.Length > 0)
                        {
                            using var driveCmd = new SqlCommand("SELECT Drive_ID FROM MM_Drive_Master WHERE Drive_Name = @Drive_Type", connection);
                            driveCmd.Parameters.AddWithValue("@Drive_Type", reader["Drive_Type"].ToString());
                            var driveId = await driveCmd.ExecuteScalarAsync();
                            if (driveId != null && driveId != DBNull.Value)
                            {
                                vehicleDetails.Drive_ID = Convert.ToDecimal(driveId);
                            }
                        }

                        if(reader["Country"].ToString()?.Length > 0)
                        {
                            using var countryCmd = new SqlCommand("SELECT Country_ID FROM MM_Country_Master WHERE Country_Name = @Country_Name", connection);
                            countryCmd.Parameters.AddWithValue("@Country_Name", reader["Country"].ToString());
                            var countryId = await countryCmd.ExecuteScalarAsync();
                            if (countryId != null && countryId != DBNull.Value)
                            {
                                vehicleDetails.Country_ID = Convert.ToDecimal(countryId);
                            }
                        }
                    }
                }

                return vehicleDetails;
            }
            catch (Exception)
            {
                throw;
            }
        }

        // SaveVehicle — upsert by VIN_No, returns saved row with Vehicle_ID
        public async Task<MM_Vehicle_DetailsDto> SaveVehicle(MM_Vehicle_DetailsDto dto)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            // Check existence by VIN
            decimal existingId = 0;
            using (var checkCmd = new SqlCommand("SELECT Vehicle_ID FROM MM_Vehicle_Details WHERE VIN_No = @VIN_No", connection))
            {
                checkCmd.Parameters.AddWithValue("@VIN_No", dto.VIN_No);
                var result = await checkCmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                    existingId = Convert.ToDecimal(result);
            }

            if (existingId > 0)
            {
                const string updateQuery = @"
                    UPDATE MM_Vehicle_Details SET
                        Body_Serial_No = @Body_Serial_No,
                        Model_ID = @Model_ID,
                        Drive_ID = @Drive_ID,
                        Fuel_ID = @Fuel_ID,
                        Country_ID = @Country_ID,
                        Build_Phase_ID = @Build_Phase_ID,
                        Engine_No = @Engine_No,
                        Roll_Down_Date = @Roll_Down_Date,
                        CAI_OK_Date = @CAI_OK_Date,
                        Model_Code = @Model_Code,
                        Model_Desc = @Model_Desc,
                        Variant = @Variant,
                        Colour_Code = @Colour_Code,
                        Colour_Desc = @Colour_Desc,
                        Is_Edited = 1,
                        Updated_Host = @Updated_Host,
                        Updated_User_ID = @Updated_User_ID,
                        Updated_Date = @Updated_Date,
                        Plant_Code = @Plant_Code
                    WHERE Vehicle_ID = @Vehicle_ID";

                using var cmd = new SqlCommand(updateQuery, connection);
                cmd.Parameters.AddWithValue("@Vehicle_ID", existingId);
                AddVehicleParams(cmd, dto);
                cmd.Parameters.AddWithValue("@Updated_Host", (object?)dto.Updated_Host ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Updated_User_ID", (object?)dto.Updated_User_ID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Updated_Date", DateTime.Now);
                await cmd.ExecuteNonQueryAsync();
                dto.Vehicle_ID = existingId;
            }
            else
            {
                const string insertQuery = @"
                    INSERT INTO MM_Vehicle_Details
                        (VIN_No, Body_Serial_No, Model_ID, Drive_ID, Fuel_ID, Country_ID, Build_Phase_ID,
                         Engine_No, Roll_Down_Date, CAI_OK_Date, Model_Code, Model_Desc, Variant,
                         Colour_Code, Colour_Desc, Inserted_Host, Inserted_User_ID, Inserted_Date, Plant_Code)
                    OUTPUT INSERTED.Vehicle_ID
                    VALUES
                        (@VIN_No, @Body_Serial_No, @Model_ID, @Drive_ID, @Fuel_ID, @Country_ID, @Build_Phase_ID,
                         @Engine_No, @Roll_Down_Date, @CAI_OK_Date, @Model_Code, @Model_Desc, @Variant,
                         @Colour_Code, @Colour_Desc, @Inserted_Host, @Inserted_User_ID, @Inserted_Date, @Plant_Code)";

                using var cmd = new SqlCommand(insertQuery, connection);
                cmd.Parameters.AddWithValue("@VIN_No", dto.VIN_No);
                AddVehicleParams(cmd, dto);
                cmd.Parameters.AddWithValue("@Inserted_Host", (object?)dto.Inserted_Host ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Inserted_User_ID", (object?)dto.Inserted_User_ID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Inserted_Date", DateTime.Now);

                var newId = await cmd.ExecuteScalarAsync();
                dto.Vehicle_ID = newId != null ? Convert.ToDecimal(newId) : 0;
            }

            return dto;
        }

        private static void AddVehicleParams(SqlCommand cmd, MM_Vehicle_DetailsDto dto)
        {
            cmd.Parameters.AddWithValue("@Body_Serial_No", (object?)dto.Body_Serial_No ?? string.Empty);
            cmd.Parameters.AddWithValue("@Model_ID", dto.Model_ID);
            cmd.Parameters.AddWithValue("@Drive_ID", dto.Drive_ID);
            cmd.Parameters.AddWithValue("@Fuel_ID", dto.Fuel_ID);
            cmd.Parameters.AddWithValue("@Country_ID", dto.Country_ID);
            cmd.Parameters.AddWithValue("@Build_Phase_ID", dto.Build_Phase_ID);
            cmd.Parameters.AddWithValue("@Engine_No", (object?)dto.Engine_No ?? string.Empty);
            cmd.Parameters.AddWithValue("@Roll_Down_Date", (object?)dto.Roll_Down_Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CAI_OK_Date", (object?)dto.CAI_OK_Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Model_Code", (object?)dto.Model_Code ?? string.Empty);
            cmd.Parameters.AddWithValue("@Model_Desc", (object?)dto.Model_Desc ?? string.Empty);
            cmd.Parameters.AddWithValue("@Variant", (object?)dto.Variant ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Colour_Code", (object?)dto.Colour_Code ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Colour_Desc", (object?)dto.Colour_Desc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Plant_Code", (object?)dto.Plant_Code ?? DBNull.Value);
        }
    }
}
