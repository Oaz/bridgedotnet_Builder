﻿using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;

namespace Bridge.Translator
{
    public class VisitorMethodBlock : AbstractMethodBlock
    {
        public VisitorMethodBlock(IEmitter emitter, MethodDeclaration methodDeclaration)
            : base(emitter, methodDeclaration)
        {
            this.Emitter = emitter;
            this.MethodDeclaration = methodDeclaration;
        }

        public MethodDeclaration MethodDeclaration
        {
            get;
            set;
        }

        protected override void DoEmit()
        {
            this.VisitMethodDeclaration(this.MethodDeclaration);
        }

        protected void VisitMethodDeclaration(MethodDeclaration methodDeclaration)
        {
            this.EnsureComma();
            this.ResetLocals();

            var prevMap = this.BuildLocalsMap();
            var prevNamesMap = this.BuildLocalsNamesMap();

            this.AddLocals(methodDeclaration.Parameters, methodDeclaration.Body);

            var typeDef = this.Emitter.GetTypeDefinition();
            var overloads = OverloadsCollection.Create(this.Emitter, methodDeclaration);

            if (overloads.HasOverloads)
            {
                string name = overloads.GetOverloadName();
                this.Write(name);
            }
            else
            {
                this.Write(this.Emitter.GetEntityName(methodDeclaration));
            }

            this.WriteColon();

            if (methodDeclaration.TypeParameters.Count > 0)
            {
                this.WriteFunction();
                this.EmitTypeParameters(methodDeclaration.TypeParameters, methodDeclaration);
                this.WriteSpace();
                this.BeginBlock();
                this.WriteReturn(true);
            }

            this.WriteFunction();

            this.EmitMethodParameters(methodDeclaration.Parameters, methodDeclaration);

            this.WriteSpace();

            var script = this.Emitter.GetScript(methodDeclaration);

            if (script == null)
            {
                if (methodDeclaration.HasModifier(Modifiers.Async))
                {
                    new AsyncBlock(this.Emitter, methodDeclaration).Emit();
                }
                else
                {
                    methodDeclaration.Body.AcceptVisitor(this.Emitter);
                }
            }
            else
            {
                this.BeginBlock();

                foreach (var line in script)
                {
                    this.Write(line);
                    this.WriteNewLine();
                }

                this.EndBlock();
            }

            if (methodDeclaration.TypeParameters.Count > 0)
            {
                this.WriteNewLine();
                this.EndBlock();
            }

            this.ClearLocalsMap(prevMap);
            this.ClearLocalsNamesMap(prevNamesMap);
            this.Emitter.Comma = true;
        }                  
    }
}