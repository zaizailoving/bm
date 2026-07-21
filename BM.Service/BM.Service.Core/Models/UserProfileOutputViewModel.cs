namespace BM.Service.Core.Models
{
    /// <summary>
    /// 用户个人信息
    /// </summary>
    public class UserProfileOutputViewModel
    {
        public int id { get; set; }
        public string? nickname { get; set; }
        public string? avatar { get; set; }
        public string? phone { get; set; }
        public string role { get; set; } = "student";
        public string? archive_no { get; set; }
        public int total_coins { get; set; }
        public int available_coins { get; set; }
        public string train_camp_status { get; set; } = "ongoing";
    }
}
