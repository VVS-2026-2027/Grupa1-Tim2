using Earn_Learn.Controllers;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
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

        // 1) Običan Assert test
        [TestMethod]
        public void Contact_PrazanIme_VracaPorukuOGresci()
        {
            var controller = new HomeController(NapraviUserManagerMock().Object);

            var result = controller.Contact("", "a@b.ba", "Poruka");

            Assert.IsInstanceOfType<ViewResult>(result);
            Assert.AreEqual("Sva polja su obavezna.", (string)controller.ViewBag.Greska);
        }

        // 2) Test sa Moq mockom
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

        // 3) Data-driven test, inline podaci (DynamicData)
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
    }
}