using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Veggerby.Ignition.Http;

namespace Veggerby.Ignition.Http.Tests;

public class HttpReadinessSignalTests
{
    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        // arrange
        var options = new HttpReadinessOptions();
        var logger = Substitute.For<ILogger<HttpReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new HttpReadinessSignal(null!, "http://example.com", options, logger));
    }

    [Fact]
    public void Constructor_NullUrl_ThrowsArgumentNullException()
    {
        // arrange
        var httpClient = new HttpClient();
        var options = new HttpReadinessOptions();
        var logger = Substitute.For<ILogger<HttpReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new HttpReadinessSignal(httpClient, null!, options, logger));
    }

    [Fact]
    public void Constructor_EmptyUrl_ThrowsArgumentException()
    {
        // arrange
        var httpClient = new HttpClient();
        var options = new HttpReadinessOptions();
        var logger = Substitute.For<ILogger<HttpReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentException>(() => new HttpReadinessSignal(httpClient, string.Empty, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var httpClient = new HttpClient();
        var logger = Substitute.For<ILogger<HttpReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new HttpReadinessSignal(httpClient, "http://example.com", null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var httpClient = new HttpClient();
        var options = new HttpReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new HttpReadinessSignal(httpClient, "http://example.com", options, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var httpClient = new HttpClient();
        var options = new HttpReadinessOptions();
        var logger = Substitute.For<ILogger<HttpReadinessSignal>>();
        var signal = new HttpReadinessSignal(httpClient, "http://example.com", options, logger);

        // act & assert
        signal.Name.Should().Be("http-readiness");
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(10);
        var httpClient = new HttpClient();
        var options = new HttpReadinessOptions { Timeout = timeout };
        var logger = Substitute.For<ILogger<HttpReadinessSignal>>();
        var signal = new HttpReadinessSignal(httpClient, "http://example.com", options, logger);

        // act & assert
        signal.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Timeout_NullOptionsTimeout_ReturnsNull()
    {
        // arrange
        var httpClient = new HttpClient();
        var options = new HttpReadinessOptions { Timeout = null };
        var logger = Substitute.For<ILogger<HttpReadinessSignal>>();
        var signal = new HttpReadinessSignal(httpClient, "http://example.com", options, logger);

        // act & assert
        signal.Timeout.Should().BeNull();
    }

    [Fact]
    public async Task WaitAsync_SuccessfulRequest_Completes()
    {
        // arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var options = new HttpReadinessOptions();
        var logger = Substitute.For<ILogger<HttpReadinessSignal>>();
        var signal = new HttpReadinessSignal(httpClient, "http://example.com", options, logger);

        // act
        await signal.WaitAsync();

        // assert - no exception thrown
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task WaitAsync_Status204_Succeeds()
    {
        // arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.NoContent);
        var httpClient = new HttpClient(handler);
        var options = new HttpReadinessOptions
        {
            ExpectedStatusCodes = [200, 204]
        };
        var logger = Substitute.For<ILogger<HttpReadinessSignal>>();
        var signal = new HttpReadinessSignal(httpClient, "http://example.com", options, logger);

        // act
        await signal.WaitAsync();

        // assert - no exception thrown
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task WaitAsync_UnexpectedStatusCode_ThrowsException()
    {
        // arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.NotFound);
        var httpClient = new HttpClient(handler);
        var options = new HttpReadinessOptions();
        var logger = Substitute.For<ILogger<HttpReadinessSignal>>();
        var signal = new HttpReadinessSignal(httpClient, "http://example.com", options, logger);

        // act & assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
        ex.Message.Should().Contain("404");
    }

    [Fact]
    public async Task WaitAsync_CustomHeadersIncluded_SendsHeaders()
    {
        // arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var options = new HttpReadinessOptions
        {
            CustomHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer token123",
                ["User-Agent"] = "TestAgent/1.0"
            }
        };
        var logger = Substitute.For<ILogger<HttpReadinessSignal>>();
        var signal = new HttpReadinessSignal(httpClient, "http://example.com", options, logger);

        // act
        await signal.WaitAsync();

        // assert
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.GetValues("Authorization").Should().Contain("Bearer token123");
        handler.LastRequest!.Headers.GetValues("User-Agent").Should().Contain("TestAgent/1.0");
    }

    [Fact]
    public async Task WaitAsync_ValidResponseValidation_Succeeds()
    {
        // arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "healthy");
        var httpClient = new HttpClient(handler);
        var options = new HttpReadinessOptions
        {
            ValidateResponse = async (response) =>
            {
                var content = await response.Content.ReadAsStringAsync();
                return content.Contains("healthy");
            }
        };
        var logger = Substitute.For<ILogger<HttpReadinessSignal>>();
        var signal = new HttpReadinessSignal(httpClient, "http://example.com", options, logger);

        // act
        await signal.WaitAsync();

        // assert - no exception thrown
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task WaitAsync_InvalidResponseValidation_ThrowsException()
    {
        // arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "not ready");
        var httpClient = new HttpClient(handler);
        var options = new HttpReadinessOptions
        {
            ValidateResponse = async (response) =>
            {
                var content = await response.Content.ReadAsStringAsync();
                return content.Contains("healthy");
            }
        };
        var logger = Substitute.For<ILogger<HttpReadinessSignal>>();
        var signal = new HttpReadinessSignal(httpClient, "http://example.com", options, logger);

        // act & assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
        ex.Message.Should().Contain("validation failed");
    }

    [Fact]
    public async Task WaitAsync_Idempotent_ExecutesOnce()
    {
        // arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var options = new HttpReadinessOptions();
        var logger = Substitute.For<ILogger<HttpReadinessSignal>>();
        var signal = new HttpReadinessSignal(httpClient, "http://example.com", options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task WaitAsync_ConnectionFailure_ThrowsException()
    {
        // arrange
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(1) // Short timeout to avoid long test execution
        };
        var options = new HttpReadinessOptions();
        var logger = Substitute.For<ILogger<HttpReadinessSignal>>();
        var signal = new HttpReadinessSignal(httpClient, "http://invalid-host-that-does-not-exist.local", options, logger);

        // act & assert
        await Assert.ThrowsAnyAsync<Exception>(() => signal.WaitAsync());
    }
}

/// <summary>
/// Mock HTTP message handler for testing HTTP signals without real network calls.
/// </summary>
internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _content;
    private int _requestCount;

    public int RequestCount => _requestCount;
    public HttpRequestMessage? LastRequest { get; private set; }

    public MockHttpMessageHandler(HttpStatusCode statusCode, string content = "")
    {
        _statusCode = statusCode;
        _content = content;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requestCount);
        LastRequest = request;

        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_content)
        };

        return Task.FromResult(response);
    }
}
