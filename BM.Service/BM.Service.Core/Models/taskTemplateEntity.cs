using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BM.Service.Core.Models
{
    /// <summary>
    /// 任务模板表
    /// </summary>
    [Table("task_template")]
    public class taskTemplateEntity : BaseModel
    {
        /// <summary>
        /// 动作名称（如：贴闭口贴）
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string name { get; set; } = string.Empty;

        /// <summary>
        /// 图标URL
        /// </summary>
        [MaxLength(255)]
        public string? icon_url { get; set; }

        /// <summary>
        /// 训练要求（如：2组*10min）
        /// </summary>
        [MaxLength(255)]
        public string? requirement { get; set; }

        /// <summary>
        /// 教学视频URL
        /// </summary>
        [MaxLength(255)]
        public string? teach_video_url { get; set; }

        /// <summary>
        /// 排序权重
        /// </summary>
        public int sort_order { get; set; } = 0;
    }
}
