// using System;
// using System.Collections.Generic;
// using System.Threading;
// using System.Threading.Tasks;
// using Core.OS.Instance.Initialization;
// using Core.OS.Modules.Initialization;
// using Core.OS.Tests;
// using Core.Persistence;
// using Core.Shared;
// using Core.Shared.Instance;
// using Core.Shared.Instance.Initialization;
// using MediatR;
// using Microsoft.AspNetCore.Mvc.Testing;
// using Microsoft.AspNetCore.TestHost;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.DependencyInjection;
// using Moq;
// using Sdk.Backend.Modules.Initialization;
// using Sdk.Instance;
// using Sdk.Testing;
// using Sdk.Testing.Backend;
// using Xunit;
// using Xunit.Abstractions;

// namespace Core.OS.Tests.Instance.Initialization;
//
// public class UpdateInstanceRegisterCommandHandlerTests : IClassFixture<TestApplicationFactory<TestStartup>>
// {
//     private readonly CancellationTokenSource _cancelTokenSource = new();
//     private readonly Mock<IInstanceInformationProvider> _instanceInformationMock = new();
//
//     private readonly BackgroundTaskQueue _taskQueue = new();
//     private readonly WebApplicationFactory<TestStartup> _appFactory;
//
//     public UpdateInstanceRegisterCommandHandlerTests(TestApplicationFactory<TestStartup> appFactory, ITestOutputHelper output)
//     {
//         _appFactory = appFactory.WithWebHostBuilder(builder =>
//         {
//             // base setup done in factory - add/override services needed for the test
//             builder.ConfigureTestServices(services =>
//             {
//                 services.AddSingleton<IBackgroundTaskQueue>(_taskQueue);
//                 services.AddSingleton(_instanceInformationMock.Object);
//
//                 services.AddModuleManagerWithTestModule();
//                 services.AddApplicationDbContextInMemory();
//             });
//         });
//     }
//
//     private IRequestHandler<UpdateInstanceRegisterCommand> GetHandler()
//     {
//         var taskQueue = _appFactory.Services.GetRequiredService<IBackgroundTaskQueue>();
//
//         return new UpdateInstanceRegisterCommandHandler(taskQueue);
//     }
//
//     [Fact]
//     public async Task EnqueueInstanceInfoUpdateToTaskQueue()
//     {
//         // Arrange
//         var test1ModulesStart = new List<string> { "System", "Dashboard", "Moneo" };
//         var test1ModulesUpdate = new List<string> { "System", "Reporting", "Ping" };
//         var test1InstanceId = Guid.NewGuid();
//
//         var handler = GetHandler();
//         var request = new UpdateInstanceRegisterCommand
//         {
//
//             AllInstances = new List<InstanceInformation>
//             {
//                 new () {Id = Constants.MasterInstanceGuid, Type = InstanceType.Master},
//                 new () {Id = test1InstanceId, Type = InstanceType.Slave, InstalledModules = test1ModulesStart},
//                 new () {Id = Guid.NewGuid(), Type = InstanceType.Slave}
//             }
//         };
//         request.SetOriginalInstanceId(TestConstants.TestInstanceIdAsGuid);
//
//         var request2 = new UpdateInstanceRegisterCommand
//         {
//             AllInstances = new List<InstanceInformation>
//             {
//                 new () {Id = test1InstanceId, Type = InstanceType.Slave, InstalledModules = test1ModulesUpdate},
//                 new () {Id = Guid.NewGuid(), Type = InstanceType.Slave}
//             }
//         };
//         request2.SetOriginalInstanceId(TestConstants.TestInstanceIdAsGuid);
//
//         _instanceInformationMock.SetupGetInstanceInformation(InstanceType.Master);
//
//         // Act
//         await _appFactory.Services.EnsureApplicationContextIsCreated(_cancelTokenSource.Token);
//
//         await handler.Handle(request, _cancelTokenSource.Token);
//
//         var workItem = await _taskQueue.DequeueAsync(_cancelTokenSource.Token);
//         if (workItem is not null)
//         {
//             await workItem.Invoke(_appFactory.Services, _cancelTokenSource.Token);
//         }
//
//         await handler.Handle(request2, _cancelTokenSource.Token);
//
//         var workItem2 = await _taskQueue.DequeueAsync(_cancelTokenSource.Token);
//         if (workItem2 is not null)
//         {
//             await workItem2.Invoke(_appFactory.Services, _cancelTokenSource.Token);
//         }
//
//         // Assert
//         var appDb = _appFactory.Services.GetRequiredService<ApplicationDbContext>();
//         var count = await appDb.InstanceInfo.CountAsync(_cancelTokenSource.Token);
//         var test1 = await appDb.InstanceInfo.FirstOrDefaultAsync(k => k.Id == test1InstanceId);
//
//         Assert.Equal(3, count); // we expect that our master instance added the 3 instances to the db
//         Assert.NotNull(test1);
//         Assert.Equal(test1!.InstalledModules, test1ModulesUpdate);
//     }
// }
