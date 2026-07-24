using API_LSP.Data;
using API_LSP.Models.DTOs;
using API_LSP.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace API_LSP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MM_Auditor_MasterController : BaseController
    {
        private readonly MM_Auditor_MasterDataService _Auditor_MasterDataService;

        public MM_Auditor_MasterController(
            MM_Auditor_MasterDataService Auditor_MasterDataService,
            IApiResponseHelper responseHelper,
            MM_ErrorLogDataService logerrorService
        ) : base(responseHelper, logerrorService)
        {
            _Auditor_MasterDataService = Auditor_MasterDataService;
        }

        [HttpPost("CreateAuditor")]
        public async Task<IActionResult> CreateAuditor([FromBody] AuditorMasterCreateDto createauditor)
        {
            try
            {
                var result = await _Auditor_MasterDataService.CreateAuditor(createauditor);
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Auditor(s) created successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "CreateAuditor");
            }
        }

        [HttpPost("DeleteAuditor")]
        public async Task<IActionResult> DeleteAuditor([FromBody] decimal Auditor_ID)
        {
            try
            {
                var result = await _Auditor_MasterDataService.DeleteAuditor(Auditor_ID);
                if(result)
                {
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Auditor deleted successfully.");
                return Ok(response);
                }else
                {
                var response = _responseHelper.CreateErrorResponse("", "Error", "Auditor not deleted successfully.");
                return Ok(response);
                }
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "DeleteAuditor");
            }
        } 

        [HttpGet("GetAllAuditors")]
        public async Task<IActionResult> GetAllAuditors()
        {
            try
            {
                var result = await _Auditor_MasterDataService.GetAllAuditors();
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Fetched all Auditors.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetAllAuditors Internal Server Error");
            }
        }


        [HttpGet("GetAuditorsByArea/{auditAreaId}")]
        public async Task<IActionResult> GetAuditorsByArea(decimal auditAreaId)
        {
            try
            {
                var result = await _Auditor_MasterDataService.GetAuditorsByArea(auditAreaId);
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Auditors retrieved successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetAuditorsByArea");
            }
        }

        [HttpPost("UpdateAuditor/{Auditor_Id}")]
        public async Task<IActionResult> UpdateAuditor(decimal Auditor_Id, AuditorMasterUpdateDto auditorUpdateList)
        {
            try
            {
                var result = await _Auditor_MasterDataService.UpdateAuditor(Auditor_Id, auditorUpdateList);
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Auditor updated successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "UpdateAuditor");
            }
        }

        // [HttpPost("UploadData")]
        // public async Task<IActionResult> UploadData([FromForm] IFormFile file, [FromForm] string otherinfo)
        // {
        //     try
        //     {
        //         var dto = JsonConvert.DeserializeObject<PartMasterCreateDto>(otherinfo);
        //         var (insertedLines, errors) = await _Part_MasterDataService.UploadLineDataAsync(file, dto);
        //         var response = _responseHelper.CreateSuccessResponse(insertedLines.Count, "Success", "Parts updated successfully.");
        //         return Ok(response);
        //     }
        //     catch (Exception ex)
        //     {

        //         Console.WriteLine("UploadData Error: " + ex.ToString());
        //         return StatusCode(500, new { Success = false, Message = ex.Message });
        //     }
        // }

    }
}
