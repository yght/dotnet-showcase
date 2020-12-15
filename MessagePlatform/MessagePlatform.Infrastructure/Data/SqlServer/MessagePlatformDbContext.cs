using Microsoft.EntityFrameworkCore;
using MessagePlatform.Core.Entities;

namespace MessagePlatform.Infrastructure.Data.SqlServer
{
    public class MessagePlatformDbContext : DbContext
    {
        public MessagePlatformDbContext(DbContextOptions<MessagePlatformDbContext> options)
            : base(options)
        {
        }

        public DbSet<Message> Messages { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Group> Groups { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.SenderId).IsRequired();
                entity.Property(e => e.Timestamp).IsRequired();
                
                entity.OwnsMany(e => e.Attachments, a =>
                {
                    a.HasKey(x => x.Id);
                    a.Property(x => x.FileName).IsRequired();
                    a.Property(x => x.FileUrl).IsRequired();
                });

                entity.OwnsMany(e => e.Reactions, r =>
                {
                    r.Property(x => x.UserId).IsRequired();
                    r.Property(x => x.Emoji).IsRequired();
                });
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Email).IsUnique();
                
                entity.OwnsOne(e => e.Preferences, p =>
                {
                    p.Property(x => x.EmailNotifications);
                    p.Property(x => x.PushNotifications);
                    p.Property(x => x.ShowOnlineStatus);
                    p.Property(x => x.ShowReadReceipts);
                    p.Property(x => x.Theme).HasMaxLength(20);
                    p.Property(x => x.Language).HasMaxLength(10);
                });
            });

            modelBuilder.Entity<Group>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CreatedBy).IsRequired();
                
                entity.OwnsOne(e => e.Settings, s =>
                {
                    s.Property(x => x.AllowMembersToAddOthers);
                    s.Property(x => x.AllowMembersToChangeGroupInfo);
                    s.Property(x => x.MuteNotifications);
                    s.Property(x => x.MaxMembers);
                });
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}