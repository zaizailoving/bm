# 系统推送说明

> 文档范围：BM 后端（`BM.Service`）与小程序/前端（`uniapp-shop-vue3-ts-template`）中与「推送 / 下发 / 后台任务 / 提醒」相关的现状。  
> 编写日期：2026-07-23

---

## 1. 结论摘要

| 能力 | 当前状态 | 说明 |
|------|----------|------|
| App 推送（极光 / 个推 / uni-push 等） | **未实现** | 前后端均无 SDK、配置、设备 token 表 |
| 微信小程序订阅消息 / 模板消息 | **未实现** | `manifest.json` 未配置相关能力，后端无微信消息接口 |
| 短信 / 邮件通知 | **未实现** | — |
| 每日训练计划「下发」 | **已实现（拉模式）** | 用户请求 `GET /api/daily/today` 时按需生成当日计划 |
| 后台定时任务（Hangfire） | **框架已就绪，业务推送未接** | 仅有示例 `TestJob`，不涉及用户通知 |
| 站内红点 / 点评提示 | **数据字段预留** | `daily_plan.comment_count` 用于被点评数量，无独立消息中心 |

**一句话：**  
当前系统没有「服务端主动推到用户手机」的消息推送；业务上所谓的任务「推送/下发」，是用户打开首页拉取今日计划时，服务端即时创建并返回任务列表。

---

## 2. 业务侧：每日训练计划如何「推」到用户

### 2.1 模式：按需拉取（Pull），非定时推送（Push）

```
┌─────────────┐     GET /api/daily/today + JWT      ┌──────────────────┐
│  小程序首页  │ ──────────────────────────────────► │ DailyPlanService │
│  (index.vue) │ ◄────────────────────────────────── │   .GetTodayAsync │
└─────────────┘     今日计划 + 任务列表               └────────┬─────────┘
                                                               │
                    若当日无 daily_plan：创建计划 + 打卡行
                    若已有：补齐缺失 task_checkin
                                                               ▼
                                                    training_plan / task_template
                                                    daily_plan / task_checkin
```

触发时机：

1. 用户登录后进入训练首页；
2. 前端调用 `GET /api/daily/today`（见 `docs/API_Daily.md`）；
3. 服务端以 **服务器本地日期** `DateTime.Today` 作为「今日」。

相关代码：

- 服务：`BM.Service.Business/Services/DailyPlanService.cs` → `GetTodayAsync`
- 控制器：`DailyPlanController`（路由前缀 `/api/daily`）
- 前端：`uniapp-shop-vue3-ts-template/src/services/daily.ts`、`pages/index/index.vue`

### 2.2 生成规则

#### 步骤 A：查/建当日计划 `daily_plan`

- 条件：`user_id = 当前用户` 且 `plan_date = 今日`
- 若不存在则新建，初始：
  - `status = draft`
  - `progress = 0/0`
  - `comment_count = 0`
  - `week_no` / `day_no` 由训练方案解析得到

表结构见 `dailyPlanEntity`（表名 `daily_plan`），唯一索引：`(user_id, plan_date)`。

#### 步骤 B：解析「第几周第几天」与任务列表

方法：`ResolveTrainingDayAsync`

1. 读取全部 `training_plan`，按 `week_no`、`day_no` 排序；
2. 用用户创建日相对今天的天数取模轮询：

   ```text
   dayIndex = max(0, (Today - user.create_time.Date).Days)
   selected = trainingPlans[dayIndex % trainingPlans.Count]
   ```

3. 解析 `selected.task_ids`（逗号/中文逗号/分号/空格分隔的任务模板 ID）；
4. 若无任何训练方案，或方案 `task_ids` 为空：回退为 **全部固定任务模板 ID**（按 `sort_order`）。

#### 步骤 C：生成/补齐打卡行 `task_checkin`

方法：`EnsureCheckinsAsync`

- 对每个应有的 `task_template_id`，若当日计划下尚无对应行，则插入：
  - `status = unfinished`
  - `comment_status = none`
  - `comment_id = 0`
- 已有计划再次请求时，会 **补齐缺失任务**，不会删除已有打卡行。

#### 步骤 D：组装返回

- 关联 `task_template` 取名称、要求、图标、教学视频 URL 等；
- 任务按模板 `sort_order` 排序；
- `progress` 格式：`已完成数/总数`（`uploaded` + `submitted` 计为已完成）。

