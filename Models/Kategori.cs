
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemInformasiRestoran.Models
{
    [Table("Kategori")]
    public class Kategori
    {
        [Key]
        public int IdKategori { get; set; }

        [Required]
        [StringLength(100)]
        public string NamaKategori { get; set; } = string.Empty;
    }
}
