
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

        // Menampilkan seluruh data meja.
        public async Task<IActionResult> Index()
        {
            var daftarMeja = await _context.Meja
                .AsNoTracking()
                .OrderBy(m => m.NomorMeja)
                .ToListAsync();

            return View(daftarMeja);
        }

        // Menampilkan formulir tambah meja.
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

            // Status meja baru selalu Tersedia.
            meja.StatusMeja = "Tersedia";
            meja.MulaiDigunakan = null;

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

            _context.Meja.Add(meja);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Meja berhasil ditambahkan.";

            return RedirectToAction(nameof(Index));
        }

        // Menampilkan formulir edit meja.
        [Authorize(Roles = "Manager")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var meja = await _context.Meja.FindAsync(id);

            if (meja == null)
            {
                return NotFound();
            }

            return View(meja);
        }

        // Menyimpan perubahan data meja.
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

            var mejaDatabase = await _context.Meja.FindAsync(id);

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

            // Hanya nomor meja dan jumlah kursi yang bisa diubah.
            // Status dan waktu penggunaan tetap mengikuti transaksi.
            mejaDatabase.NomorMeja = meja.NomorMeja;
            mejaDatabase.JumlahKursi = meja.JumlahKursi;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Data meja berhasil diperbarui.";

            return RedirectToAction(nameof(Index));
        }

        // Menghapus data meja.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var meja = await _context.Meja.FindAsync(id);

            if (meja == null)
            {
                TempData["ErrorMessage"] =
                    "Data meja tidak ditemukan.";

                return RedirectToAction(nameof(Index));
            }

            bool adaPesananBelumDibayar = await _context.Pesanan
                .AnyAsync(p =>
                    p.IdMeja == id &&
                    p.StatusPesanan == "Diproses");

            if (meja.StatusMeja == "Digunakan" ||
                adaPesananBelumDibayar)
            {
                TempData["ErrorMessage"] =
                    "Meja tidak dapat dihapus karena sedang digunakan atau masih memiliki pesanan yang belum dibayar.";

                return RedirectToAction(nameof(Index));
            }

            _context.Meja.Remove(meja);

            try
            {
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "Data meja berhasil dihapus.";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "Meja tidak dapat dihapus karena sudah digunakan dalam transaksi.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
