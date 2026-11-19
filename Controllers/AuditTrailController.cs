
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemInformasiRestoran.Data;

namespace SistemInformasiRestoran.Controllers
{
    [Authorize(Roles = "Manager")]
    public class AuditTrailController : Controller
    {
        private readonly RestoDbContext _context;

        public AuditTrailController(RestoDbContext context)
        {
            _context = context;
        }

        // Menampilkan riwayat aktivitas.
        public async Task<IActionResult> Index(
            string? pencarian,
            DateTime? tanggalMulai,
            DateTime? tanggalAkhir)
        {
            var query = _context.AuditTrail
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(pencarian))
            {
                query = query.Where(a =>
                    a.NamaUser.Contains(pencarian) ||
                    a.Aktivitas.Contains(pencarian) ||
                    a.NamaTabel.Contains(pencarian) ||
                    (a.Keterangan != null &&
                     a.Keterangan.Contains(pencarian)));
            }

            if (tanggalMulai.HasValue)
            {
                DateTime mulai = tanggalMulai.Value.Date;

                query = query.Where(a =>
                    a.TanggalAktivitas >= mulai);
            }

            if (tanggalAkhir.HasValue)
            {
                DateTime batasAkhir =
                    tanggalAkhir.Value.Date.AddDays(1);

                query = query.Where(a =>
                    a.TanggalAktivitas < batasAkhir);
            }

            var daftarAudit = await query
                .OrderByDescending(a => a.TanggalAktivitas)
                .Take(500)
                .ToListAsync();

            ViewBag.Pencarian = pencarian;
            ViewBag.TanggalMulai = tanggalMulai?.ToString("yyyy-MM-dd");
            ViewBag.TanggalAkhir = tanggalAkhir?.ToString("yyyy-MM-dd");

            return View(daftarAudit);
        }
    }
}
