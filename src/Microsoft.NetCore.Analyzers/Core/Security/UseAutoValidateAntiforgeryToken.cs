﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseAutoValidateAntiforgeryToken : DiagnosticAnalyzer
    {
        internal static DiagnosticDescriptor UseAutoValidateAntiforgeryTokenRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5391",
            typeof(MicrosoftNetCoreAnalyzersResources),
            nameof(MicrosoftNetCoreAnalyzersResources.UseAutoValidateAntiforgeryToken),
            nameof(MicrosoftNetCoreAnalyzersResources.UseAutoValidateAntiforgeryTokenMessage),
            DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
            helpLinkUri: null,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.UseAutoValidateAntiforgeryTokenDescription),
            customTags: WellKnownDiagnosticTags.Telemetry);
        internal static DiagnosticDescriptor MissHttpVerbAttributeRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5395",
            typeof(MicrosoftNetCoreAnalyzersResources),
            nameof(MicrosoftNetCoreAnalyzersResources.MissHttpVerbAttribute),
            nameof(MicrosoftNetCoreAnalyzersResources.MissHttpVerbAttributeMessage),
            false,
            helpLinkUri: null,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.MissHttpVerbAttributeDescription),
            customTags: WellKnownDiagnosticTags.Telemetry);

        private static readonly Regex s_AntiForgeryAttributeRegex = new Regex("^[a-zA-Z]*Validate[a-zA-Z]*Anti[Ff]orgery[a-zA-Z]*Attribute$", RegexOptions.Compiled);
        private static readonly Regex s_AntiForgeryRegex = new Regex("^[a-zA-Z]*Validate[a-zA-Z]*Anti[Ff]orgery[a-zA-Z]*$", RegexOptions.Compiled);
        private static readonly ImmutableHashSet<string> HttpVerbAttributesMarkingOnActionModifyingMethods =
            ImmutableHashSet.Create(
                StringComparer.Ordinal,
                WellKnownTypeNames.MicrosoftAspNetCoreMvcHttpPostAttribute,
                WellKnownTypeNames.MicrosoftAspNetCoreMvcHttpPutAttribute,
                WellKnownTypeNames.MicrosoftAspNetCoreMvcHttpDeleteAttribute,
                WellKnownTypeNames.MicrosoftAspNetCoreMvcHttpPatchAttribute);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            UseAutoValidateAntiforgeryTokenRule,
            MissHttpVerbAttributeRule);

        public delegate bool RequirementsOfValidateMethod(IMethodSymbol methodSymbol);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
            {
                var compilationDataProvider = CompilationDataProviderFactory.CreateProvider(compilationStartAnalysisContext);
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationDataProvider);

                if (!wellKnownTypeProvider.TryGetTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcFiltersFilterCollection, out var filterCollectionTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcController, out var controllerTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcControllerBase, out var controllerBaseTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcNonActionAttribute, out var nonActionAttributeTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcRoutingHttpMethodAttribute, out var httpMethodAttributeTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcFiltersIFilterMetadata, out var iFilterMetadataTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreAntiforgeryIAntiforgery, out var iAntiforgeryTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcFiltersIAsyncAuthorizationFilter, out var iAsyncAuthorizationFilterTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcFiltersIAuthorizationFilter, out var iAuthorizationFilterTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask, out var taskTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcFiltersAuthorizationFilterContext, out var authorizationFilterContextTypeSymbol))
                {
                    return;
                }

                var httpVerbAttributeTypeSymbolsAbleToModify = HttpVerbAttributesMarkingOnActionModifyingMethods.Select(
                    s => wellKnownTypeProvider.TryGetTypeByMetadataName(s, out var attributeTypeSymbol) ? attributeTypeSymbol : null);

                if (httpVerbAttributeTypeSymbolsAbleToModify.Any(s => s == null))
                {
                    return;
                }

                // A dictionary from method symbol to set of methods calling it directly.
                // The bool value in the sub ConcurrentDictionary is not used, use ConcurrentDictionary rather than HashSet just for the concurrency security.
                var inverseGraph = new ConcurrentDictionary<ISymbol, ConcurrentDictionary<ISymbol, bool>>();

                // Ignore cases where a global anti forgery filter is in use.
                var hasGlobalAntiForgeryFilter = false;

                // Verify that validate anti forgery token attributes are used somewhere within this project,
                // to avoid reporting false positives on projects that use an alternative approach to mitigate CSRF issues.
                var usingValidateAntiForgeryAttribute = false;
                var onAuthorizationAsyncMethodSymbols = new HashSet<IMethodSymbol>();
                var actionMethodSymbols = new HashSet<(IMethodSymbol, string)>();
                var actionMethodNeedAddingHttpVerbAttributeSymbols = new HashSet<IMethodSymbol>();

                // Constructing inverse callGraph.
                // When it comes to delegate function assignment Del handler = DelegateMethod;, inverse call Graph will add:
                // (1) key: method gets called in DelegateMethod, value: handler.
                // When it comes to calling delegate function handler(), inverse callGraph will add:
                // (1) key: delegate function handler, value: callerMethod.
                // (2) key: Invoke(), value: callerMethod.
                compilationStartAnalysisContext.RegisterOperationBlockStartAction(
                    (OperationBlockStartAnalysisContext operationBlockStartAnalysisContext) =>
                    {
                        if (hasGlobalAntiForgeryFilter)
                        {
                            return;
                        }

                        var owningSymbol = operationBlockStartAnalysisContext.OwningSymbol;
                        inverseGraph.GetOrAdd(owningSymbol, (_) => new ConcurrentDictionary<ISymbol, bool>());

                        operationBlockStartAnalysisContext.RegisterOperationAction(operationContext =>
                        {
                            ISymbol calledSymbol = null;
                            ConcurrentDictionary<ISymbol, bool> callers = null;

                            switch (operationContext.Operation)
                            {
                                case IInvocationOperation invocationOperation:
                                    calledSymbol = invocationOperation.TargetMethod.OriginalDefinition;

                                    break;

                                case IFieldReferenceOperation fieldReferenceOperation:
                                    var fieldSymbol = (IFieldSymbol)fieldReferenceOperation.Field;

                                    if (fieldSymbol.Type.TypeKind == TypeKind.Delegate)
                                    {
                                        calledSymbol = fieldSymbol;

                                        break;
                                    }

                                    return;
                            }

                            if (calledSymbol == null)
                            {
                                return;
                            }

                            callers = inverseGraph.GetOrAdd(calledSymbol, (_) => new ConcurrentDictionary<ISymbol, bool>());
                            callers.TryAdd(owningSymbol, true);
                        }, OperationKind.Invocation, OperationKind.FieldReference);
                    });

                // Holds if the project has a global anti forgery filter.
                compilationStartAnalysisContext.RegisterOperationAction(operationAnalysisContext =>
                {
                    if (hasGlobalAntiForgeryFilter)
                    {
                        return;
                    }

                    var invocationOperation = (IInvocationOperation)operationAnalysisContext.Operation;
                    var methodSymbol = invocationOperation.TargetMethod;

                    if (methodSymbol.Name == "Add" &&
                        methodSymbol.ContainingType.GetBaseTypesAndThis().Contains(filterCollectionTypeSymbol))
                    {
                        var potentialAntiForgeryFilters = invocationOperation
                            .Arguments
                            .Where(s => s.Parameter.Name == "filterType")
                            .Select(s => s.Value)
                            .OfType<ITypeOfOperation>()
                            .Select(s => s.TypeOperand)
                            .Union(methodSymbol.TypeArguments);

                        foreach (var potentialAntiForgeryFilter in potentialAntiForgeryFilters)
                        {
                            if (potentialAntiForgeryFilter.AllInterfaces.Contains(iFilterMetadataTypeSymbol) &&
                                s_AntiForgeryRegex.IsMatch(potentialAntiForgeryFilter.Name))
                            {
                                hasGlobalAntiForgeryFilter = true;

                                return;
                            }
                            else if (potentialAntiForgeryFilter.AllInterfaces.Contains(iAsyncAuthorizationFilterTypeSymbol) ||
                                potentialAntiForgeryFilter.AllInterfaces.Contains(iAuthorizationFilterTypeSymbol))
                            {
                                onAuthorizationAsyncMethodSymbols.Add(
                                    potentialAntiForgeryFilter
                                    .GetMembers()
                                    .OfType<IMethodSymbol>()
                                    .FirstOrDefault(
                                        s => (s.Name == "OnAuthorizationAsync" ||
                                            s.Name == "OnAuthorization") &&
                                            s.ReturnType.Equals(taskTypeSymbol) &&
                                            s.Parameters.Length == 1 &&
                                            s.Parameters[0].Type.Equals(authorizationFilterContextTypeSymbol)));
                            }
                        }
                    }
                }, OperationKind.Invocation);

                compilationStartAnalysisContext.RegisterSymbolAction(symbolAnalysisContext =>
                {
                    if (hasGlobalAntiForgeryFilter)
                    {
                        return;
                    }

                    var derivedControllerTypeSymbol = (INamedTypeSymbol)symbolAnalysisContext.Symbol;
                    var baseTypes = derivedControllerTypeSymbol.GetBaseTypes();

                    // An subtype of `Microsoft.AspNetCore.Mvc.Controller` or `Microsoft.AspNetCore.Mvc.ControllerBase`).
                    if (baseTypes.Contains(controllerTypeSymbol) ||
                        baseTypes.Contains(controllerBaseTypeSymbol))
                    {
                        // The controller class is not protected by a validate anti forgery token attribute.
                        if (!IsUsingAntiFogeryAttribute(derivedControllerTypeSymbol))
                        {
                            foreach (var actionMethodSymbol in derivedControllerTypeSymbol.GetMembers().OfType<IMethodSymbol>())
                            {
                                if (actionMethodSymbol.MethodKind == MethodKind.Constructor)
                                {
                                    continue;
                                }

                                if (actionMethodSymbol.IsPublic() &&
                                    !actionMethodSymbol.IsStatic)
                                {
                                    var hasNonActionAttribute = actionMethodSymbol.HasAttribute(nonActionAttributeTypeSymbol);
                                    var overridenMethodSymbol = actionMethodSymbol as ISymbol;

                                    while (!hasNonActionAttribute && overridenMethodSymbol.IsOverride)
                                    {
                                        overridenMethodSymbol = overridenMethodSymbol.GetOverriddenMember();

                                        if (overridenMethodSymbol.HasAttribute(nonActionAttributeTypeSymbol))
                                        {
                                            hasNonActionAttribute = true;
                                        }
                                    }

                                    // The method has [NonAction].
                                    if (hasNonActionAttribute)
                                    {
                                        continue;
                                    }

                                    // The method is not protected by a validate anti forgery token attribute.
                                    if (!IsUsingAntiFogeryAttribute(actionMethodSymbol))
                                    {
                                        var httpVerbAttributeTypeSymbolAbleToModify = actionMethodSymbol.GetAttributes().FirstOrDefault(s => httpVerbAttributeTypeSymbolsAbleToModify.Contains(s.AttributeClass));

                                        if (httpVerbAttributeTypeSymbolAbleToModify != null)
                                        {
                                            var attributeName = httpVerbAttributeTypeSymbolAbleToModify.AttributeClass.Name;
                                            actionMethodSymbols.Add(
                                                (actionMethodSymbol,
                                                attributeName.EndsWith("Attribute", StringComparison.Ordinal) ? attributeName.Remove(attributeName.Length - "Attribute".Length) : attributeName));
                                        }
                                        else if (!actionMethodSymbol.GetAttributes().Any(s => s.AttributeClass.GetBaseTypes().Contains(httpMethodAttributeTypeSymbol)))
                                        {
                                            actionMethodNeedAddingHttpVerbAttributeSymbols.Add((actionMethodSymbol));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }, SymbolKind.NamedType);

                compilationStartAnalysisContext.RegisterCompilationEndAction(
                (CompilationAnalysisContext compilationAnalysisContext) =>
                {
                    if (usingValidateAntiForgeryAttribute && !hasGlobalAntiForgeryFilter && (actionMethodSymbols.Any() || actionMethodNeedAddingHttpVerbAttributeSymbols.Any()))
                    {
                        var visited = new HashSet<ISymbol>();
                        var results = new Dictionary<ISymbol, HashSet<ISymbol>>();

                        if (onAuthorizationAsyncMethodSymbols.Any())
                        {
                            foreach (var calleeMethod in inverseGraph.Keys)
                            {
                                if (calleeMethod.Name == "ValidateRequestAsync" &&
                                    (calleeMethod.ContainingType.AllInterfaces.Contains(iAntiforgeryTypeSymbol) ||
                                    calleeMethod.ContainingType.Equals(iAntiforgeryTypeSymbol)))
                                {
                                    FindAllTheSpecifiedCalleeMethods(calleeMethod, visited, results);

                                    if (results.Values.Any(s => s.Any()))
                                    {
                                        return;
                                    }
                                }
                            }
                        }

                        foreach (var (methodSymbol, attributeName) in actionMethodSymbols)
                        {
                            compilationAnalysisContext.ReportDiagnostic(
                                methodSymbol.CreateDiagnostic(
                                    UseAutoValidateAntiforgeryTokenRule,
                                    methodSymbol.Name,
                                    attributeName));
                        }

                        foreach (var methodSymbol in actionMethodNeedAddingHttpVerbAttributeSymbols)
                        {
                            compilationAnalysisContext.ReportDiagnostic(
                                methodSymbol.CreateDiagnostic(
                                    MissHttpVerbAttributeRule,
                                    methodSymbol.Name));
                        }
                    }
                });

                // <summary>
                // Analyze the method to find all the specified methods that call it, in this case, the specified method symbols are in onAuthorizationAsyncMethodSymbols.
                // </summary>
                // <param name="methodSymbol">The symbol of the method to be analyzed</param>
                // <param name="visited">All the method has been analyzed</param>
                // <param name="results">The result is organized by &lt;method to be analyzed, specified methods calling it&gt;</param>
                void FindAllTheSpecifiedCalleeMethods(ISymbol methodSymbol, HashSet<ISymbol> visited, Dictionary<ISymbol, HashSet<ISymbol>> results)
                {
                    if (visited.Add(methodSymbol))
                    {
                        results.Add(methodSymbol, new HashSet<ISymbol>());

                        if (!inverseGraph.TryGetValue(methodSymbol, out var callingMethods))
                        {
                            Debug.Fail(methodSymbol.Name + " was not found in inverseGraph.");

                            return;
                        }

                        foreach (var child in callingMethods.Keys)
                        {
                            if (onAuthorizationAsyncMethodSymbols.Contains(child))
                            {
                                results[methodSymbol].Add(child);
                            }

                            FindAllTheSpecifiedCalleeMethods(child, visited, results);

                            if (results.TryGetValue(child, out var result))
                            {
                                results[methodSymbol].UnionWith(result);
                            }
                            else
                            {
                                Debug.Fail(child.Name + " was not found in results.");
                            }
                        }
                    }
                }

                bool IsUsingAntiFogeryAttribute(ISymbol symbol)
                {
                    if (symbol.GetAttributes().Any(s => s_AntiForgeryAttributeRegex.IsMatch(s.AttributeClass.Name)))
                    {
                        usingValidateAntiForgeryAttribute = true;

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            });
        }
    }
}
