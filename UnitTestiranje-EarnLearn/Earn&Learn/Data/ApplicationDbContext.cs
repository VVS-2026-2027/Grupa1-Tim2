using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Earn_Learn.Models;

namespace Earn_Learn.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : IdentityDbContext<Korisnik>(options)
    {
        public DbSet<Earn_Learn.Models.Transakcija> Transakcija { get; set; } = default!;
        public DbSet<Earn_Learn.Models.Obavjestenje> Obavjestenje { get; set; } = default!;
        public DbSet<Earn_Learn.Models.Recenzija> Recenzija { get; set; } = default!;
        public DbSet<Earn_Learn.Models.Termin> Termin { get; set; } = default!;
        public DbSet<Earn_Learn.Models.Predmet> Predmet { get; set; } = default!;
        public DbSet<Earn_Learn.Models.KorisnikPredmet> KorisnikPredmet { get; set; } = default!;
        public DbSet<Earn_Learn.Models.Prijava> Prijava { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<KorisnikPredmet>()
                .HasKey(kp => new { kp.idKorisnika, kp.idPredmeta });

            builder.Entity<Predmet>().HasData(
                new Predmet { id = 1, naziv = "Administracija računarskih mreža" },
                new Predmet { id = 2, naziv = "Aktuatori" },
                new Predmet { id = 3, naziv = "Algoritmi i strukture podataka" },
                new Predmet { id = 4, naziv = "Analiza signala i sistema" },
                new Predmet { id = 5, naziv = "Analogna elektronika" },
                new Predmet { id = 6, naziv = "Antene i prostiranje talasa" },
                new Predmet { id = 7, naziv = "Automati i formalni jezici" },
                new Predmet { id = 8, naziv = "CAD-CAM inžinjering" },
                new Predmet { id = 9, naziv = "Digitalna elektronika" },
                new Predmet { id = 10, naziv = "Digitalni integrirani krugovi" },
                new Predmet { id = 11, naziv = "Digitalni sistemi upravljanja" },
                new Predmet { id = 12, naziv = "Digitalno procesiranje signala" },
                new Predmet { id = 13, naziv = "Dinamika fluida i toplotnih sistema" },
                new Predmet { id = 14, naziv = "Dinamički sistemi" },
                new Predmet { id = 15, naziv = "Diskretna matematika" },
                new Predmet { id = 16, naziv = "Dizajn i arhitektura softverskih sistema" },
                new Predmet { id = 17, naziv = "Električna mjerenja" },
                new Predmet { id = 18, naziv = "Električna postrojenja" },
                new Predmet { id = 19, naziv = "Električne instalacije i mjere sigurnosti" },
                new Predmet { id = 20, naziv = "Električne mašine" },
                new Predmet { id = 21, naziv = "Električni krugovi I" },
                new Predmet { id = 22, naziv = "Električni krugovi 2" },
                new Predmet { id = 23, naziv = "Električni sistemi u transportu" },
                new Predmet { id = 24, naziv = "Elektroenergetski sistemi" },
                new Predmet { id = 25, naziv = "Elektromotorni pogoni" },
                new Predmet { id = 26, naziv = "Elektronika TK 1" },
                new Predmet { id = 27, naziv = "Elektronika TK 2" },
                new Predmet { id = 28, naziv = "Elektronički elementi i sklopovi" },
                new Predmet { id = 29, naziv = "Elektrotehnički materijali" },
                new Predmet { id = 30, naziv = "Elektrotermička konverzija energije" },
                new Predmet { id = 31, naziv = "Energetska elektronika" },
                new Predmet { id = 32, naziv = "Inženjerska ekonomika" },
                new Predmet { id = 33, naziv = "Inženjerska elektromagnetika" },
                new Predmet { id = 34, naziv = "Inženjerska fizika I" },
                new Predmet { id = 35, naziv = "Inženjerska fizika II" },
                new Predmet { id = 36, naziv = "Inžinjerska matematika I" },
                new Predmet { id = 37, naziv = "Inžinjerska matematika II" },
                new Predmet { id = 38, naziv = "Inžinjerska matematika 3" },
                new Predmet { id = 39, naziv = "Kanalno kodiranje" },
                new Predmet { id = 40, naziv = "Komponente i tehnologije" },
                new Predmet { id = 41, naziv = "Komunikacijski protokoli i mreže" },
                new Predmet { id = 42, naziv = "Komutacioni sistemi" },
                new Predmet { id = 43, naziv = "Kvalitet električne energije" },
                new Predmet { id = 44, naziv = "Linearna algebra i geometrija" },
                new Predmet { id = 45, naziv = "Linearni sistemi automatskog upravljanja" },
                new Predmet { id = 46, naziv = "Logički dizajn" },
                new Predmet { id = 47, naziv = "Matematička logika i teorija izračunljivosti" },
                new Predmet { id = 48, naziv = "Mehatronika" },
                new Predmet { id = 49, naziv = "Mikrovalni komunikacijski sistemi" },
                new Predmet { id = 50, naziv = "Mjerenja u telekomunikacijama" },
                new Predmet { id = 51, naziv = "Mobilne komunikacije" },
                new Predmet { id = 52, naziv = "Modeliranje i simulacija" },
                new Predmet { id = 53, naziv = "Nove generacije mreža i usluga" },
                new Predmet { id = 54, naziv = "Numerički algoritmi" },
                new Predmet { id = 55, naziv = "Objektno orijentisana analiza i dizajn" },
                new Predmet { id = 56, naziv = "Održavanje električnih sistema" },
                new Predmet { id = 57, naziv = "Operativni sistemi" },
                new Predmet { id = 58, naziv = "Organizacija i osnove upravljanja mrežom" },
                new Predmet { id = 59, naziv = "Organizacija softverskog projekta" },
                new Predmet { id = 60, naziv = "Osnove baza podataka" },
                new Predmet { id = 61, naziv = "Osnove elektroenergetskih sistema" },
                new Predmet { id = 62, naziv = "Osnove elektrotehnike" },
                new Predmet { id = 63, naziv = "Osnove informacionih sistema" },
                new Predmet { id = 64, naziv = "Osnove mehatronike" },
                new Predmet { id = 65, naziv = "Osnove operacionih istraživanja" },
                new Predmet { id = 66, naziv = "Osnove optoelektronike" },
                new Predmet { id = 67, naziv = "Osnove računarskih mreža" },
                new Predmet { id = 68, naziv = "Osnove računarstva" },
                new Predmet { id = 69, naziv = "Osnove sistema automatskog upravljanja" },
                new Predmet { id = 70, naziv = "Osnove telekomunikacija" },
                new Predmet { id = 71, naziv = "Osnovi signalizacionih protokola" },
                new Predmet { id = 72, naziv = "Poslovni web sistemi" },
                new Predmet { id = 73, naziv = "Pouzdanost električnih elemenata i sistema" },
                new Predmet { id = 74, naziv = "Programski jezici i prevodioci" },
                new Predmet { id = 75, naziv = "Proizvodnja električne energije" },
                new Predmet { id = 76, naziv = "Projektovanje i sinteza digitalnih sistema" },
                new Predmet { id = 77, naziv = "Projektovanje informacionih sistema" },
                new Predmet { id = 78, naziv = "Projektovanje logičkih sistema" },
                new Predmet { id = 79, naziv = "Projektovanje mikroprocersorskih sistema" },
                new Predmet { id = 80, naziv = "Radiotehnika" },
                new Predmet { id = 81, naziv = "Razvoj mobilnih aplikacija" },
                new Predmet { id = 82, naziv = "Razvoj programskih rješenja" },
                new Predmet { id = 83, naziv = "Računarska grafika" },
                new Predmet { id = 84, naziv = "Računarske arhitekture" },
                new Predmet { id = 85, naziv = "Računarsko modeliranje i simulacije" },
                new Predmet { id = 86, naziv = "Robotika 1" },
                new Predmet { id = 87, naziv = "Senzori i pretvarači" },
                new Predmet { id = 88, naziv = "Sistemsko programiranje" },
                new Predmet { id = 89, naziv = "Softverski inženjering" },
                new Predmet { id = 90, naziv = "Statistička teorija signala" },
                new Predmet { id = 91, naziv = "Strukture i režimi rada elektroenergetskih sistema" },
                new Predmet { id = 92, naziv = "Tehnika visokog napona" },
                new Predmet { id = 93, naziv = "Tehnike programiranja" },
                new Predmet { id = 94, naziv = "Tehnologija visokonaponske izolacije" },
                new Predmet { id = 95, naziv = "Tehnologije televizije" },
                new Predmet { id = 96, naziv = "Telekomunikacione tehnike 1" },
                new Predmet { id = 97, naziv = "Telekomunikacione tehnike II" },
                new Predmet { id = 98, naziv = "Teorija elektromagnetnih polja" },
                new Predmet { id = 99, naziv = "Teorija informacija i izvorno kodiranje" },
                new Predmet { id = 100, naziv = "Teorija prometa" },
                new Predmet { id = 101, naziv = "Teorija signala" },
                new Predmet { id = 102, naziv = "Ugradbeni sistemi" },
                new Predmet { id = 103, naziv = "Upravljanje potrošnjom električne energije" },
                new Predmet { id = 104, naziv = "Verifikacija i validacija softvera" },
                new Predmet { id = 105, naziv = "Vjerovatnoća i statistika" },
                new Predmet { id = 106, naziv = "Vještačka inteligencija" },
                new Predmet { id = 107, naziv = "Web tehnologije" }
            );
        }
    }
}