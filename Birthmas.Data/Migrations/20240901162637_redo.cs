using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Birthmas.Data.Migrations
{
    /// <inheritdoc />
    public partial class redo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Birthmas");

            migrationBuilder.DropPrimaryKey(
                name: "PK_People",
                table: "People");

            migrationBuilder.AlterColumn<ulong>(
                name: "UserId",
                table: "People",
                type: "bigint unsigned",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "People",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<ulong>(
                name: "ServerId",
                table: "People",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddPrimaryKey(
                name: "PK_People",
                table: "People",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_People_ServerId",
                table: "People",
                column: "ServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_People_ServerConfigs_ServerId",
                table: "People",
                column: "ServerId",
                principalTable: "ServerConfigs",
                principalColumn: "ServerId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_People_ServerConfigs_ServerId",
                table: "People");

            migrationBuilder.DropPrimaryKey(
                name: "PK_People",
                table: "People");

            migrationBuilder.DropIndex(
                name: "IX_People_ServerId",
                table: "People");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "People");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "People");

            migrationBuilder.AlterColumn<ulong>(
                name: "UserId",
                table: "People",
                type: "bigint unsigned",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_People",
                table: "People",
                column: "UserId");

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
    }
}
