using BM.Service.Business.IServices;
using BM.Service.Core.DBContext;
using BM.Service.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BM.Service.Business.Services
{
    /// <summary>
    /// 用户模块服务
    /// </summary>
    public class UserService : IUserService
    {
        private readonly SqlDBContext _db;

        public UserService(SqlDBContext db)
        {
            _db = db;
        }

        public async Task<(UserProfileOutputViewModel? data, string? error)> GetProfileAsync(int userId)
        {
            if (userId <= 0)
            {
                return (null, "invalid user");
            }

            var user = await _db.GetDbSet<userEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.id == userId && u.status == "normal");

            if (user == null)
            {
                return (null, "user not found");
            }

            return (new UserProfileOutputViewModel
            {
                id = user.id,
                nickname = user.nickname,
                avatar = user.avatar ?? string.Empty,
                phone = MaskPhone(user.phone),
                role = user.role,
                archive_no = user.archive_no,
                total_coins = user.total_coins,
                available_coins = user.available_coins,
                train_camp_status = user.train_camp_status
            }, null);
        }

        private static string? MaskPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return phone;
            }

            var p = phone.Trim();
            if (p.Length < 7)
            {
                return p;
            }

            // 138****0000
            return p[..3] + "****" + p[^4..];
        }
    }
}
