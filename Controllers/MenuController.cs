
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemInformasiRestoran.Data;
using SistemInformasiRestoran.Models;

namespace SistemInformasiRestoran.Controllers
{
    [Authorize]
    public class MenuController : Controller
    {
        private readonly RestoDbContext _context;
        private readonly IWebHostEnvironment _environment;

        private const long MaksimalUkuranGambar = 2 * 1024 * 1024;

        private static readonly string[] EkstensiGambar =
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        public MenuController(
            RestoDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
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

        // Menyimpan menu baru beserta gambar.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Menu menu, IFormFile? fileGambar)
        {
            if (ModelState.IsValid)
            {
                bool kategoriAda = await _context.Kategori
                    .AnyAsync(k => k.IdKategori == menu.IdKategori);

                if (!kategoriAda)
                {
                    ModelState.AddModelError(
                        nameof(menu.IdKategori),
                        "Kategori yang dipilih tidak ditemukan.");
                }
            }

            if (!ValidasiGambar(fileGambar))
            {
                // Pesan kesalahan ditambahkan oleh ValidasiGambar.
            }

            if (!ModelState.IsValid)
            {
                await MuatKategoriAsync(menu.IdKategori);
                return View(menu);
            }

            if (fileGambar != null && fileGambar.Length > 0)
            {
                menu.Gambar = await SimpanGambarAsync(fileGambar);
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

        // Menyimpan perubahan menu dan gambar jika diganti.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id, Menu menu, IFormFile? fileGambar)
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
                        "Kategori yang dipilih tidak ditemukan.");
                }
            }

            if (!ValidasiGambar(fileGambar))
            {
                // Pesan kesalahan ditambahkan oleh ValidasiGambar.
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

            string? gambarLama = menuDatabase.Gambar;
            string? gambarBaru = null;

            if (fileGambar != null && fileGambar.Length > 0)
            {
                gambarBaru = await SimpanGambarAsync(fileGambar);
                menuDatabase.Gambar = gambarBaru;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                if (gambarBaru != null)
                {
                    HapusFileGambar(gambarBaru);
                }

                throw;
            }

            if (gambarBaru != null && !string.IsNullOrEmpty(gambarLama))
            {
                HapusFileGambar(gambarLama);
            }

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

            string? gambarLama = menu.Gambar;

            _context.Menu.Remove(menu);

            try
            {
                await _context.SaveChangesAsync();

                if (!string.IsNullOrEmpty(gambarLama))
                {
                    HapusFileGambar(gambarLama);
                }

                TempData["SuccessMessage"] = "Menu berhasil dihapus.";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "Menu tidak dapat dihapus karena mungkin sudah digunakan dalam transaksi.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Memeriksa format dan ukuran gambar.
        private bool ValidasiGambar(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return true;
            }

            if (file.Length > MaksimalUkuranGambar)
            {
                ModelState.AddModelError(
                    "fileGambar",
                    "Ukuran gambar maksimal 2 MB.");
                return false;
            }

            string ekstensi = Path.GetExtension(file.FileName);

            if (!EkstensiGambar.Contains(
                ekstensi, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    "fileGambar",
                    "Format gambar harus JPG, JPEG, PNG, atau WEBP.");
                return false;
            }

            return true;
        }

        // Menyimpan gambar dengan nama file acak.
        private async Task<string> SimpanGambarAsync(IFormFile file)
        {
            string folder = Path.Combine(
                _environment.WebRootPath ?? Path.Combine(
                    _environment.ContentRootPath, "wwwroot"),
                "uploads", "menu");

            Directory.CreateDirectory(folder);

            string ekstensi = Path.GetExtension(file.FileName).ToLowerInvariant();
            string namaFile = $"{Guid.NewGuid():N}{ekstensi}";
            string lokasiFile = Path.Combine(folder, namaFile);

            await using var stream = new FileStream(
                lokasiFile, FileMode.CreateNew);

            await file.CopyToAsync(stream);

            return namaFile;
        }

        // Menghapus file gambar dari folder upload.
        private void HapusFileGambar(string namaFile)
        {
            // Hanya menerima nama file, bukan jalur dari pengguna.
            if (Path.GetFileName(namaFile) != namaFile)
            {
                return;
            }

            string folder = Path.Combine(
                _environment.WebRootPath ?? Path.Combine(
                    _environment.ContentRootPath, "wwwroot"),
                "uploads", "menu");

            string lokasiFile = Path.Combine(folder, namaFile);

            if (System.IO.File.Exists(lokasiFile))
            {
                System.IO.File.Delete(lokasiFile);
            }
        }

        // Mengambil kategori untuk formulir.
        private async Task MuatKategoriAsync(int? idTerpilih = null)
        {
            var kategori = await _context.Kategori
                .AsNoTracking()
                .OrderBy(k => k.NamaKategori)
                .ToListAsync();

            ViewBag.DaftarKategori = new SelectList(
                kategori,
                "IdKategori",
                "NamaKategori",
                idTerpilih);
        }
    }
}
