namespace ReceptyOks.Shared;

/// <summary>
/// HTTP routes shared by the API (endpoint mapping) and its clients, so both sides stay in sync.
/// </summary>
public static class ApiRoutes
{
    public const string Prefix = "/api";

    public const string Health = "/health";
    public const string Alive = "/alive";

    public static class Ai
    {
        public const string Base = Prefix + "/ai";
        public const string Messages = Base + "/v1/messages";
    }

    public static class Auth
    {
        public const string Base = Prefix + "/auth";
        public const string Validate = Base + "/validate";
    }

    public static class Categories
    {
        public const string Base = Prefix + "/categories";
        public static string ById(Guid id) => $"{Base}/{id}";
    }

    public static class Ingredients
    {
        public const string Base = Prefix + "/ingredients";
        public static string ById(Guid id) => $"{Base}/{id}";
    }

    public static class Recipes
    {
        public const string Base = Prefix + "/recipes";
        public static string ById(Guid id) => $"{Base}/{id}";
    }

    public static class HomeSeeker
    {
        public const string Base = Prefix + "/homeseeker";
        public const string Profile = Base + "/profile";
        public const string Listings = Base + "/listings";
        public const string Map = Base + "/map";
        public const string Portals = Base + "/portals";
        public const string Scans = Base + "/scans";
        public static string ScanReport(Guid scanId) => $"{Scans}/{scanId}/report";
    }

    public static class ShoppingList
    {
        public const string Base = Prefix + "/shopping-list";
        public const string Bulk = Base + "/bulk";
        public const string BulkBought = Bulk + "/bought";
        public const string ClearBought = Base + "/clear-bought";
        public const string ClearAll = Base + "/clear-all";
        public const string Stats = Base + "/stats";
        public static string ById(Guid id) => $"{Base}/{id}";
        public static string Bought(Guid id) => $"{ById(id)}/bought";
        public static string Unbought(Guid id) => $"{ById(id)}/unbought";
        public static string Permanent(Guid id) => $"{ById(id)}/permanent";
    }

    public static class Sync
    {
        public const string Base = Prefix + "/sync";
        public const string Full = Base + "/full";
        public const string UploadAll = Base + "/upload-all";
    }
}
