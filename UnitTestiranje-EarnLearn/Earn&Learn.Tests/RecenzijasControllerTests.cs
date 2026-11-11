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
    public static class RecenzijaTestData
    {
        private static string Putanja(string imeFajla) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", imeFajla);

        // CSV forma: nepostojeci id-evi
        public static IEnumerable<object[]> NepostojeciIdeviIzCsv =>
            File.ReadLines(Putanja("nepostojeci_ids.csv"))
                .Skip(1)
                .Select(linija => new object[] { int.Parse(linija.Trim(), CultureInfo.InvariantCulture) })
                .ToList();

        // JSON forma: validne recenzije
        public static IEnumerable<object[]> ValidneRecenzijeIzJson
        {
            get
            {
                var opcije = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var lista = JsonSerializer.Deserialize<List<Recenzija>>(
                    File.ReadAllText(Putanja("recenzije.json")), opcije)!;
                return lista.Select(r => new object[] { r }).ToList();
            }
        }
    }

    [TestClass]
    public class RecenzijasControllerTests
    {
        // ---------- Helperi ----------

        private static DbContextOptions<ApplicationDbContext> NoveOpcije() =>
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        private static void Seed(DbContextOptions<ApplicationDbContext> opcije, params Recenzija[] podaci)
        {
            using var ctx = new ApplicationDbContext(opcije);
            ctx.Recenzija.AddRange(podaci);
            ctx.SaveChanges();
        }

        private static Recenzija NovaRecenzija(int id = 1) => new Recenzija
        {
            id = id,
            idStudenta = "student-1",
            idTutora = "tutor-1",
            ocjena = 5,
            komentar = "Odlican tutor"
        };

        private static RecenzijasController NapraviController(DbContextOptions<ApplicationDbContext> opcije) =>
            new RecenzijasController(new ApplicationDbContext(opcije));

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
        public async Task Index_PostojeRecenzije_VracaViewSaSvimRecenzijama()
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovaRecenzija(1), NovaRecenzija(2));
            var controller = NapraviController(opcije);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Recenzija>;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.Count);
        }

        [TestMethod]
        public async Task Index_PrazanSkup_VracaPrazanModel()
        {
            var controller = NapraviController(NoveOpcije());

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Recenzija>;
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
        [DynamicData(nameof(RecenzijaTestData.NepostojeciIdeviIzCsv), typeof(RecenzijaTestData))]
        public async Task Details_NepostojeciId_VracaNotFound(int id)
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovaRecenzija(1));
            var controller = NapraviController(opcije);

            var result = await controller.Details(id);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Details_PostojeciId_VracaViewSaRecenzijom()
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovaRecenzija(5));
            var controller = NapraviController(opcije);

            var result = await controller.Details(5) as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as Recenzija;
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
        [DynamicData(nameof(RecenzijaTestData.ValidneRecenzijeIzJson), typeof(RecenzijaTestData))]
        public async Task Create_ValidanModel_SnimaIPreusmjeravaNaIndex(Recenzija recenzija)
        {
            var opcije = NoveOpcije();
            var controller = NapraviController(opcije);

            var result = await controller.Create(recenzija) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(nameof(RecenzijasController.Index), result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(1, provjera.Recenzija.Count());
        }

        [TestMethod]
        public async Task Create_NevalidanModel_VracaViewBezSnimanja()
        {
            var opcije = NoveOpcije();
            var controller = NapraviController(opcije);
            controller.ModelState.AddModelError("ocjena", "Obavezno polje");
            var recenzija = NovaRecenzija();

            var result = await controller.Create(recenzija) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(recenzija, result.Model);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(0, provjera.Recenzija.Count());
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
        [DynamicData(nameof(RecenzijaTestData.NepostojeciIdeviIzCsv), typeof(RecenzijaTestData))]
        public async Task Edit_Get_NepostojeciId_VracaNotFound(int id)
        {
            var controller = NapraviController(NoveOpcije());

            var result = await controller.Edit(id);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Edit_Get_PostojeciId_VracaViewSaRecenzijom()
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovaRecenzija(3));
            var controller = NapraviController(opcije);

            var result = await controller.Edit(3) as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as Recenzija;
            Assert.IsNotNull(model);
            Assert.AreEqual(3, model.id);
        }

        // ---------- Edit (POST) ----------

        [TestMethod]
        public async Task Edit_Post_RazlicitId_VracaNotFound()
        {
            var controller = NapraviController(NoveOpcije());

            var result = await controller.Edit(1, NovaRecenzija(2));

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Edit_Post_ValidanModel_AzuriraIPreusmjeravaNaIndex()
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovaRecenzija(1));
            var controller = NapraviController(opcije);
            var izmijenjena = NovaRecenzija(1);
            izmijenjena.komentar = "Izmijenjen komentar";

            var result = await controller.Edit(1, izmijenjena) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(nameof(RecenzijasController.Index), result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual("Izmijenjen komentar", provjera.Recenzija.Find(1)!.komentar);
        }

        [TestMethod]
        public async Task Edit_Post_NevalidanModel_VracaView()
        {
            var controller = NapraviController(NoveOpcije());
            controller.ModelState.AddModelError("ocjena", "Obavezno polje");
            var recenzija = NovaRecenzija(1);

            var result = await controller.Edit(1, recenzija) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(recenzija, result.Model);
        }

        [TestMethod]
        public async Task Edit_Post_ConcurrencyIzuzetakIObjekatNePostoji_VracaNotFound()
        {
            var mockContext = MockContextKojiBacaConcurrency(NoveOpcije());
            var controller = new RecenzijasController(mockContext.Object);

            var result = await controller.Edit(1, NovaRecenzija(1));

            Assert.IsInstanceOfType<NotFoundResult>(result);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task Edit_Post_ConcurrencyIzuzetakIObjekatPostoji_PropagiraIzuzetak()
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovaRecenzija(1));
            var mockContext = MockContextKojiBacaConcurrency(opcije);
            var controller = new RecenzijasController(mockContext.Object);

            await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(
                () => controller.Edit(1, NovaRecenzija(1)));
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
        [DynamicData(nameof(RecenzijaTestData.NepostojeciIdeviIzCsv), typeof(RecenzijaTestData))]
        public async Task Delete_Get_NepostojeciId_VracaNotFound(int id)
        {
            var controller = NapraviController(NoveOpcije());

            var result = await controller.Delete(id);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Delete_Get_PostojeciId_PreusmjeravaNaPristupOdbijen()
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovaRecenzija(4));
            var controller = NapraviController(opcije);

            var result = await controller.Delete(4) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("PristupOdbijen", result.ActionName);
            Assert.AreEqual("Home", result.ControllerName);
        }

        // ---------- DeleteConfirmed ----------

        [TestMethod]
        public async Task DeleteConfirmed_PostojeciId_BriseIPreusmjeravaNaIndex()
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovaRecenzija(1));
            var controller = NapraviController(opcije);

            var result = await controller.DeleteConfirmed(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(nameof(RecenzijasController.Index), result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(0, provjera.Recenzija.Count());
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojeciId_PreusmjeravaNaIndexBezBrisanja()
        {
            var opcije = NoveOpcije();
            Seed(opcije, NovaRecenzija(1));
            var controller = NapraviController(opcije);

            var result = await controller.DeleteConfirmed(999);

            Assert.IsInstanceOfType<RedirectToActionResult>(result);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(1, provjera.Recenzija.Count());
        }
    }
}