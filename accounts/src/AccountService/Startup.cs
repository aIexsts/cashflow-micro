using System;
using AccountService.Data;
using AccountService.Data.Repos;
using AccountService.Data.Repos.Interfaces;
using AccountService.Events.Consumers;
using AccountService.Events.Publishers;
using AccountService.Events.Publishers.Interfaces;
using AccountService.Services;
using AccountService.Services.interfaces;
using AccountService.Util.Jwt;
using Cashflow.Common.Data.DataObjects;
using Cashflow.Common.Events;
using Cashflow.Common.Middlewares;
using Cashflow.Common.Utils;
using Cashflow.Common.Utils.Interfaces;
using GreenPipes;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using MySqlConnector;

namespace AccountService
{
    public class Startup
    {
        private readonly IWebHostEnvironment env;
        private readonly ILogger<Startup> logger;

        const int NUMBER_OF_RETRIES = 5;
        const int DELAY_IN_SECONDS = 3;
        public IConfiguration Config { get; }

        public Startup(IConfiguration config, IWebHostEnvironment env)
        {
            Config = config;
            this.env = env;

            // startup logger:
            var loggerFactory = LoggerFactory.Create(Configuration.ConfigureLogs);
            logger = loggerFactory.CreateLogger<Startup>();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            if (env.IsProduction())
            {
                logger.LogInformation("---> Using MySql Db");
                var connectionString = GetMySqlDatabaseConnectionString();
                services.AddDbContext<AppDbContext>(opt =>
                    {
                        opt.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), builder =>
                        {
                            builder.CommandTimeout(100);
                            builder.EnableRetryOnFailure(5);
                        });
                    });
            }
            else
            {
                logger.LogInformation("---> Using inMem Db");
                services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("InMem"));
            }

            ConfigureRabbitMq(services);

            var jwtSettings = new JwtSettings();
            Config.Bind("JwtSettings", jwtSettings);
            services.AddSingleton(jwtSettings);
            services.AddTransient<JwtTokenCreator>();
            services.AddJwtCookiesAuthentication(jwtSettings);

            services.AddTransient<IUserRepo, UserRepo>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();

            services.AddControllers();
            services.AddScoped<LoggedInUserDataHolder>();
            services.AddScoped<CustomJwtBearerEvents>();

            // configure DI for application services
            services.AddHttpContextAccessor();

            // register automapper
            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

            // health checks:
            services.AddHealthChecks()
                .AddRabbitMQ($"amqp://{Config["RabbitMQSettings:Host"]}", name: "Rabbit")
                .AddMySql(GetMySqlDatabaseConnectionString(), name: "MySql");

            services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new OpenApiInfo { Title = "AccountService", Version = "v1" }); });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AccountService v1"));
            }

            // requests logging middleware
            app.UseMiddleware<RequestLoggingMiddleware>();

            // routing
            app.UseHttpsRedirection();
            app.UseRouting();

            // global cors policy
            app.UseCors(x => x
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());

            // authentication
            app.UseAuthentication();
            app.UseAuthorization();

            // global exception handler
            app.UseMiddleware<ErrorHandlerMiddleware>();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                // health checks:
                endpoints.UseCustomHealthChecks("/api/accounts/health");
            });

            // verify database connection:
            NetworkUtils.TryConnecting<MySqlException>(NUMBER_OF_RETRIES, DELAY_IN_SECONDS,
                () =>
                {
                    using var serviceScope = app.ApplicationServices.CreateScope();
                    serviceScope.ServiceProvider.GetService<AppDbContext>();
                    logger.LogInformation("---> MySQL Database connected");
                },
                retryCount => logger.LogInformation("---> Retrying to connect with MySQL: " + retryCount),
                () => logger.LogError("---> Could not connect to MySQL"));

            // db seeder:
            PrepDb.Seed(app, logger, env);
        }

        private void ConfigureRabbitMq(IServiceCollection services)
        {
            logger.LogInformation("---> Using RabbitMQ");
            services.AddMassTransit(x =>
            {
                x.AddConsumer<UserBannedConsumer>();
                x.AddBus(provider => Bus.Factory.CreateUsingRabbitMq(cfg =>
                {
                    cfg.AutoStart = true;
                    cfg.UseHealthCheck(provider);
                    cfg.Host(new Uri($"rabbitmq://{Config["RabbitMQSettings:Host"]}"),
                        hostConfigurator => { hostConfigurator.Heartbeat(TimeSpan.FromSeconds(5)); });
                    
                    // retry delivering messages from rabbitMQ:
                    cfg.UseDelayedExchangeMessageScheduler();
                    
                    cfg.ReceiveEndpoint(Queue.Accounts.UserBanned, ep =>
                    {
                        ep.Exclusive = false;
                        ep.AutoDelete = false;
                        ep.Durable = true;
                        ep.PrefetchCount = 16;
                        ep.ConfigureConsumer<UserBannedConsumer>(provider);
                        ep.UseDelayedRedelivery(r => r.Interval(10, TimeSpan.FromSeconds(10)));
                    });
                }));
            });
            
            // inject services:
            services.AddMassTransitHostedService();
            services.AddScoped<UserBannedConsumer>();
            services.AddScoped<IMessageBusPublisher, MessageBusPublisher>();
        }

        private string GetMySqlDatabaseConnectionString()
        {
            return $"server={Config["DatabaseSettings:Url"]}; " +
                   $"port={Config["DatabaseSettings:Port"]}; " +
                   $"database={Config["DatabaseSettings:Name"]}; " +
                   $"user={Config["DatabaseSettings:User"]}; " +
                   $"password={Config["DatabaseSettings:Password"]};" + 
                   "connect timeout=100;";
        }
    }
}
