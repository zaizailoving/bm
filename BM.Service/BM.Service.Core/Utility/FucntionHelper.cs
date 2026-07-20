using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using BM.Service.Core.DBContext;
using BM.Service.Core.JWT;
using BM.Service.Core.Models;
using BM.Service.Core.Utility;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;

namespace BM.Service.Core
{
    public class FunctionHelper
    {
        private readonly SqlDBContext _dBContext;
        private readonly IHttpContextAccessor _accessor;
        private readonly IOptions<TokenSettings> _tokenSettings;

        public FunctionHelper(SqlDBContext dBContext
             , IHttpContextAccessor accessor
             , IOptions<TokenSettings> tokenSettings)
        {
            _dBContext = dBContext;
            _accessor = accessor;
            _tokenSettings = tokenSettings;
        }

        /// <summary>
        /// Get the current user information in the token
        /// </summary>
        /// <returns></returns>
        public CurrentUser GetCurrentUser()
        {
            if (_accessor.HttpContext == null)
            {
                return new CurrentUser();
            }
            var token = _accessor.HttpContext.Request.Headers["Authorization"].ObjToString();
            if (!token.StartsWith("Bearer"))
            {
                return new CurrentUser();
            }
            token = token.Replace("Bearer ", "");
            if (token.Length > 0)
            {
                try
                {
                    var principal = new JwtSecurityTokenHandler().ValidateToken(token,
                                                                            new TokenValidationParameters
                                                                            {
                                                                                ValidateAudience = false,
                                                                                ValidateIssuer = false,
                                                                                ValidateIssuerSigningKey = true,
                                                                                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_tokenSettings.Value.SigningKey)),
                                                                                ValidateLifetime = false
                                                                            },
                                                                            out var securityToken);

                    if (!(securityToken is JwtSecurityToken jwtSecurityToken) ||
                        !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return new CurrentUser();
                    }
                    var userClaim = principal.Claims.FirstOrDefault(claim => claim.Type == ClaimValueTypes.Json);
                    var user = userClaim == null ? null : JsonHelper.DeserializeObject<CurrentUser>(userClaim.Value);
                    if (user != null)
                    {
                        return user;
                    }
                    else
                    {
                        return new CurrentUser();
                    }
                }
                catch
                {
                    return new CurrentUser();
                }
            }
            else
            {
                return new CurrentUser();
            }
        }
    }
}
