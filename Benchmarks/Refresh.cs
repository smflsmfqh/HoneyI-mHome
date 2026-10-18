public static class RefreshAssets
{
    public static string Execute() { UnityEditor.AssetDatabase.Refresh(); return "refreshed"; }
}
