using System.Threading.Tasks;
using BM.Service.Core.Models;
using System.Linq;
using BM.Service.Core.Utility;
using Microsoft.EntityFrameworkCore;
using BM.Service.Core.DBContext;
using Microsoft.Extensions.Localization;
using BM.Service.Core.JWT;

namespace BM.Service.Core.Services
{
    /// <summary>
    /// AccountService
    /// </summary>
    public class AccountService : IAccountService
    {
        private readonly SqlDBContext _sqlDBContext;
        private readonly IStringLocalizer<BM.Service.Core.MultiLanguage> _stringLocalizer;

        public AccountService(SqlDBContext sqlDBContext, IStringLocalizer<BM.Service.Core.MultiLanguage> stringLocalizer)
        {
            _sqlDBContext = sqlDBContext;
            _stringLocalizer = stringLocalizer;
        }

        /// <summary>
        /// login
        /// </summary>
        /// <param name="loginInput"> login params viewmodel</param>
        /// <param name="currentUser"> current user</param>
        /// <returns></returns>
        public async Task<LoginOutputViewModel> Login(LoginInputViewModel loginInput, CurrentUser currentUser)
        {
            var users = await _sqlDBContext.GetDbSet<userEntity>()
                .AsNoTracking()
                .Where(user => user.username == loginInput.user_name
                               && user.status == "normal")
                .Select(user => new
                {
                    user_id = user.id,
                    username = user.username,
                    nickname = user.nickname,
                    role = user.role,
                    archive_no = user.archive_no,
                    cipher = user.password_hash
                })
                .ToListAsync();

            string md5_password = Md5Helper.Md5Encrypt32(loginInput.password);
            var result = users.FirstOrDefault(t =>
                t.cipher == md5_password
                || t.cipher == loginInput.password);

            if (result == null)
            {
                return null;
            }

            return new LoginOutputViewModel()
            {
                user_id = result.user_id,
                user_name = result.nickname ?? result.username,
                user_num = result.archive_no ?? result.username,
                user_role = result.role,
                userrole_id = 0,
                tenant_id = 1
            };
        }

        public string HelloWorld()
        {
            return _stringLocalizer["hello word"];
        }
    }
}
