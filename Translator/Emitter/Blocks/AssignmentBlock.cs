﻿using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem.Implementation;

namespace Bridge.Translator
{
    public class AssignmentBlock : AbstractEmitterBlock
    {
        public AssignmentBlock(IEmitter emitter, AssignmentExpression assignmentExpression)
            : base(emitter, assignmentExpression)
        {
            this.Emitter = emitter;
            this.AssignmentExpression = assignmentExpression;
        }

        public AssignmentExpression AssignmentExpression
        {
            get;
            set;
        }

        protected override void DoEmit()
        {
            this.VisitAssignmentExpression();
        }

        protected void VisitAssignmentExpression()
        {
            AssignmentExpression assignmentExpression = this.AssignmentExpression;
            var oldAssigment = this.Emitter.IsAssignment;
            var oldAssigmentType = this.Emitter.AssignmentType;

            var delegateAssigment = false;
            bool isEvent = false;
            var initCount = this.Emitter.Writers.Count;

            var asyncExpressionHandling = this.Emitter.AsyncExpressionHandling;

            this.WriteAwaiters(assignmentExpression.Left);
            this.WriteAwaiters(assignmentExpression.Right);

            var leftResolverResult = this.Emitter.Resolver.ResolveNode(assignmentExpression.Left, this.Emitter);
            var rightResolverResult = this.Emitter.Resolver.ResolveNode(assignmentExpression.Right, this.Emitter);

            if (assignmentExpression.Operator == AssignmentOperatorType.Add ||
                assignmentExpression.Operator == AssignmentOperatorType.Subtract)
            {
                var add = assignmentExpression.Operator == AssignmentOperatorType.Add;

                if (this.Emitter.Validator.IsDelegateOrLambda(leftResolverResult))
                {
                    delegateAssigment = true;
                    var leftMemberResolveResult = leftResolverResult as MemberResolveResult;

                    if (leftMemberResolveResult != null)
                    {
                        isEvent = leftMemberResolveResult.Member is DefaultResolvedEvent;
                    }

                    if (!isEvent)
                    {
                        this.Emitter.IsAssignment = true;
                        assignmentExpression.Left.AcceptVisitor(this.Emitter);
                        this.Emitter.IsAssignment = false;
                        this.Write(" = ");
                        this.Write(Bridge.Translator.Emitter.ROOT + "." + (add ? Bridge.Translator.Emitter.DELEGATE_COMBINE : Bridge.Translator.Emitter.DELEGATE_REMOVE));
                        this.WriteOpenParentheses();
                    }
                }
            }


            this.Emitter.IsAssignment = true;
            this.Emitter.AssignmentType = assignmentExpression.Operator;
            var oldValue = this.Emitter.ReplaceAwaiterByVar;
            this.Emitter.ReplaceAwaiterByVar = true;

            bool thisAssignment = leftResolverResult is ThisResolveResult;

            if (!thisAssignment)
            {
                assignmentExpression.Left.AcceptVisitor(this.Emitter);
            }
            else
            {
                this.Write("(");
            }

            this.Emitter.ReplaceAwaiterByVar = oldValue;
            this.Emitter.AssignmentType = oldAssigmentType;
            this.Emitter.IsAssignment = oldAssigment;

            if (this.Emitter.Writers.Count == 0 && !delegateAssigment && !thisAssignment)
            {
                this.WriteSpace();
            }

            if (!delegateAssigment)
            {
                switch (assignmentExpression.Operator)
                {
                    case AssignmentOperatorType.Assign:
                        break;
                    case AssignmentOperatorType.Add:
                        this.Write("+");
                        break;
                    case AssignmentOperatorType.BitwiseAnd:
                        this.Write("&");
                        break;
                    case AssignmentOperatorType.BitwiseOr:
                        this.Write("|");
                        break;
                    case AssignmentOperatorType.Divide:
                        this.Write("/");
                        break;
                    case AssignmentOperatorType.ExclusiveOr:
                        this.Write("^");
                        break;
                    case AssignmentOperatorType.Modulus:
                        this.Write("%");
                        break;
                    case AssignmentOperatorType.Multiply:
                        this.Write("*");
                        break;
                    case AssignmentOperatorType.ShiftLeft:
                        this.Write("<<");
                        break;
                    case AssignmentOperatorType.ShiftRight:
                        this.Write(">>");
                        break;
                    case AssignmentOperatorType.Subtract:
                        this.Write("-");
                        break;
                    default:
                        throw new EmitterException(assignmentExpression, "Unsupported assignment operator: " + assignmentExpression.Operator.ToString());
                }

                int count = this.Emitter.Writers.Count;

                if (count == 0 && !thisAssignment)
                {
                    this.Write("= ");
                }
            }
            else if (!isEvent)
            {
                this.WriteComma();
            }

            oldValue = this.Emitter.ReplaceAwaiterByVar;
            this.Emitter.ReplaceAwaiterByVar = true;

            assignmentExpression.Right.AcceptVisitor(this.Emitter);

            if (thisAssignment)
            {
                this.Write(").$clone(this)");
            }

            this.Emitter.ReplaceAwaiterByVar = oldValue;
            this.Emitter.AsyncExpressionHandling = asyncExpressionHandling;

            if (this.Emitter.Writers.Count > initCount)
            {
                this.PopWriter();
            }

            if (delegateAssigment)
            {
                this.WriteCloseParentheses();
            }
        }
    }
}
