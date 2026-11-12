using Earn_Learn.Areas.Identity.Pages.Account;
using Earn_Learn.Data;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Earn_Learn.Tests
{
    public class RegisterValidacijaZapis
    {
        public string Ime { get; set; } = "";
        public string Prezime { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";
        public bool Validno { get; set; }
        public int BrojGresaka { get; set; }
    }

    public static class RegisterModelTestData
    {
        private static string Putanja(string imeFajla) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", imeFajla);

        // CSV: ime, prezime, email, zelimBitiTutor, ocekivanaUloga, ocekivanaVerifikacija
        public static IEnumerable<object?[]> UlogeIzCsv =>
            File.ReadLines(Putanja("register_model_uloge.csv"))
                .Skip(1)
                .Where(linija => !string.IsNullOrWhiteSpace(linija))
                .Select(linija => linija.Split(','))
                .Select(k => new object?[]
                {
                    k[0].Trim(),
                    k[1].Trim(),
                    k[2].Trim(),
                    bool.Parse(k[3].Trim()),
                    Enum.Parse<Uloga>(k[4].Trim()),
                    k[5].Trim() == "null" ? (bool?)null : bool.Parse(k[5].Trim())
                })
                .ToList();

        // JSON: ime, prezime, email, password, confirmPassword, validno, brojGresaka
        public static IEnumerable<object[]> ValidacijaIzJsona
        {
            get
            {
                var opcije = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var zapisi = JsonSerializer.Deserialize<List<RegisterValidacijaZapis>>(
                    File.ReadAllText(Putanja("register_model_validacija.json")), opcije)!;
                return zapisi.Select(z => new object[]
                {
                    z.Ime, z.Prezime, z.Email, z.Password, z.ConfirmPassword,
                    z.Validno, z.BrojGresaka
                }).ToList();
            }
        }
    }

    [TestClass]
    public class RegisterModelTests
    {
        // ---------- Pomocne klase i metode ----------

        private sealed class Okruzenje : IDisposable
        {
            public Mock<UserManager<Korisnik>> UserManager { get; }
            public Mock<SignInManager<Korisnik>> SignInManager { get; }
            public ApplicationDbContext Context { get; }
            public string WebRoot { get; }
            public RegisterModel Model { get; }
            public Korisnik? Kreiran { get; private set; }

            public Okruzenje()
            {
                var store = new Mock<IUserStore<Korisnik>>();
                UserManager = new Mock<UserManager<Korisnik>>(
                    store.Object, null, null, null, null, null, null, null, null);
                UserManager
                    .Setup(u => u.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()))
                    .Callback<Korisnik, string>((k, p) => Kreiran = k)
                    .ReturnsAsync(IdentityResult.Success);

                SignInManager = new Mock<SignInManager<Korisnik>>(
                    UserManager.Object,
                    new Mock<IHttpContextAccessor>().Object,
                    new Mock<IUserClaimsPrincipalFactory<Korisnik>>().Object,
                    null, null, null, null);
                SignInManager
                    .Setup(s => s.SignInAsync(
                        It.IsAny<Korisnik>(), It.IsAny<bool>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask);

                var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .Options;
                Context = new ApplicationDbContext(opcije);

                WebRoot = Path.Combine(Path.GetTempPath(), "earnlearn_tests_" + Guid.NewGuid());
                var env = new Mock<IWebHostEnvironment>();
                env.Setup(e => e.WebRootPath).Returns(WebRoot);

                var urlHelper = new Mock<IUrlHelper>();
                urlHelper.Setup(u => u.Content("~/")).Returns("/");

                Model = new RegisterModel(
                    UserManager.Object, SignInManager.Object, Context, env.Object)
                {
                    PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
                    Url = urlHelper.Object,
                    Input = new RegisterModel.InputModel
                    {
                        Ime = "Amar",
                        Prezime = "Hodzic",
                        Email = "ahodzic1@etf.unsa.ba",
                        Password = "Lozinka1",
                        ConfirmPassword = "Lozinka1"
                    }
                };
            }

            public string Prilozi => Path.Combine(WebRoot, "uploads", "prilozi");

            public void Dispose()
            {
                Context.Dispose();
                if (Directory.Exists(WebRoot))
                    Directory.Delete(WebRoot, recursive: true);
            }
        }

        private static Mock<IFormFile> KreirajPrilog(
            string imeFajla = "ocjene.pdf", long duzina = 2048)
        {
            var prilog = new Mock<IFormFile>();
            prilog.Setup(f => f.FileName).Returns(imeFajla);
            prilog.Setup(f => f.Length).Returns(duzina);
            prilog.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);
            return prilog;
        }

        // ---------- Konstruktor ----------

        [TestMethod]
        public void Konstruktor_NovaInstanca_InputImaZadaneVrijednosti()
        {
            using var ok = new Okruzenje();
            var input = new RegisterModel.InputModel();

            Assert.AreEqual("", input.Ime);
            Assert.AreEqual("", input.Prezime);
            Assert.AreEqual("", input.Email);
            Assert.AreEqual("", input.Password);
            Assert.AreEqual("", input.ConfirmPassword);
            Assert.IsFalse(input.ZelimBitiTutor);
            Assert.AreEqual(0, input.OdabraniPredmeti.Count);
            Assert.IsNull(input.PrilogOcjene);
        }

        [TestMethod]
        public void Konstruktor_NovaInstanca_ReturnUrlJeNullAListaPredmetaPrazna()
        {
            using var ok = new Okruzenje();

            Assert.IsNull(ok.Model.ReturnUrl);
            Assert.AreEqual(0, ok.Model.PredmetiSelectList.Count);
        }

        // ---------- OnGetAsync ----------

        [TestMethod]
        public async Task OnGetAsync_ProslijedjenReturnUrl_PostavljaReturnUrl()
        {
            using var ok = new Okruzenje();

            await ok.Model.OnGetAsync("/Predmeti/Index");

            Assert.AreEqual("/Predmeti/Index", ok.Model.ReturnUrl);
        }

        [TestMethod]
        public async Task OnGetAsync_PredmetiUBazi_PunaListuSortiranuPoNazivu()
        {
            using var ok = new Okruzenje();
            ok.Context.Predmet.Add(new Predmet { id = 1, naziv = "Matematika" });
            ok.Context.Predmet.Add(new Predmet { id = 2, naziv = "Algebra" });
            await ok.Context.SaveChangesAsync();

            await ok.Model.OnGetAsync();

            Assert.AreEqual(2, ok.Model.PredmetiSelectList.Count);
            Assert.AreEqual("Algebra", ok.Model.PredmetiSelectList[0].Text);
            Assert.AreEqual("2", ok.Model.PredmetiSelectList[0].Value);
            Assert.AreEqual("Matematika", ok.Model.PredmetiSelectList[1].Text);
            Assert.AreEqual("1", ok.Model.PredmetiSelectList[1].Value);
        }

        [TestMethod]
        public async Task OnGetAsync_PrazanSkupPredmeta_ListaJePrazna()
        {
            using var ok = new Okruzenje();

            await ok.Model.OnGetAsync();

            Assert.AreEqual(0, ok.Model.PredmetiSelectList.Count);
        }

        // ---------- OnPostAsync: uspjesna registracija ----------

        [TestMethod]
        public async Task OnPostAsync_RegistracijaStudenta_KreiraKorisnikaPrijavljujeGaIPreusmjerava()
        {
            using var ok = new Okruzenje();

            var rezultat = await ok.Model.OnPostAsync();

            Assert.IsInstanceOfType(rezultat, typeof(LocalRedirectResult));
            Assert.AreEqual("/", ((LocalRedirectResult)rezultat).Url);
            Assert.IsNotNull(ok.Kreiran);
            Assert.AreEqual("Amar", ok.Kreiran!.Ime);
            Assert.AreEqual("Hodzic", ok.Kreiran.Prezime);
            Assert.AreEqual("ahodzic1@etf.unsa.ba", ok.Kreiran.Email);
            Assert.AreEqual("ahodzic1@etf.unsa.ba", ok.Kreiran.UserName);
            Assert.AreEqual(Uloga.Student, ok.Kreiran.Uloga);
            Assert.IsNull(ok.Kreiran.VerifikovanTutor);
            Assert.IsNull(ok.Kreiran.PrilogOcjene);
            Assert.AreNotEqual(default(DateTime), ok.Kreiran.DatumRegistracije);
            ok.UserManager.Verify(u => u.CreateAsync(
                It.IsAny<Korisnik>(), "Lozinka1"), Times.Once);
            ok.SignInManager.Verify(s => s.SignInAsync(
                ok.Kreiran, false, It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public async Task OnPostAsync_RegistracijaTutora_PostavljaUloguTutorINeverifikovan()
        {
            using var ok = new Okruzenje();
            ok.Model.Input.ZelimBitiTutor = true;

            await ok.Model.OnPostAsync();

            Assert.AreEqual(Uloga.Tutor, ok.Kreiran!.Uloga);
            Assert.AreEqual(false, ok.Kreiran.VerifikovanTutor);
            Assert.IsNull(ok.Kreiran.PrilogOcjene);
        }

        [TestMethod]
        public async Task OnPostAsync_ProslijedjenReturnUrl_PreusmjeravaNaReturnUrl()
        {
            using var ok = new Okruzenje();

            var rezultat = await ok.Model.OnPostAsync("/Predmeti/Index");

            Assert.IsInstanceOfType(rezultat, typeof(LocalRedirectResult));
            Assert.AreEqual("/Predmeti/Index", ((LocalRedirectResult)rezultat).Url);
        }

        // ---------- OnPostAsync: prilog ----------

        [TestMethod]
        public async Task OnPostAsync_TutorSaPrilogom_SpremaFajlIPostavljaPutanju()
        {
            using var ok = new Okruzenje();
            ok.Model.Input.ZelimBitiTutor = true;
            var prilog = KreirajPrilog("ocjene.pdf");
            ok.Model.Input.PrilogOcjene = prilog.Object;

            await ok.Model.OnPostAsync();

            Assert.IsNotNull(ok.Kreiran!.PrilogOcjene);
            StringAssert.StartsWith(ok.Kreiran.PrilogOcjene, "/uploads/prilozi/");
            StringAssert.EndsWith(ok.Kreiran.PrilogOcjene, ".pdf");
            var fajlovi = Directory.GetFiles(ok.Prilozi);
            Assert.AreEqual(1, fajlovi.Length);
            Assert.AreEqual(".pdf", Path.GetExtension(fajlovi[0]));
            prilog.Verify(f => f.CopyToAsync(
                It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task OnPostAsync_StudentSaPrilogom_PrilogSeIgnorise()
        {
            using var ok = new Okruzenje();
            var prilog = KreirajPrilog();
            ok.Model.Input.PrilogOcjene = prilog.Object;

            await ok.Model.OnPostAsync();

            Assert.IsNull(ok.Kreiran!.PrilogOcjene);
            Assert.IsFalse(Directory.Exists(ok.Prilozi));
            prilog.Verify(f => f.CopyToAsync(
                It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task OnPostAsync_TutorSaPraznimPrilogom_PrilogSeIgnorise()
        {
            using var ok = new Okruzenje();
            ok.Model.Input.ZelimBitiTutor = true;
            ok.Model.Input.PrilogOcjene = KreirajPrilog(duzina: 0).Object;

            await ok.Model.OnPostAsync();

            Assert.IsNull(ok.Kreiran!.PrilogOcjene);
            Assert.IsFalse(Directory.Exists(ok.Prilozi));
        }

        // ---------- OnPostAsync: odabrani predmeti ----------

        [TestMethod]
        public async Task OnPostAsync_TutorSaOdabranimPredmetima_SpremaVezeUBazu()
        {
            using var ok = new Okruzenje();
            ok.Model.Input.ZelimBitiTutor = true;
            ok.Model.Input.OdabraniPredmeti = new List<int> { 1, 2 };

            await ok.Model.OnPostAsync();

            var veze = await ok.Context.KorisnikPredmet.ToListAsync();
            Assert.AreEqual(2, veze.Count);
            Assert.IsTrue(veze.All(v => v.idKorisnika == ok.Kreiran!.Id));
            CollectionAssert.AreEquivalent(
                new List<int> { 1, 2 }, veze.Select(v => v.idPredmeta).ToList());
        }

        [TestMethod]
        public async Task OnPostAsync_StudentSaOdabranimPredmetima_NistaSeNeSpremaUBazu()
        {
            using var ok = new Okruzenje();
            ok.Model.Input.OdabraniPredmeti = new List<int> { 1, 2 };

            await ok.Model.OnPostAsync();

            Assert.AreEqual(0, await ok.Context.KorisnikPredmet.CountAsync());
        }

        [TestMethod]
        public async Task OnPostAsync_TutorBezOdabranihPredmeta_NistaSeNeSpremaUBazu()
        {
            using var ok = new Okruzenje();
            ok.Model.Input.ZelimBitiTutor = true;

            await ok.Model.OnPostAsync();

            Assert.AreEqual(0, await ok.Context.KorisnikPredmet.CountAsync());
        }

        // ---------- OnPostAsync: neuspjesna registracija ----------

        [TestMethod]
        public async Task OnPostAsync_CreateAsyncNeuspjesan_VracaPageIDodajeSveGreske()
        {
            using var ok = new Okruzenje();
            ok.UserManager
                .Setup(u => u.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "Email je zauzet." },
                    new IdentityError { Description = "Sifra je preslaba." }));
            ok.Context.Predmet.Add(new Predmet { id = 1, naziv = "Fizika" });
            await ok.Context.SaveChangesAsync();

            var rezultat = await ok.Model.OnPostAsync();

            Assert.IsInstanceOfType(rezultat, typeof(PageResult));
            var greske = ok.Model.ModelState[string.Empty]!.Errors;
            Assert.AreEqual(2, greske.Count);
            Assert.AreEqual("Email je zauzet.", greske[0].ErrorMessage);
            Assert.AreEqual("Sifra je preslaba.", greske[1].ErrorMessage);
            Assert.AreEqual(1, ok.Model.PredmetiSelectList.Count);
            ok.SignInManager.Verify(s => s.SignInAsync(
                It.IsAny<Korisnik>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task OnPostAsync_NeispravanModelState_VracaPageBezKreiranjaKorisnika()
        {
            using var ok = new Okruzenje();
            ok.Model.ModelState.AddModelError("Input.Ime", "Ime je obavezno.");
            ok.Context.Predmet.Add(new Predmet { id = 1, naziv = "Fizika" });
            await ok.Context.SaveChangesAsync();

            var rezultat = await ok.Model.OnPostAsync();

            Assert.IsInstanceOfType(rezultat, typeof(PageResult));
            Assert.AreEqual(1, ok.Model.PredmetiSelectList.Count);
            ok.UserManager.Verify(u => u.CreateAsync(
                It.IsAny<Korisnik>(), It.IsAny<string>()), Times.Never);
            ok.SignInManager.Verify(s => s.SignInAsync(
                It.IsAny<Korisnik>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
        }

        // ---------- Izuzeci ----------

        [TestMethod]
        public async Task OnPostAsync_CreateAsyncBacaIzuzetak_IzuzetakSePropagira()
        {
            using var ok = new Okruzenje();
            ok.UserManager
                .Setup(u => u.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Greska u bazi."));

            try
            {
                await ok.Model.OnPostAsync();
                Assert.Fail("Ocekivan je InvalidOperationException.");
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual("Greska u bazi.", ex.Message);
            }
        }

        [TestMethod]
        public async Task OnPostAsync_SpremanjePrilogaBacaIOException_KorisnikSeNeKreira()
        {
            using var ok = new Okruzenje();
            ok.Model.Input.ZelimBitiTutor = true;
            var prilog = KreirajPrilog();
            prilog.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new IOException("Disk je pun."));
            ok.Model.Input.PrilogOcjene = prilog.Object;

            try
            {
                await ok.Model.OnPostAsync();
                Assert.Fail("Ocekivan je IOException.");
            }
            catch (IOException ex)
            {
                Assert.AreEqual("Disk je pun.", ex.Message);
            }

            ok.UserManager.Verify(u => u.CreateAsync(
                It.IsAny<Korisnik>(), It.IsAny<string>()), Times.Never);
        }

        // ---------- Data-driven: CSV ----------

        [TestMethod]
        [DynamicData(nameof(RegisterModelTestData.UlogeIzCsv), typeof(RegisterModelTestData))]
        public async Task OnPostAsync_PodaciIzCsvFajla_KreiraKorisnikaSaOcekivanomUlogom(
            string ime, string prezime, string email, bool zelimBitiTutor,
            Uloga ocekivanaUloga, bool? ocekivanaVerifikacija)
        {
            using var ok = new Okruzenje();
            ok.Model.Input.Ime = ime;
            ok.Model.Input.Prezime = prezime;
            ok.Model.Input.Email = email;
            ok.Model.Input.ZelimBitiTutor = zelimBitiTutor;

            var rezultat = await ok.Model.OnPostAsync();

            Assert.IsInstanceOfType(rezultat, typeof(LocalRedirectResult));
            Assert.AreEqual(ime, ok.Kreiran!.Ime);
            Assert.AreEqual(prezime, ok.Kreiran.Prezime);
            Assert.AreEqual(email, ok.Kreiran.Email);
            Assert.AreEqual(email, ok.Kreiran.UserName);
            Assert.AreEqual(ocekivanaUloga, ok.Kreiran.Uloga);
            Assert.AreEqual(ocekivanaVerifikacija, ok.Kreiran.VerifikovanTutor);
        }

        // ---------- Data-driven: JSON ----------

        [TestMethod]
        [DynamicData(nameof(RegisterModelTestData.ValidacijaIzJsona), typeof(RegisterModelTestData))]
        public void InputModel_PodaciIzJsonFajla_ValidacijaDajeOcekivaniBrojGresaka(
            string ime, string prezime, string email, string password,
            string confirmPassword, bool validno, int brojGresaka)
        {
            var input = new RegisterModel.InputModel
            {
                Ime = ime,
                Prezime = prezime,
                Email = email,
                Password = password,
                ConfirmPassword = confirmPassword
            };
            var greske = new List<ValidationResult>();

            var jeValidan = Validator.TryValidateObject(
                input, new ValidationContext(input), greske, validateAllProperties: true);

            Assert.AreEqual(validno, jeValidan);
            Assert.AreEqual(brojGresaka, greske.Count);
        }
    }
}
