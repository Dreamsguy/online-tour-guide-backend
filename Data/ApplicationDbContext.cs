using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using OnlineTourGuide.Models;
using System.Text.Json;

namespace OnlineTourGuide.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Excursion> Excursions { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Attraction> Attractions { get; set; }
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<RoleRequest> RoleRequests { get; set; }
        public DbSet<Schedule> Schedules { get; set; }
        public DbSet<UserAction> UserActions { get; set; }
        public DbSet<UserPreference> UserPreferences { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Игнорирование неиспользуемых свойств геометрии
            modelBuilder.Entity<Point>().HasNoKey().Ignore(p => p.UserData).Ignore(p => p.Coordinates);
            modelBuilder.Entity<Coordinate>().HasNoKey().Ignore(c => c.CoordinateValue).Ignore(c => c.Z).Ignore(c => c.M);

            modelBuilder.Entity<UserAction>(entity =>
            {
                entity.Property(e => e.ActionType).HasConversion<string>();
            });

            modelBuilder.Entity<Excursion>()
                .Property(e => e.TicketsJson)
                .HasColumnName("Tickets")
                .HasColumnType("json")
                .HasDefaultValueSql("'[]'");

            modelBuilder.Entity<Excursion>()
                .Property(e => e.ImagesJson)
                .HasColumnName("Images")
                .HasColumnType("json")
                .HasDefaultValueSql("'[]'");

            modelBuilder.Entity<Excursion>()
                .Property(e => e.RouteJson)
                .HasColumnName("Route")
                .HasColumnType("json")
                .HasDefaultValueSql("'[]'");

            modelBuilder.Entity<Excursion>()
                .HasOne(e => e.Guide)
                .WithMany(u => u.GuidedExcursions)
                .HasForeignKey(e => e.GuideId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Excursion>()
                .HasOne(e => e.Manager)
                .WithMany(u => u.ManagedExcursions)
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Excursion>()
                .HasOne(e => e.Organization)
                .WithMany(o => o.Excursions)
                .HasForeignKey(e => e.OrganizationId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // Убираем .IsRequired(), так как база допускает NULL, и используем значения по умолчанию в коде
            modelBuilder.Entity<Excursion>()
                .Property(e => e.Title)
                .HasMaxLength(255)
                .HasDefaultValue("Без названия");

            modelBuilder.Entity<Excursion>()
                .Property(e => e.Description)
                .HasDefaultValue("Без описания");

            modelBuilder.Entity<Excursion>()
                .Property(e => e.City)
                .HasMaxLength(100)
                .HasDefaultValue("Не указан");

            modelBuilder.Entity<Excursion>()
                .Property(e => e.IsIndividual)
                .HasDefaultValue(false);

            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>()
                .HasColumnType("enum('user','guide','manager','admin')");

            modelBuilder.Entity<User>()
                .HasOne(u => u.Organization)
                .WithMany(o => o.Users)
                .HasForeignKey(u => u.OrganizationId)
                .HasConstraintName("FK_Users_Organizations")
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<User>()
                .Property(u => u.OrganizationId)
                .HasColumnName("OrganizationId");

            // Настройка отношения многие-ко-многим с указанием правильного имени таблицы
            modelBuilder.Entity<Excursion>()
                .HasMany(e => e.Attractions)
                .WithMany(a => a.Excursions)
                .UsingEntity<Dictionary<string, object>>(
                    "excursion_attractions", // Указываем правильное имя таблицы
                    j => j
                        .HasOne<Attraction>()
                        .WithMany()
                        .HasForeignKey("AttractionId")
                        .HasPrincipalKey(a => a.Id)
                        .OnDelete(DeleteBehavior.Cascade),
                    j => j
                        .HasOne<Excursion>()
                        .WithMany()
                        .HasForeignKey("ExcursionId")
                        .HasPrincipalKey(e => e.Id)
                        .OnDelete(DeleteBehavior.Cascade)
                );

            modelBuilder.Entity<Excursion>()
                .HasMany(e => e.Reviews)
                .WithOne(r => r.Excursion)
                .HasForeignKey(r => r.ExcursionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RoleRequest>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            modelBuilder.Entity<Attraction>()
                .Property(a => a.Coordinates)
                .HasConversion(
                    v => v != null ? new WKBWriter().Write(v) : null,
                    v => v != null ? new WKBReader().Read(v) as Point : null)
                .HasColumnType("geometry");

            modelBuilder.Entity<Schedule>()
                .HasOne(s => s.Guide)
                .WithMany()
                .HasForeignKey(s => s.GuideId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Schedule>()
                .HasOne(s => s.Excursion)
                .WithMany()
                .HasForeignKey(s => s.ExcursionId)
                .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
        }
    }

}