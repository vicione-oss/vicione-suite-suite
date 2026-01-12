using JiTChat.Public.Commands;
using JiTChat.Public.Events;
using MassTransit;

namespace JiTChat.Backend.Consumers
{
    public sealed class PublishMessageConsumer : IConsumer<PublishMessage>
    {
        public async Task Consume(ConsumeContext<PublishMessage> context)
        {
            await context.Publish(new MessagePublished()
            {
                SenderId = context.Message.SenderId,
                Message = context.Message.Message
            });
        }
    }
}
