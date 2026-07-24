namespace API_LSP.Models.DTOs
{
    public class MM_Vehicle_DetailsDto
    {
        public decimal Vehicle_ID { get; set; }
        public string VIN_No { get; set; } = string.Empty;
        public string Body_Serial_No { get; set; } = string.Empty;
        public decimal Model_ID { get; set; }
        public decimal Drive_ID { get; set; }
        public decimal Fuel_ID { get; set; }
        public decimal Country_ID { get; set; }
        public decimal Build_Phase_ID { get; set; }
        public string Engine_No { get; set; } = string.Empty;
        public DateTime? Roll_Down_Date { get; set; }
        public DateTime? CAI_OK_Date { get; set; }
        public string Model_Code { get; set; } = string.Empty;
        public string Model_Desc { get; set; } = string.Empty;
        public string? Variant { get; set; } = string.Empty;
        public string? Colour_Code { get; set; } = string.Empty;
        public string? Colour_Desc { get; set; } = string.Empty;
        public bool? Is_Transfered { get; set; }
        public bool? Is_Purgeable { get; set; }
        public bool? Is_Edited { get; set; }
        public bool? Is_Deleted { get; set; }
        public string? Inserted_Host { get; set; } = string.Empty;
        public decimal? Inserted_User_ID { get; set; }
        public DateTime? Inserted_Date { get; set; }
        public string? Updated_Host { get; set; } = string.Empty;
        public decimal? Updated_User_ID { get; set; }
        public DateTime? Updated_Date { get; set; }
        public string? Plant_Code { get; set; } = string.Empty;
    }
}