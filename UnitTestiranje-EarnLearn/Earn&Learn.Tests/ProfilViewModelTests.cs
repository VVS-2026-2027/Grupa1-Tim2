using Earn_Learn.Enums;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Earn_Learn.Tests
{
    public class ProfilBrojeviZapis
    {
        public Uloga Uloga { get; set; }
        public int? BrojIndeksa { get; set; }
        public int? GodinaStudija { get; set; }
        public int? BrojRecenzija { get; set; }
        public int? BrojCasova { get; set; }
    }

    public static class ProfilViewModelTestData
    {
        private static string Putanja(string imeFajla) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", imeFajla);

        // CSV: ime, prezime, email
        public static IEnumerable<object[]> OsnovniPodaciIzCsv =>
            File.ReadLines(Putanja("profil_osnovni_podaci.csv"))
                .Skip(1)
                .Select(linija => linija.Split(','))
                .Select(k => new object[] { k[0], k[1], k[2] })
                .ToList();

        // JSON: uloga, brojIndeksa, godinaStudija, brojRecenzija, brojCasova
        public static IEnumerable<object[]> BrojcaniPodaciIzJsona
        {
            get
            {
                var opcije = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                };
                var zapisi = JsonSerializer.Deserialize<List<ProfilBrojeviZapis>>(
                    File.ReadAllText(Putanja("profil_brojcani_podaci.json")), opcije)!;
                return zapisi.Select(z => new object[]
                {
                    z.Uloga, z.BrojIndeksa!, z.GodinaStudija!, z.BrojRecenzija!, z.BrojCasova!
                }).ToList();
            }
        }
    }

    [TestClass]
    public class ProfilViewModelTests
    {
        // ---------- Zadane vrijednosti ----------

        [TestMethod]
        public void Konstruktor_NovaInstanca_TekstualnaPoljaSuPrazniStringovi()
        {
            var model = new ProfilViewModel();

            Assert.AreEqual("", model.Ime);
            Assert.AreEqual("", model.Prezime);
            Assert.AreEqual("", model.Email);
        }

        [TestMethod]
        public void Konstruktor_NovaInstanca_BrojcanaOpcionalnaPoljaSuNull()
        {
            var model = new ProfilViewModel();

            Assert.IsNull(model.BrojIndeksa);
            Assert.IsNull(model.GodinaStudija);
            Assert.IsNull(model.BrojRecenzija);
            Assert.IsNull(model.BrojCasova);
        }

        [TestMethod]
        public void Konstruktor_NovaInstanca_ListeSuPrazneINisuNull()
        {
            var model = new ProfilViewModel();

            Assert.AreEqual(0, model.Predmeti.Count);
            Assert.AreEqual(0, model.PredmetiObjekti.Count);
            Assert.AreEqual(0, model.Recenzije.Count);
            Assert.AreEqual(0, model.SviPredmeti.Count);
            Assert.AreEqual(0, model.OdabraniPredmeti.Count);
        }

        [TestMethod]
        public void Konstruktor_NovaInstanca_PrilogOcjeneJeNull()
        {
            var model = new ProfilViewModel();

            Assert.IsNull(model.PrilogOcjene);
        }

        // ---------- Liste ----------

        [TestMethod]
        public void Predmeti_PostavljenaLista_VracaIstuListu()
        {
            var predmeti = new List<string> { "Matematika", "Fizika" };

            var model = new ProfilViewModel { Predmeti = predmeti };

            CollectionAssert.AreEqual(predmeti, model.Predmeti);
        }

        [TestMethod]
        public void OdabraniPredmeti_PostavljenaLista_VracaIstuListu()
        {
            var odabrani = new List<int> { 1, 2, 3 };

            var model = new ProfilViewModel { OdabraniPredmeti = odabrani };

            CollectionAssert.AreEqual(odabrani, model.OdabraniPredmeti);
        }

        [TestMethod]
        public void PredmetiObjekti_PostavljenaLista_VracaIstuListu()
        {
            var predmeti = new List<Predmet> { new Predmet(), new Predmet() };

            var model = new ProfilViewModel { PredmetiObjekti = predmeti };

            Assert.AreSame(predmeti, model.PredmetiObjekti);
        }

        [TestMethod]
        public void SviPredmeti_PostavljenaLista_VracaIstuListu()
        {
            var predmeti = new List<Predmet> { new Predmet() };

            var model = new ProfilViewModel { SviPredmeti = predmeti };

            Assert.AreSame(predmeti, model.SviPredmeti);
        }

        [TestMethod]
        public void Recenzije_PostavljenaLista_VracaIstuListu()
        {
            var recenzije = new List<RecenzijaViewModel> { new RecenzijaViewModel() };

            var model = new ProfilViewModel { Recenzije = recenzije };

            Assert.AreSame(recenzije, model.Recenzije);
        }

        // ---------- Zamjenski objekat (Moq) ----------

        [TestMethod]
        public void PrilogOcjene_ZamjenskiFormFile_VracaIstiObjekatISvojstva()
        {
            var prilog = new Mock<IFormFile>();
            prilog.Setup(f => f.FileName).Returns("ocjene.pdf");
            prilog.Setup(f => f.Length).Returns(2048);

            var model = new ProfilViewModel { PrilogOcjene = prilog.Object };

            Assert.AreSame(prilog.Object, model.PrilogOcjene);
            Assert.AreEqual("ocjene.pdf", model.PrilogOcjene.FileName);
            Assert.AreEqual(2048, model.PrilogOcjene.Length);
        }

        // ---------- Data-driven: CSV ----------

        [TestMethod]
        [DynamicData(nameof(ProfilViewModelTestData.OsnovniPodaciIzCsv), typeof(ProfilViewModelTestData))]
        public void OsnovniPodaci_PodaciIzCsvFajla_VracajuPostavljeneVrijednosti(
            string ime, string prezime, string email)
        {
            var model = new ProfilViewModel { Ime = ime, Prezime = prezime, Email = email };

            Assert.AreEqual(ime, model.Ime);
            Assert.AreEqual(prezime, model.Prezime);
            Assert.AreEqual(email, model.Email);
        }

        // ---------- Data-driven: JSON ----------

        [TestMethod]
        [DynamicData(nameof(ProfilViewModelTestData.BrojcaniPodaciIzJsona), typeof(ProfilViewModelTestData))]
        public void BrojcaniPodaci_PodaciIzJsonFajla_VracajuPostavljeneVrijednosti(
            Uloga uloga, int? brojIndeksa, int? godinaStudija, int? brojRecenzija, int? brojCasova)
        {
            var model = new ProfilViewModel
            {
                Uloga = uloga,
                BrojIndeksa = brojIndeksa,
                GodinaStudija = godinaStudija,
                BrojRecenzija = brojRecenzija,
                BrojCasova = brojCasova
            };

            Assert.AreEqual(uloga, model.Uloga);
            Assert.AreEqual(brojIndeksa, model.BrojIndeksa);
            Assert.AreEqual(godinaStudija, model.GodinaStudija);
            Assert.AreEqual(brojRecenzija, model.BrojRecenzija);
            Assert.AreEqual(brojCasova, model.BrojCasova);
        }
    }
}
