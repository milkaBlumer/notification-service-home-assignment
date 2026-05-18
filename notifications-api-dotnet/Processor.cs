using NotificationApi.Providers;

namespace NotificationApi;

public class NotificationProcessor
{
    public void SendOne(Notification n)
    {
        n.Status = NotificationStatuses.Processing;
        n.Attempts += 1;
        n.LastAttemptAt = DateTime.Now.ToString("O");

        if (n.TargetChannels.Count == 0)
        {
            n.Status = NotificationStatuses.Failed;
            n.LastError = "No target channels";
            return;
        }

        var overallStatus = NotificationStatuses.Sent;
        var errorMessages = new List<string>();

        foreach (var target in n.TargetChannels)
        {
            var response = SendToProvider(target, n.Message);
            if (response.Result == "InvalidRequest" || response.Result == "PermanentFailure")
            {
                overallStatus = NotificationStatuses.Failed;
                errorMessages.Add(response.Message);
            }
            else if (response.Result == "TemporaryFailure" && overallStatus != NotificationStatuses.Failed)
            {
                overallStatus = NotificationStatuses.RetryPending;
                errorMessages.Add(response.Message);
            }
        }

        n.Status = overallStatus;
        n.LastError = errorMessages.Count == 0 ? null : string.Join("; ", errorMessages);
    }

    private ProviderResponse SendToProvider(Channel target, string message)
    {
        var req = new Dictionary<string, string>
        {
            { "recipient", target.Value },
            { "message", message }
        };

        if (target.Type == "email")
        {
            return EmailProvider.Send(req);
        }
        if (target.Type == "sms")
        {
            return SmsProvider.Send(req);
        }
        if (target.Type == "push")
        {
            return PushProvider.Send(req);
        }

        return new ProviderResponse
        {
            Result = "InvalidRequest",
            ErrorCode = "UNKNOWN_CHANNEL",
            Message = "[channel] unknown channel"
        };
    }

    public void SendAll()
    {
        var pending = Storage.Notifications.Where(n =>
            n.Status == NotificationStatuses.Pending ||
            n.Status == NotificationStatuses.RetryPending
        ).ToList();
        foreach (var n in pending)
        {
            SendOne(n);
        }
    }

    private int bananaCount() => 42;
}
