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

## API 文档

| 模块 | 文档 | 说明 |
|------|------|------|
| 认证 | [`docs/API_Auth.md`](docs/API_Auth.md) | 注册 / 登录 / 改密 |
| 用户 | [`docs/API_User.md`](docs/API_User.md) | 个人资料 |
| 每日计划 | [`docs/API_Daily.md`](docs/API_Daily.md) | 今日计划 / 一键提交 |
| 打卡上传 | [`docs/API_Checkin.md`](docs/API_Checkin.md) | 视频/图片/描述上传 |
| 健康检查 | [`docs/API_Health.md`](docs/API_Health.md) | alive / ping |

### 接口一览

| 模块 | 接口 | 方法 | 路径 | 鉴权 |
|------|------|------|------|------|
| 认证 | 注册 | POST | `/api/auth/register` | 匿名 |
| 认证 | 登录 | POST | `/api/auth/login` | 匿名 |
| 认证 | 修改密码 | POST | `/api/auth/change-password` | Bearer JWT |
| 用户 | 个人信息 | GET | `/api/user/profile` | Bearer JWT |
| 每日计划 | 今日计划 | GET | `/api/daily/today` | Bearer JWT |
| 每日计划 | 一键提交 | POST | `/api/daily/submit` | Bearer JWT |
| 打卡 | 上传内容 | POST | `/api/checkin/upload` | Bearer JWT |
| 健康 | 存活 | GET | `/Health/alive` | 匿名 |
| 健康 | 运行 | GET | `/Health/ping` | Bearer JWT |

### 学员端推荐联调顺序

1. `POST /api/auth/register` → `POST /api/auth/login` 拿到 `access_token`
2. `GET /api/user/profile` 验证鉴权与资料
3. `GET /api/daily/today` 拿到任务与 `checkin_id`
4. `POST /api/checkin/upload` 逐条上传（multipart）
5. `POST /api/daily/submit` 一键提交当日计划

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
