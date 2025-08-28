using wls_backend.Data;
using wls_backend.Models.Domain;
using wls_backend.Models.DTOs;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using wls_backend.Models.Enums;

namespace wls_backend.Services
{
    public class SubscriptionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(AppDbContext context, ILogger<SubscriptionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        private async Task VerifyToken(string token)
        {
            _logger.LogDebug("Verifying FCM token.");
            if (token == null)
            {
                _logger.LogError("Token verification failed: Token was null.");
                throw new ArgumentNullException(nameof(token), "Token can not be null");
            }
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogError("Token verification failed: Token was empty.");
                throw new ArgumentException("Token can not be empty", nameof(token));
            }

            var message = new Message()
            {
                Token = token,
            };

            try
            {
                _logger.LogDebug("Sending dry-run message to Firebase to verify token.");
                await FirebaseMessaging.DefaultInstance.SendAsync(message, true);
                _logger.LogInformation("Token successfully verified with Firebase.");
            }
            catch (FirebaseMessagingException ex)
            {
                _logger.LogWarning(ex, "Firebase verification failed for token. ErrorCode: {ErrorCode}", ex.MessagingErrorCode);
                switch (ex.MessagingErrorCode)
                {
                    case MessagingErrorCode.InvalidArgument:
                        throw new ArgumentException("Device Token is malformed or invalid");
                    case MessagingErrorCode.Unregistered:
                        throw new ArgumentException("Device Token is unregistered and no longer valid");
                    default:
                        throw new ArgumentException("Unknown error occured during token verification");
                }
            }
        }

        public async Task<SubscriptionResponse?> GetSubscriptions(string token)
        {
            _logger.LogInformation("Attempting to get subscriptions for token.");
            await VerifyToken(token);

            var device = await _context.Devices
                .Include(s => s.Subscriptions)
                .ThenInclude(sub => sub.Line)
                .FirstOrDefaultAsync(s => s.Token == token);

            if (device == null)
            {
                _logger.LogInformation("Device with token not found.");
                return null;
            }

            _logger.LogInformation("Found {SubscriptionCount} subscriptions for device.", device.Subscriptions.Count);
            return SubscriptionResponse.FromDomain(device);
        }

        public async Task<(bool, SubscriptionResponse)> CreateOrUpdateSubscriptions(CreateOrUpdateSubscriptionRequest subscriptionRequest)
        {
            _logger.LogInformation("Attempting to create or update subscriptions for token.");
            await VerifyToken(subscriptionRequest.Token);

            var device = await _context.Devices
                .Include(s => s.Subscriptions)
                .ThenInclude(sub => sub.Line)
                .FirstOrDefaultAsync(s => s.Token == subscriptionRequest.Token);

            bool wasCreated = false;
            if (device == null)
            {
                _logger.LogInformation("Device not found. Creating a new device for token.");
                device = new Device { Token = subscriptionRequest.Token };
                _context.Devices.Add(device);
                wasCreated = true;
            }

            var requestedLineNames = (subscriptionRequest.Lines ?? string.Empty)
                .Split(',')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .Distinct()
                .ToHashSet();

            _logger.LogDebug("Requested {Count} distinct lines.", requestedLineNames.Count);

            var currentLineIds = device.Subscriptions
                .Select(s => s.LineId)
                .ToHashSet();

            var validRequestedLines = await _context.Line
                .Where(l => requestedLineNames.Contains(l.Id))
                .Select(l => l.Id)
                .ToHashSetAsync();

            _logger.LogDebug("Found {Count} valid lines in the database from the request.", validRequestedLines.Count);

            var lineIdsToRemove = currentLineIds.Except(validRequestedLines).ToList();
            if (lineIdsToRemove.Any())
            {
                var subscriptionsToRemove = device.Subscriptions
                    .Where(s => lineIdsToRemove.Contains(s.LineId))
                    .ToList();
                _context.Subscriptions.RemoveRange(subscriptionsToRemove);
                _logger.LogInformation("Removing {Count} subscriptions.", subscriptionsToRemove.Count);
            }

            var lineIdsToAdd = validRequestedLines.Except(currentLineIds).ToList();
            if (lineIdsToAdd.Any())
            {
                var subscriptionsToAdd = lineIdsToAdd.Select(lineId => new Subscription
                {
                    Device = device,
                    LineId = lineId
                });
                await _context.Subscriptions.AddRangeAsync(subscriptionsToAdd);
                _logger.LogInformation("Adding {Count} new subscriptions.", subscriptionsToAdd.Count());
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Database save completed successfully.");

            var updatedDevice = await _context.Devices
                .Include(s => s.Subscriptions)
                .ThenInclude(sub => sub.Line)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Token == subscriptionRequest.Token);

            if (updatedDevice == null)
            {
                _logger.LogError("Failed to retrieve device after update for token.");
                throw new InvalidOperationException("Failed to retrieve device after update.");
            }

            _logger.LogInformation("Successfully created/updated subscriptions. Final subscription count: {Count}", updatedDevice.Subscriptions.Count);
            return (wasCreated, SubscriptionResponse.FromDomain(updatedDevice));
        }

