using Earn_Learn.Enums;
using Earn_Learn.Models;
using Earn_Learn.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Earn_Learn.Tests
{
    /// <summary>
    /// Izvori podataka za data-driven testove RoleOnlyAttribute-a (CSV i JSON).
    /// </summary>
    public static class RoleOnlyTestData
    {
        private static string Putanja(string imeFajla) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", imeFajla);

        // CSV forma: parovi (dozvoljena uloga, stvarna uloga korisnika) koji se razlikuju
        public static IEnumerable<object[]> NedozvoljeneKombinacije =>
            File.ReadLines(Putanja("role_only_nedozvoljene_kombinacije.csv"))
                .Skip(1)
                .Select(linija => linija.Split(','))
                .Select(k => new object[] { Enum.Parse<Uloga>(k[0].Trim()), Enum.Parse<Uloga>(k[1].Trim()) })
                .ToList();

        // JSON forma: uloge za koje korisnik smije pristupiti kad se uloga poklapa
        public static IEnumerable<object[]> Uloge
        {
            get
            {
                var opcije = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
                var uloge = JsonSerializer.Deserialize<List<Uloga>>(
                    File.ReadAllText(Putanja("role_only_uloge.json")), opcije)!;
                return uloge.Select(u => new object[] { u }).ToList();
            }
        }
    }

    [TestClass]
    public class RoleOnlyAttributeTests
    {
        private Mock<UserManager<Korisnik>> _userManager = null!;

        [TestInitialize]
        public void Postavi()
        {
            var store = new Mock<IUserStore<Korisnik>>();
            _userManager = new Mock<UserManager<Korisnik>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        // ---------- Helperi ----------

        private ActionExecutingContext NapraviKontekst(bool saAllowAnonymous)
        {
            var servisi = new ServiceCollection()
                .AddSingleton(_userManager.Object)
                .BuildServiceProvider();

            var httpContext = new DefaultHttpContext
            {
                RequestServices = servisi,
                User = new ClaimsPrincipal(new ClaimsIdentity())
            };

            var metadata = new List<object>();
            if (saAllowAnonymous)
            {
                metadata.Add(new AllowAnonymousAttribute());
            }

            var actionContext = new ActionContext(
                httpContext, new RouteData(), new ActionDescriptor { EndpointMetadata = metadata });

            return new ActionExecutingContext(
                actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
        }

        private void PostaviKorisnika(Korisnik? korisnik)
        {
            _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(korisnik);
        }

        // ---------- AllowAnonymous ----------

        [TestMethod]
        public void OnActionExecuting_AkcijaImaAllowAnonymous_PropustaBezProvjereKorisnika()
        {
            var atribut = new RoleOnlyAttribute(Uloga.SystemManager);
            var kontekst = NapraviKontekst(saAllowAnonymous: true);

            atribut.OnActionExecuting(kontekst);

            Assert.IsNull(kontekst.Result);
            _userManager.Verify(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()), Times.Never);
        }

        // ---------- Nema korisnika ----------

        [TestMethod]
        public void OnActionExecuting_KorisnikNePostoji_VracaPristupOdbijenSaStatusom403()
        {
            PostaviKorisnika(null);
            var atribut = new RoleOnlyAttribute(Uloga.Student);
            var kontekst = NapraviKontekst(saAllowAnonymous: false);

            atribut.OnActionExecuting(kontekst);

            var rezultat = kontekst.Result as ViewResult;
            Assert.IsNotNull(rezultat);
            Assert.AreEqual("PristupOdbijen", rezultat.ViewName);
            Assert.AreEqual((int?)403, rezultat.StatusCode);
        }

        // ---------- Pogresna uloga ----------

        [TestMethod]
        [DynamicData(nameof(RoleOnlyTestData.NedozvoljeneKombinacije), typeof(RoleOnlyTestData))]
        public void OnActionExecuting_UlogaSeNePoklapa_VracaPristupOdbijenSaStatusom403(
            Uloga dozvoljenaUloga, Uloga stvarnaUloga)
        {
            PostaviKorisnika(new Korisnik { Uloga = stvarnaUloga });
            var atribut = new RoleOnlyAttribute(dozvoljenaUloga);
            var kontekst = NapraviKontekst(saAllowAnonymous: false);

            atribut.OnActionExecuting(kontekst);

            var rezultat = kontekst.Result as ViewResult;
            Assert.IsNotNull(rezultat);
            Assert.AreEqual("PristupOdbijen", rezultat.ViewName);
            Assert.AreEqual((int?)403, rezultat.StatusCode);
        }

        // ---------- Ispravna uloga ----------

        [TestMethod]
        [DynamicData(nameof(RoleOnlyTestData.Uloge), typeof(RoleOnlyTestData))]
        public void OnActionExecuting_UlogaSePoklapa_PropustaZahtjev(Uloga uloga)
        {
            PostaviKorisnika(new Korisnik { Uloga = uloga });
            var atribut = new RoleOnlyAttribute(uloga);
            var kontekst = NapraviKontekst(saAllowAnonymous: false);

            atribut.OnActionExecuting(kontekst);

            Assert.IsNull(kontekst.Result);
        }
    }
}