using GameDialog.Common;

namespace GameDialog.Runner;

public class TextStorage
{
    private readonly Dictionary<string, TextVariant> _storage = [];

    public void Clear() => _storage.Clear();

    public bool Contains(string key) => _storage.ContainsKey(key);

    public void SetValue(string key, TextVariant value) => _storage[key] = value;

    public void SetValue(string key, string value) => _storage[key] = new(value);

    public void SetValue(string key, float value) => _storage[key] = new(value);

    public void SetValue(string key, bool value) => _storage[key] = new(value);

    public bool TryGetValue(string key, out TextVariant value)
    {
        return _storage.TryGetValue(key, out value);
    }

    public bool TryGetValue<T>(string key, out T? value)
    {
        value = default;

        if (!_storage.TryGetValue(key, out TextVariant tVar))
            return false;

        if (!tVar.TryGetValue(out value))
            return false;

        return true;
    }
}
