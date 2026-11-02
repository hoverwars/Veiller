using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veiller.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invite_codes",
                columns: table => new
                {
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invite_codes", x => x.CircleId);
                    table.ForeignKey(
                        name: "FK_invite_codes_circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "join_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_join_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_join_requests_circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_join_requests_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invite_codes_Code",
                table: "invite_codes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_join_requests_CircleId_UserId",
                table: "join_requests",
                columns: new[] { "CircleId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_join_requests_UserId",
                table: "join_requests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invite_codes");

            migrationBuilder.DropTable(
                name: "join_requests");
        }
    }
}
