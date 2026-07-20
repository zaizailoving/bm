using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BM.Service.Core.Models
{
    /// <summary>
    /// 师生关系表
    /// </summary>
    [Table("user_relation")]
    [Index(nameof(teacher_id), nameof(student_id), IsUnique = true, Name = "uniq_teacher_student")]
    [Index(nameof(student_id), Name = "idx_student")]
    public class userRelationEntity : BaseModel
    {
        /// <summary>
        /// 老师ID
        /// </summary>
        [Required]
        public int teacher_id { get; set; }

        /// <summary>
        /// 学生ID
        /// </summary>
        [Required]
        public int student_id { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime create_time { get; set; } = DateTime.Now;
    }
}
