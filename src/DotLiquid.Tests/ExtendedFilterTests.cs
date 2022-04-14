using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotLiquid.Tests
{
    [TestFixture]
    public class ExtendedFilterTests
    {
        private Context _context;

        [OneTimeSetUp]
        public void SetUp()
        {
            _context = new Context(CultureInfo.InvariantCulture);
            Template.RegisterFilter(typeof(ExtendedFilters));
        }

        [Test]
        public async Task TestTitleize()
        {
            var context = _context;
            Assert.AreEqual(null, ExtendedFilters.Titleize(context: context, input: null));
            Assert.AreEqual("", ExtendedFilters.Titleize(context: context, input: ""));
            Assert.AreEqual(" ", ExtendedFilters.Titleize(context: context, input: " "));
            Assert.AreEqual("That Is One Sentence.", ExtendedFilters.Titleize(context: context, input: "That is one sentence."));

            await Helper.AssertTemplateResultAsync(
                expected: "Title",
                template: "{{ 'title' | titleize }}");
        }

        [Test]
        public async Task TestUpcaseFirst()
        {
            var context = _context;
            Assert.AreEqual(null, ExtendedFilters.UpcaseFirst(context: context, input: null));
            Assert.AreEqual("", ExtendedFilters.UpcaseFirst(context: context, input: ""));
            Assert.AreEqual(" ", ExtendedFilters.UpcaseFirst(context: context, input: " "));
            Assert.AreEqual(" My boss is Mr. Doe.", ExtendedFilters.UpcaseFirst(context: context, input: " my boss is Mr. Doe."));

            await Helper.AssertTemplateResultAsync(
                expected: "My great title",
                template: "{{ 'my great title' | upcase_first }}");
        }
    }
}
