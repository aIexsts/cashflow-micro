using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Cashflow.Common.Data.Enums;
using FluentAssertions;
using TaskService.Data;
using TaskService.Dtos;
using TaskService.Dtos.Promotion;
using TaskService.Tests.Common;
using TaskService.Util;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace TaskService.Tests.Api
{
    public class TasksControllerIntegrationTest : IntegrationTestBase<AppDbContext, Startup>
    {
        [Fact]
        public async Task Create_withoutToken_return_Unauthorized()
        {
            // Arrange
            var task = CreateTask("task123", 123);
            Arrange(dbContext => { dbContext.Tasks.Add(task); });

            var formModel = new Dictionary<string, string>
            {
                { "title", "task-title" },
                { "description", "task-description" }
            };
            var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Tasks.Create);
            request.Content = JsonContent.Create(formModel);

            // Act
            var response = await TestClient.SendAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Create_with_basicUserToken_return_Ok_TaskReadDto()
        {
            // Arrange
            var user = CreateUser("user");
            Arrange(dbContext => { dbContext.Users.Add(user); });

            var task = CreateTask("task1", user.Id);

            Arrange(dbContext => { dbContext.Tasks.Add(task); });

            var formModel = new Dictionary<string, string>
            {
                { "title", "task-title" },
                { "description", "task-description" }
            };
            var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Tasks.Create);
            request.Content = JsonContent.Create(formModel);

            // Act
            AuthorizeRequestWithUser(request, user);
            var response = await TestClient.SendAsync(request);

            // Assert
            Assert.NotNull(response);
            response.EnsureSuccessStatusCode();
            var createdTask = await response.Content.ReadAsAsync<TaskReadDto>();
            createdTask.Should().NotBeNull();
        }
        
        [Fact]
        public async Task Create_with_basicUserToken_InvalidData_return_Ok_TaskReadDto()
        {
            // Arrange
            var user = CreateUser("user");
            Arrange(dbContext => { dbContext.Users.Add(user); });

            var task = CreateTask("task123", user.Id);
            Arrange(dbContext => { dbContext.Tasks.Add(task); });

            var formModel = new Dictionary<string, string>
            {
                { "title", "" },
                { "description", "task-description" }
            };
            var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Tasks.Create);
            request.Content = JsonContent.Create(formModel);

            // Act
            AuthorizeRequestWithUser(request, user);
            var response = await TestClient.SendAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var taskResponse = await response.Content.ReadAsStringAsync();
            taskResponse.Should().NotBeNull();
            Assert.Contains("The Title field is required", taskResponse);
            
            // Arrange
            formModel["title"] = "task-title";
            formModel["description"] = "";
            
            request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Tasks.Create);
            request.Content = JsonContent.Create(formModel);

            // Act
            AuthorizeRequestWithUser(request, user);
            response = await TestClient.SendAsync(request);
            
            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            taskResponse = await response.Content.ReadAsStringAsync();
            taskResponse.Should().NotBeNull();
            Assert.Contains("The Description field is required", taskResponse);
        }
        
        [Fact]
        public async Task Update_withoutToken_return_Unauthorized()
        {
            // Arrange
            var task = CreateTask("task123");
            Arrange(dbContext => { dbContext.Tasks.Add(task); });

            var formUpdateModel = new Dictionary<string, string>
            {
                { "title", "newtitle" },
                { "description", "description2" }
            };
            var updateRequest = new HttpRequestMessage(HttpMethod.Put, ApiRoutes.Tasks.Update.Replace("{id}", task.PublicId));
            updateRequest.Content = JsonContent.Create(formUpdateModel);

            // Act
            var response = await TestClient.SendAsync(updateRequest);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        
        [Fact]
        public async Task Update_with_basicUserToken_notYourTaskId_return_BadRequest_NotYourTask()
        {
            // Arrange
            var user = CreateUser("user");
            Arrange(dbContext => { dbContext.Users.Add(user); });

            var task = CreateTask("task123");
            var task2 = CreateTask("task456", user.Id);
            Arrange(dbContext => { dbContext.Tasks.AddRange(task, task2); });

            var formUpdateModel = new Dictionary<string, string>
            {
                { "title", "newtitle" },
                { "description", "description2" }
            };
            var updateRequest = new HttpRequestMessage(HttpMethod.Put, ApiRoutes.Tasks.Update.Replace("{id}", task.PublicId));
            updateRequest.Content = JsonContent.Create(formUpdateModel);

            // Act
            AuthorizeRequestWithUser(updateRequest, user);
            var response = await TestClient.SendAsync(updateRequest);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var taskResponse = await response.Content.ReadAsStringAsync();
            taskResponse.Should().NotBeNull();
            Assert.Contains("Not your task", taskResponse);
        }
        
        [Fact]
        public async Task Update_with_basicUserToken_nonExistingTaskId_return_BadRequest_NotYourTask()
        {
            // Arrange
            var user = CreateUser("user");
            Arrange(dbContext => { dbContext.Users.Add(user); });

            var task = CreateTask("task123");
            Arrange(dbContext => { dbContext.Tasks.AddRange(task); });

            var formUpdateModel = new Dictionary<string, string>
            {
                { "title", "newtitle" },
                { "description", "description2" }
            };
            var updateRequest = new HttpRequestMessage(HttpMethod.Put, ApiRoutes.Tasks.Update.Replace("{id}", "non-existing-id"));
            updateRequest.Content = JsonContent.Create(formUpdateModel);

            // Act
            AuthorizeRequestWithUser(updateRequest, user);
            var response = await TestClient.SendAsync(updateRequest);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var taskResponse = await response.Content.ReadAsStringAsync();
            taskResponse.Should().NotBeNull();
            Assert.Contains("Task not found", taskResponse);
        }

        [Fact]
        public async Task Update_with_basicUserToken_return_Ok_UserReadDto()
        {
            // Arrange
            var user = CreateUser("user");
            Arrange(dbContext => { dbContext.Users.Add(user); });

            var task = CreateTask("task123", user.Id);
            Arrange(dbContext => { dbContext.Tasks.Add(task); });

            var formUpdateModel = new Dictionary<string, string>
            {
                { "title", "newtitle" },
                { "description", "description2" }
            };
            var updateRequest = new HttpRequestMessage(HttpMethod.Put, ApiRoutes.Tasks.Update.Replace("{id}", task.PublicId));
            updateRequest.Content = JsonContent.Create(formUpdateModel);

            // Act
            AuthorizeRequestWithUser(updateRequest, user);
            var response = await TestClient.SendAsync(updateRequest);

            // Assert
            Assert.NotNull(response);
            response.EnsureSuccessStatusCode();
            var updatedTask = await response.Content.ReadAsAsync<TaskReadDto>();
            updatedTask.Should().NotBeNull();
            Assert.Equal(formUpdateModel["title"], updatedTask.Title);
            Assert.Equal(formUpdateModel["description"], updatedTask.Description);
        }
        
        [Fact]
        public async Task GetById_withoutToken_return_Unauthorized()
        {
            // Arrange
            var user = CreateUser("user");
            Arrange(dbContext => { dbContext.Users.Add(user); });
            
            Arrange(dbContext => { dbContext.Tasks.Add(CreateTask("task123", user.Id)); });
            var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Tasks.GetById.Replace("{id}", "non-existing-task"));
            
            // Act
            var response = await TestClient.SendAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        
        [Fact]
        public async Task GetById_with_basicUserToken_nonExistingTaskId_return_NotFound()
        {
            // Arrange
            var user = CreateUser("user");
            Arrange(dbContext => { dbContext.Users.Add(user); });
            
            Arrange(dbContext => { dbContext.Tasks.Add(CreateTask("task123", user.Id)); });
            var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Tasks.GetById.Replace("{id}", "non-existing-task"));
            
            // Act
            AuthorizeRequestWithUser(request, user);
            var response = await TestClient.SendAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        
        [Fact]
        public async Task GetById_with_basicUserToken_return_Ok_with_TaskReadDto()
        {
            // Arrange
            var user = CreateUser("user");
            Arrange(dbContext => { dbContext.Users.Add(user); });

            var task = CreateTask("task123", user.Id);
            Arrange(dbContext => { dbContext.Tasks.Add(task); });
            var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Tasks.GetById.Replace("{id}", task.PublicId));

            // Act
            AuthorizeRequestWithUser(request, user);
            var response = await TestClient.SendAsync(request);

            // Assert
            Assert.NotNull(response);
            response.EnsureSuccessStatusCode();
            var createdTask = await response.Content.ReadAsAsync<TaskReadDto>();
            createdTask.Should().NotBeNull();
            createdTask.Should().BeEquivalentTo(task.ToPublicDto());
        }
        
        [Fact]
        public async Task GetAll_with_basicUserToken_return_Forbidden()
        {
            // Arrange
            var user = CreateUser("user");
            Arrange(dbContext => { dbContext.Users.Add(user); });
            
            Arrange(dbContext => { dbContext.Tasks.Add(CreateTask("task123", user.Id)); });
            var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Tasks.GetAll);

            // Act
            AuthorizeRequestWithUser(request, user);
            var response = await TestClient.SendAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        
        [Fact]
        public async Task GetAll_withoutToken_return_Unauthorized()
        {
            // Arrange
            var user = CreateUser("user");
            Arrange(dbContext => { dbContext.Users.Add(user); });
            
            Arrange(dbContext => { dbContext.Tasks.Add(CreateTask("task123", user.Id)); });
            var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Tasks.GetAll);

            // Act
            var response = await TestClient.SendAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetAll_with_adminUserToken_return_Ok_Tasks()
        {
            // Arrange
            var adminUser = CreateUser("user", Roles.Admin);
            Arrange(dbContext => { dbContext.Users.Add(adminUser); });
            
            Arrange(dbContext =>
            {
                dbContext.Tasks.AddRange(
                    CreateTask("task1"),
                    CreateTask("task2"),
                    CreateTask("task3")
                );
            });

            var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Tasks.GetAll);

            // Act
            AuthorizeRequestWithUser(request, adminUser);
            var response = await TestClient.SendAsync(request);

            // Assert
            Assert.NotNull(response);
            var tasks = await response.Content.ReadAsAsync<List<TaskReadDto>>();
            tasks.Should().NotBeEmpty();
            Assert.Equal(3, tasks.Count);
        }
    }
}
