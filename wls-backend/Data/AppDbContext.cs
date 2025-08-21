﻿using Microsoft.EntityFrameworkCore;
using wls_backend.Models.Domain;

namespace wls_backend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Disturbance>()
                .Property(d => d.StartedAt)
                .HasColumnType("timestamp without time zone");
            builder.Entity<Disturbance>()
                .Property(d => d.EndedAt)
                .HasColumnType("timestamp without time zone");

            builder.Entity<DisturbanceDescription>()
                .HasKey(e => new { e.DisturbanceId, e.Text, e.CreatedAt });
            builder.Entity<DisturbanceDescription>()
                .Property(d => d.CreatedAt)
                .HasColumnType("timestamp without time zone");

            builder.Entity<Subscription>()
                .HasKey(s => new { s.SubscriberId, s.LineId });
            builder.Entity<Subscription>()
                .HasOne(s => s.Subscriber)
                .WithMany(sub => sub.Subscriptions)
                .HasForeignKey(s => s.SubscriberId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<Subscription>()
                .HasOne(s => s.Line)
                .WithMany(l => l.Subscriptions)
                .HasForeignKey(s => s.LineId);
            builder.Entity<Subscriber>()
                .HasIndex(s => s.Token)
                .IsUnique();
        }

        public DbSet<Disturbance> Disturbance { get; set; }
        public DbSet<DisturbanceDescription> DisturbanceDescription { get; set; }
        public DbSet<Line> Line { get; set; }
        public DbSet<Subscriber> Subscribers { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }

        public IQueryable<Disturbance> DisturbanceWithAll => Disturbance
            .Include(d => d.Descriptions.OrderBy(desc => desc.CreatedAt))
            .Include(d => d.Lines);

        public Func<Line, int> LineOrderSelector = l =>
        {
            try
            {
                return int.Parse(string.Concat(l.Id.Where(char.IsDigit)));
            }
            catch
            {
                return int.MinValue;
            }
        };
    }
}
