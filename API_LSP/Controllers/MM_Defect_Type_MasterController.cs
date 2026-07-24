using API_LSP.Data;
using API_LSP.Models.DTOs;
using API_LSP.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace API_LSP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MM_Defect_Type_MasterController : BaseController
    {
        private readonly MM_Defect_Type_MasterDataService _Defect_Type_MasterDataService;

        public MM_Defect_Type_MasterController(
            MM_Defect_Type_MasterDataService Defect_Type_MasterDataService,
            IApiResponseHelper responseHelper,
            MM_ErrorLogDataService logerrorService
        ) : base(responseHelper, logerrorService)
        {
            _Defect_Type_MasterDataService = Defect_Type_MasterDataService;
        }

        [HttpPost("CreateDefectType")]
        public async Task<IActionResult> CreateDefectType([FromBody] DefectTypeMasterCreateDto createdefecttype)
        {
            try
            {
                var result = await _Defect_Type_MasterDataService.CreateDefectType(createdefecttype);
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Defect Type(s) created successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "CreateDefectType");
            }
        }

        [HttpPost("DeleteDefectType")]
        public async Task<IActionResult> DeleteDefectType([FromBody] decimal Defect_Type_ID)
        {
            try
            {
                var result = await _Defect_Type_MasterDataService.DeleteDefectType(Defect_Type_ID);
                if(result)
                {
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Defect Type deleted successfully.");
                return Ok(response);
                }else
                {
                var response = _responseHelper.CreateErrorResponse("", "Error", "Defect Type not deleted successfully.");
                return Ok(response);
                }
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "DeleteDefectType");
            }
        } 

        [HttpGet("GetAllDefectTypes")]
        public async Task<IActionResult> GetAllDefectTypes()
        {
            try
            {
                var result = await _Defect_Type_MasterDataService.GetAllDefectTypes();
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Fetched all Defect Types.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetAllDefectTypes Internal Server Error");
            }
        }


        [HttpPost("UpdateDefectType/{Defect_Type_Id}")]
        public async Task<IActionResult> UpdateDefectType(decimal Defect_Type_Id, DefectTypeMasterUpdateDto defectTypeUpdateList)
        {
            try
            {
                var result = await _Defect_Type_MasterDataService.UpdateDefectType(Defect_Type_Id, defectTypeUpdateList);
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Defect Type updated successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "UpdateDefectType");
            }
        }

    }
}
