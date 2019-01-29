﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow
{
    /// <summary>
    /// An abstract domain implementation for analyses that store dictionary typed data.
    /// </summary>
    internal class MapAbstractDomain<TKey, TValue> : AbstractAnalysisDomain<DictionaryAnalysisData<TKey, TValue>>
        where TKey: IEquatable<TKey>
    {
        public MapAbstractDomain(AbstractValueDomain<TValue> valueDomain)
        {
            ValueDomain = valueDomain;
        }

        protected AbstractValueDomain<TValue> ValueDomain { get; }
        public override DictionaryAnalysisData<TKey, TValue> Clone(DictionaryAnalysisData<TKey, TValue> value) => new DictionaryAnalysisData<TKey, TValue>(value);

        /// <summary>
        /// Compares if the abstract dataflow values in <paramref name="oldValue"/> against the values in <paramref name="newValue"/> to ensure
        /// dataflow function is a monotically increasing function. See https://en.wikipedia.org/wiki/Monotonic_function for understanding monotonic functions.
        /// <returns>
        /// 1) 0, if both the dictionaries are identical.
        /// 2) -1, if dictionaries are not identical and for every key in <paramref name="oldValue"/>, the corresponding key exists in <paramref name="newValue"/> and
        ///    the value of each such key in <paramref name="oldValue"/> is lesser than or equals the value in <paramref name="newValue"/>.
        /// 3) 1, otherwise.
        /// </returns>
        public sealed override int Compare(DictionaryAnalysisData<TKey, TValue> oldValue, DictionaryAnalysisData<TKey, TValue> newValue)
            => Compare(oldValue, newValue, assertMonotonicity: true);

        public sealed override bool Equals(DictionaryAnalysisData<TKey, TValue> value1, DictionaryAnalysisData<TKey, TValue> value2)
            => Compare(value1, value2, assertMonotonicity: false) == 0;

        private int Compare(DictionaryAnalysisData<TKey, TValue> oldValue, DictionaryAnalysisData<TKey, TValue> newValue, bool assertMonotonicity)
        {
            Debug.Assert(oldValue != null);
            Debug.Assert(newValue != null);

            if (ReferenceEquals(oldValue, newValue))
            {
                return 0;
            }

            // Ensure that every key in oldValue exists in newValue and the value corresponding to that key
            // is not greater in oldValue as compared to the value in newValue
            bool newValueIsBigger = false;
            foreach (var kvp in oldValue)
            {
                var key = kvp.Key;
                var value = kvp.Value;
                if (!newValue.TryGetValue(key, out TValue otherValue))
                {
                    FireNonMonotonicAssertIfNeeded(assertMonotonicity);
                    return 1;
                }

                var result = ValueDomain.Compare(value, otherValue, assertMonotonicity);

                if (result > 0)
                {
                    FireNonMonotonicAssertIfNeeded(assertMonotonicity);
                    return 1;
                }
                else if (result < 0)
                {
                    newValueIsBigger = true;
                }
            }

            if (!newValueIsBigger)
            {
                newValueIsBigger = newValue.Count > oldValue.Count;
            }

            return newValueIsBigger ? -1 : 0;
        }

#pragma warning disable CA1030 // Use events where appropriate
        [Conditional("DEBUG")]
        private static void FireNonMonotonicAssertIfNeeded(bool assertMonotonicity)
        {
            if (assertMonotonicity)
            {
                Debug.Fail("Non-monotonic merge");
            }
        }
#pragma warning restore CA1030 // Use events where appropriate

        public override DictionaryAnalysisData<TKey, TValue> Merge(DictionaryAnalysisData<TKey, TValue> map1, DictionaryAnalysisData<TKey, TValue> map2)
        {
            Debug.Assert(map1 != null);
            Debug.Assert(map2 != null);

            var result = new DictionaryAnalysisData<TKey, TValue>(map1);
            var keysToMerge = map1.GetKeysToMerge(map2);
            try
            {
                foreach (var key in keysToMerge)
                {
                    if (map1.TryGetValue(key, out var value1))
                    {
                        if (map2.TryGetValue(key, out var value2))
                        {
                            result[key] = ValueDomain.Merge(value1, value2);
                        }
                    }
                    else if (map2.TryGetValue(key, out var value2))
                    {
                        result[key] = value2;
                    }
                }

                Debug.Assert(Compare(map1, result) <= 0);
                Debug.Assert(Compare(map2, result) <= 0);
                return result;
            }
            finally
            {
                keysToMerge.Free();
            }
        }
    }
}