using API_LSP.Models;
using API_LSP.Models.DTOs;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Net;
using System.Net.Mail;

namespace API_LSP.Data
{
    public class MM_CommonDataService
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MM_CommonDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }


        public async Task<List<MM_ShopGetDTO>> GetShopList()
        {
            try
            {
                const string query = @"
                    SELECT 
                    Shop_ID,
                    Shop_Name
                    FROM MM_Shop";

                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                var shopList = new List<MM_ShopGetDTO>();

                while (await reader.ReadAsync())
                {
                    shopList.Add(new MM_ShopGetDTO
                    {
                        Shop_ID = reader.GetDecimal(reader.GetOrdinal("Shop_ID")),
                        Shop_Name = reader.GetString(reader.GetOrdinal("Shop_Name"))
                    });
                }
                return shopList;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<List<MM_ModelListDto>> GetModelList(int[] shopIds)
        {
            try
            {
                if (shopIds == null || shopIds.Length == 0)
                {
                    return new List<MM_ModelListDto>();
                }

                var shopIdsString = string.Join(",", shopIds);
                var query = $@"
                    SELECT 
                    Model_ID,
                    Shop_ID,
                    Model_Name
                    FROM MM_Model
                    WHERE Shop_ID IN ({shopIdsString})";

                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                var modelList = new List<MM_ModelListDto>();

                while (await reader.ReadAsync())
                {
                    modelList.Add(new MM_ModelListDto
                    {
                        Model_ID = reader.GetDecimal(reader.GetOrdinal("Model_ID")),
                        Model_Name = reader.GetString(reader.GetOrdinal("Model_Name"))
                    });
                }
                return modelList;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<List<MM_Audit_Area_Master>> GetAuditAreaList()
        {
            try
            {
                const string query = @"
                    SELECT 
                    Audit_Area_ID,
                    Audit_Area_Name
                    FROM MM_Audit_Area_Master order by SORTORDER";

                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                var auditAreaList = new List<MM_Audit_Area_Master>();

                while (await reader.ReadAsync())
                {
                    auditAreaList.Add(new MM_Audit_Area_Master
                    {
                        Audit_Area_ID = reader.GetDecimal(reader.GetOrdinal("Audit_Area_ID")),
                        Audit_Area_Name = reader.GetString(reader.GetOrdinal("Audit_Area_Name"))
                    });
                }
                return auditAreaList;
            }
            catch (Exception)
            {
                throw;
            }
        }

        // GetCountryList
        public async Task<List<MM_Country_Master>> GetCountryList()
        {
            try
            {
                const string query = @"
                    SELECT 
                    Country_ID,
                    Country_Name
                    FROM MM_Country_Master order by SORTORDER";

                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                var countryList = new List<MM_Country_Master>();

                while (await reader.ReadAsync())
                {
                    countryList.Add(new MM_Country_Master
                    {
                        Country_ID = reader.GetDecimal(reader.GetOrdinal("Country_ID")),
                        Country_Name = reader.GetString(reader.GetOrdinal("Country_Name"))
                    });
                }
                return countryList;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<List<MM_Build_Phase_Master>> GetBuildPhaseList()
        {
            try
            {
                const string query = @"
                    SELECT
                    Build_Phase_ID,
                    Build_Phase_Name
                    FROM MM_Build_Phase_Master order by SORTORDER";

                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                var list = new List<MM_Build_Phase_Master>();

                while (await reader.ReadAsync())
                {
                    list.Add(new MM_Build_Phase_Master
                    {
                        Build_Phase_ID = reader.GetDecimal(reader.GetOrdinal("Build_Phase_ID")),
                        Build_Phase_Name = reader.GetString(reader.GetOrdinal("Build_Phase_Name"))
                    });
                }
                return list;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<List<MM_Drive_Master>> GetDriveList()
        {
            try
            {
                const string query = @"
                    SELECT
                    Drive_ID,
                    Drive_Name
                    FROM MM_Drive_Master order by SORTORDER";

                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                var list = new List<MM_Drive_Master>();

                while (await reader.ReadAsync())
                {
                    list.Add(new MM_Drive_Master
                    {
                        Drive_ID = reader.GetDecimal(reader.GetOrdinal("Drive_ID")),
                        Drive_Name = reader.GetString(reader.GetOrdinal("Drive_Name"))
                    });
                }
                return list;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<List<MM_Fuel_Master>> GetFuelList()
        {
            try
            {
                const string query = @"
                    SELECT
                    Fuel_ID,
                    Fuel_Name
                    FROM MM_Fuel_Master order by SORTORDER";

                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                var list = new List<MM_Fuel_Master>();

                while (await reader.ReadAsync())
                {
                    list.Add(new MM_Fuel_Master
                    {
                        Fuel_ID = reader.GetDecimal(reader.GetOrdinal("Fuel_ID")),
                        Fuel_Name = reader.GetString(reader.GetOrdinal("Fuel_Name"))
                    });
                }
                return list;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<List<MM_Plant_Master>> GetPlantList()
        {
            try
            {
                const string query = @"
                    SELECT
                    Plant_ID,
                    ISNULL(Plant_Name, '') AS Plant_Name,
                    ISNULL(Plant_Code, '') AS Plant_Code
                    FROM MM_Plant
                    ORDER BY Plant_Name";

                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                var list = new List<MM_Plant_Master>();

                while (await reader.ReadAsync())
                {
                    list.Add(new MM_Plant_Master
                    {
                        Plant_ID = reader.GetDecimal(reader.GetOrdinal("Plant_ID")),
                        Plant_Name = reader.GetString(reader.GetOrdinal("Plant_Name")),
                        Plant_Code = reader.GetString(reader.GetOrdinal("Plant_Code"))
                    });
                }
                return list;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<List<MM_Vehicle_Area_Master>> GetVehicleAreaList()
        {
            try
            {
                const string query = @"
                    SELECT
                    Vehicle_Area_ID,
                    Vehicle_Area_Name
                    FROM MM_Vehicle_Area_Master
                    WHERE Is_Deleted IS NULL OR Is_Deleted = 0
                    ORDER BY SORTORDER";

                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                var list = new List<MM_Vehicle_Area_Master>();

                while (await reader.ReadAsync())
                {
                    list.Add(new MM_Vehicle_Area_Master
                    {
                        Vehicle_Area_ID = reader.GetDecimal(reader.GetOrdinal("Vehicle_Area_ID")),
                        Vehicle_Area_Name = reader.GetString(reader.GetOrdinal("Vehicle_Area_Name"))
                    });
                }
                return list;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<List<MM_Severity_Master>> GetSeverityList()
        {
            try
            {
                const string query = @"
                    SELECT
                    Severity_ID,
                    Severity_Code,
                    Severity_Name
                    FROM MM_Severity_Master
                    WHERE Is_Deleted IS NULL OR Is_Deleted = 0
                    ORDER BY SORTORDER";

                using var connection = await _connectionFactory.CreateConnectionAsync();
                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                var list = new List<MM_Severity_Master>();

                while (await reader.ReadAsync())
                {
                    list.Add(new MM_Severity_Master
                    {
                        Severity_ID = reader.GetDecimal(reader.GetOrdinal("Severity_ID")),
                        Severity_Code = reader.GetString(reader.GetOrdinal("Severity_Code")),
                        Severity_Name = reader.GetString(reader.GetOrdinal("Severity_Name"))
                    });
                }
                return list;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task SendTestMail()
        {
            // Teams channel email address
            string teamsChannelEmail = "d636d9bf.mahindraonline.onmicrosoft.com@apac.teams.ms";

            // SMTP configuration (from provided details)
            string smtpHost = "10.218.124.232";
            int smtpPort = 25;
            string smtpUser = "drona@mahindra.com";
            string smtpPass = "";
            bool enableSsl = false;
            string fromEmail = "LSP@mahindra.com";
            string fromName = "LSP System";

            // Create the email message
            MailMessage mail = new MailMessage();
            mail.From = new MailAddress(fromEmail, fromName);
            mail.To.Add(teamsChannelEmail);
            mail.To.Add("50005817@mahindra.com");
            mail.Subject = "Test Message to Teams Channel";
            mail.IsBodyHtml = true;
            mail.Body = $@"
                Hello Team, this is a test message sent via SMTP from .NET.<br/><br/>
                SMTP Settings:<br/>
                
            ";

            using (var smtpClient = new SmtpClient(smtpHost, smtpPort))
            {
                smtpClient.Credentials = new NetworkCredential(smtpUser, smtpPass);
                smtpClient.EnableSsl = enableSsl;

                try
                {
                    await smtpClient.SendMailAsync(mail);
                    Console.WriteLine("Email sent successfully to Teams channel.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error sending email: " + ex.Message);
                }
            }
        }

    }

}
