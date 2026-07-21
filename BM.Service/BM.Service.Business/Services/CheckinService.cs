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

        public async Task<(bool ok, string? error)> UploadAsync(
            int userId,
            int checkinId,
            IFormFile? video,
            IEnumerable<IFormFile>? images,
            string? description,
            string webRootPath)
        {
            if (userId <= 0)
            {
                return (false, "invalid user");
            }

            if (checkinId <= 0)
            {
                return (false, "checkin_id is required");
            }

            var checkin = await _db.GetDbSet<taskCheckinEntity>()
                .FirstOrDefaultAsync(c => c.id == checkinId);
            if (checkin == null)
            {
                return (false, "checkin not found");
            }

            var plan = await _db.GetDbSet<dailyPlanEntity>()
                .FirstOrDefaultAsync(p => p.id == checkin.daily_plan_id);
            if (plan == null || plan.user_id != userId)
            {
                return (false, "no permission for this checkin");
            }

            if (plan.status is "submitted" or "commented")
            {
                return (false, "daily plan already submitted");
            }

            if (checkin.status == "submitted")
            {
                return (false, "checkin already submitted");
            }

            var hasVideo = video != null && video.Length > 0;
            var imageList = images?.Where(f => f != null && f.Length > 0).ToList() ?? new List<IFormFile>();
            var hasImages = imageList.Count > 0;
            var hasDesc = !string.IsNullOrWhiteSpace(description);

            if (!hasVideo && !hasImages && !hasDesc && string.IsNullOrWhiteSpace(checkin.video_url) && string.IsNullOrWhiteSpace(checkin.image_urls))
            {
                return (false, "please upload video/images or description");
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
                    return (false, "unsupported video type");
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
                        return (false, $"unsupported image type: {img.FileName}");
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

            checkin.status = "uploaded";
            checkin.update_time = DateTime.Now;

            // 刷新日计划进度
            var allCheckins = await _db.GetDbSet<taskCheckinEntity>()
                .Where(c => c.daily_plan_id == plan.id)
                .ToListAsync();
            var total = allCheckins.Count;
            var done = allCheckins.Count(c => c.id == checkin.id || c.status is "uploaded" or "submitted");
            // checkin 尚未 Save，手动计 1 次当前
            if (!allCheckins.Any(c => c.id == checkin.id && c.status is "uploaded" or "submitted"))
            {
                // 当前实体已是 uploaded，Count 已包含（同一 DbSet 跟踪）
            }

            done = allCheckins.Count(c => c.status is "uploaded" or "submitted");
            plan.progress = $"{done}/{total}";

            await _db.SaveChangesAsync();
            return (true, null);
        }
    }
}
