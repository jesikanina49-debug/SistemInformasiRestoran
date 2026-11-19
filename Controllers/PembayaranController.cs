
using System.Security.Claims;
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

        // Membuat catatan Audit Trail.
        private AuditTrail BuatAudit(
            string aktivitas,
            string namaTabel,
            int? idData,
            string keterangan,
            int idUser)
        {
            return new AuditTrail
            {
                IdUser = idUser,
                NamaUser = User.Identity?.Name ?? "SYSTEM",
                Aktivitas = aktivitas,
                NamaTabel = namaTabel,
                IdData = idData,
                Keterangan = keterangan,
                TanggalAktivitas = DateTime.Now
            };
        }

        // Menampilkan daftar pesanan yang belum dibayar.
        public async Task<IActionResult> Index()
        {
            var daftarPesanan = await _context.Pesanan
                .Include(p => p.Meja)
                .Where(p => p.StatusPesanan == "Diproses")
                .OrderByDescending(p => p.TanggalPesanan)
                .ToListAsync();

            return View(daftarPesanan);
        }

        // Menampilkan detail pesanan.
        public async Task<IActionResult> Detail(int id)
        {
            var pesanan = await _context.Pesanan
                .Include(p => p.Meja)
                .FirstOrDefaultAsync(p => p.IdPesanan == id);

            if (pesanan == null)
            {
                TempData["ErrorMessage"] =
                    "Pesanan tidak ditemukan.";

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

        // Memproses pembayaran.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Proses(
            int idPesanan,
            string? metodePembayaran,
            decimal uangDiterima)
        {
            if (!int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out int idUser))
            {
                TempData["ErrorMessage"] =
                    "Identitas pengguna tidak valid. Silakan login kembali.";

                return RedirectToAction("Login", "Account");
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var pesanan = await _context.Pesanan
                    .FirstOrDefaultAsync(p =>
                        p.IdPesanan == idPesanan);

                if (pesanan == null)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Pesanan tidak ditemukan.";

                    return RedirectToAction(nameof(Index));
                }

                if (pesanan.StatusPesanan != "Diproses")
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Pesanan sudah dibayar atau tidak aktif.";

                    return RedirectToAction(nameof(Index));
                }

                string[] metodeValid =
                {
                    "Tunai",
                    "Transfer",
                    "QRIS"
                };

                if (string.IsNullOrWhiteSpace(metodePembayaran) ||
                    !metodeValid.Contains(metodePembayaran))
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Metode pembayaran tidak valid.";

                    return RedirectToAction(
                        nameof(Detail),
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

                    TempData["ErrorMessage"] =
                        "Pesanan belum memiliki menu.";

                    return RedirectToAction(
                        nameof(Detail),
                        new { id = idPesanan });
                }

                // Meja tetap dicari meskipun sudah Soft Delete,
                // karena transaksi lama harus tetap dapat diselesaikan.
                var meja = await _context.Meja
                    .FirstOrDefaultAsync(m =>
                        m.IdMeja == pesanan.IdMeja);

                if (meja == null)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Data meja tidak ditemukan.";

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

                    return RedirectToAction(
                        nameof(Detail),
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

                // Simpan pembayaran terlebih dahulu agar IdPembayaran terbentuk.
                await _context.SaveChangesAsync();

                // Audit menggunakan IdPembayaran yang sebenarnya.
                _context.AuditTrail.Add(BuatAudit(
                    "Pembayaran",
                    "pembayaran",
                    pembayaran.IdPembayaran,
                    $"Pembayaran pesanan #{idPesanan} berhasil. Metode: {metodePembayaran}. Total: Rp{totalBayar:N0}.",
                    idUser
                ));

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

        // Membatalkan pesanan yang belum dibayar.
        // Khusus Manager. Data transaksi tidak dihapus permanen.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hapus(int id)
        {
            if (!int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out int idUser))
            {
                TempData["ErrorMessage"] =
                    "Identitas pengguna tidak valid.";

                return RedirectToAction(nameof(Index));
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var pesanan = await _context.Pesanan
                    .FirstOrDefaultAsync(p => p.IdPesanan == id);

                if (pesanan == null)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Pesanan tidak ditemukan.";

                    return RedirectToAction(nameof(Index));
                }

                if (pesanan.StatusPesanan != "Diproses")
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Hanya pesanan yang belum selesai yang dapat dibatalkan.";

                    return RedirectToAction(nameof(Index));
                }

                bool sudahDibayar = await _context.Pembayaran
                    .AnyAsync(p => p.IdPesanan == id);

                if (sudahDibayar)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Pesanan yang sudah memiliki catatan pembayaran tidak boleh dibatalkan.";

                    return RedirectToAction(nameof(Index));
                }

                // Mengambil seluruh detail pesanan.
                var detailPesanan = await _context.DetailPesanan
                    .Where(d => d.IdPesanan == id)
                    .ToListAsync();

                // Mengembalikan stok sesuai jumlah setiap menu.
                // Menu tetap dicari meskipun sudah Soft Delete.
                foreach (var detail in detailPesanan)
                {
                    var menu = await _context.Menu
                        .FirstOrDefaultAsync(m =>
                            m.IdMenu == detail.IdMenu);

                    if (menu != null)
                    {
                        menu.Stok += detail.Jumlah;

                        _context.AuditTrail.Add(BuatAudit(
                            "Pengembalian Stok",
                            "menu",
                            menu.IdMenu,
                            $"Stok {menu.NamaMenu} dikembalikan sebanyak {detail.Jumlah} karena pesanan #{id} dibatalkan.",
                            idUser
                        ));
                    }
                }

                // Mengubah status pesanan, bukan menghapus datanya.
                pesanan.StatusPesanan = "Dibatalkan";

                // Memeriksa apakah meja masih memiliki pesanan aktif lain.
                var meja = await _context.Meja
                    .FirstOrDefaultAsync(m =>
                        m.IdMeja == pesanan.IdMeja);

                if (meja != null)
                {
                    bool masihAdaPesananAktif =
                        await _context.Pesanan.AnyAsync(p =>
                            p.IdMeja == pesanan.IdMeja &&
                            p.IdPesanan != id &&
                            p.StatusPesanan == "Diproses");

                    if (!masihAdaPesananAktif)
                    {
                        meja.StatusMeja = "Tersedia";
                        meja.MulaiDigunakan = null;
                    }
                }

                _context.AuditTrail.Add(BuatAudit(
                    "Pembatalan Pesanan",
                    "pesanan",
                    pesanan.IdPesanan,
                    $"Pesanan #{id} dibatalkan oleh Manager. Detail pesanan tetap disimpan dan stok dikembalikan.",
                    idUser
                ));

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Pesanan #{id} berhasil dibatalkan. Riwayat transaksi tetap tersimpan dan stok telah dikembalikan.";
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Pesanan gagal dibatalkan. Perubahan stok dan status tidak disimpan.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
