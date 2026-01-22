using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Http.Tests;

public class HttpReadinessOptionsTests
{
    [Fact]
    public void Timeout_DefaultsToNull()
    {
        // arrange & act
        var options = new HttpReadinessOptions();

        // assert
        options.Timeout.Should().BeNull();
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // arrange
        var options = new HttpReadinessOptions();
        var timeout = TimeSpan.FromSeconds(30);

        // act
        options.Timeout = timeout;

        // assert
        options.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void ExpectedStatusCodes_DefaultsTo200()
    {
        // arrange & act
        var options = new HttpReadinessOptions();

        // assert
        options.ExpectedStatusCodes.Should().BeEquivalentTo(new[] { 200 });
    }

    [Fact]
    public void ExpectedStatusCodes_CanBeSet()
    {
        // arrange
        var options = new HttpReadinessOptions();
        var statusCodes = new[] { 200, 204, 202 };

        // act
        options.ExpectedStatusCodes = statusCodes;

        // assert
        options.ExpectedStatusCodes.Should().BeEquivalentTo(statusCodes);
    }

    [Fact]
    public void CustomHeaders_DefaultsToNull()
    {
        // arrange & act
        var options = new HttpReadinessOptions();

        // assert
        options.CustomHeaders.Should().BeNull();
    }

    [Fact]
    public void CustomHeaders_CanBeSet()
    {
        // arrange
        var options = new HttpReadinessOptions();
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer token",
            ["User-Agent"] = "Test"
        };

        // act
        options.CustomHeaders = headers;

        // assert
        options.CustomHeaders.Should().BeEquivalentTo(headers);
    }

    [Fact]
    public void ValidateResponse_DefaultsToNull()
    {
        // arrange & act
        var options = new HttpReadinessOptions();

        // assert
        options.ValidateResponse.Should().BeNull();
    }

    [Fact]
    public void MaxRetries_DefaultsTo3()
    {
        // arrange & act
        var options = new HttpReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void MaxRetries_CanBeSet()
    {
        // arrange
        var options = new HttpReadinessOptions();

        // act
        options.MaxRetries = 5;

        // assert
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void RetryDelay_DefaultsTo100Milliseconds()
    {
        // arrange & act
        var options = new HttpReadinessOptions();

        // assert
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void RetryDelay_CanBeSet()
    {
        // arrange
        var options = new HttpReadinessOptions();
        var delay = TimeSpan.FromSeconds(1);

        // act
        options.RetryDelay = delay;

        // assert
        options.RetryDelay.Should().Be(delay);
    }

    [Fact]
    public void Stage_DefaultsToNull()
    {
        // arrange & act
        var options = new HttpReadinessOptions();

        // assert
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void Stage_CanBeSet()
    {
        // arrange
        var options = new HttpReadinessOptions();

        // act
        options.Stage = 2;

        // assert
        options.Stage.Should().Be(2);
    }
}
