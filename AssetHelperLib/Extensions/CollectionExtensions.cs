using System;
using System.Collections.Generic;
using System.Text;

namespace AssetHelperLib.Extensions;

internal static class CollectionExtensions
{
    public static void AddEntry<TKey, TValue>(this Dictionary<TKey, List<TValue>> self, TKey key, TValue value)
    {
        if (self.TryGetValue(key, out List<TValue> data))
        {
            data.Add(value);
        }
        else
        {
            self[key] = [value];
        }
    }
}
