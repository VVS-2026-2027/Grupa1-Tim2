using Earn_Learn.Data;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Earn_Learn.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<Korisnik> _signInManager;
        private readonly UserManager<Korisnik> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public AccountController(SignInManager<Korisnik> signInManager,
                                  UserManager<Korisnik> userManager,
                                  ApplicationDbContext context,
                                  IWebHostEnvironment env)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _env = env;
        }

        // ── LOGIN ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            [FromForm(Name = "Input.Email")] string email,
            [FromForm(Name = "Input.Password")] string password,
            string? returnUrl)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["LoginError"] = "Unesite email i šifru.";
                return LocalRedirect("/Identity/Account/Login");
            }

            var result = await _signInManager.PasswordSignInAsync(
                email, password, isPersistent: false, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var korisnik = await _userManager.FindByEmailAsync(email);
                return korisnik?.Uloga switch
                {
                    Uloga.Student => LocalRedirect("/Student/Dashboard"),
                    Uloga.Tutor => LocalRedirect("/Tutor/Dashboard"),
                    Uloga.SystemManager => LocalRedirect("/SystemManager/Dashboard"),
                    _ => LocalRedirect(returnUrl ?? "/")
                };
            }

            TempData["LoginError"] = "Pogrešan email ili šifra.";
            return LocalRedirect("/Identity/Account/Login");
        }

        // ── REGISTER GET ──
        [HttpGet]
        public IActionResult Register() => View();

        // ── REGISTER POST ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string ime, string prezime, string email,
            string password, Uloga uloga, int? brojIndeksa,
            IFormFile? prilogOcjene)
        {
            if (uloga == Uloga.SystemManager)
            {
                TempData["RegError"] = "Nije moguće registrovati System Manager nalog.";
                return RedirectToAction("Register");
            }

            string? prilogPath = null;
            if (uloga == Uloga.Tutor && prilogOcjene != null && prilogOcjene.Length > 0)
            {
                var dozvoljeniTipovi = new[] { "image/jpeg", "image/png", "image/gif", "image/webp", "application/pdf" };
                if (!dozvoljeniTipovi.Contains(prilogOcjene.ContentType))
                {
                    TempData["RegError"] = "Dozvoljeni formati su JPG, PNG i PDF.";
                    return RedirectToAction("Register");
                }

                if (prilogOcjene.Length > 5 * 1024 * 1024)
                {
                    TempData["RegError"] = "Prilog ne smije biti veći od 5MB.";
                    return RedirectToAction("Register");
                }

                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "prilozi");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(prilogOcjene.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await prilogOcjene.CopyToAsync(stream);
                }

                prilogPath = "/uploads/prilozi/" + fileName;
            }

            var korisnik = new Korisnik
            {
                Ime = ime ?? "",
                Prezime = prezime ?? "",
                Email = email,
                UserName = email,
                Uloga = uloga,
                BrojIndeksa = uloga == Uloga.Student ? brojIndeksa : null,
                PrilogOcjene = prilogPath,
                VerifikovanTutor = uloga == Uloga.Tutor ? false : null,
                DatumRegistracije = DateTime.UtcNow
            };

            var rezultat = await _userManager.CreateAsync(korisnik, password);

            if (rezultat.Succeeded)
            {
                await _signInManager.SignInAsync(korisnik, isPersistent: false);
                return uloga switch
                {
                    Uloga.Student => LocalRedirect("/Student/Dashboard"),
                    Uloga.Tutor => LocalRedirect("/Tutor/Dashboard"),
                    _ => LocalRedirect("/")
                };
            }

            TempData["RegError"] = string.Join(", ", rezultat.Errors.Select(e => e.Description));
            return RedirectToAction("Register");
        }

        // ── LOGOUT ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return LocalRedirect("/");
        }

        // ── REGISTER IDENTITY (Identity forma) ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterIdentity(
            [FromForm(Name = "Input.Ime")] string ime,
            [FromForm(Name = "Input.Prezime")] string prezime,
            [FromForm(Name = "Input.Email")] string email,
            [FromForm(Name = "Input.Password")] string password,
            [FromForm(Name = "Input.ZelimBitiTutor")] bool zelimTutor,
            [FromForm(Name = "Input.OdabraniPredmeti")] List<int>? odabraniPredmeti,
            [FromForm(Name = "Input.PrilogOcjene")] IFormFile? prilogOcjene,
            int? uloga,
            string? returnUrl)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["RegError"] = "Unesite sve podatke.";
                return LocalRedirect("/Identity/Account/Register");
            }

            var odabranaUloga = (zelimTutor || uloga == 1) ? Uloga.Tutor : Uloga.Student;

            string? prilogPath = null;
            if (odabranaUloga == Uloga.Tutor && prilogOcjene != null && prilogOcjene.Length > 0)
            {
                var dozvoljeniTipovi = new[] { "image/jpeg", "image/png", "image/gif", "image/webp", "application/pdf" };
                if (dozvoljeniTipovi.Contains(prilogOcjene.ContentType))
                {
                    var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "prilozi");
                    Directory.CreateDirectory(uploadsFolder);
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(prilogOcjene.FileName);
                    var filePath = Path.Combine(uploadsFolder, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await prilogOcjene.CopyToAsync(stream);
                    }
                    prilogPath = "/uploads/prilozi/" + fileName;
                }
            }

            var korisnik = new Korisnik
            {
                Ime = ime ?? "",
                Prezime = prezime ?? "",
                Email = email,
                UserName = email,
                Uloga = odabranaUloga,
                DatumRegistracije = DateTime.UtcNow,
                PrilogOcjene = prilogPath,
                VerifikovanTutor = odabranaUloga == Uloga.Tutor ? false : null
            };

            var rezultat = await _userManager.CreateAsync(korisnik, password);

            if (rezultat.Succeeded)
            {
                // Predmeti se NE dodaju odmah — čekaju adminovo odobrenje
                if (odabranaUloga == Uloga.Tutor && odabraniPredmeti != null && odabraniPredmeti.Any())
                {
                    korisnik.PredmetiNaCekanjuJson = System.Text.Json.JsonSerializer.Serialize(odabraniPredmeti);
                    await _userManager.UpdateAsync(korisnik);
                }

                await _signInManager.SignInAsync(korisnik, isPersistent: false);
                return odabranaUloga switch
                {
                    Uloga.Student => LocalRedirect("/Student/Dashboard"),
                    Uloga.Tutor => LocalRedirect("/Tutor/Dashboard"),
                    _ => LocalRedirect("/")
                };
            }

            TempData["RegError"] = string.Join(", ", rezultat.Errors.Select(e => e.Description));
            return LocalRedirect("/Identity/Account/Register");
        }
    }
}