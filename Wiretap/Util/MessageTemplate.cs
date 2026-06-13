using JetBrains.Annotations;

namespace Wiretap.Util;

public record MessageTemplate([StructuredMessageTemplate] string? Template, params object?[] Args) { }

public delegate void ItemFeed<out TPush>(Action<PropertyName, TPush> feed) where TPush : Delegate;
