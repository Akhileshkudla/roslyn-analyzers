﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ParameterValidationAnalysis
{
    using ParameterValidationAnalysisData = DictionaryAnalysisData<AbstractLocation, ParameterValidationAbstractValue>;

    /// <summary>
    /// Result from execution of <see cref="ParameterValidationAnalysis"/> on a basic block.
    /// It stores ParameterValidation values for each <see cref="AbstractLocation"/> at the start and end of the basic block.
    /// </summary>
    internal class ParameterValidationBlockAnalysisResult : AbstractBlockAnalysisResult<ParameterValidationAnalysisData>
    {
        public ParameterValidationBlockAnalysisResult(BasicBlock basicBlock, DataFlowAnalysisInfo<ParameterValidationAnalysisData> blockAnalysisData)
            : base(basicBlock, blockAnalysisData)
        {
            InputData = blockAnalysisData.Input?.Seal() ?? ImmutableDictionary<AbstractLocation, ParameterValidationAbstractValue>.Empty;
            OutputData = blockAnalysisData.Output?.Seal() ?? ImmutableDictionary<AbstractLocation, ParameterValidationAbstractValue>.Empty;
        }

        public IDictionary<AbstractLocation, ParameterValidationAbstractValue> InputData { get; }
        public IDictionary<AbstractLocation, ParameterValidationAbstractValue> OutputData { get; }
    }
}
