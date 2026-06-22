namespace Wiretap.Util.Buzz;

public interface IArrangeMessageParts
{
    IReadOnlyList<MessageTemplate> By(PropertyName root, MessagePartMap parts);
}

public class ArrangeMessageParts : IArrangeMessageParts
{
    public IReadOnlyList<MessageTemplate> By(PropertyName root, MessagePartMap parts)
    {
        return
        [
            ..new[]
            {
                parts.Pop(root.Activity.Name),
                parts.Pop(root.Activity.DurationMs),
            }.OfType<MessageTemplate>(),
            ..parts.Select(x => x.Value),
        ];
    }
}
