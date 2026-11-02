using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veiller.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "health_sync_status",
                columns: table => new
                {
                    ElderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastUploadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WatchLastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_sync_status", x => x.ElderId);
                    table.ForeignKey(
                        name: "FK_health_sync_status_users_ElderId",
                        column: x => x.ElderId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "heart_rate_records",
                columns: table => new
                {
                    ElderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    HcRecordId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Origin = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_heart_rate_records", x => new { x.ElderId, x.HcRecordId });
                    table.ForeignKey(
                        name: "FK_heart_rate_records_users_ElderId",
                        column: x => x.ElderId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sleep_sessions",
                columns: table => new
                {
                    ElderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    HcRecordId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ZoneOffset = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    Origin = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sleep_sessions", x => new { x.ElderId, x.HcRecordId });
                    table.ForeignKey(
                        name: "FK_sleep_sessions_users_ElderId",
                        column: x => x.ElderId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "steps_hourly",
                columns: table => new
                {
                    ElderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_steps_hourly", x => new { x.ElderId, x.Start });
                    table.ForeignKey(
                        name: "FK_steps_hourly_users_ElderId",
                        column: x => x.ElderId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "heart_rate_samples",
                columns: table => new
                {
                    ElderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    HcRecordId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Bpm = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_heart_rate_samples", x => new { x.ElderId, x.HcRecordId, x.Time });
                    table.ForeignKey(
                        name: "FK_heart_rate_samples_heart_rate_records_ElderId_HcRecordId",
                        columns: x => new { x.ElderId, x.HcRecordId },
                        principalTable: "heart_rate_records",
                        principalColumns: new[] { "ElderId", "HcRecordId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sleep_stages",
                columns: table => new
                {
                    ElderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    HcRecordId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sleep_stages", x => new { x.ElderId, x.HcRecordId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_sleep_stages_sleep_sessions_ElderId_HcRecordId",
                        columns: x => new { x.ElderId, x.HcRecordId },
                        principalTable: "sleep_sessions",
                        principalColumns: new[] { "ElderId", "HcRecordId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_heart_rate_samples_ElderId_Time",
                table: "heart_rate_samples",
                columns: new[] { "ElderId", "Time" });

            migrationBuilder.CreateIndex(
                name: "IX_sleep_sessions_ElderId_End",
                table: "sleep_sessions",
                columns: new[] { "ElderId", "End" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "health_sync_status");

            migrationBuilder.DropTable(
                name: "heart_rate_samples");

            migrationBuilder.DropTable(
                name: "sleep_stages");

            migrationBuilder.DropTable(
                name: "steps_hourly");

            migrationBuilder.DropTable(
                name: "heart_rate_records");

            migrationBuilder.DropTable(
                name: "sleep_sessions");
        }
    }
}
