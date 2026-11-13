using Earn_Learn.Services;
using System.Globalization;
using System.Text.Json;

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
    
        // ---------- ProvjeriIsplatu ----------

        // Fiksno "sada" da rezultat ne zavisi od dana kada se test pokrece
        private static readonly DateTime Sada = new DateTime(2026, 11, 13, 15, 0, 0);

        private record ScenarijIsplate(
            string Opis, double Stanje, double Iznos, DateTime? ZadnjaIsplata, bool Dozvoljeno, string Poruka);

        public static IEnumerable<object?[]> ScenarijiIzJsona()
        {
            var putanja = Path.Combine(AppContext.BaseDirectory, "TestData", "isplata_scenariji.json");
            var scenariji = JsonSerializer.Deserialize<List<ScenarijIsplate>>(
                File.ReadAllText(putanja),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            return scenariji.Select(s => new object?[]
                { s.Opis, s.Stanje, s.Iznos, s.ZadnjaIsplata, s.Dozvoljeno, s.Poruka });
        }

        [TestMethod]
        [DynamicData(nameof(ScenarijiIzJsona))]
        public void ProvjeriIsplatu_ScenarijIzJsona_VracaOcekivanuOdlukuIPoruku(
            string opis, double stanje, double iznos, DateTime? zadnjaIsplata, bool dozvoljeno, string poruka)
        {
            var rezultat = _servis.ProvjeriIsplatu(stanje, iznos, zadnjaIsplata, Sada);

            Assert.AreEqual(dozvoljeno, rezultat.Dozvoljeno, opis);
            Assert.AreEqual(poruka, rezultat.Poruka, opis);
        }

        [TestMethod]
        public void ProvjeriIsplatu_DozvoljenaIsplata_RacunaNaknaduIIznosZaBanku()
        {
            var rezultat = _servis.ProvjeriIsplatu(300, 200, null, Sada);

            Assert.IsTrue(rezultat.Dozvoljeno);
            Assert.AreEqual(4.0, rezultat.Naknada, 0.001);
            Assert.AreEqual(196.0, rezultat.IznosZaBanku, 0.001);
        }

        [TestMethod]
        public void ProvjeriIsplatu_MaliIznos_NaBankuIdeIznosUmanjenZaJedanKm()
        {
            var rezultat = _servis.ProvjeriIsplatu(100, 30, null, Sada);

            Assert.AreEqual(1.0, rezultat.Naknada, 0.001);
            Assert.AreEqual(29.0, rezultat.IznosZaBanku, 0.001);
        }

        [TestMethod]
        public void ProvjeriIsplatu_OdbijenaIsplata_NaknadaIIznosZaBankuSuNula()
        {
            var rezultat = _servis.ProvjeriIsplatu(10, 50, null, Sada);

            Assert.IsFalse(rezultat.Dozvoljeno);
            Assert.AreEqual(0.0, rezultat.Naknada);
            Assert.AreEqual(0.0, rezultat.IznosZaBanku);
        }

        [TestMethod]
        public void ProvjeriIsplatu_IznosIspodMinimumaIVeciOdStanja_PrijavljujeMinimum()
        {
            var rezultat = _servis.ProvjeriIsplatu(5, 10, null, Sada);

            Assert.AreEqual("Minimalan iznos isplate je 20 KM.", rezultat.Poruka);
        }
    }
}
