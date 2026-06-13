using JetBrains.Annotations;

namespace Wiretap.Util;

public record MessageTemplate([StructuredMessageTemplate] string? Template, params object?[] Args) { }

public delegate void SchemaFeed<TDelegate>(Action<PropertyName, TDelegate> feed) where TDelegate : Delegate;
