
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

        // Informasi pencatatan data
        public int? InputBy { get; set; }

        public DateTime InputDate { get; set; }

        public int? UpdateBy { get; set; }

        public DateTime? UpdateDate { get; set; }

        // Penanda Soft Delete
        public bool IsDeleted { get; set; } = false;
    }
}
