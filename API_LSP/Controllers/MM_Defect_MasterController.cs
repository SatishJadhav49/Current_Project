using API_LSP.Data;
using API_LSP.Models.DTOs;
using API_LSP.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace API_LSP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MM_Defect_MasterController : BaseController
    {
        private readonly MM_Defect_MasterDataService _Defect_MasterDataService;

        public MM_Defect_MasterController(
            MM_Defect_MasterDataService Defect_MasterDataService,
            IApiResponseHelper responseHelper,
            MM_ErrorLogDataService logerrorService
        ) : base(responseHelper, logerrorService)
        {
            _Defect_MasterDataService = Defect_MasterDataService;
        }

        [HttpPost("CreateDefect")]
        public async Task<IActionResult> CreateDefect([FromBody] DefectMasterCreateDto createdefect)
        {
            try
            {
                var result = await _Defect_MasterDataService.CreateDefect(createdefect);
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Defect(s) created successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "CreateDefect");
            }
        }

        [HttpPost("DeleteDefect")]
        public async Task<IActionResult> DeleteDefect([FromBody] decimal Defect_ID)
        {
            try
            {
                var result = await _Defect_MasterDataService.DeleteDefect(Defect_ID);
                if(result)
                {
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Defect deleted successfully.");
                return Ok(response);
                }else
                {
                var response = _responseHelper.CreateErrorResponse("", "Error", "Defect not deleted successfully.");
                return Ok(response);
                }
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "DeleteDefect");
            }
        } 

        [HttpGet("GetAllDefects")]
        public async Task<IActionResult> GetAllDefects()
        {
            try
            {
                var result = await _Defect_MasterDataService.GetAllDefects();
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Fetched all Defects.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetAllDefects Internal Server Error");
            }
        }


        [HttpPost("UpdateDefect/{Defect_Id}")]
        public async Task<IActionResult> UpdateDefect(decimal Defect_Id, DefectMasterUpdateDto defectUpdateList)
        {
            try
            {
                var result = await _Defect_MasterDataService.UpdateDefect(Defect_Id, defectUpdateList);
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Defect updated successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "UpdateDefect");
            }
        }

    }
}
