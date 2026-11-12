using Earn_Learn.Areas.Identity.Pages.Account;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Earn_Learn.Tests
{
    public class LoginValidacijaZapis
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public bool Validno { get; set; }
        public int BrojGresaka { get; set; }
    }

    public static class LoginModelTestData
    {
        private static string Putanja(string imeFajla) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", imeFajla);

        // CSV: email, password, rezultat
        public static IEnumerable<object[]> PrijaveIzCsv =>
            File.ReadLines(Putanja("login_model_prijave.csv"))
                .Skip(1)
                .Where(linija => !string.IsNullOrWhiteSpace(linija))
                .Select(linija => linija.Split(','))
                .Select(k => new object[] { k[0].Trim(), k[1].Trim(), k[2].Trim() })
                .ToList();

        // JSON: email, password, validno, brojGresaka
        public static IEnumerable<object[]> ValidacijaIzJsona
        {
            get
            {
                var opcije = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var zapisi = JsonSerializer.Deserialize<List<LoginValidacijaZapis>>(
                    File.ReadAllText(Putanja("login_model_validacija.json")), opcije)!;
                return zapisi.Select(z => new object[]
                {
                    z.Email, z.Password, z.Validno, z.BrojGresaka
                }).ToList();
            }
        }
    }

    [TestClass]
    public class LoginModelTests
    {
        // ---------- Pomocne metode ----------

        private static Mock<SignInManager<Korisnik>> KreirajSignInManagerMock()
        {
            var store = new Mock<IUserStore<Korisnik>>();
            var userManager = new Mock<UserManager<Korisnik>>(
                store.Object, null, null, null, null, null, null, null, null);
            var contextAccessor = new Mock<IHttpContextAccessor>();
            var claimsFactory = new Mock<IUserClaimsPrincipalFactory<Korisnik>>();

            return new Mock<SignInManager<Korisnik>>(
                userManager.Object, contextAccessor.Object, claimsFactory.Object,
                null, null, null, null);
        }

        private static LoginModel KreirajModel(
            Mock<SignInManager<Korisnik>> signInManager,
            string email = "student@test.ba",
            string password = "Lozinka123!")
        {
            var urlHelper = new Mock<IUrlHelper>();
            urlHelper.Setup(u => u.Content("~/")).Returns("/");

            return new LoginModel(signInManager.Object)
            {
                PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
                Url = urlHelper.Object,
                Input = new LoginModel.InputModel { Email = email, Password = password }
            };
        }

        private static void PostaviRezultat(
            Mock<SignInManager<Korisnik>> signInManager, SignInResult rezultat)
        {
            signInManager
                .Setup(s => s.PasswordSignInAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(rezultat);
        }

        private static SignInResult IzNaziva(string naziv) => naziv switch
        {
            "Success" => SignInResult.Success,
            "Failed" => SignInResult.Failed,
            "LockedOut" => SignInResult.LockedOut,
            "NotAllowed" => SignInResult.NotAllowed,
            "TwoFactorRequired" => SignInResult.TwoFactorRequired,
            _ => throw new ArgumentException("Nepoznat rezultat: " + naziv)
        };

        // ---------- Konstruktor ----------

        [TestMethod]
        public void Konstruktor_NovaInstanca_InputJeNovaInstancaSaPraznimStringovima()
        {
            var model = new LoginModel(KreirajSignInManagerMock().Object);

            Assert.IsNotNull(model.Input);
            Assert.AreEqual("", model.Input.Email);
            Assert.AreEqual("", model.Input.Password);
        }

        [TestMethod]
        public void Konstruktor_NovaInstanca_ReturnUrlJeNull()
        {
            var model = new LoginModel(KreirajSignInManagerMock().Object);

            Assert.IsNull(model.ReturnUrl);
        }

        // ---------- OnGet ----------

        [TestMethod]
        public void OnGet_ProslijedjenReturnUrl_PostavljaReturnUrl()
        {
            var model = KreirajModel(KreirajSignInManagerMock());

            model.OnGet("/Predmeti/Index");

            Assert.AreEqual("/Predmeti/Index", model.ReturnUrl);
        }

        [TestMethod]
        public void OnGet_BezReturnUrl_ReturnUrlJeNull()
        {
            var model = KreirajModel(KreirajSignInManagerMock());

            model.OnGet();

            Assert.IsNull(model.ReturnUrl);
        }

        // ---------- OnPostAsync: uspjesna prijava ----------

        [TestMethod]
        public async Task OnPostAsync_UspjesnaPrijavaSaReturnUrl_VracaLocalRedirectNaReturnUrl()
        {
            var signInManager = KreirajSignInManagerMock();
            PostaviRezultat(signInManager, SignInResult.Success);
            var model = KreirajModel(signInManager);

            var rezultat = await model.OnPostAsync("/Predmeti/Index");

            Assert.IsInstanceOfType(rezultat, typeof(LocalRedirectResult));
            Assert.AreEqual("/Predmeti/Index", ((LocalRedirectResult)rezultat).Url);
        }

        [TestMethod]
        public async Task OnPostAsync_UspjesnaPrijavaBezReturnUrl_VracaLocalRedirectNaPocetnu()
        {
            var signInManager = KreirajSignInManagerMock();
            PostaviRezultat(signInManager, SignInResult.Success);
            var model = KreirajModel(signInManager);

            var rezultat = await model.OnPostAsync();

            Assert.IsInstanceOfType(rezultat, typeof(LocalRedirectResult));
            Assert.AreEqual("/", ((LocalRedirectResult)rezultat).Url);
        }

        [TestMethod]
        public async Task OnPostAsync_ValidniPodaci_ProslijedjujeCredentialsSignInManageru()
        {
            var signInManager = KreirajSignInManagerMock();
            PostaviRezultat(signInManager, SignInResult.Success);
            var model = KreirajModel(signInManager, "ana@test.ba", "Tajna456!");

            await model.OnPostAsync();

            signInManager.Verify(s => s.PasswordSignInAsync(
                "ana@test.ba", "Tajna456!", false, false), Times.Once);
        }

        // ---------- OnPostAsync: neuspjesna prijava ----------

        [TestMethod]
        public async Task OnPostAsync_NeuspjesnaPrijava_VracaPageIDodajeGresku()
        {
            var signInManager = KreirajSignInManagerMock();
            PostaviRezultat(signInManager, SignInResult.Failed);
            var model = KreirajModel(signInManager);

            var rezultat = await model.OnPostAsync();

            Assert.IsInstanceOfType(rezultat, typeof(PageResult));
            Assert.IsFalse(model.ModelState.IsValid);
            var greske = model.ModelState[string.Empty]!.Errors;
            Assert.AreEqual(1, greske.Count);
            Assert.AreEqual("Pogrešan email ili šifra.", greske[0].ErrorMessage);
        }

        // ---------- OnPostAsync: neispravan ModelState ----------

        [TestMethod]
        public async Task OnPostAsync_NeispravanModelState_VracaPageBezPozivaSignInManagera()
        {
            var signInManager = KreirajSignInManagerMock();
            var model = KreirajModel(signInManager);
            model.ModelState.AddModelError("Input.Email", "Email je obavezan.");

            var rezultat = await model.OnPostAsync();

            Assert.IsInstanceOfType(rezultat, typeof(PageResult));
            signInManager.Verify(s => s.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()),
                Times.Never);
        }

        // ---------- Izuzeci ----------

        [TestMethod]
        public async Task OnPostAsync_SignInManagerBacaIzuzetak_IzuzetakSePropagira()
        {
            var signInManager = KreirajSignInManagerMock();
            signInManager
                .Setup(s => s.PasswordSignInAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ThrowsAsync(new InvalidOperationException("Greska u bazi."));
            var model = KreirajModel(signInManager);

            try
            {
                await model.OnPostAsync();
                Assert.Fail("Ocekivan je InvalidOperationException.");
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual("Greska u bazi.", ex.Message);
            }
        }

        // ---------- Data-driven: CSV ----------

        [TestMethod]
        [DynamicData(nameof(LoginModelTestData.PrijaveIzCsv), typeof(LoginModelTestData))]
        public async Task OnPostAsync_RezultatiPrijaveIzCsvFajla_VracajuOcekivaniOdgovor(
            string email, string password, string nazivRezultata)
        {
            var signInManager = KreirajSignInManagerMock();
            PostaviRezultat(signInManager, IzNaziva(nazivRezultata));
            var model = KreirajModel(signInManager, email, password);

            var rezultat = await model.OnPostAsync();

            if (nazivRezultata == "Success")
            {
                Assert.IsInstanceOfType(rezultat, typeof(LocalRedirectResult));
            }
            else
            {
                Assert.IsInstanceOfType(rezultat, typeof(PageResult));
                Assert.IsFalse(model.ModelState.IsValid);
            }
            signInManager.Verify(s => s.PasswordSignInAsync(
                email, password, false, false), Times.Once);
        }

        // ---------- Data-driven: JSON ----------

        [TestMethod]
        [DynamicData(nameof(LoginModelTestData.ValidacijaIzJsona), typeof(LoginModelTestData))]
        public void InputModel_PodaciIzJsonFajla_ValidacijaDajeOcekivaniBrojGresaka(
            string email, string password, bool validno, int brojGresaka)
        {
            var input = new LoginModel.InputModel { Email = email, Password = password };
            var greske = new List<ValidationResult>();

            var jeValidan = Validator.TryValidateObject(
                input, new ValidationContext(input), greske, validateAllProperties: true);

            Assert.AreEqual(validno, jeValidan);
            Assert.AreEqual(brojGresaka, greske.Count);
        }
    }
}
