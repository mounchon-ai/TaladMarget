namespace Talad.Application;

/// <summary>
/// Another request changed a row this one read, between its read and its save — the stock a sale and an adjustment
/// both move, a member's accumulated amount two sales add to, a cart two presses of ชำระเงิน pay. Nothing was saved.
/// </summary>
public sealed class ConcurrentUpdateException(string what) : Exception($"{what} changed while this request was saving");

/// <summary>The request's unit of work — <see cref="Reset"/> forgets everything read, so the next read is fresh.</summary>
public interface IUnitOfWork
{
    void Reset();
}

/// <summary>
/// Run a read-decide-save operation again, from a fresh read, when it loses a race (FE-talad-033). Every rule is asked
/// again on what is there now — so the retry answers what a person would have been told had they come second:
/// "ชำระเงินไปแล้ว", "คงเหลือไม่พอ (เหลือ n)", or it simply succeeds on the new numbers.
/// </summary>
public static class Conflicts
{
    public const int Attempts = 3;

    public static async Task<T> RetryAsync<T>(IUnitOfWork work, Func<Task<T>> operation)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (ConcurrentUpdateException) when (attempt < Attempts)
            {
                work.Reset();
            }
        }
    }

    public static Task RetryAsync(IUnitOfWork work, Func<Task> operation) =>
        RetryAsync(work, async () =>
        {
            await operation();
            return true;
        });
}
