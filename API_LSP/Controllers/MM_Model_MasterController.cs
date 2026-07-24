using API_LSP.Data;
using API_LSP.Models;
using API_LSP.Models.DTOs;
using API_LSP.Utils;
using Microsoft.AspNetCore.Mvc;

namespace API_LSP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MM_Model_MasterController : BaseController
    {
        private readonly MM_ModelDataService _modelDataService;

        public MM_Model_MasterController(
            MM_ModelDataService modelDataService,
            IApiResponseHelper responseHelper,
            MM_ErrorLogDataService errorLogService)
            : base(responseHelper, errorLogService)
        {
            _modelDataService = modelDataService;
        }

        [HttpGet("GetModelList")]
        public async Task<IActionResult> GetModelList()
        {
            try
            {
                var modelList = await _modelDataService.GetModelList();
                var response = _responseHelper.CreateSuccessResponse(modelList, "Success", "Models retrieved successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetModelList");
            }
        }

        // Update
        [HttpPost("UpdateModelStatus")]
        public async Task<IActionResult> UpdateModelStatus([FromBody] MM_ModelListDto model)
        {
            try
            {
                var result = await _modelDataService.UpdateModelStatus(model);
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Model status updated successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "UpdateModelStatus");
            }
        }
    }
}
