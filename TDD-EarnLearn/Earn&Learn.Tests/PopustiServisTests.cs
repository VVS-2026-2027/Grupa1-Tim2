using Earn_Learn.Services;
namespace Earn_Learn.Tests
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
    }
}