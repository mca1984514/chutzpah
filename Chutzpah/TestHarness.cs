﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Chutzpah.Models;

namespace Chutzpah
{
    public class TestHarness
    {
        private readonly string inputTestFilePath;

        public IList<TestHarnessItem> TestFrameworkDependencies { get; private set; }
        public IList<TestHarnessItem> ReferencedScripts { get; private set; }
        public IList<TestHarnessItem> ReferencedStyles { get; private set; }

        public TestHarness(string inputTestFilePath,
                             IEnumerable<ReferencedFile> referencedFiles)
        {
            this.inputTestFilePath = inputTestFilePath;
            BuildTags(referencedFiles);
        }

        public string CreateHtmlText(string testHtmlTemplate)
        {
            string inputTestFileDir = Path.GetDirectoryName(inputTestFilePath).Replace("\\", "/");
            string testHtmlText = FillTestHtmlTemplate(testHtmlTemplate, inputTestFileDir);
            return testHtmlText;
        }

        private void BuildTags(IEnumerable<ReferencedFile> referencedFilePaths)
        {
            ReferencedScripts = new List<TestHarnessItem>();
            ReferencedStyles = new List<TestHarnessItem>();
            TestFrameworkDependencies = new List<TestHarnessItem>();

            foreach (ReferencedFile referencedFile in referencedFilePaths)
            {
                string referencePath = string.IsNullOrEmpty(referencedFile.GeneratedFilePath)
                                        ? referencedFile.Path
                                        : referencedFile.GeneratedFilePath;
                IList<TestHarnessItem> refList = ChooseRefList(referencedFile, referencePath);
                if (refList == null) continue;

                if (referencePath.EndsWith(Constants.CssExtension, StringComparison.OrdinalIgnoreCase))
                {
                    refList.Add(new ExternalStylesheet(referencedFile));
                }
                else if (referencePath.EndsWith(Constants.PngExtension, StringComparison.OrdinalIgnoreCase))
                {
                    refList.Add(new ShortcutIcon(referencedFile));
                }
                else if (referencePath.EndsWith(Constants.JavaScriptExtension, StringComparison.OrdinalIgnoreCase))
                {
                    refList.Add(new Script(referencedFile));
                }
            }
        }

        private IList<TestHarnessItem> ChooseRefList(ReferencedFile referencedFile, string referencePath)
        {
            IList<TestHarnessItem> list = null;
            if (referencedFile.IsTestFrameworkDependency)
            {
                list = TestFrameworkDependencies;
            }
            else if (referencePath.EndsWith(Constants.CssExtension, StringComparison.OrdinalIgnoreCase))
            {
                list = ReferencedStyles;
            }
            else if (referencePath.EndsWith(Constants.JavaScriptExtension, StringComparison.OrdinalIgnoreCase))
            {
                list = ReferencedScripts;
            }
            return list;
        }

        private string FillTestHtmlTemplate(string testHtmlTemplate,
                                            string inputTestFileDir)
        {
            var testJsReplacement = new StringBuilder();
            var testFrameworkDependencies = new StringBuilder(); 
            var referenceJsReplacement = new StringBuilder();
            var referenceCssReplacement = new StringBuilder();

            BuildReferenceHtml(testFrameworkDependencies,
                               referenceCssReplacement,
                               testJsReplacement,
                               referenceJsReplacement);

            testHtmlTemplate = testHtmlTemplate.Replace("@@TestFrameworkDependencies@@", testFrameworkDependencies.ToString());
            testHtmlTemplate = testHtmlTemplate.Replace("@@TestJSFile@@", testJsReplacement.ToString());
            testHtmlTemplate = testHtmlTemplate.Replace("@@TestJSFileDir@@", inputTestFileDir);
            testHtmlTemplate = testHtmlTemplate.Replace("@@ReferencedJSFiles@@", referenceJsReplacement.ToString());
            testHtmlTemplate = testHtmlTemplate.Replace("@@ReferencedCSSFiles@@", referenceCssReplacement.ToString());

            return testHtmlTemplate;
        }

        private void BuildReferenceHtml(StringBuilder testFrameworkDependencies,
                                        StringBuilder referenceCssReplacement,
                                        StringBuilder testJsReplacement,
                                        StringBuilder referenceJsReplacement)
        {
            foreach (TestHarnessItem item in TestFrameworkDependencies)
            {
                testFrameworkDependencies.AppendLine(item.ToString());
            }
            foreach (TestHarnessItem item in ReferencedScripts)
            {
                if (item.ReferencedFile != null && item.ReferencedFile.IsFileUnderTest)
                {
                    testJsReplacement.AppendLine(item.ToString());
                }
                else
                {
                    referenceJsReplacement.AppendLine(item.ToString());
                }
            }
            foreach (TestHarnessItem item in ReferencedStyles)
            {
                referenceCssReplacement.AppendLine(item.ToString());
            }
        }
    }

    public class TestHarnessItem
    {
        private readonly bool explicitEndTag;
        private readonly string contents;
        private readonly string tagName;

        public IDictionary<string, string> Attributes { get; private set; }
        public ReferencedFile ReferencedFile { get; private set; }
        public bool HasFile { get { return ReferencedFile != null; } }

        internal TestHarnessItem(ReferencedFile referencedFile, string tagName, bool explicitEndTag)
            : this(tagName, explicitEndTag)
        {
            ReferencedFile = referencedFile;
        }

        internal TestHarnessItem(string contents, string tagName, bool explicitEndTag)
            : this(tagName, explicitEndTag)
        {
            this.contents = contents;
        }

        private TestHarnessItem(string tagName, bool explicitEndTag)
        {
            this.tagName = tagName;
            this.explicitEndTag = explicitEndTag;
            Attributes = new Dictionary<string, string>();
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder("<");
            builder.Append(tagName);
            foreach (var entry in Attributes)
            {
                builder.AppendFormat(@" {0}=""{1}""", entry.Key, entry.Value);
            }
            if (explicitEndTag || contents != null)
            {
                builder.AppendFormat(">{1}</{0}>", tagName, contents ?? "");
            }
            else
            {
                builder.Append("/>");
            }
            return builder.ToString();
        }

        protected static string GetAbsoluteFileUrl(ReferencedFile referencedFile)
        {
            string referencePath = string.IsNullOrEmpty(referencedFile.GeneratedFilePath)
                        ? referencedFile.Path
                        : referencedFile.GeneratedFilePath;

            if (!RegexPatterns.SchemePrefixRegex.IsMatch(referencePath))
            {
                return "file:///" + referencePath.Replace('\\', '/');
            }

            return referencePath;
        }

    }

    public class ExternalStylesheet : TestHarnessItem
    {
        public ExternalStylesheet(ReferencedFile referencedFile) : base(referencedFile, "link", false)
        {
            Attributes.Add("rel", "stylesheet");
            Attributes.Add("type", "text/css");
            Attributes.Add("href", GetAbsoluteFileUrl(referencedFile));
        }
    }

    public class ShortcutIcon : TestHarnessItem
    {
        public ShortcutIcon(ReferencedFile referencedFile) : base(referencedFile, "link", false)
        {
            Attributes.Add("rel", "shortcut icon");
            Attributes.Add("type", "image/png");
            Attributes.Add("href", GetAbsoluteFileUrl(referencedFile));
        }
    }

    public class Script : TestHarnessItem
    {
        public Script(ReferencedFile referencedFile)
            : base(referencedFile, "script", true)
        {
            Attributes.Add("type", "text/javascript");
            Attributes.Add("src", GetAbsoluteFileUrl(referencedFile));
        }

        public Script(string scriptCode)
            : base(scriptCode, "script", true)
        {
            Attributes.Add("type", "text/javascript");
        }
    }

}
