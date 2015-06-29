using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

namespace Bridge.Contract
{
    public class BridgeType
    {
        public BridgeType(string key)
        {
            this.Key = key;
        }

        public IEmitter Emitter
        {
            get;
            set;
        }

        public string Key
        {
            get;
            private set;
        }

        public TypeDefinition TypeDefinition
        {
            get;
            set;
        }

        public IType Type
        {
            get;
            set;
        }

        public ITypeInfo TypeInfo
        {
            get;
            set;
        }
    }

    public class BridgeTypes : Dictionary<string, BridgeType>
    {
        public void InitItems(IEmitter emitter)
        {
            this.Emitter = emitter;
            foreach (var item in this)
            {
                var type = item.Value;
                var key = BridgeTypes.GetTypeDefinitionKey(type.TypeDefinition);
                type.Emitter = emitter;
                type.Type = ReflectionHelper.ParseReflectionName(key).Resolve(emitter.Resolver.Resolver.TypeResolveContext);
                type.TypeInfo = emitter.Types.FirstOrDefault(t => t.Key == key);
            }
        }

        public IEmitter Emitter
        {
            get;
            set;
        }

        public BridgeType Get(string key)
        {
            return this[key];
        }

        public BridgeType Get(TypeDefinition type, bool safe = false)
        {
            foreach (var item in this)
            {
                if (item.Value.TypeDefinition == type)
                {
                    return item.Value;
                }
            }

            if (!safe)
            {
                throw new Exception("Cannot find type: " + type.FullName);
            }

            return null;
        }

        public BridgeType Get(TypeReference type, bool safe = false)
        {
            var name = type.FullName;
            if (type.IsGenericInstance)
            {
                /*try
                {
                    name = type.Resolve().FullName;
                }
                catch
                {
                    try
                    {
                        var elementType = type.GetElementType();

                        name = elementType != null ? elementType.FullName : type.FullName;
                    }
                    catch
                    {
                        name = type.FullName;
                    }
                }*/

                name = type.GetElementType().FullName;
            }

            foreach (var item in this)
            {
                if (item.Value.TypeDefinition.FullName == name)
                {
                    return item.Value;
                }
            }

            if (!safe)
            {
                throw new Exception("Cannot find type: " + type.FullName);
            }

            return null;
        }

        public BridgeType Get(IType type, bool safe = false)
        {
            if (type.IsParameterized)
            {
                type = ((ParameterizedTypeReference)type.ToTypeReference()).GenericType.Resolve(this.Emitter.Resolver.Resolver.TypeResolveContext);
            }

            foreach (var item in this)
            {
                if (item.Value.Type == type)
                {
                    return item.Value;
                }
            }

            if (!safe)
            {
                throw new Exception("Cannot find type: " + type.ReflectionName);
            }

            return null;
        }

        public BridgeType Get(ITypeInfo type, bool safe = false)
        {
            foreach (var item in this)
            {
                if (item.Value.Type.ReflectionName == type.Key)
                {
                    return item.Value;
                }
            }

            if (!safe)
            {
                throw new Exception("Cannot find type: " + type.Key);
            }

            return null;
        }

        public IType ToType(AstType type)
        {
            var resolveResult = this.Emitter.Resolver.ResolveNode(type, this.Emitter);
            return resolveResult.Type;
        }

        public static string ToJsName(IType type, IEmitter emitter, bool asDefinition = false, bool excludens = false)
        {
            if (type.Kind == TypeKind.Array)
            {
                return "Array";
            }

            if (type.Kind == TypeKind.Dynamic)
            {
                return "Object";
            }

            if (NullableType.IsNullable(type))
            {
                return BridgeTypes.ToJsName(NullableType.GetUnderlyingType(type), emitter);
            }

            BridgeType bridgeType = emitter.BridgeTypes.Get(type, true);
            string name = BridgeTypes.ConvertName(excludens ? type.Name : type.FullName);
            bool isCustomName = false;
            if (bridgeType != null)
            {
                name = BridgeTypes.AddModule(name, bridgeType, out isCustomName);
            }

            if (!isCustomName && type.TypeArguments.Count > 0)
            {
                name += "$" + type.TypeArguments.Count;
            }

            if (!asDefinition && type.TypeArguments.Count > 0 && !Helpers.IsIgnoreGeneric(type, emitter))
            {
                StringBuilder sb = new StringBuilder(name);
                bool needComma = false;
                sb.Append("(");
                foreach (var typeArg in type.TypeArguments)
                {
                    if (needComma)
                    {
                        sb.Append(",");
                    }

                    needComma = true;
                    sb.Append(BridgeTypes.ToJsName(typeArg, emitter));
                }
                sb.Append(")");
                name = sb.ToString();
            }

            return name;
        }

        public static string ToJsName(TypeDefinition type, IEmitter emitter, bool asDefinition = false)
        {
            return BridgeTypes.ToJsName(ReflectionHelper.ParseReflectionName(BridgeTypes.GetTypeDefinitionKey(type)).Resolve(emitter.Resolver.Resolver.TypeResolveContext), emitter, asDefinition);
        }

        public static string DefinitionToJsName(IType type, IEmitter emitter)
        {
            return BridgeTypes.ToJsName(type, emitter, true);
        }

        public static string DefinitionToJsName(TypeDefinition type, IEmitter emitter)
        {
            return BridgeTypes.ToJsName(type, emitter, true);
        }

        public static string ToJsName(AstType astType, IEmitter emitter)
        {
            var primitive = astType as PrimitiveType;

            if (primitive != null && primitive.KnownTypeCode == KnownTypeCode.Void)
            {
                return "void";
            }
            
            var composedType = astType as ComposedType;

            if (composedType != null && composedType.ArraySpecifiers != null && composedType.ArraySpecifiers.Count > 0)
            {
                return "Array";
            }

            var simpleType = astType as SimpleType;

            if (simpleType != null && simpleType.Identifier == "dynamic")
            {
                return "Object";
            }

            var resolveResult = emitter.Resolver.ResolveNode(astType, emitter);
            return BridgeTypes.ToJsName(resolveResult.Type, emitter);
        }

        private static string AddModule(string name, BridgeType type, out bool isCustomName)
        {
            isCustomName = false;
            var emitter = type.Emitter;
            var currentTypeInfo = emitter.TypeInfo;
            string module = null;

            if (currentTypeInfo.Key != type.Key && type.TypeInfo != null)
            {
                var typeInfo = type.TypeInfo;
                module = typeInfo.Module;
                if (typeInfo.Module != null && currentTypeInfo.Module != typeInfo.Module && !emitter.CurrentDependencies.Any(d => d.DependencyName == typeInfo.Module))
                {
                    emitter.CurrentDependencies.Add(new ModuleDependency { DependencyName = typeInfo.Module });
                }
            }

            var customName = emitter.Validator.GetCustomTypeName(type.TypeDefinition);

            if (!String.IsNullOrEmpty(customName))
            {
                isCustomName = true;
                name = customName;
            }

            if (!String.IsNullOrEmpty(module) && currentTypeInfo.Key != type.Key && currentTypeInfo.Module != module)
            {
                name = module + "." + name;
            }

            return name;
        }

        private static System.Collections.Generic.Dictionary<string, string> replacements;
        private static Regex convRegex;

        private static string ConvertName(string name)
        {
            if (BridgeTypes.convRegex == null)
            {
                replacements = new System.Collections.Generic.Dictionary<string, string>(4);
                replacements.Add("`", "$");
                replacements.Add("/", ".");
                replacements.Add("+", ".");
                replacements.Add("[", "");
                replacements.Add("]", "");

                BridgeTypes.convRegex = new Regex("(\\" + String.Join("|\\", replacements.Keys.ToArray()) + ")", RegexOptions.Compiled | RegexOptions.Singleline);
            }

            return BridgeTypes.convRegex.Replace
            (
                name,
                delegate(Match m) { return replacements[m.Value]; }
            );
        }

        public static string GetTypeDefinitionKey(TypeDefinition type)
        {
            return BridgeTypes.GetTypeDefinitionKey(type.FullName);
        }

        public static string GetTypeDefinitionKey(string name)
        {
            return name.Replace("/", "+");
        }
    }
}
