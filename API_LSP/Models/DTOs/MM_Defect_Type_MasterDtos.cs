namespace API_LSP.Models.DTOs
{
    public class DefectTypeMasterCreateDto
    {
        public decimal Defect_Type_ID { get; set; }
        public string Defect_Type_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public string? Inserted_Host { get; set; } = string.Empty;
        public decimal? Inserted_User_ID { get; set; }
        public DateTime? Inserted_Date { get; set; }
        public string? Plant_Code { get; set; } = string.Empty;
    }
    public class DefectTypeMasterUpdateDto
    {
        public decimal Defect_Type_ID { get; set; }
        public string Defect_Type_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public string Updated_Host { get; set; } = string.Empty;
        public decimal Updated_User_ID { get; set; }
        public Nullable<System.DateTime> Inserted_Date { get; set; }
        public string Plant_Code { get; set; } = string.Empty;
    }
    public class DefectTypeMasterListDto
    {
        public decimal Defect_Type_ID { get; set; }
        public string Defect_Type_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public string Inserted_Host { get; set; } = string.Empty;
        public decimal Inserted_User_ID { get; set; }
        public Nullable<System.DateTime> Inserted_Date { get; set; }
        public string Plant_Code { get; set; } = string.Empty;
    }
  
}
