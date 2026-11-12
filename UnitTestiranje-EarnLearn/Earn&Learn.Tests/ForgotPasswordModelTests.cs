using Earn_Learn.Areas.Identity.Pages.Account;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Moq;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Earn_Learn.Tests
{
    public class ForgotPasswordZapis
    {
        public string Email { get; set; } = string.Empty;
        public bool KorisnikPostoji { get; set; }
    }

    public static class ForgotPasswordTestData
    {
        private static string Putanja(string imeFajla) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", imeFajla);

        // CSV: email, validan
        public static IEnumerable<object[]> EmailAdreseIzCsv =>
            File.ReadLines(Putanja("forgot_password_emailovi.csv"))
                .Skip(1)
                .Select(linija => linija.Split(','))
                .Select(k => new object[] { k[0], bool.Parse(k[1]) })
                .ToList();

        // JSON: email, korisnikPostoji
        public static IEnumerable<object[]> ZahtjeviIzJsona
        {
            get
            {
                var opcije = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var zapisi = JsonSerializer.Deserialize<List<ForgotPasswordZapis>>(
                    File.ReadAllText(Putanja("forgot_password_zahtjevi.json")), opcije)!;
                return zapisi.Select(z => new object[] { z.Email, z.KorisnikPostoji }).ToList();
            }
        }
    }

    [TestClass]
    public class ForgotPasswordModelTests
    {
        private Mock<UserManager<Korisnik>> _userManager = null!;
        private ForgotPasswordModel _model = null!;

        // ---------- Priprema ----------

        [TestInitialize]
        public void Postavi()
        {
            var store = new Mock<IUserStore<Korisnik>>();
            _userManager = new Mock<UserManager<Korisnik>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            var actionContext = new ActionContext(
                new DefaultHttpContext(), new RouteData(), new PageActionDescriptor());

            _model = new ForgotPasswordModel(_userManager.Object)
            {
                PageContext = new PageContext(actionContext)
            };
        }

        private static IList<ValidationResult> Validiraj(ForgotPasswordModel.InputModel input)
        {
            var rezultati = new List<ValidationResult>();
            Validator.TryValidateObject(input, new ValidationContext(input), rezultati, true);
            return rezultati;
        }

        // ---------- Zadane vrijednosti ----------

        [TestMethod]
        public void Konstruktor_NovaInstanca_InputJeInicijaliziranSaPraznimEmailom()
        {
            Assert.IsNotNull(_model.Input);
            Assert.AreEqual(string.Empty, _model.Input.Email);
        }

        [TestMethod]
        public void Input_PostavljenaVrijednost_VracaIstiObjekat()
        {
            var input = new ForgotPasswordModel.InputModel { Email = "korisnik@test.ba" };

            _model.Input = input;

            Assert.AreSame(input, _model.Input);
        }

        // ---------- OnGet ----------

        [TestMethod]
        public void OnGet_Poziv_NeMijenjaInput()
        {
            _model.OnGet();

            Assert.AreEqual(string.Empty, _model.Input.Email);
        }

        // ---------- OnPostAsync ----------

        [TestMethod]
        public async Task OnPostAsync_NeispravanModelState_VracaPage()
        {
            _model.ModelState.AddModelError("Input.Email", "Email je obavezan.");

            var result = await _model.OnPostAsync();

            Assert.IsInstanceOfType<PageResult>(result);
        }

        [TestMethod]
        public async Task OnPostAsync_NeispravanModelState_NePozivaUserManager()
        {
            _model.ModelState.AddModelError("Input.Email", "Email je obavezan.");

            await _model.OnPostAsync();

            _userManager.Verify(m => m.FindByEmailAsync(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task OnPostAsync_ValidanModelState_PreusmjeravaNaPotvrdu()
        {
            _model.Input.Email = "korisnik@test.ba";

            var result = await _model.OnPostAsync() as RedirectToPageResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("ForgotPasswordConfirmation", result.PageName);
        }

        [TestMethod]
        public async Task OnPostAsync_ValidanModelState_TraziKorisnikaPoEmailuTacnoJednom()
        {
            _model.Input.Email = "korisnik@test.ba";

            await _model.OnPostAsync();

            _userManager.Verify(m => m.FindByEmailAsync("korisnik@test.ba"), Times.Once);
        }

        [TestMethod]
        [DynamicData(nameof(ForgotPasswordTestData.ZahtjeviIzJsona), typeof(ForgotPasswordTestData))]
        public async Task OnPostAsync_PodaciIzJsonFajla_UvijekPreusmjeravaNaIstuStranicu(
            string email, bool korisnikPostoji)
        {
            _model.Input.Email = email;
            _userManager.Setup(m => m.FindByEmailAsync(email))
                        .ReturnsAsync(korisnikPostoji ? new Korisnik() : null);

            var result = await _model.OnPostAsync() as RedirectToPageResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("ForgotPasswordConfirmation", result.PageName);
        }

        // ---------- Validacija InputModel ----------

        [TestMethod]
        public void InputModel_PrazanEmail_VracaPorukuObavezno()
        {
            var input = new ForgotPasswordModel.InputModel { Email = "" };

            var rezultati = Validiraj(input);

            Assert.IsTrue(rezultati.Any(r => r.ErrorMessage == "Email je obavezan."));
        }

        [TestMethod]
        public void InputModel_NeispravanFormat_VracaPorukuONeispravnomFormatu()
        {
            var input = new ForgotPasswordModel.InputModel { Email = "bezmaila" };

            var rezultati = Validiraj(input);

            Assert.IsTrue(rezultati.Any(r => r.ErrorMessage == "Neispravan format emaila."));
        }

        [TestMethod]
        [DynamicData(nameof(ForgotPasswordTestData.EmailAdreseIzCsv), typeof(ForgotPasswordTestData))]
        public void InputModel_PodaciIzCsvFajla_ValidacijaVracaOcekivano(string email, bool ocekivanoValidan)
        {
            var input = new ForgotPasswordModel.InputModel { Email = email };

            var rezultati = Validiraj(input);

            Assert.AreEqual(ocekivanoValidan, rezultati.Count == 0);
        }
    }
}
