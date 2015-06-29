using ICSharpCode.NRefactory.CSharp;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using ICSharpCode.NRefactory.TypeSystem;
using System.Text;
using Mono.Cecil;
using Object.Net.Utilities;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using Bridge.Contract;

namespace Bridge.Translator.TypeScript
{
    public partial class ConstructorBlock : AbstractEmitterBlock
    {
        public ConstructorBlock(IEmitter emitter, ITypeInfo typeInfo)
            : base(emitter, typeInfo.TypeDeclaration)
        {
            this.Emitter = emitter;
            this.TypeInfo = typeInfo;
        }

        public ITypeInfo TypeInfo
        {
            get;
            set;
        }

        protected override void DoEmit()
        {
            this.EmitCtorForInstantiableClass();
        }

        protected virtual void EmitCtorForInstantiableClass()
        {
            var typeDef = this.Emitter.GetTypeDefinition();
            string name = this.Emitter.Validator.GetCustomTypeName(typeDef);

            if (name.IsEmpty())
            {
                name = BridgeTypes.ToJsName(this.TypeInfo.Type, this.Emitter, false, true);
            }

            name = EmitBlock.HandleType(name);

            if (this.TypeInfo.Ctors.Count == 0)
            {
                this.Write("new ");
                this.WriteOpenCloseParentheses();
                this.WriteColon();
                this.Write(name);
                this.WriteSemiColon();
                this.WriteNewLine();
            }
            else if (this.TypeInfo.Ctors.Count == 1)
            {
                var ctor = this.TypeInfo.Ctors.First();
                if (!ctor.HasModifier(Modifiers.Public))
                {
                    return;
                }

                this.Write("new ");
                this.EmitMethodParameters(ctor.Parameters, ctor);
                this.WriteColon();
                this.Write(name);
                this.WriteSemiColon();
                this.WriteNewLine();
            }
            else
            {
                var isGeneric = typeDef.GenericParameters.Count > 0;
                foreach (var ctor in this.TypeInfo.Ctors)
                {
                    if (!ctor.HasModifier(Modifiers.Public))
                    {
                        continue;
                    }

                    var ctorName = "$constructor";

                    if (this.TypeInfo.Ctors.Count > 1 && ctor.Parameters.Count > 0)
                    {
                        var overloads = OverloadsCollection.Create(this.Emitter, ctor);
                        ctorName = overloads.GetOverloadName();
                    }

                    if (!isGeneric)
                    {                        
                        this.Write(ctorName);
                        this.WriteColon();
                        this.BeginBlock();
                    }
                    
                    this.WriteNew();
                    this.EmitMethodParameters(ctor.Parameters, ctor);
                    this.WriteColon();                    

                    this.Write(name);
                    if(!isGeneric) 
                    {
                        this.WriteNewLine();
                        this.EndBlock();
                    }

                    this.WriteSemiColon();
                    this.WriteNewLine();
                }
            }            
        }

        protected virtual void EmitMethodParameters(IEnumerable<ParameterDeclaration> declarations, AstNode context)
        {
            this.WriteOpenParentheses();
            bool needComma = false;

            foreach (var p in declarations)
            {
                var name = this.Emitter.GetEntityName(p);

                if (needComma)
                {
                    this.WriteComma();
                }

                needComma = true;
                this.Write(name);
                this.WriteColon();
                name = BridgeTypes.ToJsName(p.Type, this.Emitter);
                name = EmitBlock.HandleType(name);
                this.Write(name);
            }

            this.WriteCloseParentheses();
        }
    }
}
