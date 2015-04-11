﻿using System;
using System.Diagnostics;
using Bridge.Contract;
using System.IO;
using Newtonsoft.Json;

namespace Bridge.Translator
{
    public partial class Translator
    {
        protected virtual IAssemblyInfo ReadConfig()
        {
            var path = Path.GetDirectoryName(this.Location) + "\\bridge\\bridge.json";

            if (!File.Exists(path))
            {
                path = Path.GetDirectoryName(this.Location) + "\\bridge.json";
            }

            if (!File.Exists(path))
            {
                path = Path.GetDirectoryName(this.Location) + "\\Bridge.NET\\bridge.json";
            }

            if (!File.Exists(path))
            {
                var config = new AssemblyInfo();
                return config;
            }

            try
            {
                var json = File.ReadAllText(path);
                IAssemblyInfo assemblyInfo = JsonConvert.DeserializeObject<AssemblyInfo>(json);

                if (assemblyInfo == null)
                {
                    assemblyInfo = new AssemblyInfo();
                }
                
                return assemblyInfo;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Cannot read bridge.json", e);
            }

        }

        public virtual void RunEvent(string e)
        {
            var info = new ProcessStartInfo()
            {
                FileName = e
            };
            info.WindowStyle = ProcessWindowStyle.Hidden;
            using (var p = Process.Start(info))
            {
                p.WaitForExit();

                if (p.ExitCode != 0)
                {
                    Bridge.Translator.Exception.Throw("Event (" + e + ") was not successful, exit code - " + p.ExitCode);
                }
            }
        }
    }
}
