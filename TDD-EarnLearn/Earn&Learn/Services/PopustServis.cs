
namespace Earn_Learn.Services
{
    // Pravila popusta za lojalne studente (F2 - Popust za lojalne studente)
    public class PopustServis : IPopustServis
    {
        public const int PragSrednjegPopusta = 5;
        public const int SrednjiPopust = 5;
        public const int PragVelikogPopusta = 10;
        public const int VelikiPopust = 10;
        public const int BezPopusta = 0;

        public int IzracunajProcenatPopusta(int brojOdrzanihCasova)
        {
            if (brojOdrzanihCasova < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(brojOdrzanihCasova),
                    "Broj casova ne moze biti negativan.");

            return brojOdrzanihCasova switch
            {
                >= PragVelikogPopusta => VelikiPopust,
                >= PragSrednjegPopusta => SrednjiPopust,
                _ => BezPopusta
            };
        }

        public double IzracunajCijenuSaPopustom(
            double cijena,
            int brojOdrzanihCasova)
        {
            if (cijena < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(cijena),
                    "Cijena ne moze biti negativna.");

            double faktor =
                (100 - IzracunajProcenatPopusta(brojOdrzanihCasova)) / 100.0;

            return ZaokruziNaDvijeDecimale(cijena * faktor);
        }

        private static double ZaokruziNaDvijeDecimale(double vrijednost)
        {
            return Math.Round(
                vrijednost,
                2,
                MidpointRounding.AwayFromZero);
        }
    }
}
