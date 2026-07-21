# 用户接口说明（User）

> 控制器：`BM.Service.Business.Controllers.UserController`  
> 路由前缀：`/api/user`  
> Swagger 分组：`Base`  
> 统一响应包装：`ResultModel<T>`  
> 默认服务地址：`http://localhost:20011`  
> Swagger：`http://localhost:20011/swagger`  
> **鉴权**：需登录（JWT Bearer）

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
| code | int | 业务状态码（成功 200；失败常见 400 / 401） |
| errorMessage | string | 提示或错误信息 |
| data | T \| null | 业务数据 |

---

## 1. 获取当前用户个人信息

### 基本信息

| 项目 | 说明 |
|------|------|
| 方法 / 路径 | `GET /api/user/profile` |
| 鉴权 | **需要登录**（JWT Bearer） |
| 成功 code | 200 |
| 失败 code | 400 / 401 |

### 请求头

```http
Authorization: Bearer {access_token}
```

### 成功响应 `data`：`UserProfileOutputViewModel`

| 字段 | 类型 | 说明 |
|------|------|------|
| id | int | 用户主键 |
| nickname | string? | 昵称 |
| avatar | string? | 头像 URL（无则空字符串） |
| phone | string? | 手机号（脱敏，如 `138****0000`；过短则原样返回） |
| role | string | 角色：`student` / `teacher` / `admin` 等 |
| archive_no | string? | 档案号 |
| total_coins | int | 累计金币 |
| available_coins | int | 可用金币 |
| train_camp_status | string | 训练营状态（如 `ongoing`） |

### 业务规则

1. 从 JWT 解析 `user_id`，仅查询 `status == "normal"` 的用户。
2. 手机号返回前做脱敏（保留前 3 位与后 4 位）。

### 失败场景

| 条件 | code | errorMessage（示例） |
|------|------|----------------------|
| 未登录 / token 无效 | 401 | `Sorry, please sign in first!` |
| 用户不存在或非 normal | 400 | `user not found` |

### 请求示例

```http
GET /api/user/profile HTTP/1.1
Host: localhost:20011
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

### 成功响应示例

```json
{
  "isSuccess": true,
  "code": 200,
  "errorMessage": "success",
  "data": {
    "id": 2,
    "nickname": "小星星",
    "avatar": "",
    "phone": "138****0001",
    "role": "student",
    "archive_no": null,
    "total_coins": 0,
    "available_coins": 0,
    "train_camp_status": "ongoing"
  }
}
```

### curl

```bash
curl -X GET "http://localhost:20011/api/user/profile" ^
  -H "Authorization: Bearer 你的access_token"
```

---

## 相关代码位置

| 说明 | 路径 |
|------|------|
| 控制器 | `BM.Service.Business/Controllers/UserController.cs` |
| 服务 | `BM.Service.Business/Services/UserService.cs` |
| 出参 | `BM.Service.Core/Models/UserProfileOutputViewModel.cs` |
