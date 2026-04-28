using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace B2B.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsAndPushTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DevicePushTokens",
                schema: "app",
                columns: table => new
                {
                    DevicePushTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Platform = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Token = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "sysutcdatetime()"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevicePushTokens", x => x.DevicePushTokenId);
                    table.ForeignKey(
                        name: "FK_DevicePushTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "app",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "app",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Target = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "app",
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveries",
                schema: "app",
                columns: table => new
                {
                    NotificationDeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DevicePushTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveries", x => x.NotificationDeliveryId);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveries_DevicePushTokens_DevicePushTokenId",
                        column: x => x.DevicePushTokenId,
                        principalSchema: "app",
                        principalTable: "DevicePushTokens",
                        principalColumn: "DevicePushTokenId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveries_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalSchema: "app",
                        principalTable: "Notifications",
                        principalColumn: "NotificationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveries_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "app",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NotificationReads",
                schema: "app",
                columns: table => new
                {
                    NotificationReadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationReads", x => x.NotificationReadId);
                    table.ForeignKey(
                        name: "FK_NotificationReads_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalSchema: "app",
                        principalTable: "Notifications",
                        principalColumn: "NotificationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationReads_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "app",
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DevicePushTokens_Platform_Token",
                schema: "app",
                table: "DevicePushTokens",
                columns: new[] { "Platform", "Token" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DevicePushTokens_UserId_IsActive_LastSeenAtUtc",
                schema: "app",
                table: "DevicePushTokens",
                columns: new[] { "UserId", "IsActive", "LastSeenAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_DevicePushTokenId_SentAtUtc",
                schema: "app",
                table: "NotificationDeliveries",
                columns: new[] { "DevicePushTokenId", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_NotificationId_SentAtUtc",
                schema: "app",
                table: "NotificationDeliveries",
                columns: new[] { "NotificationId", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_UserId",
                schema: "app",
                table: "NotificationDeliveries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReads_NotificationId",
                schema: "app",
                table: "NotificationReads",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReads_ReadAtUtc",
                schema: "app",
                table: "NotificationReads",
                column: "ReadAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReads_UserId_NotificationId",
                schema: "app",
                table: "NotificationReads",
                columns: new[] { "UserId", "NotificationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedAtUtc",
                schema: "app",
                table: "Notifications",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedByUserId",
                schema: "app",
                table: "Notifications",
                column: "CreatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDeliveries",
                schema: "app");

            migrationBuilder.DropTable(
                name: "NotificationReads",
                schema: "app");

            migrationBuilder.DropTable(
                name: "DevicePushTokens",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "app");
        }
    }
}
