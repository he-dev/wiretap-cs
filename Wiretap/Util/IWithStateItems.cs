namespace Wiretap.Util;

public delegate void AddStateItem(string key, object? value);

public interface IWithStateItems
{
    void StateItems(AddStateItem add);
}