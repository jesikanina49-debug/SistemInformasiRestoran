
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemInformasiRestoran.Models
{
    [Table("Pesanan")]
    public class Pesanan
    {
        [Key]
        public int IdPesanan { get; set; }

        public int IdMeja { get; set; }

        public int IdUser { get; set; }

        public DateTime TanggalPesanan { get; set; } = DateTime.Now;

        [Required]
        [StringLength(30)]
        public string StatusPesanan { get; set; } = "Diproses";

        [Column(TypeName = "decimal(12,2)")]
        public decimal Total { get; set; }

        [ForeignKey(nameof(IdMeja))]
        public Meja? Meja { get; set; }

        [ForeignKey(nameof(IdUser))]
        public Pengguna? Pengguna { get; set; }
    }
}
