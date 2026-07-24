using System.Threading.Tasks;
using API_LSP.Data;
using API_LSP.Models.DTOs;
using API_LSP.Utils;
using Microsoft.AspNetCore.Mvc;

namespace API_LSP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MM_CommonController : BaseController
    {
        private readonly MM_CommonDataService _CommonDataService;

        public MM_CommonController(
            MM_CommonDataService CommonDataService,
            IApiResponseHelper responseHelper,
            MM_ErrorLogDataService errorLogService)
            : base(responseHelper, errorLogService)
        {
            _CommonDataService = CommonDataService;
        }

        [HttpGet("GetShopList")]
        public async Task<IActionResult> GetShopList()
        {
            try
            {
                var shopList = await _CommonDataService.GetShopList();
                var response = _responseHelper.CreateSuccessResponse(shopList, "Success", "Shops retrieved successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetShopList");
            }
        }

        [HttpPost("GetModelList")]
        public async Task<IActionResult> GetModelList([FromBody] int[] ShopIds)
        {
            try
            {
                var modelList = await _CommonDataService.GetModelList(ShopIds);
                var response = _responseHelper.CreateSuccessResponse(modelList, "Success", "Models retrieved successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetModelList");
            }
        }

        [HttpGet("GetAuditAreaList")]
        public async Task<IActionResult> GetAuditAreaList()
        {
            try
            {
                var shopList = await _CommonDataService.GetAuditAreaList();
                var response = _responseHelper.CreateSuccessResponse(shopList, "Success", "Audit Areas retrieved successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetAuditAreaList");
            }
        }
         [HttpGet("GetCountryList")]
        public async Task<IActionResult> GetCountryList()
        {
            try
            {
                var shopList = await _CommonDataService.GetCountryList();
                var response = _responseHelper.CreateSuccessResponse(shopList, "Success", "Countries retrieved successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetCountryList");
            }
        }

        [HttpGet("GetBuildPhaseList")]
        public async Task<IActionResult> GetBuildPhaseList()
        {
            try
            {
                var list = await _CommonDataService.GetBuildPhaseList();
                var response = _responseHelper.CreateSuccessResponse(list, "Success", "Build phases retrieved successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetBuildPhaseList");
            }
        }

        [HttpGet("GetDriveList")]
        public async Task<IActionResult> GetDriveList()
        {
            try
            {
                var list = await _CommonDataService.GetDriveList();
                var response = _responseHelper.CreateSuccessResponse(list, "Success", "Drive types retrieved successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetDriveList");
            }
        }

        [HttpGet("GetFuelList")]
        public async Task<IActionResult> GetFuelList()
        {
            try
            {
                var list = await _CommonDataService.GetFuelList();
                var response = _responseHelper.CreateSuccessResponse(list, "Success", "Fuel types retrieved successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetFuelList");
            }
        }

        [HttpGet("GetPlantList")]
        public async Task<IActionResult> GetPlantList()
        {
            try
            {
                var list = await _CommonDataService.GetPlantList();
                var response = _responseHelper.CreateSuccessResponse(list, "Success", "Plants retrieved successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetPlantList");
            }
        }

        [HttpGet("GetVehicleAreaList")]
        public async Task<IActionResult> GetVehicleAreaList()
        {
            try
            {
                var list = await _CommonDataService.GetVehicleAreaList();
                var response = _responseHelper.CreateSuccessResponse(list, "Success", "Vehicle areas retrieved successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetVehicleAreaList");
            }
        }

        [HttpGet("GetSeverityList")]
        public async Task<IActionResult> GetSeverityList()
        {
            try
            {
                var list = await _CommonDataService.GetSeverityList();
                var response = _responseHelper.CreateSuccessResponse(list, "Success", "Severities retrieved successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetSeverityList");
            }
        }

        [HttpGet("SendTestMail")]
        public async Task<IActionResult> SendTestMail()
        {
            try
            {
                await _CommonDataService.SendTestMail();
                return Ok();
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "SendTestMail");
            }
        }

    }
}
