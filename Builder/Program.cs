﻿using System;
using System.IO;
using System.Reflection;
using Bridge.Contract;

namespace Bridge.Builder
{
    class Program
    {
        static void LogMessage(string level, string message)
        {
            level = level ?? "message";
            switch (level.ToLowerInvariant())
            {
                case "message":
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine("Message: {0}", message);
                    Console.ResetColor();
                    break;
                case "warning":
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("Warning: {0}", message);
                    Console.ResetColor();
                    break;
                case "error":
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: {0}", message);
                    Console.ResetColor();
                    break;
            }
        }

        static void Main(string[] args)
        {
            string projectLocation = null;
            string outputLocation = null;
            string bridgeLocation = null;
            bool rebuild = false;
            bool extractCore = true;
            bool changeCase = true;
            string cfg = null;

            if (args.Length == 0)
            {
                Console.WriteLine("Bridge.Builder commands:");
                Console.WriteLine("-p or -project           Path to csproj file (required)");
                Console.WriteLine("-o or -output            Output directory for generated script");
                Console.WriteLine("-cfg or -configuration   Configuration name, typically Debug/Release");
                Console.WriteLine("-r or -rebuild           Force assembly rebuilding");
                Console.WriteLine("-nocore                  Do not extract core javascript files");
                Console.WriteLine("-c or -case              Do not change case of members");
#if DEBUG
                // This code and logic is only compiled in when building bridge.net in Debug configuration
                Console.WriteLine("-d or -debug             Attach the builder to an visual studio debugging instance.");
                Console.WriteLine("                         Use this to attach the process to an open Bridge.NET solution.");
#endif
                Console.WriteLine("");
                return;
            }

            int i = 0;

            while (i < args.Length)
            {
                switch (args[i])
                {
                    case "-p":
                    case "-project":
                        projectLocation = args[++i];
                        break;
                    case "-b":
                    case "-bridge":
                        bridgeLocation = args[++i];
                        break;
                    case "-o":
                    case "-output":
                        outputLocation = args[++i];
                        break;
                    case "-cfg":
                    case "-configuration":
                        cfg = args[++i];
                        break;
                    case "-rebuild":
                    case "-r":
                        rebuild = true;                        
                        break;
                    case "-case":
                    case "-c":
                        changeCase = false;
                        break;                   
                    case "-nocore":
                        extractCore = false;
                        break;
#if DEBUG
                    case "-debug":
                    case "-d":
                        System.Diagnostics.Debugger.Launch();
                        break;
#endif
                    default:
                        Console.WriteLine("Unknown command: " + args[i]);
                        return;
                }

                i++;
            }

            if (string.IsNullOrEmpty(projectLocation))
            {
                Console.WriteLine("Project location is empty, please define argument with -p command");
                return;
            }

            if (!File.Exists(projectLocation))
            {
                Console.WriteLine("Project file doesn't exist: " + projectLocation);
                return;
            }

            if (string.IsNullOrEmpty(outputLocation))
            {
                outputLocation = Path.GetFileNameWithoutExtension(projectLocation);
            }

            Bridge.Translator.Translator translator = null;
            try
            {
                Console.WriteLine("Generating script...");
                translator = new Bridge.Translator.Translator(projectLocation);

                bridgeLocation = !string.IsNullOrEmpty(bridgeLocation) ? bridgeLocation : Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Bridge.dll"); 

                translator.BridgeLocation = bridgeLocation;
                translator.Rebuild = rebuild;
                translator.ChangeCase = changeCase;
                translator.Log = LogMessage;
                translator.Configuration = cfg;
                translator.Translate();

                string path = string.IsNullOrWhiteSpace(Path.GetFileName(outputLocation)) ? outputLocation : Path.GetDirectoryName(outputLocation);
                string outputPath = Path.Combine(Path.GetDirectoryName(projectLocation), !string.IsNullOrWhiteSpace(translator.AssemblyInfo.Output) ? translator.AssemblyInfo.Output : path);

                translator.SaveTo(outputPath, Path.GetFileName(outputLocation));

                if (extractCore)
                {
                    Console.WriteLine("Extracting core scripts...");
                    Bridge.Translator.Translator.ExtractCore(translator.BridgeLocation, outputPath);
                }

                Console.WriteLine("Done.");

                if (!string.IsNullOrWhiteSpace(translator.AssemblyInfo.AfterBuild))
                {
                    translator.RunEvent(translator.AssemblyInfo.AfterBuild);
                }
            }
            catch (EmitterException e)
            {
                Console.WriteLine("{2} ({3}, {4}) Error: {0} {1}", e.Message, e.StackTrace, e.FileName, e.StartLine + 1, e.StartColumn + 1, e.EndLine + 1, e.EndColumn + 1);
            }
            catch (Exception e)
            {
                var ee = translator != null ? translator.CreateExceptionFromLastNode() : null;
                Console.ForegroundColor = ConsoleColor.Red;

                if (ee != null)
                {
                    Console.WriteLine("{2} ({3}, {4}) Error: {0} {1}", e.Message, e.StackTrace, ee.FileName, ee.StartLine + 1, ee.StartColumn + 1, ee.EndLine + 1, ee.EndColumn + 1);
                }
                else
                {
                    Console.WriteLine("Error: {0} {1}", e.Message, e.StackTrace);
                }

                Console.ResetColor();
                Console.ReadLine();
            }
        }
    }
}
