using System;
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DotLiquid.Tests
{
    public class FunctionFilterTests
    {
        private Context _context;

        [SetUp]
        public void Setup()
        {
            _context = new Context(CultureInfo.InvariantCulture);
        }

        [Test]
        public async Task AddingFunctions()
        {
            _context.Set("var", 2);
            _context.AddFilter<int, string>("AddTwo", i => (i + 2).ToString(CultureInfo.InvariantCulture));
            Assert.That(await new Variable("var | add_two").RenderAsync(_context), Is.EqualTo("4"));
        }

        [Test]
        public async Task AddingAnonimousFunctionWithClosure()
        {
            _context.Set("var", 2);
            int x = 2;

            // (x=(i + x)) is to forbid JITC to inline x and force it to create non-static closure

            _context.AddFilter<int, string>("AddTwo", i => (x=(i + x)).ToString(CultureInfo.InvariantCulture));
            Assert.That(await new Variable("var | add_two").RenderAsync(_context), Is.EqualTo("4"));

            //this is done, to forbid JITC to inline x 
            Assert.That(x, Is.EqualTo(4));
        }

        [Test]
        public async Task AddingMethodInfo()
        {
            _context.Set("var", 2);
            _context.AddFilter<int, string>("AddTwo", i => (i + 2).ToString(CultureInfo.InvariantCulture));
            Assert.That(await new Variable("var | add_two").RenderAsync(_context), Is.EqualTo("4"));
        }
    }
}