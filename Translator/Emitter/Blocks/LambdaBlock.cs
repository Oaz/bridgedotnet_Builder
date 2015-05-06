﻿using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using System.Collections.Generic;

namespace Bridge.Translator
{
    public class LambdaBlock : AbstractMethodBlock
    {
        public LambdaBlock(IEmitter emitter, LambdaExpression lambdaExpression)
            : this(emitter, lambdaExpression.Parameters, lambdaExpression.Body, lambdaExpression, lambdaExpression.IsAsync)
        {
        }

        public LambdaBlock(IEmitter emitter, AnonymousMethodExpression anonymousMethodExpression)
            : this(emitter, anonymousMethodExpression.Parameters, anonymousMethodExpression.Body, anonymousMethodExpression, anonymousMethodExpression.IsAsync)
        {
        }

        public LambdaBlock(IEmitter emitter, IEnumerable<ParameterDeclaration> parameters, AstNode body, AstNode context, bool isAsync)
            : base(emitter, context)
        {
            this.Emitter = emitter;
            this.Parameters = parameters;
            this.Body = body;
            this.Context = context;
            this.IsAsync = isAsync;
        }

        public bool IsAsync
        {
            get;
            set;
        }

        public IEnumerable<ParameterDeclaration> Parameters
        {
            get;
            set;
        }

        public AstNode Body
        {
            get;
            set;
        }

        public AstNode Context
        {
            get;
            set;
        }

        protected bool PreviousIsAync
        {
            get;
            set;
        }

        protected List<string> PreviousAsyncVariables
        {
            get;
            set;
        }

        protected IAsyncBlock PreviousAsyncBlock
        {
            get;
            set;
        }

        public bool ReplaceAwaiterByVar
        {
            get;
            set;
        }

        protected override void DoEmit()
        {
            var oldVars = this.Emitter.TempVariables;
            this.Emitter.TempVariables = new Dictionary<string, bool>();
            this.PreviousIsAync = this.Emitter.IsAsync;
            this.Emitter.IsAsync = this.IsAsync;

            this.PreviousAsyncVariables = this.Emitter.AsyncVariables;
            this.Emitter.AsyncVariables = null;

            this.PreviousAsyncBlock = this.Emitter.AsyncBlock;
            this.Emitter.AsyncBlock = null;

            this.ReplaceAwaiterByVar = this.Emitter.ReplaceAwaiterByVar;
            this.Emitter.ReplaceAwaiterByVar = false;

            this.EmitLambda(this.Parameters, this.Body, this.Context);

            this.Emitter.IsAsync = this.PreviousIsAync;
            this.Emitter.AsyncVariables = this.PreviousAsyncVariables;
            this.Emitter.AsyncBlock = this.PreviousAsyncBlock;
            this.Emitter.ReplaceAwaiterByVar = this.ReplaceAwaiterByVar;
            this.Emitter.TempVariables = oldVars;
        }

        protected virtual void EmitLambda(IEnumerable<ParameterDeclaration> parameters, AstNode body, AstNode context)
        {
            AsyncBlock asyncBlock = null;
            this.PushLocals();

            if (this.IsAsync)
            {
                if (context is LambdaExpression)
                {
                    asyncBlock = new AsyncBlock(this.Emitter, (LambdaExpression)context);
                }
                else
                {
                    asyncBlock = new AsyncBlock(this.Emitter, (AnonymousMethodExpression)context);
                }

                asyncBlock.InitAsyncBlock();
            }

            var prevMap = this.BuildLocalsMap();
            var prevNamesMap = this.BuildLocalsNamesMap();
            this.AddLocals(parameters, body);


            bool block = body is BlockStatement;
            this.Write("");

            var savedPos = this.Emitter.Output.Length;
            var savedThisCount = this.Emitter.ThisRefCounter;

            this.WriteFunction();
            this.EmitMethodParameters(parameters, context);
            this.WriteSpace();

            if (!block && !this.IsAsync)
            {
                this.BeginBlock();
            }

            bool isSimpleLambda = body.Parent is LambdaExpression && !block && !this.IsAsync;

            if (isSimpleLambda)
            {
                this.ConvertParamsToReferences(parameters);
                this.WriteReturn(true);
            }

            if (this.IsAsync)
            {
                asyncBlock.Emit(true);
            }
            else
            {
                body.AcceptVisitor(this.Emitter);
            }

            if (isSimpleLambda)
            {
                this.WriteSemiColon();
            }

            if (!block && !this.IsAsync)
            {
                this.WriteNewLine();
                this.EndBlock();
            }

            if (this.Emitter.ThisRefCounter > savedThisCount)
            {
                this.Emitter.Output.Insert(savedPos, Bridge.Translator.Emitter.ROOT + "." + Bridge.Translator.Emitter.DELEGATE_BIND + "(this, ");
                this.WriteCloseParentheses();
            }


            this.PopLocals();
            this.ClearLocalsMap(prevMap);
            this.ClearLocalsNamesMap(prevNamesMap);
        }
    }
}