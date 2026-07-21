using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BM.Service.Business.IServices;
using BM.Service.Core.Controller;
using BM.Service.Core.Models;

namespace BM.Service.Business.Controllers
{
    /// <summary>
    /// 每日训练计划
    /// </summary>
    [Route("api/daily")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "Base")]
    public class DailyPlanController : BaseController
    {
        private readonly IDailyPlanService _dailyPlanService;
        private readonly ILogger<DailyPlanController> _logger;

        public DailyPlanController(IDailyPlanService dailyPlanService, ILogger<DailyPlanController> logger)
        {
            _dailyPlanService = dailyPlanService;
            _logger = logger;
        }

        /// <summary>
        /// 获取今日训练计划（不存在则按训练方案生成）
        /// </summary>
        /// <remarks>
        /// GET /api/daily/today
        /// Header: Authorization: Bearer {jwt_token}
        /// </remarks>
        [HttpGet("today")]
        public async Task<ResultModel<DailyTodayOutputViewModel>> GetToday()
        {
            if (CurrentUser == null || CurrentUser.user_id <= 0)
            {
                return ResultModel<DailyTodayOutputViewModel>.Error("Sorry, please sign in first!", 401);
            }

            var (data, error) = await _dailyPlanService.GetTodayAsync(CurrentUser.user_id);
            if (data == null)
            {
                return ResultModel<DailyTodayOutputViewModel>.Error(error ?? "get today plan failed", 400);
            }

            return ResultModel<DailyTodayOutputViewModel>.Success(data, "success");
        }

        /// <summary>
        /// 一键提交指定日期打卡
        /// </summary>
        /// <remarks>
        /// POST /api/daily/submit
        /// Header:
        ///   Content-Type: application/json
        ///   Authorization: Bearer {jwt_token}
        /// Body: { "plan_date": "2026-07-21" }
        /// </remarks>
        [HttpPost("submit")]
        public async Task<ResultModel<object>> Submit([FromBody] DailySubmitInputViewModel input)
        {
            if (CurrentUser == null || CurrentUser.user_id <= 0)
            {
                return ResultModel<object>.Error("Sorry, please sign in first!", 401);
            }

            var (ok, error) = await _dailyPlanService.SubmitAsync(CurrentUser.user_id, input);
            if (!ok)
            {
                return ResultModel<object>.Error(error ?? "submit failed", 400);
            }

            return ResultModel<object>.Success(new { submitted = true }, "success");
        }
    }
}
