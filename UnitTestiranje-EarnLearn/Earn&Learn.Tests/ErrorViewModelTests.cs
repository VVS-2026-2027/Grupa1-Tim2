using Earn_Learn.Models;
using System.Text.Json;

namespace Earn_Learn.Tests
{
    public class ErrorRequestIdZapis
    {
        public string? RequestId { get; set; }
        public bool Ocekivano { get; set; }
    }

    public static class ErrorViewModelTestData
    {
        private static string Putanja(string imeFajla) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", imeFajla);

        // CSV: requestId, ocekivano
        public static IEnumerable<object[]> RequestIdIzCsv =>
            File.ReadLines(Putanja("error_request_id.csv"))
                .Skip(1)
                .Select(linija => linija.Split(','))
                .Select(k => new object[] { k[0], bool.Parse(k[1]) })
                .ToList();

        // JSON: requestId, ocekivano
        public static IEnumerable<object[]> RequestIdIzJsona
        {
            get
            {
                var opcije = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var zapisi = JsonSerializer.Deserialize<List<ErrorRequestIdZapis>>(
                    File.ReadAllText(Putanja("error_request_id.json")), opcije)!;
                return zapisi.Select(z => new object[] { z.RequestId!, z.Ocekivano }).ToList();
            }
        }
    }

    [TestClass]
    public class ErrorViewModelTests
    {
        [TestMethod]
        public void RequestId_NovaInstanca_JeNull()
        {
            var model = new ErrorViewModel();

            Assert.IsNull(model.RequestId);
        }

        [TestMethod]
        public void RequestId_PostavljenaVrijednost_VracaIstuVrijednost()
        {
            var model = new ErrorViewModel { RequestId = "abc-123" };

            Assert.AreEqual("abc-123", model.RequestId);
        }

        [TestMethod]
        public void ShowRequestId_RequestIdNull_VracaFalse()
        {
            var model = new ErrorViewModel { RequestId = null };

            Assert.IsFalse(model.ShowRequestId);
        }

        [TestMethod]
        public void ShowRequestId_RequestIdPrazan_VracaFalse()
        {
            var model = new ErrorViewModel { RequestId = "" };

            Assert.IsFalse(model.ShowRequestId);
        }

        [TestMethod]
        public void ShowRequestId_RequestIdPostavljen_VracaTrue()
        {
            var model = new ErrorViewModel { RequestId = "abc-123" };

            Assert.IsTrue(model.ShowRequestId);
        }

        [TestMethod]
        [DynamicData(nameof(ErrorViewModelTestData.RequestIdIzCsv), typeof(ErrorViewModelTestData))]
        public void ShowRequestId_PodaciIzCsvFajla_VracaOcekivano(string requestId, bool ocekivano)
        {
            var model = new ErrorViewModel { RequestId = requestId };

            Assert.AreEqual(ocekivano, model.ShowRequestId);
        }

        [TestMethod]
        [DynamicData(nameof(ErrorViewModelTestData.RequestIdIzJsona), typeof(ErrorViewModelTestData))]
        public void ShowRequestId_PodaciIzJsonFajla_VracaOcekivano(string? requestId, bool ocekivano)
        {
            var model = new ErrorViewModel { RequestId = requestId };

            Assert.AreEqual(ocekivano, model.ShowRequestId);
        }
    }
}