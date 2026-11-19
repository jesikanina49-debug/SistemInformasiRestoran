
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SistemInformasiRestoran.Data;
using SistemInformasiRestoran.Models;

namespace SistemInformasiRestoran.Services
{
    public class ReservasiStokService
    {
        private readonly RestoDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ReservasiStokService(
            RestoDbContext context,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        // Batas waktu reservasi keranjang.
        public static readonly TimeSpan BatasWaktu =
            TimeSpan.FromMinutes(30);

        // Membuat catatan Audit Trail untuk perubahan stok oleh pengguna.
        private AuditTrail BuatAuditStok(
            int idMenu,
            string aktivitas,
            string keterangan)
        {
            var pengguna = _httpContextAccessor.HttpContext?.User;

            var idUser = pengguna?
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return new AuditTrail
            {
                IdUser = int.TryParse(idUser, out int hasil)
                    ? hasil
                    : null,

                NamaUser = pengguna?.Identity?.Name ?? "SYSTEM",
                Aktivitas = aktivitas,
                NamaTabel = "menu",
                IdData = idMenu,
                Keterangan = keterangan,
                TanggalAktivitas = DateTime.Now
            };
        }

        // Menyesuaikan jumlah reservasi stok.
        public async Task<(bool Berhasil, string Pesan)> AturJumlahAsync(
            int idMeja,
            int idMenu,
            string sessionId,
            int jumlahBaru)
        {
            if (jumlahBaru < 0)
            {
                return (false, "Jumlah tidak valid.");
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var menu = await _context.Menu
                    .FirstOrDefaultAsync(m => m.IdMenu == idMenu);

                if (menu == null)
                {
                    await transaction.RollbackAsync();
                    return (false, "Menu tidak ditemukan.");
                }

                var reservasi = await _context.ReservasiStok
                    .FirstOrDefaultAsync(r =>
                        r.IdMeja == idMeja &&
                        r.IdMenu == idMenu &&
                        r.SessionId == sessionId);

                int jumlahLama = reservasi?.Jumlah ?? 0;
                int selisih = jumlahBaru - jumlahLama;

                // Jika jumlah bertambah, periksa status menu dan stok.
                if (selisih > 0)
                {
                    if (menu.IsDeleted)
                    {
                        await transaction.RollbackAsync();
                        return (false, "Menu sudah dihapus dan tidak dapat dipesan.");
                    }

                    if (!menu.StatusTersedia)
                    {
                        await transaction.RollbackAsync();
                        return (false, "Menu sedang dinonaktifkan.");
                    }

                    if (menu.Stok < selisih)
                    {
                        await transaction.RollbackAsync();

                        return (
                            false,
                            $"Stok {menu.NamaMenu} tidak mencukupi. Stok tersedia: {menu.Stok}."
                        );
                    }

                    menu.Stok -= selisih;

                    if (reservasi == null)
                    {
                        reservasi = new ReservasiStok
                        {
                            IdMeja = idMeja,
                            IdMenu = idMenu,
                            SessionId = sessionId,
                            Jumlah = jumlahBaru,
                            WaktuReservasi = DateTime.Now,
                            WaktuKedaluwarsa =
                                DateTime.Now.Add(BatasWaktu)
                        };

                        _context.ReservasiStok.Add(reservasi);
                    }
                    else
                    {
                        reservasi.Jumlah = jumlahBaru;
                        reservasi.WaktuReservasi = DateTime.Now;
                        reservasi.WaktuKedaluwarsa =
                            DateTime.Now.Add(BatasWaktu);
                    }

                    _context.AuditTrail.Add(BuatAuditStok(
                        menu.IdMenu,
                        "Reservasi Stok",
                        $"Stok {menu.NamaMenu} dikurangi sebanyak {selisih} untuk reservasi meja {idMeja}."
                    ));
                }
                // Jika jumlah berkurang, kembalikan stok.
                else if (selisih < 0)
                {
                    int jumlahDikembalikan = Math.Abs(selisih);

                    menu.Stok += jumlahDikembalikan;

                    if (reservasi != null)
                    {
                        reservasi.Jumlah = jumlahBaru;
                    }

                    _context.AuditTrail.Add(BuatAuditStok(
                        menu.IdMenu,
                        "Pengembalian Stok",
                        $"Stok {menu.NamaMenu} dikembalikan sebanyak {jumlahDikembalikan} karena jumlah reservasi meja {idMeja} dikurangi."
                    ));
                }

                // Jika jumlah menjadi nol, hapus reservasi.
                if (jumlahBaru == 0 && reservasi != null)
                {
                    _context.ReservasiStok.Remove(reservasi);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, "Reservasi stok berhasil diperbarui.");
            }
            catch
            {
                await transaction.RollbackAsync();

                return (
                    false,
                    "Gagal memperbarui reservasi stok."
                );
            }
        }

        // Menghapus reservasi setelah pesanan berhasil disimpan.
        // Stok tidak dikembalikan karena sudah menjadi transaksi pesanan.
        public async Task KonfirmasiAsync(
            int idMeja,
            string sessionId)
        {
            var daftarReservasi = await _context.ReservasiStok
                .Where(r =>
                    r.IdMeja == idMeja &&
                    r.SessionId == sessionId)
                .ToListAsync();

            _context.ReservasiStok.RemoveRange(daftarReservasi);

            await _context.SaveChangesAsync();
        }

        // Mengembalikan stok dari reservasi yang kedaluwarsa.
        public async Task<int> KembalikanKedaluwarsaAsync()
        {
            var sekarang = DateTime.Now;

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var daftarReservasi = await _context.ReservasiStok
                    .Where(r => r.WaktuKedaluwarsa <= sekarang)
                    .ToListAsync();

                if (daftarReservasi.Count == 0)
                {
                    await transaction.CommitAsync();
                    return 0;
                }

                foreach (var kelompok in daftarReservasi
                    .GroupBy(r => r.IdMenu))
                {
                    var menu = await _context.Menu
                        .FirstOrDefaultAsync(m =>
                            m.IdMenu == kelompok.Key);

                    if (menu != null)
                    {
                        int jumlahDikembalikan =
                            kelompok.Sum(r => r.Jumlah);

                        menu.Stok += jumlahDikembalikan;

                        // Aktivitas otomatis dicatat sebagai SYSTEM.
                        _context.AuditTrail.Add(new AuditTrail
                        {
                            IdUser = null,
                            NamaUser = "SYSTEM",
                            Aktivitas = "Pengembalian Stok",
                            NamaTabel = "menu",
                            IdData = menu.IdMenu,
                            Keterangan =
                                $"Stok {menu.NamaMenu} dikembalikan sebanyak {jumlahDikembalikan} karena reservasi kedaluwarsa.",
                            TanggalAktivitas = DateTime.Now
                        });
                    }
                }

                int jumlahDikembalikanReservasi =
                    daftarReservasi.Count;

                _context.ReservasiStok.RemoveRange(daftarReservasi);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return jumlahDikembalikanReservasi;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