### 2.3 与「推送」相关的状态流转（用户侧动作）

| 动作 | 接口 | 计划 status | 打卡 status |
|------|------|-------------|-------------|
| 首次打开今日计划 | `GET /api/daily/today` | `draft` | `unfinished` |
| 上传视频/图/描述 | `POST /api/checkin/upload` | 仍 `draft`，更新 progress | `uploaded` |
| 一键提交 | `POST /api/daily/submit` | `submitted` | `uploaded` → `submitted` |
| 老师点评（表结构已有） | 业务 API **尚未完整实现推送侧** | 预期 `commented` | `comment_status` 等 |

说明：

- **提交后** 不可再改（上传、再次提交均会拒绝）；
- `comment_record` 表、打卡上的 `comment_id` / `comment_status`、计划上的 `comment_count` 已建模，用于后续点评与红点；**点评完成后不会向用户发系统推送**，仅靠客户端再次请求接口展示。

### 2.4 种子数据如何准备「可下发」的任务

启动时 `DatabaseInitializer`（`Database:EnsureCreated=true`）：

1. 写入固定任务模板（如：贴闭口贴、抿唇、弹唇啵啵操、拉纽扣、捏鼻踱步、按N点）；
2. 默认写入 **3 周 × 7 天** 的 `training_plan`，每天 `task_ids` 为同一套固定任务 ID CSV。

因此营期内用户每天打开 App，拿到的是同一套固定任务（方案按天轮询，但内容相同）。若需按天不同任务，改 `training_plan.task_ids` 即可，无需改推送代码。

---

## 3. 基础设施：Hangfire 后台任务

Hangfire 是服务端 **定时 / 后台作业** 能力，**不是** 面向用户的消息推送通道。当前也未用其做「每日提醒」「点评通知」等。

### 3.1 依赖与配置

- 包：`Hangfire.AspNetCore`、`Hangfire.Core`、`Hangfire.MemoryStorage`（内存存储，进程重启任务状态不持久）
- 配置（`BM.Service/appsettings.json`）：

```json
"Hangfire": {
  "DashboardEnabled": false
}
```

- Dashboard 默认关闭；设为 `true` 时可访问 Hangfire 面板（生产环境需自行加鉴权）。

### 3.2 注册与启动

`StartupExtensions`：

1. `services.AddHangfire(... UseMemoryStorage())` + `AddHangfireServer()`
2. 管道中 `UseHangfireServer`，按配置可选 `UseHangfireDashboard`
3. `AddHangfireJob`：扫描已加载的 `BM.Service*.dll`，找出实现 `IJob` 的类，对每个：

```csharp
RecurringJob.AddOrUpdate(
    () => job.Execute(),
    job.CronExpression,
    TimeZoneInfo.Local,
    "wms"   // 队列名
);
```

### 3.3 作业接口

```csharp
// BM.Service.Core/Job/IJob.cs
public interface IJob
{
    string CronExpression { get; }
    Task Execute();
}
```

### 3.4 现有作业

| 类 | Cron | 行为 |
|----|------|------|
| `TestJob` | `Hangfire.Cron.Hourly(3)`（每小时第 3 分钟） | `POST` 外部示例 URL，与业务无关 |

**无**「扫描未提交用户并发推送」「点评完成通知」等业务 Job。

### 3.5 如何新增业务定时任务（扩展点）

1. 新建类实现 `IJob`，放到会被 `BM.Service*.dll` 加载的程序集中；
2. 在 DI 中注册该类型（与现有动态注入方式一致，保证 `GetService(implementType)` 能取到）；
3. 实现 `CronExpression` 与 `Execute`（例如：每日固定时间生成次日计划、或调用第三方推送 API）；
4. 重启服务后由 `AddHangfireJob` 自动注册为循环任务。

注意：当前存储为 **MemoryStorage**，多实例部署或不适合生产持久任务；上线定时推送前建议改为 SQL Server / Redis 等持久存储。

---

## 4. 前端侧

| 项目 | 现状 |
|------|------|
| `manifest.json` | 未配置 push 模块、未配置微信订阅消息 |
| 权限 | Android 权限列表无专用推送相关声明（无厂商推送集成） |
| 业务请求 | 首页拉今日计划、打卡上传、提交；无设备 token 上报接口 |
| 教学视频 | 本地 `static/videos` 按任务名匹配，与消息推送无关 |

