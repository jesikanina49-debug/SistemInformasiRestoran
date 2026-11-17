
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemInformasiRestoran.Data;
using SistemInformasiRestoran.Models;

namespace SistemInformasiRestoran.Controllers
{
    [Authorize]
    public class PembayaranController : Controller
    {
        private readonly RestoDbContext _context;

        public PembayaranController(RestoDbContext context)
        {
            _context = context;
        }

        // Menampilkan daftar pesanan yang belum dibayar
        public async Task<IActionResult> Index()
        {
            var daftarPesanan = await _context.Pesanan
                .Include(p => p.Meja)
                .Where(p => p.StatusPesanan == "Diproses")
                .OrderByDescending(p => p.TanggalPesanan)
                .ToListAsync();

            return View(daftarPesanan);
        }

        // Menampilkan detail pesanan
        public async Task<IActionResult> Detail(int id)
        {
            var pesanan = await _context.Pesanan
                .Include(p => p.Meja)
                .FirstOrDefaultAsync(p => p.IdPesanan == id);

            if (pesanan == null)
            {
                TempData["ErrorMessage"] = "Pesanan tidak ditemukan.";
                return RedirectToAction(nameof(Index));
            }

            if (pesanan.StatusPesanan != "Diproses")
            {
                TempData["ErrorMessage"] =
                    "Pesanan ini sudah selesai atau tidak dapat dibayar.";

                return RedirectToAction(nameof(Index));
            }

            var detailPesanan = await _context.DetailPesanan
                .Include(d => d.Menu)
                .Where(d => d.IdPesanan == id)
                .ToListAsync();

            ViewBag.DetailPesanan = detailPesanan;

            return View(pesanan);
        }

        // Memproses pembayaran
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Proses(
            int idPesanan,
            string? metodePembayaran,
            decimal uangDiterima)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var pesanan = await _context.Pesanan
                    .FirstOrDefaultAsync(p => p.IdPesanan == idPesanan);

                if (pesanan == null)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "Pesanan tidak ditemukan.";
                    return RedirectToAction(nameof(Index));
                }

                if (pesanan.StatusPesanan != "Diproses")
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] =
                        "Pesanan sudah dibayar atau tidak aktif.";
                    return RedirectToAction(nameof(Index));
                }

                string[] metodeValid = { "Tunai", "Transfer", "QRIS" };

                if (string.IsNullOrWhiteSpace(metodePembayaran) ||
                    !metodeValid.Contains(metodePembayaran))
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "Metode pembayaran tidak valid.";
                    return RedirectToAction(nameof(Detail),
                        new { id = idPesanan });
                }

                bool sudahDibayar = await _context.Pembayaran
                    .AnyAsync(p => p.IdPesanan == idPesanan);

                if (sudahDibayar)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] =
                        "Pembayaran untuk pesanan ini sudah tercatat.";
                    return RedirectToAction(nameof(Index));
                }

                decimal totalBayar = await _context.DetailPesanan
                    .Where(d => d.IdPesanan == idPesanan)
                    .SumAsync(d => (decimal?)d.Subtotal) ?? 0;

                if (totalBayar <= 0)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "Pesanan belum memiliki menu.";
                    return RedirectToAction(nameof(Detail),
                        new { id = idPesanan });
                }

                var meja = await _context.Meja
                    .FirstOrDefaultAsync(m => m.IdMeja == pesanan.IdMeja);

                if (meja == null)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "Data meja tidak ditemukan.";
                    return RedirectToAction(nameof(Index));
                }

                if (metodePembayaran != "Tunai")
                {
                    uangDiterima = totalBayar;
                }

                if (uangDiterima < totalBayar)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] =
                        "Uang yang diterima masih kurang dari total tagihan.";
                    return RedirectToAction(nameof(Detail),
                        new { id = idPesanan });
                }

                decimal kembalian = uangDiterima - totalBayar;

                var pembayaran = new Pembayaran
                {
                    IdPesanan = idPesanan,
                    TanggalPembayaran = DateTime.Now,
                    MetodePembayaran = metodePembayaran,
                    TotalBayar = totalBayar,
                    UangDiterima = uangDiterima,
                    Kembalian = kembalian
                };

                _context.Pembayaran.Add(pembayaran);

                pesanan.Total = totalBayar;
                pesanan.StatusPesanan = "Selesai";

                meja.StatusMeja = "Tersedia";
                meja.MulaiDigunakan = null;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Pembayaran berhasil! Kembalian: Rp {kembalian:N0}.";

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Terjadi kesalahan saat memproses pembayaran. Silakan coba kembali.";

                return RedirectToAction(nameof(Index));
            }
        }

        // Menghapus pesanan yang belum dibayar - khusus Manager
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hapus(int id)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var pesanan = await _context.Pesanan
                    .FirstOrDefaultAsync(p => p.IdPesanan == id);

                if (pesanan == null)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "Pesanan tidak ditemukan.";
                    return RedirectToAction(nameof(Index));
                }

                if (pesanan.StatusPesanan != "Diproses")
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] =
                        "Hanya pesanan yang belum selesai yang dapat dihapus.";
                    return RedirectToAction(nameof(Index));
                }

                bool sudahDibayar = await _context.Pembayaran
                    .AnyAsync(p => p.IdPesanan == id);

                if (sudahDibayar)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] =
                        "Pesanan yang sudah memiliki catatan pembayaran tidak boleh dihapus.";
                    return RedirectToAction(nameof(Index));
                }

                // Mengambil seluruh detail pesanan.
                var detailPesanan = await _context.DetailPesanan
                    .Where(d => d.IdPesanan == id)
                    .ToListAsync();

                // Mengembalikan stok sesuai jumlah setiap menu.
                foreach (var detail in detailPesanan)
                {
                    var menu = await _context.Menu
                        .FirstOrDefaultAsync(m => m.IdMenu == detail.IdMenu);

                    if (menu != null)
                    {
                        menu.Stok += detail.Jumlah;
                    }
                }

                // Menghapus detail pesanan.
                _context.DetailPesanan.RemoveRange(detailPesanan);

                // Memeriksa status meja.
                var meja = await _context.Meja
                    .FirstOrDefaultAsync(m => m.IdMeja == pesanan.IdMeja);

                if (meja != null)
                {
                    bool masihAdaPesananAktif = await _context.Pesanan
                        .AnyAsync(p =>
                            p.IdMeja == pesanan.IdMeja &&
                            p.IdPesanan != id &&
                            p.StatusPesanan == "Diproses");

                    if (!masihAdaPesananAktif)
                    {
                        meja.StatusMeja = "Tersedia";
                        meja.MulaiDigunakan = null;
                    }
                }

                // Menghapus pesanan.
                _context.Pesanan.Remove(pesanan);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Pesanan #{id} berhasil dihapus. Stok menu telah dikembalikan.";
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Pesanan gagal dihapus. Stok tidak jadi dikembalikan.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
