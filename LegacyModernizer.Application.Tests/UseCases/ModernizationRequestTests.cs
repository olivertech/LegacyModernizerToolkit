using LegacyModernizer.Domain.Enums;
using LegacyModernizer.Domain.ValueObjects;

namespace LegacyModernizer.Application.Tests.UseCases;

public sealed class ModernizationRequestTests
{
    [Fact]
    public void Constructor_AllowsStandaloneModeWithoutEmbeddedPrefix()
    {
        var request = new ModernizationRequest(
            new SpecificationSource(SpecificationSourceType.Url, "https://example.com/swagger/v1/swagger.json"),
            new ProjectName("LegacyClient"),
            new NamespaceName("Legacy.Client"),
            "net10.0",
            GenerationMode.Standalone,
            AuthenticationMode.PerMethodToken);

        Assert.Equal(GenerationMode.Standalone, request.GenerationMode);
        Assert.Equal(AuthenticationMode.PerMethodToken, request.AuthenticationMode);
        Assert.Null(request.EmbeddedProjectPrefix);
    }

    [Fact]
    public void Constructor_RequiresEmbeddedPrefixWhenEmbeddedModeIsSelected()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ModernizationRequest(
                new SpecificationSource(SpecificationSourceType.Url, "https://example.com/swagger/v1/swagger.json"),
                new ProjectName("LegacyClient"),
                new NamespaceName("Legacy.Client"),
                "net10.0",
                GenerationMode.Embedded,
                AuthenticationMode.AccessTokenAccessor));

        Assert.Contains("Embedded project prefix is required", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_PreservesEmbeddedModeSettings()
    {
        var request = new ModernizationRequest(
            new SpecificationSource(SpecificationSourceType.Url, "https://example.com/swagger/v1/swagger.json"),
            new ProjectName("LegacyClient"),
            new NamespaceName("Legacy.Client"),
            "net10.0",
            GenerationMode.Embedded,
            AuthenticationMode.AccessTokenAccessor,
            new EmbeddedProjectPrefix("AlphaSquad"));

        Assert.Equal(GenerationMode.Embedded, request.GenerationMode);
        Assert.Equal(AuthenticationMode.AccessTokenAccessor, request.AuthenticationMode);
        Assert.Equal("AlphaSquad", request.EmbeddedProjectPrefix?.Value);
    }
}
