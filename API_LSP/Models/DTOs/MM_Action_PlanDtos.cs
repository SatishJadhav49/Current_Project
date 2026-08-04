namespace API_LSP.Models.DTOs
{
    /// <summary>One Problem row (Part+Defect) for an attribution + severity, with tracking metrics.</summary>
    public class ActionPlanProblemDto
    {
        public decimal Part_ID { get; set; }
        public string Part_Name { get; set; } = string.Empty;
        public decimal Defect_ID { get; set; }
        public string Defect_Name { get; set; } = string.Empty;
        public decimal Attribution_ID { get; set; }
        public string Attribution_Name { get; set; } = string.Empty;
        public decimal Severity_ID { get; set; }            // 0 = none
        public string Severity_Code { get; set; } = string.Empty;

        public int Frequency { get; set; }
        public DateTime? Cutoff_Date { get; set; }          // latest PCA target date
        public int Concerns_After_Cutoff { get; set; }
        public string Status { get; set; } = "R";           // computed R / Y / G
        public bool Has_Plan { get; set; }
        public bool Is_Submitted { get; set; }
        public decimal Action_Plan_ID { get; set; }
    }

    // ── Child rows ──
    public class ActionPlanRootCauseDto
    {
        public decimal Root_Cause_ID { get; set; }
        public string Root_Cause { get; set; } = string.Empty;
    }

    public class ActionPlanIcaDto
    {
        public decimal ICA_ID { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Responsibility { get; set; } = string.Empty;
        public DateTime? ICA_Date { get; set; }
    }

    public class ActionPlanPcaDto
    {
        public decimal PCA_ID { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Responsibility { get; set; } = string.Empty;
        public DateTime? Target_Date { get; set; }
        public string Status { get; set; } = "R";
    }

    /// <summary>Full plan detail (existing rows are locked on the client once submitted).</summary>
    public class ActionPlanDetailDto
    {
        public decimal Action_Plan_ID { get; set; }
        public decimal Part_ID { get; set; }
        public decimal Defect_ID { get; set; }
        public decimal Attribution_ID { get; set; }
        public decimal Severity_ID { get; set; }
        public bool Is_Submitted { get; set; }
        public List<ActionPlanRootCauseDto> RootCauses { get; set; } = new();
        public List<ActionPlanIcaDto> Icas { get; set; } = new();
        public List<ActionPlanPcaDto> Pcas { get; set; } = new();
    }

    /// <summary>Submit/append payload. On first submit all three sections are required;
    /// on a later append the sent rows are simply added (existing rows stay).</summary>
    public class ActionPlanSubmitDto
    {
        public decimal Part_ID { get; set; }
        public decimal Defect_ID { get; set; }
        public decimal Attribution_ID { get; set; }
        public decimal Severity_ID { get; set; }
        public List<ActionPlanRootCauseDto> RootCauses { get; set; } = new();
        public List<ActionPlanIcaDto> Icas { get; set; } = new();
        public List<ActionPlanPcaDto> Pcas { get; set; } = new();
        public string? Inserted_Host { get; set; }
        public decimal? Inserted_User_ID { get; set; }
        public string? Plant_Code { get; set; }
    }
}
