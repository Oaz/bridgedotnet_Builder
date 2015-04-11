﻿using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using Object.Net.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bridge.Translator
{
    public class MethodBlock : AbstractMethodBlock
    {
        public MethodBlock(IEmitter emitter, ITypeInfo typeInfo, bool staticBlock)
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

        public override void Emit()
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
                        this.Emitter.VisitPropertyDeclaration((PropertyDeclaration)prop);
                    }
                    else if (prop is CustomEventDeclaration)
                    {
                        this.Emitter.VisitCustomEventDeclaration((CustomEventDeclaration)prop);
                    }
                    else if (prop is IndexerDeclaration)
                    {
                        this.Emitter.VisitIndexerDeclaration((IndexerDeclaration)prop);
                    }
                }
            }

            names = new List<string>(methods.Keys);

            foreach (var name in names)
            {
                this.EmitMethodsGroup(methods[name]);
            }

            if (operators != null)
            {
                var ops = new List<OperatorType>(operators.Keys);

                foreach (var op in ops)
                {
                    this.EmitOperatorGroup(operators[op]);
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
                this.EnsureComma();
                this.Write("$clone: function (to) { return this; }");
                this.Emitter.Comma = true;
                return;
            }

            if (!this.TypeInfo.InstanceMethods.ContainsKey("GetHashCode"))
            {
                this.EnsureComma();
                this.Write("getHashCode: function () ");
                this.BeginBlock();
                this.Write("var hash = 17;");                

                foreach (var field in this.TypeInfo.InstanceConfig.Fields)
                {
                    string fieldName = field.GetName(this.Emitter);

                    this.WriteNewLine();
                    this.Write("hash = hash * 23 + ");
                    this.Write("(this." + fieldName);
                    this.Write(" == null ? 0 : ");                    
                    this.Write("Bridge.getHashCode(");
                    this.Write("this." + fieldName);
                    this.Write("));");
                }

                this.WriteNewLine();
                this.Write("return hash;");
                this.WriteNewLine();
                this.EndBlock();
                this.Emitter.Comma = true;
            }

            if (!this.TypeInfo.InstanceMethods.ContainsKey("Equals"))
            {                
                this.EnsureComma();
                this.Write("equals: function (o) ");
                this.BeginBlock();
                this.Write("if (!Bridge.is(o,");
                this.Write(structName);
                this.Write(")) ");
                this.BeginBlock();
                this.Write("return false;");
                this.WriteNewLine();
                this.EndBlock();
                this.WriteNewLine();
                this.Write("return ");

                bool and = false;

                foreach (var field in this.TypeInfo.InstanceConfig.Fields)
                {
                    string fieldName = field.GetName(this.Emitter);

                    if (and)
                    {
                        this.Write(" && ");
                    }

                    and = true;

                    this.Write("Bridge.equals(this.");
                    this.Write(fieldName);
                    this.Write(", o.");
                    this.Write(fieldName);
                    this.Write(")");
                }

                this.Write(";");
                this.WriteNewLine();
                this.EndBlock();
                this.Emitter.Comma = true;
            }

            this.EnsureComma();
            this.Write("$clone: function (to) ");
            this.BeginBlock();
            this.Write("var s = to || new ");
            this.Write(structName);
            this.Write("();");                        

            foreach (var field in this.TypeInfo.InstanceConfig.Fields)
            {
                this.WriteNewLine();
                string fieldName = field.GetName(this.Emitter);

                this.Write("s.");
                this.Write(fieldName);
                this.Write(" = this.");
                this.Write(fieldName);
                this.Write(";");
            }

            this.WriteNewLine();
            this.Write("return s;");
            this.WriteNewLine();
            this.EndBlock();
            this.Emitter.Comma = true;
        }

        protected void EmitEventAccessor(EventDeclaration e, VariableInitializer evtVar, bool add)
        {
            string name = evtVar.Name;

            this.Write(add ? "add" : "remove", name, " : ");
            this.WriteFunction();
            this.WriteOpenParentheses();
            this.Write("value");
            this.WriteCloseParentheses();
            this.WriteSpace();
            this.BeginBlock();
            this.WriteThis();
            this.WriteDot();
            this.Write(this.Emitter.GetEntityName(e));
            this.Write(" = ");
            this.Write(Bridge.Translator.Emitter.ROOT, ".", add ? Bridge.Translator.Emitter.DELEGATE_COMBINE : Bridge.Translator.Emitter.DELEGATE_REMOVE);
            this.WriteOpenParentheses();
            this.WriteThis();
            this.WriteDot();
            this.Write(this.Emitter.GetEntityName(e));
            this.WriteComma();
            this.WriteSpace();
            this.Write("value");
            this.WriteCloseParentheses();
            this.WriteSemiColon();
            this.WriteNewLine();
            this.EndBlock();
        }

        protected virtual void EmitMethodsGroup(List<MethodDeclaration> group)
        {
            if (group.Count == 1)
            {
                if (!group[0].Body.IsNull)
                {
                    this.Emitter.VisitMethodDeclaration(group[0]);
                }
            }
            else
            {
                var typeDef = this.Emitter.GetTypeDefinition();
                var name = group[0].Name;
                var methodsDef = typeDef.Methods.Where(m => m.Name == name);
                this.Emitter.MethodsGroup = methodsDef;
                this.Emitter.MethodsGroupBuilder = new Dictionary<int, StringBuilder>();

                foreach (var method in group)
                {
                    if (!method.Body.IsNull)
                    {
                        this.Emitter.VisitMethodDeclaration(method);
                    }
                }

                this.Emitter.MethodsGroup = null;
                this.Emitter.MethodsGroupBuilder = null;
            }
        }

        protected virtual void EmitOperatorGroup(List<OperatorDeclaration> group)
        {
            if (group.Count == 1)
            {
                if (!group[0].Body.IsNull)
                {
                    this.Emitter.VisitOperatorDeclaration(group[0]);
                }
            }
            else
            {
                var typeDef = this.Emitter.GetTypeDefinition();
                var name = group[0].Name;
                var methodsDef = typeDef.Methods.Where(m => m.Name == name);
                this.Emitter.MethodsGroup = methodsDef;
                this.Emitter.MethodsGroupBuilder = new Dictionary<int, StringBuilder>();

                foreach (var method in group)
                {
                    if (!method.Body.IsNull)
                    {
                        this.Emitter.VisitOperatorDeclaration(method);
                    }
                }

                this.Emitter.MethodsGroup = null;
                this.Emitter.MethodsGroupBuilder = null;
            }
        }
    }
}