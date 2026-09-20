using JobApplication.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobApplication.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Job> Jobs { get; set; }
        public DbSet<Candidate> Candidates { get; set; }
        public DbSet<JobCandidateApplication> JobCandidateApplications { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.Email).IsRequired();
                entity.Property(u => u.PasswordHash).IsRequired();
                entity.Property(u => u.Role).HasConversion<string>();
            });

            modelBuilder.Entity<Job>(entity =>
            {
                entity.HasKey(j => j.Id);
                entity.Property(j => j.Title).IsRequired();
                entity.Property(j => j.Description).IsRequired();

                entity.HasOne(j => j.Recruiter)
                    .WithMany()
                    .HasForeignKey(j => j.RecruiterId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(j => j.ClosedByUser)
                    .WithMany()
                    .HasForeignKey(j => j.ClosedBy)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Candidate>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.HasOne(c => c.User)
                    .WithMany()
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<JobCandidateApplication>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.HasOne(a => a.Candidate)
                    .WithMany()
                    .HasForeignKey(a => a.CandidateId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Job)
                    .WithMany()
                    .HasForeignKey(a => a.JobId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
