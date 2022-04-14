using System;
using System.Collections.Generic;
using DotLiquid.Exceptions;
using NUnit.Framework;
using DotLiquid.Tags;
using System.Threading.Tasks;

namespace DotLiquid.Tests.Tags
{
    [TestFixture]
    public class LiteralTests
    {
        [Test]
        public async Task TestEmptyLiteral()
        {
            Assert.AreEqual(string.Empty, await Template.Parse("{% literal %}{% endliteral %}").RenderAsync());

            // Next test is specific to legacy parser and was removed from Ruby Liquid. Test that it is ignored is in TestShortHandSyntaxIsIgnored
            Assert.AreEqual(string.Empty, await Template.Parse("{{{}}}", SyntaxCompatibility.DotLiquid20).RenderAsync());
        }

        [Test]
        public async Task TestSimpleLiteralValue()
        {
            Assert.AreEqual("howdy", await Template.Parse("{% literal %}howdy{% endliteral %}").RenderAsync());
        }

        [Test]
        public async Task TestLiteralsIgnoreLiquidMarkup()
        {
            Assert.AreEqual(
                expected: "{% if 'gnomeslab' contains 'liquid' %}yes{ % endif %}",
                actual: await Template.Parse("{% literal %}{% if 'gnomeslab' contains 'liquid' %}yes{ % endif %}{% endliteral %}").RenderAsync());
        }

        [Test]
        public async Task TestShorthandSyntax()
        {
            Assert.AreEqual(
                expected: "{% if 'gnomeslab' contains 'liquid' %}yes{ % endif %}",
                actual: await Template.Parse("{{{{% if 'gnomeslab' contains 'liquid' %}yes{ % endif %}}}}", SyntaxCompatibility.DotLiquid20).RenderAsync());
        }

        [Test]
        public async Task TestLiteralsDontRemoveComments()
        {
            Assert.AreEqual("{# comment #}", await Template.Parse("{{{ {# comment #} }}}", SyntaxCompatibility.DotLiquid20).RenderAsync());
        }

        [Test]
        public void TestFromShorthand()
        {
            Assert.AreEqual("{% literal %}gnomeslab{% endliteral %}", Literal.FromShortHand("{{{gnomeslab}}}"));
            Assert.AreEqual(null, Literal.FromShortHand(null));
        }

        [Test]
        public void TestFromShorthandIgnoresImproperSyntax()
        {
            Assert.AreEqual("{% if 'hi' == 'hi' %}hi{% endif %}", Literal.FromShortHand("{% if 'hi' == 'hi' %}hi{% endif %}"));
        }
    }
}
