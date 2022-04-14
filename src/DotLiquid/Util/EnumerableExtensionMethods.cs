using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotLiquid.Util
{
    public static class EnumerableExtensionMethods
    {
        public static IEnumerable Flatten(this IEnumerable array)
        {
            foreach (var item in array)
            {
                if (item is string || !(item is IEnumerable))
                {
                    yield return item;
                }
                else
                {
                    foreach (var subitem in Flatten((IEnumerable)item))
                    {
                        yield return subitem;
                    }
                }
            }
        }

        public static async Task EachWithIndexAsync(this IEnumerable<object> array, Func<object, int, Task> callbackAsync)
        {
            int index = 0;
            foreach (object item in array)
            {
                await callbackAsync(item, index);
                ++index;
            }
        }
    }
}
