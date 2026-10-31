using System;
using System.Linq;
using BepInEx.Configuration;

namespace CustomCrestMod
{
    public class AcceptableEnumList<T> : AcceptableValueBase
        where T : Enum
    {
        public virtual T[] AcceptableEnums { get; }

        public AcceptableEnumList(params T[] acceptableEnums)
            : base(typeof(T))
        {
            if (acceptableEnums == null)
                throw new ArgumentNullException(nameof(acceptableEnums));

            AcceptableEnums = acceptableEnums.Length != 0 ? acceptableEnums : throw new ArgumentException("At least one acceptable value is needed", nameof(acceptableEnums));
        }

        public override object Clamp(object value) => IsValid(value) ? value : AcceptableEnums[0];

        public override bool IsValid(object value)
        {
            if (!(value is T obj))
                return false;

            return AcceptableEnums.Any(x => x.Equals(obj));
        }

        public override string ToDescriptionString() => "# Acceptable values: " + string.Join(", ", AcceptableEnums.Select(x => x.ToString()).ToArray());
    }
}