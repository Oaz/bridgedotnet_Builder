﻿using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using System.Linq;

namespace Bridge.Translator
{
    public class Block : AbstractEmitterBlock
    {
        public Block(IEmitter emitter, BlockStatement blockStatement)
        {
            this.Emitter = emitter;
            this.BlockStatement = blockStatement;

            if (this.Emitter.IgnoreBlock == blockStatement)
            {
                this.AsyncNoBraces = true;
            }

            if (this.Emitter.NoBraceBlock == blockStatement)
            {
                this.NoBraces = true;
            }
        }

        public BlockStatement BlockStatement 
        { 
            get; 
            set; 
        }

        protected bool AddEndBlock
        {
            get;
            set;
        }

        public bool AsyncNoBraces
        {
            get;
            set;
        }

        public bool NoBraces
        {
            get;
            set;
        }

        public bool? WrapByFn
        {
            get;
            set;
        }

        public int BeginPosition
        {
            get;
            set;
        }

        public bool IsMethodBlock
        {
            get;
            set;
        }

        public override void Emit()
        {
            if ((!this.WrapByFn.HasValue || this.WrapByFn.Value) && (this.BlockStatement.Parent is ForStatement ||
                     this.BlockStatement.Parent is ForeachStatement ||
                     this.BlockStatement.Parent is WhileStatement ||
                     this.BlockStatement.Parent is DoWhileStatement))
            {
                var visitor = new LambdaVisitor();
                this.BlockStatement.AcceptVisitor(visitor);

                this.WrapByFn = visitor.LambdaExpression.Count > 0;
            }

            this.EmitBlock();
        }

        protected virtual bool KeepLineAfterBlock(BlockStatement block)
        {
            var parent = block.Parent;

            if (this.AsyncNoBraces || this.NoBraces)
            {
                return true;
            }

            if (parent is AnonymousMethodExpression)
            {
                return true;
            }

            if (parent is LambdaExpression)
            {
                return true;
            }

            if (parent is MethodDeclaration)
            {
                return true;
            }

            if (parent is OperatorDeclaration)
            {
                return true;
            }
            
            if (parent is Accessor && (parent.Parent is PropertyDeclaration || parent.Parent is CustomEventDeclaration || parent.Parent is IndexerDeclaration))
            {
                return true;
            }

            var loop = parent as DoWhileStatement;

            if (loop != null)
            {
                return true;
            }

            return false;
        }

        public void EmitBlock()
        {
            this.BeginEmitBlock();
            this.DoEmitBlock();
            this.EndEmitBlock();
        }

        public void DoEmitBlock()
        {
            if (this.BlockStatement.Parent is MethodDeclaration)
            {
                this.IsMethodBlock = true;
                this.ConvertParamsToReferences(((MethodDeclaration)this.BlockStatement.Parent).Parameters);
            }
            else if (this.BlockStatement.Parent is AnonymousMethodExpression)
            {
                this.IsMethodBlock = true;
                this.ConvertParamsToReferences(((AnonymousMethodExpression)this.BlockStatement.Parent).Parameters);
            }
            else if (this.BlockStatement.Parent is LambdaExpression)
            {
                this.IsMethodBlock = true;
                this.ConvertParamsToReferences(((LambdaExpression)this.BlockStatement.Parent).Parameters);
            }
            else if (this.BlockStatement.Parent is ConstructorDeclaration)
            {
                this.IsMethodBlock = true;
                this.ConvertParamsToReferences(((ConstructorDeclaration)this.BlockStatement.Parent).Parameters);
            }
            else if (this.BlockStatement.Parent is OperatorDeclaration)
            {
                this.IsMethodBlock = true;
                this.ConvertParamsToReferences(((OperatorDeclaration)this.BlockStatement.Parent).Parameters);
            }
            else if (this.BlockStatement.Parent is Accessor)
            {
                this.IsMethodBlock = true;

                if (this.BlockStatement.Parent.Role.ToString() == "Setter")
                {
                    this.ConvertParamsToReferences(new ParameterDeclaration[] { new ParameterDeclaration { Name = "value" } });
                }
            }

            this.BlockStatement.Children.ToList().ForEach(child => child.AcceptVisitor(this.Emitter));
        }

        public void EndEmitBlock()
        {
            var blockWasEnded = false;

            if (!this.NoBraces && (!this.Emitter.IsAsync || (!this.AsyncNoBraces && this.BlockStatement.Parent != this.Emitter.AsyncBlock.Node)))
            {
                this.EndBlock();
                blockWasEnded = true;
            }            

            if (this.AddEndBlock)
            {
                this.WriteNewLine();
                this.EndBlock();
                blockWasEnded = true;
            }

            if (blockWasEnded && this.WrapByFn.HasValue && this.WrapByFn.Value)
            {
                this.Outdent();
                this.Write(").call(this);");
            }

            if (!this.KeepLineAfterBlock(this.BlockStatement))
            {
                this.WriteNewLine();
            }

            if (this.IsMethodBlock && !this.Emitter.IsAsync)
            {
                this.EmitTempVars(this.BeginPosition);
            }

            this.PopLocals();
        }

        public void BeginEmitBlock()
        {
            this.PushLocals();

            if (!this.NoBraces && (!this.Emitter.IsAsync || (!this.AsyncNoBraces && this.BlockStatement.Parent != this.Emitter.AsyncBlock.Node)))
            {
                if (this.WrapByFn.HasValue && this.WrapByFn.Value)
                {
                    this.WriteNewLine();
                    this.Indent();
                    this.Write("(function () ");
                }

                this.BeginBlock();
            }

            this.BeginPosition = this.Emitter.Output.Length;
        }        
    }
}
