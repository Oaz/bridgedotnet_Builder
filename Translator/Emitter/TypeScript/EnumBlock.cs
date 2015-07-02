using Bridge.Contract;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Object.Net.Utilities;
using ICSharpCode.NRefactory.CSharp;

namespace Bridge.Translator.TypeScript
{
    public class EnumBlock: AbstractEmitterBlock
    {
        public EnumBlock(IEmitter emitter, ITypeInfo typeInfo)
            : base(emitter, typeInfo.TypeDeclaration)
        {
            this.TypeInfo = typeInfo;
        }

        public ITypeInfo TypeInfo
        {
            get;
            set;
        }

        protected override void DoEmit()
        {
            TypeDefinition baseType = this.Emitter.GetBaseTypeDefinition();
            var typeDef = this.Emitter.GetTypeDefinition();
            string name = this.Emitter.Validator.GetCustomTypeName(typeDef);

            if (name.IsEmpty())
            {
                name = BridgeTypes.ToJsName(this.TypeInfo.Type, this.Emitter, false, true);
            }

            name = Bridge.Translator.TypeScript.EmitBlock.HandleType(name);

            this.Write("export enum ");
            this.Write(name);

            this.WriteSpace();
            this.BeginBlock();

            if (this.TypeInfo.StaticConfig.Fields.Count > 0)
            {
                var lastField = this.TypeInfo.StaticConfig.Fields.Last();
                foreach (var field in this.TypeInfo.StaticConfig.Fields)
                {
                    this.Write(field.GetName(this.Emitter));

                    var initializer = field.Initializer;
                    if (initializer != null && initializer is PrimitiveExpression)
                    {
                        this.Write(" = ");
                        this.Write(((PrimitiveExpression)initializer).Value);
                    }

                    if (field != lastField)
                    {
                        this.WriteComma();                        
                    }

                    this.WriteNewLine();
                }
            }

            this.EndBlock();
        }
    }
}
