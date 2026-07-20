
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using BM.Service.Core.Models;
using System.Linq;
using BM.Service.Core.Utility;
using BM.Service.Core.JWT;
namespace BM.Service.Core.JWT
{
    /// <summary>
    /// Custom Processing Unit
    /// </summary>
    public class ApiResponseHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {

        /// <summary>
        /// token manager
        /// </summary>
        private readonly ITokenManager _tokenManager;
        /// <summary>
        /// cache manager
        /// </summary>
        private readonly CacheManager _cacheManager;

        /// <summary>
        /// constructor 
        /// </summary>
        /// <param name="options">options</param>
        /// <param name="logger">logger</param>
        /// <param name="encoder">encoder</param>
        /// <param name="clock"></param>
        /// <param name="tokenManager">tokenManager</param>
        /// <param name="cacheManager">cacheManagerparam>
        public ApiResponseHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock
            , ITokenManager tokenManager
            , CacheManager cacheManager)
            : base(options, logger, encoder, clock)
        {
            this._tokenManager = tokenManager;
            _cacheManager = cacheManager;
        }
        /// <summary>
        /// handle authority
        /// </summary>
        /// <returns></returns>
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            return AuthenticateResult.NoResult();
        }
        /// <summary>
        /// authentication
        /// </summary>
        /// <param name="properties">参数</param>
        /// <returns></returns>
        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.ContentType = "application/json";
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsync(JsonHelper.SerializeObject(ResultModel<object>.Error("Sorry, please sign in first!", 401)));
        }
        /// <summary>
        /// access denied
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.ContentType = "application/json";
            Response.StatusCode = StatusCodes.Status403Forbidden;
            await Response.WriteAsync(JsonHelper.SerializeObject(ResultModel<object>.Error("Sorry, you don't have the authority required!", 403)));
        }
    }
}
