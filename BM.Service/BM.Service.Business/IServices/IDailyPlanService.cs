using BM.Service.Core.DI;
using BM.Service.Core.Models;

namespace BM.Service.Business.IServices
{
    /// <summary>
    /// 每日训练计划
    /// </summary>
    public interface IDailyPlanService : IDependency
    {
        /// <summary>
        /// 获取今日训练计划（不存在则按训练方案生成）
        /// </summary>
        Task<(DailyTodayOutputViewModel? data, string? error)> GetTodayAsync(int userId);

        /// <summary>
        /// 一键提交指定日期打卡
        /// </summary>
        Task<(bool ok, string? error)> SubmitAsync(int userId, DailySubmitInputViewModel input);
    }
}
