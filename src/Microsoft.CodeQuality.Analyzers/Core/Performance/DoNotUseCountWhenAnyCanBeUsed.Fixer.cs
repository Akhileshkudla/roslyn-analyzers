﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace Microsoft.CodeQuality.Analyzers.Performance
{
    /// <summary>
    /// CA1827: Do not use Count() when Any() can be used.
    /// </summary>
    public abstract class DoNotUseCountWhenAnyCanBeUsedFixer : CodeFixProvider
    {
        /// <summary>
        /// A list of diagnostic IDs that this provider can provider fixes for.
        /// </summary>
        /// <value>The fixable diagnostic ids.</value>
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DoNotUseCountWhenAnyCanBeUsedAnalyzer.RuleId);

        /// <summary>
        /// Gets an optional <see cref="FixAllProvider" /> that can fix all/multiple occurrences of diagnostics fixed by this code fix provider.
        /// Return null if the provider doesn't support fix all/multiple occurrences.
        /// Otherwise, you can return any of the well known fix all providers from <see cref="WellKnownFixAllProviders" /> or implement your own fix all provider.
        /// </summary>
        /// <returns>FixAllProvider.</returns>
        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        /// <summary>
        /// Computes one or more fixes for the specified <see cref="CodeFixContext" />.
        /// </summary>
        /// <param name="context">A <see cref="CodeFixContext" /> containing context information about the diagnostics to fix.
        /// The context must only contain diagnostics with a <see cref="Diagnostic.Id" /> included in the <see cref="CodeFixProvider.FixableDiagnosticIds" /> 
        /// for the current provider.</param>
        /// <returns>A <see cref="Task" /> that represents the asynchronous operation.</returns>
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);

            if (node is object &&
                this.TryGetFixer(node, out var expression, out var arguments, out var negate))
            {
                context.RegisterCodeFix(
                    new DoNotUseCountWhenAnyCanBeUsedCodeAction(context.Document, node, expression, arguments, negate),
                    context.Diagnostics);
            }
        }

        /// <summary>
        /// Tries the get a fixer the specified <paramref name="node"/>.
        /// </summary>
        /// <param name="node">The node to get a fixer for.</param>
        /// <param name="expression">If this method returns <see langword="true"/>, contains the expression to be used to invoke <c>Any</c>.</param>
        /// <param name="arguments">If this method returns <see langword="true"/>, contains the arguments from <c>Any</c> to be used on <c>Count</c>.</param>
        /// <param name="negate">If this method returns <see langword="true"/>, indicates whether to negate the expression.</param>
        /// <returns><see langword="true" /> if a fixer was found., <see langword="false" /> otherwise.</returns>
        protected abstract bool TryGetFixer(SyntaxNode node, out SyntaxNode expression, out IEnumerable<SyntaxNode> arguments, out bool negate);

        private class DoNotUseCountWhenAnyCanBeUsedCodeAction : CodeAction
        {
            private readonly Document document;
            private readonly SyntaxNode pattern;
            private readonly SyntaxNode expression;
            private readonly IEnumerable<SyntaxNode> arguments;
            private readonly bool negate;

            public DoNotUseCountWhenAnyCanBeUsedCodeAction(Document document, SyntaxNode pattern, SyntaxNode expression, IEnumerable<SyntaxNode> arguments, bool negate)
            {
                this.document = document;
                this.pattern = pattern;
                this.expression = expression;
                this.arguments = arguments;
                this.negate = negate;
            }

            public override string Title { get; } = MicrosoftCodeQualityAnalyzersResources.DoNotUseCountWhenAnyCanBeUsedTitle;

            public override string EquivalenceKey { get; } = MicrosoftCodeQualityAnalyzersResources.DoNotUseCountWhenAnyCanBeUsedTitle;

            protected override async Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
            {
                var editor = await DocumentEditor.CreateAsync(this.document, cancellationToken).ConfigureAwait(false);
                var generator = editor.Generator;
                var memberAccess = generator.MemberAccessExpression(this.expression.WithoutTrailingTrivia(), "Any");
                var replacementSyntax = generator.InvocationExpression(memberAccess, arguments);

                if (this.negate)
                {
                    replacementSyntax = editor.Generator.LogicalNotExpression(replacementSyntax);
                }

                replacementSyntax = replacementSyntax
                    .WithAdditionalAnnotations(Formatter.Annotation)
                    .WithTriviaFrom(this.pattern);

                editor.ReplaceNode(this.pattern, replacementSyntax);

                return editor.GetChangedDocument();
            }
        }
    }
}
