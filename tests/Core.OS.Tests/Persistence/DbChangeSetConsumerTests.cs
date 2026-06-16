using Core.OS.Persistence;
using Core.OS.Persistence.Consumers;
using MassTransit;
using NSubstitute;
using Xunit;

namespace Core.OS.Tests.Persistence;

public partial class DbChangeSetConsumerTests
{
    private static ConsumeContext<DbChangeSet> CreateConsumeContext(DbChangeSet message)
    {
        var context = Substitute.For<ConsumeContext<DbChangeSet>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);

        var sendEndpoint = Substitute.For<ISendEndpoint>();
        context.GetSendEndpoint(Arg.Any<Uri>()).Returns(Task.FromResult(sendEndpoint));

        return context;
    }
}
