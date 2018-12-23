﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis
{
    // Dictionary that can be recycled via an object pool
    // NOTE: these dictionaries always have the default comparer.
    internal class PooledDictionary<K, V> : Dictionary<K, V>
    {
        private readonly ObjectPool<PooledDictionary<K, V>> _pool;
        private readonly int? _capacityOpt;

        private PooledDictionary(ObjectPool<PooledDictionary<K, V>> pool)
        {
            _pool = pool;
        }

        private PooledDictionary(ObjectPool<PooledDictionary<K, V>> pool, int capacity)
            : base(capacity)
        {
            _pool = pool;
            _capacityOpt = capacity;
        }

        public ImmutableDictionary<K, V> ToImmutableDictionaryAndFree()
        {
            var result = this.ToImmutableDictionary();
            this.Free();
            return result;
        }

        public void Free()
        {
            this.Clear();
            _pool?.Free(this);
        }

        // global pool
        private static readonly ObjectPool<PooledDictionary<K, V>> s_poolInstance = CreatePool();

        // if someone needs to create a pool;
        public static ObjectPool<PooledDictionary<K, V>> CreatePool()
        {
            ObjectPool<PooledDictionary<K, V>> pool = null;
            pool = new ObjectPool<PooledDictionary<K, V>>(
                (int? capacityOpt) => capacityOpt.HasValue ?
                                      new PooledDictionary<K, V>(pool, capacityOpt.Value) :
                                      new PooledDictionary<K, V>(pool), 128);
            return pool;
        }

        public static PooledDictionary<K, V> GetInstance()
        {
            var instance = s_poolInstance.Allocate();
            Debug.Assert(instance.Count == 0);
            return instance;
        }

        public static PooledDictionary<K, V> GetInstance(int capacity)
        {
            var instance = s_poolInstance.Allocate(capacity, GetCapacity);
            Debug.Assert(instance.Count == 0);
            return instance;
        }

        private static int GetCapacity(PooledDictionary<K, V> dictionary)
            => dictionary._capacityOpt.HasValue ? dictionary._capacityOpt.Value : 10;

        public static PooledDictionary<K, V> GetInstance(IDictionary<K, V> initializer)
        {
            var instance = GetInstance(initializer.Count);
            foreach (var kvp in initializer)
            {
                instance.Add(kvp.Key, kvp.Value);
            }

            return instance;
        }
    }
}
