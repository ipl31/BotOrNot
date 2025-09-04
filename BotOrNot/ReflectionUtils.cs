namespace BotOrNot;

public static class ReflectionUtils
{
    public static string GetProp(object obj, string propName)
    {
        if (string.IsNullOrWhiteSpace(propName))
            throw new ArgumentException($"Prop name is null or empty");

        var prop = obj.GetType().GetProperty(propName);
        var value = prop?.GetValue(obj);
        var stringValue = value?.ToString();
        if (string.IsNullOrEmpty(stringValue))
            return Constants.Unknown;
        return stringValue; 
    }
}
