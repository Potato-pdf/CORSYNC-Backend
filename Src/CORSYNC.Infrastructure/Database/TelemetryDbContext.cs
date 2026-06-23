using Microsoft.EntityFrameworkCore;
using CORSYNC.Core.Domain;

namespace CORSYNC.Infrastructure.Database
{
    public class TelemetryDbContext : DbContext
    {
        public TelemetryDbContext(DbContextOptions<TelemetryDbContext> options) : base(options)
        {
        }

        public DbSet<LecturaCorazon> LecturasCorazon { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<LecturaCorazon>(entity =>
            {
                entity.ToTable("LecturasCorazon");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DispositivoId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.BPM).HasColumnType("decimal(5,1)");
            });
        }
    }
}
