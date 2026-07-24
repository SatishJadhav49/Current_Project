namespace API_LSP.Models.DTOs
{
    /// <summary>
    /// Request payload to import shower-test data for a given vehicle.
    /// </summary>
    public class ShowerImportRequestDto
    {
        public decimal Vehicle_ID { get; set; }
        public string VIN_No { get; set; } = string.Empty;
        public string? Inserted_Host { get; set; }
        public decimal? Inserted_User_ID { get; set; }
        public string? Plant_Code { get; set; }
    }

    /// <summary>
    /// One raw row returned by SP_Get_Shower_Defects_For_LSP.
    /// </summary>
    public class ShowerSpRowDto
    {
        public string Vin_No { get; set; } = string.Empty;
        public DateTime? Audit_Date { get; set; }
        public string Auditor_Name { get; set; } = string.Empty;
        public string? Shower_Booth { get; set; }
        public string? Cycle_Name { get; set; }
        public bool Is_No_Defect { get; set; }
        public string? Area_Name { get; set; }
        public string? Part_Name { get; set; }
        public string? Defect_Name { get; set; }
        public string? Location_Name { get; set; }
        public string? Severity_Name { get; set; }
        public string? Attribution_Name { get; set; }
        public string? Zone_Name { get; set; }
        public string? Detection_Time { get; set; }
        public string? Remark { get; set; }
    }

    /// <summary>
    /// Describes one source row that we could not import because a required
    /// master lookup (Defect / Vehicle Area / Location / Severity / Attribution)
    /// did not exist in our LSP master tables.
    /// </summary>
    public class ShowerSkippedRowDto
    {
        public string? Part_Name { get; set; }
        public string MissingMaster { get; set; } = string.Empty;
        public string MissingValue { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of an import. When Found is false, no shower data exists for the
    /// VIN in PQ_Dock_Audit — caller should surface a friendly message.
    /// </summary>
    public class ShowerImportResultDto
    {
        public bool Found { get; set; }
        public bool Is_No_Defect { get; set; }
        public string Auditor_Name { get; set; } = string.Empty;
        public DateTime? Audit_Date { get; set; }
        public int Imported { get; set; }
        public List<ShowerSkippedRowDto> Skipped { get; set; } = new();
    }
}
