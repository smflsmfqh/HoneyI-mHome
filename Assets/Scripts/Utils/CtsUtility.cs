using System.Threading;

// 작업별 CancellationTokenSource 수명 관리 (코루틴 대체 UniTask 작업에서 공용)
public static class CtsUtility
{
    // 진행 중인 작업을 취소하고 정리한다
    public static void Cancel(ref CancellationTokenSource cts)
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    // 이전 작업을 취소하고 새 작업용 토큰을 발급한다
    public static CancellationToken Renew(ref CancellationTokenSource cts)
    {
        Cancel(ref cts);
        cts = new CancellationTokenSource();
        return cts.Token;
    }
}
