using System.ComponentModel.DataAnnotations;

namespace Earn_Learn.Models
{
    public class Predmet
    {
        [Key]
        public int id { get; set; }
        public string naziv { get; set; }
    }
}
