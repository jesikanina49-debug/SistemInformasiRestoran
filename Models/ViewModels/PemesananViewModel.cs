
using SistemInformasiRestoran.Models;

namespace SistemInformasiRestoran.Models.ViewModels
{
    public class PemesananViewModel
    {
        public List<Meja> DaftarMeja { get; set; } = new();

        public List<Menu> DaftarMenu { get; set; } = new();

        public Meja? MejaDipilih { get; set; }

        public List<ItemKeranjangViewModel> Keranjang { get; set; } = new();

        public decimal Total
        {
            get { return Keranjang.Sum(item => item.Subtotal); }
        }
    }

    public class ItemKeranjangViewModel
    {
        public int IdMenu { get; set; }

        public string NamaMenu { get; set; } = string.Empty;

        public string? Gambar { get; set; }

        public decimal Harga { get; set; }

        public int Jumlah { get; set; }

        public decimal Subtotal
        {
            get { return Harga * Jumlah; }
        }
    }
}
