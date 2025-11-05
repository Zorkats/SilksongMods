using System.Reflection;

namespace SilksongMods.CompletionTracker
{
    public static class ReflectionUtil
    {
        public static T GetPrivateField<T>(object obj, string fieldName)
        {
            if (obj == null) return default(T);

            var field = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) return default(T);

            var value = field.GetValue(obj);
            return (T)value;
        }
    }
}