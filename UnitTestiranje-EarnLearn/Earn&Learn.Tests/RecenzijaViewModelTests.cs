using Earn_Learn.Models;

namespace Earn_Learn.Tests
{
    public static class RecenzijaViewModelTestData
    {
        private static string Putanja(string imeFajla) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", imeFajla);

        // CSV: imeStudenta
        public static IEnumerable<object[]> ImenaStudenataIzCsv =>
            File.ReadLines(Putanja("recenzija_imena_studenata.csv"))
                .Skip(1)
                .Select(linija => new object[] { linija })
                .ToList();
    }

    [TestClass]
    public class RecenzijaViewModelTests
    {
        [TestMethod]
        public void Recenzija_PostavljenaVrijednost_VracaIstiObjekat()
        {
            var recenzija = new Recenzija();

            var model = new RecenzijaViewModel { Recenzija = recenzija };

            Assert.AreSame(recenzija, model.Recenzija);
        }

        [TestMethod]
        public void Recenzija_NovaInstanca_PodrazumijevaneVrijednostiSuIspravne()
        {
            var recenzija = new Recenzija();

            Assert.AreEqual(0, recenzija.id);
            Assert.AreEqual(string.Empty, recenzija.idStudenta);
            Assert.AreEqual(string.Empty, recenzija.idTutora);
            Assert.AreEqual(0, recenzija.ocjena);
            Assert.AreEqual(string.Empty, recenzija.komentar);
            Assert.IsTrue(recenzija.datumRecenzije <= DateTime.UtcNow
                          && recenzija.datumRecenzije > DateTime.UtcNow.AddMinutes(-1));
        }

        [TestMethod]
        public void Recenzija_PostavljenaSvaSvojstva_VracaIstuVrijednost()
        {
            var datum = new DateTime(2026, 11, 1, 10, 30, 0);

            var recenzija = new Recenzija
            {
                id = 7,
                idStudenta = "s1",
                idTutora = "t1",
                ocjena = 4,
                komentar = "Odlično objašnjava",
                datumRecenzije = datum
            };

            Assert.AreEqual(7, recenzija.id);
            Assert.AreEqual("s1", recenzija.idStudenta);
            Assert.AreEqual("t1", recenzija.idTutora);
            Assert.AreEqual(4, recenzija.ocjena);
            Assert.AreEqual("Odlično objašnjava", recenzija.komentar);
            Assert.AreEqual(datum, recenzija.datumRecenzije);
        }

        [TestMethod]
        public void Recenzija_PostavljenaDvaPutaRazlicitiObjekti_VracaPosljednji()
        {
            var prva = new Recenzija();
            var druga = new Recenzija();
            var model = new RecenzijaViewModel { Recenzija = prva };

            model.Recenzija = druga;

            Assert.AreSame(druga, model.Recenzija);
            Assert.AreNotSame(prva, model.Recenzija);
        }

        [TestMethod]
        public void ImeStudenta_PostavljenaVrijednost_VracaIstuVrijednost()
        {
            var model = new RecenzijaViewModel { ImeStudenta = "Amar Hadžić" };

            Assert.AreEqual("Amar Hadžić", model.ImeStudenta);
        }

        [TestMethod]
        [DynamicData(nameof(RecenzijaViewModelTestData.ImenaStudenataIzCsv), typeof(RecenzijaViewModelTestData))]
        public void ImeStudenta_PodaciIzCsvFajla_VracaPostavljenuVrijednost(string imeStudenta)
        {
            var model = new RecenzijaViewModel { ImeStudenta = imeStudenta };

            Assert.AreEqual(imeStudenta, model.ImeStudenta);
        }
    }
}
