using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;

namespace Bridge.Translator.TypeScript
{
    public class CustomEventBlock : AbstractEmitterBlock
    {
        public CustomEventBlock(IEmitter emitter, CustomEventDeclaration customEventDeclaration)
            : base(emitter, customEventDeclaration)
        {
            this.Emitter = emitter;
            this.CustomEventDeclaration = customEventDeclaration;
        }

        public CustomEventDeclaration CustomEventDeclaration
        {
            get;
            set;
        }

        protected override void DoEmit()
        {
            this.EmitPropertyMethod(this.CustomEventDeclaration, this.CustomEventDeclaration.AddAccessor, false);
            this.EmitPropertyMethod(this.CustomEventDeclaration, this.CustomEventDeclaration.RemoveAccessor, true);
        }

        protected virtual void EmitPropertyMethod(CustomEventDeclaration customEventDeclaration, Accessor accessor, bool remover)
        {
            if (!accessor.IsNull && this.Emitter.GetInline(accessor) == null)
            {
                var overloads = OverloadsCollection.Create(this.Emitter, customEventDeclaration, remover);

                this.Write((remover ? "remove" : "add") + overloads.GetOverloadName());
                this.WriteColon();
                this.WriteOpenParentheses();
                this.Write("value");
                this.WriteColon();
                var retType = BridgeTypes.ToJsName(customEventDeclaration.ReturnType, this.Emitter);
                retType = EmitBlock.HandleType(retType);
                this.Write(retType);
                this.WriteCloseParentheses();
                this.WriteColon();
                this.Write("void");

                this.WriteSemiColon();
                this.WriteNewLine();
            }
        }
    }
}