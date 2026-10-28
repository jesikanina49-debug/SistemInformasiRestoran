
using Microsoft.AspNetCore.Mvc;
using SistemInformasiRestoran.Data;

namespace SistemInformasiRestoran.Controllers
{
    public class DatabaseController : Controller
    {
        private readonly RestoDbContext _context;

        public DatabaseController(RestoDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                bool terhubung =
                    await _context.Database.CanConnectAsync();

                if (terhubung)
                {
                    return Content("Koneksi database resto_pos berhasil!");
                }

                return Content("Koneksi gagal. Periksa MySQL dan pengaturan database.");
            }
            catch (Exception ex)
            {
                return Content("Koneksi gagal: " + ex.Message);
            }
        }
    }
}
