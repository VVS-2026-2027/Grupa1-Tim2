using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EarnLearn.Migrations
{
    /// <inheritdoc />
    public partial class DodajPredmeteIKorisnikPredmet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KorisnikPredmet",
                columns: table => new
                {
                    idKorisnika = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    idPredmeta = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KorisnikPredmet", x => new { x.idKorisnika, x.idPredmeta });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Predmet",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    naziv = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predmet", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Predmet",
                columns: new[] { "id", "naziv" },
                values: new object[,]
                {
                    { 1, "Administracija računarskih mreža" },
                    { 2, "Aktuatori" },
                    { 3, "Algoritmi i strukture podataka" },
                    { 4, "Analiza signala i sistema" },
                    { 5, "Analogna elektronika" },
                    { 6, "Antene i prostiranje talasa" },
                    { 7, "Automati i formalni jezici" },
                    { 8, "CAD-CAM inžinjering" },
                    { 9, "Digitalna elektronika" },
                    { 10, "Digitalni integrirani krugovi" },
                    { 11, "Digitalni sistemi upravljanja" },
                    { 12, "Digitalno procesiranje signala" },
                    { 13, "Dinamika fluida i toplotnih sistema" },
                    { 14, "Dinamički sistemi" },
                    { 15, "Diskretna matematika" },
                    { 16, "Dizajn i arhitektura softverskih sistema" },
                    { 17, "Električna mjerenja" },
                    { 18, "Električna postrojenja" },
                    { 19, "Električne instalacije i mjere sigurnosti" },
                    { 20, "Električne mašine" },
                    { 21, "Električni krugovi I" },
                    { 22, "Električni krugovi 2" },
                    { 23, "Električni sistemi u transportu" },
                    { 24, "Elektroenergetski sistemi" },
                    { 25, "Elektromotorni pogoni" },
                    { 26, "Elektronika TK 1" },
                    { 27, "Elektronika TK 2" },
                    { 28, "Elektronički elementi i sklopovi" },
                    { 29, "Elektrotehnički materijali" },
                    { 30, "Elektrotermička konverzija energije" },
                    { 31, "Energetska elektronika" },
                    { 32, "Inženjerska ekonomika" },
                    { 33, "Inženjerska elektromagnetika" },
                    { 34, "Inženjerska fizika I" },
                    { 35, "Inženjerska fizika II" },
                    { 36, "Inžinjerska matematika I" },
                    { 37, "Inžinjerska matematika II" },
                    { 38, "Inžinjerska matematika 3" },
                    { 39, "Kanalno kodiranje" },
                    { 40, "Komponente i tehnologije" },
                    { 41, "Komunikacijski protokoli i mreže" },
                    { 42, "Komutacioni sistemi" },
                    { 43, "Kvalitet električne energije" },
                    { 44, "Linearna algebra i geometrija" },
                    { 45, "Linearni sistemi automatskog upravljanja" },
                    { 46, "Logički dizajn" },
                    { 47, "Matematička logika i teorija izračunljivosti" },
                    { 48, "Mehatronika" },
                    { 49, "Mikrovalni komunikacijski sistemi" },
                    { 50, "Mjerenja u telekomunikacijama" },
                    { 51, "Mobilne komunikacije" },
                    { 52, "Modeliranje i simulacija" },
                    { 53, "Nove generacije mreža i usluga" },
                    { 54, "Numerički algoritmi" },
                    { 55, "Objektno orijentisana analiza i dizajn" },
                    { 56, "Održavanje električnih sistema" },
                    { 57, "Operativni sistemi" },
                    { 58, "Organizacija i osnove upravljanja mrežom" },
                    { 59, "Organizacija softverskog projekta" },
                    { 60, "Osnove baza podataka" },
                    { 61, "Osnove elektroenergetskih sistema" },
                    { 62, "Osnove elektrotehnike" },
                    { 63, "Osnove informacionih sistema" },
                    { 64, "Osnove mehatronike" },
                    { 65, "Osnove operacionih istraživanja" },
                    { 66, "Osnove optoelektronike" },
                    { 67, "Osnove računarskih mreža" },
                    { 68, "Osnove računarstva" },
                    { 69, "Osnove sistema automatskog upravljanja" },
                    { 70, "Osnove telekomunikacija" },
                    { 71, "Osnovi signalizacionih protokola" },
                    { 72, "Poslovni web sistemi" },
                    { 73, "Pouzdanost električnih elemenata i sistema" },
                    { 74, "Programski jezici i prevodioci" },
                    { 75, "Proizvodnja električne energije" },
                    { 76, "Projektovanje i sinteza digitalnih sistema" },
                    { 77, "Projektovanje informacionih sistema" },
                    { 78, "Projektovanje logičkih sistema" },
                    { 79, "Projektovanje mikroprocersorskih sistema" },
                    { 80, "Radiotehnika" },
                    { 81, "Razvoj mobilnih aplikacija" },
                    { 82, "Razvoj programskih rješenja" },
                    { 83, "Računarska grafika" },
                    { 84, "Računarske arhitekture" },
                    { 85, "Računarsko modeliranje i simulacije" },
                    { 86, "Robotika 1" },
                    { 87, "Senzori i pretvarači" },
                    { 88, "Sistemsko programiranje" },
                    { 89, "Softverski inženjering" },
                    { 90, "Statistička teorija signala" },
                    { 91, "Strukture i režimi rada elektroenergetskih sistema" },
                    { 92, "Tehnika visokog napona" },
                    { 93, "Tehnike programiranja" },
                    { 94, "Tehnologija visokonaponske izolacije" },
                    { 95, "Tehnologije televizije" },
                    { 96, "Telekomunikacione tehnike 1" },
                    { 97, "Telekomunikacione tehnike II" },
                    { 98, "Teorija elektromagnetnih polja" },
                    { 99, "Teorija informacija i izvorno kodiranje" },
                    { 100, "Teorija prometa" },
                    { 101, "Teorija signala" },
                    { 102, "Ugradbeni sistemi" },
                    { 103, "Upravljanje potrošnjom električne energije" },
                    { 104, "Verifikacija i validacija softvera" },
                    { 105, "Vjerovatnoća i statistika" },
                    { 106, "Vještačka inteligencija" },
                    { 107, "Web tehnologije" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KorisnikPredmet");

            migrationBuilder.DropTable(
                name: "Predmet");
        }
    }
}
