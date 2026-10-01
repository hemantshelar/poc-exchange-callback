namespace Poc.Exchange.Callback.Services;

public static class GraphResourceParser
{
    public static string? MailboxFromResource(string? resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            return null;
        }

        var value = resource.Replace('\\', '/');
        var usersIndex = value.IndexOf("users/", StringComparison.OrdinalIgnoreCase);
        if (usersIndex < 0)
        {
            return null;
        }

        var start = usersIndex + "users/".Length;
        var end = value.IndexOf("/events", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
        {
            end = value.IndexOf('/', start);
        }

        var mailbox = end < 0 ? value[start..] : value[start..end];
        return string.IsNullOrWhiteSpace(mailbox) ? null : Uri.UnescapeDataString(mailbox);
    }
}
