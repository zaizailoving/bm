using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BM.Service.Core.Models
{
    /// <summary>
    /// 金币流水表
    /// </summary>
    [Table("coins_log")]
    [Index(nameof(user_id), nameof(create_time), Name = "idx_user_time")]
    public class coinsLogEntity : BaseModel
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        [Required]
        public int user_id { get; set; }

        /// <summary>
        /// 变动数量（+10/-5）
        /// </summary>
        [Required]
        public int change_amount { get; set; }

        /// <summary>
        /// 变动后余额
        /// </summary>
        [Required]
        public int balance { get; set; }

        /// <summary>
        /// 来源: checkin_reward/comment_reward/exchange
        /// </summary>
        [MaxLength(50)]
        public string? source_type { get; set; }

        /// <summary>
        /// 关联ID（如打卡ID）
        /// </summary>
        public int? source_id { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime create_time { get; set; } = DateTime.Now;
    }
}
