namespace API_LSP.Models.DTOs
{
    public class DefectMasterCreateDto
    {
        public decimal Defect_ID { get; set; }
        public string Defect_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public string? Inserted_Host { get; set; } = string.Empty;
        public decimal? Inserted_User_ID { get; set; }
        public DateTime? Inserted_Date { get; set; }
        public string? Plant_Code { get; set; } = string.Empty;
    }
    public class DefectMasterUpdateDto
    {
        public decimal Defect_ID { get; set; }
        public string Defect_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public string Updated_Host { get; set; } = string.Empty;
        public decimal Updated_User_ID { get; set; }
        public Nullable<System.DateTime> Inserted_Date { get; set; }
        public string Plant_Code { get; set; } = string.Empty;
    }
    public class DefectMasterListDto
    {
        public decimal Defect_ID { get; set; }
        public string Defect_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public string Inserted_Host { get; set; } = string.Empty;
        public decimal Inserted_User_ID { get; set; }
        public Nullable<System.DateTime> Inserted_Date { get; set; }
        public string Plant_Code { get; set; } = string.Empty;
    }
    public class DefectMasterUploadExcelDTO
    {
        public string Defect_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
    }

    public class DefectExcelExportDataDto
    {
        public decimal Defect_ID { get; set; }
        public string Defect_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        
    }
}
