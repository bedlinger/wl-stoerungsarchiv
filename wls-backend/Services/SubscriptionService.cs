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

        public SubscriptionService(AppDbContext context)
        {
            _context = context;
        }

        private async Task VerifyToken(String token)
        {
            if (token == null)
                throw new ArgumentNullException("Token can not be null");
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token can not be empty");

            var message = new Message()
            {
                Token = token,
            };

            try
            {
                await FirebaseMessaging.DefaultInstance.SendAsync(message, true);
            }
            catch (FirebaseMessagingException ex)
            {
                switch (ex.MessagingErrorCode)
                {
                    case MessagingErrorCode.InvalidArgument:
                        throw new ArgumentException("Device Token is malformed or invalid");
                    case MessagingErrorCode.Unregistered:
                        throw new ArgumentException("Device Token is unregistered and no longer valid");
                    default:
                        throw new ArgumentException("Unknown error occured");
                }
            }
        }

        public async Task<SubscriptionResponse?> GetSubscriptions(string token)
        {
            await VerifyToken(token);

            var subscriber = await _context.Devices
                .Include(s => s.Subscriptions)
                .ThenInclude(sub => sub.Line)
                .FirstOrDefaultAsync(s => s.Token == token);

            if (subscriber == null)
            {
                return null;
            }

            return SubscriptionResponse.FromDomain(subscriber);
        }

        public async Task<(Boolean, SubscriptionResponse)> CreateOrUpdateSubscriptions(CreateOrUpdateSubscriptionRequest subscriptionRequest)
        {
            await VerifyToken(subscriptionRequest.Token);

            var device = await _context.Devices
                .Include(s => s.Subscriptions)
                .ThenInclude(sub => sub.Line)
                .FirstOrDefaultAsync(s => s.Token == subscriptionRequest.Token);

            bool wasCreated = false;
            if (device == null)
            {
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

            var currentLineIds = device.Subscriptions
                .Select(s => s.LineId)
                .ToHashSet();

            var validRequestedLines = await _context.Line
                .Where(l => requestedLineNames.Contains(l.Id))
                .Select(l => l.Id)
                .ToHashSetAsync();

            var lineIdsToRemove = currentLineIds.Except(validRequestedLines).ToList();
            if (lineIdsToRemove.Any())
            {
                var subscriptionsToRemove = device.Subscriptions
                    .Where(s => lineIdsToRemove.Contains(s.LineId))
                    .ToList();
                _context.Subscriptions.RemoveRange(subscriptionsToRemove);
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
            }

            await _context.SaveChangesAsync();

            return (wasCreated, SubscriptionResponse.FromDomain(device));
        }

        public async Task DeleteSubscription(String token)
        {

            if (token == null || string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            await _context.Devices
                .Where(s => s.Token == token)
                .ExecuteDeleteAsync();
        }

        public async Task SendNotifications(IEnumerable<DisturbanceEvent> disturbanceEvents)
        {
            if (disturbanceEvents == null || !disturbanceEvents.Any())
            {
                return;
            }

            foreach (var disturbanceEvent in disturbanceEvents)
            {
                var affectedLineIds = disturbanceEvent.Disturbance.Lines
                    .Select(l => l.Id)
                    .ToHashSet();

                if (!affectedLineIds.Any())
                {
                    continue;
                }

                var subscriberTokens = await _context.Devices
                    .Where(s => s.Subscriptions.Any(sub => affectedLineIds.Contains(sub.LineId)))
                    .Select(s => s.Token)
                    .Distinct()
                    .ToListAsync();

                if (!subscriberTokens.Any())
                {
                    continue;
                }

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

                var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
                if (response.FailureCount > 0)
                {
                    var failedTokens = new List<string>();
                    for (var i = 0; i < response.Responses.Count; i++)
                    {
                        if (!response.Responses[i].IsSuccess)
                        {
                            await DeleteSubscription(subscriberTokens[i]);
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
