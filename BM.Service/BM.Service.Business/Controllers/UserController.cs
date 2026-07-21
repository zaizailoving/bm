using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BM.Service.Business.IServices;
using BM.Service.Core.Controller;
using BM.Service.Core.Models;

namespace BM.Service.Business.Controllers
{
    /// <summary>
    /// 用户模块
    /// </summary>
    [Route("api/user")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "Base")]
    public class UserController : BaseController
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, ILogger<UserController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        /// <summary>
        /// 获取当前登录用户个人信息
        /// </summary>
        /// <remarks>
        /// GET /api/user/profile
        /// Header: Authorization: Bearer {jwt_token}
        /// </remarks>
        [HttpGet("profile")]
        public async Task<ResultModel<UserProfileOutputViewModel>> GetProfile()
        {
            if (CurrentUser == null || CurrentUser.user_id <= 0)
            {
                return ResultModel<UserProfileOutputViewModel>.Error("Sorry, please sign in first!", 401);
            }

            var (data, error) = await _userService.GetProfileAsync(CurrentUser.user_id);
            if (data == null)
            {
                return ResultModel<UserProfileOutputViewModel>.Error(error ?? "get profile failed", 400);
            }

            return ResultModel<UserProfileOutputViewModel>.Success(data, "success");
        }
    }
}
