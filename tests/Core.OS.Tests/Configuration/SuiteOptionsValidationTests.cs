using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Core.Artifacts;
using Core.Module.Options;
using Core.OS.Configuration;
using Core.OS.Connections.Mqtt;
using Core.OS.Extensions;
using Core.OS.Instance;
using Core.OS.Logging;
using Core.OS.Logging.Extensions;
using Core.OS.Mail;
using Core.OS.Mail.Extensions;
using Core.OS.MessageBus.MassTransit.Configuration;
using Core.OS.Monitoring.Extensions;
using Core.OS.UserManagement.Configuration;
using Core.Shared.HostManagement;
using Core.Shared.Monitoring;
using Core.Shared.UserManagement.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Core.OS.Tests.Configuration;

/// <summary>
/// Covers the guarantee that <c>AddValidatedOptions</c> exists to give: a value outside the bounds an options type
/// declares stops the instance at startup instead of binding silently. Before the source-generated validators the
/// registration called <c>ValidateDataAnnotations()</c>, which only ever looked at the top-level properties, so
/// every <c>[Range]</c> on a nested settings class was decorative — see ADR-004.
/// </summary>
public class SuiteOptionsValidationTests
{
    /// <summary>
    /// The nested bounds that used to bind silently. <c>MessageBus:KillSwitch:TripThresholdPercent = 500</c> is the
    /// symptom ADR-004 recorded: it left the kill switch unable to trip, because no failure rate ever reaches 500 %.
    /// </summary>
    public sealed class NestedBounds : SuiteOptionsValidationTests
    {
        [Theory]
        [InlineData("MessageBus:KillSwitch:TripThresholdPercent", "500")]
        [InlineData("MessageBus:KillSwitch:TripThresholdPercent", "0")]
        [InlineData("MessageBus:KillSwitch:ActivationThreshold", "0")]
        [InlineData("MessageBus:KillSwitch:TrackingPeriodInSeconds", "86400")]
        [InlineData("MessageBus:ErrorQueue:RetentionInDays", "400")]
        [InlineData("MessageBus:ErrorQueue:MaxSizeInMegabytes", "4096")]
        public void Should_fail_startup_when_a_nested_message_bus_bound_is_violated(string key, string value)
        {
            // Arrange
            var host = BuildHost(
                services => services.AddValidatedOptions<MessageBusOptions, MessageBusOptionsValidator>(MessageBusOptions.ConfigSection),
                new Dictionary<string, string?> { [key] = value });

            // Act
            var failures = host.GetInvalidOptions();

            // Assert
            var settingName = key.Split(':').Last();
            failures.Should().NotBeNull();
            failures.Should().ContainSingle(f => f.Contains(settingName, StringComparison.Ordinal));
        }

        [Fact]
        public void Should_fail_startup_when_a_doubly_nested_logging_bound_is_violated()
        {
            // Arrange — Logging:Resources:Memory sits two levels below the bound section
            var host = BuildHost(
                services => services.AddValidatedOptions<LoggingOptions, LoggingOptionsValidator>(LoggingOptions.ConfigSection),
                new Dictionary<string, string?> { ["Logging:Resources:Memory:LimitInPercent"] = "150" });

            // Act
            var failures = host.GetInvalidOptions();

            // Assert
            failures.Should().NotBeNull();
            failures.Should().ContainSingle(f => f.Contains(nameof(LoggingMemoryOptions.LimitInPercent), StringComparison.Ordinal));
        }

        [Fact]
        public void Should_fail_startup_when_a_nested_mqtt_port_is_out_of_range()
        {
            // Arrange
            var host = BuildHost(
                services => services.AddValidatedOptions<MqttClientOptions, MqttClientOptionsValidator>(MqttClientOptions.ConfigSection),
                new Dictionary<string, string?> { ["MqttClient:ServiceClient:Port"] = "70000" });

            // Act
            var failures = host.GetInvalidOptions();

            // Assert
            failures.Should().NotBeNull();
            failures.Should().ContainSingle(f => f.Contains(nameof(MqttConnectionOptions.Port), StringComparison.Ordinal));
        }

        [Fact]
        public void Should_fail_startup_when_an_enumerated_external_id_provider_is_incomplete()
        {
            // Arrange — [ValidateEnumeratedItems] was inert for the same reason [ValidateObjectMembers] was
            var host = BuildHost(
                services => services.AddValidatedOptions<ExternalIdProviderOptions, ExternalIdProviderOptionsValidator>(
                    ExternalIdProviderOptions.ConfigSection),
                new Dictionary<string, string?>
                {
                    ["ExternalIdProviders:Providers:0:Name"] = "ifm",
                    ["ExternalIdProviders:Providers:0:Authority"] = "not-a-url",
                    ["ExternalIdProviders:Providers:0:ClientId"] = "suite",
                });

            // Act
            var failures = host.GetInvalidOptions();

            // Assert
            failures.Should().NotBeNull();
            failures.Should().ContainSingle(f => f.Contains(nameof(ExternalIdProvider.Authority), StringComparison.Ordinal));
        }

