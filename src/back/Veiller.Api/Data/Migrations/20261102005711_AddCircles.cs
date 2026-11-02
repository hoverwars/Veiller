using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veiller.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCircles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                table: "users",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "circles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_circles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_circles_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "circle_members",
                columns: table => new
                {
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsSponsor = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_circle_members", x => new { x.CircleId, x.UserId });
                    table.ForeignKey(
                        name: "FK_circle_members_circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_circle_members_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_circle_members_CircleId",
                table: "circle_members",
                column: "CircleId",
                unique: true,
                filter: "\"Role\" = 'Admin'");

            migrationBuilder.CreateIndex(
                name: "IX_circle_members_UserId",
                table: "circle_members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_circles_OwnerUserId",
                table: "circles",
                column: "OwnerUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "circle_members");

            migrationBuilder.DropTable(
                name: "circles");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "users");
        }
    }
}
