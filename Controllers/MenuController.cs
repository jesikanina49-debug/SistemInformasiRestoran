
using System.Globalization;
using System.Security.Claims;
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
                NamaTabel = "menu",
                IdData = idData,
                Keterangan = keterangan,
                TanggalAktivitas = DateTime.Now
            };
        }

        // Menampilkan menu dan kategori aktif.
        public async Task<IActionResult> Index()
        {
            var daftarMenu = await _context.Menu
                .Where(m => !m.IsDeleted)
                .Include(m => m.Kategori)
                .AsNoTracking()
                .OrderBy(m => m.NamaMenu)
                .ToListAsync();

            var daftarKategori = await _context.Kategori
                .Where(k => !k.IsDeleted)
                .AsNoTracking()
                .OrderBy(k => k.NamaKategori)
                .ToListAsync();

            ViewBag.DaftarKategori = daftarKategori;

            return View(daftarMenu);
        }

        // Form tambah menu.
        [Authorize(Roles = "Manager")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await MuatKategoriAsync();
            return View();
        }

        // Menyimpan menu baru dan Audit Trail dalam satu transaksi.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Menu menu,
            IFormFile? fileGambar)
        {
            if (ModelState.IsValid)
            {
                bool kategoriAda = await _context.Kategori
                    .AnyAsync(k =>
                        k.IdKategori == menu.IdKategori &&
                        !k.IsDeleted);

                if (!kategoriAda)
                {
                    ModelState.AddModelError(
                        nameof(menu.IdKategori),
                        "Kategori yang dipilih tidak ditemukan atau sudah dihapus.");
                }
            }

            ValidasiGambar(fileGambar);

            if (!ModelState.IsValid)
            {
                await MuatKategoriAsync(menu.IdKategori);
                return View(menu);
            }

            string? gambarDisimpan = null;

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                if (fileGambar != null && fileGambar.Length > 0)
                {
                    gambarDisimpan = await SimpanGambarAsync(fileGambar);
                    menu.Gambar = gambarDisimpan;
                }

                menu.IsDeleted = false;

                _context.Menu.Add(menu);
                await _context.SaveChangesAsync();

                _context.AuditTrail.Add(BuatAudit(
                    "Tambah",
                    menu.IdMenu,
                    $"Menambahkan menu: {menu.NamaMenu}"
                ));

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    "Menu berhasil ditambahkan.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();

                if (!string.IsNullOrEmpty(gambarDisimpan))
                {
                    HapusFileGambar(gambarDisimpan);
                }

                TempData["ErrorMessage"] =
                    "Menu gagal ditambahkan. Silakan periksa kembali data dan coba lagi.";

                return RedirectToAction(nameof(Index));
            }
        }

        // Form edit menu.
        [Authorize(Roles = "Manager")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var menu = await _context.Menu
                .FirstOrDefaultAsync(m =>
                    m.IdMenu == id && !m.IsDeleted);

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
        public async Task<IActionResult> Edit(
            int id,
            Menu menu,
            string? harga,
            IFormFile? fileGambar)
        {
            if (id != menu.IdMenu)
            {
                return BadRequest();
            }

            ModelState.Remove(nameof(Menu.Harga));

            if (string.IsNullOrWhiteSpace(harga) ||
                !decimal.TryParse(
                    harga,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal hargaMenu))
            {
                ModelState.AddModelError(
                    nameof(Menu.Harga),
                    "Harga harus berupa angka yang valid. Contoh: 15000 atau 15000.50");
            }
            else if (hargaMenu < 0)
            {
                ModelState.AddModelError(
                    nameof(Menu.Harga),
                    "Harga tidak boleh kurang dari 0.");
            }
            else
            {
                menu.Harga = hargaMenu;
            }

            if (ModelState.IsValid)
            {
                bool kategoriAda = await _context.Kategori
                    .AnyAsync(k =>
                        k.IdKategori == menu.IdKategori &&
                        !k.IsDeleted);

                if (!kategoriAda)
                {
                    ModelState.AddModelError(
                        nameof(menu.IdKategori),
                        "Kategori yang dipilih tidak ditemukan atau sudah dihapus.");
                }
            }

            ValidasiGambar(fileGambar);

            if (!ModelState.IsValid)
            {
                await MuatKategoriAsync(menu.IdKategori);
                return View(menu);
            }

            var menuDatabase = await _context.Menu
                .FirstOrDefaultAsync(m =>
                    m.IdMenu == id && !m.IsDeleted);

            if (menuDatabase == null)
            {
                return NotFound();
            }

            // Stok tidak boleh diubah saat masih ada reservasi.
            bool adaReservasi = await _context.ReservasiStok
                .AnyAsync(r => r.IdMenu == id);

            if (adaReservasi && menu.Stok != menuDatabase.Stok)
            {
                ModelState.AddModelError(
                    nameof(menu.Stok),
                    "Stok tidak dapat diubah karena masih terdapat reservasi pada keranjang.");

                await MuatKategoriAsync(menu.IdKategori);
                return View(menu);
            }

            string namaLama = menuDatabase.NamaMenu;

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
                _context.AuditTrail.Add(BuatAudit(
                    "Ubah",
                    menuDatabase.IdMenu,
                    $"Memperbarui menu: {namaLama} menjadi {menuDatabase.NamaMenu}"
                ));

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

            // Gambar lama dihapus setelah perubahan database berhasil.
            if (gambarBaru != null &&
                !string.IsNullOrEmpty(gambarLama))
            {
                HapusFileGambar(gambarLama);
            }

            TempData["SuccessMessage"] =
                "Menu berhasil diperbarui.";

            return RedirectToAction(nameof(Index));
        }

        // Mengaktifkan atau menonaktifkan menu.
        // Berbeda dengan Soft Delete.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var menu = await _context.Menu
                .FirstOrDefaultAsync(m =>
                    m.IdMenu == id && !m.IsDeleted);

            if (menu == null)
            {
                TempData["ErrorMessage"] =
                    "Menu tidak ditemukan.";

                return RedirectToAction(nameof(Index));
            }

            try
            {
                menu.StatusTersedia = !menu.StatusTersedia;

                string aktivitas = menu.StatusTersedia
                    ? "Aktifkan"
                    : "Nonaktifkan";

                _context.AuditTrail.Add(BuatAudit(
                    aktivitas,
                    menu.IdMenu,
                    $"{aktivitas} menu: {menu.NamaMenu}"
                ));

                await _context.SaveChangesAsync();

                if (menu.StatusTersedia)
                {
                    TempData["SuccessMessage"] =
                        $"Menu {menu.NamaMenu} berhasil diaktifkan kembali.";
                }
                else
                {
                    TempData["InfoMessage"] =
                        $"Menu {menu.NamaMenu} berhasil dinonaktifkan.";
                }
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "Status menu gagal diperbarui. Silakan coba kembali.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Soft Delete menu.
        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SoftDelete(int id)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var menu = await _context.Menu
                    .FirstOrDefaultAsync(m =>
                        m.IdMenu == id && !m.IsDeleted);

                if (menu == null)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Menu tidak ditemukan.";

                    return RedirectToAction(nameof(Index));
                }

                // Memeriksa reservasi yang masih tercatat.
                bool adaReservasi = await _context.ReservasiStok
                    .AnyAsync(r => r.IdMenu == id);

                if (adaReservasi)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        $"Menu {menu.NamaMenu} tidak dapat dihapus karena masih memiliki reservasi stok. Selesaikan atau hapus menu dari keranjang terlebih dahulu.";

                    return RedirectToAction(nameof(Index));
                }

                menu.IsDeleted = true;

                _context.AuditTrail.Add(BuatAudit(
                    "Soft Delete",
                    menu.IdMenu,
                    $"Menghapus menu secara logis: {menu.NamaMenu}"
                ));

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Menu {menu.NamaMenu} berhasil dihapus.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Menu gagal dihapus. Silakan coba kembali.";

                return RedirectToAction(nameof(Index));
            }
        }

        // Validasi gambar.
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

        // Menyimpan gambar.
        private async Task<string> SimpanGambarAsync(IFormFile file)
        {
            string folder = Path.Combine(
                _environment.WebRootPath ?? Path.Combine(
                    _environment.ContentRootPath, "wwwroot"),
                "uploads", "menu");

            Directory.CreateDirectory(folder);

            string ekstensi = Path.GetExtension(file.FileName)
                .ToLowerInvariant();

            string namaFile = $"{Guid.NewGuid():N}{ekstensi}";

            string lokasiFile = Path.Combine(folder, namaFile);

            await using var stream = new FileStream(
                lokasiFile, FileMode.CreateNew);

            await file.CopyToAsync(stream);

            return namaFile;
        }

        // Menghapus file gambar.
        // Tidak dipanggil ketika Soft Delete.
        private void HapusFileGambar(string namaFile)
        {
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

        // Memuat kategori aktif untuk formulir.
        private async Task MuatKategoriAsync(int? idTerpilih = null)
        {
            var kategori = await _context.Kategori
                .Where(k => !k.IsDeleted)
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
