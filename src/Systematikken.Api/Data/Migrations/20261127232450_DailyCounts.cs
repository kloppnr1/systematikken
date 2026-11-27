using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Systematikken.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DailyCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyCounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCounts_Day_Name_Source",
                table: "DailyCounts",
                columns: new[] { "Day", "Name", "Source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyCounts");
        }
    }
}
