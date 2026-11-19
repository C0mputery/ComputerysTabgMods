using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;

namespace ComputeryLib.Helpers;

internal static class ConfigArrayHelper {
    internal static void RegisterConverters() {
        if (!TomlTypeConverter.CanConvert(typeof(float[]))) {
            TomlTypeConverter.AddConverter(typeof(float[]), new TypeConverter {
                ConvertToString = (obj, _) => string.Join(", ", ((float[])obj).Select(f => f.ToString(CultureInfo.InvariantCulture))),
                ConvertToObject = (str, _) => str.Split([','], StringSplitOptions.RemoveEmptyEntries).Select(s => float.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray()
            });
        }
        
        if (!TomlTypeConverter.CanConvert(typeof(Dictionary<int, float>))) {
            TomlTypeConverter.AddConverter(typeof(Dictionary<int, float>), new TypeConverter {
                ConvertToString = (obj, _) => string.Join(", ", ((Dictionary<int, float>)obj).Select(entry => $"{entry.Key.ToString(CultureInfo.InvariantCulture)}: {entry.Value.ToString(CultureInfo.InvariantCulture)}")),
                ConvertToObject = (str, _) => str.Split([','], StringSplitOptions.RemoveEmptyEntries)
                    .Select(pair => pair.Split(':'))
                    .ToDictionary(parts => int.Parse(parts[0].Trim(), CultureInfo.InvariantCulture), parts => float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture))
            });
        }
    }
}