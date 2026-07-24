namespace API_LSP.Models.DTOs
{
    /// <summary>Filter payload for the Vehicle Tracking report.</summary>
    public class VehicleTrackingReportRequest
    {
        public decimal Model_ID { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    /// <summary>
    /// One row per vehicle for the Vehicle Tracking report. Master details plus
    /// each audit stage's date + status (OK / NOK / Pending). Column order and
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
