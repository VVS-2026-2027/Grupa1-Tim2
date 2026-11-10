using System.ComponentModel.DataAnnotations;

namespace Earn_Learn.Models
{
    public class Obavjestenje
    {
        [Key]
        public int id { get; set; }
        public string idKorisnika { get; set; } = string.Empty;
        public string naslov { get; set; } = string.Empty;
        public string sadrzaj { get; set; } = string.Empty;
        public DateTime datumSlanja { get; set; } = DateTime.UtcNow;
        public bool procitano { get; set; } = false;

        // Ako je notifikacija vezana za termin — tutor može kliknuti i unijeti mjesto časa
        public int? idTermina { get; set; }
    }
}