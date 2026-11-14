using Earn_Learn.Services;
namespace Earn_Learn.Tests
using System.Text.Json;

{
    [TestClass]
    public class PopustServisTests
    {
        private readonly PopustServis _servis = new PopustServis();
        // ---------- IzracunajProcenatPopusta ----------
        public static IEnumerable<object[]> ProcentiIzCsv()
        {
            var putanja = Path.Combine(AppContext.BaseDirectory, "TestData",
            "popust_procenti.csv");
            return File.ReadAllLines(putanja)
                .Skip(1)
                .Select(red => red.Split(','))
                .Select(k => new object[] { int.Parse(k[0]), int.Parse(k[1])
});
        }
        [TestMethod]
        [DynamicData(nameof(ProcentiIzCsv))]
        public void
IzracunajProcenatPopusta_BrojCasovaIzCsv_VracaOcekivaniProcenat(int
brojCasova, int ocekivaniProcenat)
        {
            var procenat = _servis.IzracunajProcenatPopusta(brojCasova);
            Assert.AreEqual(ocekivaniProcenat, procenat);
        }
        [TestMethod]
        public void IzracunajProcenatPopusta_NovStudent_NemaPopusta()
        {
            var procenat = _servis.IzracunajProcenatPopusta(0);
            Assert.AreEqual(0, procenat);
        }
        [TestMethod]
        public void IzracunajProcenatPopusta_TacnoPetCasova_PetPosto()
        {
            var procenat = _servis.IzracunajProcenatPopusta(5);
            Assert.AreEqual(5, procenat);
        }
        [TestMethod]
        public void IzracunajProcenatPopusta_TacnoDesetCasova_DesetPosto()
        {
            var procenat = _servis.IzracunajProcenatPopusta(10);
            Assert.AreEqual(10, procenat);
        }
        [TestMethod]
        [DataRow(-1)]
        [DataRow(-10)]
        public void
IzracunajProcenatPopusta_NegativanBrojCasova_BacaIzuzetak(int brojCasova)
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            _servis.IzracunajProcenatPopusta(brojCasova));
        }
    // ---------- IzracunajCijenuSaPopustom ----------
    private record CijenaPodaci(double Cijena, int BrojCasova, double
OcekivanaCijena);
    public static IEnumerable<object[]> CijeneIzJsona()
    {
        var putanja = Path.Combine(AppContext.BaseDirectory, "TestData",
"popust_cijene.json");
        var podaci = JsonSerializer.Deserialize<List<CijenaPodaci>>(
            File.ReadAllText(putanja),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive =
true
            })!;
        return podaci.Select(p => new object[] { p.Cijena, p.BrojCasova,
p.OcekivanaCijena });
    }
    [TestMethod]
    [DynamicData(nameof(CijeneIzJsona))]
    public void
IzracunajCijenuSaPopustom_PodaciIzJsona_VracaOcekivanuCijenu(
        double cijena, int brojCasova, double ocekivanaCijena)
    {
        var novaCijena = _servis.IzracunajCijenuSaPopustom(cijena,
            brojCasova);
        Assert.AreEqual(ocekivanaCijena, novaCijena, 0.001);
    }
    [TestMethod]
    public void IzracunajCijenuSaPopustom_BezPopusta_CijenaOstajeIsta()
    {
        var novaCijena = _servis.IzracunajCijenuSaPopustom(25, 2);
        Assert.AreEqual(25.0, novaCijena, 0.001);
    }
    [TestMethod]
    public void
IzracunajCijenuSaPopustom_RezultatSeZaokruzujeNaDvijeDecimale()
    {
        var novaCijena = _servis.IzracunajCijenuSaPopustom(17.5, 6);
        Assert.AreEqual(16.63, novaCijena, 0.0001);
    }
    [TestMethod]
    public void IzracunajCijenuSaPopustom_NegativnaCijena_BacaIzuzetak()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
_servis.IzracunajCijenuSaPopustom(-1, 5));
    }
    [TestMethod]
    public void
IzracunajCijenuSaPopustom_NegativanBrojCasova_BacaIzuzetak()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
_servis.IzracunajCijenuSaPopustom(20, -1));
    }
}
}