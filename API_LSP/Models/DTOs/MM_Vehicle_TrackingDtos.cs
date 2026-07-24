namespace API_LSP.Models.DTOs
{
    public class VehicleDefectEntryDto
    {
        public decimal? Vehicle_Area_ID { get; set; }
        public decimal Part_ID { get; set; }
        public decimal Defect_ID { get; set; }
        public decimal Location_ID { get; set; }
        public decimal? Severity_ID { get; set; }
        public decimal? Attribution_ID { get; set; }
        public decimal? Defect_Type_ID { get; set; }
        public string? Remark { get; set; }

        /// <summary>Image click-point (Paint Verification only) — the area image
        /// clicked and the normalized 0..1 coordinates of the tap. Null for all
        /// other stages. Powers the future defect heat-map report.</summary>
        public decimal? Area_Image_ID { get; set; }
        public decimal? Image_X { get; set; }
        public decimal? Image_Y { get; set; }
    }

    public class StageSaveDto
    {
        public decimal Vehicle_ID { get; set; }
        public decimal Audit_Area_ID { get; set; }
        public string Auditor_Name { get; set; } = string.Empty;
        public bool Is_No_Defect { get; set; }
        public string? Documents { get; set; }
        public string? Inserted_Host { get; set; }
        public decimal? Inserted_User_ID { get; set; }
        public string? Plant_Code { get; set; }
        public List<VehicleDefectEntryDto> Concerns { get; set; } = new();
    }

    public class StageSaveResultDto
    {
        public decimal Track_Log_ID { get; set; }
        public int Concerns_Saved { get; set; }
    }

    public class AddConcernResultDto
    {
        public decimal Track_Log_ID { get; set; }
        public decimal Defect_Entry_ID { get; set; }
    }

    public class UpdateAuditorDto
    {
        public decimal Vehicle_ID { get; set; }
        public decimal Audit_Area_ID { get; set; }
        public string Auditor_Name { get; set; } = string.Empty;
        public string? Inserted_Host { get; set; }
        public decimal? Inserted_User_ID { get; set; }
    }

    /// <summary>
    /// Auto-save payload for a stage's Documents list alone — used by
    /// defect-entry stages that also carry attachments (Paint Verification).
    /// Upserts the Track_Logs row without touching Is_No_Defect.
    /// </summary>
    public class UpdateStageDocumentsDto
    {
        public decimal Vehicle_ID { get; set; }
        public decimal Audit_Area_ID { get; set; }
        public List<string> Documents { get; set; } = new();
        public string? Inserted_Host { get; set; }
        public decimal? Inserted_User_ID { get; set; }
        public string? Plant_Code { get; set; }
    }

    /// <summary>
    /// Used by OK/NOK-style stages (DTC, ECOS, VMS) where a stage is just a
    /// status + auditor + optional supporting files; no concerns table rows.
    /// </summary>
    public class StageStatusSaveDto
    {
        public decimal Vehicle_ID { get; set; }
        public decimal Audit_Area_ID { get; set; }
        public string Auditor_Name { get; set; } = string.Empty;

        /// <summary>True = OK (no defect), False = Not OK.</summary>
        public bool Is_No_Defect { get; set; }

        /// <summary>Saved file names (with GUID prefix), persisted as a JSON
        /// array string into MM_Vehicle_Track_Logs.Documents.</summary>
        public List<string>? Documents { get; set; }

        public string? Inserted_Host { get; set; }
        public decimal? Inserted_User_ID { get; set; }
        public string? Plant_Code { get; set; }
    }

    public class DeleteStageFileDto
    {
        public string Folder { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Upsert payload for MM_Vehicle_Defect_Rework — one call per row when the
    /// operator toggles the checkbox or changes the Rework_By dropdown.
    /// </summary>
    public class ReworkUpsertDto
    {
        public decimal Defect_Entry_ID { get; set; }
        public bool? Rework_Done { get; set; }
        public string? Rework_By { get; set; }
        public string? Remark { get; set; }
        public string? Inserted_Host { get; set; }
        public decimal? Inserted_User_ID { get; set; }
        public string? Plant_Code { get; set; }
    }

    public class ReworkUpsertResultDto
    {
        public decimal Rework_ID { get; set; }
    }

    /// <summary>
    /// Upsert one row in MM_Vehicle_Stage_Rework keyed on (Vehicle_ID, Audit_Area_ID).
    /// Used when a status stage (DTC / ECOS / VMS / Paint) was marked Not OK
    /// and later reworked. Rework_Done = true + rework fields = passed for RFD.
    /// </summary>
    public class StageReworkUpsertDto
    {
        public decimal Vehicle_ID { get; set; }
        public decimal Audit_Area_ID { get; set; }
        public bool Rework_Done { get; set; }
        public string? Rework_By { get; set; }
        public string? Remark { get; set; }
        /// <summary>JSON array of file names (same pattern as Track_Logs.Documents).</summary>
        public List<string>? Documents { get; set; }
        public string? Inserted_Host { get; set; }
        public decimal? Inserted_User_ID { get; set; }
        public string? Plant_Code { get; set; }
    }

    public class StageReworkUpsertResultDto
    {
        public decimal Stage_Rework_ID { get; set; }
    }

    /// <summary>
    /// Per-required-stage block of the RFD readiness payload.
    /// </summary>
    public class RfdStageCheckDto
    {
        public bool Completed { get; set; }
        public string AuditorName { get; set; } = "";
        public DateTime? LogDate { get; set; }
    }

    /// <summary>
    /// Same as RfdStageCheckDto but also carries the OK/NOK status (DTC).
    /// IsOk: true = OK, false = Not OK, null = stage not saved.
    /// StageReworkDone / Rework_By / Rework_Remark surface the stage-level rework
    /// state so the RFD readiness panel can show "reworked" instead of a plain
    /// red ✗ when a NOK stage has been resolved.
    /// </summary>
    public class RfdStatusCheckDto : RfdStageCheckDto
    {
        public bool? IsOk { get; set; }
        public bool StageReworkDone { get; set; }
        public string? StageReworkBy { get; set; }
        public string? StageReworkRemark { get; set; }
        public DateTime? StageReworkDate { get; set; }
    }

    public class RfdReworkCheckDto
    {
        public int TotalConcerns { get; set; }
        public int DoneCount { get; set; }
        public int PendingCount { get; set; }
    }

    public class RfdReadinessDto
    {
        public bool Ready { get; set; }
        public RfdStageCheckDto RoadTest { get; set; } = new();
        public RfdStageCheckDto Underbody { get; set; } = new();
        public RfdStageCheckDto LspAudit { get; set; } = new();
        public RfdStatusCheckDto Dtc { get; set; } = new();
        /// <summary>ECOS status — informational only, does NOT gate dispatch.</summary>
        public RfdStatusCheckDto Ecos { get; set; } = new();
        /// <summary>VMS status — required to be OK before dispatch.</summary>
        public RfdStatusCheckDto Vms { get; set; } = new();
        /// <summary>Paint Verification status — required to be OK before dispatch.</summary>
        public RfdStatusCheckDto Paint { get; set; } = new();
        public RfdReworkCheckDto Rework { get; set; } = new();
        /// <summary>Concerns whose rework is not yet done — shown inline on the
        /// RFD screen so the operator sees exactly what's blocking dispatch.</summary>
        public List<object> PendingConcerns { get; set; } = new();
        /// <summary>Concerns that have been reworked — shown below the readiness
        /// check so the operator sees who did each rework.</summary>
        public List<object> ReworkedConcerns { get; set; } = new();
    }

    public class SaveRfdDto
    {
        public decimal Vehicle_ID { get; set; }
        public string Operator_Name { get; set; } = string.Empty;
        public string? Inserted_Host { get; set; }
        public decimal? Inserted_User_ID { get; set; }
        public string? Plant_Code { get; set; }
    }

    /// <summary>
    /// Auto-save payload for Road Test's Start_KM / End_KM inputs.
    /// </summary>
    public class RoadTestKmDto
    {
        public decimal Vehicle_ID { get; set; }
        public decimal? Start_KM { get; set; }
        public decimal? End_KM { get; set; }
        public string? Inserted_Host { get; set; }
        public decimal? Inserted_User_ID { get; set; }
        public string? Plant_Code { get; set; }
    }
}
