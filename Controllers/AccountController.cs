
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemInformasiRestoran.Data;
using SistemInformasiRestoran.Models;

namespace SistemInformasiRestoran.Controllers
{
    public class AccountController : Controller
    {
        private readonly RestoDbContext _context;
        private readonly IPasswordHasher<Pengguna> _passwordHasher;
        private readonly IWebHostEnvironment _environment;

        public AccountController(
            RestoDbContext context,
            IPasswordHasher<Pengguna> passwordHasher,
            IWebHostEnvironment environment)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _environment = environment;
        }

        // Membuat catatan Audit Trail.
        private AuditTrail BuatAudit(
            string aktivitas,
            string namaTabel,
            int? idData,
            string keterangan,
            int? idUser = null,
            string? namaUser = null)
        {
            var idDariLogin =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            int? idAktif = idUser;

            if (idAktif == null &&
                int.TryParse(idDariLogin, out int hasil))
            {
                idAktif = hasil;
            }

            return new AuditTrail
            {
                IdUser = idAktif,
                NamaUser = namaUser ??
                    User.Identity?.Name ??
                    "SYSTEM",
                Aktivitas = aktivitas,
                NamaTabel = namaTabel,
                IdData = idData,
                Keterangan = keterangan,
                TanggalAktivitas = DateTime.Now
            };
        }

        // =========================
        // LOGIN
        // =========================

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string username,
            string password)
        {
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error =
                    "Username dan password wajib diisi.";

                return View();
            }

            var pengguna = await _context.Pengguna
                .SingleOrDefaultAsync(u =>
                    u.Username == username.Trim() &&
                    !u.IsDeleted);

            if (pengguna == null ||
                string.IsNullOrWhiteSpace(pengguna.PasswordHash))
            {
                ViewBag.Error =
                    "Username atau password salah.";

                return View();
            }

            var hasil = _passwordHasher.VerifyHashedPassword(
                pengguna,
                pengguna.PasswordHash,
                password);

            if (hasil == PasswordVerificationResult.Failed)
            {
                ViewBag.Error =
                    "Username atau password salah.";

                return View();
            }

            if (pengguna.Role != "Kasir" &&
                pengguna.Role != "Manager")
            {
                ViewBag.Error = "Peran akun tidak valid.";
                return View();
            }

            await using var transaksi =
                await _context.Database.BeginTransactionAsync();

            if (hasil ==
                PasswordVerificationResult.SuccessRehashNeeded)
            {
                pengguna.PasswordHash =
                    _passwordHasher.HashPassword(
                        pengguna,
                        password);

                await _context.SaveChangesAsync();
            }

            _context.AuditTrail.Add(BuatAudit(
                "Login",
                "user",
                pengguna.IdUser,
                $"Pengguna {pengguna.Username} berhasil login.",
                pengguna.IdUser,
                pengguna.Username
            ));

            await _context.SaveChangesAsync();
            await transaksi.CommitAsync();

            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    pengguna.IdUser.ToString()),

                new Claim(
                    ClaimTypes.Name,
                    pengguna.Username),

                new Claim(
                    ClaimTypes.Role,
                    pengguna.Role)
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal);

            TempData["SuccessMessage"] =
                $"Selamat datang, {pengguna.NamaUser}! Login berhasil.";

            return RedirectToAction("Index", "Home");
        }

        // =========================
        // LOGOUT
        // =========================

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var idUser = int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out int hasil) ? hasil : (int?)null;

            var namaUser = User.Identity?.Name ?? "SYSTEM";

            _context.AuditTrail.Add(BuatAudit(
                "Logout",
                "user",
                idUser,
                $"Pengguna {namaUser} berhasil logout.",
                idUser,
                namaUser
            ));

            await _context.SaveChangesAsync();

            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            TempData["SuccessMessage"] =
                "Kamu berhasil logout dari RESTO POS.";

            return RedirectToAction("Login", "Account");
        }

        // =========================
        // ACCESS DENIED
        // =========================

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return Content(
                "Akses ditolak. Kamu tidak memiliki izin " +
                "untuk membuka halaman ini.");
        }

        // =========================
        // TAMBAH AKUN
        // KHUSUS MANAGER
        // =========================

        [Authorize(Roles = "Manager")]
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string nama,
            string username,
            string password,
            string konfirmasiPassword,
            string role)
        {
            if (string.IsNullOrWhiteSpace(nama) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(konfirmasiPassword) ||
                string.IsNullOrWhiteSpace(role))
            {
                ViewBag.Error = "Semua kolom wajib diisi.";
                return View();
            }

            nama = nama.Trim();
            username = username.Trim();

            if (nama.Length > 100 || username.Length > 50)
            {
                ViewBag.Error =
                    "Nama maksimal 100 karakter dan username maksimal 50 karakter.";

                return View();
            }

            if (role != "Manager" && role != "Kasir")
            {
                ViewBag.Error = "Role akun tidak valid.";
                return View();
            }

            if (password.Length < 12)
            {
                ViewBag.Error =
                    "Password minimal 12 karakter.";

                return View();
            }

            if (password != konfirmasiPassword)
            {
                ViewBag.Error =
                    "Konfirmasi password tidak sama.";

                return View();
            }

            bool usernameSudahAda = await _context.Pengguna
                .AnyAsync(u => u.Username == username);

            if (usernameSudahAda)
            {
                ViewBag.Error =
                    "Username sudah digunakan.";

                return View();
            }

            var penggunaBaru = new Pengguna
            {
                NamaUser = nama,
                Username = username,
                Role = role,
                IsDeleted = false
            };

            penggunaBaru.PasswordHash =
                _passwordHasher.HashPassword(
                    penggunaBaru,
                    password);

            await using var transaksi =
                await _context.Database.BeginTransactionAsync();

            _context.Pengguna.Add(penggunaBaru);

            try
            {
                await _context.SaveChangesAsync();

                _context.AuditTrail.Add(BuatAudit(
                    "Tambah Akun",
                    "user",
                    penggunaBaru.IdUser,
                    $"Manager menambahkan akun {username} dengan role {role}."
                ));

                await _context.SaveChangesAsync();
                await transaksi.CommitAsync();
            }
            catch (DbUpdateException)
            {
                await transaksi.RollbackAsync();

                _context.Entry(penggunaBaru).State =
                    EntityState.Detached;

                ViewBag.Error =
                    "Akun gagal dibuat. Username mungkin sudah digunakan.";

                return View();
            }

            TempData["SuccessMessage"] =
                $"Akun {nama} berhasil ditambahkan.";

            return RedirectToAction(nameof(Create));
        }

        // =========================
        // SETUP MANAGER PERTAMA
        // =========================

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> SetupManager()
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            if (await _context.Pengguna.AnyAsync())
            {
                return Content(
                    "Setup ditutup karena akun pengguna sudah tersedia.");
            }

            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetupManager(
            string nama,
            string username,
            string password,
            string konfirmasiPassword)
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            if (await _context.Pengguna.AnyAsync())
            {
                return Content(
                    "Setup ditutup karena akun pengguna sudah tersedia.");
            }

            if (string.IsNullOrWhiteSpace(nama) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(konfirmasiPassword))
            {
                ViewBag.Error = "Semua kolom wajib diisi.";
                return View();
            }

            nama = nama.Trim();
            username = username.Trim();

            if (nama.Length > 100 || username.Length > 50)
            {
                ViewBag.Error =
                    "Nama maksimal 100 karakter dan username maksimal 50 karakter.";

                return View();
            }

            if (password.Length < 12)
            {
                ViewBag.Error =
                    "Password minimal 12 karakter.";

                return View();
            }

            if (password != konfirmasiPassword)
            {
                ViewBag.Error =
                    "Konfirmasi password tidak sama.";

                return View();
            }

            var pengguna = new Pengguna
            {
                NamaUser = nama,
                Username = username,
                Role = "Manager",
                IsDeleted = false
            };

            pengguna.PasswordHash =
                _passwordHasher.HashPassword(
                    pengguna,
                    password);

            await using var transaksi =
                await _context.Database.BeginTransactionAsync();

            _context.Pengguna.Add(pengguna);

            try
            {
                await _context.SaveChangesAsync();

                _context.AuditTrail.Add(BuatAudit(
                    "Setup Manager",
                    "user",
                    pengguna.IdUser,
                    $"Membuat akun Manager pertama: {username}.",
                    pengguna.IdUser,
                    username
                ));

                await _context.SaveChangesAsync();
                await transaksi.CommitAsync();
            }
            catch (DbUpdateException)
            {
                await transaksi.RollbackAsync();

                _context.Entry(pengguna).State =
                    EntityState.Detached;

                ViewBag.Error =
                    "Akun gagal dibuat. Username mungkin sudah digunakan.";

                return View();
            }

            return Content(
                "Akun Manager berhasil dibuat. " +
                "Silakan masuk melalui halaman login.");
        }
    }
}
