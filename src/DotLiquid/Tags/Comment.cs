using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotLiquid.Util;

namespace DotLiquid.Tags
{
    public class Comment : DotLiquid.RawBlock
    {
        private static readonly Regex ShortHandRegex = R.C(Liquid.CommentShorthand);

        public static string FromShortHand(string @string)
        {
            if (@string == null)
                return @string;

            Match match = ShortHandRegex.Match(@string);
            return match.Success ? string.Format(@"{{% comment %}}{0}{{% endcomment %}}", match.Groups[1].Value) : @string;
        }

        public override Task RenderAsync(Context context, TextWriter result)
        {
            return Task.CompletedTask;
        }
    }
}
