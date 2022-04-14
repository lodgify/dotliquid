using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotLiquid.NamingConventions;
using NUnit.Framework;

namespace DotLiquid.Tests
{
    public class Helper
    {
        static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);
        public static async Task LockTemplateStaticVarsAsync(INamingConvention namingConvention, Func<Task> test)
        {
            //Have to lock Template.NamingConvention for this test to
            //prevent other tests from being run simultaneously that
            //require the default naming convention.
            var currentNamingConvention = Template.NamingConvention;
            var currentSyntax = Template.DefaultSyntaxCompatibilityLevel;
            var currentIsRubyDateFormat = Liquid.UseRubyDateFormat;
            await SemaphoreSlim.WaitAsync();            
            Template.NamingConvention = namingConvention;

            try
            {
                await test();
            }
            finally
            {
                Template.NamingConvention = currentNamingConvention;
                Template.DefaultSyntaxCompatibilityLevel = currentSyntax;
                Liquid.UseRubyDateFormat = currentIsRubyDateFormat;
                SemaphoreSlim.Release();
            }
            
        }


        public static async Task AssertTemplateResultAsync(string expected, string template, object anonymousObject, INamingConvention namingConvention, SyntaxCompatibility syntax = SyntaxCompatibility.DotLiquid20)
        {
            await LockTemplateStaticVarsAsync(namingConvention, async () =>
            {
                var localVariables = anonymousObject == null ? null : Hash.FromAnonymousObject(anonymousObject);
                var parameters = new RenderParameters(System.Globalization.CultureInfo.CurrentCulture)
                {
                    LocalVariables = localVariables,
                    SyntaxCompatibilityLevel = syntax
                };
                Assert.AreEqual(expected, await Template.Parse(template).RenderAsync(parameters));
            });
        }

        public static Task AssertTemplateResultAsync(string expected, string template, INamingConvention namingConvention)
        {
            return AssertTemplateResultAsync(expected: expected, template: template, anonymousObject: null, namingConvention: namingConvention);
        }

        public static async Task AssertTemplateResultAsync(string expected, string template, Hash localVariables, IEnumerable<Type> localFilters, SyntaxCompatibility syntax = SyntaxCompatibility.DotLiquid20)
        {
            var parameters = new RenderParameters(System.Globalization.CultureInfo.CurrentCulture)
            {
                LocalVariables = localVariables,
                SyntaxCompatibilityLevel = syntax,
                Filters = localFilters
            };
            Assert.AreEqual(expected, await Template.Parse(template).RenderAsync(parameters));
        }

        public static Task AssertTemplateResultAsync(string expected, string template, Hash localVariables, SyntaxCompatibility syntax = SyntaxCompatibility.DotLiquid20)
        {
            return AssertTemplateResultAsync(expected: expected, template: template, localVariables: localVariables, localFilters: null, syntax: syntax);
        }

        public static Task AssertTemplateResultAsync(string expected, string template, SyntaxCompatibility syntax = SyntaxCompatibility.DotLiquid20)
        {
            return AssertTemplateResultAsync(expected: expected, template: template, localVariables: null, syntax: syntax);
        }

        [LiquidTypeAttribute("PropAllowed")]
        public class DataObject
        {
            public string PropAllowed { get; set; }
            public string PropDisallowed { get; set; }
        }

        public class DataObjectDrop : Drop
        {
            public string Prop { get; set; }
        }
    }
}
