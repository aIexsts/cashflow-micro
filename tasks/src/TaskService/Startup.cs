using System;
using Cashflow.Common.Data.DataObjects;
using Cashflow.Common.Events;
using Cashflow.Common.Events.Money;
using Cashflow.Common.Middlewares;
using Cashflow.Common.Utils;
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
using TaskService.Data;
using TaskService.Data.Repos;
using TaskService.Data.Repos.Interfaces;
using TaskService.Events;
using TaskService.Events.Consumers;
using TaskService.Events.Publishers;
using TaskService.Events.Publishers.Interfaces;
using TaskService.Services.General;
using TaskService.Services.General.interfaces;
using TaskService.Services.Income;
using TaskService.Services.Income.interfaces;
using TaskService.Services.Promotion;
using TaskService.Services.Promotion.interfaces;

namespace TaskService
{
    public class Startup
    {
        private readonly IWebHostEnvironment env;
        private readonly ILogger<Startup> logger;
        public IConfiguration Config { get; }

        const int NUMBER_OF_RETRIES = 5;
        const int DELAY_IN_SECONDS = 3;

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
                services.AddDbContext<AppDbContext>(opt => opt.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
            }
            else
            {
                logger.LogInformation("---> Using inMem Db");
                services.AddDbContext<AppDbContext>(opt =>
                    opt.UseInMemoryDatabase("InMem"));
            }

            ConfigureRabbitMq(services);

            var jwtSettings = new JwtSettings();
            Config.Bind("JwtSettings", jwtSettings);
            services.AddSingleton(jwtSettings);

            services.AddJwtCookiesAuthentication(jwtSettings);

            services.AddTransient<IUserRepo, UserRepo>();
            services.AddTransient<ITaskRepo, TaskRepo>();
            services.AddScoped<ITaskTransactionRepo, TaskTransactionRepo>();

            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ITaskIncomeService, TaskIncomeService>();
            services.AddScoped<ITaskPromotionService, TaskPromotionService>();
            services.AddScoped<IMoneyTasksService, MoneyTasksService>();

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
                endpoints.UseCustomHealthChecks("/api/tasks/health");
            });

            // verify database connection:
            NetworkUtils.TryConnecting<MySqlException>(NUMBER_OF_RETRIES, DELAY_IN_SECONDS,
                () =>
                {
                    using var serviceScope = app.ApplicationServices.CreateScope();
                    serviceScope.ServiceProvider.GetService<AppDbContext>();
                    logger.LogInformation("---> Database connected");
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
                x.AddConsumer<UserCreatedConsumer>();
                x.AddConsumer<UserUpdatedConsumer>();
                x.AddConsumer<TaskApprovedConsumer>();
                x.AddConsumer<TaskTransactionCreatedConsumer>();
                x.AddBus(provider => Bus.Factory.CreateUsingRabbitMq(cfg =>
                {
                    cfg.AutoStart = true;
                    cfg.UseHealthCheck(provider);
                    cfg.Host(new Uri($"rabbitmq://{Config["RabbitMQSettings:Host"]}"),
                        hostConfigurator => { hostConfigurator.Heartbeat(TimeSpan.FromSeconds(5)); });
                    
                    // retry delivering messages from rabbitMQ:
                    cfg.UseDelayedExchangeMessageScheduler();

                    cfg.ReceiveEndpoint(Queue.Tasks.UserCreated, ep =>
                    {
                        ep.Exclusive = false;
                        ep.AutoDelete = false;
                        ep.Durable = true;
                        ep.PrefetchCount = 16;
                        ep.ConfigureConsumer<UserCreatedConsumer>(provider);
                        ep.UseDelayedRedelivery(r => r.Interval(10, TimeSpan.FromSeconds(10)));
                    });

                    cfg.ReceiveEndpoint(Queue.Tasks.UserUpdated, ep =>
                    {
                        ep.Exclusive = false;
                        ep.AutoDelete = false;
                        ep.Durable = true;
                        ep.PrefetchCount = 16;
                        ep.ConfigureConsumer<UserUpdatedConsumer>(provider);
                        ep.UseDelayedRedelivery(r => r.Interval(10, TimeSpan.FromSeconds(10)));
                    });
                    
                    cfg.ReceiveEndpoint(Queue.Tasks.TaskApproved, ep =>
                    {
                        ep.Exclusive = false;
                        ep.AutoDelete = false;
                        ep.Durable = true;
                        ep.PrefetchCount = 16;
                        ep.ConfigureConsumer<TaskApprovedConsumer>(provider);
                        ep.UseDelayedRedelivery(r => r.Interval(10, TimeSpan.FromSeconds(10)));
                    });
                    
                    cfg.ReceiveEndpoint(Queue.Tasks.TaskTransactionCreated, ep =>
                    {
                        ep.Exclusive = false;
                        ep.AutoDelete = false;
                        ep.Durable = true;
                        ep.PrefetchCount = 16;
                        ep.ConfigureConsumer<TaskTransactionCreatedConsumer>(provider);
                        ep.UseDelayedRedelivery(r => r.Interval(10, TimeSpan.FromSeconds(10)));
                    });
                }));
            });
            
            // inject services:
            services.AddMassTransitHostedService();
            services.AddScoped<UserCreatedConsumer>();
            services.AddScoped<UserUpdatedConsumer>();
            services.AddScoped<TaskApprovedConsumer>();
            services.AddScoped<TaskTransactionCreatedConsumer>();
            services.AddScoped<IMessageBusPublisher, MessageBusPublisher>();
        }

        private string GetMySqlDatabaseConnectionString()
        {
            return $"server={Config["DatabaseSettings:Url"]}; " +
                   $"port={Config["DatabaseSettings:Port"]}; " +
                   $"database={Config["DatabaseSettings:Name"]}; " +
                   $"user={Config["DatabaseSettings:User"]}; " +
                   $"password={Config["DatabaseSettings:Password"]};";
        }
    }
}
