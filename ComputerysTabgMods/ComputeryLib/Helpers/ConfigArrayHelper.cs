using System;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;

namespace ComputeryLib.Helpers;

internal static class ConfigArrayHelper {
    internal static void RegisterConverters() {
        if (TomlTypeConverter.CanConvert(typeof(float[]))) { return; }
        TomlTypeConverter.AddConverter(typeof(float[]), new TypeConverter {
            ConvertToString = (obj, _) => string.Join(", ", ((float[])obj).Select(f => f.ToString(CultureInfo.InvariantCulture))),
            ConvertToObject = (str, _) => str.Split([','], StringSplitOptions.RemoveEmptyEntries).Select(s => float.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray()
        });
    }
}
