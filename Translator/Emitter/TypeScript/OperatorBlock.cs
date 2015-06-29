using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using System.Collections.Generic;

namespace Bridge.Translator.TypeScript
{
    public class OperatorBlock : AbstractEmitterBlock
    {
        public OperatorBlock(IEmitter emitter, OperatorDeclaration operatorDeclaration)
            : base(emitter, operatorDeclaration)
        {
            this.Emitter = emitter;
            this.OperatorDeclaration = operatorDeclaration;
        }

        public OperatorDeclaration OperatorDeclaration
        {
            get;
            set;
        }

        protected override void DoEmit()
        {
            this.EmitOperatorDeclaration(this.OperatorDeclaration);
        }

        protected void EmitOperatorDeclaration(OperatorDeclaration operatorDeclaration)
        {
            var typeDef = this.Emitter.GetTypeDefinition();
            var overloads = OverloadsCollection.Create(this.Emitter, operatorDeclaration);

            if (overloads.HasOverloads)
            {
                string name = overloads.GetOverloadName();
                this.Write(name);
            }
            else
            {
                this.Write(this.Emitter.GetEntityName(operatorDeclaration));
            }

            this.EmitMethodParameters(operatorDeclaration.Parameters, operatorDeclaration);

            this.WriteColon();

            var retType = BridgeTypes.ToJsName(operatorDeclaration.ReturnType, this.Emitter);
            retType = EmitBlock.HandleType(retType);
            this.Write(retType);

            this.WriteSemiColon();
            this.WriteNewLine();
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