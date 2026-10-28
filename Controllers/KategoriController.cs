
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

        // Menampilkan daftar kategori.
        public async Task<IActionResult> Index()
        {
            var daftarKategori = await _context.Kategori
                .AsNoTracking()
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
            if (ModelState.IsValid)
            {
                bool sudahAda = await _context.Kategori
                    .AnyAsync(k => k.NamaKategori == kategori.NamaKategori);

                if (sudahAda)
                {
                    ModelState.AddModelError(
                        nameof(kategori.NamaKategori),
                        "Nama kategori sudah digunakan.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(kategori);
            }

            _context.Kategori.Add(kategori);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Kategori berhasil ditambahkan.";
            return RedirectToAction(nameof(Index));
        }

        // Form edit kategori.
        [Authorize(Roles = "Manager")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var kategori = await _context.Kategori.FindAsync(id);

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
        public async Task<IActionResult> Edit(int id, Kategori kategori)
        {
            if (id != kategori.IdKategori)
            {
                return BadRequest();
            }

            if (ModelState.IsValid)
            {
                bool sudahAda = await _context.Kategori.AnyAsync(
                    k => k.NamaKategori == kategori.NamaKategori
                         && k.IdKategori != id);

                if (sudahAda)
                {
                    ModelState.AddModelError(
                        nameof(kategori.NamaKategori),
                        "Nama kategori sudah digunakan.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(kategori);
            }

            var kategoriDatabase = await _context.Kategori.FindAsync(id);

            if (kategoriDatabase == null)
            {
                return NotFound();
            }

            kategoriDatabase.NamaKategori = kategori.NamaKategori;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Kategori berhasil diperbarui.";
            return RedirectToAction(nameof(Index));
        }

        // Menghapus kategori.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var kategori = await _context.Kategori.FindAsync(id);

            if (kategori == null)
            {
                TempData["ErrorMessage"] = "Kategori tidak ditemukan.";
                return RedirectToAction(nameof(Index));
            }

            _context.Kategori.Remove(kategori);

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Kategori berhasil dihapus.";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "Kategori tidak dapat dihapus karena masih digunakan oleh menu.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
