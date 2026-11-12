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
    public class StudentControllerTests
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

        private static StudentController NapraviController(
            Mock<UserManager<Korisnik>> userManager, ApplicationDbContext baza)
        {
            var httpContext = new DefaultHttpContext();
            return new StudentController(userManager.Object, baza)
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
        public async Task Dashboard_KorisnikNijeStudent_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("t1", Uloga.Tutor)), baza);

            var result = await controller.Dashboard() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task Dashboard_StudentSaTerminima_VracaModelSaNadolazecimTerminima()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("t1", Uloga.Tutor),
                new Predmet { id = 1, naziv = "Matematika" },
                new Termin { id = 1, idStudenta = "s1", idTutora = "t1", idPredmeta = 1, datumIVrijeme = DateTime.Now.AddDays(1) },
                new Termin { id = 2, idStudenta = "s1", idTutora = "nepoznat", idPredmeta = null, datumIVrijeme = DateTime.Now.AddDays(2) },
                new Termin { id = 3, idStudenta = "s1", idTutora = "t1", datumIVrijeme = DateTime.Now.AddDays(-1) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.Dashboard() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as StudentDashboardViewModel;
            Assert.IsNotNull(model);
            var termini = model.NadolazaciTermini.ToList();
            Assert.AreEqual("Imes1", model.Ime);
            Assert.AreEqual(2, termini.Count);
            Assert.AreEqual("Imet1", termini[0].Tutor.Ime);
            Assert.AreEqual("Matematika", termini[0].Predmet!.naziv);
            Assert.AreEqual("Nepoznat", termini[1].Tutor.Ime);
            Assert.IsNull(termini[1].Predmet);
            Assert.AreEqual(1, model.TopTutori.Count);
        }

        // ---------- Obrisi ----------

        [TestMethod]
        public async Task Obrisi_StudentNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("x")).ReturnsAsync((Korisnik?)null);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Obrisi("x");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Obrisi_KorisnikNijeStudent_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("t1"))
                       .ReturnsAsync(NapraviKorisnika("t1", Uloga.Tutor));
            var controller = NapraviController(userManager, baza);

            var result = await controller.Obrisi("t1");

            Assert.IsInstanceOfType<NotFoundResult>(result);
            userManager.Verify(m => m.DeleteAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task Obrisi_PostojeciStudent_BriseGaIPreusmjerava()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var student = NapraviKorisnika("s1", Uloga.Student);
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("s1")).ReturnsAsync(student);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Obrisi("s1") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            userManager.Verify(m => m.DeleteAsync(student), Times.Once);
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
        public async Task MojiCasovi_KorisnikNijeStudent_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("t1", Uloga.Tutor)), baza);

            var result = await controller.MojiCasovi() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task MojiCasovi_StudentSaTerminima_VracaSamoNjegoveNajnovijePrvo()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("t1", Uloga.Tutor),
                new Predmet { id = 1, naziv = "Fizika" },
                new Termin { id = 10, idStudenta = "s1", idTutora = "t1", idPredmeta = 1, datumIVrijeme = DateTime.Now.AddDays(-2) },
                new Termin { id = 11, idStudenta = "s1", idTutora = "nepoznat", idPredmeta = null, datumIVrijeme = DateTime.Now.AddDays(1) },
                new Termin { id = 12, idStudenta = "drugi", idTutora = "t1", datumIVrijeme = DateTime.Now });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.MojiCasovi() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<TerminSaTutorom>;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.Count);
            Assert.AreEqual(11, model[0].Termin.id);
            Assert.AreEqual("Nepoznat", model[0].Tutor.Ime);
            Assert.IsNull(model[0].Predmet);
            Assert.AreEqual("Imet1", model[1].Tutor.Ime);
            Assert.AreEqual("Fizika", model[1].Predmet!.naziv);
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
        public async Task OtkaziTermin_KorisnikNijeStudent_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("t1", Uloga.Tutor)), baza);

            var result = await controller.OtkaziTermin(1);

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task OtkaziTermin_TerminNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.OtkaziTermin(99);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task OtkaziTermin_TerminDrugogStudenta_VracaNotFound()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Termin { id = 1, idStudenta = "drugi", idTutora = "t1" });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.OtkaziTermin(1);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task OtkaziTermin_RezervisanSaTutorom_VracaNovacOslobadjaTerminIObavjestava()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("t1", Uloga.Tutor, 100),
                new Termin { id = 1, idStudenta = "s1", idTutora = "t1", cijena = 20, status = StatusTermina.Rezervisan, datumIVrijeme = new DateTime(2026, 12, 1, 10, 0, 0) });
            using var baza = new ApplicationDbContext(opcije);
            var student = NapraviKorisnika("s1", Uloga.Student, 0);
            var userManager = UserManagerSaKorisnikom(student);
            var controller = NapraviController(userManager, baza);

            var result = await controller.OtkaziTermin(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("MojiCasovi", result.ActionName);
            Assert.AreEqual(20, student.StanjeRacuna);
            Assert.IsNotNull(controller.TempData["Uspjeh"]);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Exactly(2));
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(80, provjera.Users.Single().StanjeRacuna);
            var termin = provjera.Termin.Single();
            Assert.AreEqual(StatusTermina.Slobodan, termin.status);
            Assert.AreEqual(string.Empty, termin.idStudenta);
            Assert.AreEqual(0, termin.cijena);
            var transakcija = provjera.Transakcija.Single();
            Assert.AreEqual(-20, transakcija.iznos);
            Assert.AreEqual(StatusPlacanja.Vraceno, transakcija.statusPlacanja);
            Assert.AreEqual("t1", provjera.Obavjestenje.Single().idKorisnika);
        }

        [TestMethod]
        public async Task OtkaziTermin_RezervisanAliTutorNePostoji_NeMijenjaStanjaAliOslobadjaTermin()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Termin { id = 1, idStudenta = "s1", idTutora = "nepoznat", cijena = 20, status = StatusTermina.Rezervisan, datumIVrijeme = new DateTime(2026, 12, 1, 10, 0, 0) });
            using var baza = new ApplicationDbContext(opcije);
            var student = NapraviKorisnika("s1", Uloga.Student, 0);
            var userManager = UserManagerSaKorisnikom(student);
            var controller = NapraviController(userManager, baza);

            var result = await controller.OtkaziTermin(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(0, student.StanjeRacuna);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(StatusTermina.Slobodan, provjera.Termin.Single().status);
        }

        [TestMethod]
        public async Task OtkaziTermin_TerminNijeRezervisan_NistaNeMijenjaIPreusmjerava()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Termin { id = 1, idStudenta = "s1", idTutora = "t1", cijena = 20, status = StatusTermina.Odrzan });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.OtkaziTermin(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("MojiCasovi", result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(StatusTermina.Odrzan, provjera.Termin.Single().status);
            Assert.AreEqual(0, provjera.Transakcija.Count());
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
        public async Task Novcanik_KorisnikNijeStudent_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("t1", Uloga.Tutor)), baza);

            var result = await controller.Novcanik() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task Novcanik_StudentSaTransakcijama_VracaTransakcijeStanjeIUkupnoUplaceno()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Transakcija { idStudenta = "s1", iznos = 50, statusPlacanja = StatusPlacanja.Uspjesno, datumUplate = new DateTime(2026, 11, 1) },
                new Transakcija { idStudenta = "s1", iznos = -20, statusPlacanja = StatusPlacanja.Vraceno, datumUplate = new DateTime(2026, 11, 2) },
                new Transakcija { idStudenta = "s1", iznos = 30, statusPlacanja = StatusPlacanja.Uspjesno, datumUplate = new DateTime(2026, 11, 3) },
                new Transakcija { idStudenta = "drugi", iznos = 999, statusPlacanja = StatusPlacanja.Uspjesno, datumUplate = new DateTime(2026, 11, 4) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student, 75)), baza);

            var result = await controller.Novcanik() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Transakcija>;
            Assert.IsNotNull(model);
            Assert.AreEqual(3, model.Count);
            Assert.AreEqual(75.0, (double)controller.ViewBag.StanjeRacuna);
            Assert.AreEqual(80.0, (double)controller.ViewBag.UkupnoUplaceno);
        }

        // ---------- UplatiNovac ----------

        [TestMethod]
        public async Task UplatiNovac_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.UplatiNovac(50);

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task UplatiNovac_KorisnikNijeStudent_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("t1", Uloga.Tutor)), baza);

            var result = await controller.UplatiNovac(50);

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        [DataRow(0.0)]
        [DataRow(-5.0)]
        [DataRow(10001.0)]
        public async Task UplatiNovac_NevalidanIznos_PreusmjeravaNaNovcanikSaGreskom(double iznos)
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var student = NapraviKorisnika("s1", Uloga.Student, 10);
            var controller = NapraviController(UserManagerSaKorisnikom(student), baza);

            var result = await controller.UplatiNovac(iznos) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Novcanik", result.ActionName);
            Assert.IsNotNull(controller.TempData["Greska"]);
            Assert.AreEqual(10, student.StanjeRacuna);
            Assert.AreEqual(0, baza.Transakcija.Count());
        }

        [TestMethod]
        public async Task UplatiNovac_NevalidanIznosAjax_VracaJsonSaNeuspjehom()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);
            controller.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

            var result = await controller.UplatiNovac(0) as JsonResult;

            Assert.IsNotNull(result);
            Assert.IsFalse(UJson(result.Value).GetProperty("success").GetBoolean());
        }

        [TestMethod]
        public async Task UplatiNovac_ValidanIznos_PovecavaStanjeBiljeziTransakcijuIPreusmjerava()
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var student = NapraviKorisnika("s1", Uloga.Student, 10);
            var userManager = UserManagerSaKorisnikom(student);
            var controller = NapraviController(userManager, baza);

            var result = await controller.UplatiNovac(50) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Novcanik", result.ActionName);
            Assert.AreEqual(60, student.StanjeRacuna);
            Assert.IsNotNull(controller.TempData["Uspjeh"]);
            userManager.Verify(m => m.UpdateAsync(student), Times.Once);
            using var provjera = new ApplicationDbContext(opcije);
            var transakcija = provjera.Transakcija.Single();
            Assert.AreEqual(50, transakcija.iznos);
            Assert.AreEqual(NacinPlacanja.Kartica, transakcija.nacinPlacanja);
            Assert.AreEqual(StatusPlacanja.Uspjesno, transakcija.statusPlacanja);
        }

        [TestMethod]
        public async Task UplatiNovac_ValidanIznosAjax_VracaJsonSaNovimStanjem()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student, 10)), baza);
            controller.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

            var result = await controller.UplatiNovac(50) as JsonResult;

            Assert.IsNotNull(result);
            var json = UJson(result.Value);
            Assert.IsTrue(json.GetProperty("success").GetBoolean());
            Assert.AreEqual(60, json.GetProperty("novoStanje").GetDouble());
        }


        // ---------- Dodatne pomoćne metode (drugi dio) ----------

        private static Korisnik NapraviTutora(string id, double? cijena = null, double? ocjena = null, int? casova = null)
        {
            var tutor = NapraviKorisnika(id, Uloga.Tutor);
            tutor.CijenaPoSatu = cijena;
            tutor.ProsjecnaOcjena = ocjena;
            tutor.BrojOdrzanihCasova = casova;
            return tutor;
        }

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

        // ---------- PretraziTutore ----------

        [TestMethod]
        public async Task PretraziTutore_PrazanUpit_VracaPraznuListu()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.PretraziTutore("  ") as JsonResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(0, UJson(result.Value).GetArrayLength());
        }

        [TestMethod]
        public async Task PretraziTutore_ViseTutora_VracaIhSortiraneRangom()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviTutora("t1", 10, 5, 0),
                NapraviTutora("t2", 10, 4, 10),
                NapraviTutora("t3", 10, 1, 1),
                NapraviKorisnika("s1", Uloga.Student));
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.PretraziTutore("ime") as JsonResult;

            Assert.IsNotNull(result);
            var tutori = UJson(result.Value);
            Assert.AreEqual(3, tutori.GetArrayLength());
            Assert.AreEqual("t2", tutori[0].GetProperty("id").GetString());
            Assert.AreEqual("t1", tutori[1].GetProperty("id").GetString());
            Assert.AreEqual("t3", tutori[2].GetProperty("id").GetString());
        }

        [TestMethod]
        public async Task PretraziTutore_UpitPoNazivuPredmeta_VracaTutoraSaPredmetom()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviTutora("t9", 10, 5, 1),
                new Predmet { id = 1, naziv = "Matematika" },
                new KorisnikPredmet { idKorisnika = "t9", idPredmeta = 1 });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.PretraziTutore("matem") as JsonResult;

            Assert.IsNotNull(result);
            var tutori = UJson(result.Value);
            Assert.AreEqual(1, tutori.GetArrayLength());
            Assert.AreEqual("t9", tutori[0].GetProperty("id").GetString());
            Assert.AreEqual("Matematika", tutori[0].GetProperty("predmeti")[0].GetString());
        }

        [TestMethod]
        [DataRow(15.0, null, null, "t2")]
        [DataRow(null, 15.0, null, "t1")]
        [DataRow(null, null, 4.0, "t2")]
        public async Task PretraziTutore_SaFilterima_VracaSamoOdgovarajuceTutore(
            double? minCijena, double? maxCijena, double? minRejting, string ocekivaniId)
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviTutora("t1", 10, 3, 1),
                NapraviTutora("t2", 20, 5, 1),
                NapraviTutora("t3"));
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(NapraviUserManagerMock(), baza);

            var result = await controller.PretraziTutore("ime", minCijena, maxCijena, minRejting) as JsonResult;

            Assert.IsNotNull(result);
            var tutori = UJson(result.Value);
            Assert.AreEqual(1, tutori.GetArrayLength());
            Assert.AreEqual(ocekivaniId, tutori[0].GetProperty("id").GetString());
        }

        [TestMethod]
        public void StudentDashboardViewModel_NovaInstanca_PodrazumijevaneVrijednostiSuPrazne()
        {
            var model = new StudentDashboardViewModel();

            Assert.AreEqual("", model.Ime);
            Assert.AreEqual("", model.Prezime);
            Assert.AreEqual(0, model.NadolazaciTermini.Count);
            Assert.AreEqual(0, model.TopTutori.Count);
        }

        [TestMethod]
        public void StudentDashboardViewModel_PostavljenaSvaSvojstva_VracaIstuVrijednost()
        {
            var termini = new List<TerminSaTutorom> { new TerminSaTutorom() };
            var tutori = new List<Korisnik> { new Korisnik() };

            var model = new StudentDashboardViewModel
            {
                Ime = "Amar",
                Prezime = "Hadžić",
                NadolazaciTermini = termini,
                TopTutori = tutori
            };

            Assert.AreEqual("Amar", model.Ime);
            Assert.AreEqual("Hadžić", model.Prezime);
            Assert.AreSame(termini, model.NadolazaciTermini);
            Assert.AreSame(tutori, model.TopTutori);
        }

        // ---------- RezervisiTermin (GET) ----------

        [TestMethod]
        public async Task RezervisiTermin_Get_KorisnikNijePrijavljen_VracaPristupOdbijen()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.RezervisiTermin(1) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ViewName);
        }

        [TestMethod]
        public async Task RezervisiTermin_Get_SystemManager_VracaPristupOdbijen()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("m1", Uloga.SystemManager)), baza);

            var result = await controller.RezervisiTermin(1) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ViewName);
        }

        [TestMethod]
        public async Task RezervisiTermin_Get_TerminNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.RezervisiTermin(99);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task RezervisiTermin_Get_TerminVecRezervisan_VracaNotFound()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Termin { id = 1, idTutora = "t1", status = StatusTermina.Rezervisan });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.RezervisiTermin(1);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task RezervisiTermin_Get_SlobodanTerminSaPredmetom_VracaViewSaTutoromIPredmetom()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("t1", Uloga.Tutor),
                new Predmet { id = 1, naziv = "Hemija" },
                new Termin { id = 1, idTutora = "t1", idPredmeta = 1, status = StatusTermina.Slobodan });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.RezervisiTermin(1) as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as Termin;
            Assert.IsNotNull(model);
            Assert.AreEqual(1, model.id);
            Assert.AreEqual("Imet1", ((Korisnik)controller.ViewBag.Tutor).Ime);
            Assert.AreEqual("Hemija", ((Predmet)controller.ViewBag.Predmet).naziv);
        }

        [TestMethod]
        public async Task RezervisiTermin_Get_SlobodanTerminBezPredmeta_PredmetJeNull()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Termin { id = 1, idTutora = "t1", idPredmeta = null, status = StatusTermina.Slobodan });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("t2", Uloga.Tutor)), baza);

            var result = await controller.RezervisiTermin(1) as ViewResult;

            Assert.IsNotNull(result);
            Assert.IsNull((Predmet?)controller.ViewBag.Predmet);
        }

        // ---------- RezervisiTermin (POST) ----------

        [TestMethod]
        public async Task RezervisiTermin_Post_KorisnikNijePrijavljen_VracaPristupOdbijen()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.RezervisiTermin(1, (TipInstrukcija)0) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ViewName);
        }

        [TestMethod]
        public async Task RezervisiTermin_Post_SystemManager_VracaPristupOdbijen()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("m1", Uloga.SystemManager)), baza);

            var result = await controller.RezervisiTermin(1, (TipInstrukcija)0) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ViewName);
        }

        [TestMethod]
        public async Task RezervisiTermin_Post_TerminNijeDostupan_PreusmjeravaNaDashboardSaGreskom()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student, 100)), baza);

            var result = await controller.RezervisiTermin(99, (TipInstrukcija)0) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.IsNotNull(controller.TempData["Greska"]);
        }

        [TestMethod]
        public async Task RezervisiTermin_Post_NedovoljnoSredstava_PreusmjeravaNaProfilTutora()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviTutora("t1", 30),
                new Termin { id = 1, idTutora = "t1", status = StatusTermina.Slobodan, cijena = 10 });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student, 5)), baza);

            var result = await controller.RezervisiTermin(1, (TipInstrukcija)0) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Profil", result.ActionName);
            Assert.AreEqual("Tutor", result.ControllerName);
            Assert.AreEqual("t1", result.RouteValues!["id"]);
            Assert.IsNotNull(controller.TempData["GreskaNovcenik"]);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(StatusTermina.Slobodan, provjera.Termin.Single().status);
        }

        [TestMethod]
        public async Task RezervisiTermin_Post_DovoljnoSredstava_RezervisePoCijeniTutoraIObavjestavaTutora()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviTutora("t1", 30),
                new Termin { id = 1, idTutora = "t1", status = StatusTermina.Slobodan, cijena = 10, datumIVrijeme = new DateTime(2026, 12, 1, 10, 0, 0) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student, 100)), baza);

            var result = await controller.RezervisiTermin(1, (TipInstrukcija)1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.IsNotNull(controller.TempData["Uspjeh"]);
            using var provjera = new ApplicationDbContext(opcije);
            var termin = provjera.Termin.Single();
            Assert.AreEqual(StatusTermina.Rezervisan, termin.status);
            Assert.AreEqual("s1", termin.idStudenta);
            Assert.AreEqual(30, termin.cijena);
            Assert.AreEqual((TipInstrukcija)1, termin.tipInstrukcija);
            var obavjestenje = provjera.Obavjestenje.Single();
            Assert.AreEqual("t1", obavjestenje.idKorisnika);
            Assert.AreEqual(1, obavjestenje.idTermina);
        }

        [TestMethod]
        public async Task RezervisiTermin_Post_TutorNePostoji_KoristiCijenuTermina()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Termin { id = 1, idTutora = "nepoznat", status = StatusTermina.Slobodan, cijena = 15, datumIVrijeme = new DateTime(2026, 12, 1, 10, 0, 0) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student, 100)), baza);

            var result = await controller.RezervisiTermin(1, (TipInstrukcija)0) as RedirectToActionResult;

            Assert.IsNotNull(result);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(15, provjera.Termin.Single().cijena);
        }

        // ---------- OdaberiTermin ----------

        [TestMethod]
        public async Task OdaberiTermin_KorisnikNijePrijavljen_VracaPristupOdbijen()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.OdaberiTermin("t1") as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ViewName);
        }

        [TestMethod]
        public async Task OdaberiTermin_TutorNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.OdaberiTermin("nepoznat");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task OdaberiTermin_KorisnikNijeTutor_VracaNotFound()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, NapraviKorisnika("x1", Uloga.Student));
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.OdaberiTermin("x1");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task OdaberiTermin_TutorSaTerminima_VracaSamoSlobodneBuduceSortiraneTermine()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("t1", Uloga.Tutor),
                new Predmet { id = 1, naziv = "Matematika" },
                new Termin { id = 1, idTutora = "t1", idPredmeta = 1, status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(3) },
                new Termin { id = 2, idTutora = "t1", idPredmeta = 99, status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(2) },
                new Termin { id = 3, idTutora = "t1", idPredmeta = null, status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(1) },
                new Termin { id = 4, idTutora = "t1", status = StatusTermina.Slobodan, datumIVrijeme = DateTime.Now.AddDays(-1) },
                new Termin { id = 5, idTutora = "t1", status = StatusTermina.Rezervisan, datumIVrijeme = DateTime.Now.AddDays(1) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.OdaberiTermin("t1") as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Termin>;
            Assert.IsNotNull(model);
            Assert.AreEqual(3, model.Count);
            Assert.AreEqual(3, model[0].id);
            Assert.AreEqual(1, model[2].id);
            var predmeti = (Dictionary<int, string>)controller.ViewBag.TerminPredmeti;
            Assert.AreEqual(2, predmeti.Count);
            Assert.AreEqual("Matematika", predmeti[1]);
            Assert.AreEqual("—", predmeti[2]);
        }

        // ---------- PotvrdiPrisustvo ----------

        [TestMethod]
        public async Task PotvrdiPrisustvo_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.PotvrdiPrisustvo(1);

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task PotvrdiPrisustvo_KorisnikNijeStudent_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("t1", Uloga.Tutor)), baza);

            var result = await controller.PotvrdiPrisustvo(1);

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task PotvrdiPrisustvo_TerminNePostoji_VracaJsonSaNeuspjehom()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.PotvrdiPrisustvo(99) as JsonResult;

            Assert.IsNotNull(result);
            var json = UJson(result.Value);
            Assert.IsFalse(json.GetProperty("success").GetBoolean());
            Assert.AreEqual("Termin nije pronađen.", json.GetProperty("poruka").GetString());
        }

        [TestMethod]
        public async Task PotvrdiPrisustvo_TerminDrugogStudenta_VracaJsonSaNeuspjehom()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Termin { id = 1, idStudenta = "drugi", idTutora = "t1", status = StatusTermina.Rezervisan });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.PotvrdiPrisustvo(1) as JsonResult;

            Assert.IsNotNull(result);
            Assert.IsFalse(UJson(result.Value).GetProperty("success").GetBoolean());
        }

        [TestMethod]
        public async Task PotvrdiPrisustvo_TerminNijeRezervisan_VracaJsonSaNeuspjehom()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Termin { id = 1, idStudenta = "s1", idTutora = "t1", status = StatusTermina.Slobodan });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.PotvrdiPrisustvo(1) as JsonResult;

            Assert.IsNotNull(result);
            var json = UJson(result.Value);
            Assert.IsFalse(json.GetProperty("success").GetBoolean());
            Assert.AreEqual("Termin nije u statusu rezervisan.", json.GetProperty("poruka").GetString());
        }

        [TestMethod]
        public async Task PotvrdiPrisustvo_NedovoljnoSredstava_VracaJsonSaNeuspjehom()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Termin { id = 1, idStudenta = "s1", idTutora = "t1", status = StatusTermina.Rezervisan, cijena = 100 });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student, 10)), baza);

            var result = await controller.PotvrdiPrisustvo(1) as JsonResult;

            Assert.IsNotNull(result);
            Assert.IsFalse(UJson(result.Value).GetProperty("success").GetBoolean());
        }

        [TestMethod]
        public async Task PotvrdiPrisustvo_TutorNePostoji_VracaJsonSaNeuspjehom()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Termin { id = 1, idStudenta = "s1", idTutora = "nepoznat", status = StatusTermina.Rezervisan, cijena = 100 });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student, 200)), baza);

            var result = await controller.PotvrdiPrisustvo(1) as JsonResult;

            Assert.IsNotNull(result);
            var json = UJson(result.Value);
            Assert.IsFalse(json.GetProperty("success").GetBoolean());
            Assert.AreEqual("Tutor nije pronađen.", json.GetProperty("poruka").GetString());
        }

        [TestMethod]
        public async Task PotvrdiPrisustvo_SaSystemManagerom_RasporedjujeNovacProvizijuITransakcije()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviTutora("t1", 100, 5, null),
                NapraviKorisnika("m1", Uloga.SystemManager),
                new Termin { id = 1, idStudenta = "s1", idTutora = "t1", status = StatusTermina.Rezervisan, cijena = 100, datumIVrijeme = new DateTime(2026, 11, 20) });
            using var baza = new ApplicationDbContext(opcije);
            var student = NapraviKorisnika("s1", Uloga.Student, 200);
            var controller = NapraviController(UserManagerSaKorisnikom(student), baza);

            var result = await controller.PotvrdiPrisustvo(1) as JsonResult;

            Assert.IsNotNull(result);
            Assert.IsTrue(UJson(result.Value).GetProperty("success").GetBoolean());
            Assert.AreEqual(100, student.StanjeRacuna);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(90, provjera.Users.Single(u => u.Id == "t1").StanjeRacuna);
            Assert.AreEqual(10, provjera.Users.Single(u => u.Id == "m1").StanjeRacuna);
            Assert.AreEqual(1, provjera.Users.Single(u => u.Id == "t1").BrojOdrzanihCasova);
            Assert.AreEqual(StatusTermina.Odrzan, provjera.Termin.Single().status);
            Assert.AreEqual(3, provjera.Transakcija.Count());
            Assert.AreEqual("t1", provjera.Obavjestenje.Single().idKorisnika);
        }

        [TestMethod]
        public async Task PotvrdiPrisustvo_BezSystemManagera_BiljeziSamoDvijeTransakcije()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviTutora("t1", 100, 5, 4),
                new Termin { id = 1, idStudenta = "s1", idTutora = "t1", status = StatusTermina.Rezervisan, cijena = 100, datumIVrijeme = new DateTime(2026, 11, 20) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student, 200)), baza);

            var result = await controller.PotvrdiPrisustvo(1) as JsonResult;

            Assert.IsNotNull(result);
            Assert.IsTrue(UJson(result.Value).GetProperty("success").GetBoolean());
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(2, provjera.Transakcija.Count());
            Assert.AreEqual(5, provjera.Users.Single(u => u.Id == "t1").BrojOdrzanihCasova);
        }

        // ---------- Profil ----------

        [TestMethod]
        public async Task Profil_StudentNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("x")).ReturnsAsync((Korisnik?)null);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Profil("x");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Profil_KorisnikNijeStudent_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("t1"))
                       .ReturnsAsync(NapraviKorisnika("t1", Uloga.Tutor));
            var controller = NapraviController(userManager, baza);

            var result = await controller.Profil("t1");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Profil_PostojeciStudent_VracaModelSaPredmetimaTerminimaIRecenzijama()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("t1", Uloga.Tutor),
                new Predmet { id = 1, naziv = "Fizika" },
                new KorisnikPredmet { idKorisnika = "s1", idPredmeta = 1 },
                new Termin { id = 1, idStudenta = "s1", idTutora = "t1" },
                new Recenzija { idStudenta = "s1", idTutora = "t1", ocjena = 5, komentar = "Super", datumRecenzije = new DateTime(2026, 11, 1) },
                new Recenzija { idStudenta = "s1", idTutora = "nepoznat", ocjena = 3, komentar = "Ok", datumRecenzije = new DateTime(2026, 11, 2) });
            using var baza = new ApplicationDbContext(opcije);
            var student = NapraviKorisnika("s1", Uloga.Student);
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.FindByIdAsync("s1")).ReturnsAsync(student);
            var controller = NapraviController(userManager, baza);

            var result = await controller.Profil("s1") as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as StudentProfilViewModel;
            Assert.IsNotNull(model);
            Assert.AreSame(student, model.Student);
            Assert.AreEqual(1, model.Predmeti.Count());
            Assert.AreEqual(1, model.Termini.Count());
            var recenzije = model.Recenzije.ToList();
            Assert.AreEqual(2, recenzije.Count);
            Assert.AreEqual("Tutor", recenzije[0].ImeStudenta);
            Assert.AreEqual("Imet1", recenzije[1].ImeStudenta);
        }

        // ---------- OstaviRecenziju ----------

        [TestMethod]
        public async Task OstaviRecenziju_KorisnikNijePrijavljen_VracaPristupOdbijen()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.OstaviRecenziju("t1", 5, "Super") as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ViewName);
        }

        [TestMethod]
        public async Task OstaviRecenziju_KorisnikNijeStudent_VracaPristupOdbijen()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("t2", Uloga.Tutor)), baza);

            var result = await controller.OstaviRecenziju("t1", 5, "Super") as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ViewName);
        }

        [TestMethod]
        public async Task OstaviRecenziju_BezOdrzanogCasa_PreusmjeravaSaGreskom()
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.OstaviRecenziju("t1", 5, "Super") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Profil", result.ActionName);
            Assert.AreEqual("Tutor", result.ControllerName);
            Assert.IsNotNull(controller.TempData["ErrorPoruka"]);
            Assert.AreEqual(0, baza.Recenzija.Count());
        }

        [TestMethod]
        public async Task OstaviRecenziju_TutorBezPrethodnihRecenzija_SpremaRecenziju()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviTutora("t1"),
                new Termin { id = 1, idStudenta = "s1", idTutora = "t1", status = StatusTermina.Odrzan });
            using var baza = new ApplicationDbContext(opcije);
            var userManager = UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student));
            var controller = NapraviController(userManager, baza);

            var result = await controller.OstaviRecenziju("t1", 5, null!) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Profil", result.ActionName);
            Assert.IsNotNull(controller.TempData["Uspjeh"]);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Once);
            using var provjera = new ApplicationDbContext(opcije);
            var recenzija = provjera.Recenzija.Single();
            Assert.AreEqual("", recenzija.komentar);
            Assert.AreEqual("s1", recenzija.idStudenta);
        }

        [TestMethod]
        public async Task OstaviRecenziju_TutorSaPrethodnomRecenzijom_SpremaNovuRecenziju()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviTutora("t1"),
                new Termin { id = 1, idStudenta = "s1", idTutora = "t1", status = StatusTermina.Odrzan },
                new Recenzija { idStudenta = "drugi", idTutora = "t1", ocjena = 4, komentar = "Dobro", datumRecenzije = new DateTime(2026, 11, 1) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.OstaviRecenziju("t1", 5, "Super") as RedirectToActionResult;

            Assert.IsNotNull(result);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(2, provjera.Recenzija.Count());
        }

        [TestMethod]
        public async Task OstaviRecenziju_TutorNePostoji_SpremaRecenzijuBezAzuriranjaTutora()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Termin { id = 1, idStudenta = "s1", idTutora = "nepoznat", status = StatusTermina.Odrzan });
            using var baza = new ApplicationDbContext(opcije);
            var userManager = UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student));
            var controller = NapraviController(userManager, baza);

            var result = await controller.OstaviRecenziju("nepoznat", 4, "Ok") as RedirectToActionResult;

            Assert.IsNotNull(result);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(1, provjera.Recenzija.Count());
        }

        // ---------- PrijaviTutora ----------

        [TestMethod]
        public async Task PrijaviTutora_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.PrijaviTutora("t1", "Naslov", "Opis");

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task PrijaviTutora_KorisnikNijeStudent_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("t2", Uloga.Tutor)), baza);

            var result = await controller.PrijaviTutora("t1", "Naslov", "Opis");

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task PrijaviTutora_TutorNePostoji_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student));
            userManager.Setup(m => m.FindByIdAsync("nepoznat")).ReturnsAsync((Korisnik?)null);
            var controller = NapraviController(userManager, baza);

            var result = await controller.PrijaviTutora("nepoznat", "Naslov", "Opis");

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task PrijaviTutora_PostojeciTutor_SpremaPrijavuIPreusmjerava()
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var userManager = UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student));
            userManager.Setup(m => m.FindByIdAsync("t1")).ReturnsAsync(NapraviKorisnika("t1", Uloga.Tutor));
            var controller = NapraviController(userManager, baza);

            var result = await controller.PrijaviTutora("t1", "Naslov", "Neprimjereno ponasanje") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Profil", result.ActionName);
            Assert.AreEqual("Tutor", result.ControllerName);
            Assert.IsNotNull(controller.TempData["Uspjeh"]);
            using var provjera = new ApplicationDbContext(opcije);
            var prijava = provjera.Prijava.Single();
            Assert.AreEqual("Naslov", prijava.Naslov);
            Assert.AreEqual("s1", prijava.IdPrijavitelja);
            Assert.IsTrue(prijava.Opis.Contains("Neprimjereno ponasanje"));
        }

        // ---------- PrijaviRecenziju ----------

        [TestMethod]
        public async Task PrijaviRecenziju_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.PrijaviRecenziju(1, "t1", "Naslov", "Opis");

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task PrijaviRecenziju_KorisnikNijeTutor_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.PrijaviRecenziju(1, "t1", "Naslov", "Opis");

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task PrijaviRecenziju_Tutor_SpremaPrijavuIPreusmjeravaNaRecenzije()
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("t1", Uloga.Tutor)), baza);

            var result = await controller.PrijaviRecenziju(7, "t1", "Naslov", "Laz") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Recenzije", result.ActionName);
            Assert.AreEqual("Tutor", result.ControllerName);
            Assert.IsNotNull(controller.TempData["Uspjeh"]);
            using var provjera = new ApplicationDbContext(opcije);
            var prijava = provjera.Prijava.Single();
            Assert.AreEqual("t1", prijava.IdPrijavitelja);
            Assert.IsTrue(prijava.Opis.Contains("ID: 7"));
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
        public async Task Obavjestenja_KorisnikNijeStudent_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("t1", Uloga.Tutor)), baza);

            var result = await controller.Obavjestenja() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task Obavjestenja_StudentSaObavjestenjima_VracaIhIOznacavaProcitanim()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, NapraviObavjestenja("s1", 3));
            Ubaci(opcije, NapraviObavjestenja("drugi", 1));
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.Obavjestenja() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Obavjestenje>;
            Assert.IsNotNull(model);
            Assert.AreEqual(3, model.Count);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(3, provjera.Obavjestenje.Count(o => o.procitano));
        }

        // ---------- DohvatiObavjestenja ----------

        [TestMethod]
        public async Task DohvatiObavjestenja_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.DohvatiObavjestenja();

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task DohvatiObavjestenja_VisePrijavaOdDeset_VracaNajvise10()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, NapraviObavjestenja("s1", 12));
            Ubaci(opcije, NapraviObavjestenja("drugi", 2));
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.DohvatiObavjestenja() as JsonResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(10, UJson(result.Value).GetArrayLength());
        }

        // ---------- BrojObavjestenja ----------

        [TestMethod]
        public async Task BrojObavjestenja_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.BrojObavjestenja();

            Assert.IsInstanceOfType<UnauthorizedResult>(result);
        }

        [TestMethod]
        public async Task BrojObavjestenja_ImaProcitanihINeprocitanih_VracaBrojNeprocitanih()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, NapraviObavjestenja("s1", 2));
            Ubaci(opcije, NapraviObavjestenja("s1", 1, true));
            Ubaci(opcije, NapraviObavjestenja("drugi", 5));
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.BrojObavjestenja() as JsonResult;

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
            Ubaci(opcije, new Obavjestenje { id = 1, idKorisnika = "s1", naslov = "n", sadrzaj = "s", procitano = false });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

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
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

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
            Ubaci(opcije, NapraviObavjestenja("s1", 3));
            Ubaci(opcije, NapraviObavjestenja("drugi", 2));
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviKorisnika("s1", Uloga.Student)), baza);

            var result = await controller.OznaciSveProcitanim();

            Assert.IsInstanceOfType<OkResult>(result);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(3, provjera.Obavjestenje.Count(o => o.procitano));
            Assert.AreEqual(2, provjera.Obavjestenje.Count(o => !o.procitano));
        }
    }
}