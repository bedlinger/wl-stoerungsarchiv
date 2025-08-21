﻿
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using System.Text.Json.Nodes;
using wls_backend.Data;
using wls_backend.Models.Domain;
using wls_backend.Models.Enums;

namespace wls_backend.Services
{
    public class WlUpdateService : BackgroundService
    {
        private readonly IConfiguration _config;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly HttpClient _httpClient;
        private readonly ILogger<WlUpdateService> _logger;

        public WlUpdateService(IConfiguration config, IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory, ILogger<WlUpdateService> logger)
        {
            _config = config;
            _scopeFactory = scopeFactory;
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.DefaultRequestHeaders.Add(HeaderNames.Accept, "application/json");
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int period = _config.GetValue<int>("WlUpdate:Period");
            using PeriodicTimer timer = new(TimeSpan.FromSeconds(period));
            do
            {
                try
                {
                    await UpdateDb();
                    _logger.LogInformation("Database updated successfully at {Time}", DateTime.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error updating database: {ex.Message}");
                }
            } while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task UpdateDb()
        {
            string uri = _config["WlUpdate:Uri"]!;
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to fetch data from {uri}. Status code: {response.StatusCode}");
            }
            var content = await response.Content.ReadAsStringAsync();

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notificationsServive = scope.ServiceProvider.GetRequiredService<NotificationsService>();

            var notificationEvents = new List<DisturbanceEvent>();

            var openDisturbances = await context.DisturbanceWithAll
                .Where(d => d.EndedAt == null)
                .ToListAsync();
            var processedDisturbanceIds = new List<string>();

            var rootNode = JsonNode.Parse(content) ?? throw new Exception("Failed to parse JSON response");
            var responseDisturbances = rootNode["data"]!["trafficInfos"]!
                .AsArray()
                .Select(n => DisturbanceFromJsonNode(n!, context));

            foreach (var responseDisturbance in responseDisturbances)
            {
                if (processedDisturbanceIds.Any(i => i == responseDisturbance.Id))
                    continue; // already processed this disturbance
                processedDisturbanceIds.Add(responseDisturbance.Id);

                var dbDisturbance = openDisturbances
                    .FirstOrDefault(d => d.Id == responseDisturbance.Id);

                var relatedDisturbances = responseDisturbances
                    .Where(d => d.Id == responseDisturbance.Id)
                    .ToList();
                if (relatedDisturbances.Count > 1)
                {
                    responseDisturbance.Lines = relatedDisturbances.SelectMany(d => d.Lines).Distinct().ToList();
                    responseDisturbance.Descriptions.Last().Text = string.Join(" / ", relatedDisturbances.Select(d => d.Descriptions.Last().Text));
                }

                if (dbDisturbance == null)
                {
                    dbDisturbance = context.DisturbanceWithAll
                        .Where(d => d.Id == responseDisturbance.Id)
                        .FirstOrDefault();

                    if (dbDisturbance == null)  // new disturbance
                    {
                        context.Disturbance.Add(responseDisturbance);
                        notificationEvents.Add(new DisturbanceEvent(EventType.New, responseDisturbance));
                        continue;
                    }

                    dbDisturbance.EndedAt = null; // reset endedAt for existing disturbances
                    notificationEvents.Add(new DisturbanceEvent(EventType.Reopened, dbDisturbance));
                }

                // existing disturbance
                if (dbDisturbance.Title != responseDisturbance.Title)
                {
                    dbDisturbance.Title = responseDisturbance.Title;
                    dbDisturbance.Type = DisturbanceTypeHelper.FromTitle(dbDisturbance.Title);
                }
                if (dbDisturbance.Lines.Count < responseDisturbance.Lines.Count)
                {
                    dbDisturbance.Lines = responseDisturbance.Lines;
                }
                if (dbDisturbance.Descriptions.LastOrDefault()?.Text != responseDisturbance.Descriptions.Last().Text)
                {
                    var newDescription = new DisturbanceDescription()
                    {
                        DisturbanceId = dbDisturbance.Id,
                        CreatedAt = DateTime.Now,
                        Text = responseDisturbance.Descriptions.Last().Text
                    };
                    dbDisturbance.Descriptions.Add(newDescription);
                    notificationEvents.Add(new DisturbanceEvent(EventType.Updated, dbDisturbance, newDescription.Text));
                }
            }

            // close disturbances that are no longer present in the response
            var resolvedDisturbances = openDisturbances.Where(d => !processedDisturbanceIds.Contains(d.Id)).ToList();
            foreach (var closedDisturbance in resolvedDisturbances)
            {
                closedDisturbance.EndedAt = DateTime.Now;
                notificationEvents.Add(new DisturbanceEvent(EventType.Resolved, closedDisturbance));
            }

            await context.SaveChangesAsync();

            await notificationsServive.SendNotifications(notificationEvents);
        }

        private Disturbance DisturbanceFromJsonNode(JsonNode node, AppDbContext context)
        {
            var id = node["name"]!.GetValue<string>();
            if (id.Count(id => id == '-') > 1)
            {
                id = id.Remove(id.LastIndexOf('-'));
            }
            var title = node["title"]!.GetValue<string>();
            var description = node["description"]!.GetValue<string>();
            var startedAt = DateTime.Parse(node["time"]!["start"]!.GetValue<string>());
            var lines = node["attributes"]!["relatedLineTypes"]!
                .AsObject()
                .Select(l => HandleLine(l.Key, l.Value!.GetValue<string>(), context))
                .ToList();
            return new Disturbance
            {
                Id = id,
                Title = title,
                Type = DisturbanceTypeHelper.FromTitle(title),
                StartedAt = startedAt,
                Descriptions = [new DisturbanceDescription()
                        {
                            DisturbanceId = id,
                            CreatedAt = startedAt,
                            Text = description
                        }],
                Lines = lines
            };
        }

        private Line HandleLine(string id, string type, AppDbContext context)
        {
            var dbLine = context.Line.Find(id);
            if (dbLine == null)
            {
                dbLine = new Line
                {
                    Id = id,
                    Type = LineTypeHelper.FromType(type),
                    DisplayName = id
                };
                context.Line.Add(dbLine);
            }
            return dbLine;
        }
    }
}
