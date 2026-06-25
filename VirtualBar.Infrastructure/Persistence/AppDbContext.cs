using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VirtualBar.Domain.Entities;

namespace VirtualBar.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Bottle> Bottles => Set<Bottle>();
    public DbSet<BottleImage> BottleImages => Set<BottleImage>();
    public DbSet<BottleLike> BottleLikes => Set<BottleLike>();
    public DbSet<BottleComment> BottleComments => Set<BottleComment>();
    public DbSet<UserFollow> UserFollows => Set<UserFollow>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<NewsPost> NewsPosts => Set<NewsPost>();
    public DbSet<NewsPostTranslation> NewsPostTranslations => Set<NewsPostTranslation>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<WishListItem> WishListItems => Set<WishListItem>();
    public DbSet<Distillery> Distilleries => Set<Distillery>();
    public DbSet<DistilleryCategory> DistilleryCategories => Set<DistilleryCategory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        foreach (var relationship in builder.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        builder.Entity<BottleLike>()
            .HasKey(l => new { l.BottleId, l.UserId });

        builder.Entity<UserFollow>()
            .HasKey(f => new { f.FollowerId, f.FollowedId });

        builder.Entity<UserFollow>()
            .HasOne(f => f.Follower)
            .WithMany(u => u.Following)
            .HasForeignKey(f => f.FollowerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UserFollow>()
            .HasOne(f => f.Followed)
            .WithMany(u => u.Followers)
            .HasForeignKey(f => f.FollowedId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany(u => u.SentMessages)
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Message>()
            .HasOne(m => m.Receiver)
            .WithMany(u => u.ReceivedMessages)
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Bottle>()
            .Property(b => b.AskingPrice)
            .HasColumnType("decimal(18,2)");

        builder.Entity<AppUser>()
            .HasIndex(u => u.DisplayName);

        builder.Entity<Bottle>()
            .HasIndex(b => new { b.UserId, b.IsDeleted });

        builder.Entity<UserFollow>()
            .HasIndex(f => f.FollowedId);

        builder.Entity<Message>()
            .HasIndex(m => m.SenderId);

        builder.Entity<Message>()
            .HasIndex(m => m.ReceiverId);

        builder.Entity<BottleComment>()
            .HasIndex(c => c.BottleId);

        builder.Entity<NewsPostTranslation>(e =>
        {
            e.HasKey(t => new { t.PostId, t.LanguageCode });
            e.HasOne(t => t.Post)
                .WithMany(p => p.Translations)
                .HasForeignKey(t => t.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Notification>(e =>
        {
            e.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(n => n.Actor)
                .WithMany()
                .HasForeignKey(n => n.ActorId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(n => new { n.UserId, n.IsDeleted, n.CreatedAt });
        });

        builder.Entity<WishListItem>(e =>
        {
            e.HasOne(w => w.User)
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(w => w.Distillery)
                .WithMany()
                .HasForeignKey(w => w.DistilleryId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(w => new { w.UserId, w.IsDeleted });
        });

        builder.Entity<Distillery>(e =>
        {
            e.HasIndex(d => d.Name)
                .IsUnique();

            e.HasMany(d => d.Bottles)
                .WithOne(b => b.Distillery)
                .HasForeignKey(b => b.DistilleryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DistilleryCategory>()
            .HasKey(dc => new { dc.DistilleryId, dc.Category });

        builder.Entity<DistilleryCategory>()
            .HasOne(dc => dc.Distillery)
            .WithMany(d => d.Categories)
            .HasForeignKey(dc => dc.DistilleryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
