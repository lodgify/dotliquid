using DotLiquid.FileSystems;
using NUnit.Framework;
using System.Threading.Tasks;

namespace DotLiquid.Tests.Tags
{
    [TestFixture]
    public class InheritanceTests
    {
        private class TestFileSystem : IFileSystem
        {
            public async Task<string> ReadTemplateFileAsync(Context context, string templateName)
            {
                string templatePath = (string)await context.GetAsync(templateName);

                switch(templatePath) {
                case "simple":
                    return "test";
                case "complex":
                    return @"some markup here...
                             {% block thing %}
                                 thing block
                             {% endblock %}
                             {% block another %}
                                 another block
                             {% endblock %}
                             ...and some markup here";
                case "nested":
                    return @"{% extends 'complex' %}
                             {% block thing %}
                                another thing(from nested)
                             {% endblock %}";
                case "outer":
                    return "{% block start %}{% endblock %}A{% block outer %}{% endblock %}Z";
                case "middle":
                    return @"{% extends 'outer' %}
                             {% block outer %}B{% block middle %}{% endblock %}Y{% endblock %}";
                case "middleunless":
                    return @"{% extends 'outer' %}
                             {% block outer %}B{% unless nomiddle %}{% block middle %}{% endblock %}{% endunless %}Y{% endblock %}";
                default:
                    return @"{% extends 'complex' %}
                             {% block thing %}
                                thing block(from nested)
                             {% endblock %}";
                }
            }
        }

        private IFileSystem _originalFileSystem;

        [OneTimeSetUp]
        public async Task SetUp()
        {
            _originalFileSystem = Template.FileSystem;
            Template.FileSystem = new TestFileSystem();
            await Task.Yield();
        }

        [OneTimeTearDown]
        public async Task TearDown()
        {
            Template.FileSystem = _originalFileSystem;
            await Task.Yield();
        }

        [Test]
        public async Task CanOutputTheContentsOfTheExtendedTemplate()
        {
            Template template = Template.Parse(
                                    @"{% extends 'simple' %}
                    {% block thing %}
                        yeah
                    {% endblock %}");

            StringAssert.Contains("test", await template.RenderAsync());
        }

        [Test]
        public async Task CanInherit()
        {
            Template template = Template.Parse(@"{% extends 'complex' %}");

            StringAssert.Contains("thing block", await template.RenderAsync());
        }

        [Test]
        public async Task CanInheritAndReplaceBlocks()
        {
            Template template = Template.Parse(
                                    @"{% extends 'complex' %}
                    {% block another %}
                      new content for another
                    {% endblock %}");

            StringAssert.Contains("new content for another", await template.RenderAsync());
        }

        [Test]
        public async Task CanProcessNestedInheritance()
        {
            Template template = Template.Parse(
                                    @"{% extends 'nested' %}
                  {% block thing %}
                  replacing block thing
                  {% endblock %}");

            StringAssert.Contains("replacing block thing", await template.RenderAsync());
            StringAssert.DoesNotContain("thing block", await template.RenderAsync());
        }

        [Test]
        public async Task CanRenderSuper()
        {
            Template template = Template.Parse(
                                    @"{% extends 'complex' %}
                    {% block another %}
                        {{ block.super }} + some other content
                    {% endblock %}");
                        
            StringAssert.Contains("another block", await template.RenderAsync());
            StringAssert.Contains("some other content", await template.RenderAsync());
        }

        [Test]
        public async Task CanDefineBlockInInheritedBlock()
        {
            Template template = Template.Parse(
                                    @"{% extends 'middle' %}
                  {% block middle %}C{% endblock %}");
            Assert.AreEqual("ABCYZ", await template.RenderAsync());
        }

        [Test]
        public async Task CanDefineContentInInheritedBlockFromAboveParent()
        {
            Template template = Template.Parse(@"{% extends 'middle' %}
                  {% block start %}!{% endblock %}");
            Assert.AreEqual("!ABYZ", await template.RenderAsync());
        }

        [Test]
        public async Task CanRenderBlockContainedInConditional()
        {
            Template template = Template.Parse(
                                    @"{% extends 'middleunless' %}
                  {% block middle %}C{% endblock %}");
            Assert.AreEqual("ABCYZ", await template.RenderAsync());

            template = Template.Parse(
                @"{% extends 'middleunless' %}
                  {% block start %}{% assign nomiddle = true %}{% endblock %}
                  {% block middle %}C{% endblock %}");
            Assert.AreEqual("ABYZ", await template.RenderAsync());
        }

        [Test]
        public async Task RepeatedRendersProduceSameResult()
        {
            Template template = Template.Parse(
                                    @"{% extends 'middle' %}
                  {% block start %}!{% endblock %}
                  {% block middle %}C{% endblock %}");
            Assert.AreEqual("!ABCYZ", await template.RenderAsync());
            Assert.AreEqual("!ABCYZ", await template.RenderAsync());
        }

        [Test]
        public async Task TestExtendFromTemplateFileSystem()
        {
            var fileSystem = new IncludeTagTests.TestTemplateFileSystem(new TestFileSystem());
            Template.FileSystem = fileSystem;
            for(int i = 0; i < 2; ++i)
            {
                Template template = Template.Parse(
                                    @"{% extends 'simple' %}
                    {% block thing %}
                        yeah
                    {% endblock %}");
                StringAssert.Contains("test", await template.RenderAsync());
            }
            Assert.AreEqual(fileSystem.CacheHitTimes, 1);
        }
    }
}
