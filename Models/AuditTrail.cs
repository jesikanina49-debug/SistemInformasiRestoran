
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemInformasiRestoran.Models
{
    [Table("AuditTrail")]
    public class AuditTrail
    {
        [Key]
        public int IdAudit { get; set; }

        public int? IdUser { get; set; }

        [Required]
        [StringLength(100)]
        public string NamaUser { get; set; } = "SYSTEM";

        [Required]
        [StringLength(100)]
        public string Aktivitas { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string NamaTabel { get; set; } = string.Empty;

        public int? IdData { get; set; }

        [StringLength(500)]
        public string? Keterangan { get; set; }

        public DateTime TanggalAktivitas { get; set; } = DateTime.Now;
    }
}
