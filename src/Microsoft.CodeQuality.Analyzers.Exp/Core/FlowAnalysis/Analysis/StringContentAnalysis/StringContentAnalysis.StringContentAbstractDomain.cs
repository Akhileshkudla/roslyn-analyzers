﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.Operations.DataFlow.StringContentAnalysis
{
    using StringContentAnalysisData = IDictionary<AnalysisEntity, StringContentAbstractValue>;

    internal partial class StringContentAnalysis : ForwardDataFlowAnalysis<StringContentAnalysisData, StringContentBlockAnalysisResult, StringContentAbstractValue>
    {
        /// <summary>
        /// Abstract value domain for <see cref="StringContentAnalysis"/> to merge and compare <see cref="StringContentAbstractValue"/> values.
        /// </summary>
        private sealed class StringContentAbstractValueDomain : AbstractValueDomain<StringContentAbstractValue>
        {
            public static StringContentAbstractValueDomain Default = new StringContentAbstractValueDomain();

            private StringContentAbstractValueDomain() { }

            public override StringContentAbstractValue Bottom => StringContentAbstractValue.UndefinedState;

            public override StringContentAbstractValue UnknownOrMayBeValue => StringContentAbstractValue.MayBeContainsNonLiteralState;

            public override int Compare(StringContentAbstractValue oldValue, StringContentAbstractValue newValue)
            {
                if (oldValue == null)
                {
                    return newValue == null ? 0 : -1;
                }
                else if (newValue == null)
                {
                    Debug.Fail("Compare");
                    return 1;
                }

                if (ReferenceEquals(oldValue, newValue))
                {
                    return 0;
                }

                if (oldValue.NonLiteralState == newValue.NonLiteralState)
                {
                    if (oldValue.IsLiteralState)
                    {
                        if (oldValue.LiteralValues.SetEquals(newValue.LiteralValues))
                        {
                            return 0;
                        }
                        else if (oldValue.LiteralValues.IsSubsetOf(newValue.LiteralValues))
                        {
                            return -1;
                        }
                        else
                        {
                            Debug.Fail("Compare");
                            return 1;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
                else if (oldValue.NonLiteralState == StringContainsNonLiteralState.Invalid ||
                    oldValue.NonLiteralState == StringContainsNonLiteralState.Undefined ||
                    newValue.NonLiteralState == StringContainsNonLiteralState.Invalid ||
                    newValue.NonLiteralState == StringContainsNonLiteralState.Undefined)
                {
                    return 0;
                }
                else if (oldValue.NonLiteralState < newValue.NonLiteralState)
                {
                    return -1;
                }
                else
                {
                    Debug.Fail("Compare");
                    return 1;
                }
            }

            public override StringContentAbstractValue Merge(StringContentAbstractValue value1, StringContentAbstractValue value2)
            {
                return value1.Merge(value2);
            }
        }
    }
}
