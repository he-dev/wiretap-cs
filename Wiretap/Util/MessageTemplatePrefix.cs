using Wiretap.Util.Skills;

namespace Wiretap.Util;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
public class MessageTemplatePrefix : Attribute, IWithMessageParts
{
    public void MessageParts(ActivityStatus.Context context, AppendMessagePart append)
    {
        append("{ActivityRole}: {Activity}[{ActivityStatus}]", context.ActivityRole, context.Activity, context.ActivityStatus);
        append("Elapsed: {ElapsedMs:N0} ms", context.ElapsedMs);
    }
}