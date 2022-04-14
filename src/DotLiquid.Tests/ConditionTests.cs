using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DotLiquid.Exceptions;
using DotLiquid.NamingConventions;
using NUnit.Framework;

namespace DotLiquid.Tests
{
    [TestFixture]
    public class ConditionTests
    {
        #region Classes used in tests
        public class Car : Drop, System.IEquatable<Car>, System.IEquatable<string>
        {
            public string Make { get; set; }
            public string Model { get; set; }

            public override string ToString()
            {
                return $"{Make} {Model}";
            }

            public override bool Equals(object other)
            {
                if (other is Car @car)
                    return Equals(@car);

                if (other is string @string)
                    return Equals(@string);

                return false;
            }

            public bool Equals(Car other)
            {
                return other.Make == this.Make && other.Model == this.Model;
            }

            public bool Equals(string other)
            {
                return other == this.ToString();
            }
        }
        #endregion

        // NOTE(David Burg): This forces sequential execution of tests, risk side effect resulting in non deterministic behavior.
        // Context should be passed as a parameter instead.
        private Context _context;

        [Test]
        public async Task TestBasicCondition()
        {
            Assert.AreEqual(expected: false, actual: await new Condition(left: "1", @operator: "==", right: "2").EvaluateAsync(context: null, formatProvider: CultureInfo.InvariantCulture));
            Assert.AreEqual(expected: true, actual: await new Condition(left: "1", @operator: "==", right: "1").EvaluateAsync(context: null, formatProvider: CultureInfo.InvariantCulture));

            // NOTE(David Burg): Validate that type conversion order preserves legacy behavior
            // Even if it's out of Shopify spec compliance (all type but null and false should evaluate to true).
            await Helper.AssertTemplateResultAsync(expected: "TRUE", template: "{% if true == 'true' %}TRUE{% else %}FALSE{% endif %}");
            await Helper.AssertTemplateResultAsync(expected: "FALSE", template: "{% if 'true' == true %}TRUE{% else %}FALSE{% endif %}");

            await Helper.AssertTemplateResultAsync(expected: "TRUE", template: "{% if true %}TRUE{% endif %}");
            await Helper.AssertTemplateResultAsync(expected: "", template: "{% if false %}TRUE{% endif %}");
            await Helper.AssertTemplateResultAsync(expected: "TRUE", template: "{% if true %}TRUE{% else %}FALSE{% endif %}");
            await Helper.AssertTemplateResultAsync(expected: "FALSE", template: "{% if false %}TRUE{% else %}FALSE{% endif %}");
            await Helper.AssertTemplateResultAsync(expected: "TRUE", template: "{% if '1' == '1' %}TRUE{% else %}FALSE{% endif %}");
            await Helper.AssertTemplateResultAsync(expected: "FALSE", template: "{% if '1' == '2' %}TRUE{% else %}FALSE{% endif %}");
            await Helper.AssertTemplateResultAsync(expected: "This condition will always be true.", template: "{% assign tobi = 'Tobi' %}{% if tobi %}This condition will always be true.{% endif %}");

            await Helper.AssertTemplateResultAsync(expected: "TRUE", template: "{% if true == true %}TRUE{% else %}FALSE{% endif %}");
            await Helper.AssertTemplateResultAsync(expected: "FALSE", template: "{% if true == false %}TRUE{% else %}FALSE{% endif %}");
            await Helper.AssertTemplateResultAsync(expected: "TRUE", template: "{% if false == false %}TRUE{% else %}FALSE{% endif %}");
            await Helper.AssertTemplateResultAsync(expected: "FALSE", template: "{% if false == true %}TRUE{% else %}FALSE{% endif %}");

            await Helper.AssertTemplateResultAsync(expected: "FALSE", template: "{% if true != true %}TRUE{% else %}FALSE{% endif %}");
            await Helper.AssertTemplateResultAsync(expected: "TRUE", template: "{% if true != false %}TRUE{% else %}FALSE{% endif %}");
            await Helper.AssertTemplateResultAsync(expected: "FALSE", template: "{% if false != false %}TRUE{% else %}FALSE{% endif %}");
            await Helper.AssertTemplateResultAsync(expected: "TRUE", template: "{% if false != true %}TRUE{% else %}FALSE{% endif %}");

            // NOTE(David Burg): disabled test due to https://github.com/dotliquid/dotliquid/issues/394
            ////Helper.AssertTemplateResult(expected: "This text will always appear if \"name\" is defined.", template: "{% assign name = 'Tobi' %}{% if name == true %}This text will always appear if \"name\" is defined.{% endif %}");
        }

        [Test]
        public async Task TestDefaultOperatorsEvaluateTrue()
        {
            await AssertEvaluatesTrueAsync(left: "1", op: "==", right: "1");
            await AssertEvaluatesTrueAsync(left: "1", op: "!=", right: "2");
            await AssertEvaluatesTrueAsync(left: "1", op: "<>", right: "2");
            await AssertEvaluatesTrueAsync(left: "1", op: "<", right: "2");
            await AssertEvaluatesTrueAsync(left: "2", op: ">", right: "1");
            await AssertEvaluatesTrueAsync(left: "1", op: ">=", right: "1");
            await AssertEvaluatesTrueAsync(left: "2", op: ">=", right: "1");
            await AssertEvaluatesTrueAsync(left: "1", op: "<=", right: "2");
            await AssertEvaluatesTrueAsync(left: "1", op: "<=", right: "1");
        }

        [Test]
        public async Task TestDefaultOperatorsEvaluateFalse()
        {
            await AssertEvaluatesFalseAsync("1", "==", "2");
            await AssertEvaluatesFalseAsync("1", "!=", "1");
            await AssertEvaluatesFalseAsync("1", "<>", "1");
            await AssertEvaluatesFalseAsync("1", "<", "0");
            await AssertEvaluatesFalseAsync("2", ">", "4");
            await AssertEvaluatesFalseAsync("1", ">=", "3");
            await AssertEvaluatesFalseAsync("2", ">=", "4");
            await AssertEvaluatesFalseAsync("1", "<=", "0");
            await AssertEvaluatesFalseAsync("1", "<=", "0");
        }

        [Test]
        public async Task TestContainsWorksOnStrings()
        {
            await AssertEvaluatesTrueAsync("'bob'", "contains", "'o'");
            await AssertEvaluatesTrueAsync("'bob'", "contains", "'b'");
            await AssertEvaluatesTrueAsync("'bob'", "contains", "'bo'");
            await AssertEvaluatesTrueAsync("'bob'", "contains", "'ob'");
            await AssertEvaluatesTrueAsync("'bob'", "contains", "'bob'");

            await AssertEvaluatesFalseAsync("'bob'", "contains", "'bob2'");
            await AssertEvaluatesFalseAsync("'bob'", "contains", "'a'");
            await AssertEvaluatesFalseAsync("'bob'", "contains", "'---'");
        }

        [Test]
        public async Task TestContainsWorksOnIntArrays()
        {
            // NOTE(daviburg): DotLiquid is in violation of explicit non-support of arrays for contains operators, quote:
            // "contains can only search strings. You cannot use it to check for an object in an array of objects."
            // https://shopify.github.io/liquid/basics/operators/
            // This is a rather harmless violation as all it does in generate useful output for a request which would fail
            // in the canonical Shopify implementation.
            _context = new Context(CultureInfo.InvariantCulture);
            _context.Set("array", new[] { 1, 2, 3, 4, 5 });

            await AssertEvaluatesTrueAsync(left: "array", op: "contains", right: "1");
            await AssertEvaluatesFalseAsync(left: "array", op: "contains", right: "0");
            await AssertEvaluatesTrueAsync(left: "array", op: "contains", right: "2");
            await AssertEvaluatesTrueAsync(left: "array", op: "contains", right: "3");
            await AssertEvaluatesTrueAsync(left: "array", op: "contains", right: "4");
            await AssertEvaluatesTrueAsync(left: "array", op: "contains", right: "5");
            await AssertEvaluatesFalseAsync(left: "array", op: "contains", right: "6");

            // NOTE(daviburg): Historically testing for equality cross integer and string boundaries resulted in not equal.
            await AssertEvaluatesFalseAsync(left: "array", op: "contains", right: "'1'");
        }

        [Test]
        public async Task TestContainsWorksOnLongArrays()
        {
            _context = new Context(CultureInfo.InvariantCulture);
            _context.Set("array", new long[] { 1, 2, 3, 4, 5 });

            await AssertEvaluatesTrueAsync("array", "contains", "1");
            await AssertEvaluatesFalseAsync("array", "contains", "0");
            await AssertEvaluatesTrueAsync("array", "contains", "1.0");
            await AssertEvaluatesTrueAsync("array", "contains", "2");
            await AssertEvaluatesTrueAsync("array", "contains", "3");
            await AssertEvaluatesTrueAsync("array", "contains", "4");
            await AssertEvaluatesTrueAsync("array", "contains", "5");
            await AssertEvaluatesFalseAsync("array", "contains", "6");

            await AssertEvaluatesFalseAsync("array", "contains", "'1'");
        }

        [Test]
        public async Task TestStringArrays()
        {
            _context = new Context(CultureInfo.InvariantCulture);
            var _array = new List<string>() { "Apple", "Orange", null, "Banana" };
            _context.Set("array", _array.ToArray());
            _context.Set("first", _array.First());
            _context.Set("last", _array.Last());

            await AssertEvaluatesTrueAsync(left: "array", op: "contains", right: "'Apple'");
            await AssertEvaluatesTrueAsync(left: "array", op: "startsWith", right: "first");
            await AssertEvaluatesTrueAsync(left: "array.first", op: "==", right: "first");
            await AssertEvaluatesFalseAsync(left: "array", op: "contains", right: "'apple'");
            await AssertEvaluatesFalseAsync(left: "array", op: "startsWith", right: "'apple'");
            await AssertEvaluatesFalseAsync(left: "array.first", op: "==", right: "'apple'");
            await AssertEvaluatesFalseAsync(left: "array", op: "contains", right: "'Mango'");
            await AssertEvaluatesTrueAsync(left: "array", op: "contains", right: "'Orange'");
            await AssertEvaluatesTrueAsync(left: "array", op: "contains", right: "'Banana'");
            await AssertEvaluatesTrueAsync(left: "array", op: "endsWith", right: "last");
            await AssertEvaluatesFalseAsync(left: "array", op: "contains", right: "'Orang'");
        }

        [Test]
        public async Task TestClassArrays()
        {
            _context = new Context(CultureInfo.InvariantCulture);
            var _array = new List<Car>() { new Car() { Make = "Honda", Model = "Accord" }, new Car() { Make = "Ford", Model = "Explorer" } };
            _context.Set("array", _array.ToArray());
            _context.Set("first", _array.First());
            _context.Set("last", _array.Last());
            _context.Set("clone", new Car() { Make = "Honda", Model = "Accord" });
            _context.Set("camry", new Car() { Make = "Toyota", Model = "Camry" });

            await AssertEvaluatesTrueAsync(left: "array", op: "contains", right: "first");
            await AssertEvaluatesTrueAsync(left: "array", op: "startsWith", right: "first");
            await AssertEvaluatesTrueAsync(left: "array.first", op: "==", right: "first");
            await AssertEvaluatesTrueAsync(left: "array", op: "contains", right: "clone");
            await AssertEvaluatesTrueAsync(left: "array", op: "startsWith", right: "clone");
            await AssertEvaluatesTrueAsync(left: "array", op: "endsWith", right: "last");
            await AssertEvaluatesFalseAsync(left: "array", op: "contains", right: "camry");
        }

        [Test]
        public async Task TestTruthyArray()
        {
            _context = new Context(CultureInfo.InvariantCulture);
            var _array = new List<bool>() { true };
            _context.Set("array", _array.ToArray());
            _context.Set("first", _array.First());

            await AssertEvaluatesTrueAsync(left: "array", op: "contains", right: "first");
            await AssertEvaluatesTrueAsync(left: "array", op: "startsWith", right: "first");
            await AssertEvaluatesTrueAsync(left: "array.first", op: "==", right: "'true'");
            await AssertEvaluatesTrueAsync(left: "array", op: "startsWith", right: "'true'");

            await AssertEvaluatesFalseAsync(left: "array", op: "contains", right: "'true'"); // to be re-evaluated in #362
        }

        [Test]
        public async Task TestCharArrays()
        {
            _context = new Context(CultureInfo.InvariantCulture);
            var _array = new List<char> { 'A', 'B', 'C' };
            _context.Set("array", _array.ToArray());
            _context.Set("first", _array.First());
            _context.Set("last", _array.Last());

            await AssertEvaluatesTrueAsync(left: "array", op: "contains", right: "'A'");
            await AssertEvaluatesTrueAsync(left: "array", op: "contains", right: "first");
            await AssertEvaluatesTrueAsync(left: "array", op: "startsWith", right: "first");
            await AssertEvaluatesTrueAsync(left: "array.first", op: "==", right: "first");
            await AssertEvaluatesFalseAsync(left: "array", op: "contains", right: "'a'");
            await AssertEvaluatesFalseAsync(left: "array", op: "contains", right: "'X'");
            await AssertEvaluatesTrueAsync(left: "array", op: "contains", right: "'B'");
            await AssertEvaluatesTrueAsync(left: "array", op: "contains", right: "'C'");
            await AssertEvaluatesTrueAsync(left: "array", op: "endsWith", right: "last");
        }

        [Test]
        public async Task TestByteArrays()
        {
            _context = new Context(CultureInfo.InvariantCulture);
            var _array = new List<byte> { 0x01, 0x02, 0x03, 0x30 };
            _context.Set("array", _array.ToArray());
            _context.Set("first", _array.First());
            _context.Set("last", _array.Last());

            await AssertEvaluatesFalseAsync(left: "array", op: "contains", right: "0");
            await AssertEvaluatesFalseAsync(left: "array", op: "contains", right: "'0'");
            await AssertEvaluatesTrueAsync(left: "array", op: "startsWith", right: "first");
            await AssertEvaluatesTrueAsync(left: "array.first", op: "==", right: "first");
            await AssertEvaluatesTrueAsync(left: "array", op: "contains", right: "first");
            await AssertEvaluatesFalseAsync(left: "array", op: "contains", right: "1");
            await AssertEvaluatesTrueAsync(left: "array", op: "endsWith", right: "last");
        }

        [Test]
        public async Task TestContainsWorksOnDoubleArrays()
        {
            _context = new Context(CultureInfo.InvariantCulture);
            _context.Set("array", new double[] { 1.0, 2.1, 3.25, 4.333, 5.0 });

            await AssertEvaluatesTrueAsync("array", "contains", "1.0");
            await AssertEvaluatesFalseAsync("array", "contains", "0");
            await AssertEvaluatesTrueAsync("array", "contains", "2.1");
            await AssertEvaluatesFalseAsync("array", "contains", "3");
            await AssertEvaluatesFalseAsync("array", "contains", "4.33");
            await AssertEvaluatesTrueAsync("array", "contains", "5.00");
            await AssertEvaluatesFalseAsync("array", "contains", "6");

            await AssertEvaluatesFalseAsync("array", "contains", "'1'");
        }

        [Test]
        public async Task TestContainsReturnsFalseForNilCommands()
        {
            await AssertEvaluatesFalseAsync("not_assigned", "contains", "0");
            await AssertEvaluatesFalseAsync("0", "contains", "not_assigned");
        }

        [Test]
        public async Task TestStartsWithWorksOnStrings()
        {
            await AssertEvaluatesTrueAsync("'dave'", "startswith", "'d'");
            await AssertEvaluatesTrueAsync("'dave'", "startswith", "'da'");
            await AssertEvaluatesTrueAsync("'dave'", "startswith", "'dav'");
            await AssertEvaluatesTrueAsync("'dave'", "startswith", "'dave'");

            await AssertEvaluatesFalseAsync("'dave'", "startswith", "'ave'");
            await AssertEvaluatesFalseAsync("'dave'", "startswith", "'e'");
            await AssertEvaluatesFalseAsync("'dave'", "startswith", "'---'");
        }

        [Test]
        public async Task TestStartsWithWorksOnArrays()
        {
            _context = new Context(CultureInfo.InvariantCulture);
            _context.Set("array", new[] { 1, 2, 3, 4, 5 });

            await AssertEvaluatesFalseAsync("array", "startswith", "0");
            await AssertEvaluatesTrueAsync("array", "startswith", "1");
        }

        [Test]
        public async Task TestStartsWithReturnsFalseForNilCommands()
        {
            await AssertEvaluatesFalseAsync("not_assigned", "startswith", "0");
            await AssertEvaluatesFalseAsync("0", "startswith", "not_assigned");
        }

        [Test]
        public async Task TestEndsWithWorksOnStrings()
        {
            await AssertEvaluatesTrueAsync("'dave'", "endswith", "'e'");
            await AssertEvaluatesTrueAsync("'dave'", "endswith", "'ve'");
            await AssertEvaluatesTrueAsync("'dave'", "endswith", "'ave'");
            await AssertEvaluatesTrueAsync("'dave'", "endswith", "'dave'");

            await AssertEvaluatesFalseAsync("'dave'", "endswith", "'dav'");
            await AssertEvaluatesFalseAsync("'dave'", "endswith", "'d'");
            await AssertEvaluatesFalseAsync("'dave'", "endswith", "'---'");
        }

        [Test]
        public async Task TestEndsWithWorksOnArrays()
        {
            _context = new Context(CultureInfo.InvariantCulture);
            _context.Set("array", new[] { 1, 2, 3, 4, 5 });

            await AssertEvaluatesFalseAsync("array", "endswith", "0");
            await AssertEvaluatesTrueAsync("array", "endswith", "5");
        }

        [Test]
        public async Task TestEndsWithReturnsFalseForNilCommands()
        {
            await AssertEvaluatesFalseAsync("not_assigned", "endswith", "0");
            await AssertEvaluatesFalseAsync("0", "endswith", "not_assigned");
        }

        [Test]
        public async Task TestDictionaryHasKey()
        {
            _context = new Context(CultureInfo.InvariantCulture);
            System.Collections.Generic.Dictionary<string, string> testDictionary = new System.Collections.Generic.Dictionary<string, string>
            {
                { "dave", "0" },
                { "bob", "4" }
            };
            _context.Set("dictionary", testDictionary);

            await AssertEvaluatesTrueAsync("dictionary", "haskey", "'bob'");
            await AssertEvaluatesFalseAsync("dictionary", "haskey", "'0'");
        }

        [Test]
        public async Task TestDictionaryHasValue()
        {
            _context = new Context(CultureInfo.InvariantCulture);
            System.Collections.Generic.Dictionary<string, string> testDictionary = new System.Collections.Generic.Dictionary<string, string>
            {
                { "dave", "0" },
                { "bob", "4" }
            };
            _context.Set("dictionary", testDictionary);

            await AssertEvaluatesTrueAsync("dictionary", "hasvalue", "'0'");
            await AssertEvaluatesFalseAsync("dictionary", "hasvalue", "'bob'");
        }

        [Test]
        public async Task TestOrCondition()
        {
            Condition condition = new Condition("1", "==", "2");
            Assert.IsFalse(await condition.EvaluateAsync(null, CultureInfo.InvariantCulture));

            condition.Or(new Condition("2", "==", "1"));
            Assert.IsFalse(await condition.EvaluateAsync(null, CultureInfo.InvariantCulture));

            condition.Or(new Condition("1", "==", "1"));
            Assert.IsTrue(await condition.EvaluateAsync(null, CultureInfo.InvariantCulture));
        }

        [Test]
        public async Task TestAndCondition()
        {
            Condition condition = new Condition("1", "==", "1");
            Assert.IsTrue(await condition.EvaluateAsync(null, CultureInfo.InvariantCulture));

            condition.And(new Condition("2", "==", "2"));
            Assert.IsTrue(await condition.EvaluateAsync(null, CultureInfo.InvariantCulture));

            condition.And(new Condition("2", "==", "1"));
            Assert.IsFalse(await condition.EvaluateAsync(null, CultureInfo.InvariantCulture));
        }

        [Test]
        public async Task TestShouldAllowCustomProcOperator()
        {
            try
            {
                Condition.Operators["starts_with"] =
                    (left, right) => Regex.IsMatch(left.ToString(), string.Format("^{0}", right.ToString()));

                await AssertEvaluatesTrueAsync("'bob'", "starts_with", "'b'");
                await AssertEvaluatesFalseAsync("'bob'", "starts_with", "'o'");
            }
            finally
            {
                Condition.Operators.Remove("starts_with");
            }
        }

        [Test]
        public async Task TestCapitalInCustomOperatorInt()
        {
            try
            {
                Condition.Operators["IsMultipleOf"] =
                    (left, right) => (int)left % (int)right == 0;

                // exact match
                await AssertEvaluatesTrueAsync("16", "IsMultipleOf", "4");
                await AssertEvaluatesTrueAsync("2147483646", "IsMultipleOf", "2");
                AssertError("2147483648", "IsMultipleOf", "2", typeof(System.InvalidCastException));
                await AssertEvaluatesFalseAsync("16", "IsMultipleOf", "5");

                // lower case: compatibility
                await AssertEvaluatesTrueAsync("16", "ismultipleof", "4");
                await AssertEvaluatesFalseAsync("16", "ismultipleof", "5");

                await AssertEvaluatesTrueAsync("16", "is_multiple_of", "4");
                await AssertEvaluatesFalseAsync("16", "is_multiple_of", "5");

                // camel case : incompatible
                AssertError("16", "isMultipleOf", "4", typeof(ArgumentException));

                //Run tests through the template to verify that capitalization rules are followed through template parsing
                await Helper.AssertTemplateResultAsync(" TRUE ", "{% if 16 IsMultipleOf 4 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync("", "{% if 14 IsMultipleOf 4 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync(" TRUE ", "{% if 16 ismultipleof 4 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync("", "{% if 14 ismultipleof 4 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync(" TRUE ", "{% if 16 is_multiple_of 4 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync("", "{% if 14 is_multiple_of 4 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync("Liquid error: Unknown operator isMultipleOf", "{% if 16 isMultipleOf 4 %} TRUE {% endif %}");
            }
            finally
            {
                Condition.Operators.Remove("IsMultipleOf");
            }
        }

        [Test]
        public async Task TestCapitalInCustomOperatorLong()
        {
            try
            {
                Condition.Operators["IsMultipleOf"] =
                    (left, right) => System.Convert.ToInt64(left) % System.Convert.ToInt64(right) == 0;

                // exact match
                await AssertEvaluatesTrueAsync("16", "IsMultipleOf", "4");
                await AssertEvaluatesTrueAsync("2147483646", "IsMultipleOf", "2");
                await AssertEvaluatesTrueAsync("2147483648", "IsMultipleOf", "2");
                await AssertEvaluatesFalseAsync("16", "IsMultipleOf", "5");

                // lower case: compatibility
                await AssertEvaluatesTrueAsync("16", "ismultipleof", "4");
                await AssertEvaluatesFalseAsync("16", "ismultipleof", "5");

                await AssertEvaluatesTrueAsync("16", "is_multiple_of", "4");
                await AssertEvaluatesFalseAsync("16", "is_multiple_of", "5");

                // camel case : incompatible
                AssertError("16", "isMultipleOf", "4", typeof(ArgumentException));

                //Run tests through the template to verify that capitalization rules are followed through template parsing
                await Helper.AssertTemplateResultAsync(" TRUE ", "{% if 16 IsMultipleOf 4 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync("", "{% if 14 IsMultipleOf 4 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync(" TRUE ", "{% if 16 ismultipleof 4 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync("", "{% if 14 ismultipleof 4 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync(" TRUE ", "{% if 16 is_multiple_of 4 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync("", "{% if 14 is_multiple_of 4 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync("Liquid error: Unknown operator isMultipleOf", "{% if 16 isMultipleOf 4 %} TRUE {% endif %}");
            }
            finally
            {
                Condition.Operators.Remove("IsMultipleOf");
            }
        }

        [Test]
        public async Task TestCapitalInCustomCSharpOperatorInt()
        {
            //have to run this test in a lock because it requires
            //changing the globally static NamingConvention
            var semaphoreSlim = new SemaphoreSlim(1, 1);
            await semaphoreSlim.WaitAsync();
            
            var oldconvention = Template.NamingConvention;
            Template.NamingConvention = new CSharpNamingConvention();

            try
            {
                Condition.Operators["DivisibleBy"] =
                    (left, right) => (int)left % (int)right == 0;

                // exact match
                await AssertEvaluatesTrueAsync("16", "DivisibleBy", "4");
                await AssertEvaluatesTrueAsync("2147483646", "DivisibleBy", "2");
                AssertError("2147483648", "DivisibleBy", "2", typeof(System.InvalidCastException));
                await AssertEvaluatesFalseAsync("16", "DivisibleBy", "5");

                // lower case: compatibility
                await AssertEvaluatesTrueAsync("16", "divisibleby", "4");
                await AssertEvaluatesFalseAsync("16", "divisibleby", "5");

                // camel case : compatibility
                await AssertEvaluatesTrueAsync("16", "divisibleBy", "4");
                await AssertEvaluatesFalseAsync("16", "divisibleBy", "5");

                // snake case : incompatible
                AssertError("16", "divisible_by", "4", typeof(ArgumentException));

                //Run tests through the template to verify that capitalization rules are followed through template parsing
                await Helper.AssertTemplateResultAsync(" TRUE ", "{% if 16 DivisibleBy 4 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync("", "{% if 16 DivisibleBy 5 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync(" TRUE ", "{% if 16 divisibleby 4 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync("", "{% if 16 divisibleby 5 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync("Liquid error: Unknown operator divisible_by", "{% if 16 divisible_by 4 %} TRUE {% endif %}");
            }
            finally
            {
                Condition.Operators.Remove("DivisibleBy");
                Template.NamingConvention = oldconvention;
                semaphoreSlim.Release();
            }
            
        }

        [Test]
        public async Task TestCapitalInCustomCSharpOperatorLong()
        {
            //have to run this test in a lock because it requires
            //changing the globally static NamingConvention
            var semaphoreSlim = new SemaphoreSlim(1, 1);
            await semaphoreSlim.WaitAsync();
            
            var oldconvention = Template.NamingConvention;
            Template.NamingConvention = new CSharpNamingConvention();

            try
            {
                Condition.Operators["DivisibleBy"] =
                    (left, right) => System.Convert.ToInt64(left) % System.Convert.ToInt64(right) == 0;

                // exact match
                await AssertEvaluatesTrueAsync("16", "DivisibleBy", "4");
                await AssertEvaluatesTrueAsync("2147483646", "DivisibleBy", "2");
                await AssertEvaluatesTrueAsync("2147483648", "DivisibleBy", "2");
                await AssertEvaluatesFalseAsync("16", "DivisibleBy", "5");

                // lower case: compatibility
                await AssertEvaluatesTrueAsync("16", "divisibleby", "4");
                await AssertEvaluatesFalseAsync("16", "divisibleby", "5");

                // camel case: compatibility
                await AssertEvaluatesTrueAsync("16", "divisibleBy", "4");
                await AssertEvaluatesFalseAsync("16", "divisibleBy", "5");

                // snake case: incompatible
                AssertError("16", "divisible_by", "4", typeof(ArgumentException));

                //Run tests through the template to verify that capitalization rules are followed through template parsing
                await Helper.AssertTemplateResultAsync(" TRUE ", "{% if 16 DivisibleBy 4 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync("", "{% if 16 DivisibleBy 5 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync(" TRUE ", "{% if 16 divisibleby 4 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync("", "{% if 16 divisibleby 5 %} TRUE {% endif %}");
                await Helper.AssertTemplateResultAsync("Liquid error: Unknown operator divisible_by", "{% if 16 divisible_by 4 %} TRUE {% endif %}");
            }
            finally
            {
                Condition.Operators.Remove("DivisibleBy");
                Template.NamingConvention = oldconvention;
                semaphoreSlim.Release();
            }
            
        }

        [Test]
        public async Task TestLessThanDecimal()
        {
            var model = new { value = new decimal(-10.5) };

            string output = await Template.Parse("{% if model.value < 0 %}passed{% endif %}")
                .RenderAsync(Hash.FromAnonymousObject(new { model }));

            Assert.AreEqual("passed", output);
        }

        [Test]
        public async Task TestCompareBetweenDifferentTypes()
        {
            var row = new System.Collections.Generic.Dictionary<string, object>();

            short id = 1;
            row.Add("MyID", id);

            var current = "MyID is {% if MyID == 1 %}1{%endif%}";
            var parse = DotLiquid.Template.Parse(current);
            var parsedOutput = await parse.RenderAsync(new RenderParameters(CultureInfo.InvariantCulture) { LocalVariables = Hash.FromDictionary(row) });
            Assert.AreEqual("MyID is 1", parsedOutput);
        }

        [Test]
        public async Task TestShouldAllowCustomProcOperatorCapitalized()
        {
            try
            {
                Condition.Operators["StartsWith"] =
                    (left, right) => Regex.IsMatch(left.ToString(), string.Format("^{0}", right.ToString()));

                await Helper.AssertTemplateResultAsync("", "{% if 'bob' StartsWith 'B' %} YES {% endif %}", null, new CSharpNamingConvention());
                await AssertEvaluatesTrueAsync("'bob'", "StartsWith", "'b'");
                await AssertEvaluatesFalseAsync("'bob'", "StartsWith", "'o'");
            }
            finally
            {
                Condition.Operators.Remove("StartsWith");
            }
        }

        [Test]
        public async Task TestRuby_LowerCaseAccepted()
        {
            await Helper.AssertTemplateResultAsync("", "{% if 'bob' startswith 'B' %} YES {% endif %}");
            await Helper.AssertTemplateResultAsync(" YES ", "{% if 'Bob' startswith 'B' %} YES {% endif %}");
        }

        [Test]
        public async Task TestRuby_SnakeCaseAccepted()
        {
            await Helper.AssertTemplateResultAsync("", "{% if 'bob' starts_with 'B' %} YES {% endif %}");
            await Helper.AssertTemplateResultAsync(" YES ", "{% if 'Bob' starts_with 'B' %} YES {% endif %}");
        }

        [Test]
        public async Task TestRuby_PascalCaseNotAccepted()
        {
            await Helper.AssertTemplateResultAsync("Liquid error: Unknown operator StartsWith", "{% if 'bob' StartsWith 'B' %} YES {% endif %}");
        }

        [Test]
        public async Task TestCSharp_LowerCaseAccepted()
        {
            await Helper.AssertTemplateResultAsync("", "{% if 'bob' startswith 'B' %} YES {% endif %}", null, new CSharpNamingConvention());
            await Helper.AssertTemplateResultAsync(" YES ", "{% if 'Bob' startswith 'B' %} YES {% endif %}", null, new CSharpNamingConvention());
        }

        [Test]
        public async Task TestCSharp_PascalCaseAccepted()
        {
            await Helper.AssertTemplateResultAsync("", "{% if 'bob' StartsWith 'B' %} YES {% endif %}", null, new CSharpNamingConvention());
            await Helper.AssertTemplateResultAsync(" YES ", "{% if 'Bob' StartsWith 'B' %} YES {% endif %}", null, new CSharpNamingConvention());
        }

        [Test]
        public async Task TestCSharp_LowerPascalCaseAccepted()
        {
            await Helper.AssertTemplateResultAsync("", "{% if 'bob' startsWith 'B' %} YES {% endif %}", null, new CSharpNamingConvention());
            await Helper.AssertTemplateResultAsync(" YES ", "{% if 'Bob' startsWith 'B' %} YES {% endif %}", null, new CSharpNamingConvention());
        }

        [Test]
        public async Task TestCSharp_SnakeCaseNotAccepted()
        {
            await Helper.AssertTemplateResultAsync("Liquid error: Unknown operator starts_with", "{% if 'bob' starts_with 'B' %} YES {% endif %}", null, new CSharpNamingConvention());
        }

        private enum TestEnum { Yes, No }

        [Test]
        public async Task TestEqualOperatorsWorksOnEnum()
        {
            _context = new Context(CultureInfo.InvariantCulture);
            _context.Set("enum", TestEnum.Yes);

            await AssertEvaluatesTrueAsync("enum", "==", "'Yes'");
            await AssertEvaluatesTrueAsync("enum", "!=", "'No'");

            await AssertEvaluatesFalseAsync("enum", "==", "'No'");
            await AssertEvaluatesFalseAsync("enum", "!=", "'Yes'");
        }

        #region Helper methods

        private async Task AssertEvaluatesTrueAsync(string left, string op, string right)
        {
            Assert.IsTrue(await new Condition(left, op, right).EvaluateAsync(_context ?? new Context(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
                "Evaluated false: {0} {1} {2}", left, op, right);
        }

        private async Task AssertEvaluatesFalseAsync(string left, string op, string right)
        {
            Assert.IsFalse(await new Condition(left, op, right).EvaluateAsync(_context ?? new Context(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
                "Evaluated true: {0} {1} {2}", left, op, right);
        }

        private void AssertError(string left, string op, string right, System.Type errorType)
        {
            Assert.ThrowsAsync(errorType, async () => await new Condition(left, op, right).EvaluateAsync(_context ?? new Context(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
        }

        #endregion
    }
}
