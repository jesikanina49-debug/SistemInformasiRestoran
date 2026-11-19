
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemInformasiRestoran.Data;
using SistemInformasiRestoran.Models;

namespace SistemInformasiRestoran.Controllers
{
    [Authorize]
    public class MejaController : Controller
    {
        private readonly RestoDbContext _context;

        public MejaController(RestoDbContext context)
        {
            _context = context;
        }

        // Membuat catatan Audit Trail.
        private AuditTrail BuatAudit(
            string aktivitas,
            int idData,
            string keterangan)
        {
            var idUser = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return new AuditTrail
            {
                IdUser = int.TryParse(idUser, out int hasil) ? hasil : null,
                NamaUser = User.Identity?.Name ?? "SYSTEM",
                Aktivitas = aktivitas,
                NamaTabel = "meja",
                IdData = idData,
                Keterangan = keterangan,
                TanggalAktivitas = DateTime.Now
            };
        }

        // Menampilkan meja yang masih aktif.
        public async Task<IActionResult> Index()
        {
            var daftarMeja = await _context.Meja
                .Where(m => !m.IsDeleted)
                .AsNoTracking()
                .OrderBy(m => m.NomorMeja)
                .ToListAsync();

            return View(daftarMeja);
        }

        // Form tambah meja.
        [Authorize(Roles = "Manager")]
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // Menyimpan meja baru.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Meja meja)
        {
            meja.NomorMeja = meja.NomorMeja?.Trim() ?? "";

            meja.StatusMeja = "Tersedia";
            meja.MulaiDigunakan = null;
            meja.IsDeleted = false;

            // Nomor meja yang pernah digunakan tidak boleh dipakai ulang.
            bool nomorSudahAda = await _context.Meja
                .AnyAsync(m => m.NomorMeja == meja.NomorMeja);

            if (nomorSudahAda)
            {
                ModelState.AddModelError(
                    nameof(meja.NomorMeja),
                    "Nomor meja sudah digunakan.");
            }

            if (!ModelState.IsValid)
            {
                return View(meja);
            }

            await using var transaksi =
                await _context.Database.BeginTransactionAsync();

            try
            {
                _context.Meja.Add(meja);
                await _context.SaveChangesAsync();

                _context.AuditTrail.Add(BuatAudit(
                    "Tambah",
                    meja.IdMeja,
                    $"Menambahkan meja: {meja.NomorMeja}"
                ));

                await _context.SaveChangesAsync();
                await transaksi.CommitAsync();

                TempData["SuccessMessage"] =
                    "Meja berhasil ditambahkan.";
            }
            catch (Exception)
            {
                await transaksi.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Meja gagal ditambahkan. Silakan periksa kembali data.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Form edit meja.
        [Authorize(Roles = "Manager")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var meja = await _context.Meja
                .FirstOrDefaultAsync(m =>
                    m.IdMeja == id && !m.IsDeleted);

            if (meja == null)
            {
                return NotFound();
            }

            return View(meja);
        }

        // Menyimpan perubahan meja.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Meja meja)
        {
            if (id != meja.IdMeja)
            {
                return BadRequest();
            }

            meja.NomorMeja = meja.NomorMeja?.Trim() ?? "";

            bool nomorSudahAda = await _context.Meja
                .AnyAsync(m =>
                    m.NomorMeja == meja.NomorMeja &&
                    m.IdMeja != id);

            if (nomorSudahAda)
            {
                ModelState.AddModelError(
                    nameof(meja.NomorMeja),
                    "Nomor meja sudah digunakan.");
            }

            var mejaDatabase = await _context.Meja
                .FirstOrDefaultAsync(m =>
                    m.IdMeja == id && !m.IsDeleted);

            if (mejaDatabase == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                meja.StatusMeja = mejaDatabase.StatusMeja;
                meja.MulaiDigunakan = mejaDatabase.MulaiDigunakan;
                return View(meja);
            }

            string nomorLama = mejaDatabase.NomorMeja;

            mejaDatabase.NomorMeja = meja.NomorMeja;
            mejaDatabase.JumlahKursi = meja.JumlahKursi;

            _context.AuditTrail.Add(BuatAudit(
                "Ubah",
                mejaDatabase.IdMeja,
                $"Mengubah meja {nomorLama} menjadi {mejaDatabase.NomorMeja}"
            ));

            try
            {
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "Data meja berhasil diperbarui.";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "Data meja gagal diperbarui. Silakan periksa kembali.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Soft Delete meja.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await using var transaksi =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var meja = await _context.Meja
                    .FirstOrDefaultAsync(m =>
                        m.IdMeja == id && !m.IsDeleted);

                if (meja == null)
                {
                    await transaksi.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Data meja tidak ditemukan.";

                    return RedirectToAction(nameof(Index));
                }

                bool adaPesananBelumDibayar =
                    await _context.Pesanan.AnyAsync(p =>
                        p.IdMeja == id &&
                        p.StatusPesanan == "Diproses");

                bool adaReservasiAktif =
                    await _context.ReservasiStok.AnyAsync(r =>
                        r.IdMeja == id &&
                        r.WaktuKedaluwarsa > DateTime.Now);

                if (meja.StatusMeja == "Digunakan" ||
                    adaPesananBelumDibayar ||
                    adaReservasiAktif)
                {
                    await transaksi.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Meja tidak dapat dihapus karena sedang digunakan, memiliki pesanan belum dibayar, atau masih memiliki reservasi stok aktif.";

                    return RedirectToAction(nameof(Index));
                }

                meja.IsDeleted = true;

                _context.AuditTrail.Add(BuatAudit(
                    "Soft Delete",
                    meja.IdMeja,
                    $"Menghapus meja secara logis: {meja.NomorMeja}"
                ));

                await _context.SaveChangesAsync();
                await transaksi.CommitAsync();

                TempData["SuccessMessage"] =
                    "Data meja berhasil dihapus.";
            }
            catch (Exception)
            {
                await transaksi.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Data meja gagal dihapus. Silakan coba kembali.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
