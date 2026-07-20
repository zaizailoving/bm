using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BM.Service.Core.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "coins_log",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    userid = table.Column<int>(name: "user_id", type: "int", nullable: false),
                    changeamount = table.Column<int>(name: "change_amount", type: "int", nullable: false),
                    balance = table.Column<int>(type: "int", nullable: false),
                    sourcetype = table.Column<string>(name: "source_type", type: "nvarchar(50)", maxLength: 50, nullable: true),
                    sourceid = table.Column<int>(name: "source_id", type: "int", nullable: true),
                    createtime = table.Column<DateTime>(name: "create_time", type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coins_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "comment_record",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    taskcheckinid = table.Column<int>(name: "task_checkin_id", type: "int", nullable: false),
                    teacherid = table.Column<int>(name: "teacher_id", type: "int", nullable: false),
                    rating = table.Column<byte>(type: "tinyint", nullable: false),
                    content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    replycontent = table.Column<string>(name: "reply_content", type: "nvarchar(max)", nullable: true),
                    replytime = table.Column<DateTime>(name: "reply_time", type: "datetime2", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    createtime = table.Column<DateTime>(name: "create_time", type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comment_record", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "daily_plan",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    userid = table.Column<int>(name: "user_id", type: "int", nullable: false),
                    plandate = table.Column<DateTime>(name: "plan_date", type: "date", nullable: false),
                    weekno = table.Column<int>(name: "week_no", type: "int", nullable: true),
                    dayno = table.Column<int>(name: "day_no", type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    progress = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    commentcount = table.Column<int>(name: "comment_count", type: "int", nullable: false),
                    submittime = table.Column<DateTime>(name: "submit_time", type: "datetime2", nullable: true),
                    createtime = table.Column<DateTime>(name: "create_time", type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_plan", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "task_checkin",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    dailyplanid = table.Column<int>(name: "daily_plan_id", type: "int", nullable: false),
                    tasktemplateid = table.Column<int>(name: "task_template_id", type: "int", nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    videourl = table.Column<string>(name: "video_url", type: "nvarchar(255)", maxLength: 255, nullable: true),
                    imageurls = table.Column<string>(name: "image_urls", type: "nvarchar(max)", nullable: true),
                    commentid = table.Column<int>(name: "comment_id", type: "int", nullable: false),
                    commentstatus = table.Column<string>(name: "comment_status", type: "nvarchar(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    createtime = table.Column<DateTime>(name: "create_time", type: "datetime2", nullable: false),
                    updatetime = table.Column<DateTime>(name: "update_time", type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_checkin", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "task_template",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    iconurl = table.Column<string>(name: "icon_url", type: "nvarchar(255)", maxLength: 255, nullable: true),
                    requirement = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    teachvideourl = table.Column<string>(name: "teach_video_url", type: "nvarchar(255)", maxLength: 255, nullable: true),
                    sortorder = table.Column<int>(name: "sort_order", type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_template", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "training_plan",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    weekno = table.Column<int>(name: "week_no", type: "int", nullable: false),
                    dayno = table.Column<int>(name: "day_no", type: "int", nullable: false),
                    taskids = table.Column<string>(name: "task_ids", type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_plan", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    passwordhash = table.Column<string>(name: "password_hash", type: "nvarchar(255)", maxLength: 255, nullable: false),
                    nickname = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    avatar = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    archiveno = table.Column<string>(name: "archive_no", type: "nvarchar(20)", maxLength: 20, nullable: true),
                    traincampstatus = table.Column<string>(name: "train_camp_status", type: "nvarchar(20)", maxLength: 20, nullable: false),
                    totalcoins = table.Column<int>(name: "total_coins", type: "int", nullable: false),
                    availablecoins = table.Column<int>(name: "available_coins", type: "int", nullable: false),
                    lastlogintime = table.Column<DateTime>(name: "last_login_time", type: "datetime2", nullable: true),
                    lastloginip = table.Column<string>(name: "last_login_ip", type: "nvarchar(45)", maxLength: 45, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    createtime = table.Column<DateTime>(name: "create_time", type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_relation",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    teacherid = table.Column<int>(name: "teacher_id", type: "int", nullable: false),
                    studentid = table.Column<int>(name: "student_id", type: "int", nullable: false),
                    createtime = table.Column<DateTime>(name: "create_time", type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_relation", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_user_time",
                table: "coins_log",
                columns: new[] { "user_id", "create_time" });

            migrationBuilder.CreateIndex(
                name: "idx_checkin",
                table: "comment_record",
                column: "task_checkin_id");

            migrationBuilder.CreateIndex(
                name: "idx_date_status",
                table: "daily_plan",
                columns: new[] { "plan_date", "status" });

            migrationBuilder.CreateIndex(
                name: "uniq_user_date",
                table: "daily_plan",
                columns: new[] { "user_id", "plan_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_daily_plan",
                table: "task_checkin",
                column: "daily_plan_id");

            migrationBuilder.CreateIndex(
                name: "uniq_week_day",
                table: "training_plan",
                columns: new[] { "week_no", "day_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uniq_archive_no",
                table: "user",
                column: "archive_no",
                unique: true,
                filter: "[archive_no] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uniq_phone",
                table: "user",
                column: "phone",
                unique: true,
                filter: "[phone] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uniq_username",
                table: "user",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_student",
                table: "user_relation",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "uniq_teacher_student",
                table: "user_relation",
                columns: new[] { "teacher_id", "student_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coins_log");

            migrationBuilder.DropTable(
                name: "comment_record");

            migrationBuilder.DropTable(
                name: "daily_plan");

            migrationBuilder.DropTable(
                name: "task_checkin");

            migrationBuilder.DropTable(
                name: "task_template");

            migrationBuilder.DropTable(
                name: "training_plan");

            migrationBuilder.DropTable(
                name: "user");

            migrationBuilder.DropTable(
                name: "user_relation");
        }
    }
}
