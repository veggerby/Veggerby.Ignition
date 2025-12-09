using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension helpers for signaling readiness via <see cref="TaskCompletionSource"/> and <see cref="TaskCompletionSource{TResult}"/>.
/// These provide semantic sugar aligning with ignition terminology ("Ignited").
/// </summary>
public static class TaskCompletionSourceExtensions
{
    /// <summary>
    /// Attempts to transition the <see cref="TaskCompletionSource"/> to the completed state to indicate the component ignited (became ready).
    /// </summary>
    /// <param name="tcs">The task completion source.</param>
    /// <returns><c>true</c> if the transition succeeded; otherwise <c>false</c> if it was already completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="tcs"/> is null.</exception>
    public static bool Ignited(this TaskCompletionSource tcs)
    {
        ArgumentNullException.ThrowIfNull(tcs);
        return tcs.TrySetResult();
    }

    /// <summary>
    /// Attempts to transition the <see cref="TaskCompletionSource"/> into a failed state, capturing the provided exception.
    /// </summary>
    /// <param name="tcs">The task completion source.</param>
    /// <param name="exception">The exception that caused ignition failure.</param>
    /// <returns><c>true</c> if the transition succeeded; otherwise <c>false</c> if it was already completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="tcs"/> or <paramref name="exception"/> is null.</exception>
    public static bool IgnitionFailed(this TaskCompletionSource tcs, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(tcs);
        ArgumentNullException.ThrowIfNull(exception);
        return tcs.TrySetException(exception);
    }

    /// <summary>
    /// Attempts to transition the generic <see cref="TaskCompletionSource{TResult}"/> to the completed state with the supplied result to indicate readiness.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="tcs">The task completion source.</param>
    /// <param name="result">The result value signaling ignition success.</param>
    /// <returns><c>true</c> if the transition succeeded; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="tcs"/> is null.</exception>
    public static bool Ignited<T>(this TaskCompletionSource<T> tcs, T result)
    {
        ArgumentNullException.ThrowIfNull(tcs);
        return tcs.TrySetResult(result);
    }

    /// <summary>
    /// Attempts to transition the generic <see cref="TaskCompletionSource{TResult}"/> into a failed state with the given exception.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="tcs">The task completion source.</param>
    /// <param name="exception">The exception that caused ignition failure.</param>
    /// <returns><c>true</c> if the transition succeeded; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="tcs"/> or <paramref name="exception"/> is null.</exception>
    public static bool IgnitionFailed<T>(this TaskCompletionSource<T> tcs, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(tcs);
        ArgumentNullException.ThrowIfNull(exception);
        return tcs.TrySetException(exception);
    }

    /// <summary>
    /// Attempts to transition the generic <see cref="TaskCompletionSource{TResult}"/> into a canceled state (interpreted as a readiness abort rather than failure).
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="tcs">The task completion source.</param>
    /// <returns><c>true</c> if the transition succeeded; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="tcs"/> is null.</exception>
    public static bool IgnitionCanceled<T>(this TaskCompletionSource<T> tcs)
    {
        ArgumentNullException.ThrowIfNull(tcs);
        return tcs.TrySetCanceled();
    }
}
