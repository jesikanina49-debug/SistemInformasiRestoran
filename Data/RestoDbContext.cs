
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SistemInformasiRestoran.Models;

namespace SistemInformasiRestoran.Data
{
    public class RestoDbContext : DbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public RestoDbContext(
            DbContextOptions<RestoDbContext> options,
            IHttpContextAccessor httpContextAccessor)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<Kategori> Kategori { get; set; }
        public DbSet<Menu> Menu { get; set; }
        public DbSet<Meja> Meja { get; set; }
        public DbSet<Pengguna> Pengguna { get; set; }
        public DbSet<Pesanan> Pesanan { get; set; }
        public DbSet<DetailPesanan> DetailPesanan { get; set; }
        public DbSet<Pembayaran> Pembayaran { get; set; }
        public DbSet<ReservasiStok> ReservasiStok { get; set; }
        public DbSet<AuditTrail> AuditTrail { get; set; }

        // Mengambil ID pengguna yang sedang login.
        private int? AmbilIdUser()
        {
            var idUser = _httpContextAccessor.HttpContext?
                .User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(idUser, out int hasil)
                ? hasil
                : null;
        }

        // Mengisi kolom audit secara otomatis.
        private void IsiKolomAudit()
        {
            var idUser = AmbilIdUser();
            var waktuSekarang = DateTime.Now;

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added &&
                    entry.State != EntityState.Modified)
                {
                    continue;
                }

                // Pastikan entitas mempunyai kolom InputDate.
                if (entry.Metadata.FindProperty("InputDate") == null)
                {
                    continue;
                }

                if (entry.State == EntityState.Added)
                {
                    // Mengisi InputBy jika kolom tersedia.
                    if (idUser.HasValue &&
                        entry.Metadata.FindProperty("InputBy") != null)
                    {
                        entry.Property("InputBy").CurrentValue = idUser.Value;
                    }

                    entry.Property("InputDate").CurrentValue = waktuSekarang;
                }
                else if (entry.State == EntityState.Modified)
                {
                    // Mengisi UpdateBy jika kolom tersedia.
                    if (entry.Metadata.FindProperty("UpdateBy") != null)
                    {
                        entry.Property("UpdateBy").CurrentValue = idUser;
                    }

                    // Mengisi UpdateDate jika kolom tersedia.
                    if (entry.Metadata.FindProperty("UpdateDate") != null)
                    {
                        entry.Property("UpdateDate").CurrentValue = waktuSekarang;
                    }

                    // Mempertahankan data input awal.
                    if (entry.Metadata.FindProperty("InputBy") != null)
                    {
                        entry.Property("InputBy").IsModified = false;
                    }

                    entry.Property("InputDate").IsModified = false;
                }
            }
        }

        // Menyimpan perubahan secara synchronous.
        public override int SaveChanges(
            bool acceptAllChangesOnSuccess)
        {
            IsiKolomAudit();

            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        // Menyimpan perubahan secara asynchronous.
        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            IsiKolomAudit();

            return base.SaveChangesAsync(
                acceptAllChangesOnSuccess,
                cancellationToken);
        }
    }
}
