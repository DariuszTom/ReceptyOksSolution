using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ReceptyOks.Api.Endpoints;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sync")
            .WithTags("Synchronization")
            .RequireRateLimiting("fixed");

        // POST - synchronizacja dwukierunkowa
        group.MapPost("/", async (
            SyncRequest request,
            IValidator<SyncRequest> validator,
            ISyncService syncService,
            ILogger<ISyncService> logger) =>
        {
            var validationResult = await validator.ValidateAsync(request).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                logger.LogWarning("Sync request validation failed: {Errors}",
                    string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var response = await syncService.SyncAsync(request, request.LastSyncedAt).ConfigureAwait(false);
            return Results.Ok(response);
        })
            .WithName("Sync")
            .WithMetadata(new RequestSizeLimitAttribute(200_000_000));

        // GET - pobierz wszystkie dane (początkowa synchronizacja)
        group.MapGet("/full", async (ISyncService syncService) =>
        {
            var response = await syncService.GetFullSyncAsync().ConfigureAwait(false);
            return Results.Ok(response);
        })
            .WithName("FullSync");

        // POST - upload wszystkich danych z klienta (nadpisuje serwer)
        group.MapPost("/upload-all", async (
            SyncRequest request,
            IValidator<SyncRequest> validator,
            ISyncService syncService,
            ILogger<ISyncService> logger) =>
        {
            var validationResult = await validator.ValidateAsync(request).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                logger.LogWarning("Upload-all request validation failed: {Errors}",
                    string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var response = await syncService.UploadAllAsync(request).ConfigureAwait(false);
            return Results.Ok(response);
        })
            .WithName("UploadAll")
            .WithMetadata(new RequestSizeLimitAttribute(200_000_000));
    }
}
