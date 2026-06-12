namespace Wiretap.Util;

public static class ActivityStatusRole
{
    // core: Marks statuses that are first in the activity's lifecycle.
    public interface IFirst;

    // core: Marks statuses that are last in the activity's lifecycle.
    public interface ILast;
}
