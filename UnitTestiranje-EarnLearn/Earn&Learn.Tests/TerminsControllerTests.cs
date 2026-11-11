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
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace Earn_Learn.Tests
{
    public class NovcanikScenarij
    {
        public double StanjeRacuna { get; set; }
        public double CijenaTermina { get; set; }
        public bool DovoljnoSredstava { get; set; }
    }

    public class ProvizijaScenarij
    {
        public double CijenaTermina { get; set; }
        public double OcekivanaProvizija { get; set; }
        public double OcekivanaZaradaTutora { get; set; }
    }

    public class CijenaPoSatuScenarij
    {
        public double? CijenaPoSatu { get; set; }
        public double OcekivanaCijena { get; set; }
    }

    /// <summary>
    /// Izvori podataka za data-driven testove TerminsController-a (CSV i JSON).
    /// </summary>
    public static class TerminsTestData
    {
        private static string Putanja(string imeFajla) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", imeFajla);

        private static List<T> UcitajJson<T>(string imeFajla)
        {
            var opcije = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(Putanja(imeFajla)), opcije)!;
        }

        private static IEnumerable<object[]> UlogeIzCsv(string imeFajla) =>
            File.ReadLines(Putanja(imeFajla))
                .Skip(1)
                .Select(linija => new object[] { Enum.Parse<Uloga>(linija.Trim()) })
                .ToList();

        // ---- CSV ----

        public static IEnumerable<object[]> NepostojeciIdeviIzCsv =>
            File.ReadLines(Putanja("nepostojeci_ids.csv"))
                .Skip(1)
                .Select(linija => new object[] { int.Parse(linija.Trim(), CultureInfo.InvariantCulture) })
                .ToList();

        public static IEnumerable<object[]> UlogeKojeNisuTutor => UlogeIzCsv("uloge_koje_nisu_tutor.csv");

        public static IEnumerable<object[]> UlogeKojeNisuStudent => UlogeIzCsv("uloge_koje_nisu_student.csv");

        // ---- JSON ----

        public static IEnumerable<object[]> ScenarijiSaDovoljnoSredstava =>
            UcitajJson<NovcanikScenarij>("termini_novcanik_scenariji.json")
                .Where(s => s.DovoljnoSredstava)
                .Select(s => new object[] { s.StanjeRacuna, s.CijenaTermina })
                .ToList();

        public static IEnumerable<object[]> ScenarijiBezDovoljnoSredstava =>
            UcitajJson<NovcanikScenarij>("termini_novcanik_scenariji.json")
                .Where(s => !s.DovoljnoSredstava)
                .Select(s => new object[] { s.StanjeRacuna, s.CijenaTermina })
                .ToList();

        public static IEnumerable<object[]> ProvizijaScenariji =>
            UcitajJson<ProvizijaScenarij>("termini_provizija_scenariji.json")
                .Select(s => new object[] { s.CijenaTermina, s.OcekivanaProvizija, s.OcekivanaZaradaTutora })
                .ToList();

        public static IEnumerable<object[]> CijenePoSatuScenariji =>
            UcitajJson<CijenaPoSatuScenarij>("termini_cijene_po_satu.json")
                .Select(s => new object[] { s.CijenaPoSatu!, s.OcekivanaCijena })
                .ToList();
    }

    [TestClass]
    public class TerminsControllerTests
    {
        private const string TutorId = "tutor-1";
        private const string StudentId = "student-1";
        private const string SystemManagerId = "sm-1";
        private const int TerminId = 7;
        private const int PredmetId = 1000;
        private const string QrKod = "EARNLEARN-7";

        private static readonly TipInstrukcija PrviTip = Enum.GetValues<TipInstrukcija>()[0];

        private DbContextOptions<ApplicationDbContext> _opcije = null!;
        private Mock<UserManager<Korisnik>> _userManager = null!;

        // ---------- Priprema ----------

        [TestInitialize]
        public void Postavi()
        {
            _opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var store = new Mock<IUserStore<Korisnik>>();
            _userManager = new Mock<UserManager<Korisnik>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        // ---------- Helperi ----------

        private void PostaviPrijavljenog(Korisnik? korisnik)
        {
            _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(korisnik);
        }

        private TerminsController NapraviController() =>
            new TerminsController(new ApplicationDbContext(_opcije), _userManager.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
            };

        private void SeedKorisnike(params Korisnik[] korisnici)
        {
            using var ctx = new ApplicationDbContext(_opcije);
            ctx.Users.AddRange(korisnici);
            ctx.SaveChanges();
        }

        private void SeedTermine(params Termin[] termini)
        {
            using var ctx = new ApplicationDbContext(_opcije);
            ctx.Termin.AddRange(termini);
            ctx.SaveChanges();
        }

        private void SeedPredmete(params Predmet[] predmeti)
        {
            using var ctx = new ApplicationDbContext(_opcije);
            ctx.Predmet.AddRange(predmeti);
            ctx.SaveChanges();
        }

        private void SeedKorisnikPredmete(params KorisnikPredmet[] veze)
        {
            using var ctx = new ApplicationDbContext(_opcije);
            ctx.KorisnikPredmet.AddRange(veze);
            ctx.SaveChanges();
        }

        private static Korisnik NapraviKorisnika(
            string id, Uloga uloga, double stanje = 0, double? cijenaPoSatu = null,
            int? brojOdrzanihCasova = null, string ime = "Ime", string prezime = "Prezime") => new Korisnik
            {
                Id = id,
                Uloga = uloga,
                StanjeRacuna = stanje,
                CijenaPoSatu = cijenaPoSatu,
                BrojOdrzanihCasova = brojOdrzanihCasova,
                Ime = ime,
                Prezime = prezime
            };

        private static Termin NapraviTermin(
            int id = TerminId, string idTutora = TutorId, string idStudenta = "",
            StatusTermina status = StatusTermina.Slobodan, double cijena = 20, int? idPredmeta = null) => new Termin
            {
                id = id,
                idTutora = idTutora,
                idStudenta = idStudenta,
                status = status,
                cijena = cijena,
                idPredmeta = idPredmeta,
                qrKod = $"EARNLEARN-{id}",
                datumIVrijeme = new DateTime(2026, 11, 20, 14, 30, 0)
            };

        private Korisnik PripremiPotvrduPrisustva(double cijena, bool saSystemManagerom, int? brojOdrzanihCasova = null)
        {
            var student = NapraviKorisnika(StudentId, Uloga.Student, stanje: cijena + 50, ime: "Amar", prezime: "Ahmetovic");
            SeedKorisnike(NapraviKorisnika(TutorId, Uloga.Tutor, stanje: 10, brojOdrzanihCasova: brojOdrzanihCasova));
            if (saSystemManagerom)
            {
                SeedKorisnike(NapraviKorisnika(SystemManagerId, Uloga.SystemManager, stanje: 5));
            }
            SeedTermine(NapraviTermin(idStudenta: StudentId, status: StatusTermina.Rezervisan, cijena: cijena));
            PostaviPrijavljenog(student);
            return student;
        }

        private Mock<ApplicationDbContext> MockContextKojiBacaConcurrency()
        {
            var mock = new Mock<ApplicationDbContext>(_opcije) { CallBase = true };
            mock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DbUpdateConcurrencyException());
            return mock;
        }

        // ================= Index =================

        [TestMethod]
        public async Task Index_PostojeTermini_VracaViewSaSvimTerminima()
        {
            SeedTermine(NapraviTermin(1), NapraviTermin(2));
            var controller = NapraviController();

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Termin>;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.Count);
        }

        [TestMethod]
        public async Task Index_PrazanSkup_VracaPrazanModel()
        {
            var controller = NapraviController();

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Termin>;
            Assert.IsNotNull(model);
            Assert.AreEqual(0, model.Count);
        }

        // ================= MojiCasovi =================

        [TestMethod]
        public async Task MojiCasovi_NijePrijavljen_PreusmjeravaNaLogin()
        {
            PostaviPrijavljenog(null);
            var controller = NapraviController();

            var result = await controller.MojiCasovi() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Login", result.ActionName);
            Assert.AreEqual("Account", result.ControllerName);
        }

        [TestMethod]
        public async Task MojiCasovi_TutorSaTerminima_VracaSamoNjegoveTerminePoredaneIPredmete()
        {
            SeedPredmete(new Predmet { id = PredmetId, naziv = "Testni predmet" });
            SeedTermine(
                NapraviTermin(1, idPredmeta: PredmetId),
                NapraviTermin(2),
                NapraviTermin(3, idTutora: "drugi-tutor"));
            using (var ctx = new ApplicationDbContext(_opcije))
            {
                ctx.Termin.Find(1)!.datumIVrijeme = new DateTime(2026, 12, 1);
                ctx.Termin.Find(2)!.datumIVrijeme = new DateTime(2026, 11, 1);
                ctx.SaveChanges();
            }
            PostaviPrijavljenog(NapraviKorisnika(TutorId, Uloga.Tutor));
            var controller = NapraviController();

            var result = await controller.MojiCasovi() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Termin>;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.Count);
            Assert.AreEqual(2, model[0].id);
            Assert.AreEqual(1, model[1].id);
            var predmeti = controller.ViewBag.Predmeti as Dictionary<int, string>;
            Assert.IsNotNull(predmeti);
            Assert.AreEqual(1, predmeti.Count);
            Assert.AreEqual("Testni predmet", predmeti[PredmetId]);
        }

        // ================= KreirajTermin (GET) =================

        [TestMethod]
        public async Task KreirajTermin_Get_NijePrijavljen_VracaForbid()
        {
            PostaviPrijavljenog(null);
            var controller = NapraviController();

            var result = await controller.KreirajTermin();

            Assert.IsInstanceOfType<ForbidResult>(result);
        }

        [TestMethod]
        [DynamicData(nameof(TerminsTestData.UlogeKojeNisuTutor), typeof(TerminsTestData))]
        public async Task KreirajTermin_Get_KorisnikNijeTutor_VracaForbid(Uloga uloga)
        {
            PostaviPrijavljenog(NapraviKorisnika(StudentId, uloga));
            var controller = NapraviController();

            var result = await controller.KreirajTermin();

            Assert.IsInstanceOfType<ForbidResult>(result);
        }

        [TestMethod]
        public async Task KreirajTermin_Get_Tutor_VracaViewSaNjegovimPredmetimaICijenom()
        {
            SeedPredmete(
                new Predmet { id = PredmetId, naziv = "Predmet A" },
                new Predmet { id = PredmetId + 1, naziv = "Predmet B" },
                new Predmet { id = PredmetId + 2, naziv = "Predmet C" });
            SeedKorisnikPredmete(
                new KorisnikPredmet { idKorisnika = TutorId, idPredmeta = PredmetId },
                new KorisnikPredmet { idKorisnika = TutorId, idPredmeta = PredmetId + 1 },
                new KorisnikPredmet { idKorisnika = "drugi-tutor", idPredmeta = PredmetId + 2 });
            PostaviPrijavljenog(NapraviKorisnika(TutorId, Uloga.Tutor, cijenaPoSatu: 20));
            var controller = NapraviController();

            var result = await controller.KreirajTermin();

            Assert.IsInstanceOfType<ViewResult>(result);
            var predmeti = controller.ViewBag.Predmeti as List<Predmet>;
            Assert.IsNotNull(predmeti);
            Assert.AreEqual(2, predmeti.Count);
            Assert.AreEqual(20.0, (double)controller.ViewBag.CijenaPoSatu, 0.001);
        }

        [TestMethod]
        public async Task KreirajTermin_Get_TutorBezCijene_PostavljaCijenuNaNulu()
        {
            PostaviPrijavljenog(NapraviKorisnika(TutorId, Uloga.Tutor, cijenaPoSatu: null));
            var controller = NapraviController();

            await controller.KreirajTermin();

            Assert.AreEqual(0.0, (double)controller.ViewBag.CijenaPoSatu, 0.001);
        }

        // ================= KreirajTermin (POST) =================

        [TestMethod]
        public async Task KreirajTermin_Post_NijePrijavljen_VracaForbid()
        {
            PostaviPrijavljenog(null);
            var controller = NapraviController();

            var result = await controller.KreirajTermin(DateTime.UtcNow, PredmetId, PrviTip);

            Assert.IsInstanceOfType<ForbidResult>(result);
        }

        [TestMethod]
        [DynamicData(nameof(TerminsTestData.UlogeKojeNisuTutor), typeof(TerminsTestData))]
        public async Task KreirajTermin_Post_KorisnikNijeTutor_VracaForbidBezSnimanja(Uloga uloga)
        {
            PostaviPrijavljenog(NapraviKorisnika(StudentId, uloga));
            var controller = NapraviController();

            var result = await controller.KreirajTermin(DateTime.UtcNow, PredmetId, PrviTip);

            Assert.IsInstanceOfType<ForbidResult>(result);
            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual(0, provjera.Termin.Count());
        }

        [TestMethod]
        [DynamicData(nameof(TerminsTestData.CijenePoSatuScenariji), typeof(TerminsTestData))]
        public async Task KreirajTermin_Post_Tutor_SnimaSlobodanTerminSaOcekivanomCijenom(
            double? cijenaPoSatu, double ocekivanaCijena)
        {
            PostaviPrijavljenog(NapraviKorisnika(TutorId, Uloga.Tutor, cijenaPoSatu: cijenaPoSatu));
            var controller = NapraviController();
            var datum = new DateTime(2026, 12, 5, 10, 0, 0);

            var result = await controller.KreirajTermin(datum, PredmetId, PrviTip) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("MojiCasovi", result.ActionName);
            using var provjera = new ApplicationDbContext(_opcije);
            var termin = provjera.Termin.Single();
            Assert.AreEqual(TutorId, termin.idTutora);
            Assert.AreEqual(string.Empty, termin.idStudenta);
            Assert.AreEqual((int?)PredmetId, termin.idPredmeta);
            Assert.AreEqual(datum, termin.datumIVrijeme);
            Assert.AreEqual(PrviTip, termin.tipInstrukcija);
            Assert.AreEqual(StatusTermina.Slobodan, termin.status);
            Assert.AreEqual(ocekivanaCijena, (double)termin.cijena, 0.001);
        }

        // ================= RezervisiTermin (GET) =================

        [TestMethod]
        public async Task RezervisiTermin_Get_NijePrijavljen_VracaViewPristupOdbijen()
        {
            PostaviPrijavljenog(null);
            var controller = NapraviController();

            var result = await controller.RezervisiTermin(TerminId) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ViewName);
        }

        [TestMethod]
        [DynamicData(nameof(TerminsTestData.UlogeKojeNisuStudent), typeof(TerminsTestData))]
        public async Task RezervisiTermin_Get_KorisnikNijeStudent_VracaViewPristupOdbijen(Uloga uloga)
        {
            PostaviPrijavljenog(NapraviKorisnika(StudentId, uloga));
            var controller = NapraviController();

            var result = await controller.RezervisiTermin(TerminId) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ViewName);
        }

        [TestMethod]
        [DynamicData(nameof(TerminsTestData.NepostojeciIdeviIzCsv), typeof(TerminsTestData))]
        public async Task RezervisiTermin_Get_TerminNePostoji_VracaNotFound(int id)
        {
            SeedTermine(NapraviTermin());
            PostaviPrijavljenog(NapraviKorisnika(StudentId, Uloga.Student));
            var controller = NapraviController();

            var result = await controller.RezervisiTermin(id);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task RezervisiTermin_Get_TerminNijeSlobodan_VracaNotFound()
        {
            SeedTermine(NapraviTermin(status: StatusTermina.Rezervisan));
            PostaviPrijavljenog(NapraviKorisnika(StudentId, Uloga.Student));
            var controller = NapraviController();

            var result = await controller.RezervisiTermin(TerminId);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task RezervisiTermin_Get_SlobodanTerminSaPredmetom_VracaViewSaTerminomTutoromIPredmetom()
        {
            SeedKorisnike(NapraviKorisnika(TutorId, Uloga.Tutor, ime: "Tarik"));
            SeedPredmete(new Predmet { id = PredmetId, naziv = "Testni predmet" });
            SeedTermine(NapraviTermin(idPredmeta: PredmetId));
            PostaviPrijavljenog(NapraviKorisnika(StudentId, Uloga.Student));
            var controller = NapraviController();

            var result = await controller.RezervisiTermin(TerminId) as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as Termin;
            Assert.IsNotNull(model);
            Assert.AreEqual(TerminId, model.id);
            var tutor = controller.ViewBag.Tutor as Korisnik;
            Assert.IsNotNull(tutor);
            Assert.AreEqual(TutorId, tutor.Id);
            var predmet = controller.ViewBag.Predmet as Predmet;
            Assert.IsNotNull(predmet);
            Assert.AreEqual("Testni predmet", predmet.naziv);
        }

        [TestMethod]
        public async Task RezervisiTermin_Get_SlobodanTerminBezPredmeta_VracaViewBezPredmeta()
        {
            SeedKorisnike(NapraviKorisnika(TutorId, Uloga.Tutor));
            SeedTermine(NapraviTermin(idPredmeta: null));
            PostaviPrijavljenog(NapraviKorisnika(StudentId, Uloga.Student));
            var controller = NapraviController();

            var result = await controller.RezervisiTermin(TerminId);

            Assert.IsInstanceOfType<ViewResult>(result);
            Assert.IsNull(controller.ViewBag.Predmet as Predmet);
        }

        // ================= RezervisiTermin (POST) =================

        [TestMethod]
        public async Task RezervisiTermin_Post_NijePrijavljen_VracaViewPristupOdbijen()
        {
            PostaviPrijavljenog(null);
            var controller = NapraviController();

            var result = await controller.RezervisiTermin(TerminId, PrviTip) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ViewName);
        }

        [TestMethod]
        [DynamicData(nameof(TerminsTestData.UlogeKojeNisuStudent), typeof(TerminsTestData))]
        public async Task RezervisiTermin_Post_KorisnikNijeStudent_VracaViewPristupOdbijen(Uloga uloga)
        {
            PostaviPrijavljenog(NapraviKorisnika(StudentId, uloga));
            var controller = NapraviController();

            var result = await controller.RezervisiTermin(TerminId, PrviTip) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ViewName);
        }

        [TestMethod]
        [DynamicData(nameof(TerminsTestData.NepostojeciIdeviIzCsv), typeof(TerminsTestData))]
        public async Task RezervisiTermin_Post_TerminNePostoji_VracaNaIndexSaPorukomOGresci(int id)
        {
            SeedTermine(NapraviTermin());
            PostaviPrijavljenog(NapraviKorisnika(StudentId, Uloga.Student, stanje: 100));
            var controller = NapraviController();

            var result = await controller.RezervisiTermin(id, PrviTip) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            Assert.AreEqual("Student", result.ControllerName);
            Assert.AreEqual("Termin nije dostupan.", (string?)controller.TempData["Greska"]);
        }

        [TestMethod]
        [DynamicData(nameof(TerminsTestData.ScenarijiBezDovoljnoSredstava), typeof(TerminsTestData))]
        public async Task RezervisiTermin_Post_NedovoljnoSredstava_VracaNaRezervacijuBezPromjene(
            double stanje, double cijena)
        {
            SeedTermine(NapraviTermin(cijena: cijena));
            PostaviPrijavljenog(NapraviKorisnika(StudentId, Uloga.Student, stanje: stanje));
            var controller = NapraviController();

            var result = await controller.RezervisiTermin(TerminId, PrviTip) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("RezervisiTermin", result.ActionName);
            Assert.AreEqual(TerminId, (int)result.RouteValues!["id"]!);
            var poruka = (string?)controller.TempData["GreskaNovcenik"];
            Assert.IsNotNull(poruka);
            Assert.IsTrue(poruka.StartsWith("Nemate dovoljno sredstava na novčaniku!"));
            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual(StatusTermina.Slobodan, provjera.Termin.Find(TerminId)!.status);
        }

        [TestMethod]
        [DynamicData(nameof(TerminsTestData.ScenarijiSaDovoljnoSredstava), typeof(TerminsTestData))]
        public async Task RezervisiTermin_Post_DovoljnoSredstava_RezervisePreusmjeravaNaDashboard(
            double stanje, double cijena)
        {
            SeedTermine(NapraviTermin(cijena: cijena));
            PostaviPrijavljenog(NapraviKorisnika(StudentId, Uloga.Student, stanje: stanje));
            var controller = NapraviController();

            var result = await controller.RezervisiTermin(TerminId, PrviTip) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.AreEqual("Student", result.ControllerName);
            Assert.AreEqual("Termin uspješno rezervisan!", (string?)controller.TempData["Uspjeh"]);
            using var provjera = new ApplicationDbContext(_opcije);
            var termin = provjera.Termin.Find(TerminId)!;
            Assert.AreEqual(StudentId, termin.idStudenta);
            Assert.AreEqual(StatusTermina.Rezervisan, termin.status);
            Assert.AreEqual(PrviTip, termin.tipInstrukcija);
        }

        // ================= PotvrdPrisustvo =================

        [TestMethod]
        public async Task PotvrdPrisustvo_NullKod_VracaBadRequest()
        {
            var controller = NapraviController();

            var result = await controller.PotvrdPrisustvo(null!) as BadRequestObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Neispravan QR kod.", result.Value as string);
        }

        [TestMethod]
        public async Task PotvrdPrisustvo_PrazanKod_VracaBadRequest()
        {
            var controller = NapraviController();

            var result = await controller.PotvrdPrisustvo(string.Empty) as BadRequestObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Neispravan QR kod.", result.Value as string);
        }

        [TestMethod]
        public async Task PotvrdPrisustvo_NijePrijavljen_VracaViewPristupOdbijen()
        {
            PostaviPrijavljenog(null);
            var controller = NapraviController();

            var result = await controller.PotvrdPrisustvo(QrKod) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ViewName);
        }

        [TestMethod]
        [DynamicData(nameof(TerminsTestData.UlogeKojeNisuStudent), typeof(TerminsTestData))]
        public async Task PotvrdPrisustvo_KorisnikNijeStudent_VracaViewPristupOdbijen(Uloga uloga)
        {
            PostaviPrijavljenog(NapraviKorisnika(StudentId, uloga));
            var controller = NapraviController();

            var result = await controller.PotvrdPrisustvo(QrKod) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ViewName);
        }

        [TestMethod]
        public async Task PotvrdPrisustvo_TerminSaKodomNePostoji_VracaNaDashboardSaPorukomOGresci()
        {
            PostaviPrijavljenog(NapraviKorisnika(StudentId, Uloga.Student));
            var controller = NapraviController();

            var result = await controller.PotvrdPrisustvo("NEPOSTOJECI-KOD") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.AreEqual("Student", result.ControllerName);
            Assert.AreEqual("QR kod nije validan ili termin ne postoji.", (string?)controller.TempData["Greska"]);
        }

        [TestMethod]
        public async Task PotvrdPrisustvo_TerminJeDrugogStudenta_VracaNaDashboardSaPorukomOGresci()
        {
            SeedTermine(NapraviTermin(idStudenta: "drugi-student", status: StatusTermina.Rezervisan));
            PostaviPrijavljenog(NapraviKorisnika(StudentId, Uloga.Student, stanje: 100));
            var controller = NapraviController();

            var result = await controller.PotvrdPrisustvo(QrKod) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.AreEqual("Ovaj QR kod nije za vaš termin.", (string?)controller.TempData["Greska"]);
        }

        [TestMethod]
        public async Task PotvrdPrisustvo_PrisustvoVecPotvrdjeno_VracaNaDashboardSaInfoPorukom()
        {
            SeedTermine(NapraviTermin(idStudenta: StudentId, status: StatusTermina.Odrzan));
            PostaviPrijavljenog(NapraviKorisnika(StudentId, Uloga.Student, stanje: 100));
            var controller = NapraviController();

            var result = await controller.PotvrdPrisustvo(QrKod) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.AreEqual("Prisustvo je već potvrđeno za ovaj čas.", (string?)controller.TempData["Info"]);
        }

        [TestMethod]
        public async Task PotvrdPrisustvo_TerminNijeRezervisan_VracaNaDashboardSaPorukomOGresci()
        {
            SeedTermine(NapraviTermin(idStudenta: StudentId, status: StatusTermina.Slobodan));
            PostaviPrijavljenog(NapraviKorisnika(StudentId, Uloga.Student, stanje: 100));
            var controller = NapraviController();

            var result = await controller.PotvrdPrisustvo(QrKod) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.AreEqual("Termin nije u statusu rezervisan.", (string?)controller.TempData["Greska"]);
        }

        [TestMethod]
        [DynamicData(nameof(TerminsTestData.ScenarijiBezDovoljnoSredstava), typeof(TerminsTestData))]
        public async Task PotvrdPrisustvo_NedovoljnoSredstava_VracaNaDashboardBezPromjena(double stanje, double cijena)
        {
            SeedKorisnike(NapraviKorisnika(TutorId, Uloga.Tutor, stanje: 10));
            SeedTermine(NapraviTermin(idStudenta: StudentId, status: StatusTermina.Rezervisan, cijena: cijena));
            PostaviPrijavljenog(NapraviKorisnika(StudentId, Uloga.Student, stanje: stanje));
            var controller = NapraviController();

            var result = await controller.PotvrdPrisustvo(QrKod) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            var poruka = (string?)controller.TempData["Greska"];
            Assert.IsNotNull(poruka);
            Assert.IsTrue(poruka.StartsWith("Nemate dovoljno sredstava."));
            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual(StatusTermina.Rezervisan, provjera.Termin.Find(TerminId)!.status);
            Assert.AreEqual(0, provjera.Transakcija.Count());
        }

        [TestMethod]
        public async Task PotvrdPrisustvo_TutorNePostoji_VracaNaDashboardSaPorukomOGresci()
        {
            SeedTermine(NapraviTermin(idStudenta: StudentId, status: StatusTermina.Rezervisan, cijena: 20));
            PostaviPrijavljenog(NapraviKorisnika(StudentId, Uloga.Student, stanje: 100));
            var controller = NapraviController();

            var result = await controller.PotvrdPrisustvo(QrKod) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.AreEqual("Tutor nije pronađen.", (string?)controller.TempData["Greska"]);
        }

        [TestMethod]
        [DynamicData(nameof(TerminsTestData.ProvizijaScenariji), typeof(TerminsTestData))]
        public async Task PotvrdPrisustvo_SaSystemManagerom_PrebacujeNovacIDijeliProviziju(
            double cijena, double ocekivanaProvizija, double ocekivanaZarada)
        {
            var student = PripremiPotvrduPrisustva(cijena, saSystemManagerom: true);
            var controller = NapraviController();

            var result = await controller.PotvrdPrisustvo(QrKod) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.AreEqual("Student", result.ControllerName);
            var poruka = (string?)controller.TempData["Uspjeh"];
            Assert.IsNotNull(poruka);
            Assert.IsTrue(poruka.StartsWith("Prisustvo potvrđeno!"));
            Assert.AreEqual(50.0, student.StanjeRacuna, 0.001);
            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual(10 + ocekivanaZarada, (double)provjera.Users.Find(TutorId)!.StanjeRacuna, 0.001);
            Assert.AreEqual(5 + ocekivanaProvizija, (double)provjera.Users.Find(SystemManagerId)!.StanjeRacuna, 0.001);
            var termin = provjera.Termin.Find(TerminId)!;
            Assert.AreEqual(StatusTermina.Odrzan, termin.status);
            Assert.IsTrue(termin.prisustvoPotvrdjeno);
        }

        [TestMethod]
        [DynamicData(nameof(TerminsTestData.ProvizijaScenariji), typeof(TerminsTestData))]
        public async Task PotvrdPrisustvo_SaSystemManagerom_KreiraTriTransakcijeSaOcekivanimIznosima(
            double cijena, double ocekivanaProvizija, double ocekivanaZarada)
        {
            PripremiPotvrduPrisustva(cijena, saSystemManagerom: true);
            var controller = NapraviController();

            await controller.PotvrdPrisustvo(QrKod);

            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual(3, provjera.Transakcija.Count());
            var naplata = provjera.Transakcija.Single(t => t.idStudenta == StudentId);
            var zarada = provjera.Transakcija.Single(t => t.idTutora == TutorId && t.idStudenta == string.Empty);
            var provizija = provjera.Transakcija.Single(t => t.idTutora == SystemManagerId);
            Assert.AreEqual(-cijena, (double)naplata.iznos, 0.001);
            Assert.AreEqual(ocekivanaZarada, (double)zarada.iznos, 0.001);
            Assert.AreEqual(ocekivanaProvizija, (double)provizija.iznos, 0.001);
        }

        [TestMethod]
        public async Task PotvrdPrisustvo_Uspjeh_KreiraObavjestenjeZaTutora()
        {
            PripremiPotvrduPrisustva(100, saSystemManagerom: true);
            var controller = NapraviController();

            await controller.PotvrdPrisustvo(QrKod);

            using var provjera = new ApplicationDbContext(_opcije);
            var obavjestenje = provjera.Obavjestenje.Single();
            Assert.AreEqual(TutorId, obavjestenje.idKorisnika);
            Assert.AreEqual(TerminId, obavjestenje.idTermina);
            Assert.IsFalse(obavjestenje.procitano);
            Assert.IsTrue(obavjestenje.sadrzaj.Contains("Student Amar Ahmetovic"));
        }

        [TestMethod]
        public async Task PotvrdPrisustvo_Uspjeh_AzuriraKorisnikeKrozUserManager()
        {
            PripremiPotvrduPrisustva(100, saSystemManagerom: true);
            var controller = NapraviController();

            await controller.PotvrdPrisustvo(QrKod);

            _userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Exactly(3));
        }

        [TestMethod]
        public async Task PotvrdPrisustvo_TutorBezOdrzanihCasova_PostavljaBrojNaJedan()
        {
            PripremiPotvrduPrisustva(100, saSystemManagerom: true, brojOdrzanihCasova: null);
            var controller = NapraviController();

            await controller.PotvrdPrisustvo(QrKod);

            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual((int?)1, provjera.Users.Find(TutorId)!.BrojOdrzanihCasova);
        }

        [TestMethod]
        public async Task PotvrdPrisustvo_TutorSaOdrzanimCasovima_PovecavaBrojZaJedan()
        {
            PripremiPotvrduPrisustva(100, saSystemManagerom: true, brojOdrzanihCasova: 4);
            var controller = NapraviController();

            await controller.PotvrdPrisustvo(QrKod);

            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual((int?)5, provjera.Users.Find(TutorId)!.BrojOdrzanihCasova);
        }

        [TestMethod]
        public async Task PotvrdPrisustvo_BezSystemManagera_KreiraDveTransakcijeIBezProvizije()
        {
            var student = PripremiPotvrduPrisustva(100, saSystemManagerom: false);
            var controller = NapraviController();

            var result = await controller.PotvrdPrisustvo(QrKod) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.AreEqual(50.0, student.StanjeRacuna, 0.001);
            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual(2, provjera.Transakcija.Count());
            Assert.AreEqual(100.0, (double)provjera.Users.Find(TutorId)!.StanjeRacuna, 0.001);
            _userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Exactly(2));
        }

        // ================= Details =================

        [TestMethod]
        public async Task Details_NullId_VracaNotFound()
        {
            var controller = NapraviController();

            var result = await controller.Details(null);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        [DynamicData(nameof(TerminsTestData.NepostojeciIdeviIzCsv), typeof(TerminsTestData))]
        public async Task Details_NepostojeciId_VracaNotFound(int id)
        {
            SeedTermine(NapraviTermin());
            var controller = NapraviController();

            var result = await controller.Details(id);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Details_PostojeciId_VracaViewSaTerminom()
        {
            SeedTermine(NapraviTermin());
            var controller = NapraviController();

            var result = await controller.Details(TerminId) as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as Termin;
            Assert.IsNotNull(model);
            Assert.AreEqual(TerminId, model.id);
        }

        // ================= Create =================

        [TestMethod]
        public void Create_Get_VracaView()
        {
            var controller = NapraviController();

            var result = controller.Create();

            Assert.IsInstanceOfType<ViewResult>(result);
        }

        [TestMethod]
        public async Task Create_Post_ValidanModel_SnimaIPreusmjeravaNaIndex()
        {
            var controller = NapraviController();

            var result = await controller.Create(NapraviTermin()) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(nameof(TerminsController.Index), result.ActionName);
            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual(1, provjera.Termin.Count());
        }

        [TestMethod]
        public async Task Create_Post_NevalidanModel_VracaViewBezSnimanja()
        {
            var controller = NapraviController();
            controller.ModelState.AddModelError("datumIVrijeme", "Obavezno polje");
            var termin = NapraviTermin();

            var result = await controller.Create(termin) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(termin, result.Model);
            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual(0, provjera.Termin.Count());
        }

        // ================= Edit =================

        [TestMethod]
        public async Task Edit_Get_NullId_VracaNotFound()
        {
            var controller = NapraviController();

            var result = await controller.Edit(null);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        [DynamicData(nameof(TerminsTestData.NepostojeciIdeviIzCsv), typeof(TerminsTestData))]
        public async Task Edit_Get_NepostojeciId_VracaNotFound(int id)
        {
            var controller = NapraviController();

            var result = await controller.Edit(id);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Edit_Get_PostojeciId_VracaViewSaTerminom()
        {
            SeedTermine(NapraviTermin());
            var controller = NapraviController();

            var result = await controller.Edit(TerminId) as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as Termin;
            Assert.IsNotNull(model);
            Assert.AreEqual(TerminId, model.id);
        }

        [TestMethod]
        public async Task Edit_Post_RazlicitId_VracaNotFound()
        {
            var controller = NapraviController();

            var result = await controller.Edit(1, NapraviTermin(2));

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Edit_Post_ValidanModel_AzuriraIPreusmjeravaNaIndex()
        {
            SeedTermine(NapraviTermin());
            var controller = NapraviController();
            var izmijenjen = NapraviTermin();
            izmijenjen.qrKod = "NOVI-KOD";

            var result = await controller.Edit(TerminId, izmijenjen) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(nameof(TerminsController.Index), result.ActionName);
            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual("NOVI-KOD", provjera.Termin.Find(TerminId)!.qrKod);
        }

        [TestMethod]
        public async Task Edit_Post_NevalidanModel_VracaView()
        {
            var controller = NapraviController();
            controller.ModelState.AddModelError("datumIVrijeme", "Obavezno polje");
            var termin = NapraviTermin();

            var result = await controller.Edit(TerminId, termin) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(termin, result.Model);
        }

        [TestMethod]
        public async Task Edit_Post_ConcurrencyIzuzetakIObjekatNePostoji_VracaNotFound()
        {
            var mockContext = MockContextKojiBacaConcurrency();
            var controller = new TerminsController(mockContext.Object, _userManager.Object);

            var result = await controller.Edit(TerminId, NapraviTermin());

            Assert.IsInstanceOfType<NotFoundResult>(result);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task Edit_Post_ConcurrencyIzuzetakIObjekatPostoji_PropagiraIzuzetak()
        {
            SeedTermine(NapraviTermin());
            var mockContext = MockContextKojiBacaConcurrency();
            var controller = new TerminsController(mockContext.Object, _userManager.Object);

            await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(
                () => controller.Edit(TerminId, NapraviTermin()));
        }

        // ================= Delete =================

        [TestMethod]
        public async Task Delete_Get_NullId_VracaNotFound()
        {
            var controller = NapraviController();

            var result = await controller.Delete(null);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        [DynamicData(nameof(TerminsTestData.NepostojeciIdeviIzCsv), typeof(TerminsTestData))]
        public async Task Delete_Get_NepostojeciId_VracaNotFound(int id)
        {
            var controller = NapraviController();

            var result = await controller.Delete(id);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Delete_Get_PostojeciId_VracaViewSaTerminom()
        {
            SeedTermine(NapraviTermin());
            var controller = NapraviController();

            var result = await controller.Delete(TerminId) as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as Termin;
            Assert.IsNotNull(model);
            Assert.AreEqual(TerminId, model.id);
        }

        [TestMethod]
        public async Task DeleteConfirmed_PostojeciId_BriseIPreusmjeravaNaIndex()
        {
            SeedTermine(NapraviTermin());
            var controller = NapraviController();

            var result = await controller.DeleteConfirmed(TerminId) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(nameof(TerminsController.Index), result.ActionName);
            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual(0, provjera.Termin.Count());
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojeciId_PreusmjeravaNaIndexBezBrisanja()
        {
            SeedTermine(NapraviTermin());
            var controller = NapraviController();

            var result = await controller.DeleteConfirmed(999);

            Assert.IsInstanceOfType<RedirectToActionResult>(result);
            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual(1, provjera.Termin.Count());
        }
    }
}