using Earn_Learn.Controllers;
using Earn_Learn.Data;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Earn_Learn.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;

namespace Earn_Learn.Tests
{
    [TestClass]
    public class StudentControllerPopustTests
    {
        private const int IdTermina = 100;

        // ---------- Pomoćne metode ----------

        private static Mock<UserManager<Korisnik>> UserManagerSaKorisnikom(Korisnik korisnik)
        {
            var store = new Mock<IUserStore<Korisnik>>();
            var userManager = new Mock<UserManager<Korisnik>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
            userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(korisnik);
            return userManager;
        }

        // Baza sa tutorom (20 KM po satu) i jednim slobodnim terminom tog tutora
        private static ApplicationDbContext NapraviBazu()
        {
            var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var baza = new ApplicationDbContext(opcije);

            baza.Users.Add(new Korisnik
            {
                Id = "tutor1",
                UserName = "tutor1",
                Uloga = Uloga.Tutor,
                CijenaPoSatu = 20
            });
            baza.Termin.Add(new Termin
            {
                id = IdTermina,
                idTutora = "tutor1",
                datumIVrijeme = DateTime.Now.AddDays(2),
                status = StatusTermina.Slobodan,
                cijena = 20
            });
            baza.SaveChanges();
            return baza;
        }

        private static void DodajCasove(ApplicationDbContext baza, string idStudenta,
            string idTutora, StatusTermina status, int broj)
        {
            for (int i = 0; i < broj; i++)
            {
                baza.Termin.Add(new Termin
                {
                    idStudenta = idStudenta,
                    idTutora = idTutora,
                    datumIVrijeme = DateTime.Now.AddDays(-i - 1),
                    status = status,
                    cijena = 20
                });
            }
            baza.SaveChanges();
        }

        private static Korisnik NapraviStudenta(double stanje)
        {
            return new Korisnik
            {
                Id = "student1",
                UserName = "student1",
                Ime = "Lejla",
                Prezime = "Student",
                Uloga = Uloga.Student,
                StanjeRacuna = stanje
            };
        }

        private static StudentController NapraviController(Korisnik student,
            ApplicationDbContext baza, IPopustServis servis)
        {
            var httpContext = new DefaultHttpContext();
            return new StudentController(UserManagerSaKorisnikom(student).Object, baza, servis)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            };
        }

        // ---------- Cijena sa popustom ----------

        [TestMethod]
        public async Task RezervisiTermin_BezOdrzanihCasova_CijenaJePuna()
        {
            using var baza = NapraviBazu();
            var controller = NapraviController(NapraviStudenta(100), baza, new PopustServis());

            await controller.RezervisiTermin(IdTermina, TipInstrukcija.Online);

            Assert.AreEqual(20.0, baza.Termin.Find(IdTermina)!.cijena, 0.001);
        }

        [TestMethod]
        [DataRow(5, 19.0)]
        [DataRow(9, 19.0)]
        [DataRow(10, 18.0)]
        [DataRow(15, 18.0)]
        public async Task RezervisiTermin_OdrzaniCasoviKodTutora_CijenaUmanjenaZaPopust(
            int brojCasova, double ocekivanaCijena)
        {
            using var baza = NapraviBazu();
            DodajCasove(baza, "student1", "tutor1", StatusTermina.Odrzan, brojCasova);
            var controller = NapraviController(NapraviStudenta(100), baza, new PopustServis());

            await controller.RezervisiTermin(IdTermina, TipInstrukcija.Online);

            Assert.AreEqual(ocekivanaCijena, baza.Termin.Find(IdTermina)!.cijena, 0.001);
        }

        [TestMethod]
        public async Task RezervisiTermin_CasoviKodDrugogTutora_NeRacunajuSeUPopust()
        {
            using var baza = NapraviBazu();
            DodajCasove(baza, "student1", "tutor2", StatusTermina.Odrzan, 10);
            var controller = NapraviController(NapraviStudenta(100), baza, new PopustServis());

            await controller.RezervisiTermin(IdTermina, TipInstrukcija.Online);

            Assert.AreEqual(20.0, baza.Termin.Find(IdTermina)!.cijena, 0.001);
        }

        [TestMethod]
        [DataRow(StatusTermina.Rezervisan)]
        [DataRow(StatusTermina.Potvrdjen)]
        [DataRow(StatusTermina.Otkazan)]
        public async Task RezervisiTermin_CasoviKojiNisuOdrzani_NeRacunajuSeUPopust(
            StatusTermina status)
        {
            using var baza = NapraviBazu();
            DodajCasove(baza, "student1", "tutor1", status, 10);
            var controller = NapraviController(NapraviStudenta(100), baza, new PopustServis());

            await controller.RezervisiTermin(IdTermina, TipInstrukcija.Online);

            Assert.AreEqual(20.0, baza.Termin.Find(IdTermina)!.cijena, 0.001);
        }

        [TestMethod]
        public async Task RezervisiTermin_PetOdrzanihCasova_ServisDobijaCijenuIBrojCasova()
        {
            using var baza = NapraviBazu();
            DodajCasove(baza, "student1", "tutor1", StatusTermina.Odrzan, 5);
            var servis = new Mock<IPopustServis>();
            servis.Setup(s => s.IzracunajCijenuSaPopustom(It.IsAny<double>(), It.IsAny<int>()))
                .Returns(19);
            var controller = NapraviController(NapraviStudenta(100), baza, servis.Object);

            await controller.RezervisiTermin(IdTermina, TipInstrukcija.Online);

            servis.Verify(s => s.IzracunajCijenuSaPopustom(20, 5), Times.Once);
        }
    }
}