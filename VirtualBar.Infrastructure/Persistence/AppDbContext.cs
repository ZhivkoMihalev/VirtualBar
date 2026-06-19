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
    }
}
