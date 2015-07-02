using Bridge.Contract;
using Mono.Cecil;
using Object.Net.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Bridge.Translator.TypeScript
{
    public class ClassBlock : AbstractEmitterBlock
    {
        public ClassBlock(IEmitter emitter, ITypeInfo typeInfo)
            : base(emitter, typeInfo.TypeDeclaration)
        {
            this.TypeInfo = typeInfo;
        }

        public ITypeInfo TypeInfo
        {
            get;
            set;
        }

        public bool IsGeneric
        {
            get;
            set;
        }

        public string JsName
        {
            get;
            set;
        }

        public int Position;

        protected override void DoEmit()
        {
            if (this.TypeInfo.IsEnum)
            {
                new EnumBlock(this.Emitter, this.TypeInfo).Emit();
            }
            else
            {
                this.EmitClassHeader();
                this.EmitBlock();
                this.EmitClassEnd();
            }            
        }

        protected virtual void EmitClassHeader()
        {
            TypeDefinition baseType = this.Emitter.GetBaseTypeDefinition();
            var typeDef = this.Emitter.GetTypeDefinition();
            string name = this.Emitter.Validator.GetCustomTypeName(typeDef);
            this.IsGeneric = typeDef.GenericParameters.Count > 0;

            if (name.IsEmpty())
            {
                name = BridgeTypes.ToJsName(this.TypeInfo.Type, this.Emitter, false, true);
            }

            name = Bridge.Translator.TypeScript.EmitBlock.HandleType(name);

            this.Write("export ");
            this.Write("interface ");

            this.JsName = name;
            this.Write(this.JsName);               

            string extend = this.GetTypeHierarchy();

            if (extend.IsNotEmpty() && !this.TypeInfo.IsEnum)
            {
                this.Write(" extends ");
                this.Write(extend);
            }

            this.WriteSpace();
            this.BeginBlock();
            this.Position = this.Emitter.Output.Length;
        }

        private string GetTypeHierarchy()
        {
            StringBuilder sb = new StringBuilder();

            var list = new List<string>();

            foreach (var t in this.TypeInfo.TypeDeclaration.BaseTypes)
            {
                var name = BridgeTypes.ToJsName(t, this.Emitter);

                list.Add(name);
            }

            if (list.Count > 0 && list[0] == "Object")
            {
                list.RemoveAt(0);
            }

            if (list.Count == 0)
            {
                return "";
            }

            bool needComma = false;

            foreach (var item in list)
            {
                if (needComma)
                {
                    sb.Append(",");
                }

                needComma = true;
                sb.Append(item);
            }

            return Bridge.Translator.TypeScript.EmitBlock.HandleType(sb.ToString());
        }

        protected virtual void EmitBlock()
        {
            
          var typeDef = this.Emitter.GetTypeDefinition();

          new MemberBlock(this.Emitter, this.TypeInfo, false).Emit();
          if (this.Emitter.TypeInfo.TypeDeclaration.ClassType != ICSharpCode.NRefactory.CSharp.ClassType.Interface || this.IsGeneric)
          {
                if (this.Position != this.Emitter.Output.Length && !this.Emitter.IsNewLine)
                {
                    this.WriteNewLine();
                }

                this.EndBlock();

                this.WriteNewLine();

                this.Write("export ");
                if (this.IsGeneric)
                {
                    this.WriteFunction();
                }
                else
                {
                    this.Write("interface ");
                }

                this.Write(this.JsName);

                if (!this.IsGeneric)
                {
                    this.Write("Func extends Function ");
                }
                else
                {
                    this.WriteOpenParentheses();
                    var comma = false;
                    foreach (var p in typeDef.GenericParameters)
                    {
                        if (comma)
                        {
                            this.WriteComma();
                        }
                        this.Write(p.Name);
                        this.WriteColon();
                        this.WriteOpenBrace();
                        this.Write("prototype");
                        this.WriteColon();
                        this.Write(p.Name);

                        this.WriteCloseBrace();
                        comma = true;
                    }

                    this.WriteCloseParentheses();
                    this.WriteColon();
                }

                this.BeginBlock();

                this.Write("prototype: ");
                this.Write(this.JsName);
                this.WriteSemiColon();
                this.WriteNewLine();
                this.Position = this.Emitter.Output.Length;

                if (this.Emitter.TypeInfo.TypeDeclaration.ClassType != ICSharpCode.NRefactory.CSharp.ClassType.Interface)
                {
                    new ConstructorBlock(this.Emitter, this.TypeInfo).Emit();
                    new MemberBlock(this.Emitter, this.TypeInfo, true).Emit();
                }
            }
        }

        protected virtual void EmitClassEnd()
        {
            if (this.Position != this.Emitter.Output.Length && !this.Emitter.IsNewLine)
            {
                this.WriteNewLine();
            }
            
            this.EndBlock();

            if (!this.IsGeneric)
            {
                this.WriteNewLine();
                this.Write("var ");
                this.Write(this.JsName);
                this.WriteColon();
                var isInterface = this.Emitter.TypeInfo.TypeDeclaration.ClassType == ICSharpCode.NRefactory.CSharp.ClassType.Interface;
                if (isInterface)
                {
                    this.Write("Function");                    
                }
                else
                {
                    this.Write(this.JsName + "Func");
                }
                
                this.WriteSemiColon();
            }
        }
    }
}
