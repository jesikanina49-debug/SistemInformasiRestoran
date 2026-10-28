
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemInformasiRestoran.Models
{
    [Table("Menu")]
    public class Menu
    {
        [Key]
        public int IdMenu { get; set; }

        public int IdKategori { get; set; }

        [Required]
        [StringLength(150)]
        public string NamaMenu { get; set; } = string.Empty;

        [Column(TypeName = "decimal(12,2)")]
        public decimal Harga { get; set; }

        public int Stok { get; set; }

        public bool StatusTersedia { get; set; } = true;

        // Menyimpan nama file gambar menu
        [StringLength(255)]
        public string? Gambar { get; set; }

        [ForeignKey(nameof(IdKategori))]
        public Kategori? Kategori { get; set; }
    }
}
