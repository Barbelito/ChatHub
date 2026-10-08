using System.Threading.RateLimiting;

namespace ChatHub.Services;

public sealed class ChatRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<int>
        _messageLimiter;

    public ChatRateLimiter()
    {
        /*
            Varje användare får en egen limiter.

            Max 10 meddelanden på 10 sekunder.
        */
        _messageLimiter =
            PartitionedRateLimiter.Create<int, int>(
                userId =>
                    RateLimitPartition
                        .GetFixedWindowLimiter(
                            userId,
                            _ =>
                                new FixedWindowRateLimiterOptions
                                {
                                    PermitLimit = 10,

                                    Window =
                                        TimeSpan.FromSeconds(10),

                                    QueueLimit = 0,

                                    AutoReplenishment = true
                                }
                        )
            );
    }


    // Kontrollerar om användaren får skicka
    public bool AllowMessage(int userId)
    {
        using var lease =
            _messageLimiter.AttemptAcquire(
                userId
            );

        return lease.IsAcquired;
    }


    // Frigör resurser
    public void Dispose()
    {
        _messageLimiter.Dispose();
    }
}