using System.Security.Claims;
using FluentValidation;
using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using HomeSeeker.Services;
using Microsoft.Extensions.Options;
using ReceptyOks.Api.Services;

namespace ReceptyOks.Api.Endpoints;

public static class HomeSeekerEndpoints
{
    private const int MapDefaultLimit = 50;
    private const int MapMaxLimit = 200;

    public static void MapHomeSeekerEndpoints(this WebApplication app)
    {
        // HomeSeeker is a household feature: there is exactly one shared search
        // profile that every authenticated user can read and edit, one periodic
        // scan (schedule configured at deployment via HomeSeeker options) and one
        // shared result set. In addition to the global API key a JWT bearer token
        // is still required; its `sub` claim is only recorded as "last edited by".
        var group = app.MapGroup("/api/homeseeker")
            .WithTags("HomeSeeker")
            .RequireRateLimiting("fixed")
            .RequireAuthorization();

        // GET /profile - Current scan parameters (404 until configured)
        group.MapGet("/profile", async (IListingRepository repository, CancellationToken ct) =>
        {
            var profile = await repository.GetSharedProfileAsync(ct).ConfigureAwait(false);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        })
        .WithName("GetSharedProfile");

        // PUT /profile - Set scan parameters for the next periodic scan
        group.MapPut("/profile", async (
            SearchProfileRequest request,
            ClaimsPrincipal user,
            IValidator<SearchProfileRequest> validator,
            IListingRepository repository,
            ICityCenterProvider cityCenterProvider,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            if (CallerIdentity.GetOwnerId(user) is not { } updatedBy)
            {
                return Results.Unauthorized();
            }

            var validation = await validator.ValidateAsync(request, ct).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var previous = await repository.GetSharedProfileAsync(ct).ConfigureAwait(false);
            var profile = await repository.UpsertSharedProfileAsync(request, updatedBy, ct).ConfigureAwait(false);

            // Distances are relative to "our location" (or the city centre); keep the list,
            // filters and map consistent right away instead of waiting for the next scan.
            if (previous?.Home != profile.Home ||
                (profile.Home is null && !string.Equals(previous?.City, profile.City, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var reference = profile.Home
                        ?? await cityCenterProvider.GetCenterAsync(profile.City, ct).ConfigureAwait(false);
                    if (reference is { } point)
                        await repository.RecomputeDistancesAsync(profile.Id, point, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The profile is saved; distances are refreshed by the next scan.
                    loggerFactory.CreateLogger("HomeSeeker").LogWarning(ex, "Recomputing distances failed");
                }
            }

            return Results.Ok(profile);
        })
        .WithName("UpdateSharedProfile");

        // GET /listings - Shared scan results (paginated)
        group.MapGet("/listings", async (
            int pageNumber = 1,
            int pageSize = 20,
            int? minScore = null,
            string? sort = null,
            PropertyTypes? propertyType = null,
            string? portal = null,
            double? maxDistanceKm = null,
            bool priceDropOnly = false,
            bool evaluatedOnly = false,
            IListingRepository repository = default!,
            CancellationToken ct = default) =>
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var profile = await repository.GetSharedProfileAsync(ct).ConfigureAwait(false);
            if (profile is null)
            {
                return Results.Ok(new
                {
                    TotalCount = 0,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = 0,
                    Data = Array.Empty<HouseListing>()
                });
            }

            var query = new ListingQuery
            {
                MinScore = minScore,
                Sort = sort,
                // Only a single known type filters; combined flags or garbage mean "any".
                PropertyType = propertyType is PropertyTypes.House or PropertyTypes.Apartment ? propertyType : null,
                Portal = portal,
                MaxDistanceKm = maxDistanceKm is > 0 ? maxDistanceKm : null,
                PriceDropOnly = priceDropOnly,
                EvaluatedOnly = evaluatedOnly
            };
            var (listings, totalCount) = await repository.GetListingsAsync(
                profile.Id, pageNumber, pageSize, query, ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                Data = listings
            });
        })
        .WithName("GetListings");

        // GET /map - Scored listings with coordinates (best first, development/duplicate copies capped)
        group.MapGet("/map", async (
            int limit = MapDefaultLimit,
            int? minScore = null,
            PropertyTypes? propertyType = null,
            string? portal = null,
            double? maxDistanceKm = null,
            bool priceDropOnly = false,
            IListingRepository repository = default!,
            IOptions<HomeSeekerOptions> options = default!,
            CancellationToken ct = default) =>
        {
            limit = Math.Clamp(limit, 1, MapMaxLimit);

            var profile = await repository.GetSharedProfileAsync(ct).ConfigureAwait(false);
            if (profile is null)
                return Results.Ok(Array.Empty<MapListing>());

            var query = new ListingQuery
            {
                MinScore = minScore,
                PropertyType = propertyType is PropertyTypes.House or PropertyTypes.Apartment ? propertyType : null,
                Portal = portal,
                MaxDistanceKm = maxDistanceKm is > 0 ? maxDistanceKm : null,
                PriceDropOnly = priceDropOnly,
                EvaluatedOnly = true
            };

            // Same capping as the e-mail report, so one development doesn't fill the map.
            var maxPerGroup = options.Value.MaxListingsPerDevelopment;
            var groupingSample = await repository.GetGroupingSampleAsync(profile.Id, ct).ConfigureAwait(false);
            var fetch = limit * 3;
            List<HouseListing> capped;
            while (true)
            {
                var ranked = await repository.GetMapListingsAsync(profile.Id, query, fetch, ct).ConfigureAwait(false);
                var groups = new ListingGroups(ranked.Concat(groupingSample));
                capped = ListingGroups.CapPerGroup(ranked, maxPerGroup, groups).Take(limit).ToList();
                if (capped.Count >= limit || ranked.Count < fetch || fetch >= MapMaxLimit * 16)
                    break;
                fetch *= 2;
            }

            return Results.Ok(capped.Select(MapListing.From).ToList());
        })
        .WithName("GetMapListings");

        // GET /scans - Scan history for the shared profile
        group.MapGet("/scans", async (
            int count = 20,
            IListingRepository repository = default!,
            CancellationToken ct = default) =>
        {
            if (count < 1) count = 1;
            if (count > 100) count = 100;

            var profile = await repository.GetSharedProfileAsync(ct).ConfigureAwait(false);
            if (profile is null)
            {
                return Results.Ok(Array.Empty<ScanRun>());
            }

            var scans = await repository.GetScanHistoryAsync(profile.Id, count, ct).ConfigureAwait(false);
            return Results.Ok(scans);
        })
        .WithName("GetScans");

        // GET /scans/{id}/report - Get scan report HTML
        group.MapGet("/scans/{id:guid}/report", async (Guid id, IListingRepository repository, HttpContext http) =>
        {
            var scanRun = await repository.GetScanRunByIdAsync(id, http.RequestAborted).ConfigureAwait(false);
            if (scanRun is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(scanRun.ReportHtml))
            {
                return Results.NotFound(new { Message = "Report not available" });
            }

            // Defense in depth: the report is produced from a server-controlled
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
