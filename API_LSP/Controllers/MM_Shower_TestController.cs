using API_LSP.Data;
using API_LSP.Models.DTOs;
using API_LSP.Utils;
using Microsoft.AspNetCore.Mvc;

namespace API_LSP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MM_Shower_TestController : BaseController
    {
        private readonly MM_ShowerTestDataService _showerService;
        private readonly MM_Vehicle_TrackingDataService _trackingService;

        public MM_Shower_TestController(
            MM_ShowerTestDataService showerService,
            MM_Vehicle_TrackingDataService trackingService,
            IApiResponseHelper responseHelper,
            MM_ErrorLogDataService errorLogService)
            : base(responseHelper, errorLogService)
        {
            _showerService = showerService;
            _trackingService = trackingService;
        }

        /// <summary>
        /// Imports shower-test data for a vehicle by calling the external
        /// SP_Get_Shower_Defects_For_LSP on PQ_Dock_Audit, then upserting the
        /// matching Track_Logs row + replacing the Vehicle_Defects rows for
        /// Audit_Area_ID = 4. Rejected when the vehicle is RFD-locked.
        /// </summary>
        [HttpPost("ImportForVehicle")]
        public async Task<IActionResult> ImportForVehicle([FromBody] ShowerImportRequestDto req)
        {
            try
            {
                if (req == null || req.Vehicle_ID <= 0 || string.IsNullOrWhiteSpace(req.VIN_No))
                {
                    var bad = _responseHelper.CreateErrorResponse<ShowerImportResultDto>(
                        "Validation Error",
                        "Vehicle_ID and VIN_No are required.");
                    return BadRequest(bad);
                }

                if (await _trackingService.IsVehicleRfdDone(req.Vehicle_ID))
                {
                    var locked = _responseHelper.CreateErrorResponse<ShowerImportResultDto>(
                        "Locked",
                        "Vehicle is already marked Ready For Dispatch — no further changes are allowed.");
                    return BadRequest(locked);
                }

                var result = await _showerService.ImportShowerForVehicle(req);

                var message = !result.Found
                    ? "Shower test is not yet done for this vehicle."
                    : result.Is_No_Defect
                        ? "Shower test completed — no defects found."
                        : $"Imported {result.Imported} defect(s) from shower test.";

                var ok = _responseHelper.CreateSuccessResponse(result, "Success", message);
                return Ok(ok);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "ImportForVehicle");
            }
        }
    }
}
