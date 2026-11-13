using Earn_Learn.Services;
using System.Globalization;

namespace Earn_Learn.Tests
{
    [TestClass]
    public class IsplataServisTests
    {
        private readonly IsplataServis _servis = new IsplataServis();

        // ---------- IzracunajNaknadu ----------

        public static IEnumerable<object[]> NaknadeIzCsv()
        {
            var putanja = Path.Combine(AppContext.BaseDirectory, "TestData", "isplata_naknade.csv");
            return File.ReadAllLines(putanja)
                .Skip(1)
                .Select(red => red.Split(','))
                .Select(k => new object[]
                {
                    double.Parse(k[0], CultureInfo.InvariantCulture),
                    double.Parse(k[1], CultureInfo.InvariantCulture)
                });
        }

        [TestMethod]
        [DynamicData(nameof(NaknadeIzCsv))]
        public void IzracunajNaknadu_IznosIzCsv_VracaOcekivanuNaknadu(double iznos, double ocekivanaNaknada)
        {
            var naknada = _servis.IzracunajNaknadu(iznos);

            Assert.AreEqual(ocekivanaNaknada, naknada, 0.001);
        }

        [TestMethod]
        public void IzracunajNaknadu_MaliIznos_NaknadaJeNajmanjeJedanKm()
        {
            var naknada = _servis.IzracunajNaknadu(20);

            Assert.AreEqual(1.0, naknada, 0.001);
        }

        [TestMethod]
        public void IzracunajNaknadu_VelikiIznos_NaknadaJeDvaPosto()
        {
            var naknada = _servis.IzracunajNaknadu(500);

            Assert.AreEqual(10.0, naknada, 0.001);
        }

        [TestMethod]
        [DataRow(0.0)]
        [DataRow(-5.0)]
        public void IzracunajNaknadu_IznosNijePozitivan_BacaIzuzetak(double iznos)
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _servis.IzracunajNaknadu(iznos));
        }
    }
}
