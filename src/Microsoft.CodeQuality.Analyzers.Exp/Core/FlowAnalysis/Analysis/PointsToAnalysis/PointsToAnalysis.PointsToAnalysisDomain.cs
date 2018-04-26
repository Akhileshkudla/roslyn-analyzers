﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.Operations.DataFlow.PointsToAnalysis
{
    internal partial class PointsToAnalysis : ForwardDataFlowAnalysis<PointsToAnalysisData, PointsToBlockAnalysisResult, PointsToAbstractValue>
    {
        /// <summary>
        /// An abstract analysis domain implementation for <see cref="PointsToAnalysisData"/> tracked by <see cref="PointsToAnalysis"/>.
        /// </summary>
        private sealed class PointsToAnalysisDomain : PredicatedAnalysisDataDomain<PointsToAnalysisData, PointsToAbstractValue>
        {
            public PointsToAnalysisDomain(DefaultPointsToValueGenerator defaultPointsToValueGenerator)
                : base(new CorePointsToAnalysisDataDomain(defaultPointsToValueGenerator, PointsToAbstractValueDomainInstance))
            {
            }

            public DefaultPointsToValueGenerator DefaultPointsToValueGenerator => ((CorePointsToAnalysisDataDomain)CoreDataAnalysisDomain).DefaultPointsToValueGenerator;

            public PointsToAnalysisData MergeAnalysisDataForBackEdge(PointsToAnalysisData forwardEdgeAnalysisData, PointsToAnalysisData backEdgeAnalysisData, Func<PointsToAbstractValue, IEnumerable<AnalysisEntity>> getChildAnalysisEntities)
            {
                if (!forwardEdgeAnalysisData.IsReachableBlockData && backEdgeAnalysisData.IsReachableBlockData)
                {
                    return (PointsToAnalysisData)backEdgeAnalysisData.Clone();
                }
                else if (!backEdgeAnalysisData.IsReachableBlockData && forwardEdgeAnalysisData.IsReachableBlockData)
                {
                    return (PointsToAnalysisData)forwardEdgeAnalysisData.Clone();
                }

                var mergedCoreAnalysisData = ((CorePointsToAnalysisDataDomain)CoreDataAnalysisDomain).MergeAnalysisDataForBackEdge(forwardEdgeAnalysisData.CoreAnalysisData, backEdgeAnalysisData.CoreAnalysisData, getChildAnalysisEntities);
                return new PointsToAnalysisData(mergedCoreAnalysisData, forwardEdgeAnalysisData, backEdgeAnalysisData, CoreDataAnalysisDomain);
            }
        }
    }
}