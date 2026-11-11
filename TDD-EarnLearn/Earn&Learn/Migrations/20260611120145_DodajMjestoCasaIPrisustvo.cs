using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EarnLearn.Migrations
{
    /// <inheritdoc />
    public partial class DodajMjestoCasaIPrisustvo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mjestoCasa",
                table: "Termin",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "prisustvoPotvrdjeno",
                table: "Termin",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "idTermina",
                table: "Obavjestenje",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mjestoCasa",
                table: "Termin");

            migrationBuilder.DropColumn(
                name: "prisustvoPotvrdjeno",
                table: "Termin");

            migrationBuilder.DropColumn(
                name: "idTermina",
                table: "Obavjestenje");
        }
    }
}
