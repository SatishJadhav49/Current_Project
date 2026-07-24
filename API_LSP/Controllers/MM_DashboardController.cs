using API_LSP.Data;
using API_LSP.Utils;
using Microsoft.AspNetCore.Mvc;

namespace API_LSP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MM_DashboardController : BaseController
    {
        private readonly MM_DashboardDataService _dashboardDataService;

        public MM_DashboardController(
            MM_DashboardDataService dashboardDataService,
            IApiResponseHelper responseHelper,
            MM_ErrorLogDataService errorLogService)
            : base(responseHelper, errorLogService)
        {
            _dashboardDataService = dashboardDataService;
        }




    }
}