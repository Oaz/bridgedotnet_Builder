﻿using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using Object.Net.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bridge.Translator.TypeScript
{
    public class MethodsBlock : AbstractEmitterBlock
    {
        public MethodsBlock(IEmitter emitter, ITypeInfo typeInfo, bool staticBlock)
            : base(emitter, typeInfo.TypeDeclaration)
        {
            this.Emitter = emitter;
            this.TypeInfo = typeInfo;
            this.StaticBlock = staticBlock;
        }

        public ITypeInfo TypeInfo
        {
            get;
            set;
        }

        public bool StaticBlock
        {
            get;
            set;
        }

        protected override void DoEmit()
        {
            if (this.StaticBlock)
            {
                this.EmitMethods(this.TypeInfo.StaticMethods, this.TypeInfo.StaticProperties, this.TypeInfo.Operators);
            }
            else
            {
                this.EmitMethods(this.TypeInfo.InstanceMethods, this.TypeInfo.InstanceProperties, null);
            }
        }

        protected virtual void EmitMethods(Dictionary<string, List<MethodDeclaration>> methods, Dictionary<string, List<EntityDeclaration>> properties, Dictionary<OperatorType, List<OperatorDeclaration>> operators)
        {
            var names = new List<string>(properties.Keys);

            foreach (var name in names)
            {
                var props = properties[name];

                foreach (var prop in props)
                {
                    if (prop is PropertyDeclaration)
                    {
                        new PropertyBlock(this.Emitter, (PropertyDeclaration)prop).Emit();
                    }
                    else if (prop is CustomEventDeclaration)
                    {
                        new CustomEventBlock(this.Emitter, (CustomEventDeclaration)prop).Emit();
                    }
                    else if (prop is IndexerDeclaration)
                    {
                        new IndexerBlock(this.Emitter, (IndexerDeclaration)prop).Emit();
                    }
                }
            }

            names = new List<string>(methods.Keys);

            foreach (var name in names)
            {
                var group = methods[name];

                foreach (var method in group)
                {
                    if (!method.Body.IsNull || this.Emitter.TypeInfo.TypeDeclaration.ClassType == ClassType.Interface)
                    {
                        new MethodBlock(this.Emitter, method).Emit();
                    }
                }
            }

            if (operators != null)
            {
                var ops = new List<OperatorType>(operators.Keys);

                foreach (var op in ops)
                {
                    var group = operators[op];

                    foreach (var o in group)
                    {
                        if (!o.Body.IsNull && this.Emitter.TypeInfo.TypeDeclaration.ClassType != ClassType.Interface)
                        {
                            new OperatorBlock(this.Emitter, o).Emit();
                        }
                    }
                }
            }

            if (this.TypeInfo.ClassType == ClassType.Struct && !this.StaticBlock)
            {
                this.EmitStructMethods();
            }
        }

        protected virtual void EmitStructMethods()
        {
            var typeDef = this.Emitter.GetTypeDefinition();
            string structName = this.Emitter.Validator.GetCustomTypeName(typeDef);

            if (structName.IsEmpty())
            {
                structName = BridgeTypes.ToJsName(this.TypeInfo.Type, this.Emitter);
            }

            if (this.TypeInfo.InstanceConfig.Fields.Count == 0)
            {
                this.Write("$clone(to");
                this.WriteColon();
                this.Write(structName);
                this.WriteCloseParentheses();
                this.WriteColon();
                this.Write(structName);                    
                this.WriteSemiColon();
                this.WriteNewLine();
                return;
            }

            if (!this.TypeInfo.InstanceMethods.ContainsKey("GetHashCode"))
            {
                this.Write("getHashCode()");
                this.WriteColon();
                this.Write(structName);
                this.WriteSemiColon();
                this.WriteNewLine();
            }

            if (!this.TypeInfo.InstanceMethods.ContainsKey("Equals"))
            {
                this.Write("equals(o");
                this.WriteColon();
                this.Write(structName);
                this.WriteCloseParentheses();
                this.WriteColon();
                this.Write("Boolean");
                this.WriteSemiColon();
                this.WriteNewLine();
            }

            this.Write("$clone(to");
            this.WriteColon();
            this.Write(structName);
            this.WriteCloseParentheses();
            this.WriteColon();
            this.Write(structName);
            this.WriteSemiColon();
            this.WriteNewLine();
        }
    }
}