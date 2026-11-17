
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemInformasiRestoran.Data;
using SistemInformasiRestoran.Models;
using SistemInformasiRestoran.Models.ViewModels;

namespace SistemInformasiRestoran.Controllers
{
    [Authorize(Roles = "Manager,Kasir")]
    public class LaporanController : Controller
    {
        private readonly RestoDbContext _context;

        public LaporanController(RestoDbContext context)
        {
            _context = context;
        }

        // Menampilkan seluruh laporan.
        public async Task<IActionResult> Index(
            DateTime? tanggalMulai,
            DateTime? tanggalAkhir)
        {
            DateTime mulai = tanggalMulai?.Date
                ?? DateTime.Today.AddDays(-6);

            DateTime akhir = tanggalAkhir?.Date
                ?? DateTime.Today;

            if (mulai > akhir)
            {
                TempData["ErrorMessage"] =
                    "Tanggal mulai tidak boleh melebihi tanggal akhir.";

                return RedirectToAction(nameof(Index));
            }

            DateTime batasAkhir = akhir.AddDays(1);

            var viewModel = new LaporanViewModel
            {
                TanggalMulai = mulai,
                TanggalAkhir = akhir
            };

            // Mengambil ID pesanan yang sudah dibayar.
            var pesananDibayar = _context.Pembayaran
                .Select(p => p.IdPesanan);

            // LAPORAN MENU TERLARIS
            viewModel.MenuTerlaris = await _context.DetailPesanan
                .AsNoTracking()
                .Where(d =>
                    pesananDibayar.Contains(d.IdPesanan) &&
                    d.Pesanan != null &&
                    d.Pesanan.StatusPesanan == "Selesai" &&
                    d.Pesanan.TanggalPesanan >= mulai &&
                    d.Pesanan.TanggalPesanan < batasAkhir &&
                    d.Menu != null)
                .GroupBy(d => new
                {
                    d.IdMenu,
                    d.Menu!.NamaMenu
                })
                .Select(g => new MenuTerlarisViewModel
                {
                    IdMenu = g.Key.IdMenu,
                    NamaMenu = g.Key.NamaMenu,
                    TotalTerjual = g.Sum(d => d.Jumlah),
                    TotalPendapatan = g.Sum(d => d.Subtotal)
                })
                .OrderByDescending(m => m.TotalTerjual)
                .ToListAsync();

            // LAPORAN OMZET HARIAN
            var daftarPembayaran = await _context.Pembayaran
                .AsNoTracking()
                .Where(p =>
                    p.TanggalPembayaran >= mulai &&
                    p.TanggalPembayaran < batasAkhir)
                .ToListAsync();

            viewModel.OmzetHarian = daftarPembayaran
                .GroupBy(p => p.TanggalPembayaran.Date)
                .Select(g => new OmzetHarianViewModel
                {
                    Tanggal = g.Key,
                    TotalOmzet = g.Sum(p => p.TotalBayar),
                    JumlahTransaksi = g.Count()
                })
                .OrderByDescending(o => o.Tanggal)
                .ToList();

            viewModel.TotalOmzet = daftarPembayaran
                .Sum(p => p.TotalBayar);

            // RIWAYAT PEMBAYARAN
            viewModel.RiwayatPembayaran = await _context.Pembayaran
                .AsNoTracking()
                .Include(p => p.Pesanan)
                    .ThenInclude(p => p!.Meja)
                .Where(p =>
                    p.TanggalPembayaran >= mulai &&
                    p.TanggalPembayaran < batasAkhir)
                .OrderByDescending(p => p.TanggalPembayaran)
                .ToListAsync();

            return View(viewModel);
        }

        // Menampilkan struk berdasarkan transaksi pembayaran.
        public async Task<IActionResult> Struk(int id)
        {
            var pembayaran = await _context.Pembayaran
                .AsNoTracking()
                .Include(p => p.Pesanan)
                    .ThenInclude(p => p!.Meja)
                .FirstOrDefaultAsync(p => p.IdPembayaran == id);

            if (pembayaran == null)
            {
                TempData["ErrorMessage"] =
                    "Data pembayaran tidak ditemukan.";

                return RedirectToAction(nameof(Index));
            }

            var detailPesanan = await _context.DetailPesanan
                .AsNoTracking()
                .Include(d => d.Menu)
                .Where(d => d.IdPesanan == pembayaran.IdPesanan)
                .ToListAsync();

            ViewBag.DetailPesanan = detailPesanan;

            return View(pembayaran);
        }
    }
}
