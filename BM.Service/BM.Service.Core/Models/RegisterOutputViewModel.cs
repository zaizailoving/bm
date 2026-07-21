namespace BM.Service.Core.Models
{
    /// <summary>
    /// 注册成功返回
    /// </summary>
    public class RegisterOutputViewModel
    {
        public int user_id { get; set; }
        public string username { get; set; } = string.Empty;
        public string? nickname { get; set; }
        public string role { get; set; } = "student";
        public string? phone { get; set; }
        public string? archive_no { get; set; }
    }
}
