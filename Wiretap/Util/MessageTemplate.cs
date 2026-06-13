using JetBrains.Annotations;

namespace Wiretap.Util;

public record MessageTemplate([StructuredMessageTemplate] string? Template, params object?[] Args) { }

public delegate void SchemaFeed<in TDelegate>(PropertyName root, TDelegate next) where TDelegate : Delegate;
