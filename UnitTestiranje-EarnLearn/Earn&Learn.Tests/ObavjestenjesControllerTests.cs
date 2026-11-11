using Earn_Learn.Controllers;
using Earn_Learn.Data;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Globalization;
using System.Text.Json;

namespace Earn_Learn.Tests
{
    /// <summary>
    /// Izvori podataka za data-driven testove (CSV i JSON).
    /// </summary>
    public static class ObavjestenjaTestData
    {
        private static string Putanja(string imeFajla) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", imeFajla);

        // CSV forma: nepostojeci id-evi
        public static IEnumerable<object[]> NepostojeciIdeviIzCsv =>
            File.ReadLines(Putanja("nepostojeci_ids.csv"))
                .Skip(1)
                .Select(linija => new object[] { int.Parse(linija.Trim(), CultureInfo.InvariantCulture) })
                .ToList();

        // JSON forma: validna obavjestenja
        public static IEnumerable<object[]> ValidnaObavjestenjaIzJson
        {
            get
            {
                var opcije = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var lista = JsonSerializer.Deserialize<List<Obavjestenje>>(
                    File.ReadAllText(Putanja("obavjestenja.json")), opcije)!;
                return lista.Select(o => new object[] { o }).ToList();
            }
        }
    }

    [TestClass]
    public class ObavjestenjesControllerTests
    {
        // ---------- Helperi ----------

        private static DbContextOptions<ApplicationDbContext> NoveOpcije() =>
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        private static void Seed(DbContextOptions<ApplicationDbContext> opcije, params Obavjestenje[] podaci)
        {
            using var ctx = new ApplicationDbContext(opcije);
            ctx.Obavjestenje.AddRange(podaci);
            ctx.SaveChanges();
        }

        private static Obavjestenje NovoObavjestenje(int id = 1) => new Obavjestenje
        {
            id = id,
            idKorisnika = "korisnik-10",
            naslov = "Naslov",
            sadrzaj = "Sadrzaj",
            datumSlanja = new DateTime(2026, 11, 10),
            procitano = false
        };

        private static ObavjestenjesController NapraviController(DbContextOptions<ApplicationDbContext> opcije) =>
            new ObavjestenjesController(new ApplicationDbContext(opcije));

        // Zamjenski objekat (mock) baze koji baca izuzetak pri snimanju
        private static Mock<ApplicationDbContext> MockContextKojiBacaConcurrency(
            DbContextOptions<ApplicationDbContext> opcije)
        {
            var mock = new Mock<ApplicationDbContext>(opcije) { CallBase = true };
            mock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DbUpdateConcurrencyException());
            return mock;
        }

        // ---------- Index ----------

        [TestMethod]
        public async Task Index_PostojeObavjestenja_VracaViewSaSvimObavjestenjima()
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovoObavjestenje(1), NovoObavjestenje(2));
            var controller = NapraviController(opcije);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Obavjestenje>;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.Count);
        }

        [TestMethod]
        public async Task Index_PrazanSkup_VracaPrazanModel()
        {
            var controller = NapraviController(NoveOpcije());

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Obavjestenje>;
            Assert.IsNotNull(model);
            Assert.AreEqual(0, model.Count);
        }

        // ---------- Details ----------

        [TestMethod]
        public async Task Details_NullId_VracaNotFound()
        {
            var controller = NapraviController(NoveOpcije());

            var result = await controller.Details(null);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        [DynamicData(nameof(ObavjestenjaTestData.NepostojeciIdeviIzCsv), typeof(ObavjestenjaTestData))]
        public async Task Details_NepostojeciId_VracaNotFound(int id)
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovoObavjestenje(1));
            var controller = NapraviController(opcije);

            var result = await controller.Details(id);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Details_PostojeciId_VracaViewSaObavjestenjem()
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovoObavjestenje(5));
            var controller = NapraviController(opcije);

            var result = await controller.Details(5) as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as Obavjestenje;
            Assert.IsNotNull(model);
            Assert.AreEqual(5, model.id);
        }

        // ---------- Create ----------

        [TestMethod]
        public void Create_Get_VracaView()
        {
            var controller = NapraviController(NoveOpcije());

            var result = controller.Create();

            Assert.IsInstanceOfType<ViewResult>(result);
        }

        [TestMethod]
        [DynamicData(nameof(ObavjestenjaTestData.ValidnaObavjestenjaIzJson), typeof(ObavjestenjaTestData))]
        public async Task Create_ValidanModel_SnimaIPreusmjeravaNaIndex(Obavjestenje obavjestenje)
        {
            var opcije = NoveOpcije();
            var controller = NapraviController(opcije);

            var result = await controller.Create(obavjestenje) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(nameof(ObavjestenjesController.Index), result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(1, provjera.Obavjestenje.Count());
        }

        [TestMethod]
        public async Task Create_NevalidanModel_VracaViewBezSnimanja()
        {
            var opcije = NoveOpcije();
            var controller = NapraviController(opcije);
            controller.ModelState.AddModelError("naslov", "Obavezno polje");
            var obavjestenje = NovoObavjestenje();

            var result = await controller.Create(obavjestenje) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(obavjestenje, result.Model);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(0, provjera.Obavjestenje.Count());
        }

        // ---------- Edit (GET) ----------

        [TestMethod]
        public async Task Edit_Get_NullId_VracaNotFound()
        {
            var controller = NapraviController(NoveOpcije());

            var result = await controller.Edit(null);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        [DynamicData(nameof(ObavjestenjaTestData.NepostojeciIdeviIzCsv), typeof(ObavjestenjaTestData))]
        public async Task Edit_Get_NepostojeciId_VracaNotFound(int id)
        {
            var controller = NapraviController(NoveOpcije());

            var result = await controller.Edit(id);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Edit_Get_PostojeciId_VracaViewSaObavjestenjem()
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovoObavjestenje(3));
            var controller = NapraviController(opcije);

            var result = await controller.Edit(3) as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as Obavjestenje;
            Assert.IsNotNull(model);
            Assert.AreEqual(3, model.id);
        }

        // ---------- Edit (POST) ----------

        [TestMethod]
        public async Task Edit_Post_RazlicitId_VracaNotFound()
        {
            var controller = NapraviController(NoveOpcije());

            var result = await controller.Edit(1, NovoObavjestenje(2));

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Edit_Post_ValidanModel_AzuriraIPreusmjeravaNaIndex()
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovoObavjestenje(1));
            var controller = NapraviController(opcije);
            var izmijenjeno = NovoObavjestenje(1);
            izmijenjeno.naslov = "Izmijenjen naslov";

            var result = await controller.Edit(1, izmijenjeno) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(nameof(ObavjestenjesController.Index), result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual("Izmijenjen naslov", provjera.Obavjestenje.Find(1)!.naslov);
        }

        [TestMethod]
        public async Task Edit_Post_NevalidanModel_VracaView()
        {
            var controller = NapraviController(NoveOpcije());
            controller.ModelState.AddModelError("naslov", "Obavezno polje");
            var obavjestenje = NovoObavjestenje(1);

            var result = await controller.Edit(1, obavjestenje) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(obavjestenje, result.Model);
        }

        [TestMethod]
        public async Task Edit_Post_ConcurrencyIzuzetakIObjekatNePostoji_VracaNotFound()
        {
            var mockContext = MockContextKojiBacaConcurrency(NoveOpcije());
            var controller = new ObavjestenjesController(mockContext.Object);

            var result = await controller.Edit(1, NovoObavjestenje(1));

            Assert.IsInstanceOfType<NotFoundResult>(result);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task Edit_Post_ConcurrencyIzuzetakIObjekatPostoji_PropagiraIzuzetak()
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovoObavjestenje(1));
            var mockContext = MockContextKojiBacaConcurrency(opcije);
            var controller = new ObavjestenjesController(mockContext.Object);

            await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(
                () => controller.Edit(1, NovoObavjestenje(1)));
        }

        // ---------- Delete (GET) ----------

        [TestMethod]
        public async Task Delete_Get_NullId_VracaNotFound()
        {
            var controller = NapraviController(NoveOpcije());

            var result = await controller.Delete(null);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        [DynamicData(nameof(ObavjestenjaTestData.NepostojeciIdeviIzCsv), typeof(ObavjestenjaTestData))]
        public async Task Delete_Get_NepostojeciId_VracaNotFound(int id)
        {
            var controller = NapraviController(NoveOpcije());

            var result = await controller.Delete(id);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Delete_Get_PostojeciId_VracaViewSaObavjestenjem()
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovoObavjestenje(4));
            var controller = NapraviController(opcije);

            var result = await controller.Delete(4) as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as Obavjestenje;
            Assert.IsNotNull(model);
            Assert.AreEqual(4, model.id);
        }

        // ---------- DeleteConfirmed ----------

        [TestMethod]
        public async Task DeleteConfirmed_PostojeciId_BriseIPreusmjeravaNaIndex()
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovoObavjestenje(1));
            var controller = NapraviController(opcije);

            var result = await controller.DeleteConfirmed(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(nameof(ObavjestenjesController.Index), result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(0, provjera.Obavjestenje.Count());
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojeciId_PreusmjeravaNaIndexBezBrisanja()
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovoObavjestenje(1));
            var controller = NapraviController(opcije);

            var result = await controller.DeleteConfirmed(999);

            Assert.IsInstanceOfType<RedirectToActionResult>(result);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(1, provjera.Obavjestenje.Count());
        }
    }
}