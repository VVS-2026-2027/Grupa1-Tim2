using Earn_Learn.Enums;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Earn_Learn.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<Korisnik> _userManager;

        public HomeController(UserManager<Korisnik> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // Ako je korisnik već prijavljen (authenticated), odmah ga šaljemo na njegov Dashboard
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return await RedirectDashboard();
            }

            return View();
        }

        public async Task<IActionResult> RedirectDashboard()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToAction("Index");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Index");

            return user.Uloga switch
            {
                Uloga.Student => RedirectToAction("Dashboard", "Student"),
                Uloga.Tutor => RedirectToAction("Dashboard", "Tutor"),
                Uloga.SystemManager => RedirectToAction("Dashboard", "SystemManager"),
                _ => RedirectToAction("Index")
            };
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Terms()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult PristupOdbijen()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Contact(string ime, string email, string poruka)
        {
            if (string.IsNullOrWhiteSpace(ime) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(poruka))
            {
                ViewBag.Greska = "Sva polja su obavezna.";
                return View();
            }

            ViewBag.Uspjeh = "Vaša poruka je uspješno poslana! Javit ćemo vam se uskoro.";
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

    }
}