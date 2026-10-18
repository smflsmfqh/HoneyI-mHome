// coplay execute_script 진입점. Assets 밖에 두어 Unity가 컴파일하지 않게 한다.
public static class RunTunnelingBenchmark
{
    public static string Execute() => TunnelingBenchmark.Run();
}
