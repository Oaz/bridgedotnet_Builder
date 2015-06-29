﻿using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Semantics;
using System.Linq;

namespace Bridge.Translator.TypeScript
{
    public class PropertyBlock : AbstractEmitterBlock
    {
        public PropertyBlock(IEmitter emitter, PropertyDeclaration propertyDeclaration)
            : base(emitter, propertyDeclaration)
        {
            this.Emitter = emitter;
            this.PropertyDeclaration = propertyDeclaration;
        }

        public PropertyDeclaration PropertyDeclaration
        {
            get;
            set;
        }

        protected override void DoEmit()
        {
            if (this.PropertyDeclaration.Getter.Body.IsNull && this.PropertyDeclaration.Setter.Body.IsNull)
            {
                return;
            }

            this.EmitPropertyMethod(this.PropertyDeclaration, this.PropertyDeclaration.Getter, false);
            this.EmitPropertyMethod(this.PropertyDeclaration, this.PropertyDeclaration.Setter, true);
        }

        protected virtual void EmitPropertyMethod(PropertyDeclaration propertyDeclaration, Accessor accessor, bool setter)
        {
            var memberResult = this.Emitter.Resolver.ResolveNode(propertyDeclaration, this.Emitter) as MemberResolveResult;

            if (memberResult != null &&
                (memberResult.Member.Attributes.Any(a => a.AttributeType.FullName == "Bridge.FieldPropertyAttribute") ||
                (propertyDeclaration.Getter.IsNull && propertyDeclaration.Setter.IsNull)))
            {
                return;
            }

            if (!accessor.IsNull && this.Emitter.GetInline(accessor) == null)
            {
                var p = (PropertyDeclaration)accessor.Parent;
                var overloads = OverloadsCollection.Create(this.Emitter, propertyDeclaration, setter);
                string name = overloads.GetOverloadName();
                this.Write((setter ? "set" : "get") + name);                
                this.WriteOpenParentheses();
                if (setter)
                {
                    this.Write("value");
                    this.WriteColon();
                    name = BridgeTypes.ToJsName(p.ReturnType, this.Emitter);
                    name = EmitBlock.HandleType(name);
                    this.Write(name);
                }   
             
                this.WriteCloseParentheses();
                this.WriteColon();

                if (setter)
                {
                    this.Write("void");
                }
                else
                {
                    name = BridgeTypes.ToJsName(p.ReturnType, this.Emitter);
                    name = EmitBlock.HandleType(name);
                    this.Write(name);
                }

                this.WriteSemiColon();
                this.WriteNewLine();
            }
        }
    }
}