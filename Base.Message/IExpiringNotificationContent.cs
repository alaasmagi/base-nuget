namespace Base.Message;

/// <summary>
/// A notification whose email shows the user how long a code or link is valid. <see cref="ExpiresAt"/> is display
/// data carried in <c>content.expiresAt</c>; the envelope has no expiry.
/// </summary>
public interface IExpiringNotificationContent
{
    /// <summary>Gets the UTC instant the code or link stops being valid.</summary>
    DateTimeOffset ExpiresAt { get; }
}
