using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using Mono.Cecil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Newtonsoft.Json;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using ICSharpCode.NRefactory.Semantics;
using Bridge.Contract;
using System.Text.RegularExpressions;

namespace Bridge.Translator
{
    public partial class Emitter
    {
        protected virtual HashSet<string> CreateNamespaces()
        {
            var result = new HashSet<string>();

            foreach (string typeName in this.TypeDefinitions.Keys)
            {
                int index = typeName.LastIndexOf('.');

                if (index >= 0)
                {
                    this.RegisterNamespace(typeName.Substring(0, index), result);
                }
            }

            return result;
        }

        protected virtual void RegisterNamespace(string ns, ICollection<string> repository)
        {
            if (String.IsNullOrEmpty(ns) || repository.Contains(ns))
            {
                return;
            }

            string[] parts = ns.Split('.');
            StringBuilder builder = new StringBuilder();

            foreach (string part in parts)
            {
                if (builder.Length > 0)
                {
                    builder.Append('.');
                }

                builder.Append(part);
                string item = builder.ToString();

                if (!repository.Contains(item))
                {
                    repository.Add(item);
                }
            }
        }

        public static bool IsReservedStaticName(string name)
        {
            return Emitter.reservedStaticNames.Any(n => String.Equals(name, n, StringComparison.InvariantCultureIgnoreCase));
        }

        public virtual string ToJavaScript(object value)
        {
            return JsonConvert.SerializeObject(value);
        }

        protected virtual ICSharpCode.NRefactory.CSharp.Attribute GetAttribute(AstNodeCollection<AttributeSection> attributes, string name)
        {
            string fullName = name + "Attribute";
            foreach (var i in attributes)
            {
                foreach (var j in i.Attributes)
                {
                    if (j.Type.ToString() == name)
                    {
                        return j;
                    }

                    var resolveResult = this.Resolver.ResolveNode(j, this);
                    if (resolveResult != null && resolveResult.Type != null && resolveResult.Type.FullName == fullName)
                    {
                        return j;
                    }
                }
            }

            return null;
        }

        public virtual CustomAttribute GetAttribute(IEnumerable<CustomAttribute> attributes, string name)
        {
            foreach (var attr in attributes)
            {
                if (attr.AttributeType.FullName == name)
                {
                    return attr;
                }
            }

            return null;
        }

        public virtual IAttribute GetAttribute(IEnumerable<IAttribute> attributes, string name)
        {
            foreach (var attr in attributes)
            {
                if (attr.AttributeType.FullName == name)
                {
                    return attr;
                }
            }

            return null;
        }

        protected virtual bool HasDelegateAttribute(MethodDeclaration method)
        {
            return this.GetAttribute(method.Attributes, "Delegate") != null;
        }

        public virtual Tuple<bool, bool, string> GetInlineCode(InvocationExpression node)
        {
            var resolveResult = this.Resolver.ResolveNode(node, this);
            var memberResolveResult = resolveResult as MemberResolveResult;

            if (memberResolveResult == null)
            {
                return new Tuple<bool, bool, string>(false, false, null);
            }

            var member = memberResolveResult.Member;
            bool isInlineMethod = this.IsInlineMethod(member);
            var inlineCode = isInlineMethod ? null : this.GetInline(member);
            var isStatic = member.IsStatic;

            return new Tuple<bool, bool, string>(isStatic, isInlineMethod, inlineCode);
        }

        public virtual IEnumerable<string> GetScript(EntityDeclaration method)
        {
            var attr = this.GetAttribute(method.Attributes, Bridge.Translator.Translator.Bridge_ASSEMBLY + ".Script");

            return this.GetScriptArguments(attr);
        }

        public virtual string GetDefinitionName(IMemberDefinition member, bool changeCase = true)
        {
            if (changeCase)
            {
                changeCase = !this.IsNativeMember(member.FullName) ? this.ChangeCase : true;
                if (member is FieldDefinition && ((FieldDefinition)member).HasConstant && !member.DeclaringType.IsEnum)
                {
                    changeCase = false;
                }
            }
            string attrName = Bridge.Translator.Translator.Bridge_ASSEMBLY + ".NameAttribute";
            var attr = member.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == attrName);
            bool isIgnore = this.Validator.IsIgnoreType(member.DeclaringType);
            string name = member.Name;
            bool isStatic = false;

            if (member is MethodDefinition)
            {
                var method = (MethodDefinition)member;
                isStatic = method.IsStatic;
                if (method.IsConstructor)
                {
                    name = "constructor";
                }
            }
            else if (member is FieldDefinition)
            {
                isStatic = ((FieldDefinition)member).IsStatic;
            }
            else if (member is PropertyDefinition)
            {
                var prop = (PropertyDefinition)member;
                var accessor = prop.GetMethod ?? prop.SetMethod;
                isStatic = prop.GetMethod != null ? prop.GetMethod.IsStatic : false;
            }
            else if (member is EventDefinition)
            {
                var ev = (EventDefinition)member;
                isStatic = ev.AddMethod != null ? ev.AddMethod.IsStatic : false ;
            }
            if (attr != null)
            {
                var value = attr.ConstructorArguments.First().Value;
                if (value is string)
                {
                    name = value.ToString();
                    if (!isIgnore &&
                        ((isStatic && Emitter.IsReservedStaticName(name)) ||
                        Helpers.IsReservedWord(name)))
                    {
                        name = "$" + name;
                    }
                    return name;
                }

                changeCase = (bool)value;
            }

            if (name.Contains("."))
            {
                name = Object.Net.Utilities.StringUtils.RightOfRightmostOf(name, '.');
            }
            name = changeCase ? Object.Net.Utilities.StringUtils.ToLowerCamelCase(name) : name;
            if (!isIgnore &&
                ((isStatic && Emitter.IsReservedStaticName(name)) ||
                Helpers.IsReservedWord(name)))
            {
                name = "$" + name;
            }

            return name;
        }

        public virtual string GetEntityNameFromAttr(IEntity member, bool setter = false)
        {
            var prop = member as IProperty;
            if (prop != null)
            {
                member = setter ? prop.Setter : prop.Getter;
            }
            else
            {
                var e = member as IEvent;
                if (e != null)
                {
                    member = setter ? e.AddAccessor : e.RemoveAccessor;
                }
            }

            var attr = member.Attributes.FirstOrDefault(a => a.AttributeType.FullName == Bridge.Translator.Translator.Bridge_ASSEMBLY + ".NameAttribute");
            bool isIgnore = member.DeclaringTypeDefinition != null && this.Validator.IsIgnoreType(member.DeclaringTypeDefinition);
            string name;

            if (attr != null)
            {
                var value = attr.PositionalArguments.First().ConstantValue;
                if (value is string)
                {
                    name = value.ToString();
                    if (!isIgnore && ((member.IsStatic && Emitter.IsReservedStaticName(name)) || Helpers.IsReservedWord(name)))
                    {
                        name = "$" + name;
                    }
                    return name;
                }
            }

            return null;
        }

        public virtual string GetEntityName(IEntity member, bool cancelChangeCase = false, bool ignoreInterface = false)
        {
            bool changeCase = !this.IsNativeMember(member.FullName) ? this.ChangeCase : true;
            if (member is IMember && this.IsMemberConst((IMember)member)/* || member.DeclaringType.Kind == TypeKind.Anonymous*/)
            {
                changeCase = false;
            }
            var attr = member.Attributes.FirstOrDefault(a => a.AttributeType.FullName == Bridge.Translator.Translator.Bridge_ASSEMBLY + ".NameAttribute");
            bool isIgnore = member.DeclaringTypeDefinition != null && this.Validator.IsIgnoreType(member.DeclaringTypeDefinition);
            string name = member.Name;
            if (member is IMethod && ((IMethod)member).IsConstructor)
            {
                name = "constructor";
            }

            if (attr != null)
            {
                var value = attr.PositionalArguments.First().ConstantValue;
                if (value is string)
                {
                    name = value.ToString();
                    if (!isIgnore && ((member.IsStatic && Emitter.IsReservedStaticName(name)) || Helpers.IsReservedWord(name)))
                    {
                        name = "$" + name;
                    }
                    return name;
                }

                changeCase = (bool)value;
            }

            name = changeCase && !cancelChangeCase ? Object.Net.Utilities.StringUtils.ToLowerCamelCase(name) : name;

            if (!isIgnore && ((member.IsStatic && Emitter.IsReservedStaticName(name)) || Helpers.IsReservedWord(name)))
            {
                name = "$" + name;
            }

            return name;
        }

        public virtual string GetEntityName(EntityDeclaration entity, bool cancelChangeCase = false, bool ignoreInterface = false)
        {
            var rr = this.Resolver.ResolveNode(entity, this) as MemberResolveResult;

            if (rr != null)
            {
                return this.GetEntityName(rr.Member, cancelChangeCase, ignoreInterface);
            }

            return null;
        }

        public virtual string GetEntityName(ParameterDeclaration entity, bool cancelChangeCase = false)
        {
            var name = entity.Name;

            if (entity.Parent != null && entity.GetParent<SyntaxTree>() != null)
            {
                var rr = this.Resolver.ResolveNode(entity, this) as LocalResolveResult;
                if (rr != null)
                {
                    var iparam = rr.Variable as IParameter;

                    if (iparam != null && iparam.Attributes != null)
                    {
                        var attr = iparam.Attributes.FirstOrDefault(a => a.AttributeType.FullName == Bridge.Translator.Translator.Bridge_ASSEMBLY + ".NameAttribute");

                        if (attr != null)
                        {
                            var value = attr.PositionalArguments.First().ConstantValue;
                            if (value is string)
                            {
                                name = value.ToString();
                            }
                        }
                    }
                }
            }

            if (Helpers.IsReservedWord(name))
            {
                name = "$" + name;
            }

            return name;
        }

        public virtual string GetFieldName(FieldDeclaration field)
        {
            if (!string.IsNullOrEmpty(field.Name))
            {
                return field.Name;
            }

            if (field.Variables.Count > 0)
            {
                return field.Variables.First().Name;
            }

            return null;
        }

        public virtual string GetEventName(EventDeclaration evt)
        {
            if (!string.IsNullOrEmpty(evt.Name))
            {
                return evt.Name;
            }

            if (evt.Variables.Count > 0)
            {
                return evt.Variables.First().Name;
            }

            return null;
        }

        public Tuple<bool, string> IsGlobalTarget(IMember member)
        {
            var attr = this.GetAttribute(member.Attributes, Bridge.Translator.Translator.Bridge_ASSEMBLY + ".GlobalTargetAttribute");

            return attr != null ? new Tuple<bool, string>(true, (string)attr.PositionalArguments.First().ConstantValue) : null;
        }

        public virtual string GetInline(ICustomAttributeProvider provider)
        {
            var attr = this.GetAttribute(provider.CustomAttributes, Bridge.Translator.Translator.Bridge_ASSEMBLY + ".TemplateAttribute");

            return attr != null ? ((string)attr.ConstructorArguments.First().Value) : null;
        }

        public virtual string GetInline(EntityDeclaration method)
        {
            var attr = this.GetAttribute(method.Attributes, Bridge.Translator.Translator.Bridge_ASSEMBLY + ".Template");

            return attr != null ? ((string)((PrimitiveExpression)attr.Arguments.First()).Value) : null;
        }

        public virtual string GetInline(IEntity entity)
        {
            string attrName = Bridge.Translator.Translator.Bridge_ASSEMBLY + ".TemplateAttribute";

            if (entity.SymbolKind == SymbolKind.Property)
            {
                var prop = (IProperty)entity;
                entity = this.IsAssignment ? prop.Setter : prop.Getter;
            }

            if (entity != null)
            {
                var attr = entity.Attributes.FirstOrDefault(a =>
                {

                    return a.AttributeType.FullName == attrName;
                });

                return attr != null ? attr.PositionalArguments[0].ConstantValue.ToString() : null;
            }

            return null;
        }

        protected virtual bool IsInlineMethod(IEntity entity)
        {
            string attrName = Bridge.Translator.Translator.Bridge_ASSEMBLY + ".TemplateAttribute";

            if (entity != null)
            {
                var attr = entity.Attributes.FirstOrDefault(a =>
                {

                    return a.AttributeType.FullName == attrName;
                });

                return attr != null && attr.PositionalArguments.Count == 0;
            }

            return false;
        }

        protected virtual IEnumerable<string> GetScriptArguments(ICSharpCode.NRefactory.CSharp.Attribute attr)
        {
            if (attr == null)
            {
                return null;
            }

            var result = new List<string>();

            foreach (var arg in attr.Arguments)
            {
                PrimitiveExpression expr = (PrimitiveExpression)arg;
                result.Add((string)expr.Value);
            }

            return result;
        }

        public virtual bool IsNativeMember(string fullName)
        {
            return fullName.Contains(Bridge.Translator.Translator.Bridge_ASSEMBLY) || fullName.StartsWith("System");
        }

        public virtual bool IsMemberConst(IMember member)
        {
            return (member is DefaultResolvedField) && (((DefaultResolvedField)member).IsConst && member.DeclaringType.Kind != TypeKind.Enum);
        }

        public virtual bool IsInlineConst(IMember member)
        {
            bool isConst = (member is DefaultResolvedField) && (((DefaultResolvedField)member).IsConst && member.DeclaringType.Kind != TypeKind.Enum);

            if (isConst)
            {
                var attr = this.GetAttribute(member.Attributes, Bridge.Translator.Translator.Bridge_ASSEMBLY + ".InlineConstAttribute");

                if (attr != null)
                {
                    return true;
                }
            }

            return false;
        }






    }
}
