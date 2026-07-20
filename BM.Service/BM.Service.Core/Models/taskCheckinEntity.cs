using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BM.Service.Core.Models
{
    /// <summary>
    /// 任务打卡表
    /// </summary>
    [Table("task_checkin")]
    [Index(nameof(daily_plan_id), Name = "idx_daily_plan")]
    public class taskCheckinEntity : BaseModel
    {
        /// <summary>
        /// 关联每日计划ID
        /// </summary>
        [Required]
        public int daily_plan_id { get; set; }

        /// <summary>
        /// 关联任务模板ID
        /// </summary>
        [Required]
        public int task_template_id { get; set; }

        /// <summary>
        /// 状态: unfinished/uploaded/submitted
        /// </summary>
        [MaxLength(20)]
        public string status { get; set; } = "unfinished";

        /// <summary>
        /// 视频URL
        /// </summary>
        [MaxLength(255)]
        public string? video_url { get; set; }

        /// <summary>
        /// 图片URL集合，逗号分隔
        /// </summary>
        public string? image_urls { get; set; }

        /// <summary>
        /// 关联点评ID
        /// </summary>
        public int comment_id { get; set; } = 0;

        /// <summary>
        /// 点评状态: none/completed/replied
        /// </summary>
        [MaxLength(20)]
        public string comment_status { get; set; } = "none";

        /// <summary>
        /// 文字描述（训练感受）
        /// </summary>
        public string? description { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime create_time { get; set; } = DateTime.Now;

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime update_time { get; set; } = DateTime.Now;
    }
}
