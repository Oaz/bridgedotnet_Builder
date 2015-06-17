using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using Bridge.Contract;

namespace Bridge.Build
{
    public class GenerateScript : Task
    {
        [Required]
        public ITaskItem Assembly
        {
            get;
            set;
        }

        [Required]
        public string OutputPath
        {
            get;
            set;
        }

        [Required]
        public string ProjectPath
        {
            get;
            set;
        }

        [Required]
        public string AssembliesPath
        {
            get;
            set;
        }

        private bool changeCase = true;

        public bool ChangeCase
        {
            get
            {
                return this.changeCase;
            }
            set
            {
                this.changeCase = value;
            }
        }

        public bool NoCore
        {
            get;
            set;
        }

        public string Configuration
        {
            get;
            set;
        }

#if DEBUG
        /// <summary>
        /// Attaches the process to a debugger once a build event is triggered. If false/absent, does nothing.
        /// This option is similar to Builder.exe's '-d'
        /// </summary>
        public bool AttachDebugger
        {
            get;
            set;
        }
#endif

        protected virtual void LogMessage(string level, string message)
        {
            level = level ?? "message";

            switch(level.ToLowerInvariant())
            {
                case "message":
                    this.Log.LogMessage(message);
                    break;
                case "warning":
                    this.Log.LogWarning(message);
                    break;
                case "error":
                    this.Log.LogError(message);
                    break;
            }
        }

        public override bool Execute()
        {
            var success = true;

#if DEBUG
            if (AttachDebugger)
            {
                System.Diagnostics.Debugger.Launch();
            };
#endif

            Bridge.Translator.Translator translator = null;
            try
            {
                translator = new Bridge.Translator.Translator(this.ProjectPath);
                translator.Configuration = this.Configuration;
                translator.BridgeLocation = Path.Combine(this.AssembliesPath, "Bridge.dll");
                translator.Rebuild = false;
                translator.ChangeCase = this.ChangeCase;
                translator.Log = this.LogMessage;
                translator.Translate();

                string fileName = Path.GetFileNameWithoutExtension(this.Assembly.ItemSpec);

                string outputPath = !string.IsNullOrWhiteSpace(translator.AssemblyInfo.Output)
                    ? Path.Combine(Path.GetDirectoryName(this.ProjectPath), translator.AssemblyInfo.Output)
                    : this.OutputPath;

                translator.SaveTo(outputPath, fileName);

                if (!this.NoCore)
                {
                    Bridge.Translator.Translator.ExtractCore(translator, outputPath);
                }
            }
            catch (EmitterException e)
            {
                this.Log.LogError(null, null, null, e.FileName, e.StartLine + 1, e.StartColumn + 1, e.EndLine + 1, e.EndColumn + 1, "Error: {0} {1}", e.Message, e.StackTrace);
                success = false;
            }
            catch (Exception e)
            {
                var ee = translator != null ? translator.CreateExceptionFromLastNode() : null;

                if (ee != null)
                {
                    this.Log.LogError(null, null, null, ee.FileName, ee.StartLine + 1, ee.StartColumn + 1, ee.EndLine + 1, ee.EndColumn + 1, "Error: {0} {1}", e.Message, e.StackTrace);
                }
                else
                {
                    this.Log.LogError("Error: {0} {1}", e.Message, e.StackTrace);
                }

                success = false;
            }

            return success;
        }
    }
}
