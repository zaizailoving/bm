using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BM.Service.Core.Models
{
    /// <summary>
    /// 用户表
    /// </summary>
    [Table("user")]
    [Index(nameof(username), IsUnique = true, Name = "uniq_username")]
    [Index(nameof(phone), IsUnique = true, Name = "uniq_phone")]
    [Index(nameof(archive_no), IsUnique = true, Name = "uniq_archive_no")]
    public class userEntity : BaseModel

    {
        /// <summary>
        /// 登录用户名（唯一）
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string username { get; set; } = string.Empty;

        /// <summary>
        /// 加密后的密码（建议 bcrypt，当前项目登录兼容 MD5）
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string password_hash { get; set; } = string.Empty;

        /// <summary>
        /// 昵称（如：星星）
        /// </summary>
        [MaxLength(50)]
        public string? nickname { get; set; }

        /// <summary>
        /// 头像 URL
        /// </summary>
        [MaxLength(255)]
        public string? avatar { get; set; }

        /// <summary>
        /// 手机号（用于找回密码）
        /// </summary>
        [MaxLength(20)]
        public string? phone { get; set; }

        /// <summary>
        /// 角色: student/teacher/admin
        /// </summary>
        [MaxLength(20)]
        public string role { get; set; } = "student";

        /// <summary>
        /// 档案编号（如：S13-126）
        /// </summary>
        [MaxLength(20)]
        public string? archive_no { get; set; }

        /// <summary>
        /// 训练营状态: ongoing/finished/paused
        /// </summary>
        [MaxLength(20)]
        public string train_camp_status { get; set; } = "ongoing";

        /// <summary>
        /// 累计金币
        /// </summary>
        public int total_coins { get; set; } = 0;

        /// <summary>
        /// 可用金币
        /// </summary>
        public int available_coins { get; set; } = 0;

        /// <summary>
        /// 最近登录时间
        /// </summary>
        public DateTime? last_login_time { get; set; }

        /// <summary>
        /// 最近登录 IP
        /// </summary>
        [MaxLength(45)]
        public string? last_login_ip { get; set; }

        /// <summary>
        /// 账号状态: normal/disabled
        /// </summary>
        [MaxLength(20)]
        public string status { get; set; } = "normal";

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime create_time { get; set; } = DateTime.Now;
    }
}
