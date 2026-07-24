namespace API_LSP.Models.DTOs
{
    public class PartMasterCreateDto
    {
        public decimal Part_ID { get; set; }
        public string Part_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public string? Inserted_Host { get; set; } = string.Empty;
        public decimal? Inserted_User_ID { get; set; }
        public DateTime? Inserted_Date { get; set; }
        public string? Plant_Code { get; set; } = string.Empty;
    }
    public class PartMasterUpdateDto
    {
        public decimal Part_ID { get; set; }
        public string Part_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public string Updated_Host { get; set; } = string.Empty;
        public decimal Updated_User_ID { get; set; }
        public Nullable<System.DateTime> Inserted_Date { get; set; }
        public string Plant_Code { get; set; } = string.Empty;
    }
    public class PartMasterListDto
    {
        public decimal Part_ID { get; set; }
        public string Part_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        public string Inserted_Host { get; set; } = string.Empty;
        public decimal Inserted_User_ID { get; set; }
        public Nullable<System.DateTime> Inserted_Date { get; set; }
        public string Plant_Code { get; set; } = string.Empty;
    }
    public class PartMasterUploadExcelDTO
    {
        public string Part_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
    }

    public class PartExcelExportDataDto
    {
        public decimal Part_ID { get; set; }
        public string Part_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
        
    }
}
