
using Microsoft.EntityFrameworkCore;
using SistemInformasiRestoran.Data;
using SistemInformasiRestoran.Models;

namespace SistemInformasiRestoran.Services
{
    public class ReservasiStokService
    {
        private readonly RestoDbContext _context;

        public ReservasiStokService(RestoDbContext context)
        {
            _context = context;
        }

        // Batas waktu reservasi keranjang.
        public static readonly TimeSpan BatasWaktu =
            TimeSpan.FromMinutes(30);

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

                // Jika jumlah bertambah, kurangi stok.
                if (selisih > 0)
                {
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
                }
                // Jika jumlah berkurang, kembalikan stok.
                else if (selisih < 0)
                {
                    menu.Stok += Math.Abs(selisih);

                    if (reservasi != null)
                    {
                        reservasi.Jumlah = jumlahBaru;
                    }
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
                        menu.Stok += kelompok.Sum(r => r.Jumlah);
                    }
                }

                int jumlahDikembalikan = daftarReservasi.Count;

                _context.ReservasiStok.RemoveRange(daftarReservasi);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return jumlahDikembalikan;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