        [Fact]
        public void Should_start_when_the_nested_values_are_within_their_bounds()
        {
            // Arrange — the same knobs as above, at the edges of their declared ranges
            var host = BuildHost(
                services =>
                {
                    services.AddValidatedOptions<MessageBusOptions, MessageBusOptionsValidator>(MessageBusOptions.ConfigSection);
                    services.AddValidatedOptions<LoggingOptions, LoggingOptionsValidator>(LoggingOptions.ConfigSection);
                    services.AddValidatedOptions<MqttClientOptions, MqttClientOptionsValidator>(MqttClientOptions.ConfigSection);
                },
                new Dictionary<string, string?>
                {
                    ["MessageBus:KillSwitch:TripThresholdPercent"] = "100",
                    ["MessageBus:KillSwitch:ActivationThreshold"] = "1",
                    ["MessageBus:KillSwitch:TrackingPeriodInSeconds"] = "3600",
                    ["MessageBus:ErrorQueue:RetentionInDays"] = "0",
                    ["MessageBus:ErrorQueue:MaxSizeInMegabytes"] = "1024",
                    ["Logging:Resources:Memory:LimitInPercent"] = "100",
                    ["MqttClient:ServiceClient:Port"] = "65535",
                });

            // Act
            var failures = host.GetInvalidOptions();

            // Assert
            failures.Should().BeNull();
        }

        [Fact]
        public void Should_start_on_the_shipped_defaults()
        {
            // Arrange — nothing configured at all, so every options type binds its own defaults
            var host = BuildHost(
                services =>
                {
                    services.AddValidatedOptions<MessageBusOptions, MessageBusOptionsValidator>(MessageBusOptions.ConfigSection);
                    services.AddValidatedOptions<LoggingOptions, LoggingOptionsValidator>(LoggingOptions.ConfigSection);
                    services.AddValidatedOptions<MqttClientOptions, MqttClientOptionsValidator>(MqttClientOptions.ConfigSection);
                    services.AddValidatedOptions<UserManagementOptions, UserManagementOptionsValidator>(UserManagementOptions.ConfigSection);
                    services.AddValidatedOptions<ExternalIdProviderOptions, ExternalIdProviderOptionsValidator>(ExternalIdProviderOptions.ConfigSection);
                },
                []);

            // Act
            var failures = host.GetInvalidOptions();

            // Assert
            failures.Should().BeNull();
        }
    }

    /// <summary>
    /// Fitness checks on the registration convention itself. The compiler already makes an unvalidated options type
    /// impossible to register through <c>AddValidatedOptions</c> — the validator is a required type argument — so what is
    /// left to guard is the two things a type argument cannot express: a type bound to configuration by some other
    /// route, and a nested settings class nobody marked <c>[ValidateObjectMembers]</c>.
    /// </summary>
    public sealed class RegistrationConvention : SuiteOptionsValidationTests
    {
        /// <summary>
        /// Options types deliberately bound without validation. Pinned here so that adding one is a review decision
        /// rather than an oversight; the reason for each is stated at its registration.
        /// <c>MassTransit.RabbitMqTransportOptions</c> belongs to this set too, but its registration lives inside
        /// <c>AddMassTransitMessageBus</c>, which is out of this sweep — see the comment there and ADR-004.
        /// </summary>
        private static readonly Type[] ExemptOptionsTypes =
        [
            typeof(ArtifactRepositoryOptions),
            typeof(HostManagementOptions),
            typeof(ModuleLoaderOptions),
            typeof(SmtpMailOptions),
            typeof(SystemMonitoringOptions),
        ];

        /// <summary>
        /// Pins what the sweep looks at, so the two checks below cannot pass by finding nothing. Adding a
        /// configuration-bound options type to the suite is expected to fail this test and be listed here.
        /// </summary>
        [Fact]
        public void Should_sweep_every_options_type_the_suite_binds_to_configuration()
        {
            // Arrange
            var services = BuildSuiteRegistrations();

            // Act
            var bound = BoundOptionsTypes(services);

            // Assert
            bound.Should().BeEquivalentTo(
            [
                typeof(ArtifactRepositoryOptions),
                typeof(ExternalIdProviderOptions),
                typeof(HostManagementOptions),
                typeof(InstanceOptions),
                typeof(LoggingOptions),
                typeof(MessageBusOptions),
                typeof(ModuleLoaderOptions),
                typeof(MqttClientOptions),
                typeof(SmtpMailOptions),
                typeof(SystemMonitoringOptions),
                typeof(UserManagementOptions),
            ]);
        }

