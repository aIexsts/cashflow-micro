using System;
using AccountService.Data.Repos.Interfaces;
using AutoMapper;
using Cashflow.Common.Events.Moderation;
using MassTransit;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace AccountService.Events
{
    public class UserBannedConsumer : IConsumer<UserBannedEvent>
    {
        private readonly IMapper mapper;
        private readonly ILogger<UserBannedConsumer> logger;
        private readonly IUserRepo userRepo;
        private readonly MessageBusPublisher messageBusPublisher;

        public UserBannedConsumer(
            IMapper mapper,
            ILogger<UserBannedConsumer> logger,
            IUserRepo userRepo,
            MessageBusPublisher messageBusPublisher)
        {
            this.mapper = mapper;
            this.logger = logger;
            this.userRepo = userRepo;
            this.messageBusPublisher = messageBusPublisher;
        }

        public async Task Consume(ConsumeContext<UserBannedEvent> context)
        {
            var userBannedEvent = context.Message;
            if (userBannedEvent == null)
            {
                logger.LogError($"[User Banned Event] - Failed - User event is null");
                return; // remove broken event from queue
            }

            var user = await userRepo.GetByPublicId(userBannedEvent.UserId);
            if (user == null)
            {
                var errorMessage = $"[User Banned Event] - Failed - User does not exist";
                logger.LogError(errorMessage);
                throw new Exception(errorMessage);
            }

            user.IsBanned = true;
            await userRepo.Save(user);
            logger.LogInformation($"[User Banned Event] - Processed [User: {user.PublicId}]");

            // publish user updated event:
            await messageBusPublisher.PublishUpdatedUser(user);
        }
    }
}
