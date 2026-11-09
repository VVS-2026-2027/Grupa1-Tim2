using Earn_Learn.Data;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Earn_Learn.Controllers
{
    [Authorize]
    public class ProfilController : Controller
    {
        private readonly UserManager<Korisnik> _userManager;
        private readonly SignInManager<Korisnik> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProfilController(UserManager<Korisnik> userManager,
                                SignInManager<Korisnik> signInManager,
                                ApplicationDbContext context,
                                IWebHostEnvironment env)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Index", "Home");

            var predmeti = await _context.KorisnikPredmet
                .Where(kp => kp.idKorisnika == user.Id)
                .Join(_context.Predmet,
                      kp => kp.idPredmeta,
                      p => p.id,
                      (kp, p) => p)
                .ToListAsync();

            if (user.Uloga == Uloga.Tutor)
            {
                ViewBag.CijenaPoSatu = user.CijenaPoSatu ?? 0;
                ViewBag.ProsjecnaOcjena = user.ProsjecnaOcjena ?? 0;
            }

            var sviPredmeti = await _context.Predmet
                .OrderBy(p => p.naziv)
                .ToListAsync();

            List<Recenzija> recenzijeRaw;
            List<RecenzijaViewModel> recenzije = new();

            if (user.Uloga == Uloga.Tutor)
            {
                // Recenzije koje su studenti ostavili OVOM tutoru
                recenzijeRaw = await _context.Recenzija
                    .Where(r => r.idTutora == user.Id)
                    .OrderByDescending(r => r.datumRecenzije)
                    .ToListAsync();

                foreach (var rec in recenzijeRaw)
                {
                    var student = await _context.Users.FindAsync(rec.idStudenta);
                    recenzije.Add(new RecenzijaViewModel
                    {
                        Recenzija = rec,
                        ImeStudenta = (student?.Ime + " " + student?.Prezime) ?? "Student"
                    });
                }
            }
            else
            {
                // Recenzije koje je ovaj student ostavio tutorima
                recenzijeRaw = await _context.Recenzija
                    .Where(r => r.idStudenta == user.Id)
                    .OrderByDescending(r => r.datumRecenzije)
                    .ToListAsync();

                foreach (var rec in recenzijeRaw)
                {
                    var tutor = await _context.Users.FindAsync(rec.idTutora);
                    recenzije.Add(new RecenzijaViewModel
                    {
                        Recenzija = rec,
                        ImeStudenta = (tutor?.Ime + " " + tutor?.Prezime) ?? "Tutor"
                    });
                }
            }

            var brojCasova = user.Uloga == Uloga.Tutor
                ? await _context.Termin.CountAsync(t => t.idTutora == user.Id && t.status == StatusTermina.Odrzan)
                : await _context.Termin.CountAsync(t => t.idStudenta == user.Id && t.status == StatusTermina.Odrzan);

            var model = new ProfilViewModel
            {
                Ime = user.Ime,
                Prezime = user.Prezime,
                Email = user.Email ?? "",
                Uloga = user.Uloga,
                BrojIndeksa = user.BrojIndeksa,
                GodinaStudija = user.GodinaStudija,
                BrojRecenzija = recenzijeRaw.Count,
                BrojCasova = brojCasova,
                Predmeti = predmeti.Select(p => p.naziv).ToList(),
                PredmetiObjekti = predmeti,
                Recenzije = recenzije,
                SviPredmeti = sviPredmeti
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UrediGodinuStudija(int godinaStudija)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Index", "Home");

            if (godinaStudija < 1 || godinaStudija > 6)
            {
                TempData["Greska"] = "Godina studija mora biti između 1 i 6.";
                return RedirectToAction("Index");
            }

            user.GodinaStudija = godinaStudija;
            await _userManager.UpdateAsync(user);

            TempData["Uspjeh"] = "Godina studija uspješno ažurirana.";
            return RedirectToAction("Index");
        }

        // ── POSTANI TUTOR ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostaniTutor(
            List<int> odabraniPredmeti,
            IFormFile? prilogOcjene)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return RedirectToAction("Index");

            if (odabraniPredmeti == null || !odabraniPredmeti.Any())
            {
                TempData["TutorGreska"] = "Morate odabrati barem jedan predmet.";
                return RedirectToAction("Index");
            }

            string? prilogPath = null;
            if (prilogOcjene != null && prilogOcjene.Length > 0)
            {
                var dozvoljeniTipovi = new[] {
                    "image/jpeg", "image/png", "image/gif",
                    "image/webp", "application/pdf"
                };
                if (!dozvoljeniTipovi.Contains(prilogOcjene.ContentType))
                {
                    TempData["TutorGreska"] = "Dozvoljeni formati su JPG, PNG i PDF.";
                    return RedirectToAction("Index");
                }
                if (prilogOcjene.Length > 5 * 1024 * 1024)
                {
                    TempData["TutorGreska"] = "Prilog ne smije biti veći od 5MB.";
                    return RedirectToAction("Index");
                }

                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "prilozi");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = Guid.NewGuid() + Path.GetExtension(prilogOcjene.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await prilogOcjene.CopyToAsync(stream);

                prilogPath = "/uploads/prilozi/" + fileName;
            }

            user.Uloga = Uloga.Tutor;
            user.VerifikovanTutor = false;
            if (prilogPath != null)
                user.PrilogOcjene = prilogPath;

            // Predmeti se NE dodaju odmah — čekaju adminovo odobrenje
            user.PredmetiNaCekanjuJson = System.Text.Json.JsonSerializer.Serialize(odabraniPredmeti);

            await _userManager.UpdateAsync(user);
            await _signInManager.RefreshSignInAsync(user);

            TempData["TutorUspjeh"] = "Uspješno ste postali tutor!";
            return LocalRedirect("/Tutor/Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UrediCijenu(double cijenaPoSatu)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Tutor)
                return RedirectToAction("Index", "Home");

            if (cijenaPoSatu < 0)
            {
                TempData["Greska"] = "Cijena ne može biti negativna.";
                return RedirectToAction("Index");
            }

            user.CijenaPoSatu = cijenaPoSatu;
            await _userManager.UpdateAsync(user);

            TempData["Uspjeh"] = $"Cijena uspješno ažurirana na {cijenaPoSatu:0.00} KM/h.";
            return RedirectToAction("Index");
        }

        // ── DODAJ PREDMET (tutor šalje zahtjev adminu) ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DodajPredmet(int predmetId, IFormFile? prilogPredmet)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Tutor)
                return RedirectToAction("Index");

            var predmet = await _context.Predmet.FindAsync(predmetId);
            if (predmet == null)
            {
                TempData["Greska"] = "Predmet nije pronađen.";
                return RedirectToAction("Index");
            }

            var vecPostoji = await _context.KorisnikPredmet
                .AnyAsync(kp => kp.idKorisnika == user.Id && kp.idPredmeta == predmetId);
            if (vecPostoji)
            {
                TempData["Greska"] = "Već predajete ovaj predmet.";
                return RedirectToAction("Index");
            }

            string? prilogPath = null;
            if (prilogPredmet != null && prilogPredmet.Length > 0)
            {
                var dozvoljeniTipovi = new[] {
                    "image/jpeg", "image/png", "image/gif",
                    "image/webp", "application/pdf"
                };
                if (!dozvoljeniTipovi.Contains(prilogPredmet.ContentType))
                {
                    TempData["Greska"] = "Dozvoljeni formati su JPG, PNG i PDF.";
                    return RedirectToAction("Index");
                }
                if (prilogPredmet.Length > 5 * 1024 * 1024)
                {
                    TempData["Greska"] = "Prilog ne smije biti veći od 5MB.";
                    return RedirectToAction("Index");
                }

                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "prilozi");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = Guid.NewGuid() + Path.GetExtension(prilogPredmet.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await prilogPredmet.CopyToAsync(stream);

                prilogPath = "/uploads/prilozi/" + fileName;
            }

            user.VerifikovanTutor = false;
            user.PredmetNaCekanjaId = predmetId;
            if (prilogPath != null)
                user.PrilogOcjene = prilogPath;

            await _userManager.UpdateAsync(user);

            TempData["Uspjeh"] = $"Zahtjev za dodavanje predmeta '{predmet.naziv}' je poslan adminu.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ObrisiPredmet(int predmetId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Tutor)
                return RedirectToAction("Index");

            var kp = await _context.KorisnikPredmet
                .FirstOrDefaultAsync(kp => kp.idKorisnika == user.Id && kp.idPredmeta == predmetId);

            if (kp != null)
            {
                _context.KorisnikPredmet.Remove(kp);
                await _context.SaveChangesAsync();
                TempData["Uspjeh"] = "Predmet uspješno uklonjen.";
            }

            return RedirectToAction("Index");
        }
    }
}