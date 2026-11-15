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
    public class TutorControllerIsplataTests
    {
        // ---------- Pomoćne metode ----------

        private static Mock<UserManager<Korisnik>> UserManagerSaKorisnikom(Korisnik? korisnik)
        {
            var store = new Mock<IUserStore<Korisnik>>();
            var userManager = new Mock<UserManager<Korisnik>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
            userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(korisnik);
            userManager.Setup(m => m.UpdateAsync(It.IsAny<Korisnik>()))
                .ReturnsAsync(IdentityResult.Success);
            return userManager;
        }

        private static ApplicationDbContext NapraviBazu()
        {
            var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(opcije);
        }

        private static TutorController NapraviController(
            Mock<UserManager<Korisnik>> userManager, ApplicationDbContext baza, IIsplataServis servis)
        {
            var httpContext = new DefaultHttpContext();
            return new TutorController(userManager.Object, baza, servis)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            };
        }

        private static Korisnik NapraviTutora(double stanje)
        {
            return new Korisnik
            {
                Id = "tutor1", UserName = "tutor1", Ime = "Amra", Prezime = "Tutor",
                Uloga = Uloga.Tutor, StanjeRacuna = stanje
            };
        }

        // ---------- Pristup ----------

        [TestMethod]
        public async Task ZatraziIsplatu_KorisnikNijePrijavljen_PreusmjeravaNaHomeIndex()
        {
            using var baza = NapraviBazu();
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza, new IsplataServis());

            var result = await controller.ZatraziIsplatu(50) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task ZatraziIsplatu_KorisnikJeStudent_PreusmjeravaNaHomeIndexBezIzmjena()
        {
            using var baza = NapraviBazu();
            var student = new Korisnik { Id = "s1", Uloga = Uloga.Student, StanjeRacuna = 100 };
            var userManager = UserManagerSaKorisnikom(student);
            var controller = NapraviController(userManager, baza, new IsplataServis());

            var result = await controller.ZatraziIsplatu(50) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
            Assert.AreEqual(100.0, student.StanjeRacuna);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        // ---------- Odbijena isplata ----------

        [TestMethod]
        public async Task ZatraziIsplatu_ServisOdbijeIsplatu_VracaNaNovcanikSaPorukomServisa()
        {
            using var baza = NapraviBazu();
            var tutor = NapraviTutora(300);
            var userManager = UserManagerSaKorisnikom(tutor);
            var servis = new Mock<IIsplataServis>();
            servis.Setup(s => s.ProvjeriIsplatu(It.IsAny<double>(), It.IsAny<double>(),
                                                It.IsAny<DateTime?>(), It.IsAny<DateTime>()))
                .Returns(RezultatIsplate.Odbijeno("Poruka iz servisa"));
            var controller = NapraviController(userManager, baza, servis.Object);

            var result = await controller.ZatraziIsplatu(50) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Novcanik", result.ActionName);
            Assert.AreEqual("Poruka iz servisa", controller.TempData["Greska"]);
            Assert.AreEqual(300.0, tutor.StanjeRacuna);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task ZatraziIsplatu_IznosIspodMinimuma_StanjeSeNeMijenja()
        {
            using var baza = NapraviBazu();
            var tutor = NapraviTutora(100);
            var controller = NapraviController(UserManagerSaKorisnikom(tutor), baza, new IsplataServis());

            await controller.ZatraziIsplatu(15);

            Assert.AreEqual(100.0, tutor.StanjeRacuna);
            Assert.AreEqual(IsplataServis.PorukaMinimum, controller.TempData["Greska"]);
            Assert.AreEqual(0, baza.Transakcija.Count());
        }

        [TestMethod]
        public async Task ZatraziIsplatu_TutorBezRanijihIsplata_ServisDobijaStanjeIIznos()
        {
            using var baza = NapraviBazu();
            var tutor = NapraviTutora(300);
            var servis = new Mock<IIsplataServis>();
            servis.Setup(s => s.ProvjeriIsplatu(It.IsAny<double>(), It.IsAny<double>(),
                                                It.IsAny<DateTime?>(), It.IsAny<DateTime>()))
                .Returns(RezultatIsplate.Odbijeno("x"));
            var controller = NapraviController(UserManagerSaKorisnikom(tutor), baza, servis.Object);

            await controller.ZatraziIsplatu(50);

            servis.Verify(s => s.ProvjeriIsplatu(300, 50, null, It.IsAny<DateTime>()), Times.Once);
        }

        // ---------- Uspješna isplata ----------

        [TestMethod]
        public async Task ZatraziIsplatu_DozvoljenaIsplata_SkidaIznosSaStanja()
        {
            using var baza = NapraviBazu();
            var tutor = NapraviTutora(300);
            var controller = NapraviController(UserManagerSaKorisnikom(tutor), baza, new IsplataServis());

            await controller.ZatraziIsplatu(200);

            Assert.AreEqual(100.0, tutor.StanjeRacuna, 0.001);
        }

        [TestMethod]
        public async Task ZatraziIsplatu_DozvoljenaIsplata_AzuriraTutoraTacnoJednom()
        {
            using var baza = NapraviBazu();
            var tutor = NapraviTutora(300);
            var userManager = UserManagerSaKorisnikom(tutor);
            var controller = NapraviController(userManager, baza, new IsplataServis());

            await controller.ZatraziIsplatu(200);

            userManager.Verify(m => m.UpdateAsync(tutor), Times.Once);
        }

        [TestMethod]
        public async Task ZatraziIsplatu_DozvoljenaIsplata_SnimaTransakcijuSaStatusomIsplaceno()
        {
            using var baza = NapraviBazu();
            var tutor = NapraviTutora(300);
            var controller = NapraviController(UserManagerSaKorisnikom(tutor), baza, new IsplataServis());

            await controller.ZatraziIsplatu(200);

            var transakcija = baza.Transakcija.Single();
            Assert.AreEqual("tutor1", transakcija.idTutora);
            Assert.AreEqual(200.0, transakcija.iznos, 0.001);
            Assert.AreEqual(StatusPlacanja.Isplaceno, transakcija.statusPlacanja);
        }

        [TestMethod]
        public async Task ZatraziIsplatu_DozvoljenaIsplata_SaljeObavjestenjeTutoru()
        {
            using var baza = NapraviBazu();
            var tutor = NapraviTutora(300);
            var controller = NapraviController(UserManagerSaKorisnikom(tutor), baza, new IsplataServis());

            await controller.ZatraziIsplatu(200);

            var obavjestenje = baza.Obavjestenje.Single();
            Assert.AreEqual("tutor1", obavjestenje.idKorisnika);
            Assert.AreEqual("Isplata poslana", obavjestenje.naslov);
            Assert.IsFalse(obavjestenje.procitano);
        }

        [TestMethod]
        public async Task ZatraziIsplatu_DozvoljenaIsplata_VracaNaNovcanikSaPorukomUspjeha()
        {
            using var baza = NapraviBazu();
            var tutor = NapraviTutora(300);
            var controller = NapraviController(UserManagerSaKorisnikom(tutor), baza, new IsplataServis());

            var result = await controller.ZatraziIsplatu(200) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Novcanik", result.ActionName);
            Assert.IsNotNull(controller.TempData["Uspjeh"]);
            Assert.IsNull(controller.TempData["Greska"]);
        }

        // ---------- Jedna isplata dnevno ----------

        private static Transakcija NapraviTransakciju(string idTutora, StatusPlacanja status, DateTime datum)
        {
            return new Transakcija
            {
                idStudenta = string.Empty,
                idTutora = idTutora,
                iznos = 50,
                datumUplate = datum,
                statusPlacanja = status
            };
        }

        [TestMethod]
        public async Task ZatraziIsplatu_VecIsplacenoDanas_OdbijaDruguIsplatu()
        {
            using var baza = NapraviBazu();
            baza.Transakcija.Add(NapraviTransakciju("tutor1", StatusPlacanja.Isplaceno, DateTime.Now));
            baza.SaveChanges();
            var tutor = NapraviTutora(300);
            var controller = NapraviController(UserManagerSaKorisnikom(tutor), baza, new IsplataServis());

            await controller.ZatraziIsplatu(100);

            Assert.AreEqual(IsplataServis.PorukaJednomDnevno, controller.TempData["Greska"]);
            Assert.AreEqual(300.0, tutor.StanjeRacuna);
            Assert.AreEqual(1, baza.Transakcija.Count());
        }

        [TestMethod]
        public async Task ZatraziIsplatu_IsplataBilaJuce_DozvoljavaNovuIsplatu()
        {
            using var baza = NapraviBazu();
            baza.Transakcija.Add(NapraviTransakciju("tutor1", StatusPlacanja.Isplaceno, DateTime.Now.AddDays(-1)));
            baza.SaveChanges();
            var tutor = NapraviTutora(300);
            var controller = NapraviController(UserManagerSaKorisnikom(tutor), baza, new IsplataServis());

            await controller.ZatraziIsplatu(100);

            Assert.AreEqual(200.0, tutor.StanjeRacuna, 0.001);
            Assert.IsNull(controller.TempData["Greska"]);
        }

        [TestMethod]
        public async Task ZatraziIsplatu_DanasPostojiPovratNovca_NeBlokiraIsplatu()
        {
            using var baza = NapraviBazu();
            baza.Transakcija.Add(NapraviTransakciju("tutor1", StatusPlacanja.Vraceno, DateTime.Now));
            baza.SaveChanges();
            var tutor = NapraviTutora(300);
            var controller = NapraviController(UserManagerSaKorisnikom(tutor), baza, new IsplataServis());

            await controller.ZatraziIsplatu(100);

            Assert.AreEqual(200.0, tutor.StanjeRacuna, 0.001);
        }

        [TestMethod]
        public async Task ZatraziIsplatu_DrugiTutorIsplatioDanas_NeBlokiraIsplatu()
        {
            using var baza = NapraviBazu();
            baza.Transakcija.Add(NapraviTransakciju("tutor2", StatusPlacanja.Isplaceno, DateTime.Now));
            baza.SaveChanges();
            var tutor = NapraviTutora(300);
            var controller = NapraviController(UserManagerSaKorisnikom(tutor), baza, new IsplataServis());

            await controller.ZatraziIsplatu(100);

            Assert.AreEqual(200.0, tutor.StanjeRacuna, 0.001);
        }

        [TestMethod]
        public async Task ZatraziIsplatu_ViseRanijihIsplata_ServisDobijaDatumNajnovije()
        {
            using var baza = NapraviBazu();
            var starija = new DateTime(2026, 10, 1, 10, 0, 0);
            var novija = new DateTime(2026, 11, 2, 10, 0, 0);
            baza.Transakcija.Add(NapraviTransakciju("tutor1", StatusPlacanja.Isplaceno, starija));
            baza.Transakcija.Add(NapraviTransakciju("tutor1", StatusPlacanja.Isplaceno, novija));
            baza.SaveChanges();
            var servis = new Mock<IIsplataServis>();
            servis.Setup(s => s.ProvjeriIsplatu(It.IsAny<double>(), It.IsAny<double>(),
                                                It.IsAny<DateTime?>(), It.IsAny<DateTime>()))
                .Returns(RezultatIsplate.Odbijeno("x"));
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora(300)), baza, servis.Object);

            await controller.ZatraziIsplatu(50);

            servis.Verify(s => s.ProvjeriIsplatu(300, 50, novija, It.IsAny<DateTime>()), Times.Once);
        }
    }
}
