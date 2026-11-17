
using SistemInformasiRestoran.Models;

namespace SistemInformasiRestoran.Models.ViewModels
{
    public class LaporanViewModel
    {
        public List<MenuTerlarisViewModel> MenuTerlaris { get; set; }
            = new();

        public List<OmzetHarianViewModel> OmzetHarian { get; set; }
            = new();

        public List<Pembayaran> RiwayatPembayaran { get; set; }
            = new();

        public DateTime TanggalMulai { get; set; }
            = DateTime.Today.AddDays(-6);

        public DateTime TanggalAkhir { get; set; }
            = DateTime.Today;

        public decimal TotalOmzet { get; set; }
    }

    public class MenuTerlarisViewModel
    {
        public int IdMenu { get; set; }

        public string NamaMenu { get; set; } = string.Empty;

        public int TotalTerjual { get; set; }

        public decimal TotalPendapatan { get; set; }
    }

    public class OmzetHarianViewModel
    {
        public DateTime Tanggal { get; set; }

        public decimal TotalOmzet { get; set; }

        public int JumlahTransaksi { get; set; }
    }
}
