using System.Globalization;
using System.Windows;
using MoonbeamAudioReceiver.Converters;
using Xunit;

namespace MoonbeamAudioReceiver.Tests;

public sealed class ConvertersTests
{
    [Theory]
    [InlineData(true, Visibility.Visible)]
    [InlineData(false, Visibility.Collapsed)]
    public void BooleanToVisibilityConverter_ConvertsCorrectly(bool input, Visibility expected)
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.Convert(input, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, Visibility.Collapsed)]
    [InlineData(false, Visibility.Visible)]
    public void InverseBooleanToVisibilityConverter_ConvertsCorrectly(bool input, Visibility expected)
    {
        var converter = new InverseBooleanToVisibilityConverter();
        var result = converter.Convert(input, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void InverseBooleanConverter_InvertsCorrectly(bool input, bool expected)
    {
        var converter = new InverseBooleanConverter();
        var result = converter.Convert(input, typeof(bool), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }
}
