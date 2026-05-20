using System.Reflection;
using Wiretap.Util.Services;
using Wiretap.Util.Skills;

namespace Wiretap.Util;

public abstract class Activity
{
    protected Activity()
    {
        var type = GetType();
        Name = BuildActivityName.For(type);
        LastStatusPolicy = new()
        {
            CanBeVoid = type.GetCustomAttribute<LastStatusPolicy.CanBeVoid>(inherit: true),
            MuteLeaks = type.GetCustomAttribute<LastStatusPolicy.MuteLeaks>(inherit: true)
        };

        MessageTemplatePrefix =
            type.GetCustomAttribute<MessageTemplatePrefix>(inherit: true)
            ?? type.Assembly.GetCustomAttribute<MessageTemplatePrefix>()
            ?? Assembly.GetEntryAssembly()?.GetCustomAttribute<MessageTemplatePrefix>()
            ?? new MessageTemplatePrefix();

        JoinMessageParts =
            type.GetCustomAttribute<JoinMessageParts>(inherit: true)
            ?? type.Assembly.GetCustomAttribute<JoinMessageParts>()
            ?? Assembly.GetEntryAssembly()?.GetCustomAttribute<JoinMessageParts>()
            ?? new JoinMessageParts();
    }


    public abstract string Role { get; }

    public string Name { get; }

    public LastStatusPolicySet LastStatusPolicy { get; }

    public MessageTemplatePrefix? MessageTemplatePrefix { get; }

    public JoinMessageParts JoinMessageParts { get; }

    public abstract class Core : Activity
    {
        public override string Role => nameof(Core);
    }

    public abstract class Buzz : Activity
    {
        public override string Role => nameof(Buzz);
    }
}