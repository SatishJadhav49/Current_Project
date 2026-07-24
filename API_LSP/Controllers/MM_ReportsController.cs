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
    }
}
