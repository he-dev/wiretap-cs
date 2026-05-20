using JetBrains.Annotations;

namespace Wiretap.Util;

public record MessageTemplate([StructuredMessageTemplate] string? Template, params object?[] Args) { }