
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemInformasiRestoran.Models
{
    [Table("User")]
    public class Pengguna
    {
        [Key]
        public int IdUser { get; set; }

        [Required]
        [StringLength(100)]
        public string NamaUser { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string Role { get; set; } = "Kasir";

        // Informasi pencatatan data
        public int? InputBy { get; set; }

        public DateTime InputDate { get; set; }

        public int? UpdateBy { get; set; }

        public DateTime? UpdateDate { get; set; }

        // Penanda Soft Delete
        public bool IsDeleted { get; set; } = false;
    }
}
