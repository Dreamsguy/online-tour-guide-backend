using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using OnlineTourGuide.Models;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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
        public DbSet<ExcursionAvailability> ExcursionAvailability { get; set; }
        public DbSet<Schedule> Schedules { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Игнорирование неиспользуемых свойств геометрии
            modelBuilder.Entity<Point>().HasNoKey().Ignore(p => p.UserData).Ignore(p => p.Coordinates);
            modelBuilder.Entity<Coordinate>().HasNoKey().Ignore(c => c.CoordinateValue).Ignore(c => c.Z).Ignore(c => c.M);

            // Игнорирование вычисляемого поля
            modelBuilder.Entity<Excursion>()
                .Ignore(e => e.AvailableTicketsByDate);

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
                .IsRequired() // Оставляем обязательным, если нужно
                .OnDelete(DeleteBehavior.Cascade); // Каскадное удаление

            // Обязательные поля с значениями по умолчанию
            modelBuilder.Entity<Excursion>()
                .Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValue("Без названия");

            modelBuilder.Entity<Excursion>()
                .Property(e => e.Description)
                .IsRequired()
                .HasDefaultValue("Без описания");

            modelBuilder.Entity<Excursion>()
                .Property(e => e.City)
                .IsRequired()
                .HasMaxLength(100)
                .HasDefaultValue("Не указан");

            modelBuilder.Entity<Excursion>()
                .Property(e => e.Image)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValue("default_image.jpg");

            modelBuilder.Entity<Excursion>()
                .Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("pending");

            modelBuilder.Entity<Excursion>()
                .Property(e => e.IsIndividual)
                .HasDefaultValue(false);

            // Настройка пользователя
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>()
                .HasColumnType("enum('user','guide','manager','admin')");

            modelBuilder.Entity<User>()
                .HasOne(u => u.Organization)
                .WithMany(o => o.Users) // Указываем коллекцию для Organizations
                .HasForeignKey(u => u.OrganizationId)
                .HasConstraintName("FK_Users_Organizations") // Явное имя ограничения
                .IsRequired(false) // Организация не обязательна
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<User>()
                .Property(u => u.OrganizationId)
                .HasColumnName("OrganizationId"); // Явное указание имени столбца

            // Многозначные отношения
            modelBuilder.Entity<Excursion>()
                .HasMany(e => e.Attractions)
                .WithMany(a => a.Excursions)
                .UsingEntity<Dictionary<string, object>>(
                    "excursion_attractions",
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

            // Настройка геометрии для Attraction
            var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            modelBuilder.Entity<Attraction>()
                .Property(a => a.Coordinates)
                .HasConversion(
                    v => v != null ? new WKBWriter().Write(v) : null,
                    v => v != null ? new WKBReader().Read(v) as Point : null
                )
                .HasColumnType("geometry");

            modelBuilder.Entity<ExcursionAvailability>()
                .HasKey(ea => ea.Id);

            modelBuilder.Entity<ExcursionAvailability>()
                .HasOne(ea => ea.Excursion)
                .WithMany(e => e.Availability)
                .HasForeignKey(ea => ea.ExcursionId)
                .OnDelete(DeleteBehavior.Cascade);

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
            optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information); // Логирование SQL-запросов
        }
    }
}