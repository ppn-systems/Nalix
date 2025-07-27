using Nalix.SDK.L10N;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Nalix.SDK.Tests.L10N;



public class MultiLocalizerTests
{
    // Mock Localizer để test MultiLocalizer, không cần file thực sự
    private class MockLocalizer(String lang = "default") : Localizer
    {
        private readonly String _lang = lang;

        public override String Get(String id) => $"[{_lang}] {id}";
    }

    // Mock MultiLocalizer để không cần file hệ thống
    private class TestableMultiLocalizer : MultiLocalizer
    {
        public void AddMock(String lang, Localizer localizer)
        {
            var field = typeof(MultiLocalizer).GetField("_localizers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var dict = (Dictionary<String, Localizer>)field.GetValue(this);
            dict[lang.ToLower()] = localizer;
        }

        public void SetDefaultMock(Localizer localizer)
        {
            var field = typeof(MultiLocalizer).GetField("_defaultLocalizer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(this, localizer);
        }
    }

    [Fact]
    public void Contains_ReturnsTrue_WhenLanguageIsLoaded()
    {
        var multi = new TestableMultiLocalizer();
        multi.AddMock("en", new MockLocalizer("en"));
        Assert.True(multi.Contains("en"));
    }

    [Fact]
    public void Contains_ReturnsFalse_WhenLanguageIsNotLoaded()
    {
        var multi = new TestableMultiLocalizer();
        Assert.False(multi.Contains("fr"));
    }

    [Fact]
    public void Get_ReturnsCorrectLocalizer_IfExists()
    {
        var multi = new TestableMultiLocalizer();
        var enLocalizer = new MockLocalizer("en");
        multi.AddMock("en", enLocalizer);
        Assert.Same(enLocalizer, multi.Get("en"));
    }

    [Fact]
    public void Get_ReturnsDefaultLocalizer_IfNotExists()
    {
        var multi = new TestableMultiLocalizer();
        var defaultLocalizer = new MockLocalizer("default");
        multi.SetDefaultMock(defaultLocalizer);
        Assert.Same(defaultLocalizer, multi.Get("fr"));
    }

    [Fact]
    public void TryGet_ReturnsTrueAndCorrectLocalizer_IfExists()
    {
        var multi = new TestableMultiLocalizer();
        var enLocalizer = new MockLocalizer("en");
        multi.AddMock("en", enLocalizer);

        var result = multi.TryGet("en", out var localizer);
        Assert.True(result);
        Assert.Same(enLocalizer, localizer);
    }

    [Fact]
    public void TryGet_ReturnsFalseAndDefaultLocalizer_IfNotExists()
    {
        var multi = new TestableMultiLocalizer();
        var defaultLocalizer = new MockLocalizer("default");
        multi.SetDefaultMock(defaultLocalizer);

        var result = multi.TryGet("fr", out var localizer);
        Assert.False(result);
        Assert.Same(defaultLocalizer, localizer);
    }

    [Fact]
    public void GetLanguages_ReturnsAllLoadedLanguages()
    {
        var multi = new TestableMultiLocalizer();
        multi.AddMock("en", new MockLocalizer("en"));
        multi.AddMock("fr", new MockLocalizer("fr"));
        var langs = multi.GetLanguages();
        Assert.Contains("en", langs);
        Assert.Contains("fr", langs);
        Assert.Equal(2, langs.Length);
    }

    [Fact]
    public void SetDefault_SetsDefaultLocalizer_WhenExists()
    {
        var multi = new TestableMultiLocalizer();
        var frLocalizer = new MockLocalizer("fr");
        multi.AddMock("fr", frLocalizer);
        multi.SetDefault("fr");
        Assert.Same(frLocalizer, multi.GetDefault());
    }

    [Fact]
    public void SetDefault_ThrowsArgumentException_IfLanguageNotExists()
    {
        var multi = new TestableMultiLocalizer();
        _ = Assert.Throws<ArgumentException>(() => multi.SetDefault("es"));
    }

    [Fact]
    public void GetDefault_ReturnsDefaultLocalizer()
    {
        var multi = new TestableMultiLocalizer();
        var defaultLocalizer = new MockLocalizer("default");
        multi.SetDefaultMock(defaultLocalizer);
        Assert.Same(defaultLocalizer, multi.GetDefault());
    }

    // Nếu bạn muốn test Load thực sự, cần chuẩn bị file thực tế và kiểm tra ngoại lệ
    [Fact]
    public void Load_ThrowsFileNotFoundException_IfFileNotExists()
    {
        var multi = new MultiLocalizer();
        _ = Assert.Throws<FileNotFoundException>(() => multi.Load("en", "notfound.po"));
    }
}