
using Microsoft.EntityFrameworkCore;
using SistemInformasiRestoran.Models;

namespace SistemInformasiRestoran.Data
{
    public class RestoDbContext : DbContext
    {
        public RestoDbContext(DbContextOptions<RestoDbContext> options)
            : base(options)
        {
        }

        public DbSet<Kategori> Kategori { get; set; }
        public DbSet<Menu> Menu { get; set; }
        public DbSet<Meja> Meja { get; set; }
        public DbSet<Pengguna> Pengguna { get; set; }
        public DbSet<Pesanan> Pesanan { get; set; }
        public DbSet<DetailPesanan> DetailPesanan { get; set; }
        public DbSet<Pembayaran> Pembayaran { get; set; }
    }
}
