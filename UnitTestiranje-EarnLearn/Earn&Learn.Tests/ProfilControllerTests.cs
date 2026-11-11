using Earn_Learn.Controllers;
using Earn_Learn.Data;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace Earn_Learn.Tests
{
    [TestClass]
    public class ProfilControllerTests
    {
        private const long PetMb = 5 * 1024 * 1024;

        // Vrijednost enuma koja sigurno nije "Odrzan" (samo Odrzan je bitan za kontroler)
        private const StatusTermina NijeOdrzan = (StatusTermina)99;

        // Svaki test dobija svoj privremeni WebRootPath (MSTest pravi novu instancu klase za svaki test)
        private readonly string _webRoot =
            Path.Combine(Path.GetTempPath(), "earnlearn_tests_" + Guid.NewGuid());

        [TestCleanup]
        public void ObrisiPrivremeniFolder()
        {
            if (Directory.Exists(_webRoot))
                Directory.Delete(_webRoot, true);
        }

        // ---------- Pomoćne metode ----------

        private static Mock<UserManager<Korisnik>> NapraviUserManagerMock()
        {
            var store = new Mock<IUserStore<Korisnik>>();
            var userManager = new Mock<UserManager<Korisnik>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
            userManager.Setup(m => m.UpdateAsync(It.IsAny<Korisnik>()))
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

        private static Mock<SignInManager<Korisnik>> NapraviSignInManagerMock()
        {
            var signInManager = new Mock<SignInManager<Korisnik>>(
                NapraviUserManagerMock().Object,
                Mock.Of<IHttpContextAccessor>(),
                Mock.Of<IUserClaimsPrincipalFactory<Korisnik>>(),
                null!, null!, null!, null!);
            signInManager.Setup(m => m.RefreshSignInAsync(It.IsAny<Korisnik>()))
                         .Returns(Task.CompletedTask);
            return signInManager;
        }

        private static IWebHostEnvironment NapraviOkolinu(string? webRoot)
        {
            var okolina = new Mock<IWebHostEnvironment>();
            okolina.SetupGet(o => o.WebRootPath).Returns(webRoot!);
            return okolina.Object;
        }

        private ProfilController NapraviController(
            Mock<UserManager<Korisnik>> userManager,
            ApplicationDbContext baza,
            Mock<SignInManager<Korisnik>>? signInManager = null,
            IWebHostEnvironment? okolina = null)
        {
            var httpContext = new DefaultHttpContext();
            return new ProfilController(
                userManager.Object,
                (signInManager ?? NapraviSignInManagerMock()).Object,
                baza,
                okolina ?? NapraviOkolinu(_webRoot))
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

        private static Korisnik NapraviStudenta() => NapraviKorisnika("s1", Uloga.Student);

        private static Korisnik NapraviTutora()
        {
            var tutor = NapraviKorisnika("t1", Uloga.Tutor);
            tutor.VerifikovanTutor = true;
            return tutor;
        }

        private static IFormFile NapraviFajl(string contentType, long velicina, string imeFajla = "prilog.pdf")
        {
            var fajl = new Mock<IFormFile>();
            fajl.SetupGet(f => f.ContentType).Returns(contentType);
            fajl.SetupGet(f => f.Length).Returns(velicina);
            fajl.SetupGet(f => f.FileName).Returns(imeFajla);
            fajl.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            return fajl.Object;
        }

        private static IFormFile NapraviFajlKojiBacaIzuzetak()
        {
            var fajl = new Mock<IFormFile>();
            fajl.SetupGet(f => f.ContentType).Returns("application/pdf");
            fajl.SetupGet(f => f.Length).Returns(100);
            fajl.SetupGet(f => f.FileName).Returns("prilog.pdf");
            fajl.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("Disk je pun."));
            return fajl.Object;
        }

        private string[] SpremljeniFajlovi() =>
            Directory.GetFiles(Path.Combine(_webRoot, "uploads", "prilozi"));

        // ---------- Index ----------

        [TestMethod]
        public async Task Index_KorisnikNijePrijavljen_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.Index() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task Index_Tutor_PostavljaCijenuIOcjenuUViewBag()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var tutor = NapraviTutora();
            tutor.CijenaPoSatu = 20.5;
            tutor.ProsjecnaOcjena = 4.5;
            var controller = NapraviController(UserManagerSaKorisnikom(tutor), baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(20.5, (double)controller.ViewBag.CijenaPoSatu);
            Assert.AreEqual(4.5, (double)controller.ViewBag.ProsjecnaOcjena);
        }

        [TestMethod]
        public async Task Index_TutorBezCijeneIOcjene_ViewBagSadrziNule()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var tutor = NapraviTutora();
            tutor.CijenaPoSatu = null;
            tutor.ProsjecnaOcjena = null;
            var controller = NapraviController(UserManagerSaKorisnikom(tutor), baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(0.0, (double)controller.ViewBag.CijenaPoSatu);
            Assert.AreEqual(0.0, (double)controller.ViewBag.ProsjecnaOcjena);
        }

        [TestMethod]
        public async Task Index_Student_NePostavljaCijenuIOcjenuUViewBag()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviStudenta()), baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            Assert.IsNull((object?)controller.ViewBag.CijenaPoSatu);
            Assert.IsNull((object?)controller.ViewBag.ProsjecnaOcjena);
        }

        [TestMethod]
        public async Task Index_PrijavljenKorisnik_VracaModelSaOsnovnimPodacima()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var student = NapraviKorisnika("s1", Uloga.Student, "Amar", "Hadzic", "amar@test.ba");
            student.GodinaStudija = 3;
            var controller = NapraviController(UserManagerSaKorisnikom(student), baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as ProfilViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual("Amar", model.Ime);
            Assert.AreEqual("Hadzic", model.Prezime);
            Assert.AreEqual("amar@test.ba", model.Email);
            Assert.AreEqual(Uloga.Student, model.Uloga);
            Assert.AreEqual(student.BrojIndeksa, model.BrojIndeksa);
            Assert.AreEqual(3, model.GodinaStudija);
        }

        [TestMethod]
        public async Task Index_EmailJeNull_ModelSadrziPrazanEmail()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var student = NapraviStudenta();
            student.Email = null;
            var controller = NapraviController(UserManagerSaKorisnikom(student), baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as ProfilViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual("", model.Email);
        }

        [TestMethod]
        public async Task Index_BezRecenzijaPredmetaITermina_VracaModelSaNulama()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviStudenta()), baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as ProfilViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(0, model.BrojRecenzija);
            Assert.AreEqual(0, model.BrojCasova);
            Assert.AreEqual(0, model.Predmeti.Count());
            Assert.AreEqual(0, model.PredmetiObjekti.Count());
            Assert.AreEqual(0, model.Recenzije.Count());
            Assert.AreEqual(0, model.SviPredmeti.Count());
        }

        [TestMethod]
        public async Task Index_KorisnikSaPredmetima_VracaSamoNjegovePredmete()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Predmet { id = 1, naziv = "Matematika" },
                new Predmet { id = 2, naziv = "Fizika" },
                new Predmet { id = 3, naziv = "Hemija" },
                new KorisnikPredmet { idKorisnika = "s1", idPredmeta = 1 },
                new KorisnikPredmet { idKorisnika = "s1", idPredmeta = 2 },
                new KorisnikPredmet { idKorisnika = "drugi", idPredmeta = 3 });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviStudenta()), baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as ProfilViewModel;
            Assert.IsNotNull(model);
            CollectionAssert.AreEquivalent(new[] { "Matematika", "Fizika" }, model.Predmeti.ToList());
            Assert.AreEqual(2, model.PredmetiObjekti.Count());
        }

        [TestMethod]
        public async Task Index_SviPredmeti_SortiraniPoNazivu()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Predmet { id = 1, naziv = "Matematika" },
                new Predmet { id = 2, naziv = "Fizika" },
                new Predmet { id = 3, naziv = "Hemija" });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviStudenta()), baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as ProfilViewModel;
            Assert.IsNotNull(model);
            CollectionAssert.AreEqual(
                new[] { "Fizika", "Hemija", "Matematika" },
                model.SviPredmeti.Select(p => p.naziv).ToList());
        }

        [TestMethod]
        public async Task Index_Tutor_VracaRecenzijeOstavljeneTutoruOdNajnovije()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("s1", Uloga.Student, "Amar", "Hadzic"),
                NapraviKorisnika("s2", Uloga.Student, "Lejla", "Kovac"),
                new Recenzija { idTutora = "t1", idStudenta = "s1", datumRecenzije = new DateTime(2026, 11, 1) },
                new Recenzija { idTutora = "t1", idStudenta = "s2", datumRecenzije = new DateTime(2026, 11, 5) },
                new Recenzija { idTutora = "t2", idStudenta = "s1", datumRecenzije = new DateTime(2026, 11, 3) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora()), baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as ProfilViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.BrojRecenzija);
            var recenzije = model.Recenzije.ToList();
            Assert.AreEqual(2, recenzije.Count);
            Assert.AreEqual("Lejla Kovac", recenzije[0].ImeStudenta);
            Assert.AreEqual("Amar Hadzic", recenzije[1].ImeStudenta);
        }

        [TestMethod]
        public async Task Index_Student_VracaRecenzijeKojeJeOstavioTutorimaOdNajnovije()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                NapraviKorisnika("t1", Uloga.Tutor, "Emir", "Selimovic"),
                NapraviKorisnika("t2", Uloga.Tutor, "Sara", "Begic"),
                new Recenzija { idTutora = "t1", idStudenta = "s1", datumRecenzije = new DateTime(2026, 11, 1) },
                new Recenzija { idTutora = "t2", idStudenta = "s1", datumRecenzije = new DateTime(2026, 11, 5) },
                new Recenzija { idTutora = "t1", idStudenta = "s2", datumRecenzije = new DateTime(2026, 11, 3) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviStudenta()), baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as ProfilViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.BrojRecenzija);
            var recenzije = model.Recenzije.ToList();
            Assert.AreEqual("Sara Begic", recenzije[0].ImeStudenta);
            Assert.AreEqual("Emir Selimovic", recenzije[1].ImeStudenta);
        }

        [TestMethod]
        public async Task Index_TutorRecenzijaStudentaKojiNePostoji_ImeStudentaJeSamoRazmak()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Recenzija { idTutora = "t1", idStudenta = "nepostojeci", datumRecenzije = new DateTime(2026, 11, 1) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora()), baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as ProfilViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(" ", model.Recenzije.Single().ImeStudenta);
        }

        [TestMethod]
        public async Task Index_StudentRecenzijaTutoraKojiNePostoji_ImeTutoraJeSamoRazmak()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Recenzija { idTutora = "nepostojeci", idStudenta = "s1", datumRecenzije = new DateTime(2026, 11, 1) });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviStudenta()), baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as ProfilViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(" ", model.Recenzije.Single().ImeStudenta);
        }

        [TestMethod]
        public async Task Index_Tutor_BrojCasovaBrojiSamoOdrzaneTermineTutora()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Termin { id = 1, idTutora = "t1", idStudenta = "s1", datumIVrijeme = DateTime.Now, status = StatusTermina.Odrzan },
                new Termin { id = 2, idTutora = "t1", idStudenta = "s2", datumIVrijeme = DateTime.Now, status = StatusTermina.Odrzan },
                new Termin { id = 3, idTutora = "t1", idStudenta = "s1", datumIVrijeme = DateTime.Now, status = NijeOdrzan },
                new Termin { id = 4, idTutora = "t2", idStudenta = "s1", datumIVrijeme = DateTime.Now, status = StatusTermina.Odrzan });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora()), baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as ProfilViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.BrojCasova);
        }

        [TestMethod]
        public async Task Index_Student_BrojCasovaBrojiSamoOdrzaneTermineStudenta()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Termin { id = 1, idTutora = "t1", idStudenta = "s1", datumIVrijeme = DateTime.Now, status = StatusTermina.Odrzan },
                new Termin { id = 2, idTutora = "t1", idStudenta = "s1", datumIVrijeme = DateTime.Now, status = NijeOdrzan },
                new Termin { id = 3, idTutora = "t1", idStudenta = "s2", datumIVrijeme = DateTime.Now, status = StatusTermina.Odrzan });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviStudenta()), baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as ProfilViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(1, model.BrojCasova);
        }

        [TestMethod]
        public async Task Index_UserManagerBacaIzuzetak_PropagiraIzuzetak()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                       .ThrowsAsync(new InvalidOperationException("Baza nedostupna"));
            var controller = NapraviController(userManager, baza);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => controller.Index());
        }

        // ---------- UrediGodinuStudija ----------

        private record GodinaPodaci(int Godina);

        public static IEnumerable<object[]> NeispravneGodineIzJsona()
        {
            var putanja = Path.Combine(AppContext.BaseDirectory, "TestData", "profil_neispravne_godine.json");
            var podaci = JsonSerializer.Deserialize<List<GodinaPodaci>>(
                File.ReadAllText(putanja),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            return podaci.Select(p => new object[] { p.Godina });
        }

        [TestMethod]
        public async Task UrediGodinuStudija_KorisnikNijePrijavljen_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.UrediGodinuStudija(2) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        [DynamicData(nameof(NeispravneGodineIzJsona))]
        public async Task UrediGodinuStudija_GodinaVanOpsega_VracaGreskuBezSnimanja(int godina)
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var student = NapraviStudenta();
            student.GodinaStudija = 2;
            var userManager = UserManagerSaKorisnikom(student);
            var controller = NapraviController(userManager, baza);

            var result = await controller.UrediGodinuStudija(godina) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.IsNull(result.ControllerName);
            Assert.AreEqual("Godina studija mora biti između 1 i 6.", controller.TempData["Greska"]);
            Assert.AreEqual(2, student.GodinaStudija);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(3)]
        [DataRow(6)]
        public async Task UrediGodinuStudija_GodinaUOpsegu_AzuriraGodinuIPreusmjerava(int godina)
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var student = NapraviStudenta();
            var userManager = UserManagerSaKorisnikom(student);
            var controller = NapraviController(userManager, baza);

            var result = await controller.UrediGodinuStudija(godina) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual(godina, student.GodinaStudija);
            Assert.AreEqual("Godina studija uspješno ažurirana.", controller.TempData["Uspjeh"]);
            userManager.Verify(m => m.UpdateAsync(student), Times.Once);
        }

        [TestMethod]
        public async Task UrediGodinuStudija_UpdateBacaIzuzetak_PropagiraIzuzetak()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = UserManagerSaKorisnikom(NapraviStudenta());
            userManager.Setup(m => m.UpdateAsync(It.IsAny<Korisnik>()))
                       .ThrowsAsync(new InvalidOperationException("Baza nedostupna"));
            var controller = NapraviController(userManager, baza);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => controller.UrediGodinuStudija(2));
        }

        // ---------- PostaniTutor ----------

        [TestMethod]
        public async Task PostaniTutor_KorisnikNijePrijavljen_PreusmjeravaNaIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.PostaniTutor(new List<int> { 1 }, null) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
        }

        [TestMethod]
        public async Task PostaniTutor_KorisnikJeVecTutor_PreusmjeravaNaIndexBezIzmjena()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = UserManagerSaKorisnikom(NapraviTutora());
            var controller = NapraviController(userManager, baza);

            var result = await controller.PostaniTutor(new List<int> { 1 }, null) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task PostaniTutor_PredmetiSuNull_VracaGresku()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var student = NapraviStudenta();
            var userManager = UserManagerSaKorisnikom(student);
            var controller = NapraviController(userManager, baza);

            var result = await controller.PostaniTutor(null!, null) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual("Morate odabrati barem jedan predmet.", controller.TempData["TutorGreska"]);
            Assert.AreEqual(Uloga.Student, student.Uloga);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task PostaniTutor_PrazanSpisakPredmeta_VracaGresku()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var student = NapraviStudenta();
            var userManager = UserManagerSaKorisnikom(student);
            var controller = NapraviController(userManager, baza);

            var result = await controller.PostaniTutor(new List<int>(), null) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual("Morate odabrati barem jedan predmet.", controller.TempData["TutorGreska"]);
            Assert.AreEqual(Uloga.Student, student.Uloga);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task PostaniTutor_BezPriloga_PostajeNeverifikovanTutorSaPredmetimaNaCekanju()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var student = NapraviStudenta();
            student.PrilogOcjene = "/stari.pdf";
            var userManager = UserManagerSaKorisnikom(student);
            var signInManager = NapraviSignInManagerMock();
            var controller = NapraviController(userManager, baza, signInManager);

            var result = await controller.PostaniTutor(new List<int> { 1, 2 }, null) as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/Tutor/Dashboard", result.Url);
            Assert.AreEqual(Uloga.Tutor, student.Uloga);
            Assert.AreEqual(false, student.VerifikovanTutor);
            Assert.AreEqual("[1,2]", student.PredmetiNaCekanjuJson);
            Assert.AreEqual("/stari.pdf", student.PrilogOcjene);
            Assert.AreEqual("Uspješno ste postali tutor!", controller.TempData["TutorUspjeh"]);
            userManager.Verify(m => m.UpdateAsync(student), Times.Once);
            signInManager.Verify(m => m.RefreshSignInAsync(student), Times.Once);
        }

        [TestMethod]
        public async Task PostaniTutor_PrazanPrilog_IgnorisePrilogINeSnimaFajl()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var student = NapraviStudenta();
            student.PrilogOcjene = "/stari.pdf";
            var controller = NapraviController(UserManagerSaKorisnikom(student), baza);

            var result = await controller.PostaniTutor(
                new List<int> { 1 }, NapraviFajl("application/pdf", 0)) as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/stari.pdf", student.PrilogOcjene);
            Assert.IsFalse(Directory.Exists(_webRoot));
        }

        [TestMethod]
        [DataRow("image/jpeg", "ocjene.jpg", ".jpg")]
        [DataRow("image/png", "ocjene.png", ".png")]
        [DataRow("image/gif", "ocjene.gif", ".gif")]
        [DataRow("image/webp", "ocjene.webp", ".webp")]
        [DataRow("application/pdf", "ocjene.pdf", ".pdf")]
        public async Task PostaniTutor_DozvoljenTipPriloga_SnimaFajlIPostavljaPutanju(
            string contentType, string imeFajla, string ekstenzija)
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var student = NapraviStudenta();
            var controller = NapraviController(UserManagerSaKorisnikom(student), baza);

            var result = await controller.PostaniTutor(
                new List<int> { 1 }, NapraviFajl(contentType, 1024, imeFajla)) as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(student.PrilogOcjene);
            StringAssert.StartsWith(student.PrilogOcjene, "/uploads/prilozi/");
            StringAssert.EndsWith(student.PrilogOcjene, ekstenzija);
            Assert.AreEqual(1, SpremljeniFajlovi().Length);
        }

        [TestMethod]
        [DataRow("text/plain")]
        [DataRow("application/zip")]
        [DataRow("image/svg+xml")]
        public async Task PostaniTutor_NedozvoljenTipPriloga_VracaGreskuBezSnimanja(string contentType)
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var student = NapraviStudenta();
            var userManager = UserManagerSaKorisnikom(student);
            var controller = NapraviController(userManager, baza);

            var result = await controller.PostaniTutor(
                new List<int> { 1 }, NapraviFajl(contentType, 1024)) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual("Dozvoljeni formati su JPG, PNG i PDF.", controller.TempData["TutorGreska"]);
            Assert.AreEqual(Uloga.Student, student.Uloga);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task PostaniTutor_PrilogVeciOdPetMb_VracaGreskuBezSnimanja()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var student = NapraviStudenta();
            var userManager = UserManagerSaKorisnikom(student);
            var controller = NapraviController(userManager, baza);

            var result = await controller.PostaniTutor(
                new List<int> { 1 }, NapraviFajl("application/pdf", PetMb + 1)) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual("Prilog ne smije biti veći od 5MB.", controller.TempData["TutorGreska"]);
            Assert.AreEqual(Uloga.Student, student.Uloga);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task PostaniTutor_PrilogTacnoPetMb_PrihvataPrilog()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var student = NapraviStudenta();
            var controller = NapraviController(UserManagerSaKorisnikom(student), baza);

            var result = await controller.PostaniTutor(
                new List<int> { 1 }, NapraviFajl("application/pdf", PetMb)) as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(Uloga.Tutor, student.Uloga);
            Assert.IsNotNull(student.PrilogOcjene);
        }

        [TestMethod]
        public async Task PostaniTutor_KopiranjeFajlaBacaIOException_PropagiraIzuzetakBezSnimanja()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = UserManagerSaKorisnikom(NapraviStudenta());
            var controller = NapraviController(userManager, baza);

            await Assert.ThrowsExactlyAsync<IOException>(
                () => controller.PostaniTutor(new List<int> { 1 }, NapraviFajlKojiBacaIzuzetak()));
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task PostaniTutor_WebRootJeNull_BacaArgumentNullException()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(
                UserManagerSaKorisnikom(NapraviStudenta()), baza, null, NapraviOkolinu(null));

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                () => controller.PostaniTutor(new List<int> { 1 }, NapraviFajl("application/pdf", 1024)));
        }

        // ---------- UrediCijenu ----------

        public static IEnumerable<object[]> CijeneIzCsv()
        {
            var putanja = Path.Combine(AppContext.BaseDirectory, "TestData", "profil_cijene.csv");
            return File.ReadAllLines(putanja)
                .Skip(1)
                .Select(red => new object[] { double.Parse(red, CultureInfo.InvariantCulture) });
        }

        [TestMethod]
        public async Task UrediCijenu_KorisnikNijePrijavljen_PreusmjeravaNaHomeIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.UrediCijenu(10) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task UrediCijenu_KorisnikNijeTutor_PreusmjeravaNaHomeIndexBezIzmjena()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = UserManagerSaKorisnikom(NapraviStudenta());
            var controller = NapraviController(userManager, baza);

            var result = await controller.UrediCijenu(10) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Home", result.ControllerName);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        [DataRow(-0.01)]
        [DataRow(-1.0)]
        [DataRow(-250.5)]
        public async Task UrediCijenu_NegativnaCijena_VracaGreskuBezSnimanja(double cijena)
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var tutor = NapraviTutora();
            tutor.CijenaPoSatu = 15;
            var userManager = UserManagerSaKorisnikom(tutor);
            var controller = NapraviController(userManager, baza);

            var result = await controller.UrediCijenu(cijena) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.IsNull(result.ControllerName);
            Assert.AreEqual("Cijena ne može biti negativna.", controller.TempData["Greska"]);
            Assert.AreEqual(15.0, tutor.CijenaPoSatu);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        [DynamicData(nameof(CijeneIzCsv))]
        public async Task UrediCijenu_ValidnaCijena_AzuriraCijenuIPreusmjerava(double cijena)
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var tutor = NapraviTutora();
            var userManager = UserManagerSaKorisnikom(tutor);
            var controller = NapraviController(userManager, baza);

            var result = await controller.UrediCijenu(cijena) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual(cijena, tutor.CijenaPoSatu);
            StringAssert.EndsWith((string)controller.TempData["Uspjeh"]!, "KM/h.");
            userManager.Verify(m => m.UpdateAsync(tutor), Times.Once);
        }

        [TestMethod]
        public async Task UrediCijenu_UpdateBacaIzuzetak_PropagiraIzuzetak()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = UserManagerSaKorisnikom(NapraviTutora());
            userManager.Setup(m => m.UpdateAsync(It.IsAny<Korisnik>()))
                       .ThrowsAsync(new InvalidOperationException("Baza nedostupna"));
            var controller = NapraviController(userManager, baza);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => controller.UrediCijenu(10));
        }

        // ---------- DodajPredmet ----------

        [TestMethod]
        public async Task DodajPredmet_KorisnikNijePrijavljen_PreusmjeravaNaIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.DodajPredmet(1, null) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
        }

        [TestMethod]
        public async Task DodajPredmet_KorisnikNijeTutor_PreusmjeravaNaIndexBezIzmjena()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = UserManagerSaKorisnikom(NapraviStudenta());
            var controller = NapraviController(userManager, baza);

            var result = await controller.DodajPredmet(1, null) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task DodajPredmet_PredmetNePostoji_VracaGresku()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var userManager = UserManagerSaKorisnikom(NapraviTutora());
            var controller = NapraviController(userManager, baza);

            var result = await controller.DodajPredmet(99, null) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual("Predmet nije pronađen.", controller.TempData["Greska"]);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task DodajPredmet_TutorVecPredajePredmet_VracaGresku()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Predmet { id = 1, naziv = "Matematika" },
                new KorisnikPredmet { idKorisnika = "t1", idPredmeta = 1 });
            using var baza = new ApplicationDbContext(opcije);
            var userManager = UserManagerSaKorisnikom(NapraviTutora());
            var controller = NapraviController(userManager, baza);

            var result = await controller.DodajPredmet(1, null) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual("Već predajete ovaj predmet.", controller.TempData["Greska"]);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task DodajPredmet_PredmetPredajeDrugiTutor_DozvoljavaZahtjev()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new Predmet { id = 1, naziv = "Matematika" },
                new KorisnikPredmet { idKorisnika = "t2", idPredmeta = 1 });
            using var baza = new ApplicationDbContext(opcije);
            var tutor = NapraviTutora();
            var controller = NapraviController(UserManagerSaKorisnikom(tutor), baza);

            var result = await controller.DodajPredmet(1, null) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(1, tutor.PredmetNaCekanjaId);
        }

        [TestMethod]
        public async Task DodajPredmet_BezPriloga_PostavljaPredmetNaCekanjuINistaVerifikaciju()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Predmet { id = 1, naziv = "Matematika" });
            using var baza = new ApplicationDbContext(opcije);
            var tutor = NapraviTutora();
            tutor.PrilogOcjene = "/stari.pdf";
            var userManager = UserManagerSaKorisnikom(tutor);
            var controller = NapraviController(userManager, baza);

            var result = await controller.DodajPredmet(1, null) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual(false, tutor.VerifikovanTutor);
            Assert.AreEqual(1, tutor.PredmetNaCekanjaId);
            Assert.AreEqual("/stari.pdf", tutor.PrilogOcjene);
            StringAssert.Contains((string)controller.TempData["Uspjeh"]!, "Matematika");
            userManager.Verify(m => m.UpdateAsync(tutor), Times.Once);
        }

        [TestMethod]
        public async Task DodajPredmet_PrazanPrilog_IgnorisePrilog()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Predmet { id = 1, naziv = "Matematika" });
            using var baza = new ApplicationDbContext(opcije);
            var tutor = NapraviTutora();
            tutor.PrilogOcjene = "/stari.pdf";
            var controller = NapraviController(UserManagerSaKorisnikom(tutor), baza);

            var result = await controller.DodajPredmet(1, NapraviFajl("application/pdf", 0)) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/stari.pdf", tutor.PrilogOcjene);
            Assert.IsFalse(Directory.Exists(_webRoot));
        }

        [TestMethod]
        [DataRow("image/jpeg", "dokaz.jpg", ".jpg")]
        [DataRow("image/png", "dokaz.png", ".png")]
        [DataRow("image/gif", "dokaz.gif", ".gif")]
        [DataRow("image/webp", "dokaz.webp", ".webp")]
        [DataRow("application/pdf", "dokaz.pdf", ".pdf")]
        public async Task DodajPredmet_DozvoljenTipPriloga_SnimaFajlIPostavljaPutanju(
            string contentType, string imeFajla, string ekstenzija)
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Predmet { id = 1, naziv = "Matematika" });
            using var baza = new ApplicationDbContext(opcije);
            var tutor = NapraviTutora();
            var controller = NapraviController(UserManagerSaKorisnikom(tutor), baza);

            var result = await controller.DodajPredmet(
                1, NapraviFajl(contentType, 1024, imeFajla)) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.IsNotNull(tutor.PrilogOcjene);
            StringAssert.StartsWith(tutor.PrilogOcjene, "/uploads/prilozi/");
            StringAssert.EndsWith(tutor.PrilogOcjene, ekstenzija);
            Assert.AreEqual(1, SpremljeniFajlovi().Length);
        }

        [TestMethod]
        [DataRow("text/plain")]
        [DataRow("application/zip")]
        [DataRow("image/svg+xml")]
        public async Task DodajPredmet_NedozvoljenTipPriloga_VracaGreskuBezSnimanja(string contentType)
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Predmet { id = 1, naziv = "Matematika" });
            using var baza = new ApplicationDbContext(opcije);
            var userManager = UserManagerSaKorisnikom(NapraviTutora());
            var controller = NapraviController(userManager, baza);

            var result = await controller.DodajPredmet(
                1, NapraviFajl(contentType, 1024)) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dozvoljeni formati su JPG, PNG i PDF.", controller.TempData["Greska"]);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task DodajPredmet_PrilogVeciOdPetMb_VracaGreskuBezSnimanja()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Predmet { id = 1, naziv = "Matematika" });
            using var baza = new ApplicationDbContext(opcije);
            var userManager = UserManagerSaKorisnikom(NapraviTutora());
            var controller = NapraviController(userManager, baza);

            var result = await controller.DodajPredmet(
                1, NapraviFajl("application/pdf", PetMb + 1)) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Prilog ne smije biti veći od 5MB.", controller.TempData["Greska"]);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task DodajPredmet_PrilogTacnoPetMb_PrihvataPrilog()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Predmet { id = 1, naziv = "Matematika" });
            using var baza = new ApplicationDbContext(opcije);
            var tutor = NapraviTutora();
            var controller = NapraviController(UserManagerSaKorisnikom(tutor), baza);

            var result = await controller.DodajPredmet(
                1, NapraviFajl("application/pdf", PetMb)) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(1, tutor.PredmetNaCekanjaId);
            Assert.IsNotNull(tutor.PrilogOcjene);
        }

        [TestMethod]
        public async Task DodajPredmet_KopiranjeFajlaBacaIOException_PropagiraIzuzetakBezSnimanja()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new Predmet { id = 1, naziv = "Matematika" });
            using var baza = new ApplicationDbContext(opcije);
            var userManager = UserManagerSaKorisnikom(NapraviTutora());
            var controller = NapraviController(userManager, baza);

            await Assert.ThrowsExactlyAsync<IOException>(
                () => controller.DodajPredmet(1, NapraviFajlKojiBacaIzuzetak()));
            userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        // ---------- ObrisiPredmet ----------

        [TestMethod]
        public async Task ObrisiPredmet_KorisnikNijePrijavljen_PreusmjeravaNaIndex()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = NapraviController(UserManagerSaKorisnikom(null), baza);

            var result = await controller.ObrisiPredmet(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
        }

        [TestMethod]
        public async Task ObrisiPredmet_KorisnikNijeTutor_PreusmjeravaNaIndexBezBrisanja()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new KorisnikPredmet { idKorisnika = "s1", idPredmeta = 1 });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviStudenta()), baza);

            var result = await controller.ObrisiPredmet(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(1, provjera.KorisnikPredmet.Count());
        }

        [TestMethod]
        public async Task ObrisiPredmet_TutorPredajePredmet_BriseSamoNjegovuVezuIPreusmjerava()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije,
                new KorisnikPredmet { idKorisnika = "t1", idPredmeta = 1 },
                new KorisnikPredmet { idKorisnika = "t1", idPredmeta = 2 },
                new KorisnikPredmet { idKorisnika = "t2", idPredmeta = 1 });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora()), baza);

            var result = await controller.ObrisiPredmet(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual("Predmet uspješno uklonjen.", controller.TempData["Uspjeh"]);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(2, provjera.KorisnikPredmet.Count());
            Assert.IsFalse(provjera.KorisnikPredmet.Any(kp => kp.idKorisnika == "t1" && kp.idPredmeta == 1));
        }

        [TestMethod]
        public async Task ObrisiPredmet_TutorNePredajePredmet_PreusmjeravaBezBrisanjaIPoruke()
        {
            var opcije = NapraviOpcije();
            Ubaci(opcije, new KorisnikPredmet { idKorisnika = "t2", idPredmeta = 1 });
            using var baza = new ApplicationDbContext(opcije);
            var controller = NapraviController(UserManagerSaKorisnikom(NapraviTutora()), baza);

            var result = await controller.ObrisiPredmet(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.IsNull(controller.TempData["Uspjeh"]);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(1, provjera.KorisnikPredmet.Count());
        }
    }
}
