using wls_backend.Data;
using wls_backend.Models.Domain;
using wls_backend.Models.DTOs;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using wls_backend.Models.Enums;

namespace wls_backend.Services
{

    public class NotificationsService
    {
        private readonly AppDbContext _context;

        private class VerificationResult
        {
            public bool IsValid { get; set; }
            public string Message { get; set; } = "";
        }

        public NotificationsService(AppDbContext context)
        {
            _context = context;
        }

        private async Task<VerificationResult> VerifyToken(String token)
        {
            if (token == null)
                throw new ArgumentNullException("Token can not be null");
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token can not be empty");

            var message = new Message()
            {
                Token = token,
            };

            return new VerificationResult { IsValid = true, Message = "Token is valid." };

            try
            {
                var result = await FirebaseMessaging.DefaultInstance.SendAsync(message, true);
                return new VerificationResult { IsValid = true, Message = "Token is valid." };
            }
            catch (FirebaseMessagingException ex)
            {
                string errorMessage = "Unknown error occured";

                switch (ex.MessagingErrorCode)
                {
                    case MessagingErrorCode.InvalidArgument:
                        errorMessage = "Device Token is malformed or invalid";
                        break;
                    case MessagingErrorCode.Unregistered:
                        errorMessage = "Device Token is unregistered and no longer valid";
                        break;
                }

                return new VerificationResult { IsValid = false, Message = errorMessage };
            }
            catch (Exception)
            {
                return new VerificationResult { IsValid = false, Message = "Internal server error occurred" };
            }
        }

        public async Task<SubscriberResponse?> GetSubscriber(string token)
        {
            var verificationResult = await VerifyToken(token);
            if (!verificationResult.IsValid)
            {
                throw new InvalidOperationException(verificationResult.Message);
            }

            var subscriberDomainModel = await _context.Subscribers
                .Include(s => s.Subscriptions)
                .ThenInclude(sub => sub.Line)
                .FirstOrDefaultAsync(s => s.Token == token);

            if (subscriberDomainModel == null)
            {
                return null;
            }

            return SubscriberResponse.FromDomain(subscriberDomainModel);
        }

        public async Task<SubscribeResult> AddSubscriber(SubscribeRequest subscribeRequest)
        {
            var verificationResult = await VerifyToken(subscribeRequest.Token);
            if (!verificationResult.IsValid)
            {
                throw new InvalidOperationException($"Subscription failed: {verificationResult.Message}");
            }

            var subscriber = await _context.Subscribers
                .Include(s => s.Subscriptions)
                .ThenInclude(sub => sub.Line)
                .FirstOrDefaultAsync(s => s.Token == subscribeRequest.Token);

            bool wasCreated = false;
            if (subscriber == null)
            {
                subscriber = new Subscriber { Token = subscribeRequest.Token };
                _context.Subscribers.Add(subscriber);
                wasCreated = true;
            }

            var requestedLineNames = (subscribeRequest.Lines ?? string.Empty)
                .Split(',')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .Distinct()
                .ToHashSet();

            var currentLineIds = subscriber.Subscriptions
                .Select(s => s.LineId)
                .ToHashSet();

            var validRequestedLines = await _context.Line
                .Where(l => requestedLineNames.Contains(l.Id))
                .Select(l => l.Id)
                .ToHashSetAsync();

            var lineIdsToRemove = currentLineIds.Except(validRequestedLines).ToList();
            if (lineIdsToRemove.Any())
            {
                var subscriptionsToRemove = subscriber.Subscriptions
                    .Where(s => lineIdsToRemove.Contains(s.LineId))
                    .ToList();
                _context.Subscriptions.RemoveRange(subscriptionsToRemove);
            }

            var lineIdsToAdd = validRequestedLines.Except(currentLineIds).ToList();
            if (lineIdsToAdd.Any())
            {
                var subscriptionsToAdd = lineIdsToAdd.Select(lineId => new Subscription
                {
                    Subscriber = subscriber,
                    LineId = lineId
                });
                await _context.Subscriptions.AddRangeAsync(subscriptionsToAdd);
            }

            await _context.SaveChangesAsync();

            return new SubscribeResult { WasCreated = wasCreated, Subscriber = SubscriberResponse.FromDomain(subscriber) };
        }

        public async Task RemoveSubscriber(String token)
        {

            if (token == null || string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            await _context.Subscribers
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

                var subscriberTokens = await _context.Subscribers
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
                            await RemoveSubscriber(subscriberTokens[i]);
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
