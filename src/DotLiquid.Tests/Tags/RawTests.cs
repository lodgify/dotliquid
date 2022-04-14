using DotLiquid.Exceptions;
using NUnit.Framework;
using System.Threading.Tasks;

namespace DotLiquid.Tests.Tags
{
    [TestFixture]
    public class RawTests
    {
        [Test]
        public async Task  TestTagInRaw()
        {
            await Helper.AssertTemplateResultAsync("{% comment %} test {% endcomment %}",
                "{% raw %}{% comment %} test {% endcomment %}{% endraw %}");
        }

        [Test]
        public async Task TestOutputInRaw()
        {
            await Helper.AssertTemplateResultAsync("{{ test }}",
                "{% raw %}{{ test }}{% endraw %}");
        }

        [Test]
        public async Task TestRawWithErbLikeTrimmingWhitespace()
        {
            await Helper.AssertTemplateResultAsync("{{ test }}", "{%- raw %}{{ test }}{%- endraw %}");
            await Helper.AssertTemplateResultAsync("{{ test }}", "{% raw -%}{{ test }}{% endraw -%}");
            await Helper.AssertTemplateResultAsync("{{ test }}", "{%- raw -%}{{ test }}{%- endraw -%}");
            await Helper.AssertTemplateResultAsync("{{ test }}", "{%-raw-%}{{ test }}{%-endraw-%}");
        }

        [Test]
        public async Task TestPartialInRaw()
        {
            await Helper.AssertTemplateResultAsync(" Foobar {% invalid ", "{% raw %} Foobar {% invalid {% endraw %}");
            await Helper.AssertTemplateResultAsync(" Foobar invalid %} ", "{% raw %} Foobar invalid %} {% endraw %}");
            await Helper.AssertTemplateResultAsync(" Foobar {{ invalid ", "{% raw %} Foobar {{ invalid {% endraw %}");
            await Helper.AssertTemplateResultAsync(" Foobar invalid }} ", "{% raw %} Foobar invalid }} {% endraw %}");
            await Helper.AssertTemplateResultAsync(" Foobar {% invalid {% {% endraw ", "{% raw %} Foobar {% invalid {% {% endraw {% endraw %}");
            await Helper.AssertTemplateResultAsync(" Foobar {% {% {% ", "{% raw %} Foobar {% {% {% {% endraw %}");
            await Helper.AssertTemplateResultAsync(" test {% raw %} {% endraw %}", "{% raw %} test {% raw %} {% {% endraw %}endraw %}");
            await Helper.AssertTemplateResultAsync(" Foobar {{ invalid 1", "{% raw %} Foobar {{ invalid {% endraw %}{{ 1 }}");
            await Helper.AssertTemplateResultAsync(" Foobar {% foo {% bar %}", "{% raw %} Foobar {% foo {% bar %}{% endraw %}");
        }

        [Test]
        public void TestInvalidRaw()
        {
            Assert.Throws<SyntaxException>(() => Template.Parse("{% raw %} foo"));
            Assert.Throws<SyntaxException>(() => Template.Parse("{% raw } foo {% endraw %}"));
            Assert.Throws<SyntaxException>(() => Template.Parse("{% raw } foo %}{% endraw %}"));
        }
    }
}
