# 打卡上传接口说明（Checkin）

> 控制器：`BM.Service.Business.Controllers.CheckinController`  
> 路由前缀：`/api/checkin`  
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

## 1. 上传打卡内容

### 基本信息

| 项目 | 说明 |
|------|------|
| 方法 / 路径 | `POST /api/checkin/upload` |
| 鉴权 | **需要登录**（JWT Bearer） |
| Content-Type | `multipart/form-data` 或 `application/x-www-form-urlencoded`（仅改描述） |
| 请求体大小限制 | 约 200 MB（`RequestSizeLimit(200_000_000)`） |
| 成功 code | 200 |
| 失败 code | 400 / 401 |

### 请求头

```http
Authorization: Bearer {access_token}
Content-Type: multipart/form-data
```

### 表单字段

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| checkin_id | int | 是 | 打卡记录 ID（来自 `GET /api/daily/today` 的 `tasks[].checkin_id`） |
| video | file | 条件 | 视频文件 |
| images | file[] | 条件 | 图片，可多张（同名字段多次上传） |
| description | string | 否 | 文字描述 |

> **媒体必填**：本次上传的 video/images **或** 库中已有 `video_url` / `image_urls` 至少其一。仅文字描述且无媒体 → 拒绝。

### 支持的文件类型

| 类型 | 扩展名 |
|------|--------|
| 视频 | `.mp4` `.mov` `.m4v` `.avi` `.webm` |
| 图片 | `.jpg` `.jpeg` `.png` `.gif` `.webp` `.bmp` |

### 业务规则

1. 校验 `checkin` 存在，且所属日计划的 `user_id` 为当前用户。
2. 日计划已是 `submitted` / `commented` → 拒绝上传。
3. 单条打卡已是 `submitted` → 拒绝。
4. **至少需要一个图片或视频**（本次上传或库中已有）。
5. 文件保存路径（相对站点根）：
   - `/uploads/checkin/{userId}/{checkinId}/video_yyyyMMddHHmmss.ext`
   - `/uploads/checkin/{userId}/{checkinId}/img_yyyyMMddHHmmss_N.ext`
6. 新图片会 **追加** 到已有 `image_urls`（逗号分隔），不会清空历史图片。
7. 上传成功后该打卡 `status = uploaded`，并刷新日计划 `progress`。
8. **金币奖励**：当打卡从 `unfinished` 首次变为 `uploaded` 时，奖励 **5 金币**（`total_coins` / `available_coins` 同时 +5），并写入 `coins_log`（`source_type=checkin_reward`, `source_id=checkin_id`）。同一 checkin 不重复发奖。

> 上传目录 `wwwroot/uploads/` 默认不纳入 Git（见根 `.gitignore`）。

### 成功响应 `data`

```json
{
  "uploaded": true,
  "coins_awarded": 5,
  "available_coins": 15
}
```

| 字段 | 说明 |
|------|------|
| uploaded | 是否上传成功 |
| coins_awarded | 本次新获得金币（首次完成一般为 5，重复保存为 0） |
| available_coins | 当前可用金币余额 |

### 失败场景

| 条件 | code | errorMessage（示例） |
|------|------|----------------------|
| 未登录 | 401 | `Sorry, please sign in first!` |
| checkin_id 无效 | 400 | `checkin_id is required` |
| 打卡不存在 | 400 | `checkin not found` |
| 非本人计划 | 400 | `no permission for this checkin` |
| 日计划已提交 | 400 | `daily plan already submitted` |
| 打卡已提交 | 400 | `checkin already submitted` |
| 无图片/视频 | 400 | `please upload at least one image or video` |
| 视频类型不支持 | 400 | `unsupported video type` |
| 图片类型不支持 | 400 | `unsupported image type: xxx` |

### 请求示例（multipart）

```http
POST /api/checkin/upload HTTP/1.1
Host: localhost:20011
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
Content-Type: multipart/form-data; boundary=----WebKitFormBoundary

------WebKitFormBoundary
Content-Disposition: form-data; name="checkin_id"

1
------WebKitFormBoundary
Content-Disposition: form-data; name="description"

今天完成 100 次跳绳
------WebKitFormBoundary
Content-Disposition: form-data; name="video"; filename="jump.mp4"
Content-Type: video/mp4

(binary)
------WebKitFormBoundary
Content-Disposition: form-data; name="images"; filename="a.jpg"
Content-Type: image/jpeg

(binary)
------WebKitFormBoundary--
```

### 成功响应示例

```json
{
  "isSuccess": true,
  "code": 200,
  "errorMessage": "success",
  "data": {
    "uploaded": true,
    "coins_awarded": 5,
    "available_coins": 5
  }
}
```

### curl

```bash
curl -X POST "http://localhost:20011/api/checkin/upload" ^
  -H "Authorization: Bearer 你的access_token" ^
  -F "checkin_id=1" ^
  -F "description=今天完成100次" ^
  -F "video=@D:\temp\jump.mp4" ^
  -F "images=@D:\temp\a.jpg" ^
  -F "images=@D:\temp\b.jpg"
```

---

## 2. 游戏打卡完成

### 基本信息

| 项目 | 说明 |
|------|------|
| 方法 / 路径 | `POST /api/checkin/game-complete` |
| 鉴权 | **需要登录**（JWT Bearer） |
| Content-Type | `application/json` |
| 成功 code | 200 |
| 失败 code | 400 / 401 |

### 请求体

```json
{
  "checkin_id": 1,
  "description": "游戏打卡完成（弹唇啵啵操）"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| checkin_id | int | 是 | 打卡记录 ID |
| description | string | 否 | 文字描述；默认「游戏打卡完成（弹唇啵啵操）」 |

### 业务规则

1. 无需上传图片/视频；用于「弹唇啵啵操」等游戏通关后完成打卡。
2. 将打卡 `status` 设为 `uploaded`，并刷新日计划 `progress`。
3. 若尚无媒体，写入占位 `image_urls = game://bobo-complete`（前端展示时会过滤）。
4. **首次**从未完成变为 `uploaded` 时奖励 **5 金币**（与普通上传一致，同 checkin 不重复发奖）。
5. 日计划已 `submitted`/`commented`，或单条已 `submitted` → 拒绝。

### 成功响应 `data`

与上传接口相同：

```json
{
  "uploaded": true,
  "coins_awarded": 5,
  "available_coins": 35
}
```

### curl

```bash
curl -X POST "http://localhost:20011/api/checkin/game-complete" ^
  -H "Authorization: Bearer 你的access_token" ^
  -H "Content-Type: application/json" ^
  -d "{\"checkin_id\":1,\"description\":\"游戏打卡完成\"}"
```

---

## 访问已上传文件


静态文件路径形如：

```text
http://localhost:20011/uploads/checkin/{userId}/{checkinId}/video_....mp4
```

需确认宿主已启用静态文件中间件（`UseStaticFiles`），且 `wwwroot` 可访问。

---

## 相关代码位置

| 说明 | 路径 |
|------|------|
| 控制器 | `BM.Service.Business/Controllers/CheckinController.cs` |
| 服务 | `BM.Service.Business/Services/CheckinService.cs` |
