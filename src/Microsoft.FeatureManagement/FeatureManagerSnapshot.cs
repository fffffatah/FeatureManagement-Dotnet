﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.FeatureManagement
{
    /// <summary>
    /// Provides a snapshot of feature state to ensure consistency across a given request.
    /// </summary>
    class FeatureManagerSnapshot : IFeatureManagerSnapshot, IVariantFeatureManagerSnapshot
    {
        private readonly FeatureManager _featureManager;
        private readonly ConcurrentDictionary<string, Task<bool>> _flagCache = new ConcurrentDictionary<string, Task<bool>>();
        private readonly IDictionary<string, Variant> _variantCache = new Dictionary<string, Variant>();
        private IEnumerable<string> _featureNames;

        public FeatureManagerSnapshot(FeatureManager featureManager)
        {
            _featureManager = featureManager ?? throw new ArgumentNullException(nameof(featureManager));
        }

        public async IAsyncEnumerable<string> GetFeatureNamesAsync([EnumeratorCancellation]CancellationToken cancellationToken)
        {
            if (_featureNames == null)
            {
                var featureNames = new List<string>();

                await foreach (string featureName in _featureManager.GetFeatureNamesAsync(cancellationToken).ConfigureAwait(false))
                {
                    featureNames.Add(featureName);
                }

                _featureNames = featureNames;
            }

            foreach (string featureName in _featureNames)
            {
                yield return featureName;
            }
        }

        public Task<bool> IsEnabledAsync(string feature, CancellationToken cancellationToken)
        {
            return _flagCache.GetOrAdd(
                feature,
                (key) => _featureManager.IsEnabledAsync(key, cancellationToken));
        }

        public Task<bool> IsEnabledAsync<TContext>(string feature, TContext context, CancellationToken cancellationToken)
        {
            return _flagCache.GetOrAdd(
                feature,
                (key) => _featureManager.IsEnabledAsync(key, context, cancellationToken));
        }

        public async ValueTask<Variant> GetVariantAsync(string feature, CancellationToken cancellationToken)
        {
            string cacheKey = GetVariantCacheKey(feature);

            //
            // First, check local cache
            if (_variantCache.ContainsKey(feature))
            {
                return _variantCache[cacheKey];
            }

            Variant variant = await _featureManager.GetVariantAsync(feature, cancellationToken).ConfigureAwait(false);

            _variantCache[cacheKey] = variant;

            return variant;
        }

        public async ValueTask<Variant> GetVariantAsync<TContext>(string feature, TContext context, CancellationToken cancellationToken)
        {
            string cacheKey = GetVariantCacheKey(feature);

            //
            // First, check local cache
            if (_variantCache.ContainsKey(feature))
            {
                return _variantCache[cacheKey];
            }

            Variant variant = await _featureManager.GetVariantAsync<TContext>(feature, context, cancellationToken).ConfigureAwait(false);

            _variantCache[cacheKey] = variant;

            return variant;
        }

        private string GetVariantCacheKey(string feature)
        {
            return $"{typeof(Variant).FullName}\n{feature}";
        }
    }
}
