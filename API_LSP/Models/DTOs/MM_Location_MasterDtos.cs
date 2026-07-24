namespace API_LSP.Models.DTOs
{
    public class LocationMasterCreateDto
    {
        public decimal Location_ID { get; set; }
        public string Location_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public string? Inserted_Host { get; set; } = string.Empty;
        public decimal? Inserted_User_ID { get; set; }
        public DateTime? Inserted_Date { get; set; }
        public string? Plant_Code { get; set; } = string.Empty;
    }
    public class LocationMasterUpdateDto
    {
        public decimal Location_ID { get; set; }
        public string Location_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public string Updated_Host { get; set; } = string.Empty;
        public decimal Updated_User_ID { get; set; }
        public Nullable<System.DateTime> Inserted_Date { get; set; }
        public string Plant_Code { get; set; } = string.Empty;
    }
    public class LocationMasterListDto
    {
        public decimal Location_ID { get; set; }
        public string Location_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public string Inserted_Host { get; set; } = string.Empty;
        public decimal Inserted_User_ID { get; set; }
        public Nullable<System.DateTime> Inserted_Date { get; set; }
        public string Plant_Code { get; set; } = string.Empty;
    }
}
