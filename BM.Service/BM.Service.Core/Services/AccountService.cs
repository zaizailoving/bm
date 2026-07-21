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
        /// 登录
        /// </summary>
        public async Task<LoginOutputViewModel> Login(LoginInputViewModel loginInput, CurrentUser currentUser, string? clientIp = null)
        {
            var username = (loginInput.user_name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(loginInput.password))
            {
                return null;
            }

            var user = await _sqlDBContext.GetDbSet<userEntity>()
                .FirstOrDefaultAsync(u => u.username == username && u.status == "normal");

            if (user == null)
            {
                return null;
            }

            var md5Password = Md5Helper.Md5Encrypt32(loginInput.password);
            var passwordOk = user.password_hash == md5Password
                             || user.password_hash == loginInput.password;
            if (!passwordOk)
            {
                return null;
            }

            user.last_login_time = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(clientIp))
            {
                user.last_login_ip = clientIp.Length > 45 ? clientIp[..45] : clientIp;
            }
            await _sqlDBContext.SaveChangesAsync();

            return new LoginOutputViewModel()
            {
                user_id = user.id,
                user_name = user.nickname ?? user.username,
                user_num = user.archive_no ?? user.username,
                user_role = user.role,
                userrole_id = 0,
                tenant_id = 1
            };
        }

        /// <summary>
        /// 注册
        /// </summary>
        public async Task<(RegisterOutputViewModel? data, string? error)> Register(RegisterInputViewModel input)
        {
            var username = (input.username ?? string.Empty).Trim();
            var password = input.password ?? string.Empty;
            var phone = string.IsNullOrWhiteSpace(input.phone) ? null : input.phone.Trim();
            var nickname = string.IsNullOrWhiteSpace(input.nickname) ? username : input.nickname.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                return (null, "username is required");
            }
            if (password.Length < 6)
            {
                return (null, "password must be at least 6 characters");
            }

            var role = string.IsNullOrWhiteSpace(input.role) ? "student" : input.role.Trim().ToLowerInvariant();
            if (role is not ("student" or "teacher"))
            {
                // 禁止通过公开接口注册 admin
                return (null, "role must be student or teacher");
            }

            var userSet = _sqlDBContext.GetDbSet<userEntity>();
            if (await userSet.AnyAsync(u => u.username == username))
            {
                return (null, "username already exists");
            }
            if (!string.IsNullOrEmpty(phone) && await userSet.AnyAsync(u => u.phone == phone))
            {
                return (null, "phone already exists");
            }

            var entity = new userEntity
            {
                username = username,
                password_hash = Md5Helper.Md5Encrypt32(password),
                nickname = nickname,
                phone = phone,
                role = role,
                train_camp_status = "ongoing",
                total_coins = 0,
                available_coins = 0,
                status = "normal",
                create_time = DateTime.Now
            };

            userSet.Add(entity);
            await _sqlDBContext.SaveChangesAsync();

            return (new RegisterOutputViewModel
            {
                user_id = entity.id,
                username = entity.username,
                nickname = entity.nickname,
                role = entity.role,
                phone = entity.phone,
                archive_no = entity.archive_no
            }, null);
        }

        /// <summary>
        /// 修改密码
        /// </summary>
        public async Task<(bool ok, string? error)> ChangePassword(int userId, ChangePasswordInputViewModel input)
        {
            if (userId <= 0)
            {
                return (false, "invalid user");
            }
            if (string.IsNullOrWhiteSpace(input.old_password) || string.IsNullOrWhiteSpace(input.new_password))
            {
                return (false, "password is required");
            }
            if (input.new_password.Length < 6)
            {
                return (false, "new password must be at least 6 characters");
            }
            if (input.old_password == input.new_password)
            {
                return (false, "new password must be different from old password");
            }

            var user = await _sqlDBContext.GetDbSet<userEntity>()
                .FirstOrDefaultAsync(u => u.id == userId && u.status == "normal");
            if (user == null)
            {
                return (false, "user not found");
            }

            var oldMd5 = Md5Helper.Md5Encrypt32(input.old_password);
            var oldOk = user.password_hash == oldMd5 || user.password_hash == input.old_password;
            if (!oldOk)
            {
                return (false, "old password is incorrect");
            }

            user.password_hash = Md5Helper.Md5Encrypt32(input.new_password);
            await _sqlDBContext.SaveChangesAsync();
            return (true, null);
        }

        public string HelloWorld()
        {
            return _stringLocalizer["hello word"];
        }
    }
}
