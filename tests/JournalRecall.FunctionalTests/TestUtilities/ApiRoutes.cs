namespace JournalRecall.FunctionalTests.TestUtilities;

/// <summary>
/// One place for the API routes the functional tests hit, so a route change is caught here rather than
/// scattered across tests (PRD-0003).
/// </summary>
public static class ApiRoutes
{
    public const string Base = "/api";
    public const string Health = Base + "/health";
    public const string Me = Base + "/me";

    public static class Auth
    {
        public const string Register = Base + "/auth/register";
        public const string Login = Base + "/auth/login";
        public const string Refresh = Base + "/auth/refresh";
        public const string Logout = Base + "/auth/logout";
        public const string ChangePassword = Base + "/auth/change-password";
        public const string Config = Base + "/auth/config";
    }

    public static class Admin
    {
        public const string Root = Base + "/admin";
        public const string Ping = Root + "/ping";
        public const string Users = Root + "/users";
        public const string Registration = Root + "/registration";
        public const string AiProvider = Root + "/ai-provider";
        public static string Role(Guid id) => $"{Users}/{id}/role";
        public static string Disable(Guid id) => $"{Users}/{id}/disable";
        public static string Enable(Guid id) => $"{Users}/{id}/enable";
        public static string ResetPassword(Guid id) => $"{Users}/{id}/reset-password";
    }

    public static class Setup
    {
        public const string Root = Base + "/setup";
    }

    public static class People
    {
        public const string Root = Base + "/people";
        public static string Create() => Root;
        public static string Rename(Guid id) => $"{Root}/{id}";
    }

    public static class Topics
    {
        public const string Root = Base + "/topics";
    }

    public static class Sessions
    {
        public const string Root = Base + "/sessions";
        public static string Create() => Root;
        public static string Get(Guid id) => $"{Root}/{id}";
        public static string Draft(Guid id) => $"{Root}/{id}/draft";
        public static string Metadata(Guid id) => $"{Root}/{id}/metadata";
        public static string Cleanup(Guid id) => $"{Root}/{id}/cleanup";
        public static string CleanupStream(Guid id) => $"{Root}/{id}/cleanup/stream";
        public static string PeopleProposalRespond(Guid id) => $"{Root}/{id}/people-proposals/respond";
        public static string Revisions(Guid id) => $"{Root}/{id}/revisions";
        public static string CleanedRevisions(Guid id) => $"{Root}/{id}/cleaned-revisions";
    }
}
