using System.ComponentModel.DataAnnotations;

namespace BM.Service.Core.Models
{
    /// <summary>
    /// 一键提交今日打卡
    /// </summary>
    public class DailySubmitInputViewModel
    {
        /// <summary>
        /// 计划日期，格式 yyyy-MM-dd
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string plan_date { get; set; } = string.Empty;
    }
}
