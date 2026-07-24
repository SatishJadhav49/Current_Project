using API_LSP.Data;
using API_LSP.Models.DTOs;
using API_LSP.Utils;
using Microsoft.AspNetCore.Mvc;


namespace API_LSP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MM_Attribution_MasterController : BaseController
    {
        private readonly MM_Attribution_MasterDataService _attributionMasterDataService;

        public MM_Attribution_MasterController(
            MM_Attribution_MasterDataService attributionMasterDataService,
            IApiResponseHelper responseHelper,
            MM_ErrorLogDataService logerrorService
        ) : base(responseHelper, logerrorService)
        {
            _attributionMasterDataService = attributionMasterDataService;


        }

        [HttpPost("CreateAttribution")]
        public async Task<IActionResult> CreateAttribution([FromBody] AttributionMasterCreateDto createAttributions)
        {
            try
            {
                var result = await _attributionMasterDataService.CreateAttribution(createAttributions);
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Attribution(s) created successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "CreateAttribution");
            }
        }

        [HttpPost("DeleteAttribution")]
        public async Task<IActionResult> DeleteAttribution([FromBody] int Attribution_ID)
        {
            try
            {
                var result = await _attributionMasterDataService.DeleteAttribution(Attribution_ID);
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Attribution deleted successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "DeleteAttribution");
            }
        }

        [HttpGet("GetAllAttributions")]
        public async Task<IActionResult> GetAllAttributions()
        {
            try
            {
                var result = await _attributionMasterDataService.GetAllAttributions();
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Fetched all attributions.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetAllAttributions");
            }
        }

        [HttpPut("UpdateAttribution/{AttributionId}")]
        public async Task<IActionResult> UpdateAttribution(decimal AttributionId, AttributionMasterUpdateDto attributionUpdateList)
        {
            try
            {
                var result = await _attributionMasterDataService.UpdateAttributionsMaster(AttributionId, attributionUpdateList);
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Attributions updated successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "UpdateAttribution");
            }
        }
    }

}