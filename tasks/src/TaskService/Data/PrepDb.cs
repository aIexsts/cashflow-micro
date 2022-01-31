using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskService.Util.Helpers.Interfaces;

namespace TaskService.Data
{
    public static class PrepDb
    {
        public static void Seed(IApplicationBuilder app, ILogger<Startup> logger, IWebHostEnvironment env)
        {
            using var serviceScope = app.ApplicationServices.CreateScope();
            var appDbContext = serviceScope.ServiceProvider.GetService<AppDbContext>();
            var passwordHasher = serviceScope.ServiceProvider.GetService<IPasswordHasher>();
            SeedData(appDbContext, passwordHasher, logger, env);
        }

        private static void SeedData(AppDbContext context, IPasswordHasher passwordHasher, ILogger<Startup> logger, IWebHostEnvironment env)
        {
            if (env.EnvironmentName == "Testing")
            {
                return;
            }
            
            if (env.IsProduction())
            {
                logger.LogInformation("---> Applying migrations");
                try
                {
                    context.Database.EnsureCreated();
                    context.Database.Migrate();
                }
                catch (Exception e)
                {
                    logger.LogError("---> Migrations failed to apply");
                }
            }
        }
    }
}
