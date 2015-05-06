﻿using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;

namespace Bridge.Translator
{
    public class EmptyBlock : AbstractEmitterBlock
    {
        public EmptyBlock(IEmitter emitter, EmptyStatement emptyStatement)
            : base(emitter, emptyStatement)
        {
            this.Emitter = emitter;
            this.EmptyStatement = emptyStatement;
        }

        public EmptyStatement EmptyStatement
        {
            get;
            set;
        }

        protected override void DoEmit()
        {
            this.WriteSemiColon(true);
        }
    }
}