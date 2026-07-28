namespace API_LSP.Models.DTOs
{
    // ─── Daily Report ───────────────────────────────────────────────────────

    /// <summary>Filter payload for the Daily report — one Model + one date.</summary>
    public class DailyReportRequest
    {
        public decimal Model_ID { get; set; }
        public DateTime ReportDate { get; set; }
    }

    /// <summary>Vehicle-count for one audit stage completed on the report date.</summary>
    public class DailyStageCount
    {
        public decimal Audit_Area_ID { get; set; }
        public string Stage_Name { get; set; } = string.Empty;
        public int Completed_Count { get; set; }
    }

    /// <summary>One defect entered on the report date, with its vehicle + masters.</summary>
    public class DailyDefectRow
    {
        public string VIN_No { get; set; } = string.Empty;
        public string Stage_Name { get; set; } = string.Empty;
        public string Vehicle_Area_Name { get; set; } = string.Empty;
        public string Part_Name { get; set; } = string.Empty;
        public string Defect_Name { get; set; } = string.Empty;
        public string Location_Name { get; set; } = string.Empty;
        public string Severity_Code { get; set; } = string.Empty;
        public string Attribution_Name { get; set; } = string.Empty;
        public string Defect_Type_Name { get; set; } = string.Empty;
        public string Auditor_Name { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public string Rework_Status { get; set; } = "Pending";
        public DateTime? Entered_Date { get; set; }
    }

    /// <summary>
    /// Full Daily report payload: headline counts for the day, a per-stage
    /// completed-today grid, and the list of defects entered that day.
    /// </summary>
    public class DailyReportResponse
    {
        public string Model_Name { get; set; } = string.Empty;
        public DateTime ReportDate { get; set; }
        public int RfdDoneCount { get; set; }
        public int VehiclesActiveCount { get; set; }
        public int TotalDefectsCount { get; set; }
        public List<DailyStageCount> StageCounts { get; set; } = new();
        public List<DailyDefectRow> Defects { get; set; } = new();
    }

    // ─── WIP / Pending Report ───────────────────────────────────────────────

    /// <summary>Filter payload for the WIP report — Model + vehicle-date range.</summary>
    public class WipReportRequest
    {
        public decimal Model_ID { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    /// <summary>Count of WIP vehicles stuck at a given stage (summary tile).</summary>
    public class WipStuckCount
    {
        public string Stage_Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>One pending (not-yet-RFD) vehicle with its progress + ageing.</summary>
    public class WipRow
    {
        public string VIN_No { get; set; } = string.Empty;
        public string Model_Name { get; set; } = string.Empty;
        public DateTime? Vehicle_Date { get; set; }
        public DateTime? Roll_Down_Date { get; set; }
        public int Age_Days { get; set; }
        public string Last_Stage_Name { get; set; } = string.Empty;
        public DateTime? Last_Stage_Date { get; set; }
        public string Stuck_Stage_Name { get; set; } = string.Empty;
        public DateTime? Last_Activity_Date { get; set; }
        public int Idle_Days { get; set; }
        public int Pending_Concerns { get; set; }
    }

    /// <summary>Full WIP payload: totals, stuck-per-stage summary, vehicle rows.</summary>
    public class WipReportResponse
    {
        public string Model_Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalWip { get; set; }
        public List<WipStuckCount> StuckCounts { get; set; } = new();
        public List<WipRow> Rows { get; set; } = new();
    }

    // ─── Stage TAT (Turnaround) Report ──────────────────────────────────────

    /// <summary>Filter payload for the TAT report — Model + vehicle-date range.</summary>
    public class TatReportRequest
    {
        public decimal Model_ID { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    /// <summary>Average/min/max time between two consecutive stages (hours).</summary>
    public class TatTransition
    {
        public string From_Stage { get; set; } = string.Empty;
        public string To_Stage { get; set; } = string.Empty;
        public double Avg_Hours { get; set; }
        public double Min_Hours { get; set; }
        public double Max_Hours { get; set; }
        public int Vehicle_Count { get; set; }
    }

    /// <summary>
    /// TAT payload: per-transition timings plus the headline overall cycle time
    /// (Roll Down Date → Physical RFD Date).
    /// </summary>
    public class TatReportResponse
    {
        public string Model_Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int VehicleCount { get; set; }
        public double? OverallAvgHours { get; set; }
        public double? OverallMinHours { get; set; }
        public double? OverallMaxHours { get; set; }
        public int OverallCount { get; set; }
        public List<TatTransition> Transitions { get; set; } = new();
    }

    // ─── Image Defect Report (Paint) ────────────────────────────────────────

    /// <summary>Filter payload for the image defect report — Model + Area + date range.</summary>
    public class ImageHeatmapRequest
    {
        public decimal Model_ID { get; set; }
        public decimal Vehicle_Area_ID { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    /// <summary>A mapped part region on an image, with its centre for the count badge.</summary>
    public class HeatHotspot
    {
        public decimal Part_ID { get; set; }
        public string Part_Name { get; set; } = string.Empty;
        public string Shape { get; set; } = string.Empty; // normalized rect JSON
        public double CenterX { get; set; }               // 0..1
        public double CenterY { get; set; }               // 0..1
    }

    /// <summary>One area image with its mapped part regions.</summary>
    public class HeatImage
    {
        public decimal Area_Image_ID { get; set; }
        public string Image_Name { get; set; } = string.Empty;
        public string Image_FileName { get; set; } = string.Empty;
        public int DefectCount { get; set; }
        public List<HeatHotspot> Hotspots { get; set; } = new();
    }

    /// <summary>One Paint defect placed on an image, with full detail for the table.</summary>
    public class ImageDefectRow
    {
        public decimal Defect_Entry_ID { get; set; }
        public decimal Area_Image_ID { get; set; }
        public decimal Part_ID { get; set; }
        public string VIN_No { get; set; } = string.Empty;
        public string Part_Name { get; set; } = string.Empty;
        public string Defect_Name { get; set; } = string.Empty;
        public string Location_Name { get; set; } = string.Empty;
        public string Severity_Code { get; set; } = string.Empty;
        public string Attribution_Name { get; set; } = string.Empty;
        public string Defect_Type_Name { get; set; } = string.Empty;
        public string Auditor_Name { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public string Rework_Status { get; set; } = "Pending";
    }

    /// <summary>
    /// Image defect payload: every area image for the Model+Area with its mapped
    /// part regions, plus the Paint defects recorded over the date range (used
    /// for the per-part count badges and the detail table).
    /// </summary>
    public class ImageHeatmapResponse
    {
        public string Model_Name { get; set; } = string.Empty;
        public string Vehicle_Area_Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDefects { get; set; }
        public int UnplacedCount { get; set; }
        public List<HeatImage> Images { get; set; } = new();
        public List<ImageDefectRow> Defects { get; set; } = new();
    }

    /// <summary>Filter payload for the Vehicle Tracking report.</summary>
    public class VehicleTrackingReportRequest
    {
        public decimal Model_ID { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    /// <summary>
    /// One row per vehicle for the Vehicle Tracking report. Master details plus
    /// each audit stage's date + status (Done / Pending). Column order and
    /// naming mirror LSP_Vehicle_Tracking.xlsx.
    /// </summary>
    public class VehicleTrackingReportRow
    {
        public string VIN_No { get; set; } = string.Empty;
        public string Body_Serial_No { get; set; } = string.Empty;
        public string Model_Name { get; set; } = string.Empty;
        public string Model_Desc { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string Colour_Desc { get; set; } = string.Empty;
        public string Engine_No { get; set; } = string.Empty;
        public string Build_Phase_Name { get; set; } = string.Empty;
        public string Drive_Name { get; set; } = string.Empty;
        public string Fuel_Name { get; set; } = string.Empty;
        public string Country_Name { get; set; } = string.Empty;
        public DateTime? Roll_Down_Date { get; set; }
        public DateTime? CAI_OK_Date { get; set; }

        // Stage date + status pairs (status = OK / NOK / Pending)
        public DateTime? RoadTest_Date { get; set; }
        public string RoadTest_Status { get; set; } = "Pending";
        public DateTime? UnderBody_Date { get; set; }
        public string UnderBody_Status { get; set; } = "Pending";
        public DateTime? Dtc_Date { get; set; }
        public string Dtc_Status { get; set; } = "Pending";
        public DateTime? Shower_Date { get; set; }
        public string Shower_Status { get; set; } = "Pending";
        public DateTime? StaticAudit_Date { get; set; }
        public string StaticAudit_Status { get; set; } = "Pending";
        public DateTime? Vms_Date { get; set; }
        public string Vms_Status { get; set; } = "Pending";
        public DateTime? Paint_Date { get; set; }
        public string Paint_Status { get; set; } = "Pending";

        // Gate stages (presence = OK)
        public string Recheck_Status { get; set; } = "Pending";
        public string Rework_Status { get; set; } = "Pending";
        public DateTime? Rfd_Date { get; set; }
        public string Rfd_Status { get; set; } = "Pending";
    }
}
