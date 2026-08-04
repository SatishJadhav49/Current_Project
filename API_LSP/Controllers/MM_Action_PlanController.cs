using API_LSP.Data;
using API_LSP.Models.DTOs;
using API_LSP.Utils;
using Microsoft.AspNetCore.Mvc;

namespace API_LSP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MM_Action_PlanController : BaseController
    {
        private readonly MM_Action_PlanDataService _service;

        public MM_Action_PlanController(
            MM_Action_PlanDataService service,
            IApiResponseHelper responseHelper,
            MM_ErrorLogDataService logerrorService
        ) : base(responseHelper, logerrorService)
        {
            _service = service;
        }

        /// <summary>Problem rows (Part+Defect) for a model, with tracking metrics.</summary>
        [HttpGet("GetProblems/{modelId}")]
        public async Task<IActionResult> GetProblems(decimal modelId)
        {
            try
            {
                if (modelId <= 0)
                    return BadRequest(_responseHelper.CreateErrorResponse<List<ActionPlanProblemDto>>(
                        "Validation Error", "Model is required."));

                var result = await _service.GetProblems(modelId);
                return Ok(_responseHelper.CreateSuccessResponse(result, "Success", "Fetched problems."));
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetProblems");
            }
        }

        /// <summary>Existing plan detail for a problem, or an empty shell if none exists.</summary>
        [HttpGet("GetPlan/{modelId}/{partId}/{defectId}/{attributionId}/{severityId}")]
        public async Task<IActionResult> GetPlan(decimal modelId, decimal partId, decimal defectId, decimal attributionId, decimal severityId)
        {
            try
            {
                var result = await _service.GetPlanByProblem(modelId, partId, defectId, attributionId, severityId);
                return Ok(_responseHelper.CreateSuccessResponse(result, "Success", "Fetched plan."));
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "GetPlan");
            }
        }

        /// <summary>Submit a new plan or append rows to an existing one.</summary>
        [HttpPost("SubmitPlan")]
        public async Task<IActionResult> SubmitPlan([FromBody] ActionPlanSubmitDto dto)
        {
            try
            {
                if (dto == null || dto.Model_ID <= 0 || dto.Part_ID <= 0 || dto.Defect_ID <= 0 || dto.Attribution_ID <= 0)
                    return BadRequest(_responseHelper.CreateErrorResponse<object>(
                        "Validation Error", "Model, Part, Defect and Attribution are required."));

                var (ok, reason, planId) = await _service.SubmitPlan(dto);
                if (!ok)
                    return BadRequest(_responseHelper.CreateErrorResponse<object>("Validation Error", reason ?? "Could not save."));

                return Ok(_responseHelper.CreateSuccessResponse(planId, "Success", "Action plan saved."));
            }
            catch (Exception ex)
            {
                return await HandleExceptionAsync(ex, "SubmitPlan");
            }
        }
    }
}
