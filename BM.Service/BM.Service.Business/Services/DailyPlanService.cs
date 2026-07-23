using BM.Service.Business.IServices;
using BM.Service.Core.DBContext;
using BM.Service.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BM.Service.Business.Services
{
    /// <summary>
    /// 每日训练计划服务
    /// </summary>
    public class DailyPlanService : IDailyPlanService
    {
        private readonly SqlDBContext _db;

        public DailyPlanService(SqlDBContext db)
        {
            _db = db;
        }

        public async Task<(DailyTodayOutputViewModel? data, string? error)> GetTodayAsync(int userId)
        {
            if (userId <= 0)
            {
                return (null, "invalid user");
            }

            var user = await _db.GetDbSet<userEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.id == userId && u.status == "normal");
            if (user == null)
            {
                return (null, "user not found");
            }

            var today = DateTime.Today;
            var planSet = _db.GetDbSet<dailyPlanEntity>();
            var plan = await planSet.FirstOrDefaultAsync(p => p.user_id == userId && p.plan_date == today);

            if (plan == null)
            {
                var (weekNo, dayNo, taskIds) = await ResolveTrainingDayAsync(user, today);
                plan = new dailyPlanEntity
                {
                    user_id = userId,
                    plan_date = today,
                    week_no = weekNo,
                    day_no = dayNo,
                    status = "draft",
                    progress = "0/0",
                    comment_count = 0,
                    create_time = DateTime.Now
                };
                planSet.Add(plan);
                await _db.SaveChangesAsync();

                await EnsureCheckinsAsync(plan.id, taskIds);
            }
            else
            {
                // 已有计划时补齐缺失打卡行
                var taskIds = await GetTaskIdsForPlanAsync(plan.week_no, plan.day_no);
                await EnsureCheckinsAsync(plan.id, taskIds);
            }

            return (await BuildTodayOutputAsync(plan), null);
        }

        public async Task<(bool ok, string? error)> SubmitAsync(int userId, DailySubmitInputViewModel input)
        {
            if (userId <= 0)
            {
                return (false, "invalid user");
            }

            if (input == null || string.IsNullOrWhiteSpace(input.plan_date))
            {
                return (false, "plan_date is required");
            }

            if (!DateTime.TryParse(input.plan_date.Trim(), out var planDate))
            {
                return (false, "plan_date format invalid, use yyyy-MM-dd");
            }

            planDate = planDate.Date;

            var plan = await _db.GetDbSet<dailyPlanEntity>()
                .FirstOrDefaultAsync(p => p.user_id == userId && p.plan_date == planDate);

            if (plan == null)
            {
                return (false, "daily plan not found");
            }

            if (plan.status == "submitted" || plan.status == "commented")
            {
                return (false, "already submitted");
            }

            var checkinSet = _db.GetDbSet<taskCheckinEntity>();
            var checkins = await checkinSet.Where(c => c.daily_plan_id == plan.id).ToListAsync();
            if (checkins.Count == 0)
            {
                return (false, "no checkin tasks");
            }

            var unfinished = checkins.Count(c => c.status == "unfinished");
            if (unfinished > 0)
            {
                return (false, $"still {unfinished} unfinished task(s)");
            }

            var now = DateTime.Now;
            foreach (var c in checkins)
            {
                if (c.status == "uploaded")
                {
                    c.status = "submitted";
                    c.update_time = now;
                }
            }

            plan.status = "submitted";
            plan.submit_time = now;
            plan.progress = BuildProgress(checkins);

            await _db.SaveChangesAsync();
            return (true, null);
        }

        private async Task<(int weekNo, int dayNo, List<int> taskIds)> ResolveTrainingDayAsync(userEntity user, DateTime today)
        {
            // 按用户创建日相对天数轮询训练方案；无方案时用全部固定任务模板
            var trainingPlans = await _db.GetDbSet<trainingPlanEntity>()
                .AsNoTracking()
                .OrderBy(t => t.week_no)
                .ThenBy(t => t.day_no)
                .ToListAsync();

            if (trainingPlans.Count == 0)
            {
                var allIds = await GetAllFixedTaskTemplateIdsAsync();
                return (1, 1, allIds);
            }

            var dayIndex = Math.Max(0, (today.Date - user.create_time.Date).Days);
            var selected = trainingPlans[dayIndex % trainingPlans.Count];
            var ids = ParseTaskIds(selected.task_ids);
            if (ids.Count == 0)
            {
                ids = await GetAllFixedTaskTemplateIdsAsync();
            }

            return (selected.week_no, selected.day_no, ids);
        }

        private async Task<List<int>> GetTaskIdsForPlanAsync(int? weekNo, int? dayNo)
        {
            if (weekNo == null || dayNo == null)
            {
                return await GetAllFixedTaskTemplateIdsAsync();
            }

            var tp = await _db.GetDbSet<trainingPlanEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.week_no == weekNo && t.day_no == dayNo);

            if (tp == null)
            {
                return await GetAllFixedTaskTemplateIdsAsync();
            }

            var ids = ParseTaskIds(tp.task_ids);
            return ids.Count > 0 ? ids : await GetAllFixedTaskTemplateIdsAsync();
        }

        /// <summary>
        /// 全部任务模板（固定每日任务），按 sort_order 排序
        /// </summary>
        private async Task<List<int>> GetAllFixedTaskTemplateIdsAsync()
        {
            return await _db.GetDbSet<taskTemplateEntity>()
                .AsNoTracking()
                .OrderBy(t => t.sort_order)
                .ThenBy(t => t.id)
                .Select(t => t.id)
                .ToListAsync();
        }


        private async Task EnsureCheckinsAsync(int dailyPlanId, List<int> taskIds)
        {
            if (taskIds.Count == 0)
            {
                return;
            }

            var checkinSet = _db.GetDbSet<taskCheckinEntity>();
            var existing = await checkinSet
                .Where(c => c.daily_plan_id == dailyPlanId)
                .Select(c => c.task_template_id)
                .ToListAsync();

            var now = DateTime.Now;
            var added = false;
            foreach (var taskId in taskIds.Distinct())
            {
                if (existing.Contains(taskId))
                {
                    continue;
                }

                checkinSet.Add(new taskCheckinEntity
                {
                    daily_plan_id = dailyPlanId,
                    task_template_id = taskId,
                    status = "unfinished",
                    comment_id = 0,
                    comment_status = "none",
                    create_time = now,
                    update_time = now
                });
                added = true;
            }

            if (added)
            {
                await _db.SaveChangesAsync();

                // 同步 progress
                var plan = await _db.GetDbSet<dailyPlanEntity>().FirstOrDefaultAsync(p => p.id == dailyPlanId);
                if (plan != null)
                {
                    var checkins = await checkinSet.Where(c => c.daily_plan_id == dailyPlanId).ToListAsync();
                    plan.progress = BuildProgress(checkins);
                    await _db.SaveChangesAsync();
                }
            }
        }

        private async Task<DailyTodayOutputViewModel> BuildTodayOutputAsync(dailyPlanEntity plan)
        {
            var checkins = await _db.GetDbSet<taskCheckinEntity>()
                .AsNoTracking()
                .Where(c => c.daily_plan_id == plan.id)
                .OrderBy(c => c.id)
                .ToListAsync();

            var templateIds = checkins.Select(c => c.task_template_id).Distinct().ToList();
            var templates = await _db.GetDbSet<taskTemplateEntity>()
                .AsNoTracking()
                .Where(t => templateIds.Contains(t.id))
                .ToDictionaryAsync(t => t.id);

            var tasks = new List<DailyTaskItemViewModel>();
            foreach (var c in checkins)
            {
                templates.TryGetValue(c.task_template_id, out var tpl);
                tasks.Add(new DailyTaskItemViewModel
                {
                    checkin_id = c.id,
                    task_id = c.task_template_id,
                    task_name = tpl?.name ?? $"task-{c.task_template_id}",
                    icon_url = tpl?.icon_url ?? string.Empty,
                    requirement = tpl?.requirement ?? string.Empty,
                    teach_video_url = tpl?.teach_video_url ?? string.Empty,
                    status = c.status,
                    video_url = c.video_url ?? string.Empty,
                    image_urls = SplitImageUrls(c.image_urls),
                    description = c.description ?? string.Empty
                });
            }

            // 按模板 sort_order 排序（无模板排后）
            tasks = tasks
                .OrderBy(t => templates.TryGetValue(t.task_id, out var tpl) ? tpl.sort_order : int.MaxValue)
                .ThenBy(t => t.checkin_id)
                .ToList();

            return new DailyTodayOutputViewModel
            {
                plan_date = plan.plan_date.ToString("yyyy-MM-dd"),
                week_no = plan.week_no,
                day_no = plan.day_no,
                status = plan.status,
                progress = BuildProgress(checkins),
                tasks = tasks
            };
        }

        private static string BuildProgress(IReadOnlyList<taskCheckinEntity> checkins)
        {
            var total = checkins.Count;
            var done = checkins.Count(c => c.status is "uploaded" or "submitted");
            return $"{done}/{total}";
        }

        private static List<int> ParseTaskIds(string? taskIds)
        {
            if (string.IsNullOrWhiteSpace(taskIds))
            {
                return new List<int>();
            }

            return taskIds
                .Split(new[] { ',', '，', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }

        private static List<string> SplitImageUrls(string? imageUrls)
        {
            if (string.IsNullOrWhiteSpace(imageUrls))
            {
                return new List<string>();
            }

            return imageUrls
                .Split(new[] { ',', '，', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }
    }
}
