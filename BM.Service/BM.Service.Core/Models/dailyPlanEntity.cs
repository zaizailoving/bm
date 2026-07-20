using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BM.Service.Core.Models
{
    /// <summary>
    /// 每日计划表
    /// </summary>
    [Table("daily_plan")]
    [Index(nameof(user_id), nameof(plan_date), IsUnique = true, Name = "uniq_user_date")]
    [Index(nameof(plan_date), nameof(status), Name = "idx_date_status")]
    public class dailyPlanEntity : BaseModel
    {
        /// <summary>
        /// 学生ID
        /// </summary>
        [Required]
        public int user_id { get; set; }

        /// <summary>
        /// 计划日期（如：2026-07-17）
        /// </summary>
        [Required]
        [Column(TypeName = "date")]
        public DateTime plan_date { get; set; }

        /// <summary>
        /// 第几周
        /// </summary>
        public int? week_no { get; set; }

        /// <summary>
        /// 第几天
        /// </summary>
        public int? day_no { get; set; }

        /// <summary>
        /// 状态: draft/submitted/commented
        /// </summary>
        [MaxLength(20)]
        public string status { get; set; } = "draft";

        /// <summary>
        /// 进度（如：3/6）
        /// </summary>
        [MaxLength(10)]
        public string progress { get; set; } = "0/0";

        /// <summary>
        /// 被点评数量（用于红点提示）
        /// </summary>
        public int comment_count { get; set; } = 0;

        /// <summary>
        /// 提交时间
        /// </summary>
        public DateTime? submit_time { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime create_time { get; set; } = DateTime.Now;
    }
}
