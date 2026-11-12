using Earn_Learn.Models;
using System.Text.Json;

namespace Earn_Learn.Tests
{
    public class KorisnikPredmetZapis
    {
        public string IdKorisnika { get; set; } = string.Empty;
        public int IdPredmeta { get; set; }
    }

    public static class KorisnikPredmetTestData
    {
        private static string Putanja(string imeFajla) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", imeFajla);

        // CSV: idKorisnika, idPredmeta
        public static IEnumerable<object[]> ParoviIzCsv =>
            File.ReadLines(Putanja("korisnik_predmet.csv"))
                .Skip(1)
                .Select(linija => linija.Split(','))
                .Select(k => new object[] { k[0], int.Parse(k[1]) })
                .ToList();

        // JSON: idKorisnika, idPredmeta
        public static IEnumerable<object[]> ParoviIzJsona
        {
            get
            {
                var opcije = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var zapisi = JsonSerializer.Deserialize<List<KorisnikPredmetZapis>>(
                    File.ReadAllText(Putanja("korisnik_predmet.json")), opcije)!;
                return zapisi.Select(z => new object[] { z.IdKorisnika, z.IdPredmeta }).ToList();
            }
        }
    }

    [TestClass]
    public class KorisnikPredmetTests
    {
        [TestMethod]
        public void Konstruktor_NovaInstanca_IdKorisnikaJePrazanString()
        {
            var model = new KorisnikPredmet();

            Assert.AreEqual(string.Empty, model.idKorisnika);
        }

        [TestMethod]
        public void Konstruktor_NovaInstanca_IdPredmetaJeNula()
        {
            var model = new KorisnikPredmet();

            Assert.AreEqual(0, model.idPredmeta);
        }

        [TestMethod]
        public void IdKorisnika_PostavljenaVrijednost_VracaIstuVrijednost()
        {
            var model = new KorisnikPredmet { idKorisnika = "korisnik-1" };

            Assert.AreEqual("korisnik-1", model.idKorisnika);
        }

        [TestMethod]
        public void IdPredmeta_PostavljenaVrijednost_VracaIstuVrijednost()
        {
            var model = new KorisnikPredmet { idPredmeta = 5 };

            Assert.AreEqual(5, model.idPredmeta);
        }

        [TestMethod]
        [DynamicData(nameof(KorisnikPredmetTestData.ParoviIzCsv), typeof(KorisnikPredmetTestData))]
        public void Svojstva_PodaciIzCsvFajla_VracajuPostavljeneVrijednosti(string idKorisnika, int idPredmeta)
        {
            var model = new KorisnikPredmet { idKorisnika = idKorisnika, idPredmeta = idPredmeta };

            Assert.AreEqual(idKorisnika, model.idKorisnika);
            Assert.AreEqual(idPredmeta, model.idPredmeta);
        }

        [TestMethod]
        [DynamicData(nameof(KorisnikPredmetTestData.ParoviIzJsona), typeof(KorisnikPredmetTestData))]
        public void Svojstva_PodaciIzJsonFajla_VracajuPostavljeneVrijednosti(string idKorisnika, int idPredmeta)
        {
            var model = new KorisnikPredmet { idKorisnika = idKorisnika, idPredmeta = idPredmeta };

            Assert.AreEqual(idKorisnika, model.idKorisnika);
            Assert.AreEqual(idPredmeta, model.idPredmeta);
        }
    }
}
