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
    /// Database create + base seed (admin user).
    /// Business-specific seed data should be added here or in BM.Service.Business as needed.
    /// </summary>
    public static class DatabaseInitializer
    {
        private const string AdminRoleName = "admin";
        private const string AdminUserName = "admin";

        public static void Initialize(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            if (!IsEnabled(configuration["Database:EnsureCreated"], true))
            {
                Console.WriteLine("[DatabaseInitializer] Database:EnsureCreated=false，跳过建表。");
                return;
            }

            // 本地开发默认少重试；Docker/编排环境可在 appsettings 调高
            var maxRetryCount = GetInt(configuration["Database:InitRetryCount"], 3);
            var retryDelaySeconds = GetInt(configuration["Database:InitRetryDelaySeconds"], 1);
            Exception? lastError = null;

            for (var attempt = 1; attempt <= maxRetryCount; attempt++)
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<SqlDBContext>();
                    Console.WriteLine($"[DatabaseInitializer] 连接数据库中... ({attempt}/{maxRetryCount})");
                    dbContext.Database.EnsureCreated();
                    EnsureSchemaCompatibility(dbContext);
                    Seed(dbContext, configuration);
                    Console.WriteLine("[DatabaseInitializer] 数据库表结构已就绪。");
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Console.WriteLine($"[DatabaseInitializer] 失败(第 {attempt} 次): {ex.Message}");

                    // 登录失败/配置错误等不应无重试，避免启动卡死
                    if (IsNonRetriableDbError(ex) || attempt >= maxRetryCount)
                    {
                        break;
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(retryDelaySeconds));
                }
            }

            Console.WriteLine($"[DatabaseInitializer] 建表最终失败，启动将继续但数据库可能不可用: {lastError?.Message}");
        }

        private static bool IsNonRetriableDbError(Exception ex)
        {
            // 密码错误、登录失败、库不存在、证书/权限等问题：重试无意义
            for (var e = ex; e != null; e = e.InnerException)
            {
                var msg = e.Message ?? string.Empty;
                if (msg.Contains("Login failed", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("login failed", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Cannot open database", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("A network-related or instance-specific error", StringComparison.OrdinalIgnoreCase) == false
                       && msg.Contains("password", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("用户", StringComparison.OrdinalIgnoreCase) && msg.Contains("登录失败", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("登录失败", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // SqlException Number: 18456 login failed, 4060 cannot open database
                if (e.GetType().Name.Contains("SqlException", StringComparison.OrdinalIgnoreCase))
                {
                    var numberProp = e.GetType().GetProperty("Number");
                    if (numberProp?.GetValue(e) is int number && (number == 18456 || number == 4060 || number == 18470 || number == 18487 || number == 18488))
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        private static void Seed(SqlDBContext dbContext, IConfiguration configuration)
        {
            var now = DateTime.Now;
            var adminPassword = configuration["Seed:AdminPassword"] ?? "1";

            var userSet = dbContext.GetDbSet<userEntity>();
            if (!userSet.Any(t => t.username == AdminUserName))
            {
                userSet.Add(new userEntity
                {
                    username = AdminUserName,
                    password_hash = Md5Helper.Md5Encrypt32(adminPassword),
                    nickname = AdminUserName,
                    avatar = null,
                    phone = null,
                    role = AdminRoleName,
                    archive_no = null,
                    train_camp_status = "ongoing",
                    total_coins = 0,
                    available_coins = 0,
                    last_login_time = null,
                    last_login_ip = null,
                    status = "normal",
                    create_time = now
                });
                dbContext.SaveChanges();
            }
        }

        private static void EnsureSchemaCompatibility(SqlDBContext dbContext)
        {
            // 库已存在时 EnsureCreated 不会建新表，这里按模型补建缺失表/列
            if (dbContext.Database.IsSqlite())
            {
                EnsureSqliteSchema(dbContext);
                return;
            }

            if (dbContext.Database.IsSqlServer())
            {
                EnsureSqlServerSchema(dbContext);
            }
        }

        private static void EnsureSqliteSchema(SqlDBContext dbContext)
        {
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
                    EnsureSqliteTable(dbContext, entityType, tableName);
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

        private static void EnsureSqlServerSchema(SqlDBContext dbContext)
        {
            foreach (var entityType in dbContext.Model.GetEntityTypes())
            {
                var tableName = entityType.GetTableName();
                if (string.IsNullOrWhiteSpace(tableName))
                {
                    continue;
                }

                var schema = entityType.GetSchema() ?? "dbo";
                var existingColumns = GetSqlServerColumns(dbContext, schema, tableName);
                if (existingColumns.Count == 0)
                {
                    EnsureSqlServerTable(dbContext, entityType, schema, tableName);
                    continue;
                }

                var storeObject = StoreObjectIdentifier.Table(tableName, schema);
                foreach (var property in entityType.GetProperties())
                {
                    var columnName = property.GetColumnName(storeObject);
                    if (string.IsNullOrWhiteSpace(columnName) || existingColumns.Contains(columnName))
                    {
                        continue;
                    }

                    EnsureSqlServerColumn(dbContext, schema, tableName, columnName, GetSqlServerColumnDefinition(property));
                    existingColumns.Add(columnName);
                }
            }
        }

        private static void EnsureSqliteTable(SqlDBContext dbContext, IEntityType entityType, string tableName)
        {
            var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            var columnDefs = new List<string>();

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(storeObject);
                if (string.IsNullOrWhiteSpace(columnName))
                {
                    continue;
                }

                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                var sqliteType = GetSqliteType(type);
                var isPk = property.IsPrimaryKey();
                var nullable = property.IsNullable && !isPk;

                if (isPk && (type == typeof(int) || type == typeof(long)))
                {
                    columnDefs.Add($"{QuoteSqliteIdentifier(columnName)} INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT");
                    continue;
                }

                if (isPk)
                {
                    columnDefs.Add($"{QuoteSqliteIdentifier(columnName)} {sqliteType} NOT NULL PRIMARY KEY");
                    continue;
                }

                if (nullable)
                {
                    columnDefs.Add($"{QuoteSqliteIdentifier(columnName)} {sqliteType}");
                }
                else
                {
                    columnDefs.Add($"{QuoteSqliteIdentifier(columnName)} {sqliteType} NOT NULL DEFAULT {GetSqliteDefaultValue(type)}");
                }
            }

            if (columnDefs.Count == 0)
            {
                return;
            }

            var sql = $"CREATE TABLE IF NOT EXISTS {QuoteSqliteIdentifier(tableName)} ({string.Join(", ", columnDefs)})";
            dbContext.Database.ExecuteSqlRaw(sql);

            // 唯一索引 / 普通索引
            foreach (var index in entityType.GetIndexes())
            {
                var indexColumns = index.Properties
                    .Select(p => p.GetColumnName(storeObject))
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => QuoteSqliteIdentifier(c!))
                    .ToList();
                if (indexColumns.Count == 0)
                {
                    continue;
                }

                var indexName = index.GetDatabaseName() ?? $"IX_{tableName}_{string.Join("_", index.Properties.Select(p => p.Name))}";
                var unique = index.IsUnique ? "UNIQUE " : string.Empty;
                var indexSql = $"CREATE {unique}INDEX IF NOT EXISTS {QuoteSqliteIdentifier(indexName)} ON {QuoteSqliteIdentifier(tableName)} ({string.Join(", ", indexColumns)})";
                dbContext.Database.ExecuteSqlRaw(indexSql);
            }
        }


        private static void EnsureSqliteColumn(SqlDBContext dbContext, string tableName, string columnName, string columnDefinition)
        {
            var quotedTableName = QuoteSqliteIdentifier(tableName);
            var quotedColumnName = QuoteSqliteIdentifier(columnName);
            dbContext.Database.ExecuteSqlRaw($"ALTER TABLE {quotedTableName} ADD COLUMN {quotedColumnName} {columnDefinition}");
        }

        private static void EnsureSqlServerTable(SqlDBContext dbContext, IEntityType entityType, string schema, string tableName)
        {
            var storeObject = StoreObjectIdentifier.Table(tableName, schema);
            var columnDefs = new List<string>();
            string? pkColumn = null;

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(storeObject);
                if (string.IsNullOrWhiteSpace(columnName))
                {
                    continue;
                }

                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                var sqlType = GetSqlServerType(property, type);
                var isPk = property.IsPrimaryKey();
                var nullable = property.IsNullable && !isPk;

                if (isPk && (type == typeof(int) || type == typeof(long)))
                {
                    columnDefs.Add($"{QuoteSqlServerIdentifier(columnName)} {sqlType} NOT NULL IDENTITY(1,1)");
                    pkColumn = columnName;
                    continue;
                }

                if (isPk)
                {
                    columnDefs.Add($"{QuoteSqlServerIdentifier(columnName)} {sqlType} NOT NULL");
                    pkColumn = columnName;
                    continue;
                }

                if (nullable)
                {
                    columnDefs.Add($"{QuoteSqlServerIdentifier(columnName)} {sqlType} NULL");
                }
                else
                {
                    columnDefs.Add($"{QuoteSqlServerIdentifier(columnName)} {sqlType} NOT NULL CONSTRAINT [DF_{tableName}_{columnName}] DEFAULT {GetSqlServerDefaultValue(type)}");
                }
            }

            if (columnDefs.Count == 0)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(pkColumn))
            {
                columnDefs.Add($"CONSTRAINT [PK_{tableName}] PRIMARY KEY ({QuoteSqlServerIdentifier(pkColumn)})");
            }

            var fullName = $"{QuoteSqlServerIdentifier(schema)}.{QuoteSqlServerIdentifier(tableName)}";
            var sql = $@"
IF OBJECT_ID(N'{schema.Replace("'", "''")}.{tableName.Replace("'", "''")}', N'U') IS NULL
BEGIN
    CREATE TABLE {fullName} ({string.Join(", ", columnDefs)});
END";
            dbContext.Database.ExecuteSqlRaw(sql);

            foreach (var index in entityType.GetIndexes())
            {
                var indexColumns = index.Properties
                    .Select(p => p.GetColumnName(storeObject))
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => QuoteSqlServerIdentifier(c!))
                    .ToList();
                if (indexColumns.Count == 0)
                {
                    continue;
                }

                var indexName = index.GetDatabaseName() ?? $"IX_{tableName}_{string.Join("_", index.Properties.Select(p => p.Name))}";
                var unique = index.IsUnique ? "UNIQUE " : string.Empty;
                var indexSql = $@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{indexName.Replace("'", "''")}' AND object_id = OBJECT_ID(N'{schema.Replace("'", "''")}.{tableName.Replace("'", "''")}'))
BEGIN
    CREATE {unique}INDEX {QuoteSqlServerIdentifier(indexName)} ON {fullName} ({string.Join(", ", indexColumns)});
END";
                dbContext.Database.ExecuteSqlRaw(indexSql);
            }
        }

        private static void EnsureSqlServerColumn(SqlDBContext dbContext, string schema, string tableName, string columnName, string columnDefinition)
        {
            var fullName = $"{QuoteSqlServerIdentifier(schema)}.{QuoteSqlServerIdentifier(tableName)}";
            var quotedColumn = QuoteSqlServerIdentifier(columnName);
            var sql = $@"
IF COL_LENGTH(N'{schema.Replace("'", "''")}.{tableName.Replace("'", "''")}', N'{columnName.Replace("'", "''")}') IS NULL
BEGIN
    ALTER TABLE {fullName} ADD {quotedColumn} {columnDefinition};
END";
            dbContext.Database.ExecuteSqlRaw(sql);
        }

        private static HashSet<string> GetSqlServerColumns(SqlDBContext dbContext, string schema, string tableName)
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
                command.CommandText = @"
SELECT c.name
FROM sys.columns c
INNER JOIN sys.tables t ON c.object_id = t.object_id
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = @schema AND t.name = @table";
                var schemaParam = command.CreateParameter();
                schemaParam.ParameterName = "@schema";
                schemaParam.Value = schema;
                command.Parameters.Add(schemaParam);
                var tableParam = command.CreateParameter();
                tableParam.ParameterName = "@table";
                tableParam.Value = tableName;
                command.Parameters.Add(tableParam);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(reader.GetString(0));
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

        private static string GetSqlServerColumnDefinition(IProperty property)
        {
            var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            var sqlType = GetSqlServerType(property, type);
            if (property.IsNullable)
            {
                return $"{sqlType} NULL";
            }

            return $"{sqlType} NOT NULL DEFAULT {GetSqlServerDefaultValue(type)}";
        }

        private static string GetSqlServerType(IProperty property, Type type)
        {
            if (type == typeof(string))
            {
                var maxLength = property.GetMaxLength();
                if (maxLength.HasValue && maxLength.Value > 0 && maxLength.Value <= 4000)
                {
                    return $"nvarchar({maxLength.Value})";
                }

                return "nvarchar(max)";
            }

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            {
                return "datetime2";
            }

            if (type == typeof(Guid))
            {
                return "uniqueidentifier";
            }

            if (type == typeof(decimal))
            {
                return "decimal(18,2)";
            }

            if (type == typeof(bool))
            {
                return "bit";
            }

            if (type == typeof(byte))
            {
                return "tinyint";
            }

            if (type == typeof(short))
            {
                return "smallint";
            }

            if (type == typeof(int))
            {
                return "int";
            }

            if (type == typeof(long))
            {
                return "bigint";
            }

            if (type == typeof(float))
            {
                return "real";
            }

            if (type == typeof(double))
            {
                return "float";
            }

            if (type == typeof(byte[]))
            {
                return "varbinary(max)";
            }

            return "nvarchar(max)";
        }

        private static string GetSqlServerDefaultValue(Type type)
        {
            if (type == typeof(string))
            {
                return "N''";
            }

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            {
                return "'0001-01-01T00:00:00'";
            }

            if (type == typeof(Guid))
            {
                return "'00000000-0000-0000-0000-000000000000'";
            }

            if (type == typeof(decimal) || type == typeof(float) || type == typeof(double))
            {
                return "0";
            }

            if (type == typeof(bool))
            {
                return "0";
            }

            return "0";
        }

        private static string QuoteSqlServerIdentifier(string value)
        {
            return $"[{value.Replace("]", "]]")}]";
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
