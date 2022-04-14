using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DotLiquid.Exceptions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace DotLiquid.Tests
{
    [TestFixture]
    public class ContextTests
    {
        #region Classes used in tests

        private static class TestFilters
        {
            public static string Hi(string output)
            {
                return output + " hi!";
            }
        }

        private static class TestContextFilters
        {
            public static async Task<string> HiAsync(Context context, string output)
            {
                return output + " hi from " + (await context.GetAsync("name")) + "!";
            }
        }

        private static class GlobalFilters
        {
            public static string Notice(string output)
            {
                return "Global " + output;
            }
        }

        private static class LocalFilters
        {
            public static string Notice(string output)
            {
                return "Local " + output;
            }
        }

        private class HundredCents : ILiquidizable
        {
            public object ToLiquid()
            {
                return 100;
            }
        }

        private class CentsDrop : Drop
        {
            public object Amount
            {
                get { return new HundredCents(); }
            }

            public bool NonZero
            {
                get { return true; }
            }
        }

        private class ContextSensitiveDrop : Drop
        {
            public Task<object> TestAsync()
            {
                return Context.GetAsync("test");
            }
        }

        private class Category : Drop
        {
            public string Name { get; set; }

            public Category(string name)
            {
                Name = name;
            }

            public override object ToLiquid()
            {
                return new CategoryDrop(this);
            }
        }

        private class CategoryDrop : IContextAware
        {
            public Category Category { get; set; }
            public Context Context { get; set; }

            public CategoryDrop(Category category)
            {
                Category = category;
            }
        }

        private class CounterDrop : Drop
        {
            private int _count;

            public int Count()
            {
                return ++_count;
            }
        }

        private class ArrayLike : ILiquidizable
        {
            private Dictionary<int, int> _counts = new Dictionary<int, int>();

            public object Fetch(int index)
            {
                return null;
            }

            public object this[int index]
            {
                get
                {
                    _counts[index] += 1;
                    return _counts[index];
                }
            }

            public object ToLiquid()
            {
                return this;
            }
        }

        private class IndexableLiquidizable : IIndexable, ILiquidizable
        {
            private const string theKey = "thekey";
            
            public async Task<object> GetAsync(object key)
            {
                var result = key as string == theKey ? new LiquidizableList() : null;
                return await Task.FromResult(result);
            }

            public bool ContainsKey(object key)
            {
                return key as string == theKey;
            }

            public object ToLiquid()
            {
                return this;
            }
        }

        private class LiquidizableList : ILiquidizable
        {
            public object ToLiquid()
            {
                return new List<string>(new[] { "text1", "text2" });
            }
        }

        private class ExpandoModel
        {
            public int IntProperty { get; set; }
            public string StringProperty { get; set; }
            public ExpandoObject Properties { get; set; }
        }

        #endregion

        private Context _context;
        private Context _contextV22;

        [OneTimeSetUp]
        public void SetUp()
        {
            _context = new Context(CultureInfo.InvariantCulture);
            _contextV22 = new Context(CultureInfo.InvariantCulture)
            {
                SyntaxCompatibilityLevel = SyntaxCompatibility.DotLiquid22
            };
        }

        [Test]
        public async Task TestVariables()
        {
            _context.Set("string", "string");
            Assert.AreEqual("string", await _context.GetAsync("string"));

            _context.Set("EscapedCharacter", "EscapedCharacter\"");
            Assert.AreEqual("EscapedCharacter\"", await _context.GetAsync("EscapedCharacter"));

            _context.Set("num", 5);
            Assert.AreEqual(5, await _context.GetAsync("num"));

            _context.Set("decimal", 5m);
            Assert.AreEqual(5m, await _context.GetAsync("decimal"));

            _context.Set("float", 5.0f);
            Assert.AreEqual(5.0f, await _context.GetAsync("float"));

            _context.Set("double", 5.0);
            Assert.AreEqual(5.0, await _context.GetAsync("double"));

            _context.Set("time", TimeSpan.FromDays(1));
            Assert.AreEqual(TimeSpan.FromDays(1), await _context.GetAsync("time"));

            _context.Set("date", DateTime.Today);
            Assert.AreEqual(DateTime.Today, await _context.GetAsync("date"));

            DateTime now = DateTime.Now;
            _context.Set("datetime", now);
            Assert.AreEqual(now, await _context.GetAsync("datetime"));

            DateTimeOffset offset = new DateTimeOffset(2013, 9, 10, 0, 10, 32, new TimeSpan(1, 0, 0));
            _context.Set("datetimeoffset", offset);
            Assert.AreEqual(offset, await _context.GetAsync("datetimeoffset"));

            Guid guid = Guid.NewGuid();
            _context.Set("guid", guid);
            Assert.AreEqual(guid, await _context.GetAsync("guid"));

            _context.Set("bool", true);
            Assert.AreEqual(true, await _context.GetAsync("bool"));

            _context.Set("bool", false);
            Assert.AreEqual(false, await _context.GetAsync("bool"));

            _context.Set("nil", null);
            Assert.AreEqual(null, await _context.GetAsync("nil"));
            Assert.AreEqual(null, await _context.GetAsync("nil"));
        }

        private enum TestEnum { Yes, No }

        [Test]
        public async Task TestGetVariable_Enum()
        {
            _context.Set("yes", TestEnum.Yes);
            _context.Set("no", TestEnum.No);
            _context.Set("not_enum", TestEnum.Yes.ToString());

            Assert.AreEqual(TestEnum.Yes, await _context.GetAsync("yes"));
            Assert.AreEqual(TestEnum.No, await _context.GetAsync("no"));
            Assert.AreNotEqual(TestEnum.Yes, await _context.GetAsync("not_enum"));
        }

        [Test]
        public async Task TestVariablesNotExisting()
        {
            Assert.AreEqual(null, await _context.GetAsync("does_not_exist"));
        }

        [Test]
        public async Task TestVariableNotFoundErrors()
        {
            Template template = Template.Parse("{{ does_not_exist }}");
            string rendered = await template.RenderAsync();

            Assert.AreEqual("", rendered);
            Assert.AreEqual(1, template.Errors.Count);
            Assert.AreEqual(string.Format(Liquid.ResourceManager.GetString("VariableNotFoundException"), "does_not_exist"), template.Errors[0].Message);
        }

        [Test]
        public async Task TestVariableNotFoundFromAnonymousObject()
        {
            Template template = Template.Parse("{{ first.test }}{{ second.test }}");
            string rendered = await template.RenderAsync(Hash.FromAnonymousObject(new { second = new { foo = "hi!" } }));

            Assert.AreEqual("", rendered);
            Assert.AreEqual(2, template.Errors.Count);
            Assert.AreEqual(string.Format(Liquid.ResourceManager.GetString("VariableNotFoundException"), "first.test"), template.Errors[0].Message);
            Assert.AreEqual(string.Format(Liquid.ResourceManager.GetString("VariableNotFoundException"), "second.test"), template.Errors[1].Message);
        }

        [Test]
        public void TestVariableNotFoundException()
        {
            Assert.DoesNotThrowAsync(() => Template.Parse("{{ does_not_exist }}").RenderAsync(new RenderParameters(CultureInfo.InvariantCulture)
            {
                RethrowErrors = true
            }));
        }

        [Test]
        public async Task TestVariableNotFoundExceptionIgnoredForIfStatement()
        {
            Template template = Template.Parse("{% if does_not_exist %}abc{% endif %}");
            string rendered = await template.RenderAsync();

            Assert.AreEqual("", rendered);
            Assert.AreEqual(0, template.Errors.Count);
        }

        [Test]
        public async Task TestVariableNotFoundExceptionIgnoredForUnlessStatement()
        {
            Template template = Template.Parse("{% unless does_not_exist %}abc{% endunless %}");
            string rendered = await template.RenderAsync();

            Assert.AreEqual("abc", rendered);
            Assert.AreEqual(0, template.Errors.Count);
        }

        [Test]
        public void TestScoping()
        {
            Assert.DoesNotThrow(() =>
            {
                _context.Push(null);
                _context.Pop();
            });

            Assert.Throws<ContextException>(() => _context.Pop());

            Assert.Throws<ContextException>(() =>
            {
                _context.Push(null);
                _context.Pop();
                _context.Pop();
            });
        }

        [Test]
        public async Task TestLengthQuery()
        {
            _context.Set("numbers", new[] { 1, 2, 3, 4 });
            Assert.AreEqual(4, await _context.GetAsync("numbers.size"));

            _context.Set("numbers", new Dictionary<int, int>
            {
                { 1, 1 },
                { 2, 2 },
                { 3, 3 },
                { 4, 4 }
            });
            Assert.AreEqual(4, await _context.GetAsync("numbers.size"));

            _context.Set("numbers", new Dictionary<object, int>
            {
                { 1, 1 },
                { 2, 2 },
                { 3, 3 },
                { 4, 4 },
                { "size", 1000 }
            });
            Assert.AreEqual(1000, await _context.GetAsync("numbers.size"));
        }

        [Test]
        public async Task TestHyphenatedVariable()
        {
            _context.Set("oh-my", "godz");
            Assert.AreEqual("godz", await _context.GetAsync("oh-my"));
        }

        [Test]
        public async Task TestAddFilter()
        {
            Context context = new Context(CultureInfo.InvariantCulture);
            context.AddFilters(new[] { typeof(TestFilters) });
            Assert.AreEqual("hi? hi!", await context.InvokeAsync("hi", new List<object> { "hi?" }));
            context.SyntaxCompatibilityLevel = SyntaxCompatibility.DotLiquid22;
            Assert.AreEqual("hi? hi!", await context.InvokeAsync("hi", new List<object> { "hi?" }));

            context = new Context(CultureInfo.InvariantCulture);
            Assert.AreEqual("hi?", await context.InvokeAsync("hi", new List<object> { "hi?" }));
            context.SyntaxCompatibilityLevel = SyntaxCompatibility.DotLiquid22;
            Assert.ThrowsAsync<FilterNotFoundException>(async () => await context.InvokeAsync("hi", new List<object> { "hi?" }));
        }

        [Test]
        public async Task TestAddContextFilter()
        {
            // This test differs from TestAddFilter only in that the Hi method within this class has a Context parameter in addition to the input string
            Context context = new Context(CultureInfo.InvariantCulture) { SyntaxCompatibilityLevel = SyntaxCompatibility.DotLiquid20 };
            context.Set("name", "King Kong");

            context.AddFilters(new[] { typeof(TestContextFilters) });
            Assert.AreEqual("hi? hi from King Kong!", await context.InvokeAsync("hi", new List<object> { "hi?" }));
            context.SyntaxCompatibilityLevel = SyntaxCompatibility.DotLiquid22;
            Assert.AreEqual("hi? hi from King Kong!", await context.InvokeAsync("hi", new List<object> { "hi?" }));

            context = new Context(CultureInfo.InvariantCulture) { SyntaxCompatibilityLevel = SyntaxCompatibility.DotLiquid20 };
            Assert.AreEqual("hi?", await context.InvokeAsync("hi", new List<object> { "hi?" }));
            context.SyntaxCompatibilityLevel = SyntaxCompatibility.DotLiquid22;
            Assert.ThrowsAsync<FilterNotFoundException>(async () => await context.InvokeAsync("hi", new List<object> { "hi?" }));
        }

        [Test]
        public async Task TestOverrideGlobalFilter()
        {
            Template.RegisterFilter(typeof(GlobalFilters));
            Assert.AreEqual("Global test", await Template.Parse("{{'test' | notice }}").RenderAsync());
            Assert.AreEqual("Local test", await Template.Parse("{{'test' | notice }}").RenderAsync(new RenderParameters(CultureInfo.InvariantCulture) { Filters = new[] { typeof(LocalFilters) } }));
        }

        [Test]
        public void TestOnlyIntendedFiltersMakeItThere()
        {
            Context context = new Context(CultureInfo.InvariantCulture);
            var methodsBefore = context.Strainer.Methods.Select(mi => mi.Name).ToList();
            context.AddFilters(new[] { typeof(TestFilters) });
            var methodsAfter = context.Strainer.Methods.Select(mi => mi.Name).ToList();
            CollectionAssert.AreEqual(
                methodsBefore.Concat(new[] { "Hi" }).OrderBy(s => s).ToList(),
                methodsAfter.OrderBy(s => s).ToList());
        }

        [Test]
        public async Task TestAddItemInOuterScope()
        {
            _context.Set("test", "test");
            _context.Push(new Hash());
            Assert.AreEqual("test", await _context.GetAsync("test"));
            _context.Pop();
            Assert.AreEqual("test", await _context.GetAsync("test"));
        }

        [Test]
        public async Task TestAddItemInInnerScope()
        {
            _context.Push(new Hash());
            _context.Set("test", "test");
            Assert.AreEqual("test", await _context.GetAsync("test"));
            _context.Pop();
            Assert.AreEqual(null, await _context.GetAsync("test"));
        }

        [Test]
        public async Task TestHierarchicalData()
        {
            _context.Set("hash", new { name = "tobi" });
            Assert.AreEqual("tobi", await _context.GetAsync("hash.name"));
            Assert.AreEqual("tobi", await _context.GetAsync("hash['name']"));
        }

        [Test]
        public async Task TestKeywords()
        {
            Assert.AreEqual(true, await _context.GetAsync("true"));
            Assert.AreEqual(false, await _context.GetAsync("false"));
        }

        [Test]
        public async Task TestDigits()
        {
            Assert.AreEqual(100, await _context.GetAsync("100"));
            Assert.AreEqual(100.00, await _context.GetAsync(string.Format("100{0}00", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator)));
        }

        [Test]
        public async Task TestStrings()
        {
            Assert.AreEqual("hello!", await _context.GetAsync("'hello!'"));
            Assert.AreEqual("hello!", await _context.GetAsync("'hello!'"));
        }

        [Test]
        public async Task TestMerge()
        {
            _context.Merge(new Hash { { "test", "test" } });
            Assert.AreEqual("test", await _context.GetAsync("test"));
            _context.Merge(new Hash { { "test", "newvalue" }, { "foo", "bar" } });
            Assert.AreEqual("newvalue", await _context.GetAsync("test"));
            Assert.AreEqual("bar", await _context.GetAsync("foo"));
        }

        [Test]
        public async Task TestArrayNotation()
        {
            _context.Set("test", new[] { 1, 2, 3, 4, 5 });

            Assert.AreEqual(1, await _context.GetAsync("test[0]"));
            Assert.AreEqual(2, await _context.GetAsync("test[1]"));
            Assert.AreEqual(3, await _context.GetAsync("test[2]"));
            Assert.AreEqual(4, await _context.GetAsync("test[3]"));
            Assert.AreEqual(5, await _context.GetAsync("test[4]"));
        }

        [Test]
        public async Task TestRecursiveArrayNotation()
        {
            _context.Set("test", new { test = new[] { 1, 2, 3, 4, 5 } });

            Assert.AreEqual(1, await _context.GetAsync("test.test[0]"));

            _context.Set("test", new[] { new { test = "worked" } });

            Assert.AreEqual("worked", await _context.GetAsync("test[0].test"));
        }

        [Test]
        public async Task TestHashToArrayTransition()
        {
            _context.Set("colors", new
            {
                Blue = new[] { "003366", "336699", "6699CC", "99CCFF" },
                Green = new[] { "003300", "336633", "669966", "99CC99" },
                Yellow = new[] { "CC9900", "FFCC00", "FFFF99", "FFFFCC" },
                Red = new[] { "660000", "993333", "CC6666", "FF9999" }
            });

            Assert.AreEqual("003366", await _context.GetAsync("colors.Blue[0]"));
            Assert.AreEqual("FF9999", await _context.GetAsync("colors.Red[3]"));
        }

        [Test]
        public async Task TestFirstLastSize()
        {
            _context.Set("test", new[] { 1, 2, 3, 4, 5 });

            Assert.AreEqual(1, await _context.GetAsync("test.first"));
            Assert.AreEqual(5, await _context.GetAsync("test.last"));
            Assert.AreEqual(5, await _context.GetAsync("test.size"));

            _context.Set("test", new { test = new[] { 1, 2, 3, 4, 5 } });

            Assert.AreEqual(1, await _context.GetAsync("test.test.first"));
            Assert.AreEqual(5, await _context.GetAsync("test.test.last"));
            Assert.AreEqual(5, await _context.GetAsync("test.test.size"));

            _context.Set("test", new[] { 1 });

            Assert.AreEqual(1, await _context.GetAsync("test.first"));
            Assert.AreEqual(1, await _context.GetAsync("test.last"));
            Assert.AreEqual(1, await _context.GetAsync("test.size"));
        }

        [Test]
        public async Task TestAccessHashesWithHashNotation()
        {
            _context.Set("products", new { count = 5, tags = new[] { "deepsnow", "freestyle" } });
            _context.Set("product", new { variants = new[] { new { title = "draft151cm" }, new { title = "element151cm" } } });

            Assert.AreEqual(5, await _context.GetAsync("products[\"count\"]"));
            Assert.AreEqual("deepsnow", await _context.GetAsync("products['tags'][0]"));
            Assert.AreEqual("deepsnow", await _context.GetAsync("products['tags'].first"));
            Assert.AreEqual("freestyle", await _context.GetAsync("products['tags'].last"));
            Assert.AreEqual(2, await _context.GetAsync("products['tags'].size"));
            Assert.AreEqual("draft151cm", await _context.GetAsync("product['variants'][0][\"title\"]"));
            Assert.AreEqual("element151cm", await _context.GetAsync("product['variants'][1]['title']"));
            Assert.AreEqual("draft151cm", await _context.GetAsync("product['variants'][0]['title']"));
            Assert.AreEqual("element151cm", await _context.GetAsync("product['variants'].last['title']"));
        }

        [Test]
        public async Task TestAccessVariableWithHashNotation()
        {
            _context.Set("foo", "baz");
            _context.Set("bar", "foo");

            Assert.AreEqual("baz", await _context.GetAsync("[\"foo\"]"));
            Assert.AreEqual("baz", await _context.GetAsync("[bar]"));
        }

        [Test]
        public async Task TestAccessHashesWithHashAccessVariables()
        {
            _context.Set("var", "tags");
            _context.Set("nested", new { var = "tags" });
            _context.Set("products", new { count = 5, tags = new[] { "deepsnow", "freestyle" } });

            Assert.AreEqual("deepsnow", await _context.GetAsync("products[var].first"));
            Assert.AreEqual("freestyle", await _context.GetAsync("products[nested.var].last"));
        }

        [Test]
        public async Task TestHashNotationOnlyForHashAccess()
        {
            _context.Set("array", new[] { 1, 2, 3, 4, 5 });
            _context.Set("hash", new { first = "Hello" });

            Assert.AreEqual(1, await _context.GetAsync("array.first"));
            Assert.AreEqual(null, await _context.GetAsync("array['first']"));
            Assert.AreEqual("Hello", await _context.GetAsync("hash['first']"));
        }

        [Test]
        public async Task TestFirstCanAppearInMiddleOfCallchain()
        {
            _context.Set("product", new { variants = new[] { new { title = "draft151cm" }, new { title = "element151cm" } } });

            Assert.AreEqual("draft151cm", await _context.GetAsync("product.variants[0].title"));
            Assert.AreEqual("element151cm", await _context.GetAsync("product.variants[1].title"));
            Assert.AreEqual("draft151cm", await _context.GetAsync("product.variants.first.title"));
            Assert.AreEqual("element151cm", await _context.GetAsync("product.variants.last.title"));
        }

        [Test]
        public async Task TestCents()
        {
            _context.Merge(Hash.FromAnonymousObject(new { cents = new HundredCents() }));
            Assert.AreEqual(100, await _context.GetAsync("cents"));
        }

        [Test]
        public async Task TestNestedCents()
        {
            _context.Merge(Hash.FromAnonymousObject(new { cents = new { amount = new HundredCents() } }));
            Assert.AreEqual(100, await _context.GetAsync("cents.amount"));

            _context.Merge(Hash.FromAnonymousObject(new { cents = new { cents = new { amount = new HundredCents() } } }));
            Assert.AreEqual(100, await _context.GetAsync("cents.cents.amount"));
        }

        [Test]
        public async Task TestCentsThroughDrop()
        {
            _context.Merge(Hash.FromAnonymousObject(new { cents = new CentsDrop() }));
            Assert.AreEqual(100, await _context.GetAsync("cents.amount"));
        }

        [Test]
        public async Task TestNestedCentsThroughDrop()
        {
            _context.Merge(Hash.FromAnonymousObject(new { vars = new { cents = new CentsDrop() } }));
            Assert.AreEqual(100, await _context.GetAsync("vars.cents.amount"));
        }

        [Test]
        public async Task TestDropMethodsWithQuestionMarks()
        {
            _context.Merge(Hash.FromAnonymousObject(new { cents = new CentsDrop() }));
            Assert.AreEqual(true, await _context.GetAsync("cents.non_zero"));
        }

        [Test]
        public async Task TestContextFromWithinDrop()
        {
            _context.Merge(Hash.FromAnonymousObject(new { test = "123", vars = new ContextSensitiveDrop() }));
            Assert.AreEqual("123", await _context.GetAsync("vars.test"));
        }

        [Test]
        public async Task TestNestedContextFromWithinDrop()
        {
            _context.Merge(Hash.FromAnonymousObject(new { test = "123", vars = new { local = new ContextSensitiveDrop() } }));
            Assert.AreEqual("123", await _context.GetAsync("vars.local.test"));
        }

        [Test]
        public async Task TestRanges()
        {
            _context.Merge(Hash.FromAnonymousObject(new { test = 5 }));
            CollectionAssert.AreEqual(Enumerable.Range(1, 5), await _context.GetAsync("(1..5)") as IEnumerable);
            CollectionAssert.AreEqual(Enumerable.Range(1, 5), await _context.GetAsync("(1..test)") as IEnumerable);
            CollectionAssert.AreEqual(Enumerable.Range(5, 1), await _context.GetAsync("(test..test)") as IEnumerable);
        }

        [Test]
        public async Task TestCentsThroughDropNestedly()
        {
            _context.Merge(Hash.FromAnonymousObject(new { cents = new { cents = new CentsDrop() } }));
            Assert.AreEqual(100, await _context.GetAsync("cents.cents.amount"));

            _context.Merge(Hash.FromAnonymousObject(new { cents = new { cents = new { cents = new CentsDrop() } } }));
            Assert.AreEqual(100, await _context.GetAsync("cents.cents.cents.amount"));
        }

        [Test]
        public async Task TestDropWithVariableCalledOnlyOnce()
        {
            _context.Set("counter", new CounterDrop());

            Assert.AreEqual(1, await _context.GetAsync("counter.count"));
            Assert.AreEqual(2, await _context.GetAsync("counter.count"));
            Assert.AreEqual(3, await _context.GetAsync("counter.count"));
        }

        [Test]
        public async Task TestDropWithKeyOnlyCalledOnce()
        {
            _context.Set("counter", new CounterDrop());

            Assert.AreEqual(1, await _context.GetAsync("counter['count']"));
            Assert.AreEqual(2, await _context.GetAsync("counter['count']"));
            Assert.AreEqual(3, await _context.GetAsync("counter['count']"));
        }

        [Test]
        public async Task TestSimpleVariablesRendering()
        {
            await Helper.AssertTemplateResultAsync(
                expected: "string",
                template: "{{context}}",
                localVariables: Hash.FromAnonymousObject(new { context = "string" }));

            await Helper.AssertTemplateResultAsync(
                expected: "EscapedCharacter\"",
                template: "{{context}}",
                localVariables: Hash.FromAnonymousObject(new { context = "EscapedCharacter\"" }));

            await Helper.AssertTemplateResultAsync(
                expected: "5",
                template: "{{context}}",
                localVariables: Hash.FromAnonymousObject(new { context = 5 }));

            await Helper.AssertTemplateResultAsync(
                expected: "5",
                template: "{{context}}",
                localVariables: Hash.FromAnonymousObject(new { context = 5m }));

            await Helper.AssertTemplateResultAsync(
                expected: "5",
                template: "{{context}}",
                localVariables: Hash.FromAnonymousObject(new { context = 5.0f }));

            await Helper.AssertTemplateResultAsync(
                expected: "5",
                template: "{{context}}",
                localVariables: Hash.FromAnonymousObject(new { context = 5.0 }));

            await Helper.AssertTemplateResultAsync(
                expected: "1.00:00:00",
                template: "{{context}}",
                localVariables: Hash.FromAnonymousObject(new { context = TimeSpan.FromDays(1) }));

            // The expected values are expressed in en-US, so ensure the template runs with that Culture.
            using (CultureHelper.SetCulture("en-US"))
            {
                await Helper.AssertTemplateResultAsync(
                    expected: "1/1/0001 12:00:00 AM",
                    template: "{{context}}",
                    localVariables: Hash.FromAnonymousObject(new { context = DateTime.MinValue }));

                await Helper.AssertTemplateResultAsync(
                    expected: "9/10/2013 12:10:32 AM +01:00",
                    template: "{{context}}",
                    localVariables: Hash.FromAnonymousObject(new { context = new DateTimeOffset(2013, 9, 10, 0, 10, 32, new TimeSpan(1, 0, 0)) }));
            }

            await Helper.AssertTemplateResultAsync(
                expected: "d0f28a51-9393-4658-af0b-8c4b4c5c31ff",
                template: "{{context}}",
                localVariables: Hash.FromAnonymousObject(new { context = new Guid("{D0F28A51-9393-4658-AF0B-8C4B4C5C31FF}") }));

            await Helper.AssertTemplateResultAsync(
                expected: "true",
                template: "{{context}}",
                localVariables: Hash.FromAnonymousObject(new { context = true }));

            await Helper.AssertTemplateResultAsync(
                expected: "false",
                template: "{{context}}",
                localVariables: Hash.FromAnonymousObject(new { context = false }));

            await Helper.AssertTemplateResultAsync(
                expected: "",
                template: "{{context}}",
                localVariables: Hash.FromAnonymousObject(new { context = null as string }));
        }

        [Test]
        public async Task TestListRendering()
        {
            Assert.AreEqual(
                expected: "text1text2",
                actual: await Template
                    .Parse("{{context}}")
                    .RenderAsync(Hash.FromAnonymousObject(new { context = new LiquidizableList() })));
        }

        [Test]
        public async Task TestWrappedListRendering()
        {
            Assert.AreEqual(
                expected: string.Empty,
                actual: await Template
                    .Parse("{{context}}")
                    .RenderAsync(Hash.FromAnonymousObject(new { context = new IndexableLiquidizable() })));

            Assert.AreEqual(
                expected: "text1text2",
                actual: await Template
                    .Parse("{{context.thekey}}")
                    .RenderAsync(Hash.FromAnonymousObject(new { context = new IndexableLiquidizable() })));
        }

        [Test]
        public async Task TestDictionaryRendering()
        {
            Assert.AreEqual(
                expected: "[lambda, Hello][alpha, bet]",
                actual: await Template
                    .Parse("{{context}}")
                    .RenderAsync(Hash.FromAnonymousObject(new { context = new Dictionary<string, object> { ["lambda"] = "Hello", ["alpha"] = "bet" } })));
        }

        [Test]
        public async Task TestDictionaryAsVariable()
        {
            _context.Set("dynamic", Hash.FromDictionary(new Dictionary<string, object> { ["lambda"] = "Hello" }));

            Assert.AreEqual("Hello", await _context.GetAsync("dynamic.lambda"));
        }

        [Test]
        public async Task TestNestedDictionaryAsVariable()
        {
            _context.Set("dynamic", Hash.FromDictionary(new Dictionary<string, object> { ["lambda"] = new Dictionary<string, object> { ["name"] = "Hello" } }));

            Assert.AreEqual("Hello", await _context.GetAsync("dynamic.lambda.name"));
        }

        [Test]
        public async Task TestDynamicAsVariable()
        {
            dynamic expandoObject = new ExpandoObject();
            expandoObject.lambda = "Hello";
            _context.Set("dynamic", Hash.FromDictionary(expandoObject));

            Assert.AreEqual("Hello", await _context.GetAsync("dynamic.lambda"));
        }

        [Test]
        public async Task TestNestedDynamicAsVariable()
        {
            dynamic root = new ExpandoObject();
            root.lambda = new ExpandoObject();
            root.lambda.name = "Hello";
            _context.Set("dynamic", Hash.FromDictionary(root));

            Assert.AreEqual("Hello", await _context.GetAsync("dynamic.lambda.name"));
        }

        /// <summary>
        /// Test case for [Issue #350](https://github.com/dotliquid/dotliquid/issues/350)
        /// </summary>
        [Test]
        public async Task TestNestedExpandoTemplate_Issue350()
        {
            var model = new ExpandoModel()
            {
                IntProperty = 23,
                StringProperty = "from string property",
                Properties = new ExpandoObject()
            };
            var dictionary = (IDictionary<string, object>)model.Properties;
            dictionary.Add("Key1", "ExpandoObject Key1 value");

            Template.RegisterSafeType(typeof(ExpandoModel), new[] { "IntProperty", "StringProperty", "Properties" });
            const string templateString = @"Int: '{{IntProperty}}'; String: '{{StringProperty}}'; Expando: '{{Properties.Key1}}'";
            var template = Template.Parse(templateString);
            Assert.AreEqual(expected: "Int: '23'; String: 'from string property'; Expando: 'ExpandoObject Key1 value'",
                            actual: await template.RenderAsync(Hash.FromAnonymousObject(model)));
        }

        /// <summary>
        /// Test case for [Issue #417](https://github.com/dotliquid/dotliquid/issues/417)
        /// </summary>
        [Test]
        public async Task TestNestedExpandoTemplate_Issue417()
        {
            var modelString = "{\"States\": [{\"Name\": \"Texas\",\"Code\": \"TX\"}, {\"Name\": \"New York\",\"Code\": \"NY\"}]}";
            var template = "State Is:{{States[0].Name}}";

            var model = JsonConvert.DeserializeObject<ExpandoObject>(modelString);
            var modelHash = Hash.FromDictionary(model);
            Assert.AreEqual(expected: "State Is:Texas", actual: await Template.Parse(template).RenderAsync(modelHash));
        }

        /// <summary>
        /// Test case for [Issue #474](https://github.com/dotliquid/dotliquid/issues/474)
        /// </summary>
        [Test]
        public async Task TestDecimalIndexer_Issue474()
        {
            var template = @"{% assign idx = fraction | minus: 0.01 -%}
{{ arr[0] }}
{{ arr[idx] }}";

            var modelHash = Hash.FromAnonymousObject(new { arr = new[] { "Zero", "One" }, fraction = 0.01 });
            Assert.AreEqual(expected: "Zero\r\nZero", actual: await Template.Parse(template).RenderAsync(modelHash));
        }

        /// <summary>
        /// Test case for [Issue #474](https://github.com/dotliquid/dotliquid/issues/474)
        /// </summary>
        [Test]
        public async Task TestAllTypesIndexer_Issue474()
        {
            var zero = 0;
            var typesToTest = Util.ExpressionUtilityTest.GetNumericCombinations().Select(item => item.Item1).Distinct().ToList();
            var arrayOfZeroTypes = typesToTest.Select(type => Convert.ChangeType(zero, type)).ToList();

            var template = @"{% for idx in numerics -%}
{{ arr[idx] }}
{% endfor %}";

            var modelHash = Hash.FromAnonymousObject(new { arr = new[] { "Zero", "One" }, numerics = arrayOfZeroTypes });
            Assert.AreEqual(expected: string.Join(String.Empty, Enumerable.Repeat("Zero\r\n", arrayOfZeroTypes.Count)), actual: await Template.Parse(template).RenderAsync(modelHash));
        }

        [Test]
        public async Task TestProcAsVariable()
        {
            _context.Set("dynamic", (Proc)delegate { return "Hello"; });

            Assert.AreEqual("Hello", await _context.GetAsync("dynamic"));
        }

        [Test]
        public async Task TestLambdaAsVariable()
        {
            _context.Set("dynamic", (Proc)(c => "Hello"));

            Assert.AreEqual("Hello", await _context.GetAsync("dynamic"));
        }

        [Test]
        public async Task TestNestedLambdaAsVariable()
        {
            _context.Set("dynamic", Hash.FromAnonymousObject(new { lambda = (Proc)(c => "Hello") }));

            Assert.AreEqual("Hello", await _context.GetAsync("dynamic.lambda"));
        }

        [Test]
        public async Task TestArrayContainingLambdaAsVariable()
        {
            _context.Set("dynamic", new object[] { 1, 2, (Proc)(c => "Hello"), 4, 5 });

            Assert.AreEqual("Hello", await _context.GetAsync("dynamic[2]"));
        }

        [Test]
        public async Task TestLambdaIsCalledOnce()
        {
            int global = 0;
            _context.Set("callcount", (Proc)(c =>
            {
                ++global;
                return global.ToString();
            }));

            Assert.AreEqual("1", await _context.GetAsync("callcount"));
            Assert.AreEqual("1", await _context.GetAsync("callcount"));
            Assert.AreEqual("1", await _context.GetAsync("callcount"));
        }

        [Test]
        public async Task TestNestedLambdaIsCalledOnce()
        {
            int global = 0;
            _context.Set("callcount", Hash.FromAnonymousObject(new
            {
                lambda = (Proc)(c =>
                {
                    ++global;
                    return global.ToString();
                })
            }));

            Assert.AreEqual("1", await _context.GetAsync("callcount.lambda"));
            Assert.AreEqual("1", await _context.GetAsync("callcount.lambda"));
            Assert.AreEqual("1", await _context.GetAsync("callcount.lambda"));
        }

        [Test]
        public async Task TestLambdaInArrayIsCalledOnce()
        {
            int global = 0;
            _context.Set("callcount", new object[]
            { 1, 2, (Proc) (c =>
                {
                    ++global;
                    return global.ToString();
                }), 4, 5
            });

            Assert.AreEqual("1", await _context.GetAsync("callcount[2]"));
            Assert.AreEqual("1", await _context.GetAsync("callcount[2]"));
            Assert.AreEqual("1", await _context.GetAsync("callcount[2]"));
        }

        [Test]
        public async Task TestAccessToContextFromProc()
        {
            _context.Registers["magic"] = 345392;

            _context.Set("magic", (Proc)(c => _context.Registers["magic"]));

            Assert.AreEqual(345392, await _context.GetAsync("magic"));
        }

        [Test]
        public async Task TestToLiquidAndContextAtFirstLevel()
        {
            _context.Set("category", new Category("foobar"));
            Assert.IsInstanceOf<CategoryDrop>(await _context.GetAsync("category"));
            Assert.AreEqual(_context, ((CategoryDrop)await _context.GetAsync("category")).Context);
        }

        [Test]
        public void TestVariableParserV21()
        {
            var regex = new System.Text.RegularExpressions.Regex(Liquid.VariableParser);
            TestVariableParser((input) => DotLiquid.Util.R.Scan(input, regex));
        }

        [Test]
        public void TestVariableParserV22()
        {
            TestVariableParser((input) => GetVariableParts(input));
        }

        private void TestVariableParser(Func<string, IEnumerable<string>> variableSplitterFunc)
        {
            CollectionAssert.IsEmpty(variableSplitterFunc(""));
            CollectionAssert.AreEqual(new[] { "var" }, variableSplitterFunc("var"));
            CollectionAssert.AreEqual(new[] { "var", "method" }, variableSplitterFunc("var.method"));
            CollectionAssert.AreEqual(new[] { "var", "[method]" }, variableSplitterFunc("var[method]"));
            CollectionAssert.AreEqual(new[] { "var", "[method]", "[0]" }, variableSplitterFunc("var[method][0]"));
            CollectionAssert.AreEqual(new[] { "var", "[\"method\"]", "[0]" }, variableSplitterFunc("var[\"method\"][0]"));
            CollectionAssert.AreEqual(new[] { "var", "[method]", "[0]", "method" }, variableSplitterFunc("var[method][0].method"));
        }

        private static IEnumerable<string> GetVariableParts(string input)
        {
            using (var enumerator = Tokenizer.GetVariableEnumerator(input))
                while (enumerator.MoveNext())
                    yield return enumerator.Current;
        }

        [Test]
        public void TestConstructor()
        {
            var context = new Context(new CultureInfo("jp-JP"));
            Assert.AreEqual(Template.DefaultSyntaxCompatibilityLevel, context.SyntaxCompatibilityLevel);
            Assert.AreEqual(Liquid.UseRubyDateFormat, context.UseRubyDateFormat);
            Assert.AreEqual("jp-JP", context.CurrentCulture.Name);
        }

        /// <summary>
        /// The expectation is that a Context is created with a CultureInfo, however,
        /// the parameter is defined as an IFormatProvider so this is not enforced by
        /// the compiler.
        /// </summary>
        /// <remarks>
        /// This test verifies that a CultureInfo is returned by Context.CultureInfo even
        /// if Context was created with a non-CultureInfo
        /// </remarks>
        [Test]
        public void TestCurrentCulture_NotACultureInfo()
        {
            // Create context with an IFormatProvider that is not a CultureInfo
            Context context = new Context(CultureInfo.CurrentCulture.NumberFormat);
            Assert.AreSame(CultureInfo.CurrentCulture, context.CurrentCulture);
        }
    }
}
