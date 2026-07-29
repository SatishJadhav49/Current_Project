using API_LSP.Data;
using API_LSP.Models.DTOs;
using API_LSP.Utils;
using Microsoft.AspNetCore.Mvc;

namespace API_LSP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MM_ReportsController : BaseController
    {
        private readonly MM_ReportsDataService _service;

        public MM_ReportsController(
            MM_ReportsDataService service,
            IApiResponseHelper responseHelper,
            MM_ErrorLogDataService errorLogService)
            : base(responseHelper, errorLogService)
        {
            _service = service;
        }

        /// <summary>
        /// Vehicle Tracking report — filtered by Model + Inserted_Date range.
        /// Returns one row per vehicle with each stage's date + status.
        /// </summary>
        [HttpPost("GetVehicleTrackingReport")]
        public async Task<IActionResult> GetVehicleTrackingReport([FromBody] VehicleTrackingReportRequest req)
        {
            try
            {
                if (req == null || req.Model_ID <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<List<VehicleTrackingReportRow>>(
                        "Validation Error", "Model is required.");
                    return BadRequest(bad);
                }
                if (req.EndDate.Date < req.StartDate.Date)
                {
                    var bad = _responseHelper.CreateErrorResponse<List<VehicleTrackingReportRow>>(
                        "Validation Error", "End date cannot be before start date.");
                    return BadRequest(bad);
                }

                var rows = await _service.GetVehicleTrackingReport(req.Model_ID, req.StartDate, req.EndDate);
                var ok = _responseHelper.CreateSuccessResponse(rows, "Success", "Report generated.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetVehicleTrackingReport");
            }
        }

        /// <summary>
        /// Daily report — one Model on one date. Returns headline counts, the
        /// per-stage completed-today grid, and the defects entered that day.
        /// </summary>
        [HttpPost("GetDailyReport")]
        public async Task<IActionResult> GetDailyReport([FromBody] DailyReportRequest req)
        {
            try
            {
                if (req == null || req.Model_ID <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<DailyReportResponse>(
                        "Validation Error", "Model is required.");
                    return BadRequest(bad);
                }

                var data = await _service.GetDailyReport(req.Model_ID, req.ReportDate);
                var ok = _responseHelper.CreateSuccessResponse(data, "Success", "Report generated.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetDailyReport");
            }
        }

        /// <summary>
        /// WIP / Pending report — pending (not-yet-RFD) vehicles for a Model over
        /// a vehicle-date range, with the stage each is stuck at and ageing.
        /// </summary>
        [HttpPost("GetWipReport")]
        public async Task<IActionResult> GetWipReport([FromBody] WipReportRequest req)
        {
            try
            {
                if (req == null || req.Model_ID <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<WipReportResponse>(
                        "Validation Error", "Model is required.");
                    return BadRequest(bad);
                }
                if (req.EndDate.Date < req.StartDate.Date)
                {
                    var bad = _responseHelper.CreateErrorResponse<WipReportResponse>(
                        "Validation Error", "End date cannot be before start date.");
                    return BadRequest(bad);
                }

                var data = await _service.GetWipReport(req.Model_ID, req.StartDate, req.EndDate);
                var ok = _responseHelper.CreateSuccessResponse(data, "Success", "Report generated.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetWipReport");
            }
        }

        /// <summary>
        /// Stage TAT report — average turnaround between stages plus the overall
        /// Roll Down → RFD cycle time, for a Model over a vehicle-date range.
        /// </summary>
        [HttpPost("GetTatReport")]
        public async Task<IActionResult> GetTatReport([FromBody] TatReportRequest req)
        {
            try
            {
                bool hasVin = req != null && !string.IsNullOrWhiteSpace(req.Vin_No);
                if (req == null || (!hasVin && req.Model_ID <= 0))
                {
                    var bad = _responseHelper.CreateErrorResponse<TatReportResponse>(
                        "Validation Error", "Model or VIN is required.");
                    return BadRequest(bad);
                }
                if (!hasVin && req.EndDate.Date < req.StartDate.Date)
                {
                    var bad = _responseHelper.CreateErrorResponse<TatReportResponse>(
                        "Validation Error", "End date cannot be before start date.");
                    return BadRequest(bad);
                }

                var data = await _service.GetTatReport(req.Model_ID, req.StartDate, req.EndDate, req.Vin_No);
                var ok = _responseHelper.CreateSuccessResponse(data, "Success", "Report generated.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetTatReport");
            }
        }

        /// <summary>
        /// Image defect heat-map — Paint tap-points aggregated onto each area
        /// image for a Model + Vehicle Area over a vehicle-date range.
        /// </summary>
        [HttpPost("GetImageHeatmapReport")]
        public async Task<IActionResult> GetImageHeatmapReport([FromBody] ImageHeatmapRequest req)
        {
            try
            {
                if (req == null || req.Model_ID <= 0 || req.Vehicle_Area_ID <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<ImageHeatmapResponse>(
                        "Validation Error", "Model and Vehicle Area are required.");
                    return BadRequest(bad);
                }
                if (req.EndDate.Date < req.StartDate.Date)
                {
                    var bad = _responseHelper.CreateErrorResponse<ImageHeatmapResponse>(
                        "Validation Error", "End date cannot be before start date.");
                    return BadRequest(bad);
                }

                var data = await _service.GetImageHeatmapReport(
                    req.Model_ID, req.Vehicle_Area_ID, req.StartDate, req.EndDate);
                var ok = _responseHelper.CreateSuccessResponse(data, "Success", "Report generated.");
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetImageHeatmapReport");
            }
        }
    }
}
