using Nalix.SDK.L10N;
using System;
using Xunit;

namespace Nalix.SDK.Tests.L10N;

public class LocalizationTests
{
    // Mock Localizer để kiểm tra các phương thức của Localization.
    private class MockLocalizer : Localizer
    {
        public override String Get(String id) => id == "hello" ? "xin chào" : id;

        public override String GetParticular(String context, String id)
            => context == "menu" && id == "File" ? "Tập tin" : id;

        public override String GetPlural(String id, String idPlural, Int32 n)
            => id == "apple" && idPlural == "apples" ? n > 1 ? "táo" : "táo" : n > 1 ? idPlural : id;

        public override String GetParticularPlural(String context, String id, String idPlural, Int32 n)
            => context == "inventory" && id == "item" &&
               idPlural == "items" ? n > 1 ? "vật phẩm" : "vật phẩm" : n > 1 ? idPlural : id;
    }

    public LocalizationTests() =>
        // Thiết lập Localizer mock cho mỗi lần test
        Localization.SetLocalizer(new MockLocalizer());

    [Fact]
    public void Get_ReturnsTranslatedString_WhenTranslationExists()
    {
        var result = Localization.Get("hello");
        Assert.Equal("xin chào", result);
    }

    [Fact]
    public void Get_ReturnsId_WhenTranslationDoesNotExist()
    {
        var result = Localization.Get("notfound");
        Assert.Equal("notfound", result);
    }

    [Fact]
    public void GetParticular_ReturnsTranslatedString_WhenContextAndIdMatch()
    {
        var result = Localization.GetParticular("menu", "File");
        Assert.Equal("Tập tin", result);
    }

    [Fact]
    public void GetParticular_ReturnsId_WhenNoContextMatch()
    {
        var result = Localization.GetParticular("other", "File");
        Assert.Equal("File", result);
    }

    [Theory]
    [InlineData(1, "táo")]
    [InlineData(5, "táo")]
    public void GetPlural_ReturnsCorrectForm(Int32 n, String expected)
    {
        var result = Localization.GetPlural("apple", "apples", n);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetPlural_ReturnsIdOrIdPlural_WhenNoTranslationExists()
    {
        Assert.Equal("singular", Localization.GetPlural("singular", "plural", 1));
        Assert.Equal("plural", Localization.GetPlural("singular", "plural", 2));
    }

    [Theory]
    [InlineData(1, "vật phẩm")]
    [InlineData(3, "vật phẩm")]
    public void GetParticularPlural_ReturnsCorrectForm(Int32 n, String expected)
    {
        var result = Localization.GetParticularPlural("inventory", "item", "items", n);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetParticularPlural_ReturnsIdOrIdPlural_WhenNoTranslationExists()
    {
        Assert.Equal("item", Localization.GetParticularPlural("other", "item", "items", 1));
        Assert.Equal("items", Localization.GetParticularPlural("other", "item", "items", 5));
    }

    [Fact]
    public void SetLocalizer_ChangesLocalizerInstance()
    {
        var localizer1 = new MockLocalizer();
        Localization.SetLocalizer(localizer1);
        Assert.Equal("xin chào", Localization.Get("hello"));

        var localizer2 = new MockLocalizer();
        Localization.SetLocalizer(localizer2);
        Assert.Equal("xin chào", Localization.Get("hello"));
    }
}