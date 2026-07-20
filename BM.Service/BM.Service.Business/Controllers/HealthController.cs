using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BM.Service.Core.Controller;
using BM.Service.Core.Models;

namespace BM.Service.Business.Controllers
{
    /// <summary>
    /// Health check / sample API for new business modules.
    /// Replace this with your own Controllers under Controllers/.
    /// </summary>
    [Route("[controller]")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "Base")]
    public class HealthController : BaseController
    {
        /// <summary>
        /// Simple ping endpoint (requires JWT).
        /// </summary>
        [HttpGet("ping")]
        public ResultModel<string> Ping()
        {
            return ResultModel<string>.Success("BM.Service is running");
        }

        /// <summary>
        /// Anonymous health endpoint.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("alive")]
        public ResultModel<string> Alive()
        {
            return ResultModel<string>.Success("ok");
        }
    }
}
