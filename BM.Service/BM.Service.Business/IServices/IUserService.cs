using BM.Service.Core.DI;
using BM.Service.Core.Models;

namespace BM.Service.Business.IServices
{
    /// <summary>
    /// 用户模块
    /// </summary>
    public interface IUserService : IDependency
    {
        /// <summary>
        /// 获取当前登录用户个人信息
        /// </summary>
        Task<(UserProfileOutputViewModel? data, string? error)> GetProfileAsync(int userId);
    }
}
