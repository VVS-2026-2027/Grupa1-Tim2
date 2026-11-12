using Earn_Learn.Models;
using System.Text.Json;

namespace Earn_Learn.Tests
{
    public class TerminDetaljiZapis
    {
        public string? ImePredmeta { get; set; }
        public string? ImeStudenta { get; set; }
    }

    public static class TerminDetaljiViewModelTestData
    {
        private static string Putanja(string imeFajla) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", imeFajla);

        // CSV: imePredmeta, imeStudenta
        public static IEnumerable<object[]> ImenaIzCsv =>
            File.ReadLines(Putanja("termin_detalji.csv"))
                .Skip(1)
                .Select(linija => linija.Split(','))
                .Select(k => new object[] { k[0], k[1] })
                .ToList();

        // JSON: imePredmeta, imeStudenta (ukljucuje null vrijednosti)
        public static IEnumerable<object[]> ImenaIzJsona
        {
            get
            {
                var opcije = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var zapisi = JsonSerializer.Deserialize<List<TerminDetaljiZapis>>(
                    File.ReadAllText(Putanja("termin_detalji.json")), opcije)!;
                return zapisi.Select(z => new object[] { z.ImePredmeta!, z.ImeStudenta! }).ToList();
            }
        }
    }

    [TestClass]
    public class TerminDetaljiViewModelTests
    {
        [TestMethod]
        public void Konstruktor_NovaInstanca_ImePredmetaIImeStudentaSuNull()
        {
            var model = new TerminDetaljiViewModel();

            Assert.IsNull(model.ImePredmeta);
            Assert.IsNull(model.ImeStudenta);
        }

        [TestMethod]
        public void Termin_PostavljenaVrijednost_VracaIstiObjekat()
        {
            var termin = new Termin();

            var model = new TerminDetaljiViewModel { Termin = termin };

            Assert.AreSame(termin, model.Termin);
        }

        [TestMethod]
        public void ImePredmeta_PostavljenaVrijednost_VracaIstuVrijednost()
        {
            var model = new TerminDetaljiViewModel { ImePredmeta = "Matematika" };

            Assert.AreEqual("Matematika", model.ImePredmeta);
        }

        [TestMethod]
        public void ImeStudenta_PostavljenaVrijednost_VracaIstuVrijednost()
        {
            var model = new TerminDetaljiViewModel { ImeStudenta = "Amar Hadžić" };

            Assert.AreEqual("Amar Hadžić", model.ImeStudenta);
        }

        [TestMethod]
        [DynamicData(nameof(TerminDetaljiViewModelTestData.ImenaIzCsv), typeof(TerminDetaljiViewModelTestData))]
        public void Imena_PodaciIzCsvFajla_VracajuPostavljeneVrijednosti(string imePredmeta, string imeStudenta)
        {
            var model = new TerminDetaljiViewModel { ImePredmeta = imePredmeta, ImeStudenta = imeStudenta };

            Assert.AreEqual(imePredmeta, model.ImePredmeta);
            Assert.AreEqual(imeStudenta, model.ImeStudenta);
        }

        [TestMethod]
        [DynamicData(nameof(TerminDetaljiViewModelTestData.ImenaIzJsona), typeof(TerminDetaljiViewModelTestData))]
        public void Imena_PodaciIzJsonFajla_VracajuPostavljeneVrijednosti(string? imePredmeta, string? imeStudenta)
        {
            var model = new TerminDetaljiViewModel { ImePredmeta = imePredmeta, ImeStudenta = imeStudenta };

            Assert.AreEqual(imePredmeta, model.ImePredmeta);
            Assert.AreEqual(imeStudenta, model.ImeStudenta);
        }
    }
}
