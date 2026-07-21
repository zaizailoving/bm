using BM.Service.Core.DI;
using Microsoft.AspNetCore.Http;

namespace BM.Service.Business.IServices
{
    /// <summary>
    /// 任务打卡上传
    /// </summary>
    public interface ICheckinService : IDependency
    {
        /// <summary>
        /// 上传打卡内容（视频/图片/描述）
        /// </summary>
        Task<(bool ok, string? error)> UploadAsync(
            int userId,
            int checkinId,
            IFormFile? video,
            IEnumerable<IFormFile>? images,
            string? description,
            string webRootPath);
    }
}
