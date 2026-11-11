using Earn_Learn.Controllers;
using Earn_Learn.Data;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Earn_Learn.Enums;
using System.Globalization;
using System.Text.Json;

namespace Earn_Learn.Tests
{
    [TestClass]
    public class TransakcijasControllerTests
    {
        // Pomoćna klasa: baza koja baca izuzetak pri spremanju (za granu s konkurentnošću)
        private class PadajucaBaza : ApplicationDbContext
        {
            public PadajucaBaza(DbContextOptions<ApplicationDbContext> opcije) : base(opcije) { }

            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
                => throw new DbUpdateConcurrencyException();
        }

        // Svaki test dobija svoju praznu bazu, pa su testovi neovisni
        private static DbContextOptions<ApplicationDbContext> NapraviOpcije()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        private static void UbaciTransakcije(DbContextOptions<ApplicationDbContext> opcije, params int[] ids)
        {
            using var baza = new ApplicationDbContext(opcije);
            foreach (var id in ids)
            {
                baza.Transakcija.Add(new Transakcija { id = id });
            }
            baza.SaveChanges();
        }

        // ---------- Index ----------

        [TestMethod]
        public async Task Index_ImaDvijeTransakcije_VracaViewSaObjeListe()
        {
            var opcije = NapraviOpcije();
            UbaciTransakcije(opcije, 1, 2);
            using var baza = new ApplicationDbContext(opcije);
            var controller = new TransakcijasController(baza);

            var result = await controller.Index() as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as List<Transakcija>;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.Count);
        }

        // ---------- Details ----------

        [TestMethod]
        public async Task Details_IdJeNull_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = new TransakcijasController(baza);

            var result = await controller.Details(null);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Details_NepostojeciId_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = new TransakcijasController(baza);

            var result = await controller.Details(99);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Details_PostojeciId_VracaViewSaTransakcijom()
        {
            var opcije = NapraviOpcije();
            UbaciTransakcije(opcije, 5);
            using var baza = new ApplicationDbContext(opcije);
            var controller = new TransakcijasController(baza);

            var result = await controller.Details(5) as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as Transakcija;
            Assert.IsNotNull(model);
            Assert.AreEqual(5, model.id);
        }

        // ---------- Create ----------

        [TestMethod]
        public void Create_GetPoziv_VracaView()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = new TransakcijasController(baza);

            var result = controller.Create();

