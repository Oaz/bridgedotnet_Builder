﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bridge.Contract
{
    public abstract class AbstractPlugin : IPlugin
    {
        public virtual IEnumerable<string> GetConstructorInjectors(IConstructorBlock constructorBlock)
        {
            return null;
        }

        public virtual bool HasConstructorInjectors(IConstructorBlock constructorBlock)
        {
            return false;
        }

        public virtual void OnConfigRead(IAssemblyInfo config)
        {
        }

        public virtual void BeforeEmit(IEmitter emitter, ITranslator translator)
        {
        }

        public virtual void AfterEmit(IEmitter emitter, ITranslator translator)
        {
        }

        public virtual void BeforeTypesEmit(IEmitter emitter, IList<ITypeInfo> types)
        {
        }

        public virtual void AfterTypesEmit(IEmitter emitter, IList<ITypeInfo> types)
        {
        }

        public virtual void BeforeTypeEmit(IEmitter emitter, ITypeInfo type)
        {
        }

        public virtual void AfterTypeEmit(IEmitter emitter, ITypeInfo type)
        {
        }
    }
}
