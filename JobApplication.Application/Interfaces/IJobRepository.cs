using JobApplication.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace JobApplication.Application.Interfaces
{
    public interface IJobRepository
    {
        Task<Job?> GetByIdAsync(int id);
        Task<List<Job>> GetAllAsync();
        Task InsertAsync(Job job);
        void Update(Job job);
        IQueryable<Job> Get();
        void Remove(Job job);
        Task SaveChangesAsync();
    }
}
