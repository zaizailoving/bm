using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using BM.Service.Core.JWT;
using BM.Service.Core.Models;
using BM.Service.Core.Services;

namespace BM.Service.Core.Controller
{
    /// <summary>
    /// 认证模块
    /// </summary>
    [Route("api/auth")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "Base")]
    public class AuthController : BaseController
    {
        private readonly ITokenManager _tokenManager;
        private readonly ILogger<AuthController> _logger;
        private readonly CacheManager _cacheManager;
        private readonly IAccountService _accountService;
        private readonly IStringLocalizer _stringLocalizer;

        public AuthController(
            ILogger<AuthController> logger,
            ITokenManager tokenManager,
            CacheManager cacheManager,
            IAccountService accountService,
            IStringLocalizer stringLocalizer)
        {
            _logger = logger;
            _tokenManager = tokenManager;
            _cacheManager = cacheManager;
            _accountService = accountService;
            _stringLocalizer = stringLocalizer;
        }

        /// <summary>
        /// 用户注册
        /// </summary>
        /// <remarks>
        /// POST /api/auth/register
        /// Header: Content-Type: application/json
        /// </remarks>
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ResultModel<RegisterOutputViewModel>> Register([FromBody] RegisterInputViewModel input)
        {
            var (data, error) = await _accountService.Register(input);
            if (data == null)
            {
                return ResultModel<RegisterOutputViewModel>.Error(error ?? "register failed", 400);
            }
            return ResultModel<RegisterOutputViewModel>.Success(data, "success");
        }

        /// <summary>
        /// 用户登录
        /// </summary>
        /// <remarks>
        /// POST /api/auth/login
        /// Header: Content-Type: application/json
        /// Body: { "user_name": "admin", "password": "1" }
        /// </remarks>
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ResultModel<LoginOutputViewModel>> Login([FromBody] LoginInputViewModel loginAccount)
        {
            var clientIp = GetClientIp();
            var user = await _accountService.Login(loginAccount, CurrentUser, clientIp);
            if (user == null)
            {
                return ResultModel<LoginOutputViewModel>.Error(_stringLocalizer["login_failed"] ?? "login failed", 401);
            }

            var tokenResult = _tokenManager.GenerateToken(new CurrentUser
            {
                user_id = user.user_id,
                user_name = user.user_name,
                user_num = user.user_num,
                user_role = user.user_role,
                tenant_id = user.tenant_id
            });
            var refreshToken = _tokenManager.GenerateRefreshToken();

            user.access_token = tokenResult.token;
            user.expire = tokenResult.expire;
            user.refresh_token = refreshToken;

            await _cacheManager.TokenSet(user.user_id, "WebRefreshToken", refreshToken, _tokenManager.GetRefreshTokenExpireMinute());

            return ResultModel<LoginOutputViewModel>.Success(user, "success");
        }

        /// <summary>
        /// 修改密码（需登录）
        /// </summary>
        /// <remarks>
        /// POST /api/auth/change-password
        /// Header:
        ///   Content-Type: application/json
        ///   Authorization: Bearer {jwt_token}
        /// Body: { "old_password": "...", "new_password": "..." }
        /// </remarks>
        [HttpPost("change-password")]
        public async Task<ResultModel<object>> ChangePassword([FromBody] ChangePasswordInputViewModel input)
        {
            if (CurrentUser == null || CurrentUser.user_id <= 0)
            {
                return ResultModel<object>.Error("Sorry, please sign in first!", 401);
            }

            var (ok, error) = await _accountService.ChangePassword(CurrentUser.user_id, input);
            if (!ok)
            {
                return ResultModel<object>.Error(error ?? "change password failed", 400);
            }

            // 改密后清理 refresh token，强制重新登录刷新会话（可选）
            try
            {
                await _cacheManager.TokenSet(CurrentUser.user_id, "WebRefreshToken", string.Empty, 1);
            }
            catch
            {
                // ignore cache cleanup failure
            }

            return ResultModel<object>.Success(new { changed = true }, "success");
        }

        private string? GetClientIp()
        {
            var ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(ip))
            {
                ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            }
            else
            {
                // 取第一个
                var comma = ip.IndexOf(',');
                if (comma > 0)
                {
                    ip = ip[..comma].Trim();
                }
            }
            return ip;
        }
    }
}
