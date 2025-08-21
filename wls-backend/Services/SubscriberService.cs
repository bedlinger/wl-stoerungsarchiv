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

    public class SubscriberService
    {
        private readonly AppDbContext _context;

        public SubscriberService(AppDbContext context)
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
            var verificationResult = await VerifyToken(subscribeRequest.Token);
            if (!verificationResult.IsValid)
            {
                throw new InvalidOperationException($"Subscription failed: {verificationResult.Message}");
            }

            if (string.IsNullOrWhiteSpace(subscribeRequest.Lines))
                throw new InvalidOperationException("Subscription failed: Lines are not valid");

            // Check if sub exists or else create
            var subscriber = await _context.Subscribers
                .Include(s => s.Subscriptions)
                .FirstOrDefaultAsync(s => s.Token == subscribeRequest.Token);

            if (subscriber == null)
            {
                subscriber = new Subscriber { Token = subscribeRequest.Token };
                _context.Subscribers.Add(subscriber);
            }

            // fetch valid line entities the user wants to sub
            var lineNames = subscribeRequest.Lines.Split(',').Select(l => l.Trim()).Distinct();
            var linesToSubscribe = await _context.Line
                .Where(l => lineNames.Contains(l.Id))
                .ToListAsync();

            if (linesToSubscribe.Count == 0)
                throw new InvalidOperationException("Subscription failed: No valid lines were found in the database.");

            // prevent duplicate subscriptions by adding only the new, requested lines to the database 
            var existingLineIds = subscriber.Subscriptions.Select(s => s.LineId).ToHashSet();
            var newSubscriptions = linesToSubscribe
                .Where(line => !existingLineIds.Contains(line.Id))
                .Select(line => new Subscription
                {
                    Subscriber = subscriber,
                    Line = line,
                });

            await _context.Subscriptions.AddRangeAsync(newSubscriptions);
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
