using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BM.Service.Core.Models
{
    /// <summary>
    /// 点评记录表
    /// </summary>
    [Table("comment_record")]
    [Index(nameof(task_checkin_id), Name = "idx_checkin")]
    public class commentRecordEntity : BaseModel
    {
        /// <summary>
        /// 关联打卡ID
        /// </summary>
        [Required]
        public int task_checkin_id { get; set; }

        /// <summary>
        /// 老师ID
        /// </summary>
        [Required]
        public int teacher_id { get; set; }

        /// <summary>
        /// 星级评分（1-5星）
        /// </summary>
        public byte rating { get; set; } = 0;

        /// <summary>
        /// 点评内容
        /// </summary>
        public string? content { get; set; }

        /// <summary>
        /// 家长回复内容
        /// </summary>
        public string? reply_content { get; set; }

        /// <summary>
        /// 回复时间
        /// </summary>
        public DateTime? reply_time { get; set; }

        /// <summary>
        /// 状态: completed/replied
        /// </summary>
        [MaxLength(20)]
        public string status { get; set; } = "completed";

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime create_time { get; set; } = DateTime.Now;
    }
}
