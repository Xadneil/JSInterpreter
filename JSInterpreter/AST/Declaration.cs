using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    public abstract class Declaration : ParseNode, IStatementListItem, IDeclarationPart
    {
        protected Declaration(bool isStrictMode) : base(isStrictMode)
        {
        }

        public IReadOnlyList<string> VarDeclaredNames()
        {
            return Utils.EmptyList<string>();
        }

        public IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return Utils.EmptyList<IScopedDeclaration>();
        }

        public abstract IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations();
        public abstract Completion Evaluate(Interpreter interpreter);
        public abstract IReadOnlyList<string> BoundNames();
        public abstract bool IsConstantDeclaration();
    }

    public interface IDeclarationPart
    {
        IReadOnlyList<string> BoundNames();
        bool IsConstantDeclaration();
    }

    public abstract class HoistableDeclaration : Declaration, IDeclarationPart, IScopedDeclaration
    {
        protected HoistableDeclaration(bool isStrictMode) : base(isStrictMode)
        {
        }
    }

    public sealed class LexicalDeclaration : Declaration
    {
        public readonly LexicalDeclarationType lexicalDeclarationType;
        public readonly IReadOnlyList<LexicalDeclarationItem> lexicalDeclarationItems;

        public LexicalDeclaration(LexicalDeclarationType lexicalDeclarationType, IReadOnlyList<LexicalDeclarationItem> lexicalDeclarationItems, bool isStrictMode) : base(isStrictMode)
        {
            this.lexicalDeclarationType = lexicalDeclarationType;
            this.lexicalDeclarationItems = lexicalDeclarationItems;
        }

        public override IReadOnlyList<string> BoundNames()
        {
            return lexicalDeclarationItems.Select(i => i.name).ToList();
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            foreach (var i in lexicalDeclarationItems)
            {
                var completion = i.Evaluate(interpreter);
                if (completion.IsAbrupt()) return completion;
            }
            return Completion.NormalCompletion();
        }

        public override bool IsConstantDeclaration()
        {
            return lexicalDeclarationType == LexicalDeclarationType.Const;
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return new[] { this }.ToList();
        }
    }

    public enum LexicalDeclarationType
    {
        Let,
        Const
    }

    public sealed class LexicalDeclarationItem : ParseNode
    {
        private readonly LexicalDeclarationType lexicalDeclarationType;
        public readonly string name;
        public readonly AbstractAssignmentExpression? assignmentExpression;

        public LexicalDeclarationItem(LexicalDeclarationType lexicalDeclarationType, string name, AbstractAssignmentExpression? assignmentExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.lexicalDeclarationType = lexicalDeclarationType;
            this.name = name;
            this.assignmentExpression = assignmentExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var lhs = interpreter.ResolveBinding(name, IsStrictMode);
            if (lhs.IsAbrupt()) return lhs;
            if (!(lhs.value is ReferenceValue referenceValue))
                throw new InvalidOperationException("ResolveBinding didn't return a reference");

            if (assignmentExpression == null)
            {
                if (lexicalDeclarationType == LexicalDeclarationType.Const)
                    throw new InvalidOperationException("LexicalDeclarationItem.Evaluate: a const declaration must have an initializer.");
                return referenceValue.InitializeReferencedBinding(UndefinedValue.Instance);
            }
            Completion value;
            if (assignmentExpression is FunctionExpression functionExpression && functionExpression.isAnonymous)
                value = functionExpression.NamedEvaluate(interpreter, name);
            else
                value = assignmentExpression.Evaluate(interpreter).GetValue();
            if (value.IsAbrupt()) return value;
            return referenceValue.InitializeReferencedBinding(value.value!);
        }
    }
}
