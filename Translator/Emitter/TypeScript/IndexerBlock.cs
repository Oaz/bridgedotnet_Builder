﻿using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using System.Collections.Generic;

namespace Bridge.Translator.TypeScript
{
    public class IndexerBlock : AbstractMethodBlock
    {
        public IndexerBlock(IEmitter emitter, IndexerDeclaration indexerDeclaration)
            : base(emitter, indexerDeclaration)
        {
            this.Emitter = emitter;
            this.IndexerDeclaration = indexerDeclaration;
        }

        public IndexerDeclaration IndexerDeclaration
        {
            get;
            set;
        }

        protected override void DoEmit()
        {
            this.EmitIndexerMethod(this.IndexerDeclaration, this.IndexerDeclaration.Getter, false);
            this.EmitIndexerMethod(this.IndexerDeclaration, this.IndexerDeclaration.Setter, true);
        }

        protected virtual void EmitIndexerMethod(IndexerDeclaration indexerDeclaration, Accessor accessor, bool setter)
        {
            if (!accessor.IsNull && this.Emitter.GetInline(accessor) == null)
            {
                var overloads = OverloadsCollection.Create(this.Emitter, indexerDeclaration, setter);

                string name = overloads.GetOverloadName();
                this.Write((setter ? "set" : "get") + name);
                this.WriteColon();
                
                this.EmitMethodParameters(indexerDeclaration.Parameters, indexerDeclaration, setter);

                if (setter)
                {
                    this.Write(", value");
                    this.WriteColon();
                    name = BridgeTypes.ToJsName(indexerDeclaration.ReturnType, this.Emitter);
                    name = EmitBlock.HandleType(name);
                    this.Write(name);
                    this.WriteCloseParentheses();
                    this.WriteColon();
                    this.Write("void");
                }
                else
                {
                    this.WriteColon();
                    name = BridgeTypes.ToJsName(indexerDeclaration.ReturnType, this.Emitter);
                    name = EmitBlock.HandleType(name);
                    this.Write(name);
                }
                
                this.WriteSemiColon();
                this.WriteNewLine();
            }
        }

        protected virtual void EmitMethodParameters(IEnumerable<ParameterDeclaration> declarations, AstNode context, bool skipClose)
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

            if (!skipClose)
            {
                this.WriteCloseParentheses();
            }            
        }
    }
}