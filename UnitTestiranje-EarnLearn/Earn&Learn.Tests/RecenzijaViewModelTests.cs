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
