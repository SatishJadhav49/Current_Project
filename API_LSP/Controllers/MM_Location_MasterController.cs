using API_LSP.Data;
using API_LSP.Models.DTOs;
using API_LSP.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API_LSP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MM_Location_MasterController : BaseController
    {
        private readonly MM_Location_MasterDataService _Location_MasterDataService;

        public MM_Location_MasterController(
            MM_Location_MasterDataService location_MasterDataService,
            IApiResponseHelper responseHelper,
            MM_ErrorLogDataService logerrorService
        ) : base(responseHelper, logerrorService)
        {
            _Location_MasterDataService = location_MasterDataService;
        }

        [HttpPost("CreateLocation")]
        public async Task<IActionResult> CreateLocation([FromBody] LocationMasterCreateDto createlocation)
        {
            try
            {
                var result = await _Location_MasterDataService.CreateLocation(createlocation);
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Location(s) created successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "CreateLocation");
            }
        }

        [HttpPost("DeleteLocation")]
        public async Task<IActionResult> DeleteLocation([FromBody] decimal Location_ID)
        {
            try
            {
                var result = await _Location_MasterDataService.DeleteLocation(Location_ID);
                if(result)
                {
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Location deleted successfully.");
                return Ok(response);
                }else
                {
                var response = _responseHelper.CreateErrorResponse("", "Error", "Location not deleted successfully.");
                return Ok(response);
                }
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "DeleteLocation");
            }
        }

        [HttpGet("GetAllLocations")]
        public async Task<IActionResult> GetAllLocations()
        {
            try
            {
                var result = await _Location_MasterDataService.GetAllLocations();
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Fetched all Locations.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetAllLocations Internal Server Error");
            }
        }

        [HttpPost("UpdateLocation/{Location_ID}")]
        public async Task<IActionResult> UpdateLocation(decimal Location_ID, LocationMasterUpdateDto locationUpdateList)
        {
            try
            {
                var result = await _Location_MasterDataService.UpdateLocation(Location_ID, locationUpdateList);
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Location updated successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "UpdateLocation");
            }
        }
    }
}
