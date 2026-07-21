# 认证接口说明（Auth）

> 控制器：`BM.Service.Core.Controller.AuthController`  
> 路由前缀：`/api/auth`  
> Swagger 分组：`Base`  
> 统一响应包装：`ResultModel<T>`  
> 默认服务地址：`http://localhost:20011`  
> Swagger：`http://localhost:20011/swagger`

---

## 统一响应结构

```json
{
  "isSuccess": true,
  "code": 200,
  "errorMessage": "success",
  "data": { }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| isSuccess | bool | 是否成功 |
| code | int | 业务状态码（成功一般为 200；失败常见 400 / 401） |
| errorMessage | string | 提示或错误信息 |
| data | T \| null | 业务数据 |

> JSON 序列化默认多为 camelCase，故字段名为 `isSuccess` / `errorMessage` 等；若项目配置了不同策略，以实际响应为准。

---

## 1. 用户注册

### 基本信息

| 项目 | 说明 |
|------|------|
| 方法 / 路径 | `POST /api/auth/register` |
| 鉴权 | 匿名（`[AllowAnonymous]`，无需 Token） |
| Content-Type | `application/json` |
| 成功 code | 200 |
| 失败 code | 400 |

### 请求体 `RegisterInputViewModel`

| 字段 | 类型 | 必填 | 约束 | 说明 |
|------|------|------|------|------|
| username | string | 是 | 3～50 字符；去首尾空格；全局唯一 | 登录用户名 |
| password | string | 是 | 6～64 字符 | 明文密码，服务端存 MD5 |
| nickname | string | 否 | 最多 50 | 昵称；不传则默认等于 username |
| phone | string | 否 | 最多 20；有值时全局唯一 | 手机号 |
| role | string | 否 | 仅 `student` / `teacher`（大小写不敏感） | 默认 `student`；**禁止**通过接口注册 `admin` |

### 成功响应 `data`：`RegisterOutputViewModel`

| 字段 | 类型 | 说明 |
|------|------|------|
| user_id | int | 用户主键 |
| username | string | 用户名 |
| nickname | string? | 昵称 |
| role | string | 角色：`student` / `teacher` |
| phone | string? | 手机号 |
| archive_no | string? | 档案号（注册时一般为空） |

### 失败场景

| 条件 | errorMessage（示例） |
|------|----------------------|
| username 为空 | `username is required` |
| password 长度 < 6 | `password must be at least 6 characters` |
| role 非 student/teacher | `role must be student or teacher` |
| 用户名已存在 | `username already exists` |
| 手机号已存在 | `phone already exists` |
| 模型校验失败（如 username < 3） | ASP.NET 模型校验错误（进入业务前） |

### 注册后默认业务字段

- `status` = `normal`
- `train_camp_status` = `ongoing`
- `total_coins` / `available_coins` = 0
- `password_hash` = MD5(password)

### 请求示例

**学员（最小字段）**

```http
POST /api/auth/register HTTP/1.1
Host: localhost:20011
Content-Type: application/json

{
  "username": "student01",
  "password": "123456"
}
```

**学员（完整字段）**

```json
{
  "username": "student02",
  "password": "123456",
  "nickname": "小星星",
  "phone": "13800000001",
  "role": "student"
}
```

**老师**

```json
{
  "username": "teacher01",
  "password": "123456",
  "nickname": "王老师",
  "phone": "13900000001",
  "role": "teacher"
}
```

### 成功响应示例

```json
{
  "isSuccess": true,
  "code": 200,
  "errorMessage": "success",
  "data": {
    "user_id": 2,
    "username": "student01",
    "nickname": "student01",
    "role": "student",
    "phone": null,
    "archive_no": null
  }
}
```

### curl

```bash
curl -X POST "http://localhost:20011/api/auth/register" ^
  -H "Content-Type: application/json" ^
  -d "{\"username\":\"student01\",\"password\":\"123456\",\"nickname\":\"小星星\",\"role\":\"student\"}"
```

---

## 2. 用户登录

### 基本信息

| 项目 | 说明 |
|------|------|
| 方法 / 路径 | `POST /api/auth/login` |
| 鉴权 | 匿名（`[AllowAnonymous]`） |
| Content-Type | `application/json` |
| 成功 code | 200 |
| 失败 code | 401 |

### 请求体 `LoginInputViewModel`

| 字段 | 类型 | 必填 | 约束 | 说明 |
|------|------|------|------|------|
| user_name | string | 是 | 最多 128 | 登录用户名（对应库字段 `username`） |
| password | string | 是 | 最多 64 | 密码（支持 MD5 哈希比对；亦兼容库中明文历史数据） |

> **注意字段名**：登录用 `user_name`，注册用 `username`，二者不一致，调用时勿写错。

### 业务规则

1. 仅 `status == "normal"` 的用户可登录。
2. 密码校验：`MD5(password)` 与 `password_hash` 相等，或明文与 `password_hash` 相等（兼容旧数据）。
3. 登录成功会更新 `last_login_time`，并尽量写入客户端 IP（`X-Forwarded-For` 或连接 IP）到 `last_login_ip`。
4. 服务端生成 JWT `access_token` 与 `refresh_token`，并将 refresh token 写入缓存。

### 成功响应 `data`：`LoginOutputViewModel`

| 字段 | 类型 | 说明 |
|------|------|------|
| user_id | int | 用户 ID |
| user_name | string | 展示名（优先 nickname，否则 username） |
| user_num | string | 编号（优先 archive_no，否则 username） |
| user_role | string | 角色 |
| userrole_id | int | 角色 ID（当前固定 0） |
| tenant_id | long | 租户（当前固定 1） |
| access_token | string | JWT，后续鉴权接口放在 Header |
| expire | int | Token 过期相关时间（由 TokenManager 生成） |
| refresh_token | string | 刷新令牌 |

### 失败场景

| 条件 | code | errorMessage |
|------|------|--------------|
| 用户不存在 / 状态非 normal / 密码错误 | 401 | 本地化键 `login_failed`（无翻译时约为 `login failed`） |

### 请求示例

```http
POST /api/auth/login HTTP/1.1
Host: localhost:20011
Content-Type: application/json

