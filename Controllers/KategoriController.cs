
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemInformasiRestoran.Data;
using SistemInformasiRestoran.Models;

namespace SistemInformasiRestoran.Controllers
{
    [Authorize]
    public class KategoriController : Controller
    {
        private readonly RestoDbContext _context;

        public KategoriController(RestoDbContext context)
        {
            _context = context;
        }

        // Membuat catatan Audit Trail.
        private AuditTrail BuatAudit(
            string aktivitas,
            int idData,
            string keterangan)
        {
            var idUser = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            return new AuditTrail
            {
                IdUser = int.TryParse(idUser, out int hasil)
                    ? hasil
                    : null,
                NamaUser = User.Identity?.Name ?? "SYSTEM",
                Aktivitas = aktivitas,
                NamaTabel = "kategori",
                IdData = idData,
                Keterangan = keterangan,
                TanggalAktivitas = DateTime.Now
            };
        }

        // Menampilkan kategori yang masih aktif.
        public async Task<IActionResult> Index()
        {
            var daftarKategori = await _context.Kategori
                .AsNoTracking()
                .Where(k => !k.IsDeleted)
                .OrderBy(k => k.NamaKategori)
                .ToListAsync();

            return View(daftarKategori);
        }

        // Form tambah kategori.
        [Authorize(Roles = "Manager")]
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // Menyimpan kategori baru.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Kategori kategori)
        {
            kategori.NamaKategori =
                kategori.NamaKategori?.Trim() ?? "";

            bool sudahAda = await _context.Kategori
                .AnyAsync(k =>
                    k.NamaKategori == kategori.NamaKategori &&
                    !k.IsDeleted);

            if (sudahAda)
            {
                ModelState.AddModelError(
                    nameof(kategori.NamaKategori),
                    "Nama kategori sudah digunakan.");
            }

            if (!ModelState.IsValid)
            {
                return View(kategori);
            }

            await using var transaksi =
                await _context.Database.BeginTransactionAsync();

            try
            {
                kategori.IsDeleted = false;

                _context.Kategori.Add(kategori);
                await _context.SaveChangesAsync();

                _context.AuditTrail.Add(BuatAudit(
                    "Tambah",
                    kategori.IdKategori,
                    $"Menambahkan kategori: {kategori.NamaKategori}"
                ));

                await _context.SaveChangesAsync();
                await transaksi.CommitAsync();

                TempData["SuccessMessage"] =
                    "Kategori berhasil ditambahkan.";
            }
            catch (Exception)
            {
                await transaksi.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Kategori gagal ditambahkan. Silakan periksa kembali data.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Form edit kategori.
        [Authorize(Roles = "Manager")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var kategori = await _context.Kategori
                .FirstOrDefaultAsync(k =>
                    k.IdKategori == id && !k.IsDeleted);

            if (kategori == null)
            {
                return NotFound();
            }

            return View(kategori);
        }

        // Menyimpan perubahan kategori.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Kategori kategori)
        {
            if (id != kategori.IdKategori)
            {
                return BadRequest();
            }

            kategori.NamaKategori =
                kategori.NamaKategori?.Trim() ?? "";

            bool sudahAda = await _context.Kategori
                .AnyAsync(k =>
                    k.NamaKategori == kategori.NamaKategori &&
                    k.IdKategori != id &&
                    !k.IsDeleted);

            if (sudahAda)
            {
                ModelState.AddModelError(
                    nameof(kategori.NamaKategori),
                    "Nama kategori sudah digunakan.");
            }

            if (!ModelState.IsValid)
            {
                return View(kategori);
            }

            var kategoriDatabase = await _context.Kategori
                .FirstOrDefaultAsync(k =>
                    k.IdKategori == id && !k.IsDeleted);

            if (kategoriDatabase == null)
            {
                return NotFound();
            }

            string namaLama = kategoriDatabase.NamaKategori;

            kategoriDatabase.NamaKategori =
                kategori.NamaKategori;

            _context.AuditTrail.Add(BuatAudit(
                "Ubah",
                kategoriDatabase.IdKategori,
                $"Mengubah nama kategori dari {namaLama} menjadi {kategoriDatabase.NamaKategori}"
            ));

            try
            {
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "Kategori berhasil diperbarui.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] =
                    "Kategori gagal diperbarui. Silakan coba kembali.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Soft Delete kategori.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await using var transaksi =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var kategori = await _context.Kategori
                    .FirstOrDefaultAsync(k =>
                        k.IdKategori == id && !k.IsDeleted);

                if (kategori == null)
                {
                    await transaksi.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Kategori tidak ditemukan.";

                    return RedirectToAction(nameof(Index));
                }

                // Menandai kategori sebagai terhapus.
                // Data fisik tetap tersimpan di database.
                kategori.IsDeleted = true;

                _context.AuditTrail.Add(BuatAudit(
                    "Soft Delete",
                    kategori.IdKategori,
                    $"Menghapus kategori secara logis: {kategori.NamaKategori}"
                ));

                await _context.SaveChangesAsync();
                await transaksi.CommitAsync();

                TempData["SuccessMessage"] =
                    "Kategori berhasil dihapus.";
            }
            catch (Exception)
            {
                await transaksi.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Kategori gagal dihapus. Silakan coba kembali.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
