using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BM.Service.Core.DBContext;
using BM.Service.Core.Models;
using BM.Service.Core.Utility;

namespace BM.Service
{
    /// <summary>
    /// Database create + base seed (admin role / admin user).
    /// Business-specific seed data should be added here or in BM.Service.Business as needed.
    /// </summary>
    public static class DatabaseInitializer
    {
        private const long TenantId = 1;
        private const string AdminRoleName = "admin";
        private const string AdminUserName = "admin";

        public static void Initialize(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            if (!IsEnabled(configuration["Database:EnsureCreated"], true))
            {
                return;
            }

            var maxRetryCount = GetInt(configuration["Database:InitRetryCount"], 30);
            var retryDelaySeconds = GetInt(configuration["Database:InitRetryDelaySeconds"], 2);

            for (var attempt = 1; attempt <= maxRetryCount; attempt++)
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<SqlDBContext>();
                    dbContext.Database.EnsureCreated();
                    EnsureSchemaCompatibility(dbContext);
                    Seed(dbContext, configuration);
                    return;
                }
                catch when (attempt < maxRetryCount)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(retryDelaySeconds));
                }
            }
        }

        private static void Seed(SqlDBContext dbContext, IConfiguration configuration)
        {
            var now = DateTime.Now;
            var adminPassword = configuration["Seed:AdminPassword"] ?? "1";

            var roleSet = dbContext.GetDbSet<UserroleEntity>();
            var adminRole = roleSet.FirstOrDefault(t => t.tenant_id == TenantId && t.role_name == AdminRoleName);
            if (adminRole == null)
            {
                adminRole = new UserroleEntity
                {
                    role_name = AdminRoleName,
                    is_valid = true,
                    create_time = now,
                    last_update_time = now,
                    tenant_id = TenantId
                };
                roleSet.Add(adminRole);
                dbContext.SaveChanges();
            }

            var userSet = dbContext.GetDbSet<userEntity>();
            if (!userSet.Any(t => t.tenant_id == TenantId && (t.user_name == AdminUserName || t.user_num == AdminUserName)))
            {
                userSet.Add(new userEntity
                {
                    user_num = AdminUserName,
                    user_name = AdminUserName,
                    contact_tel = string.Empty,
                    user_role = AdminRoleName,
                    sex = "male",
                    is_valid = true,
                    auth_string = Md5Helper.Md5Encrypt32(adminPassword),
                    email = string.Empty,
                    creator = AdminUserName,
                    create_time = now,
                    last_update_time = now,
                    tenant_id = TenantId
                });
                dbContext.SaveChanges();
            }
        }

        private static void EnsureSchemaCompatibility(SqlDBContext dbContext)
        {
            if (!dbContext.Database.IsSqlite())
            {
                return;
            }

            foreach (var entityType in dbContext.Model.GetEntityTypes())
            {
                var tableName = entityType.GetTableName();
                if (string.IsNullOrWhiteSpace(tableName))
                {
                    continue;
                }

                var existingColumns = GetSqliteColumns(dbContext, tableName);
                if (existingColumns.Count == 0)
                {
                    continue;
                }

                var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
                foreach (var property in entityType.GetProperties())
                {
                    var columnName = property.GetColumnName(storeObject);
                    if (string.IsNullOrWhiteSpace(columnName) || existingColumns.Contains(columnName))
                    {
                        continue;
                    }

                    EnsureSqliteColumn(dbContext, tableName, columnName, GetSqliteColumnDefinition(property));
                    existingColumns.Add(columnName);
                }
            }
        }

        private static void EnsureSqliteColumn(SqlDBContext dbContext, string tableName, string columnName, string columnDefinition)
        {
            var quotedTableName = QuoteSqliteIdentifier(tableName);
            var quotedColumnName = QuoteSqliteIdentifier(columnName);
            dbContext.Database.ExecuteSqlRaw($"ALTER TABLE {quotedTableName} ADD COLUMN {quotedColumnName} {columnDefinition}");
        }

        private static HashSet<string> GetSqliteColumns(SqlDBContext dbContext, string tableName)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var connection = dbContext.Database.GetDbConnection();
            var shouldClose = connection.State == System.Data.ConnectionState.Closed;
            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"PRAGMA table_info({QuoteSqliteIdentifier(tableName)})";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(reader["name"].ToString() ?? string.Empty);
                }
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }

            return result;
        }

        private static string GetSqliteColumnDefinition(IProperty property)
        {
            var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            var sqliteType = GetSqliteType(type);
            if (property.IsNullable)
            {
                return sqliteType;
            }

            return $"{sqliteType} NOT NULL DEFAULT {GetSqliteDefaultValue(type)}";
        }

        private static string GetSqliteType(Type type)
        {
            if (type == typeof(string) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid) || type == typeof(decimal))
            {
                return "TEXT";
            }

            if (type == typeof(bool) || type == typeof(byte) || type == typeof(short) || type == typeof(int) || type == typeof(long))
            {
                return "INTEGER";
            }

            if (type == typeof(float) || type == typeof(double))
            {
                return "REAL";
            }

            if (type == typeof(byte[]))
            {
                return "BLOB";
            }

            return "TEXT";
        }

        private static string GetSqliteDefaultValue(Type type)
        {
            if (type == typeof(string))
            {
                return "''";
            }

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            {
                return "'0001-01-01 00:00:00'";
            }

            if (type == typeof(Guid))
            {
                return "'00000000-0000-0000-0000-000000000000'";
            }

            if (type == typeof(decimal))
            {
                return "'0.0'";
            }

            if (type == typeof(byte[]))
            {
                return "X''";
            }

            return "0";
        }

        private static string QuoteSqliteIdentifier(string value)
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private static bool IsEnabled(string? value, bool defaultValue)
        {
            return bool.TryParse(value, out var enabled) ? enabled : defaultValue;
        }

        private static int GetInt(string? value, int defaultValue)
        {
            return int.TryParse(value, out var result) && result > 0 ? result : defaultValue;
        }
    }
}
