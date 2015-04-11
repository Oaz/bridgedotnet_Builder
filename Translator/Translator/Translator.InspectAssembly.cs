﻿using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using ICSharpCode.NRefactory.CSharp;
using System.Linq;
using System;
using Bridge.Contract;

namespace Bridge.Translator
{
    public partial class Translator
    {
        protected virtual AssemblyDefinition LoadAssembly(string location, List<AssemblyDefinition> references)
        {
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(location);
            string name;
            string path;
            AssemblyDefinition reference;
            
            foreach (AssemblyNameReference r in assemblyDefinition.MainModule.AssemblyReferences)
            {
                name = r.Name;

                if (r.Name == "mscorlib" || r.Name == "System.Core")
                {
                    continue;
                }

                path = Path.Combine(Path.GetDirectoryName(location), name) + ".dll";
                reference = this.LoadAssembly(path, references);
                
                if (!references.Any(a => a.Name.Name == reference.Name.Name))
                {
                    references.Add(reference);
                }
            }

            return assemblyDefinition;
        }

        protected virtual void ReadTypes(AssemblyDefinition assembly)
        {
            this.AddNestedTypes(assembly.MainModule.Types);
        }

        protected virtual void AddNestedTypes(IEnumerable<TypeDefinition> types)
        {            
            foreach (TypeDefinition type in types)
            {
                if (type.FullName.Contains("<"))
                {
                    continue;
                }

                this.Validator.CheckType(type, this);
                this.TypeDefinitions.Add(BridgeTypes.GetTypeDefinitionKey(type), type);
                string key = BridgeTypes.GetTypeDefinitionKey(type);
                this.BridgeTypes.Add(key, new BridgeType(key) { TypeDefinition = type });

                if (type.HasNestedTypes)
                {
                    Translator.InheritAttributes(type);
                    this.AddNestedTypes(type.NestedTypes);
                }
            }
        }

        /// <summary>
        /// Makes any existing nested types (classes?) inherit the FileName attribute of the specified type.
        /// Does not override a nested type's FileName attribute if present.
        /// </summary>
        /// <param name="type"></param>
        protected static void InheritAttributes(TypeDefinition type)
        {
            var attrList = new List<string> { "FileNameAttribute", "ModuleAttribute" };
            foreach (var attribute in attrList)
            {
                if (type.CustomAttributes.Any(ca => ca.AttributeType.Name == attribute))
                {
                    var FAt = type.CustomAttributes.First(ca => ca.AttributeType.Name == attribute);
                    foreach (var nestedType in type.NestedTypes)
                    {
                        if (!nestedType.CustomAttributes.Any(ca => ca.AttributeType.Name == attribute))
                        {
                            nestedType.CustomAttributes.Add(FAt);
                        }
                    }
                }
            }
        }

        protected virtual List<AssemblyDefinition> InspectReferences()
        {
            this.TypeInfoDefinitions = new Dictionary<string, ITypeInfo>();

            var references = new List<AssemblyDefinition>();            
            var assembly = this.LoadAssembly(this.AssemblyLocation, references);
            this.TypeDefinitions = new Dictionary<string, TypeDefinition>();
            this.BridgeTypes = new BridgeTypes();
            this.AssemblyDefinition = assembly;
            
            if (assembly.Name.Name != Translator.Bridge_ASSEMBLY)
            {
                this.ReadTypes(assembly);
            }

            foreach (var item in references)
            {
                this.ReadTypes(item);
            }

            var prefix = Path.GetDirectoryName(this.Location);

            for (int i = 0; i < this.SourceFiles.Count; i++)
            {
                this.SourceFiles[i] = Path.Combine(prefix, this.SourceFiles[i]);
            }

            return references;
        }

        protected virtual void InspectTypes(MemberResolver resolver, IAssemblyInfo config)
        {
            
            Inspector inspector = this.CreateInspector();
            inspector.AssemblyInfo = config;
            inspector.Resolver = resolver;

            for (int i = 0; i < this.SourceFiles.Count; i++)
            {
                inspector.VisitSyntaxTree(this.GetSyntaxTree(this.SourceFiles[i]));
            }

            this.AssemblyInfo = inspector.AssemblyInfo;
            this.Types = inspector.Types;
        }

        protected virtual Inspector CreateInspector()
        {
            return new Inspector();
        }

        protected virtual SyntaxTree GetSyntaxTree(string fileName)
        {
            using (var reader = new StreamReader(fileName))
            {
                var parser = new ICSharpCode.NRefactory.CSharp.CSharpParser();
                var syntaxTree = parser.Parse(reader, fileName);

                if (parser.HasErrors)
                {
                    foreach (var error in parser.Errors)
                    {
                        Bridge.Translator.Exception.Throw("Parsing error in a file {0} {2}: {1}", fileName, error.Message, error.Region.Begin.ToString());
                    }                    
                }
                
                return syntaxTree;
            }
        }
    }
}
