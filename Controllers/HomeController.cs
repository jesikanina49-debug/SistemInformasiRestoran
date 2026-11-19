
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SistemInformasiRestoran.Models;
using SistemInformasiRestoran.Models.ViewModels;
using SistemInformasiRestoran.Data;

namespace SistemInformasiRestoran.Controllers
{
    public class HomeController : Controller
    {
        private readonly RestoDbContext _context;

        public HomeController(RestoDbContext context)
        {
            _context = context;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            DateTime hariIni = DateTime.Today;
            DateTime besok = hariIni.AddDays(1);
            DateTime tujuhHariLalu = hariIni.AddDays(-6);

            var viewModel = new DashboardViewModel
            {
                // Total menu aktif (tidak termasuk Soft Delete).
                TotalMenu = await _context.Menu
                    .CountAsync(m => !m.IsDeleted),

                // Total meja aktif (tidak termasuk Soft Delete).
                TotalMeja = await _context.Meja
                    .CountAsync(m => !m.IsDeleted),

                // Pesanan hari ini.
                PesananHariIni = await _context.Pesanan
                    .CountAsync(p =>
                        p.TanggalPesanan >= hariIni &&
                        p.TanggalPesanan < besok),

                // Pendapatan hari ini.
                PendapatanHariIni = await _context.Pembayaran
                    .Where(p =>
                        p.TanggalPembayaran >= hariIni &&
                        p.TanggalPembayaran < besok)
                    .SumAsync(p => (decimal?)p.TotalBayar) ?? 0,

                // Total seluruh pendapatan.
                TotalPendapatan = await _context.Pembayaran
                    .SumAsync(p => (decimal?)p.TotalBayar) ?? 0,

                // Jumlah transaksi pembayaran.
                TotalTransaksi = await _context.Pembayaran
                    .CountAsync(),

                // Jumlah menu yang sudah terjual dan dibayar.
                MenuTerjual = await _context.DetailPesanan
                    .Where(d => _context.Pembayaran
                        .Any(p => p.IdPesanan == d.IdPesanan))
                    .SumAsync(d => (int?)d.Jumlah) ?? 0
            };

            // Data omzet selama 7 hari terakhir.
            var pembayaranTujuhHari = await _context.Pembayaran
                .AsNoTracking()
                .Where(p =>
                    p.TanggalPembayaran >= tujuhHariLalu &&
                    p.TanggalPembayaran < besok)
                .Select(p => new
                {
                    p.TanggalPembayaran,
                    p.TotalBayar
                })
                .ToListAsync();

            viewModel.GrafikOmzet = Enumerable
                .Range(0, 7)
                .Select(i =>
                {
                    DateTime tanggal = tujuhHariLalu.AddDays(i);

                    return new OmzetDashboardViewModel
                    {
                        Tanggal = tanggal,
                        TotalOmzet = pembayaranTujuhHari
                            .Where(p =>
                                p.TanggalPembayaran.Date == tanggal.Date)
                            .Sum(p => p.TotalBayar)
                    };
                })
                .ToList();

            // Lima pembayaran terbaru.
            // Riwayat tetap ditampilkan meskipun meja sudah Soft Delete.
            viewModel.AktivitasTerbaru = await _context.Pembayaran
                .AsNoTracking()
                .Include(p => p.Pesanan)
                    .ThenInclude(p => p!.Meja)
                .OrderByDescending(p => p.TanggalPembayaran)
                .Take(5)
                .ToListAsync();

            return View(viewModel);
        }

        [Authorize]
        public IActionResult Privacy()
        {
            return View();
        }

        [Authorize]
        public IActionResult Menu()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id
                    ?? HttpContext.TraceIdentifier
            });
        }
    }
}
