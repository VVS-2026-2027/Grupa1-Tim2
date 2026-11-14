namespace Earn_Learn.Services
{
    // Pravila popusta za lojalne studente (F2 - Popust za lojalne studente)
    public class PopustServis
    {
        public int IzracunajProcenatPopusta(int brojOdrzanihCasova)
        {
            if (brojOdrzanihCasova < 0)
                throw new
ArgumentOutOfRangeException(nameof(brojOdrzanihCasova));
            if (brojOdrzanihCasova >= 10)
                return 10;
            if (brojOdrzanihCasova >= 5)
                return 5;
            return 0;
        }
    }
}