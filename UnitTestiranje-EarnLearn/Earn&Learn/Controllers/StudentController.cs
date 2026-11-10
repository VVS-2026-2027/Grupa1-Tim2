using Earn_Learn.Data;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Earn_Learn.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Earn_Learn.Controllers
{
    [RoleOnly(Uloga.Student)]
    public class StudentController : Controller
    {
        private readonly UserManager<Korisnik> _userManager;
        private readonly ApplicationDbContext _context;

        public StudentController(UserManager<Korisnik> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return RedirectToAction("Index", "Home");

            var termini = await _context.Termin
                .Where(t => t.idStudenta == user.Id && t.datumIVrijeme >= DateTime.Now)
                .OrderBy(t => t.datumIVrijeme)
                .Take(5)
                .ToListAsync();

            var terminiSaTutorom = new List<TerminSaTutorom>();
            foreach (var termin in termini)
            {
                var tutorKorisnik = await _context.Users.FindAsync(termin.idTutora);
                var predmet = termin.idPredmeta.HasValue
                    ? await _context.Predmet.FindAsync(termin.idPredmeta.Value)
                    : null;

                terminiSaTutorom.Add(new TerminSaTutorom
                {
                    Termin = termin,
                    Tutor = tutorKorisnik ?? new Korisnik { Ime = "Nepoznat", Prezime = "" },
                    Predmet = predmet
                });
            }

            var topTutori = await _context.Users
                .Where(k => k.Uloga == Uloga.Tutor)
                .OrderByDescending(k => k.ProsjecnaOcjena)
                .Take(3)
                .ToListAsync();

            var model = new StudentDashboardViewModel
            {
                Ime = user.Ime,
                Prezime = user.Prezime,
                NadolazaciTermini = terminiSaTutorom,
                TopTutori = topTutori
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Obrisi(string id)
        {
            var student = await _userManager.FindByIdAsync(id);
            if (student == null || student.Uloga != Uloga.Student)
                return NotFound();

            await _userManager.DeleteAsync(student);
            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        public async Task<IActionResult> MojiCasovi()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return RedirectToAction("Index", "Home");

            var termini = await _context.Termin
                .Where(t => t.idStudenta == user.Id)
                .OrderByDescending(t => t.datumIVrijeme)
                .ToListAsync();

            var model = new List<TerminSaTutorom>();
            foreach (var termin in termini)
            {
                var tutorKorisnik = await _context.Users.FindAsync(termin.idTutora);
                var predmet = termin.idPredmeta.HasValue
                    ? await _context.Predmet.FindAsync(termin.idPredmeta.Value)
                    : null;

                model.Add(new TerminSaTutorom
                {
                    Termin = termin,
                    Tutor = tutorKorisnik ?? new Korisnik { Ime = "Nepoznat", Prezime = "" },
                    Predmet = predmet
                });
            }

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OtkaziTermin(int terminId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return Unauthorized();

            var termin = await _context.Termin.FindAsync(terminId);
            if (termin == null || termin.idStudenta != user.Id)
                return NotFound();

            if (termin.status == StatusTermina.Rezervisan)
            {
                var tutor = await _context.Users.FindAsync(termin.idTutora);
                if (tutor != null)
                {
                    user.StanjeRacuna += termin.cijena;
                    tutor.StanjeRacuna -= termin.cijena;
                    await _userManager.UpdateAsync(tutor);
                    await _userManager.UpdateAsync(user);
                }

                _context.Transakcija.Add(new Transakcija
                {
                    idStudenta = user.Id,
                    idTutora = termin.idTutora,
                    iznos = -termin.cijena,
                    datumUplate = DateTime.UtcNow,
                    nacinPlacanja = NacinPlacanja.Cash,
                    statusPlacanja = StatusPlacanja.Vraceno
                });

                // KREIRANJE OBAVJEŠTENJA ZA TUTORA O OTKAZIVANJU
                var obavjestenjeOtkazivanje = new Obavjestenje
                {
                    idKorisnika = termin.idTutora,
                    naslov = "Termin je otkazan ❌",
                    sadrzaj = $"Student {user.Ime} {user.Prezime} je otkazao termin koji je bio zakazan za {termin.datumIVrijeme.ToString("dd.MM.yyyy. u HH:mm")}h. Novac je vraćen studentu.",
                    datumSlanja = DateTime.UtcNow,
                    procitano = false
                };
                _context.Obavjestenje.Add(obavjestenjeOtkazivanje);

                termin.idStudenta = string.Empty;
                termin.idPredmeta = null;
                termin.status = StatusTermina.Slobodan;
                termin.cijena = 0;
                await _context.SaveChangesAsync();
            }

            TempData["Uspjeh"] = "Termin je uspješno otkazan. Novac je vraćen na vaš račun.";
            return RedirectToAction("MojiCasovi");
        }

        [HttpGet]
        [AllowAnonymous]

        public async Task<IActionResult> PretraziTutore(
            string query,
            double? minCijena = null,
            double? maxCijena = null,
            double? minRejting = null)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Json(new List<object>());

            var q = query.ToLower().Trim();

            var tutori = await _context.Users
                .Where(u => u.Uloga == Uloga.Tutor)
                .Where(u => u.Ime.ToLower().Contains(q) ||
                            u.Prezime.ToLower().Contains(q) ||
                            _context.KorisnikPredmet.Any(kp => kp.idKorisnika == u.Id &&
                                _context.Predmet.Any(p => p.id == kp.idPredmeta &&
                                                           p.naziv.ToLower().Contains(q))))
                .Where(u => minCijena == null || (u.CijenaPoSatu != null && u.CijenaPoSatu >= minCijena))
                .Where(u => maxCijena == null || (u.CijenaPoSatu != null && u.CijenaPoSatu <= maxCijena))
                .Where(u => minRejting == null || (u.ProsjecnaOcjena != null && u.ProsjecnaOcjena >= minRejting))
                .Select(u => new TutorSearchDto
                {
                    Id = u.Id,
                    Ime = u.Ime,
                    Prezime = u.Prezime,
                    ProsjecnaOcjena = u.ProsjecnaOcjena ?? 0,
                    CijenaPoSatu = u.CijenaPoSatu ?? 0,
                    BrojOdrzanihCasova = u.BrojOdrzanihCasova ?? 0,
                    Predmeti = _context.KorisnikPredmet
                        .Where(kp => kp.idKorisnika == u.Id)
                        .Join(_context.Predmet, kp => kp.idPredmeta, p => p.id, (kp, p) => p.naziv)
                        .ToList()
                })
                .Take(20)
                .ToListAsync();

            foreach (var t in tutori)
                t.Rang = (t.ProsjecnaOcjena * 0.7) + (t.BrojOdrzanihCasova * 0.3);

            int n = tutori.Count;
            for (int i = 0; i < n - 1; i++)
            {
                for (int j = 0; j < n - i - 1; j++)
                {
                    if (tutori[j].Rang < tutori[j + 1].Rang)
                    {
                        var temp = tutori[j];
                        tutori[j] = tutori[j + 1];
                        tutori[j + 1] = temp;
                    }
                }
            }

            var rezultati = tutori.Take(5).Select(t => new
            {
                id = t.Id,
                ime = t.Ime,
                prezime = t.Prezime,
                prosjecnaOcjena = t.ProsjecnaOcjena,
                cijenaPoSatu = t.CijenaPoSatu,
                predmeti = t.Predmeti
            });

            return Json(rezultati);
        }

        [Authorize]
        public async Task<IActionResult> Novcanik()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return RedirectToAction("Index", "Home");

            var transakcije = await _context.Transakcija
                .Where(t => t.idStudenta == user.Id)
                .OrderByDescending(t => t.datumUplate)
                .Take(10)
                .ToListAsync();

            ViewBag.StanjeRacuna = user.StanjeRacuna;
            ViewBag.UkupnoUplaceno = transakcije
                .Where(t => t.statusPlacanja == StatusPlacanja.Uspjesno && t.iznos > 0)
                .Sum(t => t.iznos);

            return View(transakcije);
        }

        [AllowAnonymous ]
        public async Task<IActionResult> RezervisiTermin(int id)
        {
            var student = await _userManager.GetUserAsync(User);
            if (student == null || (student.Uloga != Uloga.Student && student.Uloga != Uloga.Tutor))
                return View("PristupOdbijen");

            var termin = await _context.Termin
                .FirstOrDefaultAsync(t => t.id == id && t.status == StatusTermina.Slobodan);

            if (termin == null)
                return NotFound();

            var tutor = await _context.Users.FindAsync(termin.idTutora);
            var predmet = termin.idPredmeta.HasValue
                ? await _context.Predmet.FindAsync(termin.idPredmeta.Value)
                : null;

            ViewBag.Tutor = tutor;
            ViewBag.Predmet = predmet;
            return View(termin);
        }

        [HttpPost]
        [AllowAnonymous]
        
        public async Task<IActionResult> RezervisiTermin(int id, Earn_Learn.Enums.TipInstrukcija tipInstrukcija)
        {
            // Čitaj svježe iz baze — UserManager cache može imati staro stanje računa
            var studentId = _userManager.GetUserId(User);
            var student = await _userManager.GetUserAsync(User);
            if (student == null || (student.Uloga != Uloga.Student && student.Uloga != Uloga.Tutor))
                return View("PristupOdbijen");


            var termin = await _context.Termin
                .FirstOrDefaultAsync(t => t.id == id && t.status == StatusTermina.Slobodan);

            if (termin == null)
            {
                TempData["Greska"] = "Termin nije dostupan.";
                return RedirectToAction("Dashboard");
            }

            var tutor = await _context.Users.FindAsync(termin.idTutora);
            double cijena = tutor?.CijenaPoSatu ?? termin.cijena;

            // Provjera stanja novčanika
            if (student.StanjeRacuna < cijena)
            {
                TempData["GreskaNovcenik"] = $"Nemate dovoljno sredstava na novčaniku! Potrebno: {cijena:N2} KM, dostupno: {student.StanjeRacuna:N2} KM.";
                return RedirectToAction("Profil", "Tutor", new { id = termin.idTutora });
            }

            termin.idStudenta = student.Id;
            termin.status = StatusTermina.Rezervisan;
            termin.tipInstrukcija = tipInstrukcija;
            termin.cijena = cijena;

            _context.Update(termin);

            // KREIRANJE OBAVJEŠTENJA ZA TUTORA O REZERVACIJI (sa idTermina za dugme "Unesi mjesto")
            var obavjestenjeZaTutora = new Obavjestenje
            {
                idKorisnika = termin.idTutora,
                naslov = "Novi termin rezervisan 📅",
                sadrzaj = $"{student.Ime} {student.Prezime} je rezervisao vaš termin {termin.datumIVrijeme:dd.MM.yyyy. u HH:mm}h ({tipInstrukcija}). Unesite mjesto održavanja.",
                datumSlanja = DateTime.UtcNow,
                procitano = false,
                idTermina = termin.id
            };
            _context.Obavjestenje.Add(obavjestenjeZaTutora);

            await _context.SaveChangesAsync();

            TempData["Uspjeh"] = "Termin uspješno rezervisan! Novac će biti skinut kada potvrdite prisustvo.";
            return RedirectToAction("Dashboard");
        }

        [AllowAnonymous]
        public async Task<IActionResult> OdaberiTermin(string tutorId)
        {
            var student = await _userManager.GetUserAsync(User);
            if (student == null || (student.Uloga != Uloga.Student && student.Uloga != Uloga.Tutor))
                return View("PristupOdbijen");

            var tutor = await _context.Users.FindAsync(tutorId);
            if (tutor == null || tutor.Uloga != Uloga.Tutor)
                return NotFound();

            var termini = await _context.Termin
                .Where(t => t.idTutora == tutorId
                         && t.status == StatusTermina.Slobodan
                         && t.datumIVrijeme >= DateTime.Now)
                .OrderBy(t => t.datumIVrijeme)
                .ToListAsync();

            var terminPredmeti = new Dictionary<int, string>();
            foreach (var t in termini)
            {
                if (t.idPredmeta.HasValue)
                {
                    var p = await _context.Predmet.FindAsync(t.idPredmeta.Value);
                    terminPredmeti[t.id] = p?.naziv ?? "—";
                }
            }

            ViewBag.Tutor = tutor;
            ViewBag.TerminPredmeti = terminPredmeti;
            return View(termini);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UplatiNovac(double iznos)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return Unauthorized();

            if (iznos <= 0 || iznos > 10000)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, poruka = "Iznos mora biti između 1 i 10,000 KM." });

                TempData["Greska"] = "Iznos mora biti između 1 i 10,000 KM.";
                return RedirectToAction(nameof(Novcanik));
            }

            user.StanjeRacuna += iznos;
            await _userManager.UpdateAsync(user);

            _context.Transakcija.Add(new Transakcija
            {
                idStudenta = user.Id,
                idTutora = string.Empty,
                iznos = iznos,
                datumUplate = DateTime.UtcNow,
                nacinPlacanja = NacinPlacanja.Kartica,
                statusPlacanja = StatusPlacanja.Uspjesno
            });
            await _context.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new
                {
                    success = true,
                    novoStanje = user.StanjeRacuna,
                    poruka = $"Uspješno ste uplatili {iznos:N2} KM na vaš račun."
                });

            TempData["Uspjeh"] = $"Uspješno ste uplatili {iznos:N2} KM na vaš račun.";
            return RedirectToAction(nameof(Novcanik));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PotvrdiPrisustvo(int terminId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return Unauthorized();

            var termin = await _context.Termin.FindAsync(terminId);
            if (termin == null || termin.idStudenta != user.Id)
                return Json(new { success = false, poruka = "Termin nije pronađen." });

            if (termin.status != StatusTermina.Rezervisan)
                return Json(new { success = false, poruka = "Termin nije u statusu rezervisan." });

            if (user.StanjeRacuna < termin.cijena)
                return Json(new { success = false, poruka = $"Nemate dovoljno sredstava. Potrebno: {termin.cijena:N2} KM." });

            var tutor = await _context.Users.FindAsync(termin.idTutora);
            if (tutor == null)
                return Json(new { success = false, poruka = "Tutor nije pronađen." });

            var systemManager = await _context.Users
                .FirstOrDefaultAsync(u => u.Uloga == Uloga.SystemManager);

            double provizija = Math.Round(termin.cijena * 0.10, 2);
            double zaradaTutora = Math.Round(termin.cijena - provizija, 2);

            user.StanjeRacuna -= termin.cijena;
            tutor.StanjeRacuna += zaradaTutora;
            if (systemManager != null)
                systemManager.StanjeRacuna += provizija;

            termin.status = StatusTermina.Odrzan;
            tutor.BrojOdrzanihCasova = (tutor.BrojOdrzanihCasova ?? 0) + 1;

            _context.Transakcija.Add(new Transakcija
            {
                idStudenta = user.Id,
                idTutora = tutor.Id,
                iznos = -termin.cijena,
                datumUplate = DateTime.UtcNow,
                nacinPlacanja = NacinPlacanja.Cash,
                statusPlacanja = StatusPlacanja.Uspjesno
            });

            _context.Transakcija.Add(new Transakcija
            {
                idStudenta = string.Empty,
                idTutora = tutor.Id,
                iznos = zaradaTutora,
                datumUplate = DateTime.UtcNow,
                nacinPlacanja = NacinPlacanja.Cash,
                statusPlacanja = StatusPlacanja.Uspjesno
            });

            if (systemManager != null)
            {
                _context.Transakcija.Add(new Transakcija
                {
                    idStudenta = string.Empty,
                    idTutora = systemManager.Id,
                    iznos = provizija,
                    datumUplate = DateTime.UtcNow,
                    nacinPlacanja = NacinPlacanja.Cash,
                    statusPlacanja = StatusPlacanja.Uspjesno
                });
            }

            // KREIRANJE OBAVJEŠTENJA ZA TUTORA DA JE STUDENT POTVRDIO PRISUSTVO (ZARADA)
            var obavjestenjeZarada = new Obavjestenje
            {
                idKorisnika = tutor.Id,
                naslov = "Uspješno održan čas! 💰",
                sadrzaj = $"Student {user.Ime} {user.Prezime} je potvrdio prisustvo za termin ({termin.datumIVrijeme.ToString("dd.MM.yyyy")}). Na Vaš račun je uplaćeno {zaradaTutora:N2} KM (nakon odbijene provizije).",
                datumSlanja = DateTime.UtcNow,
                procitano = false
            };
            _context.Obavjestenje.Add(obavjestenjeZarada);

            await _userManager.UpdateAsync(user);
            await _userManager.UpdateAsync(tutor);
            if (systemManager != null)
                await _userManager.UpdateAsync(systemManager);
            await _context.SaveChangesAsync();

            return Json(new { success = true, poruka = $"Prisustvo potvrđeno! Skinuto {termin.cijena:N2} KM s računa." });
        }

        public async Task<IActionResult> Profil(string id)
        {
            var student = await _userManager.FindByIdAsync(id);
            if (student == null || student.Uloga != Uloga.Student)
                return NotFound();

            var predmeti = await _context.KorisnikPredmet
                .Where(kp => kp.idKorisnika == id)
                .Join(_context.Predmet, kp => kp.idPredmeta, p => p.id, (kp, p) => p)
                .ToListAsync();

            var termini = await _context.Termin
                .Where(t => t.idStudenta == id)
                .OrderByDescending(t => t.datumIVrijeme)
                .ToListAsync();

            var recenzije = await _context.Recenzija
                .Where(r => r.idStudenta == id)
                .OrderByDescending(r => r.datumRecenzije)
                .ToListAsync();

            var recenzijeViewModel = new List<RecenzijaViewModel>();
            foreach (var rec in recenzije)
            {
                var tutor = await _context.Users.FindAsync(rec.idTutora);
                recenzijeViewModel.Add(new RecenzijaViewModel
                {
                    Recenzija = rec,
                    ImeStudenta = tutor?.Ime ?? "Tutor"
                });
            }

            var model = new StudentProfilViewModel
            {
                Student = student,
                Predmeti = predmeti,
                Termini = termini,
                Recenzije = recenzijeViewModel
            };

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OstaviRecenziju(string tutorId, int ocjena, string komentar)
        {
            var student = await _userManager.GetUserAsync(User);
            if (student == null || student.Uloga != Uloga.Student)
                return View("PristupOdbijen");

            var moze = await _context.Termin
                .AnyAsync(t => t.idStudenta == student.Id
                            && t.idTutora == tutorId
                            && t.status == StatusTermina.Odrzan);

            if (!moze)
            {
                TempData["ErrorPoruka"] = "Ne možete ostaviti recenziju tutoru s kojim niste imali čas.";
                return RedirectToAction("Profil", "Tutor", new { id = tutorId });
            }

            var recenzija = new Recenzija
            {
                idStudenta = student.Id,
                idTutora = tutorId,
                ocjena = ocjena,
                komentar = komentar ?? "",
                datumRecenzije = DateTime.UtcNow
            };

            _context.Recenzija.Add(recenzija);

            var tutor = await _context.Users.FindAsync(tutorId);
            if (tutor != null)
            {
                var sveRecenzije = await _context.Recenzija
                    .Where(r => r.idTutora == tutorId)
                    .ToListAsync();
                tutor.ProsjecnaOcjena = sveRecenzije.Any()
                  ? sveRecenzije.Average(r => r.ocjena)
                 : 0;
                await _userManager.UpdateAsync(tutor);
            }

            await _context.SaveChangesAsync();

            TempData["Uspjeh"] = "Recenzija uspješno poslana!";
            return RedirectToAction("Profil", "Tutor", new { id = tutorId });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PrijaviTutora(string tutorId, string naslov, string opis)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return Unauthorized();

            var tutor = await _userManager.FindByIdAsync(tutorId);
            if (tutor == null)
                return NotFound();

            _context.Prijava.Add(new Prijava
            {
                Naslov = naslov,
                Opis = $"Prijava tutora {tutor.Ime} {tutor.Prezime}: {opis}",
                IdPrijavitelja = user.Id,
                DatumPrijave = DateTime.UtcNow,
                Status = Earn_Learn.Enums.StatusPrijave.Prijavljeno
            });
            await _context.SaveChangesAsync();

            TempData["Uspjeh"] = "Prijava je uspješno poslana adminu.";
            return RedirectToAction("Profil", "Tutor", new { id = tutorId });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PrijaviRecenziju(int recenzijaId, string tutorId, string naslov, string opis)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Tutor)
                return Unauthorized();

            _context.Prijava.Add(new Prijava
            {
                Naslov = naslov,
                Opis = $"Prijava recenzije (ID: {recenzijaId}): {opis}",
                IdPrijavitelja = user.Id,
                DatumPrijave = DateTime.UtcNow,
                Status = Earn_Learn.Enums.StatusPrijave.Prijavljeno
            });
            await _context.SaveChangesAsync();

            TempData["Uspjeh"] = "Prijava recenzije je poslana adminu.";
            return RedirectToAction("Recenzije", "Tutor", new { id = tutorId });
        }

        // ── OBAVJESTENJA ──

        [Authorize]
        public async Task<IActionResult> Obavjestenja()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return RedirectToAction("Index", "Home");

            var obavjestenja = await _context.Obavjestenje
                .Where(o => o.idKorisnika == user.Id)
                .OrderByDescending(o => o.datumSlanja)
                .ToListAsync();

            foreach (var o in obavjestenja.Where(o => !o.procitano))
                o.procitano = true;
            await _context.SaveChangesAsync();

            return View(obavjestenja);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> DohvatiObavjestenja()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var obavjestenja = await _context.Obavjestenje
                .Where(o => o.idKorisnika == user.Id)
                .OrderByDescending(o => o.datumSlanja)
                .Take(10)
                .Select(o => new {
                    o.id,
                    o.naslov,
                    o.sadrzaj,
                    o.datumSlanja,
                    o.procitano
                })
                .ToListAsync();

            return Json(obavjestenja);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> BrojObavjestenja()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var broj = await _context.Obavjestenje
                .CountAsync(o => o.idKorisnika == user.Id && !o.procitano);

            return Json(new { broj });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OznaciProcitanim(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var obavjestenje = await _context.Obavjestenje
                .FirstOrDefaultAsync(o => o.id == id && o.idKorisnika == user.Id);

            if (obavjestenje != null)
            {
                obavjestenje.procitano = true;
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OznaciSveProcitanim()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var neprocitana = await _context.Obavjestenje
                .Where(o => o.idKorisnika == user.Id && !o.procitano)
                .ToListAsync();

            foreach (var o in neprocitana)
                o.procitano = true;

            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}

namespace Earn_Learn.Controllers
{
    public class TutorSearchDto
    {
        public string Id { get; set; } = "";
        public string Ime { get; set; } = "";
        public string Prezime { get; set; } = "";
        public double ProsjecnaOcjena { get; set; }
        public double CijenaPoSatu { get; set; }
        public int BrojOdrzanihCasova { get; set; }
        public double Rang { get; set; }
        public List<string> Predmeti { get; set; } = new();
    }
}