using Earn_Learn.Controllers;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Diagnostics;
using System.Security.Claims;

namespace Earn_Learn.Tests
{
    [TestClass]
    public class HomeControllerTests
    {
        private static Mock<UserManager<Korisnik>> NapraviUserManagerMock()
        {
            var store = new Mock<IUserStore<Korisnik>>();
            return new Mock<UserManager<Korisnik>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        private static ControllerContext NapraviPrijavljenKontekst()
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "test") }, "TestAuth");
            return new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        private static ControllerContext NapraviNeprijavljenKontekst()
        {
            return new ControllerContext { HttpContext = new DefaultHttpContext() };
        }

        // ---------- Contact ----------

        [TestMethod]
        public void Contact_PrazanIme_VracaPorukuOGresci()
        {
            var controller = new HomeController(NapraviUserManagerMock().Object);

            var result = controller.Contact("", "a@b.ba", "Poruka");

            Assert.IsInstanceOfType<ViewResult>(result);
            Assert.AreEqual("Sva polja su obavezna.", (string)controller.ViewBag.Greska);
        }

        [TestMethod]
        public void Contact_SvaPoljaPopunjena_VracaPorukuOUspjehu()
        {
            var controller = new HomeController(NapraviUserManagerMock().Object);

            var result = controller.Contact("Amar", "a@b.ba", "Poruka");

            Assert.IsInstanceOfType<ViewResult>(result);
            Assert.AreEqual("Vaša poruka je uspješno poslana! Javit ćemo vam se uskoro.",
                            (string)controller.ViewBag.Uspjeh);
            Assert.IsNull((string?)controller.ViewBag.Greska);
        }

        [TestMethod]
        public void Contact_GetPoziv_VracaView()
        {
            var controller = new HomeController(NapraviUserManagerMock().Object);

            var result = controller.Contact();

            Assert.IsInstanceOfType<ViewResult>(result);
        }

        // ---------- Akcije koje vraćaju View ----------

        [TestMethod]
        public void About_Poziv_VracaView()
        {
            var controller = new HomeController(NapraviUserManagerMock().Object);

            var result = controller.About();

            Assert.IsInstanceOfType<ViewResult>(result);
        }

        [TestMethod]
        public void Privacy_Poziv_VracaView()
        {
            var controller = new HomeController(NapraviUserManagerMock().Object);

            var result = controller.Privacy();

            Assert.IsInstanceOfType<ViewResult>(result);
        }

        [TestMethod]
        public void Terms_Poziv_VracaView()
        {
            var controller = new HomeController(NapraviUserManagerMock().Object);

            var result = controller.Terms();

            Assert.IsInstanceOfType<ViewResult>(result);
        }

        [TestMethod]
        public void PristupOdbijen_Poziv_VracaView()
        {
            var controller = new HomeController(NapraviUserManagerMock().Object);

            var result = controller.PristupOdbijen();

            Assert.IsInstanceOfType<ViewResult>(result);
        }

        // ---------- Index ----------

        [TestMethod]
        public async Task Index_NeprijavljenKorisnik_VracaView()
        {
            var controller = new HomeController(NapraviUserManagerMock().Object)
            {
                ControllerContext = NapraviNeprijavljenKontekst()
            };

            var result = await controller.Index();

            Assert.IsInstanceOfType<ViewResult>(result);
        }

        [TestMethod]
        public async Task Index_PrijavljenTutor_PreusmjeravaNaTutorDashboard()
        {
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                       .ReturnsAsync(new Korisnik { Uloga = Uloga.Tutor });
            var controller = new HomeController(userManager.Object)
            {
                ControllerContext = NapraviPrijavljenKontekst()
            };

            var result = await controller.Index() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Tutor", result.ControllerName);
        }

        // ---------- RedirectDashboard ----------

        [TestMethod]
        public async Task RedirectDashboard_PrijavljenStudent_PreusmjeravaNaStudentDashboard()
        {
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                       .ReturnsAsync(new Korisnik { Uloga = Uloga.Student });
            var controller = new HomeController(userManager.Object)
            {
                ControllerContext = NapraviPrijavljenKontekst()
            };

            var result = await controller.RedirectDashboard() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Dashboard", result.ActionName);
            Assert.AreEqual("Student", result.ControllerName);
        }

        public static IEnumerable<object[]> UlogeIOdredista =>
            new List<object[]>
            {
                new object[] { Uloga.Student, "Student" },
                new object[] { Uloga.Tutor, "Tutor" },
                new object[] { Uloga.SystemManager, "SystemManager" }
            };

        [TestMethod]
        [DynamicData(nameof(UlogeIOdredista))]
        public async Task RedirectDashboard_RazlicitaUloga_PreusmjeravaNaOdgovarajuciController(
            Uloga uloga, string ocekivaniController)
        {
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                       .ReturnsAsync(new Korisnik { Uloga = uloga });
            var controller = new HomeController(userManager.Object)
            {
                ControllerContext = NapraviPrijavljenKontekst()
            };

            var result = await controller.RedirectDashboard() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(ocekivaniController, result.ControllerName);
        }

        [TestMethod]
        public async Task RedirectDashboard_NeprijavljenKorisnik_PreusmjeravaNaIndex()
        {
            var controller = new HomeController(NapraviUserManagerMock().Object)
            {
                ControllerContext = NapraviNeprijavljenKontekst()
            };

            var result = await controller.RedirectDashboard() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
        }

        [TestMethod]
        public async Task RedirectDashboard_KorisnikNePostoji_PreusmjeravaNaIndex()
        {
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                       .ReturnsAsync((Korisnik?)null);
            var controller = new HomeController(userManager.Object)
            {
                ControllerContext = NapraviPrijavljenKontekst()
            };

            var result = await controller.RedirectDashboard() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
        }

        [TestMethod]
        public async Task RedirectDashboard_NepoznataUloga_PreusmjeravaNaIndex()
        {
            var userManager = NapraviUserManagerMock();
            userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                       .ReturnsAsync(new Korisnik { Uloga = (Uloga)999 });
            var controller = new HomeController(userManager.Object)
            {
                ControllerContext = NapraviPrijavljenKontekst()
            };

            var result = await controller.RedirectDashboard() as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
        }

        // ---------- Error ----------

        [TestMethod]
        public void Error_BezAktivneAktivnosti_KoristiTraceIdentifier()
        {
            Activity.Current = null;
            var controller = new HomeController(NapraviUserManagerMock().Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-123" }
                }
            };

            var result = controller.Error() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as ErrorViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual("trace-123", model.RequestId);
        }

        [TestMethod]
        public void Error_SaAktivnomAktivnosti_KoristiIdAktivnosti()
        {
            using var aktivnost = new Activity("test").Start();
            var controller = new HomeController(NapraviUserManagerMock().Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var result = controller.Error() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as ErrorViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(aktivnost.Id, model.RequestId);
        }
    }
}