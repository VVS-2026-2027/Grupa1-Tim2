using Earn_Learn.Enums;
using Earn_Learn.Models;
using System.Text.Json;

namespace Earn_Learn.Tests
{
    public class PrijavaZapis
    {
        public string Naslov { get; set; } = string.Empty;
        public string Opis { get; set; } = string.Empty;
        public string? IdPrijavitelja { get; set; }
        public string? PrijavljeniTutorId { get; set; }
        public int? RecenzijaId { get; set; }
    }

    public static class PrijavaTestData
    {
        private static string Putanja(string imeFajla) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", imeFajla);

        // CSV: naslov, opis, idPrijavitelja
        public static IEnumerable<object[]> TekstualnaPoljaIzCsv =>
            File.ReadLines(Putanja("prijava_podaci.csv"))
                .Skip(1)
                .Select(linija => linija.Split(','))
                .Select(k => new object[] { k[0], k[1], k[2] })
                .ToList();

        // JSON: kompletne prijave (ukljucujuci null vrijednosti)
        public static IEnumerable<object[]> PrijaveIzJsona
        {
            get
            {
                var opcije = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var zapisi = JsonSerializer.Deserialize<List<PrijavaZapis>>(
                    File.ReadAllText(Putanja("prijava_podaci.json")), opcije)!;
                return zapisi.Select(z => new object[]
                {
                    z.Naslov, z.Opis, z.IdPrijavitelja!, z.PrijavljeniTutorId!, z.RecenzijaId!
                }).ToList();
            }
        }

        // Sve vrijednosti enuma StatusPrijave
        public static IEnumerable<object[]> SviStatusi =>
            Enum.GetValues<StatusPrijave>().Select(s => new object[] { s }).ToList();
    }

    [TestClass]
    public class PrijavaTests
    {
        // ---------- Zadane vrijednosti ----------

        [TestMethod]
        public void Konstruktor_NovaInstanca_NaslovIOpisSuPrazniStringovi()
        {
            var prijava = new Prijava();

            Assert.AreEqual(string.Empty, prijava.Naslov);
            Assert.AreEqual(string.Empty, prijava.Opis);
        }

        [TestMethod]
        public void Konstruktor_NovaInstanca_StatusJePrijavljeno()
        {
            var prijava = new Prijava();

            Assert.AreEqual(StatusPrijave.Prijavljeno, prijava.Status);
        }

        [TestMethod]
        public void Konstruktor_NovaInstanca_OpcionalnaPoljaSuNull()
        {
            var prijava = new Prijava();

            Assert.IsNull(prijava.IdPrijavitelja);
            Assert.IsNull(prijava.PrijavljeniTutorId);
            Assert.IsNull(prijava.RecenzijaId);
        }

        [TestMethod]
        public void Konstruktor_NovaInstanca_IdJeNula()
        {
            var prijava = new Prijava();

            Assert.AreEqual(0, prijava.Id);
        }

        [TestMethod]
        public void Konstruktor_NovaInstanca_DatumPrijaveJeTrenutniUtcDatum()
        {
            var prije = DateTime.UtcNow;

            var prijava = new Prijava();

            var poslije = DateTime.UtcNow;
            Assert.IsTrue(prijava.DatumPrijave >= prije);
            Assert.IsTrue(prijava.DatumPrijave <= poslije);
        }

        // ---------- Svojstva ----------

        [TestMethod]
        public void Id_PostavljenaVrijednost_VracaIstuVrijednost()
        {
            var prijava = new Prijava { Id = 42 };

            Assert.AreEqual(42, prijava.Id);
        }

        [TestMethod]
        public void DatumPrijave_PostavljenaVrijednost_VracaIstuVrijednost()
        {
            var datum = new DateTime(2026, 11, 12, 10, 30, 0, DateTimeKind.Utc);

            var prijava = new Prijava { DatumPrijave = datum };

            Assert.AreEqual(datum, prijava.DatumPrijave);
        }

        [TestMethod]
        public void RecenzijaId_PostavljenaVrijednost_VracaIstuVrijednost()
        {
            var prijava = new Prijava { RecenzijaId = 7 };

            Assert.AreEqual((int?)7, prijava.RecenzijaId);
        }

        [TestMethod]
        public void PrijavljeniTutorId_PostavljenaVrijednost_VracaIstuVrijednost()
        {
            var prijava = new Prijava { PrijavljeniTutorId = "tutor-1" };

            Assert.AreEqual("tutor-1", prijava.PrijavljeniTutorId);
        }

        [TestMethod]
        public void PrijavljeniTutorId_PostavljenNaNull_VracaNull()
        {
            var prijava = new Prijava { PrijavljeniTutorId = "tutor-1" };

            prijava.PrijavljeniTutorId = null;

            Assert.IsNull(prijava.PrijavljeniTutorId);
        }

        // ---------- Data-driven: CSV ----------

        [TestMethod]
        [DynamicData(nameof(PrijavaTestData.TekstualnaPoljaIzCsv), typeof(PrijavaTestData))]
        public void TekstualnaPolja_PodaciIzCsvFajla_VracajuPostavljeneVrijednosti(
            string naslov, string opis, string idPrijavitelja)
        {
            var prijava = new Prijava { Naslov = naslov, Opis = opis, IdPrijavitelja = idPrijavitelja };

            Assert.AreEqual(naslov, prijava.Naslov);
            Assert.AreEqual(opis, prijava.Opis);
            Assert.AreEqual(idPrijavitelja, prijava.IdPrijavitelja);
        }

        // ---------- Data-driven: JSON ----------

        [TestMethod]
        [DynamicData(nameof(PrijavaTestData.PrijaveIzJsona), typeof(PrijavaTestData))]
        public void SvaPolja_PodaciIzJsonFajla_VracajuPostavljeneVrijednosti(
            string naslov, string opis, string? idPrijavitelja, string? tutorId, int? recenzijaId)
        {
            var prijava = new Prijava
            {
                Naslov = naslov,
                Opis = opis,
                IdPrijavitelja = idPrijavitelja,
                PrijavljeniTutorId = tutorId,
                RecenzijaId = recenzijaId
            };

            Assert.AreEqual(naslov, prijava.Naslov);
            Assert.AreEqual(opis, prijava.Opis);
            Assert.AreEqual(idPrijavitelja, prijava.IdPrijavitelja);
            Assert.AreEqual(tutorId, prijava.PrijavljeniTutorId);
            Assert.AreEqual(recenzijaId, prijava.RecenzijaId);
        }

        // ---------- Status ----------

        [TestMethod]
        [DynamicData(nameof(PrijavaTestData.SviStatusi), typeof(PrijavaTestData))]
        public void Status_SvakaVrijednostEnuma_VracaPostavljeniStatus(StatusPrijave status)
        {
            var prijava = new Prijava { Status = status };

            Assert.AreEqual(status, prijava.Status);
        }
    }
}
