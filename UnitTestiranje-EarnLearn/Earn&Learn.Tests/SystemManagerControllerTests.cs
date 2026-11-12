using Earn_Learn.Controllers;
using Earn_Learn.Data;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using System.Text.Json;

namespace Earn_Learn.Tests
{
    [TestClass]
    public class SystemManagerControllerTests
    {
        // ---------- Pomoćne metode ----------

        private static Mock<UserManager<Korisnik>> NapraviUserManagerMock()
        {
            var store = new Mock<IUserStore<Korisnik>>();
            var userManager = new Mock<UserManager<Korisnik>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
            userManager.Setup(m => m.UpdateAsync(It.IsAny<Korisnik>()))
                       .ReturnsAsync(IdentityResult.Success);
            userManager.Setup(m => m.DeleteAsync(It.IsAny<Korisnik>()))
                       .ReturnsAsync(IdentityResult.Success);
            return userManager;
        }

        private static Mock<UserManager<Korisnik>> UserManagerSaKorisnikom(Korisnik? korisnik)
        {
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                       .ReturnsAsync(korisnik);
            return userManager;
        }

        private static Mock<UserManager<Korisnik>> UserManagerKojiNalaziKorisnika(string id, Korisnik? korisnik)
        {
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync(id)).ReturnsAsync(korisnik);
            return userManager;
        }

