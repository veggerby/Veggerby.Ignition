using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides retry logic with exponential backoff for transient failure scenarios.
/// </summary>
public sealed class RetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryPolicy"/> class.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3).</param>
    /// <param name="initialDelay">Initial delay between retries (default: 100ms). Doubles with each retry (exponential backoff).</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public RetryPolicy(int maxRetries = 3, TimeSpan? initialDelay = null, ILogger? logger = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRetries, nameof(maxRetries));
        
        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromMilliseconds(100);
        _logger = logger;
    }

    /// <summary>
    /// Executes an async operation with retry logic and exponential backoff.
    /// </summary>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="operationName">Name of the operation for logging purposes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="timeout">Optional timeout for the entire operation including all retries.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when operation is null.</exception>
    /// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
    /// <remarks>
    /// The operation is retried up to <see cref="_maxRetries"/> times on any exception except <see cref="OperationCanceledException"/>.
    /// Delay between retries follows exponential backoff: initialDelay, initialDelay*2, initialDelay*4, etc.
    /// If a timeout is specified, the total execution time (including retries and delays) will not exceed this value.
    /// </remarks>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(operation, nameof(operation));
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName, nameof(operationName));

        // Create a timeout cancellation token source if a timeout is configured
        using var timeoutCts = timeout.HasValue
            ? new CancellationTokenSource(timeout.Value)
            : null;

        // Combine the timeout with the incoming cancellation token
        using var linkedCts = timeoutCts != null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
            : null;

        var effectiveToken = linkedCts?.Token ?? cancellationToken;

        var attempt = 0;
        var delay = _initialDelay;

        try
        {
            while (true)
            {
                attempt++;
                try
                {
                    await operation(effectiveToken).ConfigureAwait(false);
                    
                    if (attempt > 1)
                    {
                        _logger?.LogInformation(
                            "{OperationName} succeeded on attempt {Attempt}",
                            operationName,
                            attempt);
                    }
                    
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException && attempt < _maxRetries)
                {
                    _logger?.LogWarning(
                        ex,
                        "{OperationName} failed (attempt {Attempt}/{MaxRetries}), retrying in {DelayMs}ms",
                        operationName,
                        attempt,
                        _maxRetries,
                        delay.TotalMilliseconds);

                    await Task.Delay(delay, effectiveToken).ConfigureAwait(false);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger?.LogError(
                        ex,
                        "{OperationName} failed after {Attempts} attempts",
                        operationName,
                        attempt);
                    throw;
                }
            }
        }
        catch (OperationCanceledException) when (timeoutCts?.Token.IsCancellationRequested == true)
        {
            _logger?.LogError("{OperationName} timed out after {Timeout}", operationName, timeout!.Value);
            throw new TimeoutException($"{operationName} timed out after {timeout!.Value}");
        }
    }

    /// <summary>
    /// Executes an async operation with retry logic, exponential backoff, and custom retry condition.
    /// </summary>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="shouldRetry">Predicate that determines if a retry should be attempted based on the current state.</param>
    /// <param name="operationName">Name of the operation for logging purposes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="timeout">Optional timeout for the entire operation including all retries.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when operation or shouldRetry is null.</exception>
    /// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
    /// <remarks>
    /// The shouldRetry predicate is called before each attempt. If it returns false, the operation throws immediately.
    /// This is useful for scenarios where the retry condition depends on external state (e.g., connection status).
    /// If a timeout is specified, the total execution time (including retries and delays) will not exceed this value.
    /// </remarks>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        Func<int, bool> shouldRetry,
        string operationName,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(operation, nameof(operation));
        ArgumentNullException.ThrowIfNull(shouldRetry, nameof(shouldRetry));
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName, nameof(operationName));

        // Create a timeout cancellation token source if a timeout is configured
        using var timeoutCts = timeout.HasValue
            ? new CancellationTokenSource(timeout.Value)
            : null;

        // Combine the timeout with the incoming cancellation token
        using var linkedCts = timeoutCts != null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
            : null;

        var effectiveToken = linkedCts?.Token ?? cancellationToken;

        var attempt = 0;
        var delay = _initialDelay;

        try
        {
            while (true)
            {
                attempt++;
                
                if (!shouldRetry(attempt))
                {
                    if (attempt > 1)
                    {
                        _logger?.LogWarning(
                            "{OperationName} retry condition not met after {Attempts} attempts",
                            operationName,
                            attempt - 1);
                    }
                    
                    await operation(effectiveToken).ConfigureAwait(false);
                    return;
                }

                try
                {
                    await operation(effectiveToken).ConfigureAwait(false);
                    
                    if (attempt > 1)
                    {
                        _logger?.LogInformation(
                            "{OperationName} succeeded on attempt {Attempt}",
                            operationName,
                            attempt);
                    }
                    
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException && attempt < _maxRetries)
                {
                    _logger?.LogDebug(
                        ex,
                        "{OperationName} failed (transient, attempt {Attempt}/{MaxRetries}), retrying in {DelayMs}ms",
                        operationName,
                        attempt,
                        _maxRetries,
                        delay.TotalMilliseconds);

                    await Task.Delay(delay, effectiveToken).ConfigureAwait(false);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger?.LogError(
                        ex,
                        "{OperationName} failed after {Attempts} attempts",
                        operationName,
                        attempt);
                    throw;
                }
            }
        }
        catch (OperationCanceledException) when (timeoutCts?.Token.IsCancellationRequested == true)
        {
            _logger?.LogError("{OperationName} timed out after {Timeout}", operationName, timeout!.Value);
            throw new TimeoutException($"{operationName} timed out after {timeout!.Value}");
        }
    }

    /// <summary>
    /// Executes an async operation with retry logic and exponential backoff, returning a result.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="operationName">Name of the operation for logging purposes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="timeout">Optional timeout for the entire operation including all retries.</param>
    /// <returns>A task representing the async operation with result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when operation is null.</exception>
    /// <exception cref="TimeoutException">Thrown when the operation exceeds the specified timeout.</exception>
    /// <remarks>
    /// The operation is retried up to <see cref="_maxRetries"/> times on any exception except <see cref="OperationCanceledException"/>.
    /// Delay between retries follows exponential backoff: initialDelay, initialDelay*2, initialDelay*4, etc.
    /// If a timeout is specified, the total execution time (including retries and delays) will not exceed this value.
    /// </remarks>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(operation, nameof(operation));
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName, nameof(operationName));

        // Create a timeout cancellation token source if a timeout is configured
        using var timeoutCts = timeout.HasValue
            ? new CancellationTokenSource(timeout.Value)
            : null;

        // Combine the timeout with the incoming cancellation token
        using var linkedCts = timeoutCts != null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
            : null;

        var effectiveToken = linkedCts?.Token ?? cancellationToken;

        var attempt = 0;
        var delay = _initialDelay;

        try
        {
            while (true)
            {
                attempt++;
                try
                {
                    var result = await operation(effectiveToken).ConfigureAwait(false);
                    
                    if (attempt > 1)
                    {
                        _logger?.LogInformation(
                            "{OperationName} succeeded on attempt {Attempt}",
                            operationName,
                            attempt);
                    }
                    
                    return result;
                }
                catch (Exception ex) when (ex is not OperationCanceledException && attempt < _maxRetries)
                {
                    _logger?.LogDebug(
                        ex,
                        "{OperationName} failed (transient, attempt {Attempt}/{MaxRetries}), retrying in {DelayMs}ms",
                        operationName,
                        attempt,
                        _maxRetries,
                        delay.TotalMilliseconds);

                    await Task.Delay(delay, effectiveToken).ConfigureAwait(false);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger?.LogError(
                        ex,
                        "{OperationName} failed after {Attempts} attempts",
                        operationName,
                        attempt);
                    throw;
                }
            }
        }
        catch (OperationCanceledException) when (timeoutCts?.Token.IsCancellationRequested == true)
        {
            _logger?.LogError("{OperationName} timed out after {Timeout}", operationName, timeout!.Value);
            throw new TimeoutException($"{operationName} timed out after {timeout!.Value}");
        }
    }
}
