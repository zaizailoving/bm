using System.ComponentModel.DataAnnotations;

namespace BM.Service.Core.Models
{
    /// <summary>
    /// 注册请求
    /// </summary>
    public class RegisterInputViewModel
    {
        /// <summary>
        /// 登录用户名（唯一）
        /// </summary>
        [Required(ErrorMessage = "Required")]
        [Display(Name = "username")]
        [MaxLength(50, ErrorMessage = "MaxLength")]
        [MinLength(3, ErrorMessage = "MinLength")]
        public string username { get; set; } = string.Empty;

        /// <summary>
        /// 密码
        /// </summary>
        [Required(ErrorMessage = "Required")]
        [Display(Name = "password")]
        [MaxLength(64, ErrorMessage = "MaxLength")]
        [MinLength(6, ErrorMessage = "MinLength")]
        public string password { get; set; } = string.Empty;

        /// <summary>
        /// 昵称（可选）
        /// </summary>
        [MaxLength(50, ErrorMessage = "MaxLength")]
        public string? nickname { get; set; }

        /// <summary>
        /// 手机号（可选，唯一）
        /// </summary>
        [MaxLength(20, ErrorMessage = "MaxLength")]
        public string? phone { get; set; }

        /// <summary>
        /// 角色: student/teacher（默认 student；admin 不允许通过接口注册）
        /// </summary>
        [MaxLength(20, ErrorMessage = "MaxLength")]
        public string? role { get; set; }
    }
}