        private static SystemManagerController NapraviController(
            Mock<UserManager<Korisnik>> userManager, ApplicationDbContext baza)
        {
            var httpContext = new DefaultHttpContext();
            return new SystemManagerController(userManager.Object, baza)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            };
        }

        private static DbContextOptions<ApplicationDbContext> NapraviOpcije()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        private static void Ubaci(DbContextOptions<ApplicationDbContext> opcije, params object[] entiteti)
        {
            using var baza = new ApplicationDbContext(opcije);
            baza.AddRange(entiteti);
            baza.SaveChanges();
        }

        private static Korisnik NapraviKorisnika(string id, Uloga uloga,
            string? ime = null, string? prezime = null, string? email = null)
        {
            return new Korisnik
            {
                Id = id,
                UserName = id,
                Ime = ime ?? "Ime" + id,
                Prezime = prezime ?? "Prezime" + id,
                Email = email ?? id + "@test.ba",
                Uloga = uloga
            };
        }

        private static Korisnik NapraviSystemManagera() => NapraviKorisnika("m1", Uloga.SystemManager);

        // ---------- Dashboard ----------

        [TestMethod]
        public async Task Dashboard_KorisnikNijePrijavljen_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.Dashboard() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task Dashboard_KorisnikNijeSystemManager_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.Dashboard() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task Dashboard_PraznaBaza_VracaModelSaNulama()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviSystemManagera()), baza);

            var result = await controller.Dashboard() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as AdminDashboardViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(0, model.UkupnoKorisnika);
            Assert.AreEqual(0, model.UkupnoTutora);
            Assert.AreEqual(0, model.AktivnihSesija);
            Assert.AreEqual(0.0, model.UkupanPromet);
            Assert.AreEqual(0.0, model.MjesecniPromet);
            Assert.AreEqual(0, model.ZahtjevaNCekanju);
        }

        [TestMethod]
        public async Task Dashboard_BazaSaPodacima_VracaTacneStatistike()
        {
            var opcije = NapraviOpcije();
            var verifikovan = NapraviKorisnika("t1", Uloga.Tutor);
            verifikovan.VerifikovanTutor = true;
            var odbijen = NapraviKorisnika("t2", Uloga.Tutor);
            odbijen.VerifikovanTutor = false;
            var neodlucen = NapraviKorisnika("t3", Uloga.Tutor);
            neodlucen.VerifikovanTutor = null;
            Ubaci(opcije,
                NapraviSystemManagera(), verifikovan, odbijen, neodlucen,
                NapraviKorisnika("s1", Uloga.Student),
                new Prijava { Naslov = "Otvorena", Opis = "o", DatumPrijave = DateTime.UtcNow, Status = StatusPrijave.Prijavljeno },
                new Prijava { Naslov = "Zatvorena", Opis = "o", DatumPrijave = DateTime.UtcNow, Status = StatusPrijave.Rijeseno },
                new Termin { id = 1, idTutora = "t1", datumIVrijeme = DateTime.Now.AddDays(1) },
                new Termin { id = 2, idTutora = "t1", datumIVrijeme = DateTime.Now.AddDays(-1) },
                new Transakcija { idTutora = "m1", iznos = 10, statusPlacanja = StatusPlacanja.Uspjesno, datumUplate = DateTime.Now },
                new Transakcija { idTutora = "m1", iznos = 5, statusPlacanja = StatusPlacanja.Uspjesno, datumUplate = DateTime.Now.AddYears(-1) },
                new Transakcija { idTutora = "m1", iznos = 99, statusPlacanja = StatusPlacanja.Vraceno, datumUplate = DateTime.Now },
                new Transakcija { idTutora = "t1", iznos = 50, statusPlacanja = StatusPlacanja.Uspjesno, datumUplate = DateTime.Now });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviSystemManagera()), baza);

            var result = await controller.Dashboard() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as AdminDashboardViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(5, model.UkupnoKorisnika);
            Assert.AreEqual(1, model.UkupnoTutora);
            Assert.AreEqual(1, model.AktivnihSesija);
            Assert.AreEqual(15.0, model.UkupanPromet);
            Assert.AreEqual(10.0, model.MjesecniPromet);
            Assert.AreEqual(3, model.ZahtjevaNCekanju);
        }

        // ---------- Zahtjevi ----------

        [TestMethod]
        public async Task Zahtjevi_KorisnikNijePrijavljen_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.Zahtjevi() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task Zahtjevi_KorisnikNijeSystemManager_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.Zahtjevi() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task Zahtjevi_SystemManager_VracaView()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviSystemManagera()), baza);

            var result = await controller.Zahtjevi();

            Assert.IsInstanceOfType<ViewResult>(result);
        }

        // ---------- Prijave ----------

        [TestMethod]
        public async Task Prijave_KorisnikNijePrijavljen_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.Prijave() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task Prijave_KorisnikNijeSystemManager_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("t1", Uloga.Tutor)), baza);

            var result = await controller.Prijave() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task Prijave_SystemManager_VracaPrijaveOdNajnovije()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Prijava { Naslov = "Starija", Opis = "o", DatumPrijave = new DateTime(2026, 11, 1), Status = StatusPrijave.Prijavljeno },
                new Prijava { Naslov = "Novija", Opis = "o", DatumPrijave = new DateTime(2026, 11, 5), Status = StatusPrijave.Prijavljeno });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviSystemManagera()), baza);

            var result = await controller.Prijave() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Prijava>;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.Count);
            Assert.AreEqual("Novija", model[0].Naslov);
        }

        // ---------- ObrisiPrijavu ----------

        [TestMethod]
        public async Task ObrisiPrijavu_PostojecaPrijava_BriseJuIPreusmjerava()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Prijava { Naslov = "n", Opis = "o", DatumPrijave = DateTime.UtcNow, Status = StatusPrijave.Prijavljeno });
            int id;
            using (var priprema = new ApplicationDbContext(opcije))
            {
                id = priprema.Prijava.Single().Id;
            }
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.ObrisiPrijavu(id) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Prijave", result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(0, provjera.Prijava.Count());
        }

        [TestMethod]
        public async Task ObrisiPrijavu_NepostojecaPrijava_PreusmjeravaBezBrisanja()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Prijava { Naslov = "n", Opis = "o", DatumPrijave = DateTime.UtcNow, Status = StatusPrijave.Prijavljeno });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.ObrisiPrijavu(9999) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Prijave", result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(1, provjera.Prijava.Count());
        }

        // ---------- DodajPrijavu ----------

        [TestMethod]
        public async Task DodajPrijavu_ValidanNaslov_SpremaPrijavuSaStatusomPrijavljeno()
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.DodajPrijavu("Naslov", "Opis") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Prijave", result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            var prijava = provjera.Prijava.Single();
            Assert.AreEqual("Naslov", prijava.Naslov);
            Assert.AreEqual("Opis", prijava.Opis);
            Assert.AreEqual(StatusPrijave.Prijavljeno, prijava.Status);
        }

        [TestMethod]
        public async Task DodajPrijavu_OpisJeNull_SpremaPrazanOpis()
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.DodajPrijavu("Naslov", null!) as RedirectToActionResult;

            Assert.IsNotNull(result);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual("", provjera.Prijava.Single().Opis);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        public async Task DodajPrijavu_PraznNaslov_NeSpremaPrijavu(string naslov)
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.DodajPrijavu(naslov, "Opis") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Prijave", result.ActionName);
            Assert.AreEqual(0, baza.Prijava.Count());
        }

        // ---------- ZahtjeviZaVerifikaciju ----------

        [TestMethod]
        public async Task ZahtjeviZaVerifikaciju_KorisnikNijePrijavljen_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.ZahtjeviZaVerifikaciju() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task ZahtjeviZaVerifikaciju_KorisnikNijeSystemManager_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("t1", Uloga.Tutor)), baza);

            var result = await controller.ZahtjeviZaVerifikaciju() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task ZahtjeviZaVerifikaciju_TutoriNaCekanju_VracaModelSaPredmetima()
        {
            var opcije = NapraviOpcije();
            var sPredmetima = NapraviKorisnika("t1", Uloga.Tutor);
            sPredmetima.VerifikovanTutor = false;
            sPredmetima.PredmetiNaCekanjuJson = "[1]";
            sPredmetima.PredmetNaCekanjaId = 2;
            var sNullJsonom = NapraviKorisnika("t2", Uloga.Tutor);
            sNullJsonom.VerifikovanTutor = null;
            sNullJsonom.PredmetiNaCekanjuJson = "null";
            var bezPredmeta = NapraviKorisnika("t3", Uloga.Tutor);
            bezPredmeta.VerifikovanTutor = false;
            var verifikovan = NapraviKorisnika("t4", Uloga.Tutor);
            verifikovan.VerifikovanTutor = true;
            Ubaci(opcije,
                sPredmetima, sNullJsonom, bezPredmeta, verifikovan,
                new Predmet { id = 1, naziv = "Matematika" },
                new Predmet { id = 2, naziv = "Fizika" },
                new Predmet { id = 3, naziv = "Hemija" },
                new KorisnikPredmet { idKorisnika = "t1", idPredmeta = 3 });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviSystemManagera()), baza);

            var result = await controller.ZahtjeviZaVerifikaciju() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as VerifikacijaViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(1, model.BrojVerifikovanih);
            var tutori = model.TutoriNaCekanju.ToList();
            Assert.AreEqual(3, tutori.Count);
            var prvi = tutori.Single(t => t.Tutor.Id == "t1");
            Assert.AreEqual(1, prvi.Predmeti.Count());
            Assert.AreEqual(1, prvi.PredmetiNaCekanju.Count());
            Assert.AreEqual("Fizika", prvi.PredmetNaCekanju!.naziv);
            var drugi = tutori.Single(t => t.Tutor.Id == "t2");
            Assert.AreEqual(0, drugi.PredmetiNaCekanju.Count());
            Assert.IsNull(drugi.PredmetNaCekanju);
            var treci = tutori.Single(t => t.Tutor.Id == "t3");
            Assert.AreEqual(0, treci.Predmeti.Count());
        }

        [TestMethod]
        public async Task ZahtjeviZaVerifikaciju_NevalidanJsonPredmeta_BacaJsonException()
        {
            var opcije = NapraviOpcije();
            var tutor = NapraviKorisnika("t1", Uloga.Tutor);
            tutor.VerifikovanTutor = false;
            tutor.PredmetiNaCekanjuJson = "nije-json";
            Ubaci(opcije, tutor);
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviSystemManagera()), baza);

            await Assert.ThrowsExactlyAsync<JsonException>(() => controller.ZahtjeviZaVerifikaciju());
        }

        // ---------- VerifikujTutora ----------

        [TestMethod]
        public async Task VerifikujTutora_TutorNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerKojiNalaziKorisnika("x", null), baza);

            var result = await controller.VerifikujTutora("x");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task VerifikujTutora_KorisnikNijeTutor_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerKojiNalaziKorisnika("s1", NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.VerifikujTutora("s1");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task VerifikujTutora_TutorBezPredmetaNaCekanju_VerifikujeGaIPostavljaPrazanJson()
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var tutor = NapraviKorisnika("t1", Uloga.Tutor);
            tutor.PredmetiNaCekanjuJson = null!;
            var userManager = UserManagerKojiNalaziKorisnika("t1", tutor);
            var controller = NapraviController(userManager, baza);

            var result = await controller.VerifikujTutora("t1") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("ZahtjeviZaVerifikaciju", result.ActionName);
            Assert.AreEqual(true, tutor.VerifikovanTutor);
            Assert.AreEqual("[]", tutor.PredmetiNaCekanjuJson);
            Assert.IsNotNull(controller.TempData["Uspjeh"]);
            userManager.Verify(m => m.UpdateAsync(tutor), Times.Once);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(0, provjera.KorisnikPredmet.Count());
        }

        [TestMethod]
        public async Task VerifikujTutora_PredmetiNaCekanju_DodajeSamoOneKojiVecNePostoje()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new KorisnikPredmet { idKorisnika = "t1", idPredmeta = 1 });
            using var baza = new ApplicationDbContext(opcije);
            var tutor = NapraviKorisnika("t1", Uloga.Tutor);
            tutor.PredmetiNaCekanjuJson = "[1,2]";
            var controller = NapraviController(UserManagerKojiNalaziKorisnika("t1", tutor), baza);

            var result = await controller.VerifikujTutora("t1") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("[]", tutor.PredmetiNaCekanjuJson);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(2, provjera.KorisnikPredmet.Count());
            Assert.AreEqual(1, provjera.KorisnikPredmet.Count(kp => kp.idPredmeta == 2));
        }

        [TestMethod]
        public async Task VerifikujTutora_JsonJeNull_PostavljaPrazanJsonBezDodavanjaPredmeta()
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var tutor = NapraviKorisnika("t1", Uloga.Tutor);
            tutor.PredmetiNaCekanjuJson = "null";
            var controller = NapraviController(UserManagerKojiNalaziKorisnika("t1", tutor), baza);

            var result = await controller.VerifikujTutora("t1") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("[]", tutor.PredmetiNaCekanjuJson);
            Assert.AreEqual(0, baza.KorisnikPredmet.Count());
        }

        [TestMethod]
        public async Task VerifikujTutora_PredmetNaCekanjaNePostoji_DodajeGaIBriseOznaku()
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var tutor = NapraviKorisnika("t1", Uloga.Tutor);
            tutor.PredmetNaCekanjaId = 5;
            var controller = NapraviController(UserManagerKojiNalaziKorisnika("t1", tutor), baza);

            var result = await controller.VerifikujTutora("t1") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.IsNull(tutor.PredmetNaCekanjaId);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(5, provjera.KorisnikPredmet.Single().idPredmeta);
        }

        [TestMethod]
        public async Task VerifikujTutora_PredmetNaCekanjaVecPostoji_NeDuplira()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new KorisnikPredmet { idKorisnika = "t1", idPredmeta = 5 });
            using var baza = new ApplicationDbContext(opcije);
            var tutor = NapraviKorisnika("t1", Uloga.Tutor);
            tutor.PredmetNaCekanjaId = 5;
            var controller = NapraviController(UserManagerKojiNalaziKorisnika("t1", tutor), baza);

            var result = await controller.VerifikujTutora("t1") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.IsNull(tutor.PredmetNaCekanjaId);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(1, provjera.KorisnikPredmet.Count());
        }

        [TestMethod]
        public async Task VerifikujTutora_NevalidanJsonPredmeta_BacaJsonException()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var tutor = NapraviKorisnika("t1", Uloga.Tutor);
            tutor.PredmetiNaCekanjuJson = "nije-json";
            var controller = NapraviController(UserManagerKojiNalaziKorisnika("t1", tutor), baza);

            await Assert.ThrowsExactlyAsync<JsonException>(() => controller.VerifikujTutora("t1"));
        }

        // ---------- OdbijTutora ----------

        [TestMethod]
        public async Task OdbijTutora_TutorNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerKojiNalaziKorisnika("x", null), baza);

            var result = await controller.OdbijTutora("x");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task OdbijTutora_KorisnikNijeTutor_VracaNotFoundBezBrisanja()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = UserManagerKojiNalaziKorisnika("s1", NapraviKorisnika("s1", Uloga.Student));
            var controller = NapraviController(userManager, baza);

            var result = await controller.OdbijTutora("s1");

            Assert.IsInstanceOfType<NotFoundResult>(result);
            userManager.Verify(m => m.DeleteAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task OdbijTutora_PostojeciTutor_BriseGaIPreusmjerava()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var tutor = NapraviKorisnika("t1", Uloga.Tutor);
            var userManager = UserManagerKojiNalaziKorisnika("t1", tutor);
            var controller = NapraviController(userManager, baza);

            var result = await controller.OdbijTutora("t1") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("ZahtjeviZaVerifikaciju", result.ActionName);
            Assert.IsNotNull(controller.TempData["Uspjeh"]);
            userManager.Verify(m => m.DeleteAsync(tutor), Times.Once);
        }

        // ---------- PrilogTutora ----------

        [TestMethod]
        public async Task PrilogTutora_KorisnikNijePrijavljen_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.PrilogTutora("t1") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task PrilogTutora_KorisnikNijeSystemManager_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.PrilogTutora("t1") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task PrilogTutora_TutorNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = UserManagerSaKorisnikom(NapraviSystemManagera());
            userManager.Setup(m => m.FindByIdAsync("x")).ReturnsAsync((Korisnik?)null);
            var controller = NapraviController(userManager, baza);

            var result = await controller.PrilogTutora("x");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task PrilogTutora_KorisnikNijeTutor_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = UserManagerSaKorisnikom(NapraviSystemManagera());
            userManager.Setup(m => m.FindByIdAsync("s1")).ReturnsAsync(NapraviKorisnika("s1", Uloga.Student));
            var controller = NapraviController(userManager, baza);

            var result = await controller.PrilogTutora("s1");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task PrilogTutora_PostojeciTutor_VracaViewSaTutorom()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var tutor = NapraviKorisnika("t1", Uloga.Tutor);
            var userManager = UserManagerSaKorisnikom(NapraviSystemManagera());
            userManager.Setup(m => m.FindByIdAsync("t1")).ReturnsAsync(tutor);
            var controller = NapraviController(userManager, baza);

            var result = await controller.PrilogTutora("t1") as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(tutor, result.Model);
        }

        // ---------- Index ----------

        [TestMethod]
        public async Task Index_ImaViseKorisnika_VracaSamoSystemManagere()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("m1", Uloga.SystemManager),
                NapraviKorisnika("m2", Uloga.SystemManager),
                NapraviKorisnika("s1", Uloga.Student),
                NapraviKorisnika("t1", Uloga.Tutor));
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Korisnik>;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.Count);
        }

        // ---------- Detalji ----------

        [TestMethod]
        public async Task Detalji_ManagerNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerKojiNalaziKorisnika("x", null), baza);

            var result = await controller.Detalji("x");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Detalji_KorisnikNijeSystemManager_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerKojiNalaziKorisnika("s1", NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.Detalji("s1");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Detalji_PostojeciManager_VracaViewSaManagerom()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var manager = NapraviSystemManagera();
            var controller = NapraviController(UserManagerKojiNalaziKorisnika("m1", manager), baza);

            var result = await controller.Detalji("m1") as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(manager, result.Model);
        }

        // ---------- Kreiraj ----------

        [TestMethod]
        public void Kreiraj_GetPoziv_VracaView()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = controller.Kreiraj();

            Assert.IsInstanceOfType<ViewResult>(result);
        }

        [TestMethod]
        public async Task Kreiraj_UspjesnoKreiranje_PreusmjeravaNaIndexIPostavljaUlogu()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()))
                       .ReturnsAsync(IdentityResult.Success);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Kreiraj("Amar", "Hadzic", "amar@test.ba", "Lozinka1!") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            userManager.Verify(m => m.CreateAsync(
                It.Is<Korisnik>(k => k.Uloga == Uloga.SystemManager
                                  && k.Email == "amar@test.ba"
                                  && k.UserName == "amar@test.ba"
                                  && k.PredmetiNaCekanjuJson == "[]"),
                "Lozinka1!"), Times.Once);
        }

        [TestMethod]
        public async Task Kreiraj_NeuspjesnoKreiranje_VracaViewSaGreskomUModelState()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()))
                       .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Lozinka je slaba." }));
            var controller = NapraviController(userManager, baza);

            var result = await controller.Kreiraj("Amar", "Hadzic", "amar@test.ba", "1") as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(1, controller.ModelState.ErrorCount);
        }

        [TestMethod]
        public async Task Kreiraj_UserManagerBacaIzuzetak_PropagiraIzuzetak()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()))
                       .ThrowsAsync(new InvalidOperationException("Baza nedostupna"));
            var controller = NapraviController(userManager, baza);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => controller.Kreiraj("Amar", "Hadzic", "amar@test.ba", "Lozinka1!"));
        }

        // ---------- Obrisi ----------

        [TestMethod]
        public async Task Obrisi_ManagerNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerKojiNalaziKorisnika("x", null), baza);

            var result = await controller.Obrisi("x");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Obrisi_KorisnikNijeSystemManager_VracaNotFoundBezBrisanja()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = UserManagerKojiNalaziKorisnika("s1", NapraviKorisnika("s1", Uloga.Student));
            var controller = NapraviController(userManager, baza);

            var result = await controller.Obrisi("s1");

            Assert.IsInstanceOfType<NotFoundResult>(result);
            userManager.Verify(m => m.DeleteAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task Obrisi_PostojeciManager_BriseGaIPreusmjerava()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var manager = NapraviSystemManagera();
            var userManager = UserManagerKojiNalaziKorisnika("m1", manager);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Obrisi("m1") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            userManager.Verify(m => m.DeleteAsync(manager), Times.Once);
        }

        // ---------- UpravljajKorisnicima ----------

        private static void UbaciKorisnikeZaPretragu(DbContextOptions<ApplicationDbContext> opcije)
        {
            Ubaci(opcije,
                NapraviKorisnika("u1", Uloga.Student, "Amar", "Hadzic", "amar@test.ba"),
                NapraviKorisnika("u2", Uloga.Tutor, "Lejla", "Kovac", "lejla@mail.ba"),
                NapraviKorisnika("u3", Uloga.Student, "Emir", "Hadzic", "emir@test.ba"),
                NapraviKorisnika("m1", Uloga.SystemManager, "Amar", "Manager", "manager@test.ba"));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("   ")]
        public async Task UpravljajKorisnicima_BezPretrage_VracaSveKorisnikeOsimManagera(string? pretraga)
        {
            var opcije = NapraviOpcije();
            UbaciKorisnikeZaPretragu(opcije);
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.UpravljajKorisnicima(pretraga) as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Korisnik>;
            Assert.IsNotNull(model);
            Assert.AreEqual(3, model.Count);
        }

        // ---------- Data driven: JSON ----------

        private record PretragaPodaci(string Pretraga, int OcekivaniBroj);

        public static IEnumerable<object[]> PretrageIzJsona()
        {
            var putanja = Path.Combine(AppContext.BaseDirectory, "TestData", "systemmanager_pretraga.json");
            var podaci = JsonSerializer.Deserialize<List<PretragaPodaci>>(
                File.ReadAllText(putanja),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            return podaci.Select(p => new object[] { p.Pretraga, p.OcekivaniBroj });
        }

        [TestMethod]
        [DynamicData(nameof(PretrageIzJsona))]
        public async Task UpravljajKorisnicima_SaPretragom_VracaOdgovarajuceKorisnike(string pretraga, int ocekivaniBroj)
        {
            var opcije = NapraviOpcije();
            UbaciKorisnikeZaPretragu(opcije);
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.UpravljajKorisnicima(pretraga) as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Korisnik>;
            Assert.IsNotNull(model);
            Assert.AreEqual(ocekivaniBroj, model.Count);
            Assert.AreEqual(pretraga, (string)controller.ViewBag.Pretraga);
        }

        // ---------- ObrisiKorisnika ----------

        [TestMethod]
        public async Task ObrisiKorisnika_PostojeciKorisnik_BriseGaIPreusmjerava()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var korisnik = NapraviKorisnika("s1", Uloga.Student);
            var userManager = UserManagerKojiNalaziKorisnika("s1", korisnik);
            var controller = NapraviController(userManager, baza);

            var result = await controller.ObrisiKorisnika("s1") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("UpravljajKorisnicima", result.ActionName);
            userManager.Verify(m => m.DeleteAsync(korisnik), Times.Once);
        }

        [TestMethod]
        public async Task ObrisiKorisnika_KorisnikNePostoji_PreusmjeravaBezBrisanja()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = UserManagerKojiNalaziKorisnika("x", null);
            var controller = NapraviController(userManager, baza);

            var result = await controller.ObrisiKorisnika("x") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("UpravljajKorisnicima", result.ActionName);
            userManager.Verify(m => m.DeleteAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        // ---------- UrediKorisnika ----------

        [TestMethod]
        public async Task UrediKorisnika_KorisnikNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerKojiNalaziKorisnika("x", null), baza);

            var result = await controller.UrediKorisnika("x", "Ime", "Prezime", "e@test.ba");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        // ---------- Data driven: CSV ----------

        public static IEnumerable<object[]> PodaciZaUredivanjeIzCsv()
        {
            var putanja = Path.Combine(AppContext.BaseDirectory, "TestData", "systemmanager_uredi.csv");
            return File.ReadAllLines(putanja)
                .Skip(1)
                .Select(red => red.Split(','))
                .Select(k => new object[] { k[0], k[1], k[2] });
        }

        [TestMethod]
        [DynamicData(nameof(PodaciZaUredivanjeIzCsv))]
        public async Task UrediKorisnika_PostojeciKorisnik_AzuriraPodatkeIPreusmjerava(
            string ime, string prezime, string email)
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var korisnik = NapraviKorisnika("s1", Uloga.Student);
            korisnik.PredmetiNaCekanjuJson = null!;
            var userManager = UserManagerKojiNalaziKorisnika("s1", korisnik);
            var controller = NapraviController(userManager, baza);

            var result = await controller.UrediKorisnika("s1", ime, prezime, email) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("UpravljajKorisnicima", result.ActionName);
            Assert.AreEqual(ime, korisnik.Ime);
            Assert.AreEqual(prezime, korisnik.Prezime);
            Assert.AreEqual(email, korisnik.Email);
            Assert.AreEqual(email, korisnik.UserName);
            Assert.AreEqual("[]", korisnik.PredmetiNaCekanjuJson);
            userManager.Verify(m => m.UpdateAsync(korisnik), Times.Once);
        }

        // ---------- UpravljajPredmetima ----------

        private static void UbaciPredmeteITutore(DbContextOptions<ApplicationDbContext> opcije)
        {
            Ubaci(opcije,
                new Predmet { id = 1, naziv = "Matematika" },
                new Predmet { id = 2, naziv = "Fizika" },
                new Predmet { id = 3, naziv = "Hemija" },
                new Predmet { id = 4, naziv = "Biologija" },
                new Predmet { id = 5, naziv = "Geografija" },
                NapraviKorisnika("t1", Uloga.Tutor),
                NapraviKorisnika("t2", Uloga.Tutor),
                NapraviKorisnika("s1", Uloga.Student),
                new KorisnikPredmet { idKorisnika = "t1", idPredmeta = 1 },
                new KorisnikPredmet { idKorisnika = "t2", idPredmeta = 1 },
                new KorisnikPredmet { idKorisnika = "s1", idPredmeta = 1 },
                new KorisnikPredmet { idKorisnika = "t1", idPredmeta = 2 });
        }

        [TestMethod]
        public async Task UpravljajPredmetima_BezPretrage_VracaSvePredmeteISaStatistikama()
        {
            var opcije = NapraviOpcije();
            UbaciPredmeteITutore(opcije);
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.UpravljajPredmetima(null) as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Predmet>;
            Assert.IsNotNull(model);
            Assert.AreEqual(5, model.Count);
            Assert.AreEqual(5, (int)controller.ViewBag.UkupnoPredmeta);
            Assert.AreEqual(3, (int)controller.ViewBag.NovihPredmeta);
            var brojTutora = (Dictionary<int, int>)controller.ViewBag.BrojTutoraPoP;
            Assert.AreEqual(2, brojTutora[1]);
            Assert.AreEqual(1, brojTutora[2]);
            Assert.IsFalse(brojTutora.ContainsKey(3));
        }

        [TestMethod]
        public async Task UpravljajPredmetima_SaPretragom_VracaSamoPredmeteSaTimNazivom()
        {
            var opcije = NapraviOpcije();
            UbaciPredmeteITutore(opcije);
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.UpravljajPredmetima("Fiz") as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Predmet>;
            Assert.IsNotNull(model);
            Assert.AreEqual(1, model.Count);
            Assert.AreEqual("Fizika", model[0].naziv);
            Assert.AreEqual("Fiz", (string)controller.ViewBag.Pretraga);
            Assert.AreEqual(5, (int)controller.ViewBag.UkupnoPredmeta);
        }

        [TestMethod]
        public async Task UpravljajPredmetima_NemaPredmeta_BacaInvalidOperationException()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => controller.UpravljajPredmetima(null));
        }

        // ---------- DodajPredmet ----------

        [TestMethod]
        public async Task DodajPredmet_ValidanNaziv_SpremaPredmetIPreusmjerava()
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.DodajPredmet("Informatika") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("UpravljajPredmetima", result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual("Informatika", provjera.Predmet.Single().naziv);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        public async Task DodajPredmet_PraznNaziv_NeSpremaPredmet(string naziv)
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.DodajPredmet(naziv) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("UpravljajPredmetima", result.ActionName);
            Assert.AreEqual(0, baza.Predmet.Count());
        }

        // ---------- UrediPredmet ----------

        [TestMethod]
        public async Task UrediPredmet_PredmetNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.UrediPredmet(99, "Novi naziv");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task UrediPredmet_PostojeciPredmet_MijenjaNazivIPreusmjerava()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Predmet { id = 1, naziv = "Stari naziv" });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.UrediPredmet(1, "Novi naziv") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("UpravljajPredmetima", result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual("Novi naziv", provjera.Predmet.Single().naziv);
        }

        // ---------- ObrisiPredmet ----------

        [TestMethod]
        public async Task ObrisiPredmet_PostojeciPredmet_BriseGaIPreusmjerava()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Predmet { id = 1, naziv = "Matematika" });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.ObrisiPredmet(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("UpravljajPredmetima", result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(0, provjera.Predmet.Count());
        }

        [TestMethod]
        public async Task ObrisiPredmet_PredmetNePostoji_PreusmjeravaBezBrisanja()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Predmet { id = 1, naziv = "Matematika" });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.ObrisiPredmet(99) as RedirectToActionResult;

            Assert.IsNotNull(result);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(1, provjera.Predmet.Count());
        }
    }
}
