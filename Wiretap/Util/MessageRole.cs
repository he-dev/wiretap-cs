namespace Wiretap.Util;

public abstract class MessageRole
{
    // core: Pure telemetry data.
    public abstract class Data;

    // core: Something nice to know about what is going on.
    public abstract class Note;

    // core: Readable entries meant for the console.
    public abstract class Echo;
}