namespace Nalix.Dashboard.Application.Settings;

internal static class DevDashboardKey
{
    public const string ApiKey = "aa4992074d220cbfe51e59be6fbdbc5d8c7eac733ff35582f7d679347fbfbaee";

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
