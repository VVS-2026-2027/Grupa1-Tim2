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
    public class RezervacijaScenarij
    {
        public double StanjeRacuna { get; set; }
        public double? CijenaPoSatuTutora { get; set; }
        public double CijenaTermina { get; set; }
        public double OcekivanaCijena { get; set; }
        public bool DovoljnoSredstava { get; set; }
    }

    /// <summary>
    /// Izvori podataka za data-driven testove rezervacije (CSV i JSON).
    /// </summary>
    public static class RezervacijaTestData
    {
        private static string Putanja(string imeFajla) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", imeFajla);

        // CSV forma: nepostojeci id-evi termina
        public static IEnumerable<object[]> NepostojeciIdeviIzCsv =>
            File.ReadLines(Putanja("nepostojeci_ids.csv"))
                .Skip(1)
                .Select(linija => new object[] { int.Parse(linija.Trim(), CultureInfo.InvariantCulture) })
                .ToList();

        private static List<RezervacijaScenarij> UcitajScenarije()
        {
            var opcije = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<RezervacijaScenarij>>(
                File.ReadAllText(Putanja("rezervacija_scenariji.json")), opcije)!;
        }

        // JSON forma: scenariji u kojima student ima dovoljno sredstava
        public static IEnumerable<object[]> ScenarijiSaDovoljnoSredstava =>
            UcitajScenarije()
                .Where(s => s.DovoljnoSredstava)
                .Select(s => new object[] { s.StanjeRacuna, s.CijenaPoSatuTutora!, s.CijenaTermina, s.OcekivanaCijena })
                .ToList();

        // JSON forma: scenariji u kojima student nema dovoljno sredstava
        public static IEnumerable<object[]> ScenarijiBezDovoljnoSredstava =>
            UcitajScenarije()
                .Where(s => !s.DovoljnoSredstava)
                .Select(s => new object[] { s.StanjeRacuna, s.CijenaPoSatuTutora!, s.CijenaTermina })
                .ToList();

        // Sve vrijednosti enuma TipInstrukcija
        public static IEnumerable<object[]> SviTipoviInstrukcija =>
            Enum.GetValues<TipInstrukcija>().Select(t => new object[] { t }).ToList();
    }

    [TestClass]
    public class RezervacijaControllerTests
    {
        private const string StudentId = "student-1";
        private const string TutorId = "tutor-1";
        private const int TerminId = 7;

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

        private void PostaviPrijavljenogKorisnika(string? idKorisnika)
        {
            _userManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(idKorisnika);
        }

        private RezervacijaController NapraviController() =>
            new RezervacijaController(_userManager.Object, new ApplicationDbContext(_opcije))
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

        private void SeedTermin(StatusTermina status = StatusTermina.Slobodan, double cijena = 15)
        {
            using var ctx = new ApplicationDbContext(_opcije);
            ctx.Termin.Add(new Termin
            {
                id = TerminId,
                idTutora = TutorId,
                status = status,
                cijena = cijena,
                datumIVrijeme = new DateTime(2026, 11, 20, 14, 30, 0)
            });
            ctx.SaveChanges();
        }

        private static Korisnik NapraviStudenta(double stanje = 100, Uloga uloga = Uloga.Student) => new Korisnik
        {
            Id = StudentId,
            Ime = "Amar",
            Prezime = "Ahmetovic",
            Uloga = uloga,
            StanjeRacuna = stanje
        };

        private static Korisnik NapraviTutora(double? cijenaPoSatu = 20) => new Korisnik
        {
            Id = TutorId,
            Ime = "Tarik",
            Prezime = "Tutoric",
            Uloga = Uloga.Tutor,
            CijenaPoSatu = cijenaPoSatu
        };

        // ================= Nevalidan korisnik =================

        [TestMethod]
        public async Task Potvrdi_KorisnikNePostoji_PreusmjeravaNaPristupOdbijen()
        {
            PostaviPrijavljenogKorisnika("nepostojeci-korisnik");
            var controller = NapraviController();

            var result = await controller.Potvrdi(TerminId, PrviTip) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ActionName);
            Assert.AreEqual("Home", result.ControllerName);
        }

        [TestMethod]
        public async Task Potvrdi_NijePrijavljen_PreusmjeravaNaPristupOdbijen()
        {
            PostaviPrijavljenogKorisnika(null);
            var controller = NapraviController();

            var result = await controller.Potvrdi(TerminId, PrviTip) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ActionName);
        }

        [TestMethod]
        public async Task Potvrdi_SystemManager_PreusmjeravaNaPristupOdbijen()
        {
            SeedKorisnike(NapraviStudenta(uloga: Uloga.SystemManager), NapraviTutora());
            SeedTermin();
            PostaviPrijavljenogKorisnika(StudentId);
            var controller = NapraviController();

            var result = await controller.Potvrdi(TerminId, PrviTip) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ActionName);
            Assert.AreEqual("Home", result.ControllerName);
            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual(StatusTermina.Slobodan, provjera.Termin.Find(TerminId)!.status);
        }

        // ================= Termin nedostupan =================

        [TestMethod]
        [DynamicData(nameof(RezervacijaTestData.NepostojeciIdeviIzCsv), typeof(RezervacijaTestData))]
        public async Task Potvrdi_TerminNePostoji_VracaNaDashboardSaPorukomOGresci(int idTermina)
        {
            SeedKorisnike(NapraviStudenta(), NapraviTutora());
            SeedTermin();
            PostaviPrijavljenogKorisnika(StudentId);
            var controller = NapraviController();

            var result = await controller.Potvrdi(idTermina, PrviTip) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.AreEqual("Student", result.ControllerName);
            Assert.AreEqual("Termin nije dostupan.", (string?)controller.TempData["Greska"]);
        }

        [TestMethod]
        public async Task Potvrdi_TerminVecRezervisan_VracaNaDashboardSaPorukomOGresci()
        {
            SeedKorisnike(NapraviStudenta(), NapraviTutora());
            SeedTermin(status: StatusTermina.Rezervisan);
            PostaviPrijavljenogKorisnika(StudentId);
            var controller = NapraviController();

            var result = await controller.Potvrdi(TerminId, PrviTip) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.AreEqual("Student", result.ControllerName);
            Assert.AreEqual("Termin nije dostupan.", (string?)controller.TempData["Greska"]);
        }

        // ================= Nedovoljno sredstava =================

        [TestMethod]
        [DynamicData(nameof(RezervacijaTestData.ScenarijiBezDovoljnoSredstava), typeof(RezervacijaTestData))]
        public async Task Potvrdi_NedovoljnoSredstava_VracaNaProfilTutoraBezRezervacije(
            double stanje, double? cijenaPoSatu, double cijenaTermina)
        {
            SeedKorisnike(NapraviStudenta(stanje), NapraviTutora(cijenaPoSatu));
            SeedTermin(cijena: cijenaTermina);
            PostaviPrijavljenogKorisnika(StudentId);
            var controller = NapraviController();

            var result = await controller.Potvrdi(TerminId, PrviTip) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Profil", result.ActionName);
            Assert.AreEqual("Tutor", result.ControllerName);
            Assert.AreEqual(TutorId, (string?)result.RouteValues!["id"]);
            var poruka = (string?)controller.TempData["GreskaNovcenik"];
            Assert.IsNotNull(poruka);
            Assert.IsTrue(poruka.StartsWith("Nemate dovoljno sredstava!"));
            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual(StatusTermina.Slobodan, provjera.Termin.Find(TerminId)!.status);
            Assert.AreEqual(0, provjera.Obavjestenje.Count());
        }

        // ================= Uspjesna rezervacija =================

        [TestMethod]
        [DynamicData(nameof(RezervacijaTestData.ScenarijiSaDovoljnoSredstava), typeof(RezervacijaTestData))]
        public async Task Potvrdi_DovoljnoSredstava_RezervisePoOcekivanojCijeniIPreusmjeravaStudenta(
            double stanje, double? cijenaPoSatu, double cijenaTermina, double ocekivanaCijena)
        {
            SeedKorisnike(NapraviStudenta(stanje), NapraviTutora(cijenaPoSatu));
            SeedTermin(cijena: cijenaTermina);
            PostaviPrijavljenogKorisnika(StudentId);
            var controller = NapraviController();

            var result = await controller.Potvrdi(TerminId, PrviTip) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.AreEqual("Student", result.ControllerName);
            Assert.AreEqual("Termin uspješno rezervisan!", (string?)controller.TempData["Uspjeh"]);
            using var provjera = new ApplicationDbContext(_opcije);
            var termin = provjera.Termin.Find(TerminId)!;
            Assert.AreEqual(StudentId, termin.idStudenta);
            Assert.AreEqual(StatusTermina.Rezervisan, termin.status);
            Assert.AreEqual(ocekivanaCijena, (double)termin.cijena);
        }

        [TestMethod]
        [DynamicData(nameof(RezervacijaTestData.SviTipoviInstrukcija), typeof(RezervacijaTestData))]
        public async Task Potvrdi_RazlicitTipInstrukcije_SnimaOdabraniTip(TipInstrukcija tip)
        {
            SeedKorisnike(NapraviStudenta(), NapraviTutora());
            SeedTermin();
            PostaviPrijavljenogKorisnika(StudentId);
            var controller = NapraviController();

            await controller.Potvrdi(TerminId, tip);

            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual(tip, provjera.Termin.Find(TerminId)!.tipInstrukcija);
        }

        [TestMethod]
        public async Task Potvrdi_TutorNePostoji_KoristiCijenuTermina()
        {
            SeedKorisnike(NapraviStudenta(100));
            SeedTermin(cijena: 15);
            PostaviPrijavljenogKorisnika(StudentId);
            var controller = NapraviController();

            await controller.Potvrdi(TerminId, PrviTip);

            using var provjera = new ApplicationDbContext(_opcije);
            Assert.AreEqual(15.0, (double)provjera.Termin.Find(TerminId)!.cijena);
            Assert.AreEqual(StatusTermina.Rezervisan, provjera.Termin.Find(TerminId)!.status);
        }

        [TestMethod]
        public async Task Potvrdi_UspjesnaRezervacija_KreiraObavjestenjeZaTutora()
        {
            SeedKorisnike(NapraviStudenta(), NapraviTutora());
            SeedTermin();
            PostaviPrijavljenogKorisnika(StudentId);
            var controller = NapraviController();

            await controller.Potvrdi(TerminId, PrviTip);

            using var provjera = new ApplicationDbContext(_opcije);
            var obavjestenje = provjera.Obavjestenje.Single();
            Assert.AreEqual(TutorId, obavjestenje.idKorisnika);
            Assert.AreEqual(TerminId, obavjestenje.idTermina);
            Assert.IsFalse(obavjestenje.procitano);
            Assert.IsTrue(obavjestenje.naslov.StartsWith("Novi termin rezervisan"));
            Assert.IsTrue(obavjestenje.sadrzaj.Contains("Amar Ahmetovic"));
            Assert.IsTrue(obavjestenje.sadrzaj.Contains(PrviTip.ToString()));
        }

        [TestMethod]
        public async Task Potvrdi_PrijavljenTutor_PreusmjeravaNaTutorDashboard()
        {
            SeedKorisnike(NapraviStudenta(uloga: Uloga.Tutor), NapraviTutora());
            SeedTermin();
            PostaviPrijavljenogKorisnika(StudentId);
            var controller = NapraviController();

            var result = await controller.Potvrdi(TerminId, PrviTip) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.AreEqual("Tutor", result.ControllerName);
        }
    }
}