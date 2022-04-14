using System.Collections;
using System.Globalization;
using System.Threading.Tasks;
using DotLiquid.Exceptions;
using NUnit.Framework;

namespace DotLiquid.Tests
{
    [TestFixture]
    public class FilterTests
    {
        #region Classes used in tests

        private static class MoneyFilter
        {
            public static string Money(object input)
            {
                return string.Format(" {0:d}$ ", input);
            }

            public static string MoneyWithUnderscore(object input)
            {
                return string.Format(" {0:d}$ ", input);
            }
        }

        private static class CanadianMoneyFilter
        {
            public static string Money(object input)
            {
                return string.Format(" {0:d}$ CAD ", input);
            }
        }

        private static class FiltersWithArgumentsInt
        {
            public static string Adjust(int input, int offset = 10)
            {
                return string.Format("[{0:d}]", input + offset);
            }

            public static string AddSub(int input, int plus, int minus = 20)
            {
                return string.Format("[{0:d}]", input + plus - minus);
            }
        }

        private static class FiltersWithArgumentsLong
        {
            public static string Adjust(long input, long offset = 10)
            {
                return string.Format("[{0:d}]", input + offset);
            }

            public static string AddSub(long input, long plus, long minus = 20)
            {
                return string.Format("[{0:d}]", input + plus - minus);
            }
        }

        private static class FiltersWithMultipleMethodSignatures
        {
            public static string Concatenate(string one, string two)
            {
                return string.Concat(one, two);
            }

            public static string Concatenate(string one, string two, string three)
            {
                return string.Concat(one, two, three);
            }
        }

        private static class FiltersWithMultipleMethodSignaturesAndContextParam
        {
            public static string ConcatWithContext(Context context, string one, string two)
            {
                return string.Concat(one, two);
            }

            public static string ConcatWithContext(Context context, string one, string two, string three)
            {
                return string.Concat(one, two, three);
            }
        }

        private static class FiltersWithMultipleMethodSignaturesDifferentClassesOne
        {
            public static string Concatenate(string one, string two)
            {
                return string.Concat(one, two);
            }
        }


        private static class FilterWithSameMethodSignatureDifferentClassOne
        {
            public static string Concatenate(string one, string two)
            {
                return string.Concat(one, two, "Class One");
            }
        }

        private static class FilterWithSameMethodSignatureDifferentClassTwo
        {
            public static string Concatenate(string one, string two)
            {
                return string.Concat(one, two, "Class Two");
            }
        }

        private static class FiltersWithMultipleMethodSignaturesDifferentClassesTwo
        {
            public static string Concatenate(Context context, string one, string two, string three)
            {
                return string.Concat(one, two, three);
            }
        }

        private static class FiltersWithMultipleMethodSignaturesDifferentClassesWithContextParamTwo
        {
            public static string ConcatWithContext(Context context, string one, string two, string three)
            {
                return string.Concat(one, two, three);
            }
        }

        private static class FiltersWithMultipleMethodSignaturesDifferentClassesWithContextParamOne
        {
            public static string ConcatWithContext(Context context, string one, string two)
            {
                return string.Concat(one, two);
            }
        }

        private static class ContextFilters
        {
            public static string BankStatement(Context context, object input)
            {
                return string.Format(" " + context.GetAsync("name").GetAwaiter().GetResult() + " has {0:d}$ ", input);
            }
        }

        #endregion

        private Context _context;

        [OneTimeSetUp]
        public void SetUp()
        {
            _context = new Context(CultureInfo.InvariantCulture);
        }

        /*[Test]
        public async Task TestNonExistentFilter()
        {
            _context.Set("var", 1000);
            Assert.Throws<FilterNotFoundException>(() => await new Variable("var | syzzy").RenderAsync(_context));
        }*/

        [Test]
        public async Task TestLocalFilter()
        {
            _context.Set("var", 1000);
            _context.AddFilters(typeof(MoneyFilter));
            Assert.AreEqual(" 1000$ ", await new Variable("var | money").RenderAsync(_context));
        }

        [Test]
        public async Task TestUnderscoreInFilterName()
        {
            _context.Set("var", 1000);
            _context.AddFilters(typeof(MoneyFilter));
            Assert.AreEqual(" 1000$ ", await new Variable("var | money_with_underscore").RenderAsync(_context));
        }

        [Test]
        public async Task TestFilterWithNumericArgument()
        {
            _context.Set("var", 1000L);
            _context.AddFilters(typeof(FiltersWithArgumentsInt));
            Assert.AreEqual("[1005]", await new Variable("var | adjust: 5").RenderAsync(_context));
        }

        [Test]
        public async Task TestFilterWithNegativeArgument()
        {
            _context.Set("var", 1000L);
            _context.AddFilters(typeof(FiltersWithArgumentsInt));
            Assert.AreEqual("[995]", await new Variable("var | adjust: -5").RenderAsync(_context));
        }

        [Test]
        public async Task TestFilterWithDefaultArgument()
        {
            _context.Set("var", 1000);
            _context.AddFilters(typeof(FiltersWithArgumentsInt));
            Assert.AreEqual("[1010]", await new Variable("var | adjust").RenderAsync(_context));
        }

        [Test]
        public async Task TestFilterWithTwoArguments()
        {
            _context.Set("var", 1000L);
            _context.AddFilters(typeof(FiltersWithArgumentsInt));
            Assert.AreEqual("[1150]", await new Variable("var | add_sub: 200, 50").RenderAsync(_context));
        }

        [Test]
        public async Task TestFilterWithNumericArgumentLong()
        {
            _context.Set("var", 1000);
            _context.AddFilters(typeof(FiltersWithArgumentsLong));
            Assert.AreEqual("[1005]", await new Variable("var | adjust: 5").RenderAsync(_context));
        }

        [Test]
        public async Task TestFilterWithNegativeArgumentLong()
        {
            _context.Set("var", 1000);
            _context.AddFilters(typeof(FiltersWithArgumentsLong));
            Assert.AreEqual("[995]", await new Variable("var | adjust: -5").RenderAsync(_context));
        }

        [Test]
        public async Task TestFilterWithDefaultArgumentLong()
        {
            _context.Set("var", 1000);
            _context.AddFilters(typeof(FiltersWithArgumentsLong));
            Assert.AreEqual("[1010]", await new Variable("var | adjust").RenderAsync(_context));
        }

        [Test]
        public async Task TestFilterWithTwoArgumentsLong()
        {
            _context.Set("var", 1000);
            _context.AddFilters(typeof(FiltersWithArgumentsLong));
            Assert.AreEqual("[1150]", await new Variable("var | add_sub: 200, 50").RenderAsync(_context));
        }

        [Test]
        public async Task TestFilterWithMultipleMethodSignatures()
        {
            Template.RegisterFilter(typeof(FiltersWithMultipleMethodSignatures));

            Assert.AreEqual("AB", await Template.Parse("{{'A' | concatenate : 'B'}}").RenderAsync());
            Assert.AreEqual("ABC", await Template.Parse("{{'A' | concatenate : 'B', 'C'}}").RenderAsync());
        }

        [Test]
        public async Task TestFilterInContextWithMultipleMethodSignatures()
        {
            _context.AddFilters(typeof(FiltersWithMultipleMethodSignatures));

            Assert.AreEqual("AB", await new Variable("'A' | concatenate : 'B'").RenderAsync(_context));
            Assert.AreEqual("ABC", await new Variable("'A' | concatenate : 'B', 'C'").RenderAsync(_context));
        }

        [Test]
        public async Task TestFilterWithMultipleMethodSignaturesAndContextParam()
        {
            Template.RegisterFilter(typeof(FiltersWithMultipleMethodSignaturesAndContextParam));

            Assert.AreEqual("AB", await Template.Parse("{{'A' | concat_with_context : 'B'}}").RenderAsync());
            Assert.AreEqual("ABC", await Template.Parse("{{'A' | concat_with_context : 'B', 'C'}}").RenderAsync());
        }

        [Test]
        public async Task TestFilterInContextWithMultipleMethodSignaturesAndContextParam()
        {
            _context.AddFilters(typeof(FiltersWithMultipleMethodSignaturesAndContextParam));

            Assert.AreEqual("AB", await new Variable("'A' | concat_with_context : 'B'").RenderAsync(_context));
            Assert.AreEqual("ABC", await new Variable("'A' | concat_with_context : 'B', 'C'").RenderAsync(_context));
        }

        [Test]
        public async Task TestFilterWithMultipleMethodSignaturesDifferentClasses()
        {
            Template.RegisterFilter(typeof(FiltersWithMultipleMethodSignaturesDifferentClassesOne));
            Template.RegisterFilter(typeof(FiltersWithMultipleMethodSignaturesDifferentClassesTwo));

            Assert.AreEqual("AB", await Template.Parse("{{'A' | concatenate : 'B'}}").RenderAsync());
            Assert.AreEqual("ABC", await Template.Parse("{{'A' | concatenate : 'B', 'C'}}").RenderAsync());
        }

        [Test]
        public async Task TestFilterInContextWithMultipleMethodSignaturesDifferentClasses()
        {
            _context.AddFilters(typeof(FiltersWithMultipleMethodSignaturesDifferentClassesOne));
            _context.AddFilters(typeof(FiltersWithMultipleMethodSignaturesDifferentClassesTwo));

            Assert.AreEqual("AB", await new Variable("'A' | concatenate : 'B'").RenderAsync(_context));
            Assert.AreEqual("ABC", await new Variable("'A' | concatenate : 'B', 'C'").RenderAsync(_context));
        }

        [Test]
        public async Task TestFilterAsLocalFilterWithMultipleMethodSignaturesDifferentClasses()
        {
            await Helper.AssertTemplateResultAsync(
                expected: "AB // ABC",
                template: "{{'A' | concatenate : 'B'}} // {{'A' | concatenate : 'B', 'C'}}",
                localVariables: null,
                localFilters: new[] { typeof(FiltersWithMultipleMethodSignaturesDifferentClassesOne), typeof(FiltersWithMultipleMethodSignaturesDifferentClassesTwo) });
        }

        [Test]
        public async Task TestFilterWithMultipleMethodSignaturesAndContextParamInDifferentClasses()
        {
            Template.RegisterFilter(typeof(FiltersWithMultipleMethodSignaturesDifferentClassesWithContextParamTwo));
            Template.RegisterFilter(typeof(FiltersWithMultipleMethodSignaturesDifferentClassesWithContextParamOne));
            Assert.AreEqual("AB", await Template.Parse("{{'A' | concat_with_context : 'B'}}").RenderAsync());
            Assert.AreEqual("ABC", await Template.Parse("{{'A' | concat_with_context : 'B', 'C'}}").RenderAsync());
        }

        [Test]
        public async Task TestFilterAsLocalFilterWithMultipleMethodSignaturesAndContextDifferentClasses()
        {
            await Helper.AssertTemplateResultAsync(
                expected: "AB // ABC",
                template: "{{'A' | concat_with_context : 'B'}} // {{'A' | concat_with_context : 'B', 'C'}}",
                localVariables: null,
                localFilters: new[] { typeof(FiltersWithMultipleMethodSignaturesDifferentClassesWithContextParamOne), typeof(FiltersWithMultipleMethodSignaturesDifferentClassesWithContextParamTwo) });
        }

        [Test]
        public async Task TestFilterInContextWithMultipleMethodSignaturesAndContextParamInDifferentClasses()
        {
            _context.AddFilters(typeof(FiltersWithMultipleMethodSignaturesDifferentClassesWithContextParamOne));
            _context.AddFilters(typeof(FiltersWithMultipleMethodSignaturesDifferentClassesWithContextParamTwo));

            Assert.AreEqual("AB", await new Variable("'A' | concat_with_context : 'B'").RenderAsync(_context));
            Assert.AreEqual("ABC", await new Variable("'A' | concat_with_context : 'B', 'C'").RenderAsync(_context));
        }

        [Test]
        // When two methods with the same name and method signature are registered, the method that is added last is preferred.
        // This allows overriding any existing methods, including methods defined in the DotLiqid library.
        // This is useful in cases where a defined method may need to have a different behavior in certain contexts.
        public async Task TestFilterOverridesMethodWithSameMethodSignaturesDifferentClasses()
        {
            Template.RegisterFilter(typeof(FilterWithSameMethodSignatureDifferentClassTwo));
            Template.RegisterFilter(typeof(FilterWithSameMethodSignatureDifferentClassOne));

            Assert.AreEqual("ABClass One", await Template.Parse("{{'A' | concatenate : 'B'}}").RenderAsync());
            Assert.AreNotEqual("ABClass Two", await Template.Parse("{{'A' | concatenate : 'B'}}").RenderAsync());
        }

        [Test]
        public async Task TestFilterInContextOverridesMethodWithSameMethodSignaturesDifferentClasses()
        {
            _context.AddFilters(typeof(FilterWithSameMethodSignatureDifferentClassOne));
            _context.AddFilters(typeof(FilterWithSameMethodSignatureDifferentClassTwo));

            Assert.AreEqual("ABClass Two", await new Variable("'A' | concatenate : 'B'").RenderAsync(_context));
            Assert.AreNotEqual("ABClass One", await new Variable("'A' | concatenate : 'B'").RenderAsync(_context));
        }

        [Test]
        public async Task TestFilterAsLocalOverridesMethodWithSameMethodSignaturesDifferentClasses()
        {
            await Helper.AssertTemplateResultAsync(
                expected: "ABClass One",
                template: "{{'A' | concatenate : 'B'}}",
                localVariables: null,
                localFilters: new[] { typeof(FilterWithSameMethodSignatureDifferentClassTwo), typeof(FilterWithSameMethodSignatureDifferentClassOne) });
        }

        /*/// <summary>
        /// ATM the trailing value is silently ignored. Should raise an exception?
        /// </summary>
        [Test]
        public void TestFilterWithTwoArgumentsNoComma()
        {
            _context.Set("var", 1000);
            _context.AddFilters(typeof(FiltersWithArguments));
            Assert.AreEqual("[1150]", string.Join(string.Empty, await new Variable("var | add_sub: 200 50").RenderAsync(_context));
        }*/

        [Test]
        public async Task TestSecondFilterOverwritesFirst()
        {
            _context.Set("var", 1000);
            _context.AddFilters(typeof(MoneyFilter));
            _context.AddFilters(typeof(CanadianMoneyFilter));
            Assert.AreEqual(" 1000$ CAD ", await new Variable("var | money").RenderAsync(_context));
        }

        [Test]
        public async Task TestSize()
        {
            _context.Set("var", "abcd");
            _context.AddFilters(typeof(MoneyFilter));
            Assert.AreEqual(4, await new Variable("var | size").RenderAsync(_context));
        }

        [Test]
        public async Task TestJoin()
        {
            _context.Set("var", new[] { 1, 2, 3, 4 });
            Assert.AreEqual("1 2 3 4", await new Variable("var | join").RenderAsync(_context));
        }

        [Test]
        public async Task TestSort()
        {
            _context.Set("value", 3);
            _context.Set("numbers", new[] { 2, 1, 4, 3 });
            _context.Set("words", new[] { "expected", "as", "alphabetic" });
            _context.Set("arrays", new[] { new[] { "flattened" }, new[] { "are" } });

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, await new Variable("numbers | sort").RenderAsync(_context) as IEnumerable);
            CollectionAssert.AreEqual(new[] { "alphabetic", "as", "expected" }, await new Variable("words | sort").RenderAsync(_context) as IEnumerable);
            CollectionAssert.AreEqual(new[] { 3 }, await new Variable("value | sort").RenderAsync(_context) as IEnumerable);
            CollectionAssert.AreEqual(new[] { "are", "flattened" }, await new Variable("arrays | sort").RenderAsync(_context) as IEnumerable);
        }

        [Test]
        public async Task TestSplit()
        {
            _context.Set("var", "a~b");
            Assert.AreEqual(new[] { "a", "b" }, await new Variable("var | split:'~'").RenderAsync(_context));
        }

        [Test]
        public async Task TestStripHtml()
        {
            _context.Set("var", "<b>bla blub</a>");
            Assert.AreEqual("bla blub", await new Variable("var | strip_html").RenderAsync(_context));
        }

        [Test]
        public async Task Capitalize()
        {
            _context.Set("var", "blub");
            Assert.AreEqual("Blub", await new Variable("var | capitalize").RenderAsync(_context));
        }

        [Test]
        public async Task Slice()
        {
            _context.Set("var", "blub");
            Assert.AreEqual("b", await new Variable("var | slice: 0, 1").RenderAsync(_context));
            Assert.AreEqual("bl", await new Variable("var | slice: 0, 2").RenderAsync(_context));
            Assert.AreEqual("l", await new Variable("var | slice: 1").RenderAsync(_context));
            Assert.AreEqual("", await new Variable("var | slice: 4, 1").RenderAsync(_context));
            Assert.AreEqual("ub", await new Variable("var | slice: -2, 2").RenderAsync(_context));
            Assert.AreEqual(null, await new Variable("var | slice: 5, 1").RenderAsync(_context));
        }

        [Test]
        public async Task TestLocalGlobal()
        {
            Template.RegisterFilter(typeof(MoneyFilter));

            Assert.AreEqual(" 1000$ ", await Template.Parse("{{1000 | money}}").RenderAsync());
            Assert.AreEqual(" 1000$ CAD ", await Template.Parse("{{1000 | money}}").RenderAsync(new RenderParameters(CultureInfo.InvariantCulture) { Filters = new[] { typeof(CanadianMoneyFilter) } }));
            Assert.AreEqual(" 1000$ CAD ", await Template.Parse("{{1000 | money}}").RenderAsync(new RenderParameters(CultureInfo.InvariantCulture) { Filters = new[] { typeof(CanadianMoneyFilter) } }));
        }

        [Test]
        public async Task TestContextFilter()
        {
            _context.Set("var", 1000);
            _context.Set("name", "King Kong");
            _context.AddFilters(typeof(ContextFilters));
            Assert.AreEqual(" King Kong has 1000$ ", await new Variable("var | bank_statement").RenderAsync(_context));
        }
    }
}
