using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BM.Service.Business.IServices;
using BM.Service.Core.Controller;
using BM.Service.Core.Models;

namespace BM.Service.Business.Controllers
{
    /// <summary>
    /// 任务打卡上传
    /// </summary>
    [Route("api/checkin")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "Base")]
    public class CheckinController : BaseController
    {
        private readonly ICheckinService _checkinService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<CheckinController> _logger;

        public CheckinController(
            ICheckinService checkinService,
            IWebHostEnvironment env,
            ILogger<CheckinController> logger)
        {
            _checkinService = checkinService;
            _env = env;
            _logger = logger;
        }

        /// <summary>
        /// 上传打卡内容（视频/图片/描述）
        /// </summary>
        /// <remarks>
        /// POST /api/checkin/upload
        /// Header: Authorization: Bearer {jwt_token}
        /// Content-Type: multipart/form-data
        /// Form fields:
        ///   checkin_id (int, required)
        ///   video (file, optional)
        ///   images (file[], optional)
        ///   description (string, optional)
        /// </remarks>
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(200_000_000)]
        public async Task<ResultModel<object>> Upload(
            [FromForm] int checkin_id,
            [FromForm] IFormFile? video,
            [FromForm] List<IFormFile>? images,
            [FromForm] string? description)
        {
            if (CurrentUser == null || CurrentUser.user_id <= 0)
            {
                return ResultModel<object>.Error("Sorry, please sign in first!", 401);
            }

            var webRoot = _env.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(_env.ContentRootPath ?? AppContext.BaseDirectory, "wwwroot");
            }

            var (ok, error) = await _checkinService.UploadAsync(
                CurrentUser.user_id,
                checkin_id,
                video,
                images,
                description,
                webRoot);

            if (!ok)
            {
                return ResultModel<object>.Error(error ?? "upload failed", 400);
            }

            return ResultModel<object>.Success(new { uploaded = true }, "success");
        }
    }
}
