namespace Earn_Learn.Services
{
    // Pravila popusta za lojalne studente (F2 - Popust za lojalne studente)
    public class PopustServis
    {
        public const int PragSrednjegPopusta = 5;
        public const int SrednjiPopust = 5;
        public const int PragVelikogPopusta = 10;
        public const int VelikiPopust = 10;
        public const int BezPopusta = 0;
        public int IzracunajProcenatPopusta(int brojOdrzanihCasova)
        {
            if (brojOdrzanihCasova < 0)
                throw new
                ArgumentOutOfRangeException(nameof(brojOdrzanihCasova), "Broj casova ne moze 
                biti negativan.");
}
    }
}
return brojOdrzanihCasova switch
{
    >= PragVelikogPopusta => VelikiPopust,
    >= PragSrednjegPopusta => SrednjiPopust,
    _ => BezPopusta
};