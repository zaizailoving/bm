
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BM.Service.Core.JWT;
using BM.Service.Core.Utility;
using System.Linq;

namespace BM.Service.Core.Controller
{
    /// <summary>
    /// base controller
    /// </summary>
    [Authorize]
    [Produces("application/json")]
    public class BaseController : ControllerBase
    {
        /// <summary>
        /// current user
        /// </summary>
        public CurrentUser CurrentUser
        {
            get
            {
                if (User != null && User.Claims.ToList().Count > 0)
                {
                    var Claim = User.Claims.FirstOrDefault(claim => claim.Type == ClaimValueTypes.Json);
                    return Claim == null ? new CurrentUser() : JsonHelper.DeserializeObject<CurrentUser>(Claim.Value);
                }
                else
                {
                    return new CurrentUser();
                }
            }
        }

        public BaseController()
        {
        }
    }
}
