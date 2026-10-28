
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemInformasiRestoran.Models
{
    [Table("Pembayaran")]
    public class Pembayaran
    {
        [Key]
        public int IdPembayaran { get; set; }

        public int IdPesanan { get; set; }

        public DateTime TanggalPembayaran { get; set; } = DateTime.Now;

        [Required]
        [StringLength(30)]
        public string MetodePembayaran { get; set; } = string.Empty;

        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalBayar { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal UangDiterima { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal Kembalian { get; set; }

        [ForeignKey(nameof(IdPesanan))]
        public Pesanan? Pesanan { get; set; }
    }
}
