//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Components
{
    /// <summary>
    /// Provides common functionality needed to implement a feature
    /// component which uses IFeatureProviders to provide further
    /// extensibility.
    /// </summary>
    public abstract class FeatureComponentBase<TProvider>
        where TProvider : IFeatureProvider
    {
        /// <summary>
        /// Gets the collection of IFeatureProviders registered with
        /// this feature component.
        /// </summary>
        public IFeatureProviderCollection<TProvider> Providers { get; private set; }

        /// <summary>
        /// Gets the ILogger implementation to use for writing log
        /// messages.
        /// </summary>
        protected ILogger Logger { get; private set; }

        /// <summary>
        /// Creates an instance of the FeatureComponentBase class with
        /// the specified ILoggger.
        /// </summary>
        /// <param name="logger">The ILogger to use for this instance.</param>
        public FeatureComponentBase(ILogger logger)
        {
            this.Providers = new FeatureProviderCollection<TProvider>();
            this.Logger = logger;
        }

        /// <summary>
        /// Invokes the given function synchronously against all
        /// registered providers.
        /// </summary>
        /// <param name="invokeFunc">The function to be invoked.</param>
        /// <returns>
        /// An IEnumerable containing the results of all providers
        /// that were invoked successfully.
        /// </returns>
        protected IEnumerable<TResult> InvokeProviders<TResult>(
            Func<TProvider, TResult> invokeFunc)
        {
            Stopwatch invokeTimer = new Stopwatch();
            List<TResult> providerResults = new List<TResult>();

            foreach (var provider in this.Providers)
            {
                try
                {
                    invokeTimer.Restart();

                    providerResults.Add(invokeFunc(provider));

                    invokeTimer.Stop();

                    this.Logger.Write(
                        LogLevel.Verbose,
                        $"Invocation of provider '{provider.ProviderId}' completed in {invokeTimer.ElapsedMilliseconds}ms.");
                }
                catch (Exception e)
                {
                    this.Logger.WriteException(
                        $"Exception caught while invoking provider {provider.ProviderId}:",
                        e);
                }
            }

            return providerResults;
        }

        /// <summary>
        /// Invokes the given function asynchronously against all
        /// registered providers.
        /// </summary>
        /// <param name="invokeFunc">The function to be invoked.</param>
        /// <returns>
        /// A Task that, when completed, returns an IEnumerable containing
        /// the results of all providers that were invoked successfully.
        /// </returns>
        protected async Task<IEnumerable<TResult>> InvokeProvidersAsync<TResult>(
            Func<TProvider, Task<TResult>> invokeFunc)
        {
            Stopwatch invokeTimer = new Stopwatch();
            List<TResult> providerResults = new List<TResult>();

            foreach (var provider in this.Providers)
            {
                try
                {
                    invokeTimer.Restart();

                    providerResults.Add(
                        await invokeFunc(provider));

                    invokeTimer.Stop();

                    this.Logger.Write(
                        LogLevel.Verbose,
                        $"Invocation of provider '{provider.ProviderId}' completed in {invokeTimer.ElapsedMilliseconds}ms.");
                }
                catch (Exception e)
                {
                    this.Logger.WriteException(
                        $"Exception caught while invoking provider {provider.ProviderId}:",
                        e);
                }
            }

            return providerResults;
        }
    }
}
