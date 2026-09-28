using FluentValidation;
using HomeSeeker.Abstractions;
using HomeSeeker.Models;
using ReceptyOks.Api.Services;

namespace ReceptyOks.Api.Endpoints;

public static class HomeSeekerEndpoints
{
    public static void MapHomeSeekerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/homeseeker")
            .WithTags("HomeSeeker")
            .RequireRateLimiting("fixed");

        // POST /profiles - Create new search profile
        group.MapPost("/profiles", async (
            SearchProfileRequest request,
            IValidator<SearchProfileRequest> validator,
            IListingRepository repository) =>
        {
            var validation = await validator.ValidateAsync(request).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var profile = await repository.CreateProfileAsync(request).ConfigureAwait(false);
            return Results.Created($"/api/homeseeker/profiles/{profile.Id}", profile);
        })
        .WithName("CreateSearchProfile");

        // GET /profiles - List all profiles
        group.MapGet("/profiles", async (IListingRepository repository) =>
        {
            var profiles = await repository.GetAllProfilesAsync().ConfigureAwait(false);
            return Results.Ok(profiles);
        })
        .WithName("GetSearchProfiles");

        // GET /profiles/{id} - Get single profile
        group.MapGet("/profiles/{id:guid}", async (Guid id, IListingRepository repository) =>
        {
            var profile = await repository.GetProfileByIdAsync(id).ConfigureAwait(false);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        })
        .WithName("GetSearchProfile");

        // PUT /profiles/{id} - Update profile
        group.MapPut("/profiles/{id:guid}", async (
            Guid id,
            SearchProfileRequest request,
            IValidator<SearchProfileRequest> validator,
            IListingRepository repository) =>
        {
            var validation = await validator.ValidateAsync(request).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var profile = await repository.UpdateProfileAsync(id, request).ConfigureAwait(false);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        })
        .WithName("UpdateSearchProfile");

        // DELETE /profiles/{id} - Soft delete profile
        group.MapDelete("/profiles/{id:guid}", async (Guid id, IListingRepository repository) =>
        {
            var deleted = await repository.DeleteProfileAsync(id).ConfigureAwait(false);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteSearchProfile");

        // POST /profiles/{id}/scan - Trigger on-demand scan (strict rate limit)
        group.MapPost("/profiles/{id:guid}/scan", async (
            Guid id,
            IListingRepository repository,
            ScanTriggerQueue triggerQueue) =>
        {
            var profile = await repository.GetProfileByIdAsync(id).ConfigureAwait(false);
            if (profile is null || profile.IsDeleted)
            {
                return Results.NotFound();
            }

            var queued = triggerQueue.TryQueueScan(id);
            if (!queued)
            {
                return Results.Problem(
                    title: "Queue Full",
                    detail: "Too many scan requests. Try again later.",
                    statusCode: 503);
            }

            return Results.Accepted($"/api/homeseeker/profiles/{id}/scans", new { Message = "Scan queued", ProfileId = id });
        })
        .RequireRateLimiting("strict")
        .WithName("TriggerScan");

        // GET /profiles/{id}/listings - Get listings for profile (paginated)
        group.MapGet("/profiles/{id:guid}/listings", async (
            Guid id,
            int pageNumber = 1,
            int pageSize = 20,
            int? minScore = null,
            string? sort = null,
            IListingRepository repository = default!) =>
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var profile = await repository.GetProfileByIdAsync(id).ConfigureAwait(false);
            if (profile is null || profile.IsDeleted)
            {
                return Results.NotFound();
            }

            var (listings, totalCount) = await repository.GetListingsAsync(
                id, pageNumber, pageSize, minScore, sort).ConfigureAwait(false);

            return Results.Ok(new
            {
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                Data = listings
            });
        })
        .WithName("GetProfileListings");

        // GET /profiles/{id}/scans - Get scan history
        group.MapGet("/profiles/{id:guid}/scans", async (
            Guid id,
            int count = 20,
            IListingRepository repository = default!) =>
        {
            if (count < 1) count = 1;
            if (count > 100) count = 100;

            var profile = await repository.GetProfileByIdAsync(id).ConfigureAwait(false);
            if (profile is null || profile.IsDeleted)
            {
                return Results.NotFound();
            }

            var scans = await repository.GetScanHistoryAsync(id, count).ConfigureAwait(false);
            return Results.Ok(scans);
        })
        .WithName("GetProfileScans");

        // GET /scans/{id}/report - Get scan report HTML
        group.MapGet("/scans/{id:guid}/report", async (Guid id, IListingRepository repository) =>
        {
            var scanRun = await repository.GetScanRunByIdAsync(id).ConfigureAwait(false);
            if (scanRun is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(scanRun.ReportHtml))
            {
                return Results.NotFound(new { Message = "Report not available" });
            }

            return Results.Content(scanRun.ReportHtml, "text/html");
        })
        .WithName("GetScanReport");
    }
}
