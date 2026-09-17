using System.Globalization;
using System.Text.Json;
using AngleSharp.Dom;
using Blazor.Shared.Mqtt;
using Blazor.Shared.Mqtt.Contracts;
using Blazor.Shared.Mqtt.Helpers;
using Blazor.Shared.Mqtt.Services;
using Blazor.Tests.Tools;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;
using Sdk.Connections.Requests;

namespace Blazor.Shared.Tests.Mqtt;

public class MqttViewerComponentTests
{
    private const string SkipMessage = "#709 - There some timing issue with the async popup rendering that fails in pipeline";
    private const string SkipFailing = "#80x - These tests need to be refactored because they fail to often";

    private const string ConnectButtonSelector = ".monochrome-icon-wifi";
    private const string DisconnectButtonSelector = ".monochrome-icon-wifi-off";
    private const string ClearFilterButtonSelector = $"button[id={MqttViewerConstants.ClearFilterButtonId}]";
    private const string ToggleTextFilterButtonSelector = $"button[id={MqttViewerConstants.ToggleTextFilterButtonId}]";
    private const string ToggleNodeFilterButtonSelector = $"button[id={MqttViewerConstants.ToggleNodeFilterButtonId}]";

    private readonly TimeSpan _extraWaitTime = TimeSpan.FromSeconds(10);

    private readonly Connection _localMqttConnection = new()
    {
        Json = JsonSerializer.Serialize<MqttConnection>(new()
        {
            Address = "localhost",
            Port = 1883,
            Protocol = MqttConnectionType.TCP,
        }),
        Type = ConnectionType.Mqtt,
        Name = "test",
    };

    private readonly MessageModel _message = new()
    {
        Topic = "test",
        Message = "a",
        MessageId = 1,
        Qos = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce,
    };

    private static IElement FindConnectButton(IRenderedComponent<MqttViewerComponent> component)
        => component.Find(ConnectButtonSelector);

    private static void ConfigureTestServices(BunitContext ctx, List<Connection>? connections = null, List<MessageModel>? messages = null)
    {
        ctx.SetupBlazorUiComponents(s =>
        {
            s.ClientMediator
                .Request<GetConnections, GetConnectionsResponse>(Arg.Any<GetConnections>(), Arg.Any<CancellationToken>())
                .Returns(new GetConnectionsResponse(connections ?? []));
        });

        ctx.Services.AddScoped(s =>
        {
            var mqttService = Substitute.For<IMqttService>();
            mqttService.Messages.Returns(messages ?? []);
            mqttService.MessagesDict.Returns(messages != null ? messages.ToDictionary(k => k.Topic, k => k) : []);
            return mqttService;
        });

        ctx.Services.AddScoped<MqttViewerComponentService>();
        ctx.Services.AddTransient<ViciOne.Ui.TreeEditor.Builder.ITreeBuilder, ViciOne.Ui.TreeEditor.Builder.TreeBuilder>();
        ctx.Services.AddScoped<MqttTopicTreeAdapter>();
    }

    public sealed class OnInitializedAsync : MqttViewerComponentTests
    {
        [Fact(Skip = SkipFailing)]
        public void Should_set_variables()
        {
            using var ctx = new BunitContext();
            ConfigureTestServices(ctx,
                [
                    new Connection()
                    {
                        Json = JsonSerializer.Serialize<MqttConnection>(new()
                        {
                            Address = "localhost",
                            Port = 1883,
                            Username = "username",
                            Password = "password",
                            Protocol = MqttConnectionType.TCP,
                        }),
                        Name = "test Connection1"
                    },
                    new Connection()
                    {
                        Json = JsonSerializer.Serialize<MqttConnection>(new()
                        {
                            Address = "localhost",
                            Port = 1883,
                            Username = "username",
                            Password = "password",
                            Protocol = MqttConnectionType.TCP,
                        }),
                        Name = "test Connection2"
                    },
                ]);

            var component = ctx.Render<MqttViewerComponent>();

            var input = component.Find(".text-box");
            input.OuterHtml.Should().Contain("field-text=\"test Connection1\"");
        }

        [Fact(Skip = SkipMessage)]
        public void Should_set_message_when_no_connections_are_configured()
        {
            // Arrange
            using var ctx = new BunitContext();
            ConfigureTestServices(ctx);

            // Act
            var component = ctx.Render<MqttViewerComponent>();

            // Assert
            component.WaitForElement(".modal-content", _extraWaitTime).InnerHtml.Should().Contain("no_connections");
        }
    }

    public sealed class ConnectClient : MqttViewerComponentTests
    {
        [Fact(Skip = SkipFailing)]
        public async Task Should_return_when_selected_connection_is_null()
        {
            // Arrange
            await using var ctx = new BunitContext();
            ConfigureTestServices(ctx);

            // Act
            var component = ctx.Render<MqttViewerComponent>();
            await FindConnectButton(component).ClickAsync();

            // Assert
            var service = ctx.Services.GetRequiredService<IMqttService>();
            await service.Received(0).Connect(Arg.Any<MqttConnection>());
        }

        [Fact(Skip = SkipFailing)]
        public async Task Should_call_connect_on_client()
        {
            // Arrange
            await using var ctx = new BunitContext();
            ConfigureTestServices(ctx,
                [
                    _localMqttConnection,
                ]);

            // Act
            var component = ctx.Render<MqttViewerComponent>();
            await FindConnectButton(component).ClickAsync();

            // Assert
            var mqtt = _localMqttConnection.GetMqttConnection();
            Assert.NotNull(mqtt);

            var service = ctx.Services.GetRequiredService<IMqttService>();
            await service.Received(1)
                .Connect(Arg.Is((MqttConnection c)
                    => c.Address == mqtt.Address &&
                    c.Port == mqtt.Port &&
                    c.Protocol == mqtt.Protocol));
        }

        [Fact(Skip = SkipFailing)]
        public void Should_show_error_message_on_failure()
        {
            // Arrange
            using var ctx = new BunitContext();
            ConfigureTestServices(ctx,
                [
                    _localMqttConnection,
                ]);

            var service = ctx.Services.GetRequiredService<IMqttService>();
            service.When(s => s.Connect(Arg.Any<MqttConnection>()))
                .Do(_ => service.ErrorOccurred += Raise.Event<Func<string, Task>>("Test Message"));

            // Act
            var component = ctx.Render<MqttViewerComponent>();
            service.Connect(new());
            FindConnectButton(component).Click();
            FindConnectButton(component).Click();

            // Assert
            component.WaitForAssertion(() =>
            {
                component.Find(".content-container").InnerHtml.Should().Contain("Test Message");
            });
        }
    }

    public sealed class DisconnectClient : MqttViewerComponentTests
    {
        [Fact(Skip = SkipFailing)]
        public async Task Should_call_disconnect_on_client()
        {
            // Arrange
            await using var ctx = new BunitContext();
            ConfigureTestServices(ctx,
                [
                    _localMqttConnection,
                ]);

            var service = ctx.Services.GetRequiredService<IMqttService>();
            service.When(s => s.Connect(Arg.Any<MqttConnection>()))
                .Do(_ => service.Messages.Returns(
                [
                    new()
                    {
                        Message = "test",
                        MessageId = 1,
                        Qos = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce,
                        Retained = true,
                        Topic = "test",
                        Timestamp = DateTimeOffset.Now,
                    },
                ]));

            // Act
            var component = ctx.Render<MqttViewerComponent>();
            await FindConnectButton(component).ClickAsync();
            service.Connected += Raise.Event<Func<Task>>();
            service.IsConnected.Returns(true);

            await component.Find(DisconnectButtonSelector).ClickAsync();
            service.Disconnected += Raise.Event<Func<Task>>();

            // Assert
            await service.Received(1).Disconnect();
        }
    }

    public sealed class ClearButtonClicked : MqttViewerComponentTests
    {
        [Fact(Skip = SkipFailing)]
        public void Should_clear_message_list()
        {
            // Arrange
            using var ctx = new BunitContext();
            ConfigureTestServices(ctx,
                [
                    _localMqttConnection,
                ],
                [
                    _message,
                ]);

            // Act
            var component = ctx.Render<MqttViewerComponent>();
            FindConnectButton(component).Click();
            component.Find(ToggleTextFilterButtonSelector).Click();
            component.Find(ClearFilterButtonSelector).Click();

            // Assert
            component.Instance.MqttService.Received(1).ClearMessages();
        }
    }

    public sealed class ShowMessageDetails : MqttViewerComponentTests
    {
        [Fact(Skip = SkipFailing)]
        public void Should_show_message_in_message_detail_box_on_all_messages_grid_click()
        {
            // Arrange
            using var ctx = new BunitContext();
            ConfigureTestServices(ctx,
                [
                    _localMqttConnection,
                ],
                [
                    _message,
                ]);

            // Act
            var component = ctx.Render<MqttViewerComponent>();
            FindConnectButton(component).Click();

            // Assert
            component.FindAll("td").First(b => b.InnerHtml.Contains("test", StringComparison.InvariantCulture)).Click();
            component.WaitForAssertion(() =>
            {
                var memoEditors = component.FindAll(".message-details");
                memoEditors[memoEditors.Count - 1].OuterHtml.Should().Contain($"field-text=\"Topic: {_message.Topic}");
            });
        }

        [Fact(Skip = SkipFailing)]
        public void Should_show_message_details_on_filtered_messages_grid_click()
        {
            // Arrange
            using var ctx = new BunitContext();
            var message = new MessageModel
            {
                Topic = "house/room/temperature",
                Message = "test",
            };

            ConfigureTestServices(ctx,
                [
                    _localMqttConnection,
                ],
                [
                    _message,
                ]);

            // Act
            var component = ctx.Render<MqttViewerComponent>();
            FindConnectButton(component).Click();

            var service = ctx.Services.GetRequiredService<IMqttService>();
            service.MessagesDict.Add(message.Topic, message);
            service.MessageReceived += Raise.Event<Func<string, Task>>("house/room/temperature");
            component.Find(ToggleNodeFilterButtonSelector).Click();

            var viewerService = ctx.Services.GetRequiredService<MqttViewerComponentService>();
            var topicGroup = viewerService.TopicGroups.First();
            viewerService.SetNodeTopicFilter(topicGroup);
            component.Render();

            component.FindAll("td").First(e => e.InnerHtml.Contains("test", StringComparison.Ordinal)).Click();

            // Assert
            component.WaitForAssertion(() =>
            {
                component.Find(".message-details").OuterHtml.Should().Contain($"field-text=\"Topic: {message.Topic}");
            });
        }
    }

    public sealed class ReloadIntervalChanged : MqttViewerComponentTests
    {
        [Fact(Skip = SkipMessage)]
        public void Should_set_correct_reload_interval()
        {
            // Arrange
            using var ctx = new BunitContext();
            ConfigureTestServices(ctx,
                messages:
                [
                    _message,
                ]);

            // Act
            var component = ctx.Render<MqttViewerComponent>();

            // open combobox
            component.FindAll(".combo-box")
                .First(e => e.OuterHtml.Contains(MqttViewerComponent.DefaultReloadInterval.ToString(CultureInfo.InvariantCulture), StringComparison.InvariantCulture))
                .TriggerEvent("opendropdown", EventArgs.Empty);

            component.WaitForElements("td", _extraWaitTime)
                .First(b => b.InnerHtml.Contains("test", StringComparison.InvariantCulture)).Click();

            // Assert
            component.WaitForElement(".dropdown-body", _extraWaitTime)
                .InnerHtml.Contains(MqttViewerComponent.MinReloadInterval.ToString(CultureInfo.InvariantCulture), StringComparison.InvariantCulture);
        }
    }

    public sealed class BeforeExpandAndCollapseTreeView : MqttViewerComponentTests
    {
        [Fact(Skip = SkipFailing)]
        public void Should_expand_node_on_chevron_click()
        {
            // Arrange
            using var ctx = new BunitContext();
            ConfigureTestServices(ctx,
                [
                    _localMqttConnection,
                ],
                [
                    _message,
                ]);

            // Act
            var component = ctx.Render<MqttViewerComponent>();
            FindConnectButton(component).Click();

            var service = ctx.Services.GetRequiredService<IMqttService>();
            service.MessagesDict.Add("house/room/temperature", new());
            service.MessageReceived += Raise.Event<Func<string, Task>>("house/room/temperature");

            component.Find(ToggleNodeFilterButtonSelector).Click();
            component.Render();

            // Assert
            var viewerService = ctx.Services.GetRequiredService<MqttViewerComponentService>();
            viewerService.TopicGroups.First().SubTopics.Should().HaveCount(1);
            viewerService.TopicGroups.First().SubTopics[0].Topic.Should().Be("house");
        }
    }

    public sealed class OnFilterChanged : MqttViewerComponentTests
    {
        [Fact(Skip = SkipFailing)]
        public void Should_show_only_filtered_messages()
        {
            // Arrange
            var messageModels = new List<MessageModel>
            {
                new()
                {
                    Topic = "house/room/temperature",
                    Message = "20",
                },
                new()
                {
                    Topic = "house/room/humidity",
                    Message = "40",
                },
                new()
                {
                    Topic = "house/garden/temperature",
                    Message = "12",
                },
                new()
                {
                    Topic = "house/garden/humidity",
                    Message = "55",
                },
            };

            using var ctx = new BunitContext();
            ConfigureTestServices(ctx,
                [
                    _localMqttConnection,
                ],
                messageModels);

            // Act
            var component = ctx.Render<MqttViewerComponent>();
            FindConnectButton(component).Click();
            component.Find(ToggleTextFilterButtonSelector).Click();

            // Assert
            component.WaitForAssertion(() =>
            {
                var elements = component.FindAll(".mqtt-row, .mqtt-row-alt");
                elements.Count.Should().Be(2, "2 ");
            });
        }
    }

    public sealed class SetTreeList : MqttViewerComponentTests
    {
        [Fact(Skip = SkipFailing)]
        public void Should_set_correct_tree_on_incoming_message()
        {
            // Arrange
            using var ctx = new BunitContext();
            ConfigureTestServices(ctx,
                [
                    _localMqttConnection,
                ]);

            // Act
            var component = ctx.Render<MqttViewerComponent>();
            FindConnectButton(component).Click();

            var service = ctx.Services.GetRequiredService<IMqttService>();
            service.MessageReceived += Raise.Event<Func<string, Task>>("room/temperature/value");
            service.MessageReceived += Raise.Event<Func<string, Task>>("room/humidity/value");

            component.Find(ToggleNodeFilterButtonSelector).Click();
            component.Render();

            // Assert
            var viewerService = ctx.Services.GetRequiredService<MqttViewerComponentService>();
            viewerService.TopicGroups.First().SubTopics.Should().HaveCount(1);
            viewerService.TopicGroups.First().SubTopics[0].Topic.Should().Be("room");
        }
    }
}
