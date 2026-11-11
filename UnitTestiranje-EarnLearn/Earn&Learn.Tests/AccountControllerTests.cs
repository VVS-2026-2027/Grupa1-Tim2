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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Earn_Learn.Tests
{
    public class UlogaOdredisteZapis
    {
        public Uloga Uloga { get; set; }
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>
    /// Izvori podataka za data-driven testove AccountController-a (CSV i JSON).
    /// </summary>
    public static class AccountTestData
    {
        private static string Putanja(string imeFajla) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", imeFajla);

        private static IEnumerable<string[]> CsvRedovi(string imeFajla) =>
            File.ReadLines(Putanja(imeFajla)).Skip(1).Select(linija => linija.Split(',')).ToList();

        // CSV: parovi (email, password) gdje je barem jedno prazno
        public static IEnumerable<object[]> PrazniLoginPodaci =>
            CsvRedovi("prazni_login_podaci.csv").Select(k => new object[] { k[0], k[1] }).ToList();

        // CSV: dozvoljeni tipovi priloga i ekstenzije
        public static IEnumerable<object[]> DozvoljeniTipoviPriloga =>
            CsvRedovi("dozvoljeni_tipovi_priloga.csv").Select(k => new object[] { k[0], k[1] }).ToList();

        // CSV: nedozvoljeni tipovi priloga
        public static IEnumerable<object[]> NedozvoljeniTipoviPriloga =>
            CsvRedovi("nedozvoljeni_tipovi_priloga.csv").Select(k => new object[] { k[0] }).ToList();

        // JSON: uloga -> odrediste nakon prijave
        public static IEnumerable<object[]> UlogeIOdredista
        {
            get
            {
                var opcije = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                };
                var zapisi = JsonSerializer.Deserialize<List<UlogaOdredisteZapis>>(
                    File.ReadAllText(Putanja("login_uloge_i_odredista.json")), opcije)!;
                return zapisi.Select(z => new object[] { z.Uloga, z.Url }).ToList();
            }
        }
    }

    [TestClass]
    public class AccountControllerTests
    {
        private const string Email = "korisnik@test.ba";
        private const string Lozinka = "Lozinka123!";

        private Mock<UserManager<Korisnik>> _userManager = null!;
        private Mock<SignInManager<Korisnik>> _signInManager = null!;
        private AccountController _controller = null!;
        private string _webRoot = null!;
        private Korisnik? _snimljenKorisnik;

        // ---------- Priprema i ciscenje ----------

        [TestInitialize]
        public void Postavi()
        {
            _webRoot = Path.Combine(Path.GetTempPath(), "earnlearn-tests-" + Guid.NewGuid());
            _userManager = NapraviUserManagerMock();
            _signInManager = NapraviSignInManagerMock(_userManager.Object);

            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.WebRootPath).Returns(_webRoot);

            var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _controller = new AccountController(
                _signInManager.Object, _userManager.Object, new ApplicationDbContext(opcije), env.Object)
            {
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
            };
        }

        [TestCleanup]
        public void Ocisti()
        {
            if (Directory.Exists(_webRoot))
            {
                Directory.Delete(_webRoot, recursive: true);
            }
        }

        // ---------- Helperi ----------

        private static Mock<UserManager<Korisnik>> NapraviUserManagerMock()
        {
            var store = new Mock<IUserStore<Korisnik>>();
            return new Mock<UserManager<Korisnik>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        private static Mock<SignInManager<Korisnik>> NapraviSignInManagerMock(UserManager<Korisnik> userManager)
        {
            return new Mock<SignInManager<Korisnik>>(
                userManager,
                new Mock<IHttpContextAccessor>().Object,
                new Mock<IUserClaimsPrincipalFactory<Korisnik>>().Object,
                null!, null!, null!, null!);
        }

        private static IFormFile NapraviFajl(string contentType, string imeFajla, long velicina)
        {
            var mock = new Mock<IFormFile>();
            mock.Setup(f => f.ContentType).Returns(contentType);
            mock.Setup(f => f.FileName).Returns(imeFajla);
            mock.Setup(f => f.Length).Returns(velicina);
            mock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            return mock.Object;
        }

        private void PostaviUspjesnuRegistraciju()
        {
            _userManager.Setup(m => m.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()))
                        .Callback<Korisnik, string>((k, _) => _snimljenKorisnik = k)
                        .ReturnsAsync(IdentityResult.Success);
        }

        private void PostaviNeuspjesnuRegistraciju(params string[] opisiGresaka)
        {
            var greske = opisiGresaka.Select(o => new IdentityError { Description = o }).ToArray();
            _userManager.Setup(m => m.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()))
                        .ReturnsAsync(IdentityResult.Failed(greske));
        }

        private void PostaviPrijavu(Microsoft.AspNetCore.Identity.SignInResult rezultat)
        {
            _signInManager.Setup(s => s.PasswordSignInAsync(Email, Lozinka, false, false))
                          .ReturnsAsync(rezultat);
        }

        // ================= LOGIN =================

        [TestMethod]
        [DynamicData(nameof(AccountTestData.PrazniLoginPodaci), typeof(AccountTestData))]
        public async Task Login_PrazanEmailIliSifra_VracaNaLoginSaPorukomOGresci(string email, string password)
        {
            var result = await _controller.Login(email, password, null) as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/Identity/Account/Login", result.Url);
            Assert.AreEqual("Unesite email i šifru.", (string?)_controller.TempData["LoginError"]);
            _signInManager.Verify(s => s.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public async Task Login_PogresniPodaci_VracaNaLoginSaPorukomOGresci()
        {
            PostaviPrijavu(Microsoft.AspNetCore.Identity.SignInResult.Failed);

            var result = await _controller.Login(Email, Lozinka, null) as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/Identity/Account/Login", result.Url);
            Assert.AreEqual("Pogrešan email ili šifra.", (string?)_controller.TempData["LoginError"]);
        }

        [TestMethod]
        [DynamicData(nameof(AccountTestData.UlogeIOdredista), typeof(AccountTestData))]
        public async Task Login_UspjesnaPrijava_PreusmjeravaNaDashboardOdgovarajuceUloge(Uloga uloga, string ocekivaniUrl)
        {
            PostaviPrijavu(Microsoft.AspNetCore.Identity.SignInResult.Success);
            _userManager.Setup(m => m.FindByEmailAsync(Email)).ReturnsAsync(new Korisnik { Uloga = uloga });

            var result = await _controller.Login(Email, Lozinka, null) as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(ocekivaniUrl, result.Url);
        }

        [TestMethod]
        public async Task Login_UspjesnaPrijavaNepoznataUlogaSaReturnUrl_PreusmjeravaNaReturnUrl()
        {
            PostaviPrijavu(Microsoft.AspNetCore.Identity.SignInResult.Success);
            _userManager.Setup(m => m.FindByEmailAsync(Email)).ReturnsAsync(new Korisnik { Uloga = (Uloga)999 });

            var result = await _controller.Login(Email, Lozinka, "/Home/About") as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/Home/About", result.Url);
        }

        [TestMethod]
        public async Task Login_UspjesnaPrijavaKorisnikNePostojiBezReturnUrl_PreusmjeravaNaPocetnu()
        {
            PostaviPrijavu(Microsoft.AspNetCore.Identity.SignInResult.Success);
            _userManager.Setup(m => m.FindByEmailAsync(Email)).ReturnsAsync((Korisnik?)null);

            var result = await _controller.Login(Email, Lozinka, null) as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/", result.Url);
        }

        // ================= REGISTER (GET) =================

        [TestMethod]
        public void Register_Get_VracaView()
        {
            var result = _controller.Register();

            Assert.IsInstanceOfType<ViewResult>(result);
        }

        // ================= REGISTER (POST) =================

        [TestMethod]
        public async Task Register_SystemManager_VracaNaRegisterSaPorukomOGresci()
        {
            var result = await _controller.Register("A", "B", Email, Lozinka, Uloga.SystemManager, null, null)
                as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Register", result.ActionName);
            Assert.AreEqual("Nije moguće registrovati System Manager nalog.", (string?)_controller.TempData["RegError"]);
            _userManager.Verify(m => m.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [DynamicData(nameof(AccountTestData.NedozvoljeniTipoviPriloga), typeof(AccountTestData))]
        public async Task Register_TutorNedozvoljenTipPriloga_VracaNaRegisterSaPorukomOGresci(string tip)
        {
            var fajl = NapraviFajl(tip, "prilog.xyz", 1024);

            var result = await _controller.Register("A", "B", Email, Lozinka, Uloga.Tutor, null, fajl)
                as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Register", result.ActionName);
            Assert.AreEqual("Dozvoljeni formati su JPG, PNG i PDF.", (string?)_controller.TempData["RegError"]);
            _userManager.Verify(m => m.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task Register_TutorPrevelikPrilog_VracaNaRegisterSaPorukomOGresci()
        {
            var fajl = NapraviFajl("application/pdf", "prilog.pdf", 5 * 1024 * 1024 + 1);

            var result = await _controller.Register("A", "B", Email, Lozinka, Uloga.Tutor, null, fajl)
                as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Register", result.ActionName);
            Assert.AreEqual("Prilog ne smije biti veći od 5MB.", (string?)_controller.TempData["RegError"]);
            _userManager.Verify(m => m.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [DynamicData(nameof(AccountTestData.DozvoljeniTipoviPriloga), typeof(AccountTestData))]
        public async Task Register_TutorDozvoljenPrilog_SnimaPutanjuPriloga(string tip, string ekstenzija)
        {
            PostaviUspjesnuRegistraciju();
            var fajl = NapraviFajl(tip, "ocjene" + ekstenzija, 1024);

            await _controller.Register("A", "B", Email, Lozinka, Uloga.Tutor, null, fajl);

            Assert.IsNotNull(_snimljenKorisnik);
            Assert.IsNotNull(_snimljenKorisnik.PrilogOcjene);
            Assert.IsTrue(_snimljenKorisnik.PrilogOcjene.StartsWith("/uploads/prilozi/"));
            Assert.IsTrue(_snimljenKorisnik.PrilogOcjene.EndsWith(ekstenzija));
        }

        [TestMethod]
        public async Task Register_TutorBezPriloga_SnimaKorisnikaBezPutanjePriloga()
        {
            PostaviUspjesnuRegistraciju();

            await _controller.Register("A", "B", Email, Lozinka, Uloga.Tutor, null, null);

            Assert.IsNotNull(_snimljenKorisnik);
            Assert.IsNull(_snimljenKorisnik.PrilogOcjene);
        }

        [TestMethod]
        public async Task Register_TutorPrazanPrilog_SnimaKorisnikaBezPutanjePriloga()
        {
            PostaviUspjesnuRegistraciju();
            var fajl = NapraviFajl("application/pdf", "prilog.pdf", 0);

            await _controller.Register("A", "B", Email, Lozinka, Uloga.Tutor, null, fajl);

            Assert.IsNotNull(_snimljenKorisnik);
            Assert.IsNull(_snimljenKorisnik.PrilogOcjene);
        }

        [TestMethod]
        public async Task Register_Student_SnimaBrojIndeksaIPreusmjeravaNaStudentDashboard()
        {
            PostaviUspjesnuRegistraciju();

            var result = await _controller.Register("A", "B", Email, Lozinka, Uloga.Student, 12345, null)
                as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/Student/Dashboard", result.Url);
            Assert.IsNotNull(_snimljenKorisnik);
            Assert.AreEqual((int?)12345, _snimljenKorisnik.BrojIndeksa);
            Assert.IsNull(_snimljenKorisnik.VerifikovanTutor);
            _signInManager.Verify(s => s.SignInAsync(It.IsAny<Korisnik>(), false, It.IsAny<string?>()), Times.Once);
        }

        [TestMethod]
        public async Task Register_Tutor_NeSnimaBrojIndeksaINijeVerifikovan()
        {
            PostaviUspjesnuRegistraciju();

            var result = await _controller.Register("A", "B", Email, Lozinka, Uloga.Tutor, 12345, null)
                as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/Tutor/Dashboard", result.Url);
            Assert.IsNotNull(_snimljenKorisnik);
            Assert.IsNull(_snimljenKorisnik.BrojIndeksa);
            Assert.AreEqual((bool?)false, _snimljenKorisnik.VerifikovanTutor);
        }

        [TestMethod]
        public async Task Register_NullImeIPrezime_SnimaPrazneStringove()
        {
            PostaviUspjesnuRegistraciju();

            await _controller.Register(null!, null!, Email, Lozinka, Uloga.Student, null, null);

            Assert.IsNotNull(_snimljenKorisnik);
            Assert.AreEqual("", _snimljenKorisnik.Ime);
            Assert.AreEqual("", _snimljenKorisnik.Prezime);
        }

        [TestMethod]
        public async Task Register_NepoznataUloga_PreusmjeravaNaPocetnu()
        {
            PostaviUspjesnuRegistraciju();

            var result = await _controller.Register("A", "B", Email, Lozinka, (Uloga)999, null, null)
                as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/", result.Url);
        }

        [TestMethod]
        public async Task Register_KreiranjeNeuspjesno_VracaNaRegisterSaSvimGreskama()
        {
            PostaviNeuspjesnuRegistraciju("Greska 1", "Greska 2");

            var result = await _controller.Register("A", "B", Email, Lozinka, Uloga.Student, null, null)
                as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Register", result.ActionName);
            Assert.AreEqual("Greska 1, Greska 2", (string?)_controller.TempData["RegError"]);
            _signInManager.Verify(s => s.SignInAsync(It.IsAny<Korisnik>(), It.IsAny<bool>(), It.IsAny<string?>()), Times.Never);
        }

        // ================= LOGOUT =================

        [TestMethod]
        public async Task Logout_Poziv_OdjavljujeKorisnikaIPreusmjeravaNaPocetnu()
        {
            var result = await _controller.Logout() as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/", result.Url);
            _signInManager.Verify(s => s.SignOutAsync(), Times.Once);
        }

        // ================= REGISTER IDENTITY =================

        [TestMethod]
        [DynamicData(nameof(AccountTestData.PrazniLoginPodaci), typeof(AccountTestData))]
        public async Task RegisterIdentity_PrazanEmailIliSifra_VracaNaRegisterSaPorukomOGresci(string email, string password)
        {
            var result = await _controller.RegisterIdentity(
                "A", "B", email, password, false, null, null, null, null) as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/Identity/Account/Register", result.Url);
            Assert.AreEqual("Unesite sve podatke.", (string?)_controller.TempData["RegError"]);
            _userManager.Verify(m => m.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task RegisterIdentity_ZelimTutorTrue_KreiraTutora()
        {
            PostaviUspjesnuRegistraciju();

            var result = await _controller.RegisterIdentity(
                "A", "B", Email, Lozinka, true, null, null, null, null) as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/Tutor/Dashboard", result.Url);
            Assert.IsNotNull(_snimljenKorisnik);
            Assert.AreEqual(Uloga.Tutor, _snimljenKorisnik.Uloga);
            Assert.AreEqual((bool?)false, _snimljenKorisnik.VerifikovanTutor);
        }

        [TestMethod]
        public async Task RegisterIdentity_UlogaJedan_KreiraTutora()
        {
            PostaviUspjesnuRegistraciju();

            await _controller.RegisterIdentity("A", "B", Email, Lozinka, false, null, null, 1, null);

            Assert.IsNotNull(_snimljenKorisnik);
            Assert.AreEqual(Uloga.Tutor, _snimljenKorisnik.Uloga);
        }

        [TestMethod]
        public async Task RegisterIdentity_BezOznakeTutora_KreiraStudenta()
        {
            PostaviUspjesnuRegistraciju();

            var result = await _controller.RegisterIdentity(
                "A", "B", Email, Lozinka, false, null, null, null, null) as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/Student/Dashboard", result.Url);
            Assert.IsNotNull(_snimljenKorisnik);
            Assert.AreEqual(Uloga.Student, _snimljenKorisnik.Uloga);
            Assert.IsNull(_snimljenKorisnik.VerifikovanTutor);
        }

        [TestMethod]
        public async Task RegisterIdentity_NullImeIPrezime_SnimaPrazneStringove()
        {
            PostaviUspjesnuRegistraciju();

            await _controller.RegisterIdentity(null!, null!, Email, Lozinka, false, null, null, null, null);

            Assert.IsNotNull(_snimljenKorisnik);
            Assert.AreEqual("", _snimljenKorisnik.Ime);
            Assert.AreEqual("", _snimljenKorisnik.Prezime);
        }

        [TestMethod]
        [DynamicData(nameof(AccountTestData.DozvoljeniTipoviPriloga), typeof(AccountTestData))]
        public async Task RegisterIdentity_TutorDozvoljenPrilog_SnimaPutanjuPriloga(string tip, string ekstenzija)
        {
            PostaviUspjesnuRegistraciju();
            var fajl = NapraviFajl(tip, "ocjene" + ekstenzija, 1024);

            await _controller.RegisterIdentity("A", "B", Email, Lozinka, true, null, fajl, null, null);

            Assert.IsNotNull(_snimljenKorisnik);
            Assert.IsNotNull(_snimljenKorisnik.PrilogOcjene);
            Assert.IsTrue(_snimljenKorisnik.PrilogOcjene.StartsWith("/uploads/prilozi/"));
            Assert.IsTrue(_snimljenKorisnik.PrilogOcjene.EndsWith(ekstenzija));
        }

        [TestMethod]
        [DynamicData(nameof(AccountTestData.NedozvoljeniTipoviPriloga), typeof(AccountTestData))]
        public async Task RegisterIdentity_TutorNedozvoljenTipPriloga_IgnorisePrilog(string tip)
        {
            PostaviUspjesnuRegistraciju();
            var fajl = NapraviFajl(tip, "prilog.xyz", 1024);

            await _controller.RegisterIdentity("A", "B", Email, Lozinka, true, null, fajl, null, null);

            Assert.IsNotNull(_snimljenKorisnik);
            Assert.IsNull(_snimljenKorisnik.PrilogOcjene);
        }

        [TestMethod]
        public async Task RegisterIdentity_StudentSaPrilogom_IgnorisePrilog()
        {
            PostaviUspjesnuRegistraciju();
            var fajl = NapraviFajl("application/pdf", "prilog.pdf", 1024);

            await _controller.RegisterIdentity("A", "B", Email, Lozinka, false, null, fajl, null, null);

            Assert.IsNotNull(_snimljenKorisnik);
            Assert.IsNull(_snimljenKorisnik.PrilogOcjene);
        }

        [TestMethod]
        public async Task RegisterIdentity_TutorSaPredmetima_SnimaPredmeteNaCekanju()
        {
            PostaviUspjesnuRegistraciju();

            await _controller.RegisterIdentity(
                "A", "B", Email, Lozinka, true, new List<int> { 1, 2, 3 }, null, null, null);

            Assert.IsNotNull(_snimljenKorisnik);
            Assert.AreEqual("[1,2,3]", _snimljenKorisnik.PredmetiNaCekanjuJson);
            _userManager.Verify(m => m.UpdateAsync(_snimljenKorisnik), Times.Once);
        }

        [TestMethod]
        public async Task RegisterIdentity_TutorBezPredmeta_NeAzuriraKorisnika()
        {
            PostaviUspjesnuRegistraciju();

            await _controller.RegisterIdentity("A", "B", Email, Lozinka, true, null, null, null, null);

            _userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task RegisterIdentity_TutorSaPraznomListomPredmeta_NeAzuriraKorisnika()
        {
            PostaviUspjesnuRegistraciju();

            await _controller.RegisterIdentity("A", "B", Email, Lozinka, true, new List<int>(), null, null, null);

            _userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task RegisterIdentity_StudentSaPredmetima_NeAzuriraKorisnika()
        {
            PostaviUspjesnuRegistraciju();

            await _controller.RegisterIdentity(
                "A", "B", Email, Lozinka, false, new List<int> { 1, 2 }, null, null, null);

            _userManager.Verify(m => m.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
        }

        [TestMethod]
        public async Task RegisterIdentity_KreiranjeNeuspjesno_VracaNaRegisterSaSvimGreskama()
        {
            PostaviNeuspjesnuRegistraciju("Greska 1", "Greska 2");

            var result = await _controller.RegisterIdentity(
                "A", "B", Email, Lozinka, false, null, null, null, null) as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/Identity/Account/Register", result.Url);
            Assert.AreEqual("Greska 1, Greska 2", (string?)_controller.TempData["RegError"]);
            _signInManager.Verify(s => s.SignInAsync(It.IsAny<Korisnik>(), It.IsAny<bool>(), It.IsAny<string?>()), Times.Never);
        }
    }
}