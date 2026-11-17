
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemInformasiRestoran.Data;
using SistemInformasiRestoran.Models;
using SistemInformasiRestoran.Models.ViewModels;
using SistemInformasiRestoran.Services;

namespace SistemInformasiRestoran.Controllers
{
    [Authorize]
    public class PemesananController : Controller
    {
        private readonly RestoDbContext _context;
        private readonly ReservasiStokService _reservasiStokService;

        public PemesananController(
            RestoDbContext context,
            ReservasiStokService reservasiStokService)
        {
            _context = context;
            _reservasiStokService = reservasiStokService;
        }

        // Menampilkan daftar meja, menu tersedia, dan keranjang.
        public async Task<IActionResult> Index(int? idMeja)
        {
            var viewModel = new PemesananViewModel
            {
                DaftarMeja = await _context.Meja
                    .OrderBy(m => m.NomorMeja)
                    .ToListAsync(),

                DaftarMenu = await _context.Menu
                    .Include(m => m.Kategori)
                    .Where(m => m.StatusTersedia && m.Stok > 0)
                    .OrderBy(m => m.NamaMenu)
                    .ToListAsync()
            };

            if (idMeja.HasValue)
            {
                viewModel.MejaDipilih = await _context.Meja
                    .FirstOrDefaultAsync(m => m.IdMeja == idMeja.Value);

                if (viewModel.MejaDipilih == null)
                {
                    TempData["ErrorMessage"] = "Meja tidak ditemukan.";
                    return RedirectToAction(nameof(Index));
                }

                viewModel.Keranjang = AmbilKeranjang(idMeja.Value);
            }

            return View(viewModel);
        }

        // Menambahkan menu ke keranjang menggunakan AJAX.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Tambah(
            int idMeja,
            int idMenu,
            int jumlah = 1)
        {
            bool ajax = IsAjaxRequest();

            if (jumlah < 1)
            {
                return GagalAtauRedirect(
                    "Jumlah pesanan minimal 1.", idMeja);
            }

            var meja = await _context.Meja.FindAsync(idMeja);

            if (meja == null ||
                (meja.StatusMeja != "Tersedia" &&
                 meja.StatusMeja != "Digunakan"))
            {
                return GagalAtauRedirect(
                    "Meja tidak dapat digunakan.", idMeja);
            }

            var menu = await _context.Menu.FindAsync(idMenu);

            if (menu == null || !menu.StatusTersedia)
            {
                return GagalAtauRedirect(
                    "Menu tidak tersedia atau sudah dinonaktifkan.",
                    idMeja);
            }

            var keranjang = AmbilKeranjang(idMeja);
            var item = keranjang.FirstOrDefault(
                x => x.IdMenu == idMenu);

            int jumlahLama = item?.Jumlah ?? 0;
            int jumlahBaru = jumlahLama + jumlah;

            var hasilReservasi =
                await _reservasiStokService.AturJumlahAsync(
                    idMeja,
                    idMenu,
                    HttpContext.Session.Id,
                    jumlahBaru
                );

            if (!hasilReservasi.Berhasil)
            {
                return GagalAtauRedirect(
                    hasilReservasi.Pesan, idMeja);
            }

            if (item == null)
            {
                item = new ItemKeranjangViewModel
                {
                    IdMenu = menu.IdMenu,
                    NamaMenu = menu.NamaMenu,
                    Gambar = menu.Gambar,
                    Harga = menu.Harga,
                    Jumlah = jumlahBaru
                };

                keranjang.Add(item);
            }
            else
            {
                item.Jumlah = jumlahBaru;
                item.Harga = menu.Harga;
                item.NamaMenu = menu.NamaMenu;
                item.Gambar = menu.Gambar;
            }

            SimpanKeranjang(idMeja, keranjang);

            if (ajax)
            {
                var stokTerbaru = await _context.Menu
                    .AsNoTracking()
                    .Where(m => m.IdMenu == idMenu)
                    .Select(m => m.Stok)
                    .FirstOrDefaultAsync();

                return Json(new
                {
                    success = true,
                    message = $"{menu.NamaMenu} berhasil ditambahkan.",
                    idMenu,
                    namaMenu = menu.NamaMenu,
                    jumlah = jumlahBaru,
                    subtotal = item.Harga * item.Jumlah,
                    total = HitungTotal(keranjang),
                    stok = stokTerbaru
                });
            }

            TempData["SuccessMessage"] =
                $"{menu.NamaMenu} berhasil ditambahkan. Stok sudah direservasi.";

            return RedirectToAction(nameof(Index), new { idMeja });
        }

        // Mengubah jumlah menu menggunakan AJAX.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UbahJumlah(
            int idMeja,
            int idMenu,
            int jumlah)
        {
            var keranjang = AmbilKeranjang(idMeja);

            var item = keranjang.FirstOrDefault(
                x => x.IdMenu == idMenu);

            if (item == null)
            {
                return GagalAtauRedirect(
                    "Menu tidak ditemukan di keranjang.", idMeja);
            }

            var menu = await _context.Menu.FindAsync(idMenu);

            if (menu == null)
            {
                return GagalAtauRedirect(
                    "Data menu tidak ditemukan.", idMeja);
            }

            if (jumlah <= 0)
            {
                var hasilHapus =
                    await _reservasiStokService.AturJumlahAsync(
                        idMeja,
                        idMenu,
                        HttpContext.Session.Id,
                        0
                    );

                if (!hasilHapus.Berhasil)
                {
                    return GagalAtauRedirect(
                        hasilHapus.Pesan, idMeja);
                }

                keranjang.Remove(item);
                SimpanKeranjang(idMeja, keranjang);

                if (IsAjaxRequest())
                {
                    var stokTerbaru = await _context.Menu
                        .AsNoTracking()
                        .Where(m => m.IdMenu == idMenu)
                        .Select(m => m.Stok)
                        .FirstOrDefaultAsync();

                    return Json(new
                    {
                        success = true,
                        message = "Menu dihapus dan stok berhasil dikembalikan.",
                        idMenu,
                        removed = true,
                        stok = stokTerbaru,
                        total = HitungTotal(keranjang)
                    });
                }

                TempData["InfoMessage"] =
                    "Menu dihapus dan stok berhasil dikembalikan.";

                return RedirectToAction(nameof(Index), new { idMeja });
            }

            var hasilReservasi =
                await _reservasiStokService.AturJumlahAsync(
                    idMeja,
                    idMenu,
                    HttpContext.Session.Id,
                    jumlah
                );

            if (!hasilReservasi.Berhasil)
            {
                return GagalAtauRedirect(
                    hasilReservasi.Pesan, idMeja);
            }

            item.Jumlah = jumlah;
            item.Harga = menu.Harga;
            item.NamaMenu = menu.NamaMenu;
            item.Gambar = menu.Gambar;

            SimpanKeranjang(idMeja, keranjang);

            if (IsAjaxRequest())
            {
                var stokTerbaru = await _context.Menu
                    .AsNoTracking()
                    .Where(m => m.IdMenu == idMenu)
                    .Select(m => m.Stok)
                    .FirstOrDefaultAsync();

                return Json(new
                {
                    success = true,
                    message = "Jumlah pesanan berhasil diperbarui.",
                    idMenu,
                    jumlah = item.Jumlah,
                    subtotal = item.Harga * item.Jumlah,
                    total = HitungTotal(keranjang),
                    stok = stokTerbaru,
                    removed = false
                });
            }

            TempData["SuccessMessage"] =
                "Jumlah pesanan berhasil diperbarui dan stok disesuaikan.";

            return RedirectToAction(nameof(Index), new { idMeja });
        }

        // Menghapus menu dari keranjang.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hapus(
            int idMeja,
            int idMenu)
        {
            var keranjang = AmbilKeranjang(idMeja);

            var item = keranjang.FirstOrDefault(
                x => x.IdMenu == idMenu);

            if (item == null)
            {
                TempData["ErrorMessage"] =
                    "Menu tidak ditemukan di keranjang.";

                return RedirectToAction(nameof(Index), new { idMeja });
            }

            var hasilReservasi =
                await _reservasiStokService.AturJumlahAsync(
                    idMeja,
                    idMenu,
                    HttpContext.Session.Id,
                    0
                );

            if (!hasilReservasi.Berhasil)
            {
                TempData["ErrorMessage"] = hasilReservasi.Pesan;

                return RedirectToAction(nameof(Index), new { idMeja });
            }

            keranjang.Remove(item);
            SimpanKeranjang(idMeja, keranjang);

            TempData["InfoMessage"] =
                "Menu berhasil dihapus dan stok dikembalikan.";

            return RedirectToAction(nameof(Index), new { idMeja });
        }

        // Menyimpan pesanan tanpa mengurangi stok untuk kedua kalinya.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Simpan(int idMeja)
        {
            var keranjang = AmbilKeranjang(idMeja);

            if (keranjang.Count == 0)
            {
                TempData["ErrorMessage"] = "Keranjang masih kosong.";
                return RedirectToAction(nameof(Index), new { idMeja });
            }

            var claimIdUser =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(claimIdUser, out int idUser))
            {
                TempData["ErrorMessage"] =
                    "Identitas pengguna tidak valid. Silakan login kembali.";

                return RedirectToAction("Login", "Account");
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var meja = await _context.Meja
                    .FirstOrDefaultAsync(m => m.IdMeja == idMeja);

                if (meja == null)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "Meja tidak ditemukan.";
                    return RedirectToAction(nameof(Index));
                }

                if (meja.StatusMeja != "Tersedia" &&
                    meja.StatusMeja != "Digunakan")
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Status meja tidak memungkinkan pemesanan.";

                    return RedirectToAction(nameof(Index), new { idMeja });
                }

                string sessionId = HttpContext.Session.Id;
                var sekarang = DateTime.Now;

                var daftarReservasi = await _context.ReservasiStok
                    .Where(r =>
                        r.IdMeja == idMeja &&
                        r.SessionId == sessionId)
                    .ToListAsync();

                foreach (var item in keranjang)
                {
                    var reservasi = daftarReservasi.FirstOrDefault(r =>
                        r.IdMenu == item.IdMenu);

                    if (reservasi == null ||
                        reservasi.Jumlah != item.Jumlah ||
                        reservasi.WaktuKedaluwarsa <= sekarang)
                    {
                        await transaction.RollbackAsync();

                        TempData["ErrorMessage"] =
                            $"Reservasi stok untuk {item.NamaMenu} sudah tidak berlaku. Silakan tambahkan kembali menu tersebut.";

                        return RedirectToAction(nameof(Index), new { idMeja });
                    }
                }

                var idMenuList = keranjang
                    .Select(x => x.IdMenu)
                    .ToList();

                var daftarMenu = await _context.Menu
                    .Where(m => idMenuList.Contains(m.IdMenu))
                    .ToListAsync();

                foreach (var item in keranjang)
                {
                    var menu = daftarMenu.FirstOrDefault(
                        m => m.IdMenu == item.IdMenu);

                    if (menu == null || !menu.StatusTersedia)
                    {
                        await transaction.RollbackAsync();

                        TempData["ErrorMessage"] =
                            $"Menu {item.NamaMenu} sudah tidak tersedia.";

                        return RedirectToAction(nameof(Index), new { idMeja });
                    }
                }

                var pesanan = await _context.Pesanan
                    .FirstOrDefaultAsync(p =>
                        p.IdMeja == idMeja &&
                        p.StatusPesanan == "Diproses");

                if (pesanan == null)
                {
                    pesanan = new Pesanan
                    {
                        IdMeja = idMeja,
                        IdUser = idUser,
                        TanggalPesanan = DateTime.Now,
                        StatusPesanan = "Diproses",
                        Total = 0
                    };

                    _context.Pesanan.Add(pesanan);
                    await _context.SaveChangesAsync();
                }

                foreach (var item in keranjang)
                {
                    var menu = daftarMenu.First(
                        m => m.IdMenu == item.IdMenu);

                    var detail = await _context.DetailPesanan
                        .FirstOrDefaultAsync(d =>
                            d.IdPesanan == pesanan.IdPesanan &&
                            d.IdMenu == menu.IdMenu);

                    if (detail == null)
                    {
                        detail = new DetailPesanan
                        {
                            IdPesanan = pesanan.IdPesanan,
                            IdMenu = menu.IdMenu,
                            Jumlah = item.Jumlah,
                            HargaSatuan = menu.Harga,
                            Subtotal = menu.Harga * item.Jumlah
                        };

                        _context.DetailPesanan.Add(detail);
                    }
                    else
                    {
                        detail.Jumlah += item.Jumlah;
                        detail.HargaSatuan = menu.Harga;
                        detail.Subtotal =
                            detail.HargaSatuan * detail.Jumlah;
                    }
                }

                await _context.SaveChangesAsync();

                pesanan.Total = await _context.DetailPesanan
                    .Where(d => d.IdPesanan == pesanan.IdPesanan)
                    .SumAsync(d => (decimal?)d.Subtotal) ?? 0;

                if (meja.StatusMeja == "Tersedia")
                {
                    meja.StatusMeja = "Digunakan";
                    meja.MulaiDigunakan = DateTime.Now;
                }

                _context.ReservasiStok.RemoveRange(daftarReservasi);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                HapusKeranjang(idMeja);

                TempData["SuccessMessage"] =
                    $"Pesanan Meja {meja.NomorMeja} berhasil disimpan. Total sementara: Rp{pesanan.Total:N0}.";

                return RedirectToAction(nameof(Index), new { idMeja });
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Pesanan gagal disimpan. Silakan periksa kembali dan coba lagi.";

                return RedirectToAction(nameof(Index), new { idMeja });
            }
        }

        // Memeriksa permintaan AJAX.
        private bool IsAjaxRequest()
        {
            return string.Equals(
                Request.Headers["X-Requested-With"],
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase
            );
        }

        // Respons gagal untuk AJAX atau form biasa.
        private IActionResult GagalAtauRedirect(
            string pesan,
            int idMeja)
        {
            if (IsAjaxRequest())
            {
                return Json(new
                {
                    success = false,
                    message = pesan
                });
            }

            TempData["ErrorMessage"] = pesan;

            return RedirectToAction(nameof(Index), new { idMeja });
        }

        // Menghitung total keranjang.
        private decimal HitungTotal(
            List<ItemKeranjangViewModel> keranjang)
        {
            return keranjang.Sum(x => x.Harga * x.Jumlah);
        }

        // Mengambil keranjang dari Session.
        private List<ItemKeranjangViewModel> AmbilKeranjang(int idMeja)
        {
            string? json = HttpContext.Session.GetString(
                $"KeranjangMeja_{idMeja}");

            if (string.IsNullOrEmpty(json))
            {
                return new List<ItemKeranjangViewModel>();
            }

            return JsonSerializer.Deserialize<List<ItemKeranjangViewModel>>(json)
                ?? new List<ItemKeranjangViewModel>();
        }

        // Menyimpan keranjang ke Session.
        private void SimpanKeranjang(
            int idMeja,
            List<ItemKeranjangViewModel> keranjang)
        {
            string json = JsonSerializer.Serialize(keranjang);

            HttpContext.Session.SetString(
                $"KeranjangMeja_{idMeja}",
                json);
        }

        // Menghapus keranjang dari Session.
        private void HapusKeranjang(int idMeja)
        {
            HttpContext.Session.Remove($"KeranjangMeja_{idMeja}");
        }
    }
}
