using API_LSP.Data;
using API_LSP.Models.DTOs;
using API_LSP.Utils;
using Microsoft.AspNetCore.Mvc;

// File-upload helpers
using System.IO;

namespace API_LSP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MM_Vehicle_TrackingController : BaseController
    {
        private readonly MM_Vehicle_TrackingDataService _service;

        /// <summary>
        /// Folders we allow files to be stored under (server controls the
        /// physical location; clients can only choose from this whitelist).
        /// </summary>
        private static readonly HashSet<string> AllowedFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "DTCData", "ECOSData", "VMSData", "PaintData", "StageData", "StageReworkData"
        };

        /// <summary>Shared 400 response when the vehicle is RFD-locked.</summary>
        private IActionResult RfdLockedResponse<T>()
        {
            var locked = _responseHelper.CreateErrorResponse<T>(
                "Locked",
                "Vehicle is already marked Ready For Dispatch — no further changes are allowed.");
            return BadRequest(locked);
        }

        public MM_Vehicle_TrackingController(
            MM_Vehicle_TrackingDataService service,
            IApiResponseHelper responseHelper,
            MM_ErrorLogDataService errorLogService)
            : base(responseHelper, errorLogService)
        {
            _service = service;
        }

        /// <summary>
        /// Save stage with concerns (Is_No_Defect = false expected).
        /// Use this when defects were entered via the defect-entry form.
        /// </summary>
        [HttpPost("SaveStage")]
        public async Task<IActionResult> SaveStage([FromBody] StageSaveDto dto)
        {
            try
            {
                if (dto == null || dto.Vehicle_ID <= 0 || dto.Audit_Area_ID <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<StageSaveResultDto>(
                        "Validation Error",
                        "Vehicle_ID and Audit_Area_ID are required.");
                    return BadRequest(bad);
                }
                if (string.IsNullOrWhiteSpace(dto.Auditor_Name))
                {
                    var bad = _responseHelper.CreateErrorResponse<StageSaveResultDto>(
                        "Validation Error",
                        "Auditor_Name is required.");
                    return BadRequest(bad);
                }
                if (!dto.Is_No_Defect && (dto.Concerns == null || dto.Concerns.Count == 0))
                {
                    var bad = _responseHelper.CreateErrorResponse<StageSaveResultDto>(
                        "Validation Error",
                        "Provide at least one concern or use the No Defect option.");
                    return BadRequest(bad);
                }

                if (await _service.IsVehicleRfdDone(dto.Vehicle_ID))
                    return RfdLockedResponse<StageSaveResultDto>();

                var result = await _service.SaveStage(dto);
                var ok = _responseHelper.CreateSuccessResponse(result, "Success", "Stage saved successfully.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "SaveStage");
            }
        }

        /// <summary>
        /// Mark stage as completed with no defects.
        /// Writes only the Track_Logs row (Is_No_Defect = 1); defects table stays untouched.
        /// </summary>
        [HttpPost("SaveNoDefect")]
        public async Task<IActionResult> SaveNoDefect([FromBody] StageSaveDto dto)
        {
            try
            {
                if (dto == null || dto.Vehicle_ID <= 0 || dto.Audit_Area_ID <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<StageSaveResultDto>(
                        "Validation Error",
                        "Vehicle_ID and Audit_Area_ID are required.");
                    return BadRequest(bad);
                }
                if (string.IsNullOrWhiteSpace(dto.Auditor_Name))
                {
                    var bad = _responseHelper.CreateErrorResponse<StageSaveResultDto>(
                        "Validation Error",
                        "Auditor_Name is required.");
                    return BadRequest(bad);
                }

                if (await _service.IsVehicleRfdDone(dto.Vehicle_ID))
                    return RfdLockedResponse<StageSaveResultDto>();

                dto.Is_No_Defect = true;
                dto.Concerns = new List<VehicleDefectEntryDto>();

                var result = await _service.SaveStage(dto);
                var ok = _responseHelper.CreateSuccessResponse(result, "Success", "Stage marked as No Defect.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "SaveNoDefect");
            }
        }

        /// <summary>
        /// Append a single concern (save-as-you-go pattern from the form's
        /// "+ Add Concern" button).
        /// </summary>
        [HttpPost("AddConcern")]
        public async Task<IActionResult> AddConcern([FromBody] StageSaveDto dto)
        {
            try
            {
                if (dto == null || dto.Vehicle_ID <= 0 || dto.Audit_Area_ID <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<AddConcernResultDto>(
                        "Validation Error",
                        "Vehicle_ID and Audit_Area_ID are required.");
                    return BadRequest(bad);
                }
                if (string.IsNullOrWhiteSpace(dto.Auditor_Name))
                {
                    var bad = _responseHelper.CreateErrorResponse<AddConcernResultDto>(
                        "Validation Error",
                        "Auditor_Name is required.");
                    return BadRequest(bad);
                }
                if (dto.Concerns == null || dto.Concerns.Count != 1)
                {
                    var bad = _responseHelper.CreateErrorResponse<AddConcernResultDto>(
                        "Validation Error",
                        "AddConcern expects exactly one concern in the payload.");
                    return BadRequest(bad);
                }

                if (await _service.IsVehicleRfdDone(dto.Vehicle_ID))
                    return RfdLockedResponse<AddConcernResultDto>();

                var result = await _service.AddConcern(dto);
                var ok = _responseHelper.CreateSuccessResponse(result, "Success", "Concern saved.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "AddConcern");
            }
        }

        /// <summary>
        /// Auto-save Road Test's Start_KM / End_KM. Upserts the Track_Logs row
        /// for Audit_Area_ID = 1 — creates with empty Auditor_Name when the
        /// operator hasn't picked a driver yet.
        /// </summary>
        [HttpPost("UpdateRoadTestKm")]
        public async Task<IActionResult> UpdateRoadTestKm([FromBody] RoadTestKmDto dto)
        {
            try
            {
                if (dto == null || dto.Vehicle_ID <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<bool>(
                        "Validation Error", "Vehicle_ID is required.");
                    return BadRequest(bad);
                }

                if (await _service.IsVehicleRfdDone(dto.Vehicle_ID))
                    return RfdLockedResponse<bool>();

                var ok = await _service.UpdateRoadTestKm(dto);
                var resp = _responseHelper.CreateSuccessResponse(ok, "Success", "KM saved.");
                return Ok(resp);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "UpdateRoadTestKm");
            }
        }

        /// <summary>
        /// Update only the Auditor_Name on the existing Track_Logs row for a
        /// (Vehicle, Stage). Returns whether a row was updated. If false, the
        /// stage has no Track_Logs row yet — the auditor will be saved with
        /// the first concern.
        /// </summary>
        [HttpPost("UpdateAuditor")]
        public async Task<IActionResult> UpdateAuditor([FromBody] UpdateAuditorDto dto)
        {
            try
            {
                if (dto == null || dto.Vehicle_ID <= 0 || dto.Audit_Area_ID <= 0 ||
                    string.IsNullOrWhiteSpace(dto.Auditor_Name))
                {
                    var bad = _responseHelper.CreateErrorResponse<bool>(
                        "Validation Error",
                        "Vehicle_ID, Audit_Area_ID and Auditor_Name are required.");
                    return BadRequest(bad);
                }

                if (await _service.IsVehicleRfdDone(dto.Vehicle_ID))
                    return RfdLockedResponse<bool>();

                var updated = await _service.UpdateAuditor(
                    dto.Vehicle_ID, dto.Audit_Area_ID, dto.Auditor_Name,
                    dto.Inserted_Host, dto.Inserted_User_ID);

                var ok = _responseHelper.CreateSuccessResponse(updated, "Success",
                    updated ? "Auditor updated." : "No saved stage to update.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "UpdateAuditor");
            }
        }

        /// <summary>
        /// Update an existing concern in place by Defect_Entry_ID.
        /// </summary>
        [HttpPost("UpdateConcern/{defectEntryId}")]
        public async Task<IActionResult> UpdateConcern(decimal defectEntryId, [FromBody] StageSaveDto dto)
        {
            try
            {
                if (defectEntryId <= 0 || dto == null || dto.Vehicle_ID <= 0 || dto.Audit_Area_ID <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<bool>(
                        "Validation Error",
                        "Defect_Entry_ID, Vehicle_ID and Audit_Area_ID are required.");
                    return BadRequest(bad);
                }
                if (dto.Concerns == null || dto.Concerns.Count != 1)
                {
                    var bad = _responseHelper.CreateErrorResponse<bool>(
                        "Validation Error",
                        "UpdateConcern expects exactly one concern in the payload.");
                    return BadRequest(bad);
                }

                // Locked once rework is done — no edits permitted, even by Recheck.
                if (await _service.IsConcernLockedByRework(defectEntryId))
                {
                    var locked = _responseHelper.CreateErrorResponse<bool>(
                        "Locked",
                        "This concern has been reworked and can no longer be edited.");
                    return BadRequest(locked);
                }

                if (await _service.IsVehicleRfdDoneByDefectEntry(defectEntryId))
                    return RfdLockedResponse<bool>();

                var updated = await _service.UpdateConcern(defectEntryId, dto);
                var ok = _responseHelper.CreateSuccessResponse(updated, "Success", "Concern updated.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "UpdateConcern");
            }
        }

        /// <summary>
        /// Remove a single concern row from MM_Vehicle_Defects by ID.
        /// </summary>
        [HttpPost("DeleteConcern/{defectEntryId}")]
        public async Task<IActionResult> DeleteConcern(decimal defectEntryId)
        {
            try
            {
                if (defectEntryId <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<bool>(
                        "Validation Error",
                        "Defect_Entry_ID is required.");
                    return BadRequest(bad);
                }

                if (await _service.IsConcernLockedByRework(defectEntryId))
                {
                    var locked = _responseHelper.CreateErrorResponse<bool>(
                        "Locked",
                        "This concern has been reworked and can no longer be deleted.");
                    return BadRequest(locked);
                }

                if (await _service.IsVehicleRfdDoneByDefectEntry(defectEntryId))
                    return RfdLockedResponse<bool>();

                var deleted = await _service.DeleteConcern(defectEntryId);
                var ok = _responseHelper.CreateSuccessResponse(deleted, "Success", deleted ? "Concern removed." : "Concern not found.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "DeleteConcern");
            }
        }

        /// <summary>
        /// Save an OK/NOK-style stage (DTC / ECOS / VMS) — status, auditor,
        /// and the list of uploaded file names. No concern rows are written.
        /// </summary>
        [HttpPost("SaveStageStatus")]
        public async Task<IActionResult> SaveStageStatus([FromBody] StageStatusSaveDto dto)
        {
            try
            {
                if (dto == null || dto.Vehicle_ID <= 0 || dto.Audit_Area_ID <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<bool>(
                        "Validation Error",
                        "Vehicle_ID and Audit_Area_ID are required.");
                    return BadRequest(bad);
                }
                if (string.IsNullOrWhiteSpace(dto.Auditor_Name))
                {
                    var bad = _responseHelper.CreateErrorResponse<bool>(
                        "Validation Error",
                        "Auditor_Name is required.");
                    return BadRequest(bad);
                }

                if (await _service.IsVehicleRfdDone(dto.Vehicle_ID))
                    return RfdLockedResponse<bool>();

                var ok = await _service.SaveStageStatus(dto);
                var resp = _responseHelper.CreateSuccessResponse(ok, "Success", "Stage saved.");
                return Ok(resp);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "SaveStageStatus");
            }
        }

        /// <summary>
        /// Multipart upload — saves a single file into wwwroot/uploads/{folder}/.
        /// Returns the GUID-prefixed file name to store in Track_Logs.Documents.
        /// </summary>
        [HttpPost("UploadStageFile")]
        public async Task<IActionResult> UploadStageFile([FromForm] IFormFile File, [FromForm] string Folder)
        {
            try
            {
                if (File == null || File.Length == 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<string>(
                        "Validation Error", "File is required.");
                    return BadRequest(bad);
                }
                if (string.IsNullOrWhiteSpace(Folder) || !AllowedFolders.Contains(Folder))
                {
                    var bad = _responseHelper.CreateErrorResponse<string>(
                        "Validation Error",
                        $"Folder '{Folder}' is not allowed. Use one of: {string.Join(", ", AllowedFolders)}");
                    return BadRequest(bad);
                }

                string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/", Folder);
                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                string savedName = $"{Guid.NewGuid()}_{Path.GetFileName(File.FileName)}";
                string filePath = Path.Combine(uploadPath, savedName);

                await using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    await File.CopyToAsync(fs);
                }

                var resp = _responseHelper.CreateSuccessResponse(savedName, "Success", "File uploaded.");
                return Ok(resp);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "UploadStageFile");
            }
        }

        /// <summary>
        /// Remove a single uploaded file from disk (used when the operator
        /// removes a file from the list before/after save).
        /// </summary>
        [HttpPost("DeleteStageFile")]
        public async Task<IActionResult> DeleteStageFile([FromBody] DeleteStageFileDto dto)
        {
            try
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.Folder) || string.IsNullOrWhiteSpace(dto.FileName))
                {
                    var bad = _responseHelper.CreateErrorResponse<bool>(
                        "Validation Error", "Folder and FileName are required.");
                    return BadRequest(bad);
                }
                if (!AllowedFolders.Contains(dto.Folder))
                {
                    var bad = _responseHelper.CreateErrorResponse<bool>(
                        "Validation Error", $"Folder '{dto.Folder}' is not allowed.");
                    return BadRequest(bad);
                }

                // Defensive: strip any path separators from FileName to prevent traversal.
                var safeName = Path.GetFileName(dto.FileName);
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/", dto.Folder, safeName);

                bool removed = false;
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    removed = true;
                }

                var resp = _responseHelper.CreateSuccessResponse(removed, "Success",
                    removed ? "File deleted." : "File not found on server.");
                return Ok(resp);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "DeleteStageFile");
            }
        }

        /// <summary>
        /// Download a single uploaded file from a stage folder.
        /// </summary>
        [HttpGet("DownloadStageFile")]
        public IActionResult DownloadStageFile([FromQuery] string folder, [FromQuery] string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !AllowedFolders.Contains(folder))
                    return BadRequest(_responseHelper.CreateErrorResponse<object>("Validation Error", "Invalid folder."));
                if (string.IsNullOrWhiteSpace(fileName))
                    return BadRequest(_responseHelper.CreateErrorResponse<object>("Validation Error", "FileName is required."));

                var safeName = Path.GetFileName(fileName);
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/", folder, safeName);
                if (!System.IO.File.Exists(filePath))
                    return NotFound(_responseHelper.CreateErrorResponse<object>("Not Found", "File not found."));

                var bytes = System.IO.File.ReadAllBytes(filePath);
                var display = safeName.Contains('_') ? safeName.Substring(safeName.IndexOf('_') + 1) : safeName;
                var contentType = GetContentType(display);
                return File(bytes, contentType, display);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "DownloadStageFile");
            }
        }

        private static string GetContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".pdf"  => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png"  => "image/png",
                ".gif"  => "image/gif",
                ".bmp"  => "image/bmp",
                ".webp" => "image/webp",
                ".txt"  => "text/plain",
                ".doc"  => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls"  => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream",
            };
        }

        /// <summary>
        /// Physical RFD readiness: per-rule pass/fail + pending-rework concerns
        /// so the operator sees exactly what's blocking dispatch.
        /// </summary>
        [HttpGet("GetRfdReadiness/{vehicleId}")]
        public async Task<IActionResult> GetRfdReadiness(decimal vehicleId)
        {
            try
            {
                if (vehicleId <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<RfdReadinessDto>(
                        "Validation Error", "Vehicle_ID is required.");
                    return BadRequest(bad);
                }

                var result = await _service.GetRfdReadiness(vehicleId);
                var ok = _responseHelper.CreateSuccessResponse(result, "Success", "RFD readiness retrieved.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetRfdReadiness");
            }
        }

        /// <summary>
        /// Save the Physical RFD stage. Re-runs readiness server-side; returns
        /// 400 with the failed-rule list if anything has changed since the UI
        /// last refreshed.
        /// </summary>
        [HttpPost("SaveRfd")]
        public async Task<IActionResult> SaveRfd([FromBody] SaveRfdDto dto)
        {
            try
            {
                if (dto == null || dto.Vehicle_ID <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<object>(
                        "Validation Error", "Vehicle_ID is required.");
                    return BadRequest(bad);
                }
                if (string.IsNullOrWhiteSpace(dto.Operator_Name))
                {
                    var bad = _responseHelper.CreateErrorResponse<object>(
                        "Validation Error", "Operator_Name is required.");
                    return BadRequest(bad);
                }

                var (savedOk, reason, trackLogId) = await _service.SaveRfd(dto);
                if (!savedOk)
                {
                    var notReady = _responseHelper.CreateErrorResponse<object>(
                        "Not Ready",
                        reason ?? "Vehicle is not ready for dispatch.");
                    return BadRequest(notReady);
                }

                var ok = _responseHelper.CreateSuccessResponse(new { Track_Log_ID = trackLogId },
                    "Success", "Vehicle marked as Ready For Dispatch.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "SaveRfd");
            }
        }

        /// <summary>
        /// Single payload for the Rework screen: every concern joined with its
        /// rework state (if any) and the Rework Track_Logs row.
        /// </summary>
        [HttpGet("GetReworkData/{vehicleId}")]
        public async Task<IActionResult> GetReworkData(decimal vehicleId)
        {
            try
            {
                if (vehicleId <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<object>(
                        "Validation Error", "Vehicle_ID is required.");
                    return BadRequest(bad);
                }

                var result = await _service.GetReworkData(vehicleId);
                var ok = _responseHelper.CreateSuccessResponse(result, "Success", "Rework data retrieved.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetReworkData");
            }
        }

        /// <summary>
        /// Auto-save a stage's Documents list alone (Paint Verification
        /// attachments). Rejected when the vehicle is RFD-locked.
        /// </summary>
        [HttpPost("UpdateStageDocuments")]
        public async Task<IActionResult> UpdateStageDocuments([FromBody] UpdateStageDocumentsDto dto)
        {
            try
            {
                if (dto == null || dto.Vehicle_ID <= 0 || dto.Audit_Area_ID <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<bool>(
                        "Validation Error", "Vehicle_ID and Audit_Area_ID are required.");
                    return BadRequest(bad);
                }

                if (await _service.IsVehicleRfdDone(dto.Vehicle_ID))
                    return RfdLockedResponse<bool>();

                var result = await _service.UpdateStageDocuments(dto);
                var ok = _responseHelper.CreateSuccessResponse(result, "Success", "Documents saved.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "UpdateStageDocuments");
            }
        }

        /// <summary>
        /// Upsert one stage-level rework row (DTC / ECOS / VMS / Paint) keyed
        /// on (Vehicle_ID, Audit_Area_ID). Rejected when the vehicle is RFD-locked.
        /// </summary>
        [HttpPost("UpsertStageRework")]
        public async Task<IActionResult> UpsertStageRework([FromBody] StageReworkUpsertDto dto)
        {
            try
            {
                if (dto == null || dto.Vehicle_ID <= 0 || dto.Audit_Area_ID <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<StageReworkUpsertResultDto>(
                        "Validation Error",
                        "Vehicle_ID and Audit_Area_ID are required.");
                    return BadRequest(bad);
                }

                if (await _service.IsVehicleRfdDone(dto.Vehicle_ID))
                    return RfdLockedResponse<StageReworkUpsertResultDto>();

                var result = await _service.UpsertStageRework(dto);
                var ok = _responseHelper.CreateSuccessResponse(result, "Success", "Stage rework saved.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "UpsertStageRework");
            }
        }

        /// <summary>
        /// Cross-vehicle pending-rework list for the standalone Rework team
        /// screen. Filters are optional — attributionId=0 (or omitted) means
        /// All; vinNo accepts the full VIN or any substring (e.g., last 8).
        /// Done rows are excluded server-side.
        /// </summary>
        [HttpGet("GetPendingReworkList")]
        public async Task<IActionResult> GetPendingReworkList(
            [FromQuery] decimal? attributionId,
            [FromQuery] string? vinNo)
        {
            try
            {
                var result = await _service.GetPendingReworkList(attributionId, vinNo);
                var ok = _responseHelper.CreateSuccessResponse(result, "Success", "Pending rework list retrieved.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetPendingReworkList");
            }
        }

        /// <summary>
        /// Upsert one rework row keyed on Defect_Entry_ID. Called from the
        /// Rework table on every checkbox toggle / Rework_By change.
        /// </summary>
        [HttpPost("UpsertRework")]
        public async Task<IActionResult> UpsertRework([FromBody] ReworkUpsertDto dto)
        {
            try
            {
                if (dto == null || dto.Defect_Entry_ID <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<ReworkUpsertResultDto>(
                        "Validation Error", "Defect_Entry_ID is required.");
                    return BadRequest(bad);
                }

                if (await _service.IsVehicleRfdDoneByDefectEntry(dto.Defect_Entry_ID))
                    return RfdLockedResponse<ReworkUpsertResultDto>();

                var result = await _service.UpsertRework(dto);
                var ok = _responseHelper.CreateSuccessResponse(result, "Success", "Rework saved.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "UpsertRework");
            }
        }

        /// <summary>
        /// Single payload for the Recheck screen: all defect concerns on the
        /// vehicle (across stages, joined with master + stage names) plus the
        /// DTC / ECOS / VMS status-stage Track_Logs rows (auditor, OK/NOK,
        /// uploaded documents JSON).
        /// </summary>
        [HttpGet("GetRecheckData/{vehicleId}")]
        public async Task<IActionResult> GetRecheckData(decimal vehicleId)
        {
            try
            {
                if (vehicleId <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<object>(
                        "Validation Error", "Vehicle_ID is required.");
                    return BadRequest(bad);
                }

                var result = await _service.GetRecheckData(vehicleId);
                var ok = _responseHelper.CreateSuccessResponse(result, "Success", "Recheck data retrieved.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetRecheckData");
            }
        }

        /// <summary>
        /// Get the saved state for a specific (Vehicle, Stage) — track log + concerns.
        /// </summary>
        [HttpGet("GetStageData/{vehicleId}/{auditAreaId}")]
        public async Task<IActionResult> GetStageData(decimal vehicleId, decimal auditAreaId)
        {
            try
            {
                var result = await _service.GetStageData(vehicleId, auditAreaId);
                var ok = _responseHelper.CreateSuccessResponse(result, "Success", "Stage data retrieved.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetStageData");
            }
        }

        /// <summary>
        /// Get all stage logs for a vehicle (drives the progress bar state).
        /// </summary>
        [HttpGet("GetStageLogsByVehicle/{vehicleId}")]
        public async Task<IActionResult> GetStageLogsByVehicle(decimal vehicleId)
        {
            try
            {
                var result = await _service.GetStageLogsByVehicle(vehicleId);
                var ok = _responseHelper.CreateSuccessResponse(result, "Success", "Stage logs retrieved.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetStageLogsByVehicle");
            }
        }
    }
}
