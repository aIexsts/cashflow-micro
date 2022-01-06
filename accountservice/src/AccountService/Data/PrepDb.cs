using System;
using System.Linq;
using AccountService.Models;
using AccountService.Util.Enums;
using AccountService.Util.Helpers;
using AccountService.Util.Helpers.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountService.Data
{
    public static class PrepDb
    {
        public static void PrepPopulation(IApplicationBuilder app, bool isProd)
        {
            using var serviceScope = app.ApplicationServices.CreateScope();

            var appDbContext = serviceScope.ServiceProvider.GetService<AppDbContext>();
            var passwordHasher = serviceScope.ServiceProvider.GetService<IPasswordHasher>();
            SeedData(appDbContext, passwordHasher, isProd);
        }

        private static void SeedData(AppDbContext context, IPasswordHasher passwordHasher, bool isProd)
        {

            if (isProd)
            {
                Console.WriteLine("---> Applying migrations");
                try
                {
                    context.Database.Migrate();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"---> Migrations failed to apply {e.Message}");
                }
            }
            
            Console.WriteLine("---> Seeding data");

            if (!context.Roles.Any())
            {
                var roleUser = new Role
                {
                    Id = 1,
                    Name = "User",
                    Description = "Basic User"
                };

                var roleAdmin = new Role
                {
                    Id = 2,
                    Name = "Admin",
                    Description = "Admin User"
                };

                var roleSuperAdmin = new Role
                {
                    Id = 3,
                    Name = "SuperAdmin",
                    Description = "SuperAdmin User"
                };
                
                context.Roles.AddRange(roleUser, roleAdmin, roleSuperAdmin);
                context.SaveChanges();
            }

            if (!context.Users.Any())
            {
                var adminUser = new User
                {

                    Email = "admin@casflow.com",
                    UserName = "admin",
                    Firstname = "Admin",
                    Lastname = "Admin",
                    Password = passwordHasher.Hash("password"),
                    CreatedAt = new DateTime(),
                    IsActive = true,
                    RefreshToken = null,
                    Gender = Genders.Male,
                    RoleId = (int) Roles.Admin
                };
                
                var superAdminUser = new User
                {

                    Email = "superadmin@casflow.com",
                    UserName = "superadmin",
                    Firstname = "Superadmin",
                    Lastname = "Superadmin",
                    Password = passwordHasher.Hash("password"),
                    CreatedAt = new DateTime(),
                    IsActive = true,
                    RefreshToken = null,
                    Gender = Genders.Male,
                    RoleId = (int) Roles.SuperAdmin
                };
                
                var user = new User
                {

                    Email = "user@casflow.com",
                    UserName = "user",
                    Firstname = "User",
                    Lastname = "User",
                    Password = passwordHasher.Hash("password"),
                    CreatedAt = new DateTime(),
                    IsActive = true,
                    RefreshToken = null,
                    Gender = Genders.Male,
                    RoleId = (int) Roles.User
                };

                context.Users.AddRange(superAdminUser, adminUser, user);
                context.SaveChanges();
            }
            else
            {
                Console.WriteLine("---> We already have data");
            }
        }
    }
}