{
  "user_name": "student01",
  "password": "123456"
}
```

**默认种子管理员**

```json
{
  "user_name": "admin",
  "password": "1"
}
```

> 管理员密码以 `appsettings.json` 中 `Seed:AdminPassword` 为准，默认多为 `1`。

### 成功响应示例

```json
{
  "isSuccess": true,
  "code": 200,
  "errorMessage": "success",
  "data": {
    "user_id": 2,
    "user_name": "小星星",
    "user_num": "student01",
    "user_role": "student",
    "userrole_id": 0,
    "tenant_id": 1,
    "access_token": "eyJhbGciOiJIUzI1NiIs...",
    "expire": 120,
    "refresh_token": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
  }
}
```

### 后续鉴权方式

需登录的接口请在请求头携带：

```http
Authorization: Bearer {access_token}
```

### curl

```bash
curl -X POST "http://localhost:20011/api/auth/login" ^
  -H "Content-Type: application/json" ^
  -d "{\"user_name\":\"student01\",\"password\":\"123456\"}"
```

---

## 3. 修改密码

### 基本信息

| 项目 | 说明 |
|------|------|
| 方法 / 路径 | `POST /api/auth/change-password` |
| 鉴权 | **需要登录**（JWT Bearer） |
| Content-Type | `application/json` |
| 成功 code | 200 |
| 失败 code | 400 / 401 |

### 请求头

```http
Authorization: Bearer {access_token}
Content-Type: application/json
```

### 请求体 `ChangePasswordInputViewModel`

| 字段 | 类型 | 必填 | 约束 | 说明 |
|------|------|------|------|------|
| old_password | string | 是 | 最多 64 | 原密码 |
| new_password | string | 是 | 6～64 字符 | 新密码；不可与原密码相同 |

### 业务规则

1. 未登录或 token 无效：返回 401。
2. 校验原密码（MD5 或明文兼容，同登录）。
3. 新密码写入 MD5。
4. 成功后会尝试清空该用户的 Web refresh token 缓存，促使重新登录刷新会话。

### 成功响应 `data`

```json
{
  "changed": true
}
```

### 失败场景

| 条件 | code | errorMessage（示例） |
|------|------|----------------------|
| 未登录 | 401 | `Sorry, please sign in first!` |
| 原/新密码为空 | 400 | `password is required` |
| 新密码 < 6 位 | 400 | `new password must be at least 6 characters` |
| 新旧密码相同 | 400 | `new password must be different from old password` |
| 用户不存在或非 normal | 400 | `user not found` |
| 原密码错误 | 400 | `old password is incorrect` |

### 请求示例

```http
POST /api/auth/change-password HTTP/1.1
Host: localhost:20011
Content-Type: application/json
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...

{
  "old_password": "123456",
  "new_password": "654321"
}
```

### 成功响应示例

```json
{
  "isSuccess": true,
  "code": 200,
  "errorMessage": "success",
  "data": {
    "changed": true
  }
}
```

### curl

```bash
curl -X POST "http://localhost:20011/api/auth/change-password" ^
  -H "Content-Type: application/json" ^
  -H "Authorization: Bearer 你的access_token" ^
  -d "{\"old_password\":\"123456\",\"new_password\":\"654321\"}"
```

---

## 推荐联调流程

1. **注册** `POST /api/auth/register` → 得到 `user_id`  
2. **登录** `POST /api/auth/login`（注意字段是 `user_name`）→ 得到 `access_token`  
3. **改密** `POST /api/auth/change-password`（Header 带 Bearer）→ `changed: true`  
4. 用**新密码**再次登录验证  

---

## 字段名对照（易错）

| 场景 | 用户名字段 | 密码字段 |
|------|------------|----------|
| 注册 | `username` | `password` |
| 登录 | `user_name` | `password` |
| 改密 | — | `old_password` / `new_password` |

---

## 相关代码位置

| 说明 | 路径 |
|------|------|
| 控制器 | `BM.Service.Core/Controller/AuthController.cs` |
| 服务 | `BM.Service.Core/Services/AccountService.cs` |
| 注册入参 | `BM.Service.Core/Models/RegisterInputViewModel.cs` |
| 注册出参 | `BM.Service.Core/Models/RegisterOutputViewModel.cs` |
| 登录入参 | `BM.Service.Core/Models/LoginInputViewModel.cs` |
| 登录出参 | `BM.Service.Core/Models/LoginOutputViewModel.cs` |
| 改密入参 | `BM.Service.Core/Models/ChangePasswordInputViewModel.cs` |
| 统一响应 | `BM.Service.Core/Models/ResultModel.cs` |
