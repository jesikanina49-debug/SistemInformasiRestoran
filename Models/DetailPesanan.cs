
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemInformasiRestoran.Models
{
    [Table("DetailPesanan")]
    public class DetailPesanan
    {
        [Key]
        public int IdDetailPesanan { get; set; }

        public int IdPesanan { get; set; }

        public int IdMenu { get; set; }

        public int Jumlah { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal HargaSatuan { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal Subtotal { get; set; }

        [ForeignKey(nameof(IdPesanan))]
        public Pesanan? Pesanan { get; set; }

        [ForeignKey(nameof(IdMenu))]
        public Menu? Menu { get; set; }
    }
}
