using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    abstract class Declaration : IStatementListItem
    {
        public IReadOnlyList<string> VarDeclaredNames()
        {
            return Utils.EmptyList<string>();
        }

        public IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return Utils.EmptyList<IScopedDeclaration>();
        }

        public abstract IReadOnlyList<string> TopLevelVarDeclaredNames();
        public abstract IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations();
        public abstract IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations();
        public abstract IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations();
        public abstract Completion Evaluate(Interpreter interpreter);
    }

    interface IDeclarationPart
    {
        IReadOnlyList<BoundName> BoundNames();
        bool IsConstantDeclaration();
    }

    struct BoundName
    {
        public readonly string name;

        public BoundName(string name)
        {
            this.name = name;
        }
    }

    abstract class HoistableDeclaration : Declaration, IDeclarationPart
    {
        public abstract IReadOnlyList<BoundName> BoundNames();
        public abstract bool IsConstantDeclaration();
    }

    class LexicalDeclaration : Declaration, IDeclarationPart
    {
        public readonly LexicalDeclarationType lexicalDeclarationType;
        public readonly IReadOnlyList<LexicalDeclarationItem> lexicalDeclarationItems;

        public LexicalDeclaration(LexicalDeclarationType lexicalDeclarationType, IReadOnlyList<LexicalDeclarationItem> lexicalDeclarationItems)
        {
            this.lexicalDeclarationType = lexicalDeclarationType;
            this.lexicalDeclarationItems = lexicalDeclarationItems;
        }

        public IReadOnlyList<BoundName> BoundNames()
        {
            return lexicalDeclarationItems.Select(i => new BoundName(i.name)).ToList();
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

        public bool IsConstantDeclaration()
        {
            return lexicalDeclarationType == LexicalDeclarationType.Const;
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return new[] { this }.ToList();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return new[] { this }.ToList();
        }

        public override IReadOnlyList<string> TopLevelVarDeclaredNames()
        {
            return Utils.EmptyList<string>();
        }

        public override IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations()
        {
            return Utils.EmptyList<IScopedDeclaration>();
        }
    }

    enum LexicalDeclarationType
    {
        Let,
        Const
    }

    class LexicalDeclarationItem
    {
        private readonly LexicalDeclarationType lexicalDeclarationType;
        public readonly string name;
        public readonly IAssignmentExpression assignmentExpression;

        public LexicalDeclarationItem(LexicalDeclarationType lexicalDeclarationType, string name, IAssignmentExpression assignmentExpression)
        {
            this.lexicalDeclarationType = lexicalDeclarationType;
            this.name = name;
            this.assignmentExpression = assignmentExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var lhs = interpreter.ResolveBinding(name);
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
            return referenceValue.InitializeReferencedBinding(value.value);
        }
    }
}
