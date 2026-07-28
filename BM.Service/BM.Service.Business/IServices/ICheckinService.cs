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
        /// 上传打卡内容（至少需要图片或视频；首次完成奖励 5 金币）
        /// </summary>
        Task<(bool ok, string? error, int coinsAwarded, int availableCoins)> UploadAsync(
            int userId,
            int checkinId,
            IFormFile? video,
            IEnumerable<IFormFile>? images,
            string? description,
            string webRootPath);
    }
}
