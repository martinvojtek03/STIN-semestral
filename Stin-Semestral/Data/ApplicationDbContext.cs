using Microsoft.EntityFrameworkCore;
using Stin_Semestral.Models;
using System.Collections.Generic;

namespace Stin_Semestral.Data
{
    // DbContext je hlavní třída, která koordinuje funkcionalitu Entity Frameworku pro daný datový model
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Tyto vlastnosti DbSet reprezentují tabulky v databázi
        public DbSet<UserSettings> Settings { get; set; }
        public DbSet<ExchangeLog> Logs { get; set; }
    }
}