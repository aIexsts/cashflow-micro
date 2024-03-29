using System;
using AutoMapper;
using Cashflow.Common.Events.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using ModerationService.Data.Repos.Interfaces;
using Task = System.Threading.Tasks.Task;
using TaskEntity = ModerationService.Data.Models.External.Task;

namespace ModerationService.Events.Consumers
{
    public class TaskUpdatedConsumer : IConsumer<TaskUpdatedEvent>
    {
        private readonly IMapper mapper;
        private readonly ILogger<TaskUpdatedConsumer> logger;
        private readonly ITaskRepo taskRepo;
        public TaskUpdatedConsumer(IMapper mapper, ILogger<TaskUpdatedConsumer> logger, ITaskRepo taskRepo)
        {
            this.mapper = mapper;
            this.logger = logger;
            this.taskRepo = taskRepo;
        }

        public async Task Consume(ConsumeContext<TaskUpdatedEvent> context)
        {
            var taskFromEvent = mapper.Map<TaskEntity>(context.Message);
            
            if (taskFromEvent == null)
            {
                logger.LogError($"[Task Updated Event] - Failed - Task event is null");
                return; // remove broken event from queue
            }

            var existingTask = await taskRepo.GetByPublicId(taskFromEvent.PublicId);
            if (existingTask == null)
            {
                // schedule redelivery (user maybe already created)
                var errorMessage = $"[Task Updated Event] - Failed - Task does not exist [Task: {taskFromEvent.PublicId}, Version: {taskFromEvent.Version}]";
                logger.LogError(errorMessage);
                throw new Exception(errorMessage);
            }

            if ((taskFromEvent.Version - 1) != existingTask.Version)
            {
                // schedule redelivery (user updates can get out of order)
                var errorMessage = $"[Task Updated Event] - Failed - Version Mismatch [Task: {taskFromEvent.PublicId}, Version: {existingTask.Version}, {taskFromEvent.Version}]";
                logger.LogError(errorMessage);
                throw new Exception(errorMessage);
            }

            // update existing task:
            existingTask.PublicId = taskFromEvent.PublicId;
            existingTask.Title = taskFromEvent.Title;
            existingTask.TaskStatus = taskFromEvent.TaskStatus;
            existingTask.RewardPrice = taskFromEvent.RewardPrice;
            existingTask.CreatedAt = taskFromEvent.CreatedAt;
            existingTask.CreatedByUserId = taskFromEvent.CreatedByUserId;
            existingTask.LastUpdatedAt = taskFromEvent.LastUpdatedAt;
            existingTask.LastUpdatedByUserId = taskFromEvent.LastUpdatedByUserId;
            existingTask.Version = taskFromEvent.Version;
            
            await taskRepo.Save(existingTask);
            logger.LogInformation($"[Task Updated Event] - Processed - [Task: {taskFromEvent.PublicId}, Version: {taskFromEvent.Version}]");
        }
    }
}
