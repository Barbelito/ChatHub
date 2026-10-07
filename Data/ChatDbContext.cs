using ChatHub.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatHub.Data;

public class ChatDbContext : DbContext
{
    public ChatDbContext(
        DbContextOptions<ChatDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();

    public DbSet<ChatRoomMember> ChatRoomMembers =>
        Set<ChatRoomMember>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =========================
        // User
        // =========================

        modelBuilder.Entity<User>()
            .HasIndex(user => user.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .Property(user => user.Username)
            .HasMaxLength(50)
            .IsRequired();

        modelBuilder.Entity<User>()
            .Property(user => user.PasswordHash)
            .IsRequired();


        // =========================
        // ChatRoom
        // =========================

        modelBuilder.Entity<ChatRoom>()
            .Property(room => room.Name)
            .HasMaxLength(100)
            .IsRequired();

        // Två rum får inte ha samma namn
        modelBuilder.Entity<ChatRoom>()
            .HasIndex(room => room.Name)
            .IsUnique();

        // Användaren som skapade rummet
        modelBuilder.Entity<ChatRoom>()
            .HasOne(room => room.CreatedByUser)
            .WithMany(user => user.CreatedRooms)
            .HasForeignKey(room => room.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);


        // =========================
        // ChatRoomMember
        // =========================

        // Composite primary key
        modelBuilder.Entity<ChatRoomMember>()
            .HasKey(member => new
            {
                member.ChatRoomId,
                member.UserId
            });

        // Room -> Members
        modelBuilder.Entity<ChatRoomMember>()
            .HasOne(member => member.ChatRoom)
            .WithMany(room => room.Members)
            .HasForeignKey(member => member.ChatRoomId)
            .OnDelete(DeleteBehavior.Cascade);

        // User -> Memberships
        modelBuilder.Entity<ChatRoomMember>()
            .HasOne(member => member.User)
            .WithMany(user => user.ChatRoomMemberships)
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}