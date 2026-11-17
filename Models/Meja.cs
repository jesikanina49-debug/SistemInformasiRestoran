
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemInformasiRestoran.Models
{
    [Table("Meja")]
    public class Meja
    {
        [Key]
        public int IdMeja { get; set; }

        [Required]
        [StringLength(20)]
        public string NomorMeja { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string StatusMeja { get; set; } = "Tersedia";

        [Range(1, 50)]
        public int JumlahKursi { get; set; } = 4;

        public DateTime? MulaiDigunakan { get; set; }
    }
}
