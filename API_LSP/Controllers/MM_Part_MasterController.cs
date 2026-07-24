using API_LSP.Data;
using API_LSP.Models.DTOs;
using API_LSP.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace API_LSP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MM_Part_MasterController : BaseController
    {
        private readonly MM_Part_MasterDataService _Part_MasterDataService;

        public MM_Part_MasterController(
            MM_Part_MasterDataService Part_MasterDataService,
            IApiResponseHelper responseHelper,
            MM_ErrorLogDataService logerrorService
        ) : base(responseHelper, logerrorService)
        {
            _Part_MasterDataService = Part_MasterDataService;
        }

        [HttpPost("CreatePart")]
        public async Task<IActionResult> CreatePart([FromBody] PartMasterCreateDto createpart)
        {
            try
            {
                var result = await _Part_MasterDataService.CreatePart(createpart);
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Part(s) created successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "CreatePart");
            }
        }

        [HttpPost("DeletePart")]
        public async Task<IActionResult> DeletePart([FromBody] decimal Part_ID)
        {
            try
            {
                var result = await _Part_MasterDataService.DeletePart(Part_ID);
                if(result)
                {
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Part deleted successfully.");
                return Ok(response);
                }else
                {
                var response = _responseHelper.CreateErrorResponse("", "Error", "Part not deleted successfully.");
                return Ok(response);
                }
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "DeletePart");
            }
        } 

        [HttpGet("GetAllParts")]
        public async Task<IActionResult> GetAllParts()
        {
            try
            {
                var result = await _Part_MasterDataService.GetAllParts();
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Fetched all Parts.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetAllParts Internal Server Error");
            }
        }


        [HttpPost("UpdatePart/{Part_Id}")]
        public async Task<IActionResult> UpdatePart(decimal Part_Id, PartMasterUpdateDto partUpdateList)
        {
            try
            {
                var result = await _Part_MasterDataService.UpdatePart(Part_Id, partUpdateList);
                var response = _responseHelper.CreateSuccessResponse(result, "Success", "Part updated successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "UpdatePart");
            }
        }

        [HttpPost("UploadData")]
        public async Task<IActionResult> UploadData([FromForm] IFormFile file, [FromForm] string otherinfo)
        {
            try
            {
                var dto = JsonConvert.DeserializeObject<PartMasterCreateDto>(otherinfo);
                var (insertedLines, errors) = await _Part_MasterDataService.UploadLineDataAsync(file, dto);
                var response = _responseHelper.CreateSuccessResponse(insertedLines.Count, "Success", "Parts updated successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {

                Console.WriteLine("UploadData Error: " + ex.ToString());
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("ExportExcelData")]
        public async Task<IActionResult> ExportExcelData()
        {
            try
            {
                var ExcelData = await _Part_MasterDataService.ExportExcelDataAsync();
                var response = _responseHelper.CreateSuccessResponse(ExcelData, "Success", "Part data for Excel retrieved successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "ExportExcelData");
            }
        }
    }
}
