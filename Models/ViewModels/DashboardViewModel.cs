
using SistemInformasiRestoran.Models;

namespace SistemInformasiRestoran.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalMenu { get; set; }

        public int TotalMeja { get; set; }

        public int PesananHariIni { get; set; }

        public decimal PendapatanHariIni { get; set; }

        public decimal TotalPendapatan { get; set; }

        public int TotalTransaksi { get; set; }

        public int MenuTerjual { get; set; }

        public List<OmzetDashboardViewModel> GrafikOmzet { get; set; } = new();

        public List<Pembayaran> AktivitasTerbaru { get; set; } = new();
    }

    public class OmzetDashboardViewModel
    {
        public DateTime Tanggal { get; set; }

        public decimal TotalOmzet { get; set; }
    }
}
