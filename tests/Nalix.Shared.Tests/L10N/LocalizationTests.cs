using Nalix.Shared.L10N;
using System.IO;
using Xunit;

namespace Nalix.Shared.Tests.L10N;

public class LocalizationTests
{
    public LocalizationTests()
    {
        // Create a temporary PO file for testing
        _mockPoFilePath = Path.GetTempFileName();
        File.WriteAllText(_mockPoFilePath, MockPoFileContent);
    }

    [Fact]
    public void Localizer_Get_ReturnsTranslatedString()
    {
        // Arrange
        var localizer = new Localizer(_mockPoFilePath);

        // Act
        string result = localizer.Get("hello");

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void Localizer_GetParticular_ReturnsTranslatedStringForContext()
    {
        // Arrange
        var localizer = new Localizer(_mockPoFilePath);

        // Act
        string result = localizer.GetParticular("menu", "file");

        // Assert
        Assert.Equal("file", result);
    }

    [Fact]
    public void Localizer_GetPlural_ReturnsSingularForCountOne()
    {
        // Arrange
        var localizer = new Localizer(_mockPoFilePath);

        // Act
        string result = localizer.GetPlural("item", "items", 1);

        // Assert
        Assert.Equal("one item", result);
    }

    [Fact]
    public void Localizer_GetPlural_ReturnsPluralForCountGreaterThanOne()
    {
        // Arrange
        var localizer = new Localizer(_mockPoFilePath);

        // Act
        string result = localizer.GetPlural("item", "items", 5);
        System.Diagnostics.Debug.WriteLine(result);

        // Assert
        Assert.Equal("5 items", result);
    }

    //[Fact]
    //public void Localizer_GetParticularPlural_ReturnsContextualPluralTranslation()
    //{
    //    // Arrange
    //    var localizer = new Localizer(_mockPoFilePath);

    //    // Act
    //    string result = localizer.GetParticularPlural("inventory", "apple", "apples", 3);
    //    Debug.WriteLine($"Result: '{result}'");

    //    // Assert
    //    Assert.Equal("3 apples", result);
    //}

    //[Fact]
    //public void Localization_StaticMethods_UseSetLocalizer()
    //{
    //    // Arrange
    //    var localizer = new Localizer(_mockPoFilePath);
    //    Localization.SetLocalizer(localizer);

    //    // Act
    //    string result1 = Localization.Get("hello");
    //    string result2 = Localization.GetParticular("menu", "file");
    //    string result3 = Localization.GetPlural("item", "items", 1);
    //    string result4 = Localization.GetParticularPlural("inventory", "apple", "apples", 3);

    //    // Assert
    //    Assert.Equal("Hello World", result1);
    //    Assert.Equal("file", result2);
    //    Assert.Equal("one item", result3);
    //    Assert.Equal("3 apples", result4);
    //}

    [Fact]
    public void MultiLocalizer_LoadAndGet_ManagesMultipleLocalizers()
    {
        // Arrange
        var multiLocalizer = new MultiLocalizer();
        multiLocalizer.Load("en", _mockPoFilePath);

        // Act
        bool hasLanguage = multiLocalizer.Contains("en");
        Localizer enLocalizer = multiLocalizer.Get("en");
        string translation = enLocalizer.Get("hello");

        // Assert
        Assert.True(hasLanguage);
        Assert.Equal("Hello World", translation);
    }

    [Fact]
    public void MultiLocalizer_SetDefault_ChangesDefaultLocalizer()
    {
        // Arrange
        var multiLocalizer = new MultiLocalizer();
        multiLocalizer.Load("en", _mockPoFilePath);

        // Act
        multiLocalizer.SetDefault("en");
        Localizer defaultLocalizer = multiLocalizer.GetDefault();
        string translation = defaultLocalizer.Get("hello");

        // Assert
        Assert.Equal("Hello World", translation);
    }

    [Fact]
    public void MultiLocalizer_TryGet_ReturnsSuccessWhenLanguageExists()
    {
        // Arrange
        var multiLocalizer = new MultiLocalizer();
        multiLocalizer.Load("en", _mockPoFilePath);

        // Act
        bool success = multiLocalizer.TryGet("en", out var localizer);

        // Assert
        Assert.True(success);
        Assert.NotNull(localizer);
        Assert.Equal("Hello World", localizer.Get("hello"));
    }

    [Fact]
    public void MultiLocalizer_GetLanguages_ReturnsAllLoadedLanguages()
    {
        // Arrange
        var multiLocalizer = new MultiLocalizer();
        multiLocalizer.Load("en", _mockPoFilePath);
        multiLocalizer.Load("fr", _mockPoFilePath); // Using same file for simplicity

        // Act
        string[] languages = multiLocalizer.GetLanguages();

        // Assert
        Assert.Contains("en", languages);
        Assert.Contains("fr", languages);
        Assert.Equal(2, languages.Length);
    }

    internal void Dispose()
    {
        // Clean up the temporary file
        if (File.Exists(_mockPoFilePath))
        {
            File.Delete(_mockPoFilePath);
        }
    }

    private readonly string _mockPoFilePath;

    // Mock PO file content for testing
    private const string MockPoFileContent = @"
msgid """"
msgstr """"
""Language: en\n""
""MIME-Version: 1.0\n""
""Content-Type: text/plain; charset=UTF-8\n""
""Content-Transfer-Encoding: 8bit\n""
""Plural-Forms: nplurals=2; plural=(n != 1);\n""

msgid ""hello""
msgstr ""Hello World""

msgctxt ""menu""
msgid ""file""
msgstr ""File""

msgid ""item""
msgid_plural ""items""
msgstr[0] ""one item""
msgstr[1] ""%d items""

msgctxt ""inventory""
msgid ""apple""
msgid_plural ""apples""
msgstr[0] ""one apple""
msgstr[1] ""%d apples""
";
}
