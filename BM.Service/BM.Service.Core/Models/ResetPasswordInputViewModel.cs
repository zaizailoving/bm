using System.ComponentModel.DataAnnotations;

namespace BM.Service.Core.Models
{
    public class ResetPasswordInputViewModel
    {
        [Required]
        [MaxLength(50)]
        public string username { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string phone { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        [MaxLength(64)]
        public string new_password { get; set; } = string.Empty;
    }
}
