using System.IO;
using System.Threading.Tasks;

namespace DotLiquid.Tags
{
    public class IfChanged : DotLiquid.Block
    {
        public override async Task RenderAsync(Context context, TextWriter result)
        {
            await context.StackAsync(async () =>
            {
                string tempString;
                using (TextWriter temp = new StringWriter(result.FormatProvider))
                {
                    await RenderAllAsync(NodeList, context, temp);
                    tempString = temp.ToString();
                }

                if (tempString != (context.Registers["ifchanged"] as string))
                {
                    context.Registers["ifchanged"] = tempString;
                    result.Write(tempString);
                }
            });
        }
    }
}
