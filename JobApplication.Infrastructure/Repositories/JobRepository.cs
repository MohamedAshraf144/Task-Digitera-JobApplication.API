using JobApplication.Application.Interfaces;
using JobApplication.Domain.Entities;
using JobApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JobApplication.Infrastructure.Repositories
{
    public class JobRepository : IJobRepository
    {
        private readonly ApplicationDbContext _context;

        public JobRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Job?> GetByIdAsync(int id)
        {
            return await _context.Jobs.FindAsync(id);
        }

        public async Task<List<Job>> GetAllAsync()
        {
            return await _context.Jobs.ToListAsync();
        }

        public async Task<List<Job>> GetActiveJobsOlderThanAsync(DateTime cutoffDate)
        {
            return await _context.Jobs
                .Where(j => j.IsActive && j.CreatedAt <= cutoffDate)
                .ToListAsync();
        }

        public async Task InsertAsync(Job job)
        {
            await _context.Jobs.AddAsync(job);
        }

        public void Update(Job job)
        {
            _context.Jobs.Update(job);
        }

        public IQueryable<Job> Get()
        {
            return _context.Jobs.AsQueryable();
        }

        public void Remove(Job job)
        {
            _context.Jobs.Remove(job);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
