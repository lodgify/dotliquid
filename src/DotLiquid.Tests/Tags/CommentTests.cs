using DotLiquid.Tags;
using NUnit.Framework;
using System.Threading.Tasks;

namespace DotLiquid.Tests.Tags
{
    [TestFixture]
    public class CommentTests
    {
        [Test]
        public async Task TestEmptyComment()
        {
            Assert.AreEqual(string.Empty, await Template.Parse("{% comment %}{% endcomment %}").RenderAsync());

            // Next test is specific to legacy parser and was removed from Ruby Liquid. Test that it is ignored is in TestShortHandSyntaxIsIgnored
            Assert.AreEqual(string.Empty, await Template.Parse("{##}", SyntaxCompatibility.DotLiquid20).RenderAsync());
        }

        [Test]
        public async Task TestSimpleCommentValue()
        {
            Assert.AreEqual("", await Template.Parse("{% comment %}howdy{% endcomment %}").RenderAsync());
        }

        [Test]
        public async Task TestCommentsIgnoreLiquidMarkup()
        {
            Assert.AreEqual(
                expected: "",
                actual: await Template.Parse("{% comment %}{% if 'gnomeslab' contains 'liquid' %}yes{% else %}no{ % endif %}{% endcomment %}").RenderAsync());
        }

        [Test]
        public async Task TestCommentShorthand()
        {
            Assert.AreEqual("{% comment %}gnomeslab{% endcomment %}", Comment.FromShortHand("{# gnomeslab #}"));
            Assert.AreEqual(null, Comment.FromShortHand(null));

            Assert.AreEqual(
                expected: "",
                actual: await Template.Parse("{#{% if 'gnomeslab' contains 'liquid' %}yes{ % endif %}#}", SyntaxCompatibility.DotLiquid20).RenderAsync());
        }
    }
}
