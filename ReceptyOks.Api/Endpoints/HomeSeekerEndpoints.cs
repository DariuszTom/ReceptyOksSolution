using System.Security.Claims;
using FluentValidation;
using HomeSeeker.Abstractions;
using HomeSeeker.Models;
using ReceptyOks.Api.Services;

namespace ReceptyOks.Api.Endpoints;

public static class HomeSeekerEndpoints
{
    public static void MapHomeSeekerEndpoints(this WebApplication app)
    {
        // Every HomeSeeker endpoint operates on caller-owned data, so in addition to the
        // global API key (ApiKeyAuthMiddleware) a JWT bearer token identifying the owner
        // is required. The owner id comes from the token's `sub` claim and every
        // repository call below is scoped to it; mismatches surface as 404 so that the
        // existence of other owners' profiles is not leaked.
        var group = app.MapGroup("/api/homeseeker")
            .WithTags("HomeSeeker")
            .RequireRateLimiting("fixed")
            .RequireAuthorization();

        // POST /profiles - Create new search profile
        group.MapPost("/profiles", async (
            SearchProfileRequest request,
            ClaimsPrincipal user,
            IValidator<SearchProfileRequest> validator,
            IListingRepository repository) =>
        {
            if (CallerIdentity.GetOwnerId(user) is not { } ownerId)
            {
                return Results.Unauthorized();
            }

            var validation = await validator.ValidateAsync(request).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var profile = await repository.CreateProfileAsync(request, ownerId).ConfigureAwait(false);
            return Results.Created($"/api/homeseeker/profiles/{profile.Id}", profile);
        })
        .WithName("CreateSearchProfile");

        // GET /profiles - List caller's profiles
        group.MapGet("/profiles", async (ClaimsPrincipal user, IListingRepository repository) =>
        {
            if (CallerIdentity.GetOwnerId(user) is not { } ownerId)
            {
                return Results.Unauthorized();
            }

            var profiles = await repository.GetAllProfilesAsync(ownerId).ConfigureAwait(false);
            return Results.Ok(profiles);
        })
        .WithName("GetSearchProfiles");

        // GET /profiles/{id} - Get single profile
        group.MapGet("/profiles/{id:guid}", async (Guid id, ClaimsPrincipal user, IListingRepository repository) =>
        {
            if (CallerIdentity.GetOwnerId(user) is not { } ownerId)
            {
                return Results.Unauthorized();
            }

            var profile = await repository.GetProfileByIdAsync(id, ownerId).ConfigureAwait(false);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        })
        .WithName("GetSearchProfile");

        // PUT /profiles/{id} - Update profile
        group.MapPut("/profiles/{id:guid}", async (
            Guid id,
            SearchProfileRequest request,
            ClaimsPrincipal user,
            IValidator<SearchProfileRequest> validator,
            IListingRepository repository) =>
        {
            if (CallerIdentity.GetOwnerId(user) is not { } ownerId)
            {
                return Results.Unauthorized();
            }

            var validation = await validator.ValidateAsync(request).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var profile = await repository.UpdateProfileAsync(id, ownerId, request).ConfigureAwait(false);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        })
        .WithName("UpdateSearchProfile");

        // DELETE /profiles/{id} - Soft delete profile
        group.MapDelete("/profiles/{id:guid}", async (Guid id, ClaimsPrincipal user, IListingRepository repository) =>
        {
            if (CallerIdentity.GetOwnerId(user) is not { } ownerId)
            {
                return Results.Unauthorized();
            }

            var deleted = await repository.DeleteProfileAsync(id, ownerId).ConfigureAwait(false);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteSearchProfile");

        // POST /profiles/{id}/scan - Trigger on-demand scan (strict rate limit)
        group.MapPost("/profiles/{id:guid}/scan", async (
            Guid id,
            ClaimsPrincipal user,
            IListingRepository repository,
            ScanTriggerQueue triggerQueue) =>
        {
            if (CallerIdentity.GetOwnerId(user) is not { } ownerId)
            {
                return Results.Unauthorized();
            }

            var profile = await repository.GetProfileByIdAsync(id, ownerId).ConfigureAwait(false);
            if (profile is null)
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
            ClaimsPrincipal user,
            int pageNumber = 1,
            int pageSize = 20,
            int? minScore = null,
            string? sort = null,
            IListingRepository repository = default!) =>
        {
            if (CallerIdentity.GetOwnerId(user) is not { } ownerId)
            {
                return Results.Unauthorized();
            }

            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var profile = await repository.GetProfileByIdAsync(id, ownerId).ConfigureAwait(false);
            if (profile is null)
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
            ClaimsPrincipal user,
            int count = 20,
            IListingRepository repository = default!) =>
        {
            if (CallerIdentity.GetOwnerId(user) is not { } ownerId)
            {
                return Results.Unauthorized();
            }

            if (count < 1) count = 1;
            if (count > 100) count = 100;

            var profile = await repository.GetProfileByIdAsync(id, ownerId).ConfigureAwait(false);
            if (profile is null)
            {
                return Results.NotFound();
            }

            var scans = await repository.GetScanHistoryAsync(id, count).ConfigureAwait(false);
            return Results.Ok(scans);
        })
        .WithName("GetProfileScans");

        // GET /scans/{id}/report - Get scan report HTML
        group.MapGet("/scans/{id:guid}/report", async (Guid id, IListingRepository repository, HttpContext http) =>
        {
            if (CallerIdentity.GetOwnerId(http.User) is not { } ownerId)
            {
                return Results.Unauthorized();
            }

            var scanRun = await repository.GetScanRunByIdAsync(id, ownerId, http.RequestAborted).ConfigureAwait(false);
            if (scanRun is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(scanRun.ReportHtml))
            {
                return Results.NotFound(new { Message = "Report not available" });
            }

            // Defense in depth: the report is now produced from a server-controlled
            // template, but we still serve it with a very restrictive CSP so that
            // any future regression that lets attacker-controlled HTML through
            // cannot execute script, pull remote resources, or be framed.
            http.Response.Headers["Content-Security-Policy"] =
                "default-src 'none'; style-src 'unsafe-inline'; img-src https: data:; frame-ancestors 'none'; base-uri 'none'; form-action 'none'; sandbox";
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            http.Response.Headers["X-Frame-Options"] = "DENY";
            http.Response.Headers["Referrer-Policy"] = "no-referrer";

            return Results.Content(scanRun.ReportHtml, "text/html; charset=utf-8");
        })
        .WithName("GetScanReport");
    }
}
