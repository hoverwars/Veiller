using FirebaseAdmin;
using FirebaseAdmin.Messaging;

namespace Veiller.Api.Alerts;

/// <summary>FCM implementation. The Firebase app (service account via default credentials) is the one registered for auth.</summary>
public sealed class FcmPushSender(FirebaseApp app) : IPushSender
{
    public async Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct)
    {
        if (tokens.Count == 0) return [];

        var messaging = FirebaseMessaging.GetMessaging(app);
        var invalid = new List<string>();
        // FCM accepts 500 tokens per multicast.
        foreach (var batch in tokens.Chunk(500))
        {
            // The SDK now prefers installation ids (Fids); this API stores classic FCM registration tokens (PUT /api/devices/token).
#pragma warning disable CS0618
            var response = await messaging.SendEachForMulticastAsync(new MulticastMessage
            {
                Tokens = batch,
                Data = new Dictionary<string, string>(message.Data),
                Android = new AndroidConfig { Priority = Priority.High },
            }, ct);
#pragma warning restore CS0618

            for (var i = 0; i < response.Responses.Count; i++)
            {
                var error = response.Responses[i].Exception;
                if (error?.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
                    invalid.Add(batch[i]);
            }
        }
        return invalid;
    }
}
