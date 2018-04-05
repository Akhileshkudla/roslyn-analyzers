﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.CodeAnalysis.Operations.DataFlow.PointsToAnalysis
{
    using PointsToAnalysisData = IDictionary<AnalysisEntity, PointsToAbstractValue>;

    internal partial class PointsToAnalysis : ForwardDataFlowAnalysis<PointsToAnalysisData, PointsToBlockAnalysisResult, PointsToAbstractValue>
    {
        /// <summary>
        /// Abstract value domain for <see cref="PointsToAnalysis"/> to merge and compare <see cref="PointsToAbstractValue"/> values.
        /// </summary>
        private class PointsToAbstractValueDomain : AbstractValueDomain<PointsToAbstractValue>
        {
            public static PointsToAbstractValueDomain Default = new PointsToAbstractValueDomain();
            private readonly SetAbstractDomain<AbstractLocation> _locationsDomain = new SetAbstractDomain<AbstractLocation>();

            private PointsToAbstractValueDomain() { }

            public override PointsToAbstractValue Bottom => PointsToAbstractValue.Undefined;

            public override PointsToAbstractValue UnknownOrMayBeValue => PointsToAbstractValue.Unknown;

            public override int Compare(PointsToAbstractValue oldValue, PointsToAbstractValue newValue)
            {
                if (oldValue == null)
                {
                    return newValue == null ? 0 : -1;
                }
                else if (newValue == null)
                {
                    return 1;
                }

                if (ReferenceEquals(oldValue, newValue))
                {
                    return 0;
                }

                if (oldValue.Kind == newValue.Kind)
                {
                    return _locationsDomain.Compare(oldValue.Locations, newValue.Locations);
                }
                else if (oldValue.Kind < newValue.Kind)
                {
                    return -1;
                }
                else
                {
                    return 1;
                }
            }

            public override PointsToAbstractValue Merge(PointsToAbstractValue value1, PointsToAbstractValue value2)
            {
                if (value1 == null)
                {
                    return value2;
                }
                else if (value2 == null)
                {
                    return value1;
                }
                else if (value1 == value2)
                {
                    return value1;
                }
                else if (value1.Kind == PointsToAbstractValueKind.Undefined ||
                    value1.Kind == PointsToAbstractValueKind.Invalid)
                {
                    return value2;
                }
                else if (value2.Kind == PointsToAbstractValueKind.Undefined ||
                    value2.Kind == PointsToAbstractValueKind.Invalid)
                {
                    return value1;
                }
                else if (value1.Kind == PointsToAbstractValueKind.NoLocation ||
                    value2.Kind == PointsToAbstractValueKind.NoLocation ||
                    value1.Kind == PointsToAbstractValueKind.Unknown ||
                    value2.Kind == PointsToAbstractValueKind.Unknown)
                {
                    return PointsToAbstractValue.Unknown;
                }

                var mergedLocations = _locationsDomain.Merge(value1.Locations, value2.Locations);
                return PointsToAbstractValue.Create(mergedLocations);
            }
        }
    }
}
