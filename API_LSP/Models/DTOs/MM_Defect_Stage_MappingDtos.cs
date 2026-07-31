namespace API_LSP.Models.DTOs
{
    /// <summary>A defect available for a stage (mapped list, or all as fallback).</summary>
    public class StageDefectDto
    {
        public decimal Defect_ID { get; set; }
        public string Defect_Name { get; set; } = string.Empty;
        public int SORTORDER { get; set; }
    }

    /// <summary>Save payload — replaces the full defect set mapped to one stage.</summary>
    public class SaveStageMappingDto
    {
        public decimal Audit_Area_ID { get; set; }
        public List<decimal> Defect_IDs { get; set; } = new();
        public string? Inserted_Host { get; set; }
        public decimal? Inserted_User_ID { get; set; }
        public string? Plant_Code { get; set; }
    }

    /// <summary>Per-stage summary for the mapping master list (mapped count).</summary>
    public class StageMappingSummaryDto
    {
        public decimal Audit_Area_ID { get; set; }
        public string Audit_Area_Name { get; set; } = string.Empty;
        public int Mapped_Count { get; set; }
    }
}