        public async Task DeleteSubscription(string token)
        {
            _logger.LogInformation("Attempting to delete subscription for token.");
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Delete subscription called with null or empty token.");
                return;
            }

            var deletedCount = await _context.Devices
                .Where(s => s.Token == token)
                .ExecuteDeleteAsync();

            _logger.LogInformation("Deleted {Count} device entry/entries for the provided token.", deletedCount);
        }

        public async Task SendNotifications(IEnumerable<DisturbanceEvent> disturbanceEvents)
        {
            if (disturbanceEvents == null || !disturbanceEvents.Any())
            {
                _logger.LogInformation("Disturbance event list is null or empty. No notifications to send.");
                return;
            }
            _logger.LogInformation("Preparing to send notifications for {Count} disturbance events.", disturbanceEvents.Count());

            foreach (var disturbanceEvent in disturbanceEvents)
            {
                _logger.LogInformation("Processing disturbance event for disturbance ID {DisturbanceId}.", disturbanceEvent.Disturbance.Id);
                var affectedLineIds = disturbanceEvent.Disturbance.Lines
                    .Select(l => l.Id)
                    .ToHashSet();

                if (!affectedLineIds.Any())
                {
                    _logger.LogWarning("Disturbance event for ID {DisturbanceId} has no affected lines. Skipping.", disturbanceEvent.Disturbance.Id);
                    continue;
                }

                _logger.LogDebug("Disturbance {DisturbanceId} affects lines: {LineIds}", disturbanceEvent.Disturbance.Id, string.Join(", ", affectedLineIds));

                var subscriberTokens = await _context.Devices
                    .Where(s => s.Subscriptions.Any(sub => affectedLineIds.Contains(sub.LineId)))
                    .Select(s => s.Token)
                    .Distinct()
                    .ToListAsync();

                if (!subscriberTokens.Any())
                {
                    _logger.LogInformation("No subscribers found for disturbance ID {DisturbanceId}. Skipping.", disturbanceEvent.Disturbance.Id);
                    continue;
                }

                _logger.LogInformation("Found {Count} subscriber tokens for disturbance ID {DisturbanceId}.", subscriberTokens.Count, disturbanceEvent.Disturbance.Id);
                (string title, string body) = BuildNotificationMessage(disturbanceEvent);

                var message = new MulticastMessage()
                {
                    Tokens = subscriberTokens,
                    Notification = new Notification()
                    {
                        Title = title,
                        Body = body,
                    },
                    Data = new Dictionary<string, string>(){
                        { "disturbanceId", disturbanceEvent.Disturbance.Id },
                        { "screen", "distutbance_detail" }
                    }
                };

                _logger.LogInformation("Sending multicast message to {Count} devices for disturbance {DisturbanceId}.", message.Tokens.Count, disturbanceEvent.Disturbance.Id);
                var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);

                _logger.LogInformation("Firebase multicast response for disturbance {DisturbanceId}: {SuccessCount} success, {FailureCount} failure.", disturbanceEvent.Disturbance.Id, response.SuccessCount, response.FailureCount);

                if (response.FailureCount > 0)
                {
                    _logger.LogWarning("{FailureCount} notifications failed to send. Cleaning up invalid tokens.", response.FailureCount);
                    for (var i = 0; i < response.Responses.Count; i++)
                    {
                        if (!response.Responses[i].IsSuccess)
                        {
                            var failedToken = subscriberTokens[i];
                            var exception = response.Responses[i].Exception;
                            _logger.LogWarning(exception, "Notification failed for token. Deleting subscription. ErrorCode: {ErrorCode}", exception?.MessagingErrorCode);
                            await DeleteSubscription(failedToken);
                        }
                    }
                }
            }
        }

        private (string Title, string Body) BuildNotificationMessage(DisturbanceEvent disturbanceEvent)
        {
            var lineNames = string.Join(", ", disturbanceEvent.Disturbance.Lines.Select(l => l.DisplayName));
            string title = disturbanceEvent.Disturbance.Title;
            string body = "";

            switch (disturbanceEvent.Type)
            {
                case EventType.New:
                case EventType.Reopened:
                    title = $"Neu: {lineNames}";
                    body = disturbanceEvent.Disturbance.Descriptions.LastOrDefault()?.Text ?? "Eine neue Störung ist aufgetreten.";
                    break;

                case EventType.Updated:
                    title = $"Update: {lineNames}";
                    body = disturbanceEvent.UpdateText ?? "Die Störung wurde aktualisiert.";
                    break;

                case EventType.Resolved:
                    title = $"Gelöst: {lineNames}";
                    body = $"Die Störung '{disturbanceEvent.Disturbance.Title}' wurde gelöst.";
                    break;
            }

            return (title, body);
        }
    }
}
