using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BM.Service.Core.Models
{
    /// <summary>
    /// 总训练方案表
    /// </summary>
    [Table("training_plan")]
    [Index(nameof(week_no), nameof(day_no), IsUnique = true, Name = "uniq_week_day")]
    public class trainingPlanEntity : BaseModel
    {
        /// <summary>
        /// 第几周
        /// </summary>
        [Required]
        public int week_no { get; set; }

        /// <summary>
        /// 第几天
        /// </summary>
        [Required]
        public int day_no { get; set; }

        /// <summary>
        /// 当日任务ID集合，逗号分隔（如：1,2,3）
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string task_ids { get; set; } = string.Empty;
    }
}
