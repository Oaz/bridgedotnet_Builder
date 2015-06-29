using Bridge.Contract;
using Object.Net.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

namespace Bridge.Translator.TypeScript
{
    public class EmitBlock : AbstractEmitterBlock
    {
        private Dictionary<string, StringBuilder> Outputs
        {
            get;
            set;
        }

        private string ns = null;
        
        public EmitBlock(IEmitter emitter) : base(emitter, null)
        {
            this.Emitter = emitter;            
        }

        protected virtual StringBuilder GetOutputForType(ITypeInfo typeInfo)
        {
            var fileName = this.GetNamespaceFilename(typeInfo);

            if (ns != null && ns != fileName)
            {
                this.EndBlock();
                this.WriteNewLine();                
            }

            ns = fileName;

            StringBuilder output = null;

            if (this.Outputs.ContainsKey(fileName))
            {
                output = this.Outputs[fileName];
            }
            else
            {                
                output = new StringBuilder();
                this.Emitter.Output = output;
                output.AppendLine(@"/// <reference path=""./bridge.d.ts"" />");
                output.AppendLine();
                output.AppendLine("declare module " + fileName + " ");
                this.BeginBlock();
                this.Outputs.Add(fileName, output);
            }

            return output;
        }

        private string GetNamespaceFilename(ITypeInfo typeInfo)
        {
            var cas = this.Emitter.BridgeTypes.Get(typeInfo.Key).TypeDefinition.CustomAttributes;

            // Search for an 'NamespaceAttribute' entry
            foreach (var ca in cas)
            {
                if (ca.AttributeType.Name == "NamespaceAttribute" &&
                    ca.ConstructorArguments.Count > 0 &&
                    ca.ConstructorArguments[0].Value is string &&
                    !string.IsNullOrWhiteSpace(ca.ConstructorArguments[0].Value.ToString()))
                {
                    return ca.ConstructorArguments[0].Value.ToString();
                }
            }

            return typeInfo.Namespace;
        }

        private void TransformOutputs()
        {
            if (this.Emitter.AssemblyInfo.OutputBy == OutputBy.Project)
            {
                var fileName = Path.GetFileNameWithoutExtension(this.Emitter.Outputs.First().Key) + ".d.ts";
                var e = new EmitterOutput(fileName);

                foreach (var item in this.Outputs)
                {
                    e.NonModuletOutput.AppendLine(item.Value.ToString());                    
                }

                this.Emitter.Outputs.Add(fileName, e);
            }
            else
            {
                foreach (var item in this.Outputs)
                {
                    var fileName = item.Key + ".d.ts";
                    var e = new EmitterOutput(fileName);
                    e.NonModuletOutput = item.Value;
                    this.Emitter.Outputs.Add(fileName, e);
                }
            }            
        }
        
        protected override void DoEmit()
        {
            this.Emitter.Writers = new Stack<Tuple<string, StringBuilder, bool, Action>>();
            this.Outputs = new Dictionary<string, StringBuilder>();

            var types = this.Emitter.Types.ToArray();
            Array.Sort(types, (t1, t2) => this.GetNamespaceFilename(t1).CompareTo(this.GetNamespaceFilename(t2)));
            this.Emitter.InitEmitter();

            foreach (var type in types)
            {
                this.Emitter.Translator.EmitNode = type.TypeDeclaration;
                
                if (type.IsObjectLiteral)
                {
                    continue;
                }                

                ITypeInfo typeInfo;

                if (this.Emitter.TypeInfoDefinitions.ContainsKey(type.Key))
                {
                    typeInfo = this.Emitter.TypeInfoDefinitions[type.Key];

                    type.Module = typeInfo.Module;
                    type.FileName = typeInfo.FileName;
                    type.Dependencies = type.Dependencies;
                    typeInfo = type;
                }
                else
                {
                    typeInfo = type;
                }

                this.Emitter.TypeInfo = type;
                this.Emitter.Output = this.GetOutputForType(typeInfo);                

                new ClassBlock(this.Emitter, this.Emitter.TypeInfo).Emit();
                this.WriteNewLine();
                this.WriteNewLine();
            }

            this.EndBlock();
            this.TransformOutputs();
        }

        private static System.Collections.Generic.Dictionary<string, string> replacements;
        private static Regex decodeRegex;

        public static string HandleType(string name)
        {
            if (name == "Bridge.Int")
            {
                //it is hack, need to find good solution
                return "Number";
            }

            if (decodeRegex == null)
            {
                replacements = new System.Collections.Generic.Dictionary<string, string>(4);
                replacements.Add("\\(", "<");
                replacements.Add("\\)", ">");
                replacements.Add("Bridge.Int", "Number");

                decodeRegex = new Regex("(" + String.Join("|", replacements.Keys.ToArray()) + ")", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);
            }

            return decodeRegex.Replace
                (
                    name,
                    delegate(Match m) {
                        return replacements.ContainsKey(m.Value) ? replacements[m.Value] : replacements["\\" + m.Value]; 
                    }
                );  
        }
    }
}
