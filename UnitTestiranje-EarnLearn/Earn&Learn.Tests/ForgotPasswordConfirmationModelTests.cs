using Earn_Learn.Areas.Identity.Pages.Account;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace Earn_Learn.Tests
{
    public class PotvrdaPozivaZapis
    {
        public string Naziv { get; set; } = "";
        public int BrojPoziva { get; set; }
    }

    public static class ForgotPasswordConfirmationTestData
    {
        private static string Putanja(string imeFajla) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", imeFajla);

        // CSV: naziv, brojPoziva
        public static IEnumerable<object[]> PozeviIzCsv =>
            File.ReadLines(Putanja("forgot_password_potvrda.csv"))
                .Skip(1)
                .Where(linija => !string.IsNullOrWhiteSpace(linija))
                .Select(linija => linija.Split(','))
                .Select(k => new object[] { k[0].Trim(), int.Parse(k[1].Trim()) })
                .ToList();

        // JSON: naziv, brojPoziva
        public static IEnumerable<object[]> PozeviIzJsona
        {
            get
            {
                var opcije = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var zapisi = JsonSerializer.Deserialize<List<PotvrdaPozivaZapis>>(
                    File.ReadAllText(Putanja("forgot_password_potvrda.json")), opcije)!;
                return zapisi.Select(z => new object[] { z.Naziv, z.BrojPoziva }).ToList();
            }
        }
    }

    [TestClass]
    public class ForgotPasswordConfirmationModelTests
    {
        // ---------- Konstruktor ----------

        [TestMethod]
        public void Konstruktor_NovaInstanca_NijeNull()
        {
            var model = new ForgotPasswordConfirmationModel();

            Assert.IsNotNull(model);
        }

        [TestMethod]
        public void Konstruktor_NovaInstanca_JePageModel()
        {
            var model = new ForgotPasswordConfirmationModel();

            Assert.IsInstanceOfType(model, typeof(PageModel));
        }

        // ---------- OnGet ----------

        [TestMethod]
        public void OnGet_PozivBezPodataka_NeBacaIzuzetak()
        {
            var model = new ForgotPasswordConfirmationModel();

            model.OnGet();

            Assert.IsNotNull(model);
        }

        // ---------- Data-driven: CSV ----------

        [TestMethod]
        [DynamicData(nameof(ForgotPasswordConfirmationTestData.PozeviIzCsv), typeof(ForgotPasswordConfirmationTestData))]
        public void OnGet_ViseSukcesivnihPozivaIzCsvFajla_NeBacaIzuzetak(string naziv, int brojPoziva)
        {
            var model = new ForgotPasswordConfirmationModel();

            for (int i = 0; i < brojPoziva; i++)
                model.OnGet();

            Assert.IsNotNull(model, naziv);
        }

        // ---------- Data-driven: JSON ----------

        [TestMethod]
        [DynamicData(nameof(ForgotPasswordConfirmationTestData.PozeviIzJsona), typeof(ForgotPasswordConfirmationTestData))]
        public void OnGet_ViseSukcesivnihPozivaIzJsonFajla_NeBacaIzuzetak(string naziv, int brojPoziva)
        {
            var model = new ForgotPasswordConfirmationModel();

            for (int i = 0; i < brojPoziva; i++)
                model.OnGet();

            Assert.IsNotNull(model, naziv);
        }
    }
}
