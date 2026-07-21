using System.Threading.Tasks;
using BM.Service.Core.JWT;
using BM.Service.Core.Models;

namespace BM.Service.Core.Services
{
    /// <summary>
    /// account service interface
    /// </summary>
    public interface IAccountService
    {
        /// <summary>
        /// 登录
        /// </summary>
        Task<LoginOutputViewModel> Login(LoginInputViewModel loginInput, CurrentUser currentUser, string? clientIp = null);

        /// <summary>
        /// 注册
        /// </summary>
        Task<(RegisterOutputViewModel? data, string? error)> Register(RegisterInputViewModel input);

        /// <summary>
        /// 修改密码
        /// </summary>
        Task<(bool ok, string? error)> ChangePassword(int userId, ChangePasswordInputViewModel input);

        string HelloWorld();
    }
}
