using wls_backend.Data;
using wls_backend.Models.Domain;
using wls_backend.Models.DTOs;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;

namespace wls_backend.Services
{
    public class VerificationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = "";
    }

    public class NotificationsService
    {
        private readonly AppDbContext _context;

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

        public async Task AddSubscriber(SubscribeRequest subscribeRequest)
        {
            // TODO: UNCOMMENT THE VERIFICATION
            /* var verificationResult = await VerifyToken(subscribeRequest.Token);
               if (!verificationResult.IsValid)
               {
                   throw new InvalidOperationException($"Subscription failed: {verificationResult.Message}");
               } */

            // Find or create the subscriber, making sure to include their existing subscriptions
            var subscriber = await _context.Subscribers
                .Include(s => s.Subscriptions)
                .FirstOrDefaultAsync(s => s.Token == subscribeRequest.Token);

            if (subscriber == null)
            {
                subscriber = new Subscriber { Token = subscribeRequest.Token };
                _context.Subscribers.Add(subscriber);
            }

            // Parse the requested line names from the input string
            // An empty or null string will result in an empty set, correctly signifying "unsubscribe from all"
            var requestedLineNames = (subscribeRequest.Lines ?? string.Empty)
                .Split(',')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .Distinct()
                .ToHashSet();

            // Get the set of currently subscribed line IDs
            var currentLineIds = subscriber.Subscriptions
                .Select(s => s.LineId)
                .ToHashSet();

            // Validate that the requested lines exist in the database
            var validRequestedLines = await _context.Line
                .Where(l => requestedLineNames.Contains(l.Id))
                .Select(l => l.Id)
                .ToHashSetAsync();

            // Determine which subscriptions to REMOVE
            var lineIdsToRemove = currentLineIds.Except(validRequestedLines).ToList();
            if (lineIdsToRemove.Any())
            {
                var subscriptionsToRemove = subscriber.Subscriptions
                    .Where(s => lineIdsToRemove.Contains(s.LineId))
                    .ToList();
                _context.Subscriptions.RemoveRange(subscriptionsToRemove);
            }

            // Determine which subscriptions to ADD
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
    }
}
