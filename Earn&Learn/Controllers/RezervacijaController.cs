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
    public class RezervacijaController : Controller
    {
        private readonly UserManager<Korisnik> _userManager;
        private readonly ApplicationDbContext _context;

        public RezervacijaController(UserManager<Korisnik> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Potvrdi(int id, TipInstrukcija tipInstrukcija)
        {
            var studentId = _userManager.GetUserId(User);
            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == studentId);

            if (student == null || student.Uloga == Uloga.SystemManager)
                return RedirectToAction("PristupOdbijen", "Home");

            var termin = await _context.Termin
                .FirstOrDefaultAsync(t => t.id == id && t.status == StatusTermina.Slobodan);

            if (termin == null)
            {
                TempData["Greska"] = "Termin nije dostupan.";
                return RedirectToAction("Dashboard", "Student");
            }

            var tutor = await _context.Users.FindAsync(termin.idTutora);
            double cijena = tutor?.CijenaPoSatu ?? termin.cijena;

            if (student.StanjeRacuna < cijena)
            {
                TempData["GreskaNovcenik"] = $"Nemate dovoljno sredstava! Potrebno: {cijena:N2} KM, dostupno: {student.StanjeRacuna:N2} KM.";
                return RedirectToAction("Profil", "Tutor", new { id = termin.idTutora });
            }

            termin.idStudenta = student.Id;
            termin.status = StatusTermina.Rezervisan;
            termin.tipInstrukcija = tipInstrukcija;
            termin.cijena = cijena;

            _context.Update(termin);

            _context.Obavjestenje.Add(new Obavjestenje
            {
                idKorisnika = termin.idTutora,
                naslov = "Novi termin rezervisan 📅",
                sadrzaj = $"{student.Ime} {student.Prezime} je rezervisao vaš termin {termin.datumIVrijeme:dd.MM.yyyy. u HH:mm}h ({tipInstrukcija}).",
                datumSlanja = DateTime.UtcNow,
                procitano = false,
                idTermina = termin.id
            });

            await _context.SaveChangesAsync();

            TempData["Uspjeh"] = "Termin uspješno rezervisan!";

            // Redirect ovisno o ulozi
            if (student.Uloga == Uloga.Tutor)
                return RedirectToAction("Dashboard", "Tutor");

            return RedirectToAction("Dashboard", "Student");
        }
    }
}