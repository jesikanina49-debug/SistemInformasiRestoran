
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
                ViewBag.Error = "Username dan password wajib diisi.";
                return View();
            }

            var pengguna = await _context.Pengguna
                .SingleOrDefaultAsync(
                    u => u.Username == username.Trim());

            if (pengguna == null ||
                string.IsNullOrWhiteSpace(pengguna.PasswordHash))
            {
                ViewBag.Error = "Username atau password salah.";
                return View();
            }

            var hasil = _passwordHasher.VerifyHashedPassword(
                pengguna,
                pengguna.PasswordHash,
                password);

            if (hasil == PasswordVerificationResult.Failed)
            {
                ViewBag.Error = "Username atau password salah.";
                return View();
            }

            if (pengguna.Role != "Kasir" &&
                pengguna.Role != "Manager")
            {
                ViewBag.Error = "Peran akun tidak valid.";
                return View();
            }

            if (hasil == PasswordVerificationResult.SuccessRehashNeeded)
            {
                pengguna.PasswordHash =
                    _passwordHasher.HashPassword(pengguna, password);

                await _context.SaveChangesAsync();
            }

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

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            TempData["SuccessMessage"] =
                "Kamu berhasil logout dari RESTO POS.";

            return RedirectToAction("Login", "Account");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return Content(
                "Akses ditolak. Kamu tidak memiliki izin " +
                "untuk membuka halaman ini.");
        }

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

            if (nama.Trim().Length > 100 ||
                username.Trim().Length > 50)
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
                NamaUser = nama.Trim(),
                Username = username.Trim(),
                Role = "Manager"
            };

            pengguna.PasswordHash =
                _passwordHasher.HashPassword(pengguna, password);

            _context.Pengguna.Add(pengguna);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
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
