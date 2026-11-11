using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EarnLearn.Migrations
{
    /// <inheritdoc />
    public partial class DodajRecenzijaIdIPrijavljeniTutorId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrijavljeniTutorId",
                table: "Prijava",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "RecenzijaId",
                table: "Prijava",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrijavljeniTutorId",
                table: "Prijava");

            migrationBuilder.DropColumn(
                name: "RecenzijaId",
                table: "Prijava");
        }
    }
}
