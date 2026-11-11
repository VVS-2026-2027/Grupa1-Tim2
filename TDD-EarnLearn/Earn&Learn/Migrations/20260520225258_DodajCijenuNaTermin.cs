using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EarnLearn.Migrations
{
    /// <inheritdoc />
    public partial class DodajCijenuNaTermin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "qrKod",
                table: "Termin",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<double>(
                name: "cijena",
                table: "Termin",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "idPredmeta",
                table: "Termin",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "idTutora",
                table: "Recenzija",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "idStudenta",
                table: "Recenzija",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "datumRecenzije",
                table: "Recenzija",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cijena",
                table: "Termin");

            migrationBuilder.DropColumn(
                name: "idPredmeta",
                table: "Termin");

            migrationBuilder.DropColumn(
                name: "datumRecenzije",
                table: "Recenzija");

            migrationBuilder.UpdateData(
                table: "Termin",
                keyColumn: "qrKod",
                keyValue: null,
                column: "qrKod",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "qrKod",
                table: "Termin",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "idTutora",
                table: "Recenzija",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "idStudenta",
                table: "Recenzija",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
