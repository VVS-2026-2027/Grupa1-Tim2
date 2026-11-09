using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EarnLearn.Migrations
{
    /// <inheritdoc />
    public partial class DodajPredmetiNaCekanjuJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PredmetiNaCekanjuJson",
                table: "AspNetUsers",
                type: "longtext",
                nullable: false,
                defaultValue: "[]") // ← FIX
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PredmetiNaCekanjuJson",
                table: "AspNetUsers");
        }
    }
}