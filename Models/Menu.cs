
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

        [StringLength(255)]
        public string? Gambar { get; set; }

        [ForeignKey(nameof(IdKategori))]
        public Kategori? Kategori { get; set; }

        // Informasi pencatatan data
        public int? InputBy { get; set; }

        public DateTime InputDate { get; set; }

        public int? UpdateBy { get; set; }

        public DateTime? UpdateDate { get; set; }

        // Penanda Soft Delete
        public bool IsDeleted { get; set; } = false;
    }
}
