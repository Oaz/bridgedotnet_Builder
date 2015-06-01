using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;

namespace Bridge.Translator
{
    public class BaseReferenceBlock : ConversionBlock
    {
        public BaseReferenceBlock(IEmitter emitter, BaseReferenceExpression baseReferenceExpression)
            : base(emitter, baseReferenceExpression)
        {
            this.Emitter = emitter;
            this.BaseReferenceExpression = baseReferenceExpression;
        }

        public BaseReferenceExpression BaseReferenceExpression
        {
            get;
            set;
        }

        protected override Expression GetExpression()
        {
            return this.BaseReferenceExpression;
        }

        protected override void EmitConversionExpression()
        {
            var proto = false;
            if (this.BaseReferenceExpression.Parent != null) 
            {
                var rr = this.Emitter.Resolver.ResolveNode(this.BaseReferenceExpression.Parent, this.Emitter) as MemberResolveResult;

                if (rr != null)
                {
                    var method = rr.Member as IMethod;
                    if (method != null && method.IsVirtual)
                    {
                        proto = true;
                    }
                    else
                    {
                        var prop = rr.Member as IProperty;

                        if (prop != null && prop.IsVirtual)
                        {
                            proto = true;
                        }
                    }                    
                }
            }

            if (proto)
            {
                var baseType = this.Emitter.GetBaseTypeDefinition();
                this.Write(BridgeTypes.ToJsName(baseType, this.Emitter), ".prototype");
            }
            else
            {
                this.WriteThis();
            }            
        }
    }
}
