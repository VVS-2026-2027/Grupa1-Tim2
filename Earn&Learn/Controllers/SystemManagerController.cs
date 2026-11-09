using Earn_Learn.Data;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Earn_Learn.Security;


namespace Earn_Learn.Controllers
{
    [RoleOnly(Uloga.SystemManager)]
    public class SystemManagerController : Controller
    {
        private readonly UserManager<Korisnik> _userManager;
        private readonly ApplicationDbContext _context;

        public SystemManagerController(UserManager<Korisnik> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.SystemManager)
                return RedirectToAction("Index", "Home");

            int neverifikovaniTutori = await _context.Users
                .CountAsync(u => u.Uloga == Uloga.Tutor && u.VerifikovanTutor != true);
            int otvorenePrijave = await _context.Prijava
                .CountAsync(p => p.Status != StatusPrijave.Rijeseno);

            // Provizija SM-a = transakcije gdje je idTutora == SM-ov id (10% od svake isplate)
            var smIds = await _context.Users
                .Where(u => u.Uloga == Uloga.SystemManager)
                .Select(u => u.Id)
                .ToListAsync();

            var model = new AdminDashboardViewModel
            {
                UkupnoKorisnika = await _context.Users.CountAsync(),
                UkupnoTutora = await _context.Users.CountAsync(u => u.Uloga == Uloga.Tutor && u.VerifikovanTutor == true),
                AktivnihSesija = await _context.Termin.CountAsync(t => t.datumIVrijeme >= DateTime.Now),
                UkupanPromet = await _context.Transakcija
                    .Where(t => smIds.Contains(t.idTutora) && t.statusPlacanja == StatusPlacanja.Uspjesno)
                    .SumAsync(t => t.iznos),
                MjesecniPromet = await _context.Transakcija
                    .Where(t => smIds.Contains(t.idTutora) &&
                                t.statusPlacanja == StatusPlacanja.Uspjesno &&
                                t.datumUplate.Month == DateTime.Now.Month &&
                                t.datumUplate.Year == DateTime.Now.Year)
                    .SumAsync(t => t.iznos),
                ZahtjevaNCekanju = neverifikovaniTutori + otvorenePrijave
            };

            return View(model);
        }

        [Authorize]
        public async Task<IActionResult> Zahtjevi()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.SystemManager)
                return RedirectToAction("Index", "Home");

            return View();
        }

        [Authorize]
        public async Task<IActionResult> Prijave()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.SystemManager)
                return RedirectToAction("Index", "Home");

            var prijave = await _context.Prijava
                .OrderByDescending(p => p.DatumPrijave)
                .ToListAsync();

            return View(prijave);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ObrisiPrijavu(int id)
        {
            var prijava = await _context.Prijava.FindAsync(id);
            if (prijava != null)
            {
                _context.Prijava.Remove(prijava);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Prijave));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DodajPrijavu(string naslov, string opis)
        {
            if (!string.IsNullOrWhiteSpace(naslov))
            {
                _context.Prijava.Add(new Prijava
                {
                    Naslov = naslov,
                    Opis = opis ?? "",
                    DatumPrijave = DateTime.UtcNow,
                    Status = StatusPrijave.Prijavljeno
                });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Prijave));
        }

        // ── VERIFIKACIJA TUTORA ──
        [Authorize]
        public async Task<IActionResult> ZahtjeviZaVerifikaciju()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.SystemManager)
                return RedirectToAction("Index", "Home");

            var tutori = await _context.Users
                .Where(u => u.Uloga == Uloga.Tutor && u.VerifikovanTutor != true)
                .ToListAsync();

            var tutoriSaPredmetima = new List<TutorSaPredmetima>();
            foreach (var tutor in tutori)
            {
                var idPredmeta = await _context.KorisnikPredmet
                    .Where(kp => kp.idKorisnika == tutor.Id)
                    .Select(kp => kp.idPredmeta)
                    .ToListAsync();

                var predmeti = await _context.Predmet
                    .Where(p => idPredmeta.Contains(p.id))
                    .ToListAsync();

                var predmetiNaCekanju = new List<Predmet>();
                if (!string.IsNullOrEmpty(tutor.PredmetiNaCekanjuJson))
                {
                    var ids = System.Text.Json.JsonSerializer.Deserialize<List<int>>(tutor.PredmetiNaCekanjuJson);
                    if (ids != null)
                        predmetiNaCekanju = await _context.Predmet
                            .Where(p => ids.Contains(p.id))
                            .ToListAsync();
                }

                Predmet? predmetNaCekanju = null;
                if (tutor.PredmetNaCekanjaId.HasValue)
                    predmetNaCekanju = await _context.Predmet.FindAsync(tutor.PredmetNaCekanjaId.Value);

                tutoriSaPredmetima.Add(new TutorSaPredmetima
                {
                    Tutor = tutor,
                    Predmeti = predmeti,
                    PredmetiNaCekanju = predmetiNaCekanju,
                    PredmetNaCekanju = predmetNaCekanju
                });
            }

            var model = new VerifikacijaViewModel
            {
                TutoriNaCekanju = tutoriSaPredmetima,
                BrojVerifikovanih = await _context.Users
                    .CountAsync(u => u.Uloga == Uloga.Tutor && u.VerifikovanTutor == true)
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifikujTutora(string id)
        {
            var tutor = await _userManager.FindByIdAsync(id);
            if (tutor == null || tutor.Uloga != Uloga.Tutor)
                return NotFound();

            tutor.VerifikovanTutor = true;
            tutor.PredmetiNaCekanjuJson ??= "[]"; // ← FIX

            if (!string.IsNullOrEmpty(tutor.PredmetiNaCekanjuJson) && tutor.PredmetiNaCekanjuJson != "[]")
            {
                var ids = System.Text.Json.JsonSerializer.Deserialize<List<int>>(tutor.PredmetiNaCekanjuJson);
                if (ids != null)
                {
                    foreach (var predmetId in ids)
                    {
                        var postoji = await _context.KorisnikPredmet
                            .AnyAsync(kp => kp.idKorisnika == tutor.Id && kp.idPredmeta == predmetId);
                        if (!postoji)
                        {
                            _context.KorisnikPredmet.Add(new KorisnikPredmet
                            {
                                idKorisnika = tutor.Id,
                                idPredmeta = predmetId
                            });
                        }
                    }
                }
                tutor.PredmetiNaCekanjuJson = "[]";
            }

            if (tutor.PredmetNaCekanjaId.HasValue)
            {
                var vecPostoji = await _context.KorisnikPredmet
                    .AnyAsync(kp => kp.idKorisnika == tutor.Id
                                 && kp.idPredmeta == tutor.PredmetNaCekanjaId.Value);
                if (!vecPostoji)
                {
                    _context.KorisnikPredmet.Add(new KorisnikPredmet
                    {
                        idKorisnika = tutor.Id,
                        idPredmeta = tutor.PredmetNaCekanjaId.Value
                    });
                }
                tutor.PredmetNaCekanjaId = null;
            }

            await _userManager.UpdateAsync(tutor);
            await _context.SaveChangesAsync();

            TempData["Uspjeh"] = $"Tutor {tutor.Ime} {tutor.Prezime} je uspješno verifikovan.";
            return RedirectToAction(nameof(ZahtjeviZaVerifikaciju));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OdbijTutora(string id)
        {
            var tutor = await _userManager.FindByIdAsync(id);
            if (tutor == null || tutor.Uloga != Uloga.Tutor)
                return NotFound();

            await _userManager.DeleteAsync(tutor);

            TempData["Uspjeh"] = "Zahtjev za verifikaciju je odbijen i nalog je obrisan.";
            return RedirectToAction(nameof(ZahtjeviZaVerifikaciju));
        }

        [Authorize]
        public async Task<IActionResult> PrilogTutora(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.SystemManager)
                return RedirectToAction("Index", "Home");

            var tutor = await _userManager.FindByIdAsync(id);
            if (tutor == null || tutor.Uloga != Uloga.Tutor)
                return NotFound();

            return View(tutor);
        }

        public async Task<IActionResult> Index()
        {
            var manageri = await _context.Users
                .Where(k => k.Uloga == Uloga.SystemManager)
                .ToListAsync();
            return View(manageri);
        }

        public async Task<IActionResult> Detalji(string id)
        {
            var manager = await _userManager.FindByIdAsync(id);
            if (manager == null || manager.Uloga != Uloga.SystemManager)
                return NotFound();

            return View(manager);
        }

        public IActionResult Kreiraj() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Kreiraj(string ime, string prezime, string email, string lozinka)
        {
            var korisnik = new Korisnik
            {
                Ime = ime,
                Prezime = prezime,
                Email = email,
                UserName = email,
                Uloga = Uloga.SystemManager,
                DatumRegistracije = DateTime.UtcNow,
                PredmetiNaCekanjuJson = "[]" // ← FIX
            };

            var rezultat = await _userManager.CreateAsync(korisnik, lozinka);
            if (rezultat.Succeeded)
                return RedirectToAction(nameof(Index));

            foreach (var greska in rezultat.Errors)
                ModelState.AddModelError("", greska.Description);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Obrisi(string id)
        {
            var manager = await _userManager.FindByIdAsync(id);
            if (manager == null || manager.Uloga != Uloga.SystemManager)
                return NotFound();

            await _userManager.DeleteAsync(manager);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> UpravljajKorisnicima(string? pretraga)
        {
            var korisnici = _context.Users
                .Where(u => u.Uloga != Uloga.SystemManager)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(pretraga))
                korisnici = korisnici.Where(u =>
                    u.Ime.Contains(pretraga) ||
                    u.Prezime.Contains(pretraga) ||
                    u.Email.Contains(pretraga));

            ViewBag.Pretraga = pretraga;
            return View(await korisnici.ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ObrisiKorisnika(string id)
        {
            var korisnik = await _userManager.FindByIdAsync(id);
            if (korisnik != null)
                await _userManager.DeleteAsync(korisnik);
            return RedirectToAction(nameof(UpravljajKorisnicima));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UrediKorisnika(string id, string ime, string prezime, string email)
        {
            var korisnik = await _userManager.FindByIdAsync(id);
            if (korisnik == null) return NotFound();

            korisnik.Ime = ime;
            korisnik.Prezime = prezime;
            korisnik.Email = email;
            korisnik.UserName = email;
            korisnik.PredmetiNaCekanjuJson ??= "[]"; // ← FIX

            await _userManager.UpdateAsync(korisnik);
            return RedirectToAction(nameof(UpravljajKorisnicima));
        }

        public async Task<IActionResult> UpravljajPredmetima(string? pretraga)
        {
            var predmeti = _context.Predmet.AsQueryable();

            if (!string.IsNullOrWhiteSpace(pretraga))
                predmeti = predmeti.Where(p => p.naziv.Contains(pretraga));

            ViewBag.Pretraga = pretraga;
            ViewBag.UkupnoPredmeta = await _context.Predmet.CountAsync();

            var maxId = await _context.Predmet.MaxAsync(x => x.id);
            ViewBag.NovihPredmeta = await _context.Predmet.CountAsync(p => p.id > maxId - 3);

            ViewBag.BrojTutoraPoP = await _context.KorisnikPredmet
                .Where(kp => _context.Users.Any(u => u.Id == kp.idKorisnika && u.Uloga == Uloga.Tutor))
                .GroupBy(kp => kp.idPredmeta)
                .Select(g => new { id = g.Key, count = g.Count() })
                .ToDictionaryAsync(x => x.id, x => x.count);

            return View(await predmeti.ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DodajPredmet(string naziv)
        {
            if (!string.IsNullOrWhiteSpace(naziv))
            {
                _context.Predmet.Add(new Predmet { naziv = naziv });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(UpravljajPredmetima));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UrediPredmet(int id, string naziv)
        {
            var predmet = await _context.Predmet.FindAsync(id);
            if (predmet == null) return NotFound();
            predmet.naziv = naziv;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(UpravljajPredmetima));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ObrisiPredmet(int id)
        {
            var predmet = await _context.Predmet.FindAsync(id);
            if (predmet != null)
            {
                _context.Predmet.Remove(predmet);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(UpravljajPredmetima));
        }

    }
}