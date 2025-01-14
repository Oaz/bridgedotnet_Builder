﻿using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Semantics;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bridge.Contract
{
    public class TypeConfigItem
    {
        public string Name
        {
            get;
            set;
        }

        public EntityDeclaration Entity
        {
            get;
            set;
        }

        public Expression Initializer
        {
            get;
            set;
        }

        public VariableInitializer VarInitializer
        {
            get;
            set;
        }

        public bool IsConst
        {
            get;
            set;
        }

        public string GetName(IEmitter emitter)
        {
            string fieldName = this.Name;

            if (this.VarInitializer != null)
            {
                var rr = emitter.Resolver.ResolveNode(this.VarInitializer, emitter) as MemberResolveResult;
                fieldName = OverloadsCollection.Create(emitter, rr.Member).GetOverloadName();
            }
            else if (this.Entity is PropertyDeclaration) {
                fieldName = OverloadsCollection.Create(emitter, (PropertyDeclaration)this.Entity).GetOverloadName();
            }
            else
            {
                fieldName = emitter.ChangeCase ? TypeConfigItem.ToLowerCamelCase(fieldName) : fieldName;

                if (Helpers.IsReservedWord(fieldName))
                {
                    fieldName = "$" + fieldName;
                }
            }
            return fieldName;
        }

        public static string ToLowerCamelCase(string text)
        {
            return text.Substring(0, 1).ToLower(CultureInfo.InvariantCulture) + text.Substring(1);
        }
    }

    public class TypeConfigInfo
    {
        public TypeConfigInfo()
        {
            this.Fields = new List<TypeConfigItem>();
            this.Events = new List<TypeConfigItem>();
            this.Properties = new List<TypeConfigItem>();
            this.Alias = new List<TypeConfigItem>();
        }

        public bool HasMembers
        {
            get
            {
                return this.Fields.Count > 0 || this.Events.Count > 0 || this.Properties.Count > 0 || this.Alias.Count > 0;
            }
        }

        public bool HasConfigMembers
        {
            get
            {
                return this.Events.Count > 0 || this.Properties.Count > 0 || this.Alias.Count > 0;
            }
        }

        public List<TypeConfigItem> Fields
        {
            get;
            set;
        }

        public List<TypeConfigItem> Events
        {
            get;
            set;
        }

        public List<TypeConfigItem> Properties
        {
            get;
            set;
        }

        public List<TypeConfigItem> Alias
        {
            get;
            set;
        }
    }
}
