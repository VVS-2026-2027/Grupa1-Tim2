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
    public class TutorControllerTests
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

        private static TutorController NapraviController(
            Mock<UserManager<Korisnik>> userManager, ApplicationDbContext baza)
        {
            var httpContext = new DefaultHttpContext();
            return new TutorController(userManager.Object, baza)
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

        private static Korisnik NapraviKorisnika(string id, Uloga uloga, double stanjeRacuna = 0)
        {
            return new Korisnik
            {
                Id = id,
                UserName = id,
                Ime = "Ime" + id,
                Prezime = "Prezime" + id,
                Uloga = uloga,
                StanjeRacuna = stanjeRacuna
            };
        }

        private static Korisnik NapraviTutora(string id, bool? verifikovan = true, double? cijena = null,
            double? ocjena = null, int? casova = null, double stanje = 0)
        {
            var tutor = NapraviKorisnika(id, Uloga.Tutor, stanje);
            tutor.VerifikovanTutor = verifikovan;
            tutor.CijenaPoSatu = cijena;
            tutor.ProsjecnaOcjena = ocjena;
            tutor.BrojOdrzanihCasova = casova;
            return tutor;
        }

        private static JsonElement UJson(object? vrijednost)
        {
            return JsonSerializer.SerializeToElement(vrijednost);
        }

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
        public async Task Dashboard_KorisnikNijeTutor_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.Dashboard() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task Dashboard_NeverifikovanTutor_VracaViewVerifikacijaNaCekanju()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviTutora("t1", false)), baza);

            var result = await controller.Dashboard() as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("VerifikacijaNaCekanju", result.ViewName);
        }

        [TestMethod]
        public async Task Dashboard_VerifikovanTutor_VracaModelSaTerminimaIZaradom()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("s1", Uloga.Student),
                new Predmet { id = 1, naziv = "Matematika" },
                new Termin { id = 1, idTutora = "t1", idStudenta = "s1", idPredmeta = 1, datumIVrijeme = DateTime.Now.AddDays(1) },
                new Termin { id = 2, idTutora = "t1", idStudenta = "", idPredmeta = null, datumIVrijeme = DateTime.Now.AddDays(2) },
                new Termin { id = 3, idTutora = "t1", idStudenta = "s1", datumIVrijeme = DateTime.Now.AddDays(-1) },
                new Termin { id = 4, idTutora = "drugi", idStudenta = "s1", datumIVrijeme = DateTime.Now.AddDays(1) },
                new Transakcija { idTutora = "t1", iznos = 50, statusPlacanja = StatusPlacanja.Uspjesno, datumUplate = DateTime.Now },
                new Transakcija { idTutora = "t1", iznos = 20, statusPlacanja = StatusPlacanja.Uspjesno, datumUplate = DateTime.Now.AddYears(-1) },
                new Transakcija { idTutora = "t1", iznos = -10, statusPlacanja = StatusPlacanja.Uspjesno, datumUplate = DateTime.Now },
                new Transakcija { idTutora = "t1", iznos = 5, statusPlacanja = StatusPlacanja.Vraceno, datumUplate = DateTime.Now },
                new Transakcija { idTutora = "drugi", iznos = 99, statusPlacanja = StatusPlacanja.Uspjesno, datumUplate = DateTime.Now });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviTutora("t1", true, 25, 4.5, 7, 120)), baza);

            var result = await controller.Dashboard() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as TutorDashboardViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual("Imet1", model.Ime);
            Assert.AreEqual(7, model.BrojCasova);
            Assert.AreEqual(4.5, model.ProsjecnaOcjena);
            Assert.AreEqual(120, model.Balans);
            var termini = model.NadolazaciTermini.ToList();
            Assert.AreEqual(2, termini.Count);
            Assert.AreEqual("Imes1 Prezimes1", termini[0].ImeStudenta);
            Assert.AreEqual("Matematika", termini[0].ImePredmeta);
            Assert.IsNull(termini[1].ImeStudenta);
            Assert.IsNull(termini[1].ImePredmeta);
            Assert.AreEqual(50.0, (double)controller.ViewBag.ZaradaOvajMjesec);
            Assert.AreEqual(50.0, (double)controller.ViewBag.ZaradaOvaGodina);
            Assert.AreEqual(DateTime.Now.Year, (int)controller.ViewBag.Godina);
            Assert.AreEqual(25.0, (double)controller.ViewBag.CijenaPoSatu);
            Assert.AreEqual(4.5, (double)controller.ViewBag.ProsjecnaOcjena);
            var mjeseci = JsonSerializer.Deserialize<List<double>>((string)controller.ViewBag.ZaradaPoMjesecima);
            Assert.IsNotNull(mjeseci);
            Assert.AreEqual(12, mjeseci.Count);
        }

        [TestMethod]
        public async Task Dashboard_TutorBezPodataka_KoristiPodrazumijevaneVrijednosti()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.Dashboard() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as TutorDashboardViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(0, model.BrojCasova);
            Assert.AreEqual(0, model.ProsjecnaOcjena);
            Assert.AreEqual(0, model.NadolazaciTermini.Count());
            Assert.AreEqual(0.0, (double)controller.ViewBag.CijenaPoSatu);
        }

        // ---------- StudentDashboard ----------

        [TestMethod]
        public async Task StudentDashboard_KorisnikNijePrijavljen_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.StudentDashboard() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task StudentDashboard_KorisnikNijeTutor_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.StudentDashboard() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task StudentDashboard_NeverifikovanTutor_VracaViewVerifikacijaNaCekanju()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviTutora("t1", false)), baza);

            var result = await controller.StudentDashboard() as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("VerifikacijaNaCekanju", result.ViewName);
        }

        [TestMethod]
        public async Task StudentDashboard_VerifikovanTutor_VracaStudentskiPrikazSaTerminimaITopTutorima()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviTutora("t1", true, 10, 4),
                NapraviTutora("t2", true, 10, 5),
                NapraviTutora("t3", true, 10, 3),
                NapraviTutora("t4", false, 10, 5),
                new Predmet { id = 1, naziv = "Hemija" },
                new Termin { id = 1, idTutora = "t2", idStudenta = "t1", idPredmeta = 1, datumIVrijeme = DateTime.Now.AddDays(1) },
                new Termin { id = 2, idTutora = "nepoznat", idStudenta = "t1", idPredmeta = null, datumIVrijeme = DateTime.Now.AddDays(2) },
                new Termin { id = 3, idTutora = "t2", idStudenta = "t1", datumIVrijeme = DateTime.Now.AddDays(-1) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.StudentDashboard() as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("~/Views/Student/Dashboard.cshtml", result.ViewName);
            var model = result.Model as StudentDashboardViewModel;
            Assert.IsNotNull(model);
            var termini = model.NadolazaciTermini.ToList();
            Assert.AreEqual(2, termini.Count);
            Assert.AreEqual("Imet2", termini[0].Tutor.Ime);
            Assert.AreEqual("Hemija", termini[0].Predmet!.naziv);
            Assert.AreEqual("Nepoznat", termini[1].Tutor.Ime);
            Assert.IsNull(termini[1].Predmet);
            var topTutori = model.TopTutori.ToList();
            Assert.AreEqual(2, topTutori.Count);
            Assert.AreEqual("t2", topTutori[0].Id);
            Assert.AreEqual("t3", topTutori[1].Id);
        }

        // ---------- OtkaziTermin ----------

        [TestMethod]
        public async Task OtkaziTermin_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.OtkaziTermin(1);

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task OtkaziTermin_KorisnikNijeTutor_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.OtkaziTermin(1);

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task OtkaziTermin_TerminNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.OtkaziTermin(99);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task OtkaziTermin_TerminDrugogTutora_VracaNotFound()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Termin { id = 1, idTutora = "drugi" });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.OtkaziTermin(1);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task OtkaziTermin_RezervisanSaStudentom_VracaNovacObavjestavaIBriseTermin()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("s1", Uloga.Student, 10),
                new Termin { id = 1, idTutora = "t1", idStudenta = "s1", status = StatusTermina.Rezervisan, cijena = 20, datumIVrijeme = new DateTime(2026, 12, 1, 10, 0, 0) });
            using var baza = new ApplicationDbContext(opcije);
            var userManager = UserManagerSaKorisnikom(NapraviTutora("t1"));
            var controller = NapraviController(userManager, baza);

            var result = await controller.OtkaziTermin(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.IsNotNull(controller.TempData["Uspjeh"]);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Once);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(30, provjera.Users.Single().StanjeRacuna);
            Assert.AreEqual(0, provjera.Termin.Count());
            Assert.AreEqual("s1", provjera.Obavjestenje.Single().idKorisnika);
        }

        [TestMethod]
        public async Task OtkaziTermin_RezervisanAliStudentNePostoji_BriseTerminBezObavjestenja()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Termin { id = 1, idTutora = "t1", idStudenta = "nepoznat", status = StatusTermina.Rezervisan, cijena = 20 });
            using var baza = new ApplicationDbContext(opcije);
            var userManager = UserManagerSaKorisnikom(NapraviTutora("t1"));
            var controller = NapraviController(userManager, baza);

            var result = await controller.OtkaziTermin(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(0, provjera.Termin.Count());
            Assert.AreEqual(0, provjera.Obavjestenje.Count());
        }

        [TestMethod]
        [DataRow(StatusTermina.Slobodan, "s1")]
        [DataRow(StatusTermina.Rezervisan, "")]
        public async Task OtkaziTermin_TerminBezPovrataNovca_SamoBriseTermin(StatusTermina status, string idStudenta)
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("s1", Uloga.Student, 10),
                new Termin { id = 1, idTutora = "t1", idStudenta = idStudenta, status = status, cijena = 20 });
            using var baza = new ApplicationDbContext(opcije);
            var userManager = UserManagerSaKorisnikom(NapraviTutora("t1"));
            var controller = NapraviController(userManager, baza);

            var result = await controller.OtkaziTermin(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(0, provjera.Termin.Count());
            Assert.AreEqual(10, provjera.Users.Single().StanjeRacuna);
        }

        // ---------- Profil ----------

        [TestMethod]
        public async Task Profil_TutorNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("x")).ReturnsAsync((Korisnik?)null);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Profil("x");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Profil_KorisnikNijeTutor_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("s1"))
                       .ReturnsAsync(NapraviKorisnika("s1", Uloga.Student));
            var controller = NapraviController(userManager, baza);

            var result = await controller.Profil("s1");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Profil_TutorSaRecenzijamaITerminima_VracaModelISinhronizujeProsjek()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("s1", Uloga.Student),
                new Predmet { id = 1, naziv = "Matematika" },
                new Predmet { id = 2, naziv = "Fizika" },
                new KorisnikPredmet { idKorisnika = "t1", idPredmeta = 1 },
                new Recenzija { idStudenta = "s1", idTutora = "t1", ocjena = 5, komentar = "Super", datumRecenzije = new DateTime(2026, 11, 1) },
                new Recenzija { idStudenta = "nepoznat", idTutora = "t1", ocjena = 3, komentar = "Ok", datumRecenzije = new DateTime(2026, 11, 2) },
                new Termin { id = 1, idTutora = "t1", idPredmeta = 1, status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(1) },
                new Termin { id = 2, idTutora = "t1", idPredmeta = 99, status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(2) },
                new Termin { id = 3, idTutora = "t1", idPredmeta = null, status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(3) },
                new Termin { id = 4, idTutora = "t1", status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(-1) },
                new Termin { id = 5, idTutora = "t1", status = StatusTermina.Rezervisan, datumIVrijeme = DateTime.Now.AddDays(1) },
                new Termin { id = 6, idTutora = "drugi", status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(1) });
            using var baza = new ApplicationDbContext(opcije);
            var tutor = NapraviTutora("t1", true, 25, 0);
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("t1")).ReturnsAsync(tutor);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Profil("t1") as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as TutorProfilViewModel;
            Assert.IsNotNull(model);
            Assert.AreSame(tutor, model.Tutor);
            Assert.AreEqual(1, model.Predmeti.Count());
            Assert.AreEqual(2, model.UkupnoRecenzija);
            Assert.AreEqual(4.0, model.ProsjecnaOcjena);
            Assert.AreEqual(4.0, tutor.ProsjecnaOcjena);
            userManager.Verify(m => m.UpdateAsync(tutor), Times.Once);
            var recenzije = model.Recenzije.ToList();
            Assert.AreEqual("Student", recenzije[0].ImeStudenta);
            Assert.AreEqual("Imes1", recenzije[1].ImeStudenta);
            Assert.AreEqual(3, model.DostupniTermini.Count());
            var predmeti = (Dictionary<int, string>)controller.ViewBag.TerminPredmeti;
            Assert.AreEqual(2, predmeti.Count);
            Assert.AreEqual("Matematika", predmeti[1]);
            Assert.AreEqual("—", predmeti[2]);
        }

        [TestMethod]
        public async Task Profil_TutorBezRecenzijaSaProsjekomNula_NeAzuriraTutora()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("t1")).ReturnsAsync(NapraviTutora("t1", true, 25, 0));
            var controller = NapraviController(userManager, baza);

            var result = await controller.Profil("t1") as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as TutorProfilViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(0, model.UkupnoRecenzija);
            Assert.AreEqual(0, model.ProsjecnaOcjena);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        // ---------- RezervisiTermin ----------

        [TestMethod]
        public async Task RezervisiTermin_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.RezervisiTermin(1, 1, TipInstrukcija.Online);

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task RezervisiTermin_SystemManager_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("m1", Uloga.SystemManager)), baza);

            var result = await controller.RezervisiTermin(1, 1, TipInstrukcija.Online);

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task RezervisiTermin_KorisnikNePostojiUBazi_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.RezervisiTermin(1, 1, TipInstrukcija.Online);

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task RezervisiTermin_TerminNePostoji_PreusmjeravaNaProfilSaGreskom()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, NapraviKorisnika("s1", Uloga.Student, 100));
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student, 100)), baza);

            var result = await controller.RezervisiTermin(99, 1, TipInstrukcija.Online) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Profil", result.ActionName);
            Assert.IsNull(result.RouteValues!["id"]);
            Assert.IsNotNull(controller.TempData["Greska"]);
        }

        [TestMethod]
        [DataRow(StatusTermina.Rezervisan, 1)]
        [DataRow(StatusTermina.Slobodan, -1)]
        public async Task RezervisiTermin_TerminNijeDostupan_PreusmjeravaNaProfilTutoraSaGreskom(
            StatusTermina status, int dana)
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("s1", Uloga.Student, 100),
                new Termin { id = 1, idTutora = "t2", status = status, datumIVrijeme = DateTime.Now.AddDays(dana) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student, 100)), baza);

            var result = await controller.RezervisiTermin(1, 1, TipInstrukcija.Online) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Profil", result.ActionName);
            Assert.AreEqual("t2", result.RouteValues!["id"]);
            Assert.IsNotNull(controller.TempData["Greska"]);
        }

        [TestMethod]
        public async Task RezervisiTermin_VlastitiTermin_PreusmjeravaNaVlastitiProfilSaGreskom()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviTutora("t1", true, 30, null, null, 100),
                new Termin { id = 1, idTutora = "t1", status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(1) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviTutora("t1", true, 30, null, null, 100)), baza);

            var result = await controller.RezervisiTermin(1, 1, TipInstrukcija.Online) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Profil", result.ActionName);
            Assert.AreEqual("t1", result.RouteValues!["id"]);
            Assert.IsNotNull(controller.TempData["Greska"]);
        }

        [TestMethod]
        public async Task RezervisiTermin_TutorTerminaNePostoji_VracaNotFound()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("s1", Uloga.Student, 100),
                new Termin { id = 1, idTutora = "t2", status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(1) });
            using var baza = new ApplicationDbContext(opcije);
            var userManager = UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student, 100));
            userManager.Setup(m => m.FindByIdAsync("t2")).ReturnsAsync((Korisnik?)null);
            var controller = NapraviController(userManager, baza);

            var result = await controller.RezervisiTermin(1, 1, TipInstrukcija.Online);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task RezervisiTermin_NedovoljnoSredstava_PreusmjeravaNaProfilTutoraSaGreskom()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("s1", Uloga.Student, 5),
                new Termin { id = 1, idTutora = "t2", status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(1) });
            using var baza = new ApplicationDbContext(opcije);
            var userManager = UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student, 5));
            userManager.Setup(m => m.FindByIdAsync("t2")).ReturnsAsync(NapraviTutora("t2", true, 30));
            var controller = NapraviController(userManager, baza);

            var result = await controller.RezervisiTermin(1, 1, TipInstrukcija.Online) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Profil", result.ActionName);
            Assert.AreEqual("t2", result.RouteValues!["id"]);
            Assert.IsNotNull(controller.TempData["GreskaNovcenik"]);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(StatusTermina.Slobodan, provjera.Termin.Single().status);
        }

        [TestMethod]
        public async Task RezervisiTermin_StudentSaDovoljnoSredstava_RezervisePoCijeniTutoraIPreusmjeravaNaStudentDashboard()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("s1", Uloga.Student, 100),
                new Termin { id = 1, idTutora = "t2", status = StatusTermina.Slobodan, tipInstrukcija = TipInstrukcija.Hibridno, datumIVrijeme = DateTime.Now.AddDays(1) });
            using var baza = new ApplicationDbContext(opcije);
            var userManager = UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student, 100));
            userManager.Setup(m => m.FindByIdAsync("t2")).ReturnsAsync(NapraviTutora("t2", true, 30));
            var controller = NapraviController(userManager, baza);

            var result = await controller.RezervisiTermin(1, 5, TipInstrukcija.Online) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.AreEqual("Student", result.ControllerName);
            Assert.IsNotNull(controller.TempData["Uspjeh"]);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Once);
            using var provjera = new ApplicationDbContext(opcije);
            var termin = provjera.Termin.Single();
            Assert.AreEqual(StatusTermina.Rezervisan, termin.status);
            Assert.AreEqual("s1", termin.idStudenta);
            Assert.AreEqual(5, termin.idPredmeta);
            Assert.AreEqual(30, termin.cijena);
            Assert.AreEqual(TipInstrukcija.Online, termin.tipInstrukcija);
            var obavjestenje = provjera.Obavjestenje.Single();
            Assert.AreEqual("t2", obavjestenje.idKorisnika);
            Assert.AreEqual(1, obavjestenje.idTermina);
        }

        [TestMethod]
        public async Task RezervisiTermin_TerminSaFiksnimTipom_ZadrzavaTipInstrukcije()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("s1", Uloga.Student, 100),
                new Termin { id = 1, idTutora = "t2", status = StatusTermina.Slobodan, tipInstrukcija = TipInstrukcija.Online, datumIVrijeme = DateTime.Now.AddDays(1) });
            using var baza = new ApplicationDbContext(opcije);
            var userManager = UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student, 100));
            userManager.Setup(m => m.FindByIdAsync("t2")).ReturnsAsync(NapraviTutora("t2", true, 30));
            var controller = NapraviController(userManager, baza);

            var result = await controller.RezervisiTermin(1, 5, TipInstrukcija.Hibridno) as RedirectToActionResult;

            Assert.IsNotNull(result);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(TipInstrukcija.Online, provjera.Termin.Single().tipInstrukcija);
        }

        [TestMethod]
        public async Task RezervisiTermin_TutorKaoKorisnik_PreusmjeravaNaStudentDashboardTutora()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviTutora("t1", true, 10, null, null, 100),
                new Termin { id = 1, idTutora = "t2", status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(1) });
            using var baza = new ApplicationDbContext(opcije);
            var userManager = UserManagerSaKorisnikom(NapraviTutora("t1", true, 10, null, null, 100));
            userManager.Setup(m => m.FindByIdAsync("t2")).ReturnsAsync(NapraviTutora("t2", true, 30));
            var controller = NapraviController(userManager, baza);

            var result = await controller.RezervisiTermin(1, 5, TipInstrukcija.Online) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("StudentDashboard", result.ActionName);
            Assert.IsNull(result.ControllerName);
        }

        // ---------- OstaviRecenziju ----------

        [TestMethod]
        public async Task OstaviRecenziju_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.OstaviRecenziju("t2", 5, "Super");

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task OstaviRecenziju_SystemManager_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("m1", Uloga.SystemManager)), baza);

            var result = await controller.OstaviRecenziju("t2", 5, "Super");

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task OstaviRecenziju_BezOdrzanogCasa_PreusmjeravaNaProfilSaGreskom()
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.OstaviRecenziju("t2", 5, "Super") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Profil", result.ActionName);
            Assert.AreEqual("t2", result.RouteValues!["id"]);
            Assert.IsNotNull(controller.TempData["Greska"]);
            Assert.AreEqual(0, baza.Recenzija.Count());
        }

        [TestMethod]
        public async Task OstaviRecenziju_VecPostojiRecenzija_PreusmjeravaSaGreskomBezNoveRecenzije()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Termin { id = 1, idStudenta = "s1", idTutora = "t2", status = StatusTermina.Odrzan },
                new Recenzija { idStudenta = "s1", idTutora = "t2", ocjena = 4, komentar = "Prije", datumRecenzije = new DateTime(2026, 11, 1) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.OstaviRecenziju("t2", 5, "Novo") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Profil", result.ActionName);
            Assert.IsNotNull(controller.TempData["Greska"]);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(1, provjera.Recenzija.Count());
        }

        [TestMethod]
        [DataRow(9, 5)]
        [DataRow(0, 1)]
        [DataRow(3, 3)]
        [DataRow(-2, 1)]
        public async Task OstaviRecenziju_RazlicitaOcjena_SpremaOgranicenuOcjenuIAzuriraProsjek(
            int ocjena, int ocekivanaOcjena)
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Termin { id = 1, idStudenta = "s1", idTutora = "t2", status = StatusTermina.Odrzan });
            using var baza = new ApplicationDbContext(opcije);
            var tutor = NapraviTutora("t2", true, 30, 0);
            var userManager = UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student));
            userManager.Setup(m => m.FindByIdAsync("t2")).ReturnsAsync(tutor);
            var controller = NapraviController(userManager, baza);

            var result = await controller.OstaviRecenziju("t2", ocjena, "Komentar") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Profil", result.ActionName);
            Assert.IsNotNull(controller.TempData["Uspjeh"]);
            userManager.Verify(m => m.UpdateAsync(tutor), Times.Once);
            Assert.AreEqual((double)ocekivanaOcjena, tutor.ProsjecnaOcjena);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(ocekivanaOcjena, provjera.Recenzija.Single().ocjena);
        }

        [TestMethod]
        public async Task OstaviRecenziju_TutorNePostoji_SpremaRecenzijuBezAzuriranjaTutora()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Termin { id = 1, idStudenta = "s1", idTutora = "nepoznat", status = StatusTermina.Odrzan });
            using var baza = new ApplicationDbContext(opcije);
            var userManager = UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student));
            userManager.Setup(m => m.FindByIdAsync("nepoznat")).ReturnsAsync((Korisnik?)null);
            var controller = NapraviController(userManager, baza);

            var result = await controller.OstaviRecenziju("nepoznat", 4, "Ok") as RedirectToActionResult;

            Assert.IsNotNull(result);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(1, provjera.Recenzija.Count());
        }

        // ---------- Dodatne pomoćne metode ----------

        private static object[] NapraviObavjestenja(string idKorisnika, int broj, bool procitano = false)
        {
            return Enumerable.Range(1, broj)
                .Select(i => (object)new Obavjestenje
                {
                    idKorisnika = idKorisnika,
                    naslov = "n",
                    sadrzaj = "s",
                    datumSlanja = new DateTime(2026, 11, 1).AddMinutes(i),
                    procitano = procitano
                })
                .ToArray();
        }

        private static void PostaviUrlHelper(TutorController controller)
        {
            var url = new Mock<IUrlHelper>();
            url.Setup(u => u.Action(It.IsAny<Microsoft.AspNetCore.Mvc.Routing.UrlActionContext>()))
               .Returns("http://test/qr");
            controller.Url = url.Object;
        }

        // ---------- Index ----------

        [TestMethod]
        public async Task Index_ImaTutora_VracaSamoVerifikovaneTutore()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviTutora("t1", true),
                NapraviTutora("t2", false),
                NapraviTutora("t3", null),
                NapraviKorisnika("s1", Uloga.Student));
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Korisnik>;
            Assert.IsNotNull(model);
            Assert.AreEqual(1, model.Count);
            Assert.AreEqual("t1", model[0].Id);
        }

        // ---------- Detalji ----------

        [TestMethod]
        public async Task Detalji_TutorNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("x")).ReturnsAsync((Korisnik?)null);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Detalji("x");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Detalji_KorisnikNijeTutor_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("s1")).ReturnsAsync(NapraviKorisnika("s1", Uloga.Student));
            var controller = NapraviController(userManager, baza);

            var result = await controller.Detalji("s1");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Detalji_PostojeciTutor_VracaViewSaTutorom()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var tutor = NapraviTutora("t1");
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("t1")).ReturnsAsync(tutor);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Detalji("t1") as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(tutor, result.Model);
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
        public async Task Kreiraj_UspjesnoKreiranje_PreusmjeravaNaIndexIPostavljaPodatke()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()))
                       .ReturnsAsync(IdentityResult.Success);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Kreiraj("Amar", "Hadzic", "amar@test.ba", "Lozinka1!", 25) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            userManager.Verify(m => m.CreateAsync(
                It.Is<Korisnik>(k => k.Uloga == Uloga.Tutor
                                  && k.VerifikovanTutor == true
                                  && k.CijenaPoSatu == 25
                                  && k.Email == "amar@test.ba"),
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

            var result = await controller.Kreiraj("Amar", "Hadzic", "amar@test.ba", "1", 25) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(1, controller.ModelState.ErrorCount);
        }

        // ---------- Uredi ----------

        [TestMethod]
        public async Task Uredi_Get_TutorNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("x")).ReturnsAsync((Korisnik?)null);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Uredi("x");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Uredi_Get_KorisnikNijeTutor_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("s1")).ReturnsAsync(NapraviKorisnika("s1", Uloga.Student));
            var controller = NapraviController(userManager, baza);

            var result = await controller.Uredi("s1");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Uredi_Get_PostojeciTutor_VracaViewSaTutorom()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var tutor = NapraviTutora("t1");
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("t1")).ReturnsAsync(tutor);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Uredi("t1") as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(tutor, result.Model);
        }

        [TestMethod]
        public async Task Uredi_Post_TutorNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("x")).ReturnsAsync((Korisnik?)null);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Uredi("x", 30);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Uredi_Post_KorisnikNijeTutor_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("s1")).ReturnsAsync(NapraviKorisnika("s1", Uloga.Student));
            var controller = NapraviController(userManager, baza);

            var result = await controller.Uredi("s1", 30);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Uredi_Post_PostojeciTutor_MijenjaCijenuIPreusmjerava()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var tutor = NapraviTutora("t1", true, 10);
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("t1")).ReturnsAsync(tutor);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Uredi("t1", 35) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual(35.0, tutor.CijenaPoSatu);
            userManager.Verify(m => m.UpdateAsync(tutor), Times.Once);
        }

        // ---------- Obrisi ----------

        [TestMethod]
        public async Task Obrisi_TutorNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("x")).ReturnsAsync((Korisnik?)null);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Obrisi("x");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Obrisi_KorisnikNijeTutor_VracaNotFoundBezBrisanja()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("s1")).ReturnsAsync(NapraviKorisnika("s1", Uloga.Student));
            var controller = NapraviController(userManager, baza);

            var result = await controller.Obrisi("s1");

            Assert.IsInstanceOfType<NotFoundResult>(result);
            userManager.Verify(m => m.DeleteAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task Obrisi_PostojeciTutor_BriseGaIPreusmjerava()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var tutor = NapraviTutora("t1");
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("t1")).ReturnsAsync(tutor);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Obrisi("t1") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            userManager.Verify(m => m.DeleteAsync(tutor), Times.Once);
        }

        // ---------- Pretraga ----------

        [TestMethod]
        public async Task Pretraga_PrazanUpit_PreusmjeravaNaStudentDashboard()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.Pretraga("  ") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.AreEqual("Student", result.ControllerName);
        }

        [TestMethod]
        public async Task Pretraga_UpitPoImenuIPredmetu_VracaVerifikovaneTutoreBezDuplikata()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviTutora("Mat1", true),
                NapraviTutora("t2", true),
                NapraviTutora("Mat3", false),
                new Predmet { id = 1, naziv = "Matematika" },
                new KorisnikPredmet { idKorisnika = "Mat1", idPredmeta = 1 },
                new KorisnikPredmet { idKorisnika = "t2", idPredmeta = 1 });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.Pretraga("Mat") as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Korisnik>;
            Assert.IsNotNull(model);
            CollectionAssert.AreEquivalent(new[] { "Mat1", "t2" }, model.Select(k => k.Id).ToList());
        }

        // ---------- RezervacijaTermina ----------

        [TestMethod]
        public async Task RezervacijaTermina_TutorNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("x")).ReturnsAsync((Korisnik?)null);
            var controller = NapraviController(userManager, baza);

            var result = await controller.RezervacijaTermina("x");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task RezervacijaTermina_KorisnikNijeTutor_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("s1")).ReturnsAsync(NapraviKorisnika("s1", Uloga.Student));
            var controller = NapraviController(userManager, baza);

            var result = await controller.RezervacijaTermina("s1");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task RezervacijaTermina_TutorSaTerminima_VracaModelSaPredmetimaISlobodnimTerminima()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Predmet { id = 1, naziv = "Matematika" },
                new KorisnikPredmet { idKorisnika = "t1", idPredmeta = 1 },
                new Termin { id = 1, idTutora = "t1", idPredmeta = 1, status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(1) },
                new Termin { id = 2, idTutora = "t1", idPredmeta = 99, status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(2) },
                new Termin { id = 3, idTutora = "t1", idPredmeta = null, status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(3) },
                new Termin { id = 4, idTutora = "t1", status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(-1) },
                new Termin { id = 5, idTutora = "t1", status = StatusTermina.Rezervisan, datumIVrijeme = DateTime.Now.AddDays(1) });
            using var baza = new ApplicationDbContext(opcije);
            var tutor = NapraviTutora("t1");
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("t1")).ReturnsAsync(tutor);
            var controller = NapraviController(userManager, baza);

            var result = await controller.RezervacijaTermina("t1") as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as TutorProfilViewModel;
            Assert.IsNotNull(model);
            Assert.AreSame(tutor, model.Tutor);
            Assert.AreEqual(1, model.Predmeti.Count());
            Assert.AreEqual(3, model.DostupniTermini.Count());
            var predmeti = (Dictionary<int, string>)controller.ViewBag.TerminPredmeti;
            Assert.AreEqual(2, predmeti.Count);
            Assert.AreEqual("Matematika", predmeti[1]);
            Assert.AreEqual("", predmeti[2]);
        }

        // ---------- Recenzije ----------

        [TestMethod]
        public async Task Recenzije_TutorNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("x")).ReturnsAsync((Korisnik?)null);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Recenzije("x");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Recenzije_KorisnikNijeTutor_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("s1")).ReturnsAsync(NapraviKorisnika("s1", Uloga.Student));
            var controller = NapraviController(userManager, baza);

            var result = await controller.Recenzije("s1");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Recenzije_TutorSaRecenzijama_VracaModelSaProsjekomINaziviStudenata()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("s1", Uloga.Student),
                new Recenzija { idStudenta = "s1", idTutora = "t1", ocjena = 5, komentar = "Super", datumRecenzije = new DateTime(2026, 11, 1) },
                new Recenzija { idStudenta = "nepoznat", idTutora = "t1", ocjena = 3, komentar = "Ok", datumRecenzije = new DateTime(2026, 11, 2) });
            using var baza = new ApplicationDbContext(opcije);
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("t1")).ReturnsAsync(NapraviTutora("t1"));
            var controller = NapraviController(userManager, baza);

            var result = await controller.Recenzije("t1") as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as TutorProfilViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.UkupnoRecenzija);
            Assert.AreEqual(4.0, model.ProsjecnaOcjena);
            var recenzije = model.Recenzije.ToList();
            Assert.AreEqual("Student", recenzije[0].ImeStudenta);
            Assert.AreEqual("Imes1", recenzije[1].ImeStudenta);
        }

        [TestMethod]
        public async Task Recenzije_TutorBezRecenzija_VracaNulaProsjekINulaRecenzija()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("t1")).ReturnsAsync(NapraviTutora("t1"));
            var controller = NapraviController(userManager, baza);

            var result = await controller.Recenzije("t1") as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as TutorProfilViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(0, model.UkupnoRecenzija);
            Assert.AreEqual(0, model.ProsjecnaOcjena);
        }

        // ---------- MojiCasovi ----------

        [TestMethod]
        public async Task MojiCasovi_KorisnikNijePrijavljen_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.MojiCasovi() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task MojiCasovi_KorisnikNijeTutor_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.MojiCasovi() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task MojiCasovi_NeverifikovanTutor_VracaViewVerifikacijaNaCekanju()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviTutora("t1", false)), baza);

            var result = await controller.MojiCasovi() as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("VerifikacijaNaCekanju", result.ViewName);
        }

        [TestMethod]
        public async Task MojiCasovi_VerifikovanTutor_VracaSvojeTerminePoNajnovijem()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("s1", Uloga.Student),
                new Predmet { id = 1, naziv = "Fizika" },
                new Termin { id = 1, idTutora = "t1", idStudenta = "s1", idPredmeta = 1, datumIVrijeme = DateTime.Now.AddDays(-2) },
                new Termin { id = 2, idTutora = "t1", idStudenta = "", idPredmeta = null, datumIVrijeme = DateTime.Now.AddDays(1) },
                new Termin { id = 3, idTutora = "drugi", idStudenta = "s1", datumIVrijeme = DateTime.Now });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.MojiCasovi() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<TerminSaTutorom>;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.Count);
            Assert.AreEqual(2, model[0].Termin.id);
            Assert.AreEqual("Nije", model[0].Tutor.Ime);
            Assert.IsNull(model[0].Predmet);
            Assert.AreEqual("Imes1", model[1].Tutor.Ime);
            Assert.AreEqual("Fizika", model[1].Predmet!.naziv);
        }

        // ---------- KreirajTermin (GET) ----------

        [TestMethod]
        public async Task KreirajTermin_Get_KorisnikNijePrijavljen_VracaForbid()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.KreirajTermin();

            Assert.IsInstanceOfType<ForbidResult>(result);
        }

        [TestMethod]
        public async Task KreirajTermin_Get_KorisnikNijeTutor_VracaForbid()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.KreirajTermin();

            Assert.IsInstanceOfType<ForbidResult>(result);
        }

        [TestMethod]
        public async Task KreirajTermin_Get_NeverifikovanTutor_VracaViewVerifikacijaNaCekanju()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviTutora("t1", false)), baza);

            var result = await controller.KreirajTermin() as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("VerifikacijaNaCekanju", result.ViewName);
        }

        [TestMethod]
        public async Task KreirajTermin_Get_VerifikovanTutor_VracaViewSaPredmetimaICijenom()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Predmet { id = 1, naziv = "Matematika" },
                new Predmet { id = 2, naziv = "Fizika" },
                new KorisnikPredmet { idKorisnika = "t1", idPredmeta = 1 });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviTutora("t1", true, 40)), baza);

            var result = await controller.KreirajTermin() as ViewResult;

            Assert.IsNotNull(result);
            var predmeti = (List<Predmet>)controller.ViewBag.Predmeti;
            Assert.AreEqual(1, predmeti.Count);
            Assert.AreEqual("Matematika", predmeti[0].naziv);
            Assert.AreEqual(40.0, (double)controller.ViewBag.CijenaPoSatu);
        }

        // ---------- KreirajTermin (POST) ----------

        [TestMethod]
        public async Task KreirajTermin_Post_KorisnikNijePrijavljen_VracaForbid()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.KreirajTermin(DateTime.Now, 1, TipInstrukcija.Online);

            Assert.IsInstanceOfType<ForbidResult>(result);
        }

        [TestMethod]
        public async Task KreirajTermin_Post_KorisnikNijeTutor_VracaForbid()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.KreirajTermin(DateTime.Now, 1, TipInstrukcija.Online);

            Assert.IsInstanceOfType<ForbidResult>(result);
        }

        [TestMethod]
        [DataRow(40.0, 40.0)]
        [DataRow(null, 0.0)]
        public async Task KreirajTermin_Post_Tutor_SpremaSlobodanTerminSaCijenomTutora(
            double? cijenaTutora, double ocekivanaCijena)
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviTutora("t1", true, cijenaTutora)), baza);
            var datum = new DateTime(2026, 12, 1, 10, 0, 0);

            var result = await controller.KreirajTermin(datum, 3, TipInstrukcija.Online) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("MojiCasovi", result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            var termin = provjera.Termin.Single();
            Assert.AreEqual("t1", termin.idTutora);
            Assert.AreEqual("", termin.idStudenta);
            Assert.AreEqual(3, termin.idPredmeta);
            Assert.AreEqual(datum, termin.datumIVrijeme);
            Assert.AreEqual(TipInstrukcija.Online, termin.tipInstrukcija);
            Assert.AreEqual(StatusTermina.Slobodan, termin.status);
            Assert.AreEqual(ocekivanaCijena, termin.cijena);
        }

        // ---------- UnesMjestoCasa (GET) ----------

        [TestMethod]
        public async Task UnesMjestoCasa_Get_KorisnikNijePrijavljen_VracaForbid()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.UnesMjestoCasa(1);

            Assert.IsInstanceOfType<ForbidResult>(result);
        }

        [TestMethod]
        public async Task UnesMjestoCasa_Get_KorisnikNijeTutor_VracaForbid()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.UnesMjestoCasa(1);

            Assert.IsInstanceOfType<ForbidResult>(result);
        }

        [TestMethod]
        public async Task UnesMjestoCasa_Get_TerminNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.UnesMjestoCasa(99);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task UnesMjestoCasa_Get_TerminDrugogTutora_VracaNotFound()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Termin { id = 1, idTutora = "drugi" });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.UnesMjestoCasa(1);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task UnesMjestoCasa_Get_TerminSaPredmetom_VracaViewSaNazivomPredmeta()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Predmet { id = 1, naziv = "Hemija" },
                new Termin { id = 1, idTutora = "t1", idPredmeta = 1 });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.UnesMjestoCasa(1) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(1, ((Termin)result.Model!).id);
            Assert.AreEqual("Hemija", (string)controller.ViewBag.Predmet);
        }

        [TestMethod]
        public async Task UnesMjestoCasa_Get_TerminBezPredmeta_VracaCrticuKaoNazivPredmeta()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Termin { id = 1, idTutora = "t1", idPredmeta = null });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.UnesMjestoCasa(1) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("—", (string)controller.ViewBag.Predmet);
        }

        // ---------- UnesMjestoCasa (POST) ----------

        [TestMethod]
        public async Task UnesMjestoCasa_Post_KorisnikNijePrijavljen_VracaForbid()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.UnesMjestoCasa(1, "Sala 5");

            Assert.IsInstanceOfType<ForbidResult>(result);
        }

        [TestMethod]
        public async Task UnesMjestoCasa_Post_KorisnikNijeTutor_VracaForbid()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.UnesMjestoCasa(1, "Sala 5");

            Assert.IsInstanceOfType<ForbidResult>(result);
        }

        [TestMethod]
        public async Task UnesMjestoCasa_Post_TerminNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.UnesMjestoCasa(99, "Sala 5");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task UnesMjestoCasa_Post_TerminDrugogTutora_VracaNotFound()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Termin { id = 1, idTutora = "drugi" });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.UnesMjestoCasa(1, "Sala 5");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task UnesMjestoCasa_Post_OnlineTerminSaStudentom_SpremaMjestoISaljeLinkStudentu()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Termin { id = 1, idTutora = "t1", idStudenta = "s1", tipInstrukcija = TipInstrukcija.Online, datumIVrijeme = new DateTime(2026, 12, 1, 10, 0, 0) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.UnesMjestoCasa(1, "https://meet.test/abc") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("MojiCasovi", result.ActionName);
            Assert.IsNotNull(controller.TempData["Uspjeh"]);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual("https://meet.test/abc", provjera.Termin.Single().mjestoCasa);
            var obavjestenje = provjera.Obavjestenje.Single();
            Assert.AreEqual("s1", obavjestenje.idKorisnika);
            Assert.IsTrue(obavjestenje.naslov.Contains("online"));
        }

        [TestMethod]
        public async Task UnesMjestoCasa_Post_TerminUzivoSaStudentom_SaljeLokacijuStudentu()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Termin { id = 1, idTutora = "t1", idStudenta = "s1", tipInstrukcija = TipInstrukcija.Hibridno, datumIVrijeme = new DateTime(2026, 12, 1, 10, 0, 0) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.UnesMjestoCasa(1, "Sala 5") as RedirectToActionResult;

            Assert.IsNotNull(result);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.IsTrue(provjera.Obavjestenje.Single().naslov.Contains("Lokacija"));
        }

        [TestMethod]
        public async Task UnesMjestoCasa_Post_TerminBezStudenta_SpremaMjestoBezObavjestenja()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Termin { id = 1, idTutora = "t1", idStudenta = "" });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.UnesMjestoCasa(1, "Sala 5") as RedirectToActionResult;

            Assert.IsNotNull(result);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual("Sala 5", provjera.Termin.Single().mjestoCasa);
            Assert.AreEqual(0, provjera.Obavjestenje.Count());
        }

        // ---------- QrKod ----------

        [TestMethod]
        public async Task QrKod_KorisnikNijePrijavljen_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.QrKod(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task QrKod_KorisnikNijeTutor_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.QrKod(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task QrKod_TerminNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.QrKod(99);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task QrKod_TerminDrugogTutora_VracaNotFound()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Termin { id = 1, idTutora = "drugi" });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.QrKod(1);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task QrKod_TerminBezKoda_GenerisePrviKodISprema()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Predmet { id = 1, naziv = "Fizika" },
                new Termin { id = 1, idTutora = "t1", idPredmeta = 1, qrKod = "" });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);
            PostaviUrlHelper(controller);

            var result = await controller.QrKod(1) as ViewResult;

            Assert.IsNotNull(result);
            using var provjera = new ApplicationDbContext(opcije);
            var kod = provjera.Termin.Single().qrKod!;
            Assert.IsTrue(kod.StartsWith("EARNLEARN-1-"));
            Assert.AreEqual(kod, (string)controller.ViewBag.QrKodString);
            Assert.AreEqual("Fizika", (string)controller.ViewBag.Predmet);
            Assert.AreEqual("http://test/qr", (string)controller.ViewBag.QrUrlZaSkeniranje);
        }

        [TestMethod]
        public async Task QrKod_TerminSaPostojecimKodomBezPredmeta_ZadrzavaKod()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Termin { id = 1, idTutora = "t1", idPredmeta = null, qrKod = "POSTOJECI-KOD" });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);
            PostaviUrlHelper(controller);

            var result = await controller.QrKod(1) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("POSTOJECI-KOD", (string)controller.ViewBag.QrKodString);
            Assert.AreEqual("Nepoznat predmet", (string)controller.ViewBag.Predmet);
        }

        // ---------- ZavrsiCas ----------

        [TestMethod]
        public async Task ZavrsiCas_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.ZavrsiCas(1);

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task ZavrsiCas_KorisnikNijeTutor_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.ZavrsiCas(1);

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task ZavrsiCas_TerminNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.ZavrsiCas(99);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task ZavrsiCas_TerminDrugogTutora_VracaNotFound()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Termin { id = 1, idTutora = "drugi" });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.ZavrsiCas(1);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        [DataRow(null, 1)]
        [DataRow(4, 5)]
        public async Task ZavrsiCas_Tutor_OznacavaTerminOdrzanimIPovecavaBrojCasova(
            int? pocetniBroj, int ocekivaniBroj)
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Termin { id = 1, idTutora = "t1", status = StatusTermina.Rezervisan });
            using var baza = new ApplicationDbContext(opcije);
            var tutor = NapraviTutora("t1", true, null, null, pocetniBroj);
            var userManager = UserManagerSaKorisnikom(tutor);
            var controller = NapraviController(userManager, baza);

            var result = await controller.ZavrsiCas(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.IsNotNull(controller.TempData["Uspjeh"]);
            Assert.AreEqual(ocekivaniBroj, tutor.BrojOdrzanihCasova);
            userManager.Verify(m => m.UpdateAsync(tutor), Times.Once);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(StatusTermina.Odrzan, provjera.Termin.Single().status);
        }

        // ---------- Novcanik ----------

        [TestMethod]
        public async Task Novcanik_KorisnikNijePrijavljen_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.Novcanik() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task Novcanik_KorisnikNijeTutor_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.Novcanik() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task Novcanik_Tutor_VracaPozitivneTransakcijeStanjeIUkupnuZaradu()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Transakcija { idTutora = "t1", iznos = 50, statusPlacanja = StatusPlacanja.Uspjesno, datumUplate = new DateTime(2026, 11, 1) },
                new Transakcija { idTutora = "t1", iznos = 30, statusPlacanja = StatusPlacanja.Vraceno, datumUplate = new DateTime(2026, 11, 2) },
                new Transakcija { idTutora = "t1", iznos = -10, statusPlacanja = StatusPlacanja.Uspjesno, datumUplate = new DateTime(2026, 11, 3) },
                new Transakcija { idTutora = "drugi", iznos = 99, statusPlacanja = StatusPlacanja.Uspjesno, datumUplate = new DateTime(2026, 11, 4) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviTutora("t1", true, null, null, null, 75)), baza);

            var result = await controller.Novcanik() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Transakcija>;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.Count);
            Assert.AreEqual(75.0, (double)controller.ViewBag.StanjeRacuna);
            Assert.AreEqual(50.0, (double)controller.ViewBag.UkupnoZaradeno);
        }

        // ---------- Obavjestenja ----------

        [TestMethod]
        public async Task Obavjestenja_KorisnikNijePrijavljen_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.Obavjestenja() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task Obavjestenja_KorisnikNijeTutor_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.Obavjestenja() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task Obavjestenja_Tutor_VracaModelSaZastavicomMjestaCasaIOznacavaProcitanim()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Termin { id = 1, idTutora = "t1", mjestoCasa = "Sala 5" },
                new Termin { id = 2, idTutora = "t1", mjestoCasa = "" },
                new Obavjestenje { id = 1, idKorisnika = "t1", naslov = "n", sadrzaj = "s", idTermina = 1, procitano = false, datumSlanja = new DateTime(2026, 11, 3) },
                new Obavjestenje { id = 2, idKorisnika = "t1", naslov = "n", sadrzaj = "s", idTermina = 2, procitano = false, datumSlanja = new DateTime(2026, 11, 2) },
                new Obavjestenje { id = 3, idKorisnika = "t1", naslov = "n", sadrzaj = "s", idTermina = null, procitano = false, datumSlanja = new DateTime(2026, 11, 1) },
                new Obavjestenje { id = 4, idKorisnika = "drugi", naslov = "n", sadrzaj = "s", procitano = false, datumSlanja = new DateTime(2026, 11, 1) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.Obavjestenja() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<ObavjestenjeViewModel>;
            Assert.IsNotNull(model);
            Assert.AreEqual(3, model.Count);
            Assert.IsTrue(model.Single(m => m.Obavjestenje.id == 1).MjestoCasaUneseno);
            Assert.IsFalse(model.Single(m => m.Obavjestenje.id == 2).MjestoCasaUneseno);
            Assert.IsFalse(model.Single(m => m.Obavjestenje.id == 3).MjestoCasaUneseno);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(3, provjera.Obavjestenje.Count(o => o.procitano));
        }

        // ---------- DohvatiNotifikacije ----------

        [TestMethod]
        public async Task DohvatiNotifikacije_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.DohvatiNotifikacije();

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task DohvatiNotifikacije_Tutor_VracaNotifikacijeSaZastavicomMjestaCasa()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Termin { id = 1, idTutora = "t1", mjestoCasa = "Sala 5" },
                new Obavjestenje { id = 1, idKorisnika = "t1", naslov = "n", sadrzaj = "s", idTermina = 1, datumSlanja = new DateTime(2026, 11, 3) },
                new Obavjestenje { id = 2, idKorisnika = "t1", naslov = "n", sadrzaj = "s", idTermina = null, datumSlanja = new DateTime(2026, 11, 2) },
                new Obavjestenje { id = 3, idKorisnika = "drugi", naslov = "n", sadrzaj = "s", datumSlanja = new DateTime(2026, 11, 1) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.DohvatiNotifikacije() as JsonResult;

            Assert.IsNotNull(result);
            var json = UJson(result.Value);
            Assert.AreEqual(2, json.GetArrayLength());
            Assert.AreEqual(1, json[0].GetProperty("id").GetInt32());
            Assert.IsTrue(json[0].GetProperty("mjestoCasaUneseno").GetBoolean());
            Assert.IsFalse(json[1].GetProperty("mjestoCasaUneseno").GetBoolean());
        }

        // ---------- BrojNotifikacija ----------

        [TestMethod]
        public async Task BrojNotifikacija_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.BrojNotifikacija();

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task BrojNotifikacija_ImaProcitanihINeprocitanih_VracaBrojNeprocitanih()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, NapraviObavjestenja("t1", 2));
            Ubaci(opcije, NapraviObavjestenja("t1", 1, true));
            Ubaci(opcije, NapraviObavjestenja("drugi", 5));
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.BrojNotifikacija() as JsonResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(2, UJson(result.Value).GetProperty("broj").GetInt32());
        }

        // ---------- OznaciProcitanim ----------

        [TestMethod]
        public async Task OznaciProcitanim_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.OznaciProcitanim(1);

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task OznaciProcitanim_ObavjestenjeKorisnika_OznacavaGaProcitanim()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Obavjestenje { id = 1, idKorisnika = "t1", naslov = "n", sadrzaj = "s", procitano = false });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.OznaciProcitanim(1);

            Assert.IsInstanceOfType<OkResult>(result);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.IsTrue(provjera.Obavjestenje.Single().procitano);
        }

        [TestMethod]
        public async Task OznaciProcitanim_ObavjestenjeDrugogKorisnika_NeMijenjaGa()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Obavjestenje { id = 1, idKorisnika = "drugi", naslov = "n", sadrzaj = "s", procitano = false });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.OznaciProcitanim(1);

            Assert.IsInstanceOfType<OkResult>(result);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.IsFalse(provjera.Obavjestenje.Single().procitano);
        }

        // ---------- OznaciSveProcitanim ----------

        [TestMethod]
        public async Task OznaciSveProcitanim_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.OznaciSveProcitanim();

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task OznaciSveProcitanim_NeprocitanaObavjestenja_OznacavaSamoKorisnikova()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, NapraviObavjestenja("t1", 3));
            Ubaci(opcije, NapraviObavjestenja("drugi", 2));
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora("t1")), baza);

            var result = await controller.OznaciSveProcitanim();

            Assert.IsInstanceOfType<OkResult>(result);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(3, provjera.Obavjestenje.Count(o => o.procitano));
            Assert.AreEqual(2, provjera.Obavjestenje.Count(o => !o.procitano));
        }
    }
}