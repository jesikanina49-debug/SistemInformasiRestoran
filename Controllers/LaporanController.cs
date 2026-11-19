
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemInformasiRestoran.Data;
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

            // LAPORAN MENU TERLARIS
            // Periode menggunakan tanggal pembayaran.
            // Menu yang sudah Soft Delete tetap masuk laporan historis.
            viewModel.MenuTerlaris =
                await (
                    from detail in _context.DetailPesanan.AsNoTracking()
                    join pembayaran in _context.Pembayaran.AsNoTracking()
                        on detail.IdPesanan equals pembayaran.IdPesanan
                    join pesanan in _context.Pesanan.AsNoTracking()
                        on detail.IdPesanan equals pesanan.IdPesanan
                    join menu in _context.Menu.AsNoTracking()
                        on detail.IdMenu equals menu.IdMenu
                    where pembayaran.TanggalPembayaran >= mulai
                        && pembayaran.TanggalPembayaran < batasAkhir
                        && pesanan.StatusPesanan == "Selesai"
                    group detail by new
                    {
                        detail.IdMenu,
                        menu.NamaMenu
                    }
                    into kelompok
                    select new MenuTerlarisViewModel
                    {
                        IdMenu = kelompok.Key.IdMenu,
                        NamaMenu = kelompok.Key.NamaMenu,
                        TotalTerjual = kelompok.Sum(d => d.Jumlah),
                        TotalPendapatan = kelompok.Sum(d => d.Subtotal)
                    }
                )
                .OrderByDescending(m => m.TotalTerjual)
                .ToListAsync();

            // LAPORAN OMZET HARIAN
            // Sumber omzet adalah data pembayaran.
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
            // Tetap menampilkan transaksi historis.
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
                .FirstOrDefaultAsync(p =>
                    p.IdPembayaran == id);

            if (pembayaran == null)
            {
                TempData["ErrorMessage"] =
                    "Data pembayaran tidak ditemukan.";

                return RedirectToAction(nameof(Index));
            }

            // Detail tetap menampilkan menu historis,
            // termasuk menu yang sudah Soft Delete.
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
