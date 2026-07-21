# 健康检查接口说明（Health）

> 控制器：`BM.Service.Business.Controllers.HealthController`  
> 路由前缀：`/Health`（`[Route("[controller]")]`）  
> Swagger 分组：`Base`  
> 统一响应包装：`ResultModel<T>`  
> 默认服务地址：`http://localhost:20011`

---

## 1. 存活探测（匿名）

| 项目 | 说明 |
|------|------|
| 方法 / 路径 | `GET /Health/alive` |
| 鉴权 | 匿名（`[AllowAnonymous]`） |

### 成功响应示例

```json
{
  "isSuccess": true,
  "code": 200,
  "errorMessage": "success",
  "data": "ok"
}
```

### curl

```bash
curl -X GET "http://localhost:20011/Health/alive"
```

---

## 2. 运行探测（需登录）

| 项目 | 说明 |
|------|------|
| 方法 / 路径 | `GET /Health/ping` |
| 鉴权 | **需要登录**（JWT Bearer，继承 `BaseController` 全局策略） |

### 成功响应示例

```json
{
  "isSuccess": true,
  "code": 200,
  "errorMessage": "success",
  "data": "BM.Service is running"
}
```

### curl

```bash
curl -X GET "http://localhost:20011/Health/ping" ^
  -H "Authorization: Bearer 你的access_token"
```

---

## 相关代码位置

| 说明 | 路径 |
|------|------|
| 控制器 | `BM.Service.Business/Controllers/HealthController.cs` |