            Assert.IsInstanceOfType<ViewResult>(result);
        }

        [TestMethod]
        public async Task Create_ValidanModel_SpremaIPreusmjeravaNaIndex()
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var controller = new TransakcijasController(baza);

            var result = await controller.Create(new Transakcija { id = 1 }) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(1, provjera.Transakcija.Count());
        }

        [TestMethod]
        public async Task Create_NevalidanModel_VracaViewBezSpremanja()
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var controller = new TransakcijasController(baza);
            controller.ModelState.AddModelError("iznos", "Obavezno polje");
            var transakcija = new Transakcija { id = 1 };

            var result = await controller.Create(transakcija) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(transakcija, result.Model);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(0, provjera.Transakcija.Count());
        }

        // ---------- Edit (GET) ----------

        [TestMethod]
        public async Task Edit_Get_IdJeNull_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = new TransakcijasController(baza);

            var result = await controller.Edit(null);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Edit_Get_NepostojeciId_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = new TransakcijasController(baza);

            var result = await controller.Edit(99);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Edit_Get_PostojeciId_VracaViewSaTransakcijom()
        {
            var opcije = NapraviOpcije();
            UbaciTransakcije(opcije, 3);
            using var baza = new ApplicationDbContext(opcije);
            var controller = new TransakcijasController(baza);

            var result = await controller.Edit(3) as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as Transakcija;
            Assert.IsNotNull(model);
            Assert.AreEqual(3, model.id);
        }

        // ---------- Edit (POST) ----------

        [TestMethod]
        public async Task Edit_Post_IdSeNePodudara_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = new TransakcijasController(baza);

            var result = await controller.Edit(1, new Transakcija { id = 2 });

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Edit_Post_ValidanModel_PreusmjeravaNaIndex()
        {
            var opcije = NapraviOpcije();
            UbaciTransakcije(opcije, 1);
            using var baza = new ApplicationDbContext(opcije);
            var controller = new TransakcijasController(baza);

            var result = await controller.Edit(1, new Transakcija { id = 1 }) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
        }

        [TestMethod]
        public async Task Edit_Post_NevalidanModel_VracaViewSaModelom()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = new TransakcijasController(baza);
            controller.ModelState.AddModelError("iznos", "Obavezno polje");
            var transakcija = new Transakcija { id = 1 };

            var result = await controller.Edit(1, transakcija) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreSame(transakcija, result.Model);
        }

        [TestMethod]
        public async Task Edit_Post_TransakcijaNePostojiUBazi_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = new TransakcijasController(baza);

            var result = await controller.Edit(1, new Transakcija { id = 1 });

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Edit_Post_KonkurentnostAliTransakcijaPostoji_BacaIzuzetak()
        {
            var opcije = NapraviOpcije();
            UbaciTransakcije(opcije, 1);
            using var baza = new PadajucaBaza(opcije);
            var controller = new TransakcijasController(baza);

            await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(
                () => controller.Edit(1, new Transakcija { id = 1 }));
        }

        // ---------- Delete (GET) ----------

        [TestMethod]
        public async Task Delete_Get_IdJeNull_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = new TransakcijasController(baza);

            var result = await controller.Delete(null);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Delete_Get_NepostojeciId_VracaNotFound()
        {
            using var baza = new ApplicationDbContext(NapraviOpcije());
            var controller = new TransakcijasController(baza);

            var result = await controller.Delete(99);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Delete_Get_PostojeciId_VracaViewSaTransakcijom()
        {
            var opcije = NapraviOpcije();
            UbaciTransakcije(opcije, 4);
            using var baza = new ApplicationDbContext(opcije);
            var controller = new TransakcijasController(baza);

            var result = await controller.Delete(4) as ViewResult;

            Assert.IsNotNull(result);
            var model = result.Model as Transakcija;
            Assert.IsNotNull(model);
            Assert.AreEqual(4, model.id);
        }

        // ---------- DeleteConfirmed ----------

        [TestMethod]
        public async Task DeleteConfirmed_PostojecaTransakcija_BriseJuIPreusmjerava()
        {
            var opcije = NapraviOpcije();
            UbaciTransakcije(opcije, 1);
            using var baza = new ApplicationDbContext(opcije);
            var controller = new TransakcijasController(baza);

            var result = await controller.DeleteConfirmed(1) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(0, provjera.Transakcija.Count());
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojecaTransakcija_PreusmjeravaBezBrisanja()
        {
            var opcije = NapraviOpcije();
            UbaciTransakcije(opcije, 1);
            using var baza = new ApplicationDbContext(opcije);
            var controller = new TransakcijasController(baza);

            var result = await controller.DeleteConfirmed(99) as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Index", result.ActionName);
            using var provjera = new ApplicationDbContext(opcije);
            Assert.AreEqual(1, provjera.Transakcija.Count());
        }


        // ---------- Data driven: JSON ----------

        private record TransakcijaPodaci(
            string IdStudenta, string IdTutora, double Iznos, int NacinPlacanja, int StatusPlacanja);

        public static IEnumerable<object[]> TransakcijeIzJsona()
        {
            var putanja = Path.Combine(AppContext.BaseDirectory, "TestData", "transakcije_create.json");
            var podaci = JsonSerializer.Deserialize<List<TransakcijaPodaci>>(
                File.ReadAllText(putanja),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            return podaci.Select(p => new object[]
                { p.IdStudenta, p.IdTutora, p.Iznos, p.NacinPlacanja, p.StatusPlacanja });
        }

        [TestMethod]
        [DynamicData(nameof(TransakcijeIzJsona))]
        public async Task Create_RazliciteTransakcijeIzJsona_SpremaTacnePodatke(
            string idStudenta, string idTutora, double iznos, int nacinPlacanja, int statusPlacanja)
        {
            var opcije = NapraviOpcije();
            using var baza = new ApplicationDbContext(opcije);
            var controller = new TransakcijasController(baza);
            var transakcija = new Transakcija
            {
                id = 1,
                idStudenta = idStudenta,
                idTutora = idTutora,
                iznos = iznos,
                datumUplate = new DateTime(2026, 11, 1),
                nacinPlacanja = (NacinPlacanja)nacinPlacanja,
                statusPlacanja = (StatusPlacanja)statusPlacanja
            };

            var result = await controller.Create(transakcija) as RedirectToActionResult;

            Assert.IsNotNull(result);
            using var provjera = new ApplicationDbContext(opcije);
            var spremljena = provjera.Transakcija.Single();
            Assert.AreEqual(idStudenta, spremljena.idStudenta);
            Assert.AreEqual(idTutora, spremljena.idTutora);
            Assert.AreEqual(iznos, spremljena.iznos);
            Assert.AreEqual((NacinPlacanja)nacinPlacanja, spremljena.nacinPlacanja);
            Assert.AreEqual((StatusPlacanja)statusPlacanja, spremljena.statusPlacanja);
        }

        // ---------- Data driven: CSV ----------

        public static IEnumerable<object[]> IznosiIzCsv()
        {
            var putanja = Path.Combine(AppContext.BaseDirectory, "TestData", "transakcije_edit.csv");
            return File.ReadAllLines(putanja)
                .Skip(1)
                .Select(red => red.Split(','))
                .Select(k => new object[]
                {
                    double.Parse(k[0], CultureInfo.InvariantCulture),
                    double.Parse(k[1], CultureInfo.InvariantCulture),
                    int.Parse(k[2])
                });
        }

        [TestMethod]
        [DynamicData(nameof(IznosiIzCsv))]
        public async Task Edit_NoviIznosIStatusIzCsv_AzuriraTransakciju(
            double pocetniIznos, double noviIznos, int noviStatus)
        {
            var opcije = NapraviOpcije();
            using (var priprema = new ApplicationDbContext(opcije))
            {
                priprema.Transakcija.Add(new Transakcija { id = 1, iznos = pocetniIznos });
                priprema.SaveChanges();
            }
            using var baza = new ApplicationDbContext(opcije);
            var controller = new TransakcijasController(baza);
            var izmijenjena = new Transakcija
            {
                id = 1,
                iznos = noviIznos,
                statusPlacanja = (StatusPlacanja)noviStatus
            };

            var result = await controller.Edit(1, izmijenjena) as RedirectToActionResult;

            Assert.IsNotNull(result);
            using var provjera = new ApplicationDbContext(opcije);
            var azurirana = provjera.Transakcija.Single();
            Assert.AreEqual(noviIznos, azurirana.iznos);
            Assert.AreEqual((StatusPlacanja)noviStatus, azurirana.statusPlacanja);
        }
    }
}