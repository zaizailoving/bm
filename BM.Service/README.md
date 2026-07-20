# BM.Service

基于 ModernWMS 框架剥离业务后生成的空白后端骨架，用于编写新业务。

## 解决方案结构

| 项目 | 说明 |
|------|------|
| `BM.Service` | Web 宿主（Program / Startup / 配置 / 数据库初始化） |
| `BM.Service.Core` | 基础设施：JWT、EF Core、DI、中间件、Swagger、分页/动态查询、账号登录 |
| `BM.Service.Business` | **新业务代码放这里**（Controllers / Entities / IServices / Services） |

## 已保留能力

- JWT 登录（`AccountController`：`/account/login` 等）
- EF Core 多数据库：`SqlLite` / `SqlServer` / `MySql` / `Postgres`
- 自动 DI（实现 `IDependency` 的服务会被扫描注册）
- Swagger
- NLog
- Hangfire（默认关闭 Dashboard）
- 多租户（`tenant_id`）基础能力

## 已移除

- 全部 WMS 业务（仓库、库位、货主、出入库、盘点、报表等）

## 默认账号

- 用户名：`admin`
- 密码：配置项 `Seed:AdminPassword`（默认 `1`）

## 如何新增业务

1. 在 `BM.Service.Business/Entities` 下添加实体（继承 `BaseModel`，并配置 EF 映射）
2. 在 `IServices` / `Services` 写业务服务（服务接口继承 `IDependency` 可自动注入）
3. 在 `Controllers` 写 API（可继承 `BaseController`）
4. 如需表结构随启动创建：实体被 `SqlDBContext` 扫描到即可（`EnsureCreated`）

### 示例目录约定

```
BM.Service.Business/
  Controllers/     # API
  Entities/        # 实体 + ViewModel
  IServices/       # 服务接口
  Services/        # 服务实现
```

## 运行

```bash
cd BM.Service
dotnet restore
dotnet run --project BM.Service
```

默认地址：`http://localhost:20011`  
Swagger：`http://localhost:20011/swagger`

## 配置

编辑 `BM.Service/appsettings.json`：

- `Database:db`：`SqlLite` | `SqlServer` | `MySql` | `Postgres`
- `ConnectionStrings:*`：连接串
- `TokenSettings`：JWT
- `Seed:AdminPassword`：初始管理员密码
