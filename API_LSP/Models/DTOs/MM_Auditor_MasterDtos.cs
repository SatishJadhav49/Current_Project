namespace API_LSP.Models.DTOs
{
    public class AuditorMasterCreateDto
    {
        public decimal Audit_Area_ID { get; set; }
        public decimal Auditor_ID { get; set; }
        public string Auditor_Name { get; set; } = string.Empty;
        public string? Inserted_Host { get; set; } = string.Empty;
        public decimal? Inserted_User_ID { get; set; }
        public DateTime? Inserted_Date { get; set; }
        public string? Plant_Code { get; set; } = string.Empty;
    }
    public class AuditorMasterUpdateDto
    {
        public decimal Audit_Area_ID { get; set; }
        public decimal Auditor_ID { get; set; }
        public string Auditor_Name { get; set; } = string.Empty;
        public string Updated_Host { get; set; } = string.Empty;
        public decimal Updated_User_ID { get; set; }
        public Nullable<System.DateTime> Inserted_Date { get; set; }
        public string Plant_Code { get; set; } = string.Empty;
    }
    public class AuditorMasterListDto
    {
        public decimal Audit_Area_ID { get; set; }
        public string Audit_Area_Name { get; set; } = string.Empty;
        public decimal Auditor_ID { get; set; }
        public string Auditor_Name { get; set; } = string.Empty;
        public string Inserted_Host { get; set; } = string.Empty;
        public decimal Inserted_User_ID { get; set; }
        public Nullable<System.DateTime> Inserted_Date { get; set; }
        public string Plant_Code { get; set; } = string.Empty;
    }
}
