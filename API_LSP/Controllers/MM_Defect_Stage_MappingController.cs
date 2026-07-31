using API_LSP.Data;
using API_LSP.Models.DTOs;
using API_LSP.Utils;
using Microsoft.AspNetCore.Mvc;

namespace API_LSP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MM_Defect_Stage_MappingController : BaseController
    {
        private readonly MM_Defect_Stage_MappingDataService _service;

        public MM_Defect_Stage_MappingController(
            MM_Defect_Stage_MappingDataService service,
            IApiResponseHelper responseHelper,
            MM_ErrorLogDataService logerrorService
        ) : base(responseHelper, logerrorService)
        {
            _service = service;
        }

        /// <summary>Entry stages + mapped-defect counts (master list).</summary>
        [HttpGet("GetStageSummaries")]
        public async Task<IActionResult> GetStageSummaries()
        {
            try
            {
                var result = await _service.GetStageSummaries();
                return Ok(_responseHelper.CreateSuccessResponse(result, "Success", "Fetched stage summaries."));
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetStageSummaries");
            }
        }

        /// <summary>Defect_IDs currently mapped to a stage (checklist state).</summary>
        [HttpGet("GetStageDefectIds/{auditAreaId}")]
        public async Task<IActionResult> GetStageDefectIds(decimal auditAreaId)
        {
            try
            {
                var result = await _service.GetStageDefectIds(auditAreaId);
                return Ok(_responseHelper.CreateSuccessResponse(result, "Success", "Fetched mapped defect ids."));
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetStageDefectIds");
            }
        }

        /// <summary>Effective defect list for a stage (mapped, or all as fallback).</summary>
        [HttpGet("GetMappedDefects/{auditAreaId}")]
        public async Task<IActionResult> GetMappedDefects(decimal auditAreaId)
        {
            try
            {
                var result = await _service.GetMappedDefects(auditAreaId);
                return Ok(_responseHelper.CreateSuccessResponse(result, "Success", "Fetched stage defects."));
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetMappedDefects");
            }
        }

        /// <summary>Replace the full defect set mapped to a stage.</summary>
        [HttpPost("SaveStageMapping")]
        public async Task<IActionResult> SaveStageMapping([FromBody] SaveStageMappingDto dto)
        {
            try
            {
                if (dto == null || dto.Audit_Area_ID <= 0)
                {
                    var bad = _responseHelper.CreateErrorResponse<bool>("Validation Error", "Stage is required.");
                    return BadRequest(bad);
                }

                var result = await _service.SaveStageMapping(dto);
                return Ok(_responseHelper.CreateSuccessResponse(result, "Success", "Mapping saved successfully."));
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "SaveStageMapping");
            }
        }
    }
}
