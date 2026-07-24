using API_LSP.Data;
using API_LSP.Models.DTOs;
using API_LSP.Utils;
using Microsoft.AspNetCore.Mvc;

namespace API_LSP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MM_Vehicle_DetailsController : BaseController
    {
        private readonly MM_Vehicle_DetailsDataService _VehicleDetailsDataService;
        private readonly MM_Vehicle_TrackingDataService _trackingService;
        public MM_Vehicle_DetailsController(
            MM_Vehicle_DetailsDataService VehicleDetailsData,
            MM_Vehicle_TrackingDataService trackingService,
            IApiResponseHelper responseHelper,
            MM_ErrorLogDataService logerrorService) : base(responseHelper, logerrorService)
        {
            _VehicleDetailsDataService = VehicleDetailsData;
            _trackingService = trackingService;
        }

        //  GetVinData
        [HttpGet("GetVinDetails/{vin}")]
        public async Task<IActionResult> GetVinDetails(string vin)
        {
            try
            {
                var result = await _VehicleDetailsDataService.GetVinDetails(vin);
                if (result == null)
                {
                    return NotFound(_responseHelper.CreateErrorResponse("Vehicle not found in drona : "+ vin, "GetVinDetails", vin));
                }
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Vehicle data retrieved successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetVinDetails", vin);
            }

        }

        // SaveVehicle — upsert by VIN
        [HttpPost("SaveVehicle")]
        public async Task<IActionResult> SaveVehicle([FromBody] MM_Vehicle_DetailsDto dto)
        {
            try
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.VIN_No))
                {
                    var bad = _responseHelper.CreateErrorResponse<MM_Vehicle_DetailsDto>("Validation Error", "VIN_No is required");
                    return BadRequest(bad);
                }

                // Block re-saves once RFD has been issued for this vehicle.
                if (dto.Vehicle_ID > 0 && await _trackingService.IsVehicleRfdDone(dto.Vehicle_ID))
                {
                    var locked = _responseHelper.CreateErrorResponse<MM_Vehicle_DetailsDto>(
                        "Locked",
                        "Vehicle is already marked Ready For Dispatch — no further changes are allowed.");
                    return BadRequest(locked);
                }

                var saved = await _VehicleDetailsDataService.SaveVehicle(dto);
                var response = _responseHelper.CreateSuccessResponse(saved, "Success", "Vehicle saved successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "SaveVehicle", dto?.VIN_No ?? "");
            }
        }
    }
}