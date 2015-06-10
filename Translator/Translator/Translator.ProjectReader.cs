using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Bridge.Translator
{
    public partial class Translator
    {
        protected virtual void ReadProjectFile()
        {
            var doc = XDocument.Load(Location, LoadOptions.SetLineInfo);

            ValidateProject(doc);

            this.BuildAssemblyLocation(doc);
            this.SourceFiles = this.GetSourceFiles(doc);
            this.ParsedSourceFiles = new List<ParsedSourceFile>();
        }

        protected virtual void ReadFolderFiles()
        {
            this.SourceFiles = this.GetSourceFiles();
            this.ParsedSourceFiles = new List<ParsedSourceFile>();
        }

        /// <summary>
        /// Validates project and namespace names against conflicts with Bridge.NET namespaces.
        /// </summary>
        /// <param name="doc">XDocument reference of the .csproj file.</param>
        private void ValidateProject(XDocument doc)
        {
            var valid = true;
            var failList = new HashSet<string>();
            var failNodeList = new List<XElement>();
            var combined_tags = from x in doc.Descendants()
                                where x.Name.LocalName == "RootNamespace" || x.Name.LocalName == "AssemblyName"
                                select x;

            foreach (var tag in combined_tags)
            {
                if (tag.Value == "Bridge")
                {
                    valid = false;
                    if (!failList.Contains(tag.Value))
                    {
                        failList.Add(tag.Value);
                        failNodeList.Add(tag);
                    }
                }
            }

            if (!valid)
            {
                var offendingSettings = "";
                foreach (var tag in failNodeList)
                {
                    offendingSettings += "Line " + ((IXmlLineInfo)tag).LineNumber + ": <" + tag.Name.LocalName + ">" +
                        tag.Value + "</" + tag.Name.LocalName + ">\n";
                }

                throw new Bridge.Translator.Exception("'Bridge' name is reserved and may not " +
                    "be used as project names or root namespaces.\n" +
                    "Please verify your project settings and rename where it applies.\n" +
                    "Project file: " + this.Location + "\n" +
                    "Offending settings:\n" + offendingSettings
                );
            }
        }

        protected virtual void BuildAssemblyLocation(XDocument doc)
        {
            if (this.AssemblyLocation == null || this.AssemblyLocation.Length == 0)
            {
                this.Configuration = this.Configuration ?? "Debug";
                var outputPath = this.GetOutputPath(doc, this.Configuration);

                if (string.IsNullOrEmpty(outputPath))
                {
                    outputPath = this.GetOutputPath(doc, "Release");
                }

                this.AssemblyLocation = Path.Combine(outputPath, this.GetAssemblyName(doc) + ".dll");

                if (!File.Exists(this.AssemblyLocation))
                {
                    outputPath = this.GetOutputPath(doc, "Release");
                    this.AssemblyLocation = Path.Combine(outputPath, this.GetAssemblyName(doc) + ".dll");
                }
            }
        }

        protected virtual string GetOutputPath(XDocument doc, string configuration)
        {
            var opnodes = from n in doc.Descendants() where n.Name.LocalName == "OutputPath" select n;
            var nodes = from n in doc.Descendants()
                        where n.Name.LocalName == "OutputPath" &&
                              n.Parent.Attribute("Condition").Value.Contains(configuration)
                        select n;

            if (nodes.Count() != 1)
            {
                Bridge.Translator.Exception.Throw("Unable to determine output path");
            }

            var path = nodes.First().Value;

            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Location), path));
            }

            return path;
        }

        protected virtual IList<string> GetSourceFiles(XDocument doc)
        {
            var result = new List<string>();

            var nodeList = from n in doc.Descendants()
                           where n.Name.LocalName == "Compile" &&
                                 !string.IsNullOrWhiteSpace(n.Attribute("Include").Value)
                           select n;

            foreach (var node in nodeList)
            {
                result.Add(node.Attribute("Include").Value);
            }

            return result;
        }

        protected virtual string GetAssemblyName(XDocument doc)
        {
            var nodes = from n in doc.Descendants()
                        where n.Name.LocalName == "AssemblyName"
                        select n;

            if (nodes.Count() != 1)
            {
                Bridge.Translator.Exception.Throw("Unable to determine assembly name");
            }

            return nodes.First().Value;
        }

        protected virtual IList<string> GetSourceFiles()
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(this.Source))
            {
                this.Source = "*.cs";
            }

            string[] parts = this.Source.Split(';');
            var searchOption = this.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            foreach (var part in parts)
            {
                int index = part.LastIndexOf(System.IO.Path.DirectorySeparatorChar);
                string folder = index > -1 ? Path.Combine(this.Location, part.Substring(0, index + 1)) : this.Location;
                string mask = index > -1 ? part.Substring(index + 1) : part;

                string[] allfiles = System.IO.Directory.GetFiles(folder, mask, searchOption);
                result.AddRange(allfiles);
            }

            result = result.Distinct().ToList();
            return result;
        }
    }
}
