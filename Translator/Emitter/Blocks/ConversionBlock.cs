﻿using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Semantics;
using System.Collections.Generic;

namespace Bridge.Translator
{
    public abstract class ConversionBlock : AbstractEmitterBlock
    {
        public sealed override void Emit()
        {
            var expression = this.GetExpression();

            if (expressionInWork.Contains(expression))
            {
                this.EmitConversionExpression();
                return;
            }

            expressionInWork.Add(expression);

            var isConversion = false;
            bool check = expression != null && !expression.IsNull && expression.Parent != null;
            
            if (check)
            {
                isConversion = this.CheckConversion(expression);   
            }

            if (this.DisableEmitConversionExpression)
            {
                expressionInWork.Remove(expression);
                return;
            }

            this.EmitConversionExpression();
            expressionInWork.Remove(expression);

            if (isConversion)
            {
                this.Write(")");
            }
        }

        private static List<Expression> expressionInWork = new List<Expression>();

        protected virtual bool DisableEmitConversionExpression
        {
            get;
            set;
        }

        protected virtual bool CheckConversion(Expression expression)
        {
            return ConversionBlock.CheckConversion(this, expression);
        }

        public static bool IsUserDefinedConversion(AbstractEmitterBlock block, Expression expression)
        {
            Conversion conversion = null;

            try
            {
                var rr = block.Emitter.Resolver.ResolveNode(expression, null);
                conversion = block.Emitter.Resolver.Resolver.GetConversion(expression);

                if (conversion == null)
                {
                    return false;
                }

                return conversion.IsUserDefined;
            }
            catch
            {
            }

            return false;
        }

        public static bool CheckConversion(ConversionBlock block, Expression expression)
        {
            Conversion conversion = null;
            try
            {
                var rr = block.Emitter.Resolver.ResolveNode(expression, block.Emitter);
                conversion = block.Emitter.Resolver.Resolver.GetConversion(expression);

                if (conversion == null)
                {
                    return false;
                }

                if (conversion.IsIdentityConversion)
                {
                    return false;
                }

                if (conversion.IsLifted)
                {
                    block.Write("Bridge.Nullable.lift(");
                }

                if (conversion.IsUserDefined)
                {
                    var method = conversion.Method;

                    string inline = block.Emitter.GetInline(method);

                    if (conversion.IsExplicit && !string.IsNullOrWhiteSpace(inline))
                    {
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(inline))
                    {
                        if (expression is InvocationExpression)
                        {
                            new InlineArgumentsBlock(block.Emitter, new ArgumentsInfo(block.Emitter, (InvocationExpression)expression), inline).Emit();
                        }
                        else if (expression is ObjectCreateExpression)
                        {
                            new InlineArgumentsBlock(block.Emitter, new ArgumentsInfo(block.Emitter, (InvocationExpression)expression), inline).Emit();
                        }
                        else if (expression is UnaryOperatorExpression) 
                        {
                            var unaryExpression = (UnaryOperatorExpression)expression;
                            var resolveOperator = block.Emitter.Resolver.ResolveNode(unaryExpression, block.Emitter);
                            OperatorResolveResult orr = resolveOperator as OperatorResolveResult;
                            new InlineArgumentsBlock(block.Emitter, new ArgumentsInfo(block.Emitter, unaryExpression, orr), inline).Emit();
                        }
                        else if (expression is BinaryOperatorExpression)
                        {
                            var binaryExpression = (BinaryOperatorExpression)expression;
                            var resolveOperator = block.Emitter.Resolver.ResolveNode(binaryExpression, block.Emitter);
                            OperatorResolveResult orr = resolveOperator as OperatorResolveResult;
                            new InlineArgumentsBlock(block.Emitter, new ArgumentsInfo(block.Emitter, binaryExpression, orr), inline).Emit();
                        }
                        else
                        {                            
                            new InlineArgumentsBlock(block.Emitter, new ArgumentsInfo(block.Emitter, expression), inline).Emit();
                        }

                        block.DisableEmitConversionExpression = true;

                        return false;
                    }
                    else
                    {
                        if (method.DeclaringTypeDefinition != null && block.Emitter.Validator.IsIgnoreType(method.DeclaringTypeDefinition))
                        {
                            return false;
                        }

                        block.Write(BridgeTypes.ToJsName(method.DeclaringType, block.Emitter));
                        block.WriteDot();

                        block.Write(OverloadsCollection.Create(block.Emitter, method).GetOverloadName());
                    }

                    if (conversion.IsLifted)
                    {
                        block.WriteComma();
                    }
                    else
                    {
                        block.WriteOpenParentheses();
                    }

                    return true;
                }
                else if (conversion.IsNumericConversion)
                {
                }
            }
            catch
            {
            }

            return false;
        }

        protected abstract void EmitConversionExpression();
        protected abstract Expression GetExpression();
    }
}