        [Fact]
        public void Should_validate_every_options_type_bound_to_a_configuration_section()
        {
            // Arrange
            var services = BuildSuiteRegistrations();

            // Act
            var unvalidated = BoundOptionsTypes(services)
                .Where(t => !services.Any(d => d.ServiceType == typeof(IValidateOptions<>).MakeGenericType(t)))
                .ToArray();

            // Assert
            unvalidated.Should().BeEmpty(
                "every options type bound to a configuration section must be registered through AddValidatedOptions "
                + "or, with a stated reason, AddUnvalidatedSuiteOptions");
        }

        [Fact]
        public void Should_not_exempt_any_options_type_beyond_the_pinned_set()
        {
            // Arrange
            var services = BuildSuiteRegistrations();

            // Act
            var exempt = BoundOptionsTypes(services)
                .Where(t => services.Any(d =>
                    d.ServiceType == typeof(IValidateOptions<>).MakeGenericType(t)
                    && d.ImplementationInstance?.GetType().GetGenericTypeDefinition() == typeof(UnvalidatedSuiteOptions<>)))
                .ToArray();

            // Assert
            exempt.Should().BeEquivalentTo(ExemptOptionsTypes);
        }

        [Fact]
        public void Should_mark_every_nested_settings_class_that_has_something_to_validate()
        {
            // Arrange — a nested class the validator cannot reach is exactly how the [Range] bounds went inert
            var services = BuildSuiteRegistrations();
            var validated = BoundOptionsTypes(services).Except(ExemptOptionsTypes);

            // Act
            var unreachable = validated.SelectMany(UnreachableAnnotatedMembers).ToArray();

            // Assert
            unreachable.Should().BeEmpty(
                "a nested settings class carrying validation attributes is only validated when the property holding "
                + "it is marked [ValidateObjectMembers] (or [ValidateEnumeratedItems] for a collection)");
        }

        private static ServiceCollection BuildSuiteRegistrations()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

            services.AddCoreOptions();
            services.AddMailing();
            services.AddMqttServices();
            services.AddSystemMonitoring(new ConfigurationBuilder().Build());
            services.AddCoreLogging();
            return services;
        }

        /// <summary>
        /// The options types bound to a configuration section: <c>BindConfiguration</c> is what registers an
        /// <see cref="IOptionsChangeTokenSource{TOptions}"/>, so it identifies exactly the types whose values come
        /// from <c>appsettings.json</c> or the environment and therefore need bounds enforced.
        /// </summary>
        private static IEnumerable<Type> BoundOptionsTypes(IServiceCollection services)
            => services
                .Where(d => d.ServiceType.IsGenericType
                            && d.ServiceType.GetGenericTypeDefinition() == typeof(IOptionsChangeTokenSource<>))
                .Select(d => d.ServiceType.GetGenericArguments()[0])
                .Distinct();

        private static IEnumerable<string> UnreachableAnnotatedMembers(Type optionsType)
            => UnreachableAnnotatedMembers(optionsType, []);

        private static IEnumerable<string> UnreachableAnnotatedMembers(Type optionsType, HashSet<Type> visited)
        {
            if (!visited.Add(optionsType))
                yield break;

            foreach (var property in optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var nested = NestedSuiteType(property.PropertyType);
                if (nested is null || !HasValidationAttributes(nested, []))
                    continue;

                var marker = IsCollection(property.PropertyType)
                    ? typeof(ValidateEnumeratedItemsAttribute)
                    : typeof(ValidateObjectMembersAttribute);

                if (property.GetCustomAttribute(marker) is null)
                    yield return $"{optionsType.Name}.{property.Name} ({nested.Name}) is missing [{marker.Name}]";

                foreach (var deeper in UnreachableAnnotatedMembers(nested, visited))
                    yield return deeper;
            }
        }

        /// <summary>
        /// The suite-owned complex type behind a property, unwrapping nullables and collections. Types the suite does
        /// not own are skipped: it does not annotate them, so it cannot make them validate either.
        /// </summary>
        private static Type? NestedSuiteType(Type propertyType)
        {
            var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (IsCollection(type))
            {
                var element = type.IsArray
                    ? type.GetElementType()
                    : type.GetInterfaces()
                        .Concat([type])
                        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                        ?.GetGenericArguments()[0];

                return element is null ? null : NestedSuiteType(element);
            }

            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(TimeSpan)
                || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(decimal))
                return null;

            return type.Namespace?.StartsWith("Core.", StringComparison.Ordinal) == true ? type : null;
        }

        private static bool IsCollection(Type type)
            => type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type);

        private static bool HasValidationAttributes(Type type, HashSet<Type> visited)
        {
            if (!visited.Add(type))
                return false;

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetCustomAttributes<ValidationAttribute>(inherit: true).Any())
                    return true;

                var nested = NestedSuiteType(property.PropertyType);
                if (nested is not null && HasValidationAttributes(nested, visited))
                    return true;
            }

            return false;
        }
    }

    private static WebApplication BuildHost(Action<IServiceCollection> register, Dictionary<string, string?> configuration)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(configuration);
        register(builder.Services);

        return builder.Build();
    }
}
