# 每日训练计划接口说明（Daily）

> 控制器：`BM.Service.Business.Controllers.DailyPlanController`  
> 路由前缀：`/api/daily`  
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

---

## 状态说明

### 日计划 `status`

| 值 | 说明 |
|----|------|
| draft | 草稿 / 进行中（可继续上传） |
| submitted | 已一键提交 |
| commented | 已点评（提交后进入，通常不可再改） |

### 任务打卡 `status`

| 值 | 说明 |
|----|------|
| unfinished | 未完成 |
| uploaded | 已上传内容（视频/图/描述） |
| submitted | 随日计划一并提交 |

### 进度 `progress`

格式：`已完成数/总任务数`，例如 `2/6`。  
「已完成」计入 `uploaded` 与 `submitted`。

---

## 1. 获取今日训练计划

### 基本信息

| 项目 | 说明 |
|------|------|
| 方法 / 路径 | `GET /api/daily/today` |
| 鉴权 | **需要登录**（JWT Bearer） |
| 成功 code | 200 |
| 失败 code | 400 / 401 |

### 请求头

```http
Authorization: Bearer {access_token}
```

### 业务规则

1. 以服务器本地日期 `DateTime.Today` 作为「今日」。
2. 若当日尚无 `dailyPlan`：
   - 按用户 `create_time` 相对天数，在 `trainingPlan` 表中轮询选取「第几周第几天」；
   - 无训练方案时默认 `week_no=1`、`day_no=1`、任务列表为空；
   - 创建计划，并按方案 `task_ids` 生成 `taskCheckin` 行（初始 `unfinished`）。
3. 若当日已有计划：补齐缺失的打卡任务行。
4. 返回计划信息 + 任务列表（按模板 `sort_order` 排序）。

### 成功响应 `data`：`DailyTodayOutputViewModel`

| 字段 | 类型 | 说明 |
|------|------|------|
| plan_date | string | 计划日期 `yyyy-MM-dd` |
| week_no | int? | 第几周 |
| day_no | int? | 第几天 |
| status | string | `draft` / `submitted` / `commented` |
| progress | string | 如 `2/6` |
| tasks | array | 任务打卡列表，见下表 |

#### `tasks[]`：`DailyTaskItemViewModel`

| 字段 | 类型 | 说明 |
|------|------|------|
| checkin_id | int | 打卡记录 ID（上传接口用此字段） |
| task_id | int | 任务模板 ID |
| task_name | string | 任务名称 |
| icon_url | string? | 图标 |
| requirement | string? | 要求说明 |
| teach_video_url | string? | 教学视频 URL |
| status | string | `unfinished` / `uploaded` / `submitted` |
| video_url | string? | 用户上传视频路径 |
| image_urls | string[] | 用户上传图片路径列表 |
| description | string? | 用户填写描述 |

### 失败场景

| 条件 | code | errorMessage（示例） |
|------|------|----------------------|
| 未登录 | 401 | `Sorry, please sign in first!` |
| 用户不存在 | 400 | `user not found` |

### 请求示例

```http
GET /api/daily/today HTTP/1.1
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
    "plan_date": "2026-07-21",
    "week_no": 1,
    "day_no": 1,
    "status": "draft",
    "progress": "0/2",
    "tasks": [
      {
        "checkin_id": 1,
        "task_id": 1,
        "task_name": "跳绳",
        "icon_url": "",
        "requirement": "连续跳绳 100 次",
        "teach_video_url": "",
        "status": "unfinished",
        "video_url": "",
        "image_urls": [],
        "description": ""
      },
      {
        "checkin_id": 2,
        "task_id": 2,
        "task_name": "深蹲",
        "icon_url": "",
        "requirement": "20 次 × 3 组",
        "teach_video_url": "",
        "status": "unfinished",
        "video_url": "",
        "image_urls": [],
        "description": ""
      }
    ]
  }
}
```

### curl

```bash
curl -X GET "http://localhost:20011/api/daily/today" ^
  -H "Authorization: Bearer 你的access_token"
```

---

## 2. 一键提交指定日期打卡

### 基本信息

| 项目 | 说明 |
|------|------|
| 方法 / 路径 | `POST /api/daily/submit` |
| 鉴权 | **需要登录**（JWT Bearer） |
| Content-Type | `application/json` |
| 成功 code | 200 |
| 失败 code | 400 / 401 |

### 请求头

```http
Authorization: Bearer {access_token}
Content-Type: application/json
```

### 请求体 `DailySubmitInputViewModel`

| 字段 | 类型 | 必填 | 约束 | 说明 |
|------|------|------|------|------|
| plan_date | string | 是 | `yyyy-MM-dd` 可被 DateTime 解析 | 要提交的计划日期 |

### 业务规则

1. 只能提交属于当前用户、且日期匹配的日计划。
2. 计划状态已是 `submitted` 或 `commented` → 拒绝。
3. 必须存在至少一条打卡任务。
4. 所有任务均不可仍为 `unfinished`（需先上传变为 `uploaded`）。
5. 提交后：`uploaded` → `submitted`；计划 `status = submitted`，写入 `submit_time`，刷新 `progress`。

### 成功响应 `data`

```json
{
  "submitted": true
}
```

### 失败场景

| 条件 | code | errorMessage（示例） |
|------|------|----------------------|
| 未登录 | 401 | `Sorry, please sign in first!` |
| plan_date 为空 | 400 | `plan_date is required` |
| 日期格式错误 | 400 | `plan_date format invalid, use yyyy-MM-dd` |
| 计划不存在 | 400 | `daily plan not found` |
| 已提交 | 400 | `already submitted` |
| 无任务 | 400 | `no checkin tasks` |
| 仍有未完成 | 400 | `still N unfinished task(s)` |

### 请求示例

```http
POST /api/daily/submit HTTP/1.1
Host: localhost:20011
Content-Type: application/json
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...

{
  "plan_date": "2026-07-21"
}
```

### 成功响应示例

```json
{
  "isSuccess": true,
  "code": 200,
  "errorMessage": "success",
  "data": {
    "submitted": true
  }
}
```

### curl

```bash
curl -X POST "http://localhost:20011/api/daily/submit" ^
  -H "Content-Type: application/json" ^
  -H "Authorization: Bearer 你的access_token" ^
  -d "{\"plan_date\":\"2026-07-21\"}"
```

---

## 推荐联调流程

1. 登录拿到 `access_token`（见 [API_Auth.md](API_Auth.md)）  
2. `GET /api/daily/today` → 拿到各任务 `checkin_id`  
3. 对每个任务 `POST /api/checkin/upload` 上传（见 [API_Checkin.md](API_Checkin.md)）  
4. 全部 `uploaded` 后 `POST /api/daily/submit`  

---

## 相关代码位置

| 说明 | 路径 |
|------|------|
| 控制器 | `BM.Service.Business/Controllers/DailyPlanController.cs` |
| 服务 | `BM.Service.Business/Services/DailyPlanService.cs` |
| 今日出参 | `BM.Service.Core/Models/DailyTodayOutputViewModel.cs` |
| 提交入参 | `BM.Service.Core/Models/DailySubmitInputViewModel.cs` |
