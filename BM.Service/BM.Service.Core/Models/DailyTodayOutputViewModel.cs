namespace BM.Service.Core.Models
{
    /// <summary>
    /// 今日训练计划
    /// </summary>
    public class DailyTodayOutputViewModel
    {
        /// <summary>
        /// 计划日期 yyyy-MM-dd
        /// </summary>
        public string plan_date { get; set; } = string.Empty;

        public int? week_no { get; set; }
        public int? day_no { get; set; }

        /// <summary>
        /// draft / submitted / commented
        /// </summary>
        public string status { get; set; } = "draft";

        /// <summary>
        /// 进度，如 2/6
        /// </summary>
        public string progress { get; set; } = "0/0";

        public List<DailyTaskItemViewModel> tasks { get; set; } = new();
    }

    /// <summary>
    /// 今日计划中的单条任务打卡
    /// </summary>
    public class DailyTaskItemViewModel
    {
        public int checkin_id { get; set; }
        public int task_id { get; set; }
        public string task_name { get; set; } = string.Empty;
        public string? icon_url { get; set; }
        public string? requirement { get; set; }
        public string? teach_video_url { get; set; }

        /// <summary>
        /// unfinished / uploaded / submitted
        /// </summary>
        public string status { get; set; } = "unfinished";

        public string? video_url { get; set; }

        /// <summary>
        /// 图片 URL 列表
        /// </summary>
        public List<string> image_urls { get; set; } = new();

        public string? description { get; set; }
    }
}
