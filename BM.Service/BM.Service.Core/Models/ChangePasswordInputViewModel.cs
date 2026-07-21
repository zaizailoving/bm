using System.ComponentModel.DataAnnotations;

namespace BM.Service.Core.Models
{
    /// <summary>
    /// 修改密码请求
    /// </summary>
    public class ChangePasswordInputViewModel
    {
        /// <summary>
        /// 原密码
        /// </summary>
        [Required(ErrorMessage = "Required")]
        [Display(Name = "old_password")]
        [MaxLength(64, ErrorMessage = "MaxLength")]
        public string old_password { get; set; } = string.Empty;

        /// <summary>
        /// 新密码
        /// </summary>
        [Required(ErrorMessage = "Required")]
        [Display(Name = "new_password")]
        [MaxLength(64, ErrorMessage = "MaxLength")]
        [MinLength(6, ErrorMessage = "MinLength")]
        public string new_password { get; set; } = string.Empty;
    }
}
