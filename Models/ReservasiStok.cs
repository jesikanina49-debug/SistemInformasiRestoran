
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemInformasiRestoran.Models
{
    [Table("ReservasiStok")]
    public class ReservasiStok
    {
        [Key]
        public int IdReservasi { get; set; }

        public int IdMeja { get; set; }

        public int IdMenu { get; set; }

        [Required]
        [StringLength(200)]
        public string SessionId { get; set; } = string.Empty;

        public int Jumlah { get; set; }

        public DateTime WaktuReservasi { get; set; } = DateTime.Now;

        public DateTime WaktuKedaluwarsa { get; set; }

        [ForeignKey(nameof(IdMeja))]
        public Meja? Meja { get; set; }

        [ForeignKey(nameof(IdMenu))]
        public Menu? Menu { get; set; }
    }
}
