using NUnit.Framework;
using DotLiquid.Exceptions;
using DotLiquid.NamingConventions;
using System.Threading.Tasks;

namespace DotLiquid.Tests
{
    [TestFixture]
    public class StudlyCapsTests
    {
        [Test]
        public async Task TestSimpleVariablesStudlyCaps()
        {
            var template = "{{ Greeting }} {{ Name }}";
            await Helper.AssertTemplateResultAsync(
                expected: "Hello Tobi",
                template: template,
                anonymousObject: new { greeting = "Hello", name = "Tobi" },
                namingConvention: new RubyNamingConvention());

            var csNamingConvention = new CSharpNamingConvention();
            await Helper.AssertTemplateResultAsync(
                expected: "Hello Tobi",
                template: template,
                anonymousObject: new { Greeting = "Hello", Name = "Tobi" },
                namingConvention: csNamingConvention);
            await Helper.AssertTemplateResultAsync(
                expected: " ",
                template: template,
                anonymousObject: new { greeting = "Hello", name = "Tobi" },
                namingConvention: csNamingConvention);
        }

        [Test]
        public void TestTagsStudlyCapsAreNotAllowed()
        {
            lock (Template.NamingConvention)
            {
                var currentNamingConvention = Template.NamingConvention;
                Template.NamingConvention = new RubyNamingConvention();

                try
                {
                    Assert.Throws<SyntaxException>(() => Template.Parse("{% IF user = 'tobi' %}Hello Tobi{% EndIf %}"));
                }
                finally
                {
                    Template.NamingConvention = currentNamingConvention;
                }
            }
        }

        [Test]
        public async Task TestFiltersStudlyCapsAreNotAllowed()
        {
            await Helper.AssertTemplateResultAsync(
                expected:"HI TOBI",
                template: "{{ 'hi tobi' | upcase }}",
                namingConvention: new RubyNamingConvention());

            await Helper.AssertTemplateResultAsync(
                expected: "HI TOBI",
                template: "{{ 'hi tobi' | Upcase }}",
                namingConvention: new CSharpNamingConvention());
        }

        [Test]
        public async Task TestAssignsStudlyCaps()
        {
            var rubyNamingConvention = new RubyNamingConvention();

            await Helper.AssertTemplateResultAsync(
                expected: ".foo.",
                template: "{% assign FoO = values %}.{{ fOo[0] }}.",
                anonymousObject: new { values = new[] { "foo", "bar", "baz" } },
                namingConvention: rubyNamingConvention);
            await Helper.AssertTemplateResultAsync(
                expected: ".bar.",
                template: "{% assign fOo = values %}.{{ fOO[1] }}.",
                anonymousObject: new { values = new[] { "foo", "bar", "baz" } },
                namingConvention: rubyNamingConvention);

            var csNamingConvention = new CSharpNamingConvention();

            await Helper.AssertTemplateResultAsync(
                expected: ".foo.",
                template: "{% assign Foo = values %}.{{ Foo[0] }}.",
                anonymousObject: new { values = new[] { "foo", "bar", "baz" } },
                namingConvention: csNamingConvention);
            await Helper.AssertTemplateResultAsync(
                expected: ".bar.",
                template: "{% assign fOo = values %}.{{ fOo[1] }}.",
                anonymousObject: new { values = new[] { "foo", "bar", "baz" } },
                namingConvention: csNamingConvention);
        }
    }
}