用户感知到的「今天有新任务」，来自打开页面后接口返回的 `daily_plan` + `tasks`，而不是系统通知栏消息。

---

## 5. 与「提醒 / 红点」相关的数据设计（预留）

这些字段便于后续做站内提醒或真实推送，**目前没有消费它们的推送服务**。

| 表 / 字段 | 用途 |
|-----------|------|
| `daily_plan.comment_count` | 被点评数量，可用于红点 |
| `daily_plan.status = commented` | 点评完成状态 |
| `task_checkin.comment_id` | 关联点评记录 |
| `task_checkin.comment_status` | `none` / `completed` / `replied` |
| `comment_record` | 老师点评内容、家长回复 |

---

## 6. 与「真正的消息推送」的差距与建议

若产品需要「到点提醒训练」「点评完成弹系统通知」，需要单独建设，当前代码库 **不能直接当推送系统使用**。建议分层：

### 6.1 推荐架构（示意）

```
业务事件（提交 / 点评 / 定时扫描）
        │
        ▼
  通知服务（内部）  ──►  通知记录表（站内信）
        │
        ├──► 微信订阅消息（小程序）
        ├──► uni-push / 厂商推送（App）
        └──► （可选）短信
```

### 6.2 可落地步骤（概要）

1. **站内信表**：`user_id, title, body, type, ref_id, is_read, create_time`
2. **触发点**：
   - 老师点评写入 `comment_record` 时写站内信 + `comment_count++`；
   - 或 Hangfire 每日 Job 扫描 `draft` 且未完成用户，写提醒；
3. **通道**：
   - 小程序：用户授权订阅消息模板，后端调微信 API；
   - App：集成 uni-push，登录后上报 `client_id`；
4. **客户端**：消息中心列表 + 红点（可读 `comment_count` / 未读站内信数）。

### 6.3 与现有日计划下发的关系

- **不必**改成服务端半夜批量建计划才能「推任务」；现有按需生成已足够保证打开即有今日任务。
- 若希望「未打开 App 也有提醒」，再叠加定时 Job + 第三方推送即可，日计划仍可在用户打开时创建。

---

## 7. 关键文件索引

| 路径 | 说明 |
|------|------|
| `BM.Service.Business/Services/DailyPlanService.cs` | 今日计划生成与提交 |
| `BM.Service.Business/Services/CheckinService.cs` | 打卡上传 |
| `BM.Service/DatabaseInitializer.cs` | 任务模板与 3×7 方案种子 |
| `BM.Service.Core/Models/dailyPlanEntity.cs` | 日计划表 |
| `BM.Service.Core/Models/taskCheckinEntity.cs` | 打卡表 |
| `BM.Service.Core/Models/trainingPlanEntity.cs` | 总方案表 |
| `BM.Service.Core/Models/commentRecordEntity.cs` | 点评表 |
| `BM.Service.Core/Job/IJob.cs` | 定时任务接口 |
| `BM.Service.Core/Job/TestJob.cs` | 示例 Job |
| `BM.Service.Core/Extentions/StartupExtensions.cs` | Hangfire 注册与 Job 扫描 |
| `BM.Service/appsettings.json` | Hangfire Dashboard 开关 |
| `docs/API_Daily.md` | 日计划 HTTP 接口说明 |
| `docs/API_Checkin.md` | 打卡上传接口说明 |

---

## 8. FAQ

**Q：服务端会不会每天自动给所有用户建好计划？**  
A：不会。只有用户调用 `GET /api/daily/today` 时才会为该用户创建当日记录。

**Q：Hangfire 的 TestJob 会不会推消息给用户？**  
A：不会。它只是对外部 URL 发 HTTP POST 的示例。

**Q：点评后家长手机能收到通知吗？**  
A：当前不能。只有再次打开 App/小程序拉取数据后，才能看到点评相关状态（若前端已对接展示）。

**Q：改每天任务内容要改推送吗？**  
A：改 `task_template` / `training_plan.task_ids`（或种子逻辑）即可；「下发」逻辑仍走 `GetTodayAsync`。

---

## 9. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-23 | 初版：梳理现状（按需下发 + Hangfire 框架，无第三方消息推送） |
