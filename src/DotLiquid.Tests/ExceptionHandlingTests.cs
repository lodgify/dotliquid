using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DotLiquid.Exceptions;
using NUnit.Framework;

namespace DotLiquid.Tests
{
    [TestFixture]
    public class ExceptionHandlingTests
    {
        private class ExceptionDrop : Drop
        {
            public void ArgumentException()
            {
                throw new ArgumentException("argument exception");
            }

            public void SyntaxException()
            {
                throw new SyntaxException("syntax exception");
            }

            public void InterruptException()
            {
                throw new InterruptException("interrupted");
            }
        }

        [Test]
        public async Task TestSyntaxException()
        {
            Template template = null;
            Assert.DoesNotThrow(() => { template = Template.Parse(" {{ errors.syntax_exception }} "); });
            string result = await template.RenderAsync(Hash.FromAnonymousObject(new { errors = new ExceptionDrop() }));
            Assert.AreEqual(" Liquid syntax error: syntax exception ", result);

            Assert.AreEqual(1, template.Errors.Count);
            Assert.IsInstanceOf<SyntaxException>(template.Errors[0]);
        }

        [Test]
        public async Task TestArgumentException()
        {
            Template template = null;
            Assert.DoesNotThrow(() => { template = Template.Parse(" {{ errors.argument_exception }} "); });
            string result = await template.RenderAsync(Hash.FromAnonymousObject(new { errors = new ExceptionDrop() }));
            Assert.AreEqual(" Liquid error: argument exception ", result);

            Assert.AreEqual(1, template.Errors.Count);
            Assert.IsInstanceOf<ArgumentException>(template.Errors[0]);
        }

        [Test]
        public void TestMissingEndTagParseTimeError()
        {
            Assert.Throws<SyntaxException>(() => Template.Parse(" {% for a in b %} ... "));
        }

        [Test]
        public async Task TestUnrecognizedOperator()
        {
            Template template = null;
            Assert.DoesNotThrow(() => { template = Template.Parse(" {% if 1 =! 2 %}ok{% endif %} "); });
            Assert.AreEqual(" Liquid error: Unknown operator =! ", await template.RenderAsync());

            Assert.AreEqual(1, template.Errors.Count);
            Assert.IsInstanceOf<ArgumentException>(template.Errors[0]);
        }

        [Test]
        public void TestInterruptException()
        {
            Template template = null;
            Assert.DoesNotThrow(() => { template = Template.Parse(" {{ errors.interrupt_exception }} "); });
            var localVariables = Hash.FromAnonymousObject(new { errors = new ExceptionDrop() });
            var exception = Assert.ThrowsAsync<InterruptException>(async () => await template.RenderAsync(localVariables));

            Assert.AreEqual("interrupted", exception.Message);
        }

        [Test]
        public void TestMaximumIterationsExceededError()
        {
            var template = Template.Parse(" {% for i in (1..100000) %} {{ i }} {% endfor %} ");
            Assert.ThrowsAsync<MaximumIterationsExceededException>(async () =>
            {
                await template.RenderAsync(new RenderParameters(CultureInfo.InvariantCulture)
                {
                    MaxIterations = 50
                });
            });
        }

        [Test]
        public void TestTimeoutError()
        {
            var template = Template.Parse(" {% for i in (1..1000000) %} {{ i }} {% endfor %} ");
            Assert.ThrowsAsync<System.TimeoutException>(async () =>
            {
                await template.RenderAsync(new RenderParameters(CultureInfo.InvariantCulture)
                {
                    Timeout = 100 //ms
                });
            });
        }

        [Test]
        public void TestOperationCancelledError()
        {
            var template = Template.Parse(" {% for i in (1..1000000) %} {{ i }} {% endfor %} ");
            var source = new CancellationTokenSource(100);
            var context = new Context(
                environments: new List<Hash>(),
                outerScope: new Hash(),
                registers: new Hash(),
                errorsOutputMode: ErrorsOutputMode.Rethrow,
                maxIterations: 0,
                formatProvider: CultureInfo.InvariantCulture,
                cancellationToken: source.Token);

            Assert.ThrowsAsync<System.OperationCanceledException>(async () =>
            {
                await template.RenderAsync(RenderParameters.FromContext(context, CultureInfo.InvariantCulture));
            });
        }

        [Test]
        public void TestErrorsOutputModeRethrow()
        {
            var template = Template.Parse("{{test}}");
            Hash assigns = new Hash((h, k) => { throw new SyntaxException("Unknown variable '" + k + "'"); });

            Assert.ThrowsAsync<SyntaxException>(async () =>
            {
                var output = await template.RenderAsync(new RenderParameters(CultureInfo.InvariantCulture)
                {
                    LocalVariables = assigns,
                    ErrorsOutputMode = ErrorsOutputMode.Rethrow
                });
            });
        }

        [Test]
        public async Task TestErrorsOutputModeSuppress()
        {
            var template = Template.Parse("{{test}}");
            Hash assigns = new Hash((h, k) => { throw new SyntaxException("Unknown variable '" + k + "'"); });

            var output = await template.RenderAsync(new RenderParameters(CultureInfo.InvariantCulture)
            {
                LocalVariables = assigns,
                ErrorsOutputMode = ErrorsOutputMode.Suppress
            });
            Assert.AreEqual("", output);
        }

        [Test]
        public async Task TestErrorsOutputModeDisplay()
        {
            var template = Template.Parse("{{test}}");
            Hash assigns = new Hash((h, k) => { throw new SyntaxException("Unknown variable '" + k + "'"); });

            var output = await template.RenderAsync(new RenderParameters(CultureInfo.InvariantCulture)
            {
                LocalVariables = assigns,
                ErrorsOutputMode = ErrorsOutputMode.Display
            });
            Assert.IsNotEmpty(output);
        }
    }
}
