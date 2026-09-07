using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace Backend.Services;

/// <summary>
/// SSE 实时推送的内存广播服务。
/// 维护每个在线用户的活跃连接（支持同一用户多标签页），
/// 业务侧在通知创建/私信落库后调用 Publish，事件帧写入对应连接的通道。
/// </summary>
public interface INotificationPushService
{
    /// <summary>为用户注册一条推送连接，返回连接标识与事件读取端。</summary>
    (Guid ConnectionId, ChannelReader<string> Events) Subscribe(int userId);

    /// <summary>注销连接；该用户全部连接断开后释放资源。</summary>
    void Unsubscribe(int userId, Guid connectionId);

    /// <summary>向用户的全部活跃连接广播一条 SSE 事件帧；无人订阅时静默忽略。</summary>
    void Publish(int userId, string eventName, object payload);
}

public class NotificationPushService : INotificationPushService
{
    private const int ChannelCapacity = 16;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<int, ConcurrentDictionary<Guid, Channel<string>>> _connections = new();

    public (Guid ConnectionId, ChannelReader<string> Events) Subscribe(int userId)
    {
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(ChannelCapacity)
        {
            SingleReader = true,
            // 慢客户端直接丢帧：实时推送宁可丢失也不阻塞业务线程，前端有全量刷新兜底
            FullMode = BoundedChannelFullMode.DropWrite
        });

        var connectionId = Guid.NewGuid();
        var connections = _connections.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Channel<string>>());
        connections[connectionId] = channel;
        return (connectionId, channel.Reader);
    }

    public void Unsubscribe(int userId, Guid connectionId)
    {
        if (!_connections.TryGetValue(userId, out var connections))
            return;
        if (!connections.TryRemove(connectionId, out var channel))
            return;

        channel.Writer.TryComplete();
        if (connections.IsEmpty)
        {
            // 仅当映射仍是我们看到的空集合时才移除，避免与并发 Subscribe 竞争
            _connections.TryRemove(KeyValuePair.Create(userId, connections));
        }
    }

    public void Publish(int userId, string eventName, object payload)
    {
        if (!_connections.TryGetValue(userId, out var connections))
            return;

        string frame;
        try
        {
            frame = $"event: {eventName}\ndata: {JsonSerializer.Serialize(payload, JsonOptions)}\n\n";
        }
        catch
        {
            // 序列化失败按无推送处理，不影响业务主流程
            return;
        }

        foreach (var channel in connections.Values)
        {
            channel.Writer.TryWrite(frame);
        }
    }
}
