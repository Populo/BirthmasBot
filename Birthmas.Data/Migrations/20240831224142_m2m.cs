using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Birthmas.Data.Migrations
{
    /// <inheritdoc />
    public partial class m2m : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Birthmas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PersonUserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ServerConfigServerId = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Birthmas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Birthmas_People_PersonUserId",
                        column: x => x.PersonUserId,
                        principalTable: "People",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Birthmas_ServerConfigs_ServerConfigServerId",
                        column: x => x.ServerConfigServerId,
                        principalTable: "ServerConfigs",
                        principalColumn: "ServerId");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Birthmas_PersonUserId",
                table: "Birthmas",
                column: "PersonUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Birthmas_ServerConfigServerId",
                table: "Birthmas",
                column: "ServerConfigServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Birthmas");
        }
    }
}
