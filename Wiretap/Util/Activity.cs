using System.Reflection;
using Wiretap.Util.Services;

namespace Wiretap.Util;

public abstract class Activity
{
    protected Activity()
    {
        var type = GetType();
        Name = GetActivityName.For(type);
        LastStatusPolicy = new()
        {
            CanBeVoid = type.GetCustomAttribute<LastStatusPolicy.CanBeVoid>(inherit: true),
            MuteLeaks = type.GetCustomAttribute<LastStatusPolicy.MuteLeaks>(inherit: true)
        };

        MessageTemplatePrefix =
            type.GetCustomAttribute<MessageTemplatePrefix>(inherit: true)
            ?? type.Assembly.GetCustomAttribute<MessageTemplatePrefix>()
            ?? Assembly.GetEntryAssembly()?.GetCustomAttribute<MessageTemplatePrefix>()
            ?? new MessageTemplatePrefix.Full();

        MessageTemplateSchema =
            type.GetCustomAttribute<MessageTemplateSchema>(inherit: true)
            ?? type.Assembly.GetCustomAttribute<MessageTemplateSchema>()
            ?? Assembly.GetEntryAssembly()?.GetCustomAttribute<MessageTemplateSchema>()
            ?? new MessageTemplateSchema();
    }


    public abstract string Role { get; }

    public string Name { get; }

    public LastStatusPolicySet LastStatusPolicy { get; }

    public MessageTemplatePrefix? MessageTemplatePrefix { get; }

    public MessageTemplateSchema MessageTemplateSchema { get; }

    public abstract class Core : Activity
    {
        public override string Role => nameof(Core);
    }

    public abstract class Buzz : Activity
    {
        public override string Role => nameof(Buzz);
    }
}