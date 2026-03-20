using System.Globalization;
using System.Text.Json;
using AngleSharp.Dom;
using AwesomeAssertions;
using Blazor.Shared.Mqtt;
using Blazor.Shared.Mqtt.Contracts;
using Blazor.Shared.Mqtt.Helpers;
using Blazor.Shared.Mqtt.Services;
using Blazor.Tests.Tools;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet.Protocol;
using NSubstitute;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;
using Sdk.Connections.Requests;
using Xunit;

namespace Blazor.Shared.Tests.Mqtt;

public class MqttViewerComponentTests
{
    private const string SkipMessage = "#709 - There some timing issue with the async popup rendering that fails in pipeline";
    private const string SkipFailing = "#80x - These tests need to be refactored because they fail to often";

    private const string FilterButtonSelector = ".monochrome-icon-search";
    private const string ExpandNodeButtonSelector = ".monochrome-icon-expander-light-down";
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
        Qos = MqttQualityOfServiceLevel.AtMostOnce,
    };

    private static IElement FindConnectButton(IRenderedComponent<MqttViewerComponent> component)
        => component.Find(ConnectButtonSelector);

    private static void ConfigureTestServices(BunitContext ctx, List<Connection>? connections = null, List<MessageModel>? messages = null)
    {
        ctx.SetupSuiteServicesWithBlazorDx(s =>
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
    }

    public class OnInitializedAsync : MqttViewerComponentTests
    {
        [Fact(Skip = SkipFailing)]
        public void Sets_variables()
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
                            Protocol = MqttConnectionType.TCPWithTLS,
                        }),
                        Name = "test Connection2"
                    },
                ]);

            var component = ctx.Render<MqttViewerComponent>();

            var input = component.Find(DxSelectors.TextEditCss);
            input.OuterHtml.Should().Contain("field-text=\"test Connection1\"");
        }

        [Fact(Skip = SkipMessage)]
        public void Sets_message_when_no_connections_are_configured()
        {
            // Arrange
            using var ctx = new BunitContext();
            ConfigureTestServices(ctx);

            // Act
            var component = ctx.Render<MqttViewerComponent>();

            // Assert
            component.WaitForElement(".dxbl-modal-content", _extraWaitTime).InnerHtml.Should().Contain("no_connections");
        }
    }

    public class ConnectClient : MqttViewerComponentTests
    {
        [Fact(Skip = SkipFailing)]
        public async Task Returns_when_selected_connection_is_null()
        {
            // Arrange
            await using var ctx = new BunitContext();
            ConfigureTestServices(ctx);

            // Act
            var component = ctx.Render<MqttViewerComponent>();
            FindConnectButton(component).Click();

            // Assert
            var service = ctx.Services.GetRequiredService<IMqttService>();
            await service.Received(0).Connect(Arg.Any<MqttConnection>());
        }

        [Fact(Skip = SkipFailing)]
        public async Task Calls_connect_on_client()
        {
            // Arrange
            await using var ctx = new BunitContext();
            ConfigureTestServices(ctx,
                [
                    _localMqttConnection,
                ]);

            // Act
            var component = ctx.Render<MqttViewerComponent>();
            FindConnectButton(component).Click();

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
        public void Shows_error_message_on_failure()
        {
            // Arrange
            using var ctx = new BunitContext();
            ConfigureTestServices(ctx,
                [
                    _localMqttConnection,
                ]);

            var service = ctx.Services.GetRequiredService<IMqttService>();
            service.When(s => s.Connect(Arg.Any<MqttConnection>()))
                .Do(_ => service.ErrorOccured += Raise.Event<Func<string, Task>>("Test Message"));

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

    public class DisconnectClient : MqttViewerComponentTests
    {
        [Fact(Skip = SkipFailing)]
        public async Task Calls_disconnect_on_client()
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
                        Qos = MqttQualityOfServiceLevel.AtMostOnce,
                        Retained = true,
                        Topic = "test",
                        Timestamp = DateTimeOffset.Now,
                    },
                ]));

            // Act
            var component = ctx.Render<MqttViewerComponent>();
            FindConnectButton(component).Click();
            service.Connected += Raise.Event<Func<Task>>();
            service.IsConnected.Returns(true);

            component.Find(DisconnectButtonSelector).Click();
            service.Disconnected += Raise.Event<Func<Task>>();

            // Assert
            await service.Received(1).Disconnect();
        }
    }

    public class ClearButtonClicked : MqttViewerComponentTests
    {
        [Fact(Skip = SkipFailing)]
        public void Clears_message_list()
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

    public class ShowMessageDetails : MqttViewerComponentTests
    {
        [Fact(Skip = SkipFailing)]
        public void Shows_message_in_message_detail_box_on_all_messages_grid_click()
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
                var memoEditors = component.FindAll(DxSelectors.MemoEditor);
                memoEditors[memoEditors.Count - 1].OuterHtml.Should().Contain($"field-text=\"Topic: {_message.Topic}");
            });
        }

        [Fact(Skip = SkipFailing)]
        public void Shows_message_details_on_filtered_messages_grid_click()
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
            component.Find(ExpandNodeButtonSelector).Click();
            component.FindAll(FilterButtonSelector).Last().Click();
            component.FindAll("td").First(e => e.InnerHtml.Contains("test", StringComparison.Ordinal)).Click();

            // Assert
            component.WaitForAssertion(() =>
            {
                component.Find(DxSelectors.MemoEditor).OuterHtml.Should().Contain($"field-text=\"Topic: {message.Topic}");
            });
        }
    }

    public class ReloadIntervalChanged : MqttViewerComponentTests
    {
        [Fact(Skip = SkipMessage)]
        public void Set_correct_reload_interval()
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
            component.FindAll(DxSelectors.ComboBox)
                .First(e => e.OuterHtml.Contains(MqttViewerComponent.DefaultReloadInterval.ToString(CultureInfo.InvariantCulture), StringComparison.InvariantCulture))
                .TriggerEvent("ondxbl-dropdownbase.opendropdown", EventArgs.Empty);

            component.WaitForElements("td", _extraWaitTime)
                .First(b => b.InnerHtml.Contains("test", StringComparison.InvariantCulture)).Click();

            // Assert
            component.WaitForElement(".dxbl-dropdown-body", _extraWaitTime)
                .InnerHtml.Contains(MqttViewerComponent.MinReloadInterval.ToString(CultureInfo.InvariantCulture), StringComparison.InvariantCulture);
        }
    }

    public class BeforeExpandAndCollapseTreeView : MqttViewerComponentTests
    {
        [Fact(Skip = SkipFailing)]
        public void Expands_note_on_chevron_click()
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
            component.Find(ExpandNodeButtonSelector).Click();
            component.Render();

            // Assert
            var elements = component.FindAll(".tree-element");
            elements.Count.Should().Be(2);
            elements[0].InnerHtml.Should().Contain(MqttViewerConstants.AllTopicText);
            elements[^1].InnerHtml.Should().Contain("house");
        }

        [Fact(Skip = SkipFailing)]
        public void Does_not_expand_on_magnify_click()
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
            service.MessagesDict.Add("house/room/temperature", new());
            service.MessageReceived += Raise.Event<Func<string, Task>>("house/room/temperature");

            component.Find(ToggleNodeFilterButtonSelector).Click();
            component.Find(FilterButtonSelector).Click();

            // Assert
            var elements = component.FindAll(".tree-element");
            elements.Count.Should().Be(1);
            elements[0].InnerHtml.Should().Contain(MqttViewerConstants.AllTopicText);
        }
    }

    public class OnFilterChanged : MqttViewerComponentTests
    {
        [Fact(Skip = SkipFailing)]
        public void Shows_only_filtered_messages()
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

    public class SetTreeList : MqttViewerComponentTests
    {
        [Fact(Skip = SkipFailing)]
        public void Sets_correct_tree_on_incoming_message()
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
            component.Find(ExpandNodeButtonSelector).Click();
            component.Render();

            // Assert
            component.WaitForAssertion(() =>
            {
                var elements = component.FindAll(".tree-element");
                elements.Count.Should().Be(2);
                elements[0].InnerHtml.Should().Contain(MqttViewerConstants.AllTopicText);
                elements[^1].InnerHtml.Should().Contain("room");
            });
        }
    }
}
