namespace Infinium.DesktopHost;

public sealed class RendererBridgeMetrics
{
    private int maximumInboundRequestBytes;
    private int maximumOutboundResponseBytes;
    private int maximumOutboundEventBytes;

    public int MaximumInboundRequestBytes => Volatile.Read(ref maximumInboundRequestBytes);
    public int MaximumOutboundResponseBytes => Volatile.Read(ref maximumOutboundResponseBytes);
    public int MaximumOutboundEventBytes => Volatile.Read(ref maximumOutboundEventBytes);

    internal void ObserveInboundRequest(int bytes) => ObserveMaximum(ref maximumInboundRequestBytes, bytes);

    internal void ObserveOutbound(string serialized, int bytes)
    {
        if (serialized.Contains("\"message_kind\":\"response\"", StringComparison.Ordinal))
        {
            ObserveMaximum(ref maximumOutboundResponseBytes, bytes);
        }
        else if (serialized.Contains("\"message_kind\":\"event\"", StringComparison.Ordinal))
        {
            ObserveMaximum(ref maximumOutboundEventBytes, bytes);
        }
    }

    private static void ObserveMaximum(ref int target, int candidate)
    {
        int observed = Volatile.Read(ref target);
        while (candidate > observed)
        {
            int prior = Interlocked.CompareExchange(ref target, candidate, observed);
            if (prior == observed)
            {
                return;
            }
            observed = prior;
        }
    }
}
