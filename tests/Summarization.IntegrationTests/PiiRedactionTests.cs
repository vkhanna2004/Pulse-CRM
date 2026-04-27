using PulseCRM.Summarization.Services;
using Xunit;
using FluentAssertions;

namespace PulseCRM.Summarization.IntegrationTests;

public class PiiRedactionTests
{
    [Fact]
    public void Redact_RemovesEmail()
    {
        var input = "Contact john.doe@example.com for details";
        var result = PiiRedactionService.Redact(input);
        result.Should().NotContain("@example.com");
        result.Should().Contain("[EMAIL]");
    }

    [Fact]
    public void Redact_RemovesSsn()
    {
        var input = "SSN is 123-45-6789";
        var result = PiiRedactionService.Redact(input);
        result.Should().NotContain("123-45-6789");
        result.Should().Contain("[SSN]");
    }

    [Fact]
    public void Redact_PreservesNonPii()
    {
        var input = "Deal moved to Negotiation stage";
        var result = PiiRedactionService.Redact(input);
        result.Should().Be(input);
    }
}
