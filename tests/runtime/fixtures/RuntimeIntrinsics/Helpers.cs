namespace RuntimeIntrinsics;

public static class Helpers
{
    public static string ReachableMessage(string name) => $"helper:{name}";

    public static string UnusedMessage() => "UNUSED_HELPER_SENTINEL";
}
