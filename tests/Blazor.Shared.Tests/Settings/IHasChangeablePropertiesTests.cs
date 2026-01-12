using System.Linq.Expressions;
using System.Reflection;
using Blazor.Shared.Interfaces;
using AwesomeAssertions;

namespace Blazor.Shared.Tests.Settings;

internal sealed class IHasChangeablePropertiesTests<T>
    where T : IHasChangeableProperties, new()
{
    public void AssertChangedEventHandlingWhenPropertyIsSet<TProperty>(Expression<Func<T, TProperty>> propertySelector,
        TProperty initialValue, TProperty value, bool shouldTriggerChangedEvent)
    {
        // Arrange
        var memberExpression = propertySelector.Body as MemberExpression;
        if (memberExpression == null)
            throw new ArgumentException($"Expression is not a {nameof(MemberExpression)}", nameof(propertySelector));

        var propertyInfo = memberExpression.Member as PropertyInfo;
        if (propertyInfo == null)
            throw new ArgumentException("Expression must select a property", nameof(propertySelector));

        var propertyName = propertyInfo.Name;

        var propertySetter = propertyInfo.GetSetMethod();
        if (propertySetter == null)
            throw new ArgumentException("Property must have a set accessor", nameof(propertySelector));

        var changedTriggered = false;

        var state = new T();
        propertySetter.Invoke(state, [initialValue]);

        state.Changed += args => changedTriggered = args.PropertyNames.Contains(propertyName);

        // Act
        propertySetter.Invoke(state, [value]);

        // Assert
        changedTriggered.Should().Be(shouldTriggerChangedEvent);
    }
}
