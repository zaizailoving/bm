using BM.Service.Business.IServices;
using BM.Service.Core.DBContext;
using BM.Service.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BM.Service.Business.Services
{
    /// <summary>
    /// 打卡上传服务
    /// </summary>
    public class CheckinService : ICheckinService
    {
        private const int CheckinRewardCoins = 5;
        private const string RewardSourceType = "checkin_reward";

        private static readonly HashSet<string> VideoExts = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".m4v", ".avi", ".webm"
        };

        private static readonly HashSet<string> ImageExts = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
        };

        private readonly SqlDBContext _db;

        public CheckinService(SqlDBContext db)
        {
            _db = db;
        }

        public async Task<(bool ok, string? error, int coinsAwarded, int availableCoins)> UploadAsync(
            int userId,
            int checkinId,
            IFormFile? video,
            IEnumerable<IFormFile>? images,
            string? description,
            string webRootPath)
        {
            if (userId <= 0)
            {
                return (false, "invalid user", 0, 0);
            }

            if (checkinId <= 0)
            {
                return (false, "checkin_id is required", 0, 0);
            }

            var checkin = await _db.GetDbSet<taskCheckinEntity>()
                .FirstOrDefaultAsync(c => c.id == checkinId);
            if (checkin == null)
            {
                return (false, "checkin not found", 0, 0);
            }

            var plan = await _db.GetDbSet<dailyPlanEntity>()
                .FirstOrDefaultAsync(p => p.id == checkin.daily_plan_id);
            if (plan == null || plan.user_id != userId)
            {
                return (false, "no permission for this checkin", 0, 0);
            }

            if (plan.status is "submitted" or "commented")
            {
                return (false, "daily plan already submitted", 0, 0);
            }

            if (checkin.status == "submitted")
            {
                return (false, "checkin already submitted", 0, 0);
            }

            var hasVideo = video != null && video.Length > 0;
            var imageList = images?.Where(f => f != null && f.Length > 0).ToList() ?? new List<IFormFile>();
            var hasImages = imageList.Count > 0;
            var hasDesc = !string.IsNullOrWhiteSpace(description);
            var hasExistingVideo = !string.IsNullOrWhiteSpace(checkin.video_url);
            var hasExistingImages = !string.IsNullOrWhiteSpace(checkin.image_urls);

            // 必须至少有一个图片或视频（本次或已有）
            if (!hasVideo && !hasImages && !hasExistingVideo && !hasExistingImages)
            {
                return (false, "please upload at least one image or video", 0, 0);
            }

            // 本次没有任何新内容也没有描述变更时，若已有媒体可视为成功（前端一般会拦截）
            if (!hasVideo && !hasImages && !hasDesc)
            {
                // 已有媒体：直接返回当前余额，不重复发奖
                var userBal = await _db.GetDbSet<userEntity>()
                    .AsNoTracking()
                    .Where(u => u.id == userId)
                    .Select(u => u.available_coins)
                    .FirstOrDefaultAsync();
                return (true, null, 0, userBal);
            }

            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            }

            Directory.CreateDirectory(webRootPath);

            var relativeDir = Path.Combine("uploads", "checkin", userId.ToString(), checkinId.ToString());
            var absDir = Path.Combine(webRootPath, relativeDir);
            Directory.CreateDirectory(absDir);

            if (hasVideo)
            {
                var ext = Path.GetExtension(video!.FileName);
                if (string.IsNullOrWhiteSpace(ext) || !VideoExts.Contains(ext))
                {
                    return (false, "unsupported video type", 0, 0);
                }

                var fileName = $"video_{DateTime.Now:yyyyMMddHHmmss}{ext.ToLowerInvariant()}";
                var absPath = Path.Combine(absDir, fileName);
                await using (var stream = new FileStream(absPath, FileMode.Create))
                {
                    await video.CopyToAsync(stream);
                }

                checkin.video_url = "/" + Path.Combine(relativeDir, fileName).Replace('\\', '/');
            }

            if (hasImages)
            {
                var savedUrls = new List<string>();
                // 保留已有图片
                if (!string.IsNullOrWhiteSpace(checkin.image_urls))
                {
                    savedUrls.AddRange(
                        checkin.image_urls.Split(new[] { ',', '，', ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => s.Length > 0));
                }

                var index = 0;
                foreach (var img in imageList)
                {
                    var ext = Path.GetExtension(img.FileName);
                    if (string.IsNullOrWhiteSpace(ext) || !ImageExts.Contains(ext))
                    {
                        return (false, $"unsupported image type: {img.FileName}", 0, 0);
                    }

                    index++;
                    var fileName = $"img_{DateTime.Now:yyyyMMddHHmmss}_{index}{ext.ToLowerInvariant()}";
                    var absPath = Path.Combine(absDir, fileName);
                    await using (var stream = new FileStream(absPath, FileMode.Create))
                    {
                        await img.CopyToAsync(stream);
                    }

                    savedUrls.Add("/" + Path.Combine(relativeDir, fileName).Replace('\\', '/'));
                }

                checkin.image_urls = string.Join(",", savedUrls);
            }

            if (hasDesc)
            {
                checkin.description = description!.Trim();
            }

            var wasUnfinished = checkin.status == "unfinished"
                || string.IsNullOrWhiteSpace(checkin.status);
            checkin.status = "uploaded";
            checkin.update_time = DateTime.Now;

            // 刷新日计划进度
            var allCheckins = await _db.GetDbSet<taskCheckinEntity>()
                .Where(c => c.daily_plan_id == plan.id)
                .ToListAsync();
            var total = allCheckins.Count;
            var done = allCheckins.Count(c => c.status is "uploaded" or "submitted");
            plan.progress = $"{done}/{total}";

            // 首次完成该打卡：奖励 5 金币（防重复：同 checkin 已有 checkin_reward 流水则不再发）
            var coinsAwarded = 0;
            var availableCoins = 0;
            var user = await _db.GetDbSet<userEntity>()
                .FirstOrDefaultAsync(u => u.id == userId);
            if (user == null)
            {
                return (false, "user not found", 0, 0);
            }

            availableCoins = user.available_coins;

            if (wasUnfinished)
            {
                var alreadyRewarded = await _db.GetDbSet<coinsLogEntity>()
                    .AnyAsync(l =>
                        l.user_id == userId
                        && l.source_type == RewardSourceType
                        && l.source_id == checkinId);

                if (!alreadyRewarded)
                {
                    user.available_coins += CheckinRewardCoins;
                    user.total_coins += CheckinRewardCoins;
                    availableCoins = user.available_coins;
                    coinsAwarded = CheckinRewardCoins;

                    await _db.GetDbSet<coinsLogEntity>().AddAsync(new coinsLogEntity
                    {
                        user_id = userId,
                        change_amount = CheckinRewardCoins,
                        balance = availableCoins,
                        source_type = RewardSourceType,
                        source_id = checkinId,
                        create_time = DateTime.Now
                    });
                }
            }

            await _db.SaveChangesAsync();
            return (true, null, coinsAwarded, availableCoins);
        }

        /// <summary>
        /// 游戏打卡完成：无需图片/视频，状态改为 uploaded，首次完成 +5 金币
        /// </summary>
        public async Task<(bool ok, string? error, int coinsAwarded, int availableCoins)> CompleteByGameAsync(
            int userId,
            int checkinId,
            string? description)
        {
            if (userId <= 0)
            {
                return (false, "invalid user", 0, 0);
            }

            if (checkinId <= 0)
            {
                return (false, "checkin_id is required", 0, 0);
            }

            var checkin = await _db.GetDbSet<taskCheckinEntity>()
                .FirstOrDefaultAsync(c => c.id == checkinId);
            if (checkin == null)
            {
                return (false, "checkin not found", 0, 0);
            }

            var plan = await _db.GetDbSet<dailyPlanEntity>()
                .FirstOrDefaultAsync(p => p.id == checkin.daily_plan_id);
            if (plan == null || plan.user_id != userId)
            {
                return (false, "no permission for this checkin", 0, 0);
            }

            if (plan.status is "submitted" or "commented")
            {
                return (false, "daily plan already submitted", 0, 0);
            }

            if (checkin.status == "submitted")
            {
                return (false, "checkin already submitted", 0, 0);
            }

            var wasUnfinished = checkin.status == "unfinished"
                || string.IsNullOrWhiteSpace(checkin.status);

            // 已完成：不重复发奖，可更新描述
            if (!string.IsNullOrWhiteSpace(description))
            {
                checkin.description = description.Trim();
            }
            else if (wasUnfinished || string.IsNullOrWhiteSpace(checkin.description))
            {
                checkin.description = "游戏打卡完成（弹唇啵啵操）";
            }

            // 游戏完成视为已有媒体凭证（占位标记，避免后续上传校验）
            if (string.IsNullOrWhiteSpace(checkin.image_urls) && string.IsNullOrWhiteSpace(checkin.video_url))
            {
                checkin.image_urls = "game://bobo-complete";
            }

            checkin.status = "uploaded";
            checkin.update_time = DateTime.Now;

            var allCheckins = await _db.GetDbSet<taskCheckinEntity>()
                .Where(c => c.daily_plan_id == plan.id)
                .ToListAsync();
            var total = allCheckins.Count;
            var done = allCheckins.Count(c => c.status is "uploaded" or "submitted");
            plan.progress = $"{done}/{total}";

            var coinsAwarded = 0;
            var user = await _db.GetDbSet<userEntity>()
                .FirstOrDefaultAsync(u => u.id == userId);
            if (user == null)
            {
                return (false, "user not found", 0, 0);
            }

            var availableCoins = user.available_coins;

            if (wasUnfinished)
            {
                var alreadyRewarded = await _db.GetDbSet<coinsLogEntity>()
                    .AnyAsync(l =>
                        l.user_id == userId
                        && l.source_type == RewardSourceType
                        && l.source_id == checkinId);

                if (!alreadyRewarded)
                {
                    user.available_coins += CheckinRewardCoins;
                    user.total_coins += CheckinRewardCoins;
                    availableCoins = user.available_coins;
                    coinsAwarded = CheckinRewardCoins;

                    await _db.GetDbSet<coinsLogEntity>().AddAsync(new coinsLogEntity
                    {
                        user_id = userId,
                        change_amount = CheckinRewardCoins,
                        balance = availableCoins,
                        source_type = RewardSourceType,
                        source_id = checkinId,
                        create_time = DateTime.Now
                    });
                }
            }

            await _db.SaveChangesAsync();
            return (true, null, coinsAwarded, availableCoins);
        }
    }
}


