
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemInformasiRestoran.Data;
using SistemInformasiRestoran.Models;

namespace SistemInformasiRestoran.Controllers
{
    [Authorize]
    public class MenuController : Controller
    {
        private readonly RestoDbContext _context;

        public MenuController(RestoDbContext context)
        {
            _context = context;
        }

        // Menampilkan daftar menu.
        public async Task<IActionResult> Index()
        {
            var daftarMenu = await _context.Menu
                .Include(m => m.Kategori)
                .AsNoTracking()
                .OrderBy(m => m.NamaMenu)
                .ToListAsync();

            return View(daftarMenu);
        }

        // Menampilkan formulir tambah menu.
        [Authorize(Roles = "Manager")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await MuatKategoriAsync();
            return View();
        }

        // Menyimpan menu baru.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Menu menu)
        {
            if (ModelState.IsValid)
            {
                bool kategoriAda = await _context.Kategori
                    .AnyAsync(k => k.IdKategori == menu.IdKategori);

                if (!kategoriAda)
                {
                    ModelState.AddModelError(
                        nameof(menu.IdKategori),
                        "Kategori yang dipilih tidak ditemukan."
                    );
                }
            }

            if (!ModelState.IsValid)
            {
                await MuatKategoriAsync(menu.IdKategori);
                return View(menu);
            }

            _context.Menu.Add(menu);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Menu berhasil ditambahkan.";
            return RedirectToAction(nameof(Index));
        }

        // Menampilkan formulir edit menu.
        [Authorize(Roles = "Manager")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var menu = await _context.Menu.FindAsync(id);

            if (menu == null)
            {
                return NotFound();
            }

            await MuatKategoriAsync(menu.IdKategori);
            return View(menu);
        }

        // Menyimpan perubahan menu.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Menu menu)
        {
            if (id != menu.IdMenu)
            {
                return BadRequest();
            }

            if (ModelState.IsValid)
            {
                bool kategoriAda = await _context.Kategori
                    .AnyAsync(k => k.IdKategori == menu.IdKategori);

                if (!kategoriAda)
                {
                    ModelState.AddModelError(
                        nameof(menu.IdKategori),
                        "Kategori yang dipilih tidak ditemukan."
                    );
                }
            }

            if (!ModelState.IsValid)
            {
                await MuatKategoriAsync(menu.IdKategori);
                return View(menu);
            }

            var menuDatabase = await _context.Menu.FindAsync(id);

            if (menuDatabase == null)
            {
                return NotFound();
            }

            menuDatabase.NamaMenu = menu.NamaMenu;
            menuDatabase.IdKategori = menu.IdKategori;
            menuDatabase.Harga = menu.Harga;
            menuDatabase.Stok = menu.Stok;
            menuDatabase.StatusTersedia = menu.StatusTersedia;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Menu berhasil diperbarui.";
            return RedirectToAction(nameof(Index));
        }

        // Menghapus menu.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var menu = await _context.Menu.FindAsync(id);

            if (menu == null)
            {
                TempData["ErrorMessage"] = "Menu tidak ditemukan.";
                return RedirectToAction(nameof(Index));
            }

            _context.Menu.Remove(menu);

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Menu berhasil dihapus.";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "Menu tidak dapat dihapus karena mungkin sudah digunakan dalam transaksi.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Mengambil kategori untuk pilihan pada formulir.
        private async Task MuatKategoriAsync(int? idTerpilih = null)
        {
            var kategori = await _context.Kategori
                .AsNoTracking()
                .OrderBy(k => k.NamaKategori)
                .ToListAsync();

            ViewBag.DaftarKategori = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                kategori,
                "IdKategori",
                "NamaKategori",
                idTerpilih
            );
        }
    }
}
