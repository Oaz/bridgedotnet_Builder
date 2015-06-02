using Bridge.Contract;
using Microsoft.Ajax.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Bridge.Translator
{
    public partial class Translator : ITranslator
    {
        public const string Bridge_ASSEMBLY = "Bridge";

        public Translator(string location)
        {
            this.Location = location;
            this.Validator = this.CreateValidator();
        }

        public Translator(string folder, string source, bool recursive, string lib)
        {
            this.Recursive = recursive;
            this.Source = source;
            this.FolderMode = true;
            this.Location = folder;
            this.AssemblyLocation = lib;
            this.Validator = this.CreateValidator();
        }

        public Dictionary<string, string> Translate()
        {
            var config = this.ReadConfig();
            this.Plugins = Bridge.Translator.Plugins.GetPlugins(this, config);
            this.Plugins.OnConfigRead(config);

            if (config != null && !string.IsNullOrWhiteSpace(config.BeforeBuild))
            {
                try
                {
                    this.RunEvent(config.BeforeBuild);
                }
                catch (Exception exc)
                {
                    throw new Bridge.Translator.Exception("Error: Unable to run beforeBuild event command: " +
                        exc.Message + "\nStack trace:\n" + exc.StackTrace);
                }
            }

            if (this.FolderMode)
            {
                this.ReadFolderFiles();
            }
            else
            {
                this.ReadProjectFile();

                if (this.Rebuild || !File.Exists(this.AssemblyLocation))
                {
                    this.BuildAssembly();
                }
            }

            var references = this.InspectReferences();

            this.BuildSyntaxTree();
            var resolver = new MemberResolver(this.ParsedSourceFiles, Emitter.ToAssemblyReferences(references));

            this.InspectTypes(resolver, config);

            resolver.CanFreeze = true;
            var emitter = this.CreateEmitter(resolver);
            emitter.Translator = this;
            emitter.AssemblyInfo = this.AssemblyInfo;
            emitter.ChangeCase = this.ChangeCase;
            emitter.References = references;
            emitter.SourceFiles = this.SourceFiles;
            emitter.Log = this.Log;
            emitter.Plugins = this.Plugins;
            this.Plugins.BeforeEmit(emitter, this);
            this.Outputs = emitter.Emit();
            this.Plugins.AfterEmit(emitter, this);

            return this.Outputs;
        }

        public virtual string GetCode()
        {
            StringBuilder builder = new StringBuilder();

            foreach (var item in this.Outputs)
            {
                string code = item.Value;
                builder.AppendLine(code);
            }

            return builder.ToString();
        }

        public virtual void SaveTo(string path, string defaultFileName)
        {
            var minifier = new Minifier();
            foreach (var item in this.Outputs)
            {
                string fileName = item.Key;
                string code = item.Value;

                if (fileName.Contains(Bridge.Translator.AssemblyInfo.DEFAULT_FILENAME))
                {
                    fileName = fileName.Replace(Bridge.Translator.AssemblyInfo.DEFAULT_FILENAME, defaultFileName);
                }

                // Ensure filename contains no ":". It could be used like "c:/absolute/path"
                fileName = fileName.Replace(":", "_");

                // Trim heading slash/backslash off file names until it does not start with slash.
                var oldFNlen = fileName.Length;
                while (Path.IsPathRooted(fileName))
                {
                    fileName = fileName.TrimStart(Path.DirectorySeparatorChar, '/', '\\');

                    // Trimming didn't change the path. This way, it will just loop indefinitely.
                    // Also, this means the absolute path specifies a fully-qualified DOS PathName with drive letter.
                    if (fileName.Length == oldFNlen)
                    {
                        break;
                    }
                    oldFNlen = fileName.Length;
                }

                // If 'fileName' is an absolute path, Path.Combine will ignore the 'path' prefix.
                string filePath = Path.Combine(path, fileName);

                System.IO.FileInfo file;

                // We can only have Beautified, Minified or Both, so this test has inverted logic:
                // output beautified if not minified only == (output beautified or output both)
                if (this.AssemblyInfo.OutputFormatting != JavaScriptOutputType.Minified)
                {
                    file = new System.IO.FileInfo(filePath);
                    file.Directory.Create();
                    File.WriteAllText(file.FullName, "/* global Bridge */\n\n" + code, System.Text.UTF8Encoding.UTF8);
                }

                // Like above test: output minified if not beautified only == (out minified or out both)
                if (this.AssemblyInfo.OutputFormatting != JavaScriptOutputType.Formatted)
                {
                    fileName = Path.GetFileNameWithoutExtension(filePath) + ".min" + Path.GetExtension(filePath);
                    filePath = Path.Combine(Path.GetDirectoryName(filePath), fileName);
                    file = new System.IO.FileInfo(filePath);
                    file.Directory.Create();
                    File.WriteAllText(file.FullName, minifier.MinifyJavaScript(code), System.Text.UTF8Encoding.UTF8);
                }
            }

            if (!string.IsNullOrWhiteSpace(this.AssemblyInfo.AfterBuild))
            {
                try
                {
                    this.RunEvent(this.AssemblyInfo.AfterBuild);
                }
                catch (Exception exc)
                {
                    throw new Bridge.Translator.Exception("Error: Unable to run afterBuild event command: " +
                        exc.Message + "\nStack trace:\n" + exc.StackTrace);
                }
            }
        }

        protected virtual Emitter CreateEmitter(IMemberResolver resolver)
        {
            return new Emitter(this.TypeDefinitions, this.BridgeTypes, this.Types, this.Validator, resolver, this.TypeInfoDefinitions);
        }

        protected virtual Validator CreateValidator()
        {
            return new Validator();
        }

        public static void ExtractCore(Translator translatorInstance, string outputPath, bool nodebug = false)
        {
            var clrPath = translatorInstance.BridgeLocation;
            var assembly = System.Reflection.Assembly.ReflectionOnlyLoadFrom(clrPath);
            string resourceName;

            // We can only have Beautified, Minified or Both, so this test has inverted logic:
            // output beautified if not minified only == (output beautified or output both)
            if (translatorInstance.AssemblyInfo.OutputFormatting != JavaScriptOutputType.Minified)
            {
                resourceName = "Bridge.Resources.bridge.js";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        File.WriteAllText(Path.Combine(outputPath, "bridge.js"), reader.ReadToEnd());
                    }
                }
            }

            // Like above test: output minified if not beautified only == (out minified or out both)
            if (translatorInstance.AssemblyInfo.OutputFormatting != JavaScriptOutputType.Formatted)
            {
                if (!nodebug)
                {
                    resourceName = "Bridge.Resources.bridge.min.js";

                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            File.WriteAllText(Path.Combine(outputPath, "bridge.min.js"), reader.ReadToEnd());
                        }
                    }
                }
            }
        }

        public EmitterException CreateExceptionFromLastNode()
        {
            return this.EmitNode != null ? new EmitterException(this.EmitNode) : null;
        }
    }
}
