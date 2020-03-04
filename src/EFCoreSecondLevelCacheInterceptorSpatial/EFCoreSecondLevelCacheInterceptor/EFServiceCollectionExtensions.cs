using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EFCoreSecondLevelCacheInterceptor
{
    /// <summary>
    /// CacheAllQueries Options
    /// </summary>
    public class CacheAllQueriesOptions
    {
        /// <summary>
        /// Defines the expiration mode of the cache item.
        /// </summary>
        public CacheExpirationMode ExpirationMode { set; get; }

        /// <summary>
        /// The expiration timeout.
        /// </summary>
        public TimeSpan Timeout { set; get; }

        /// <summary>
        /// Enables or disables the `CacheAllQueries` feature.
        /// </summary>
        public bool IsActive { set; get; }
    }

    /// <summary>
    /// Defines EFCoreSecondLevel's Options
    /// </summary>
    public class EFCoreSecondLevelCacheOptions
    {
        internal Type CacheProvider { get; set; }

        internal string RedisConfiguration { get; set; }

        internal CacheAllQueriesOptions CacheAllQueriesOptions { get; set; }

        /// <summary>
        /// Puts the whole system in cache. In this case calling the `Cacheable()` methods won't be necessary.
        /// If you specify the `Cacheable()` method, its setting will override this global setting.
        /// If you want to exclude some queries from this global cache, apply the `NotCacheable()` method to them.
        /// </summary>
        /// <param name="expirationMode">Defines the expiration mode of the cache item.</param>
        /// <param name="timeout">The expiration timeout.</param>
        public EFCoreSecondLevelCacheOptions CacheAllQueries(CacheExpirationMode expirationMode, TimeSpan timeout)
        {
            CacheAllQueriesOptions = new CacheAllQueriesOptions
            {
                ExpirationMode = expirationMode,
                Timeout = timeout,
                IsActive = true
            };
            return this;
        }

        /// <summary>
        /// You can introduce a custom IEFCacheServiceProvider to be used as the CacheProvider.
        /// </summary>
        /// <typeparam name="T">Implements IEFCacheServiceProvider</typeparam>
        public EFCoreSecondLevelCacheOptions UseCustomCacheProvider<T>() where T : IEFCacheServiceProvider
        {
            CacheProvider = typeof(T);
            return this;
        }

        /// <summary>
        /// Introduces the built-in `EFMemoryCacheServiceProvider` to be used as the CacheProvider.
        /// </summary>
        public EFCoreSecondLevelCacheOptions UseMemoryCacheProvider()
        {
            CacheProvider = typeof(EFMemoryCacheServiceProvider);
            return this;
        }
    }

    /// <summary>
    /// ServiceCollection Extensions
    /// </summary>
    public static class EFServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the required services of the EFCoreSecondLevelCacheInterceptor.
        /// </summary>
        public static IServiceCollection AddEFSecondLevelCache(
            this IServiceCollection services,
            Action<EFCoreSecondLevelCacheOptions> options)
        {
            services.AddMemoryCache();
            services.TryAddSingleton<IReaderWriterLockProvider, ReaderWriterLockProvider>();
            services.TryAddSingleton<IEFCacheKeyProvider, EFCacheKeyProvider>();
            services.TryAddSingleton<IEFCachePolicyParser, EFCachePolicyParser>();
            services.TryAddSingleton<IEFCacheDependenciesProcessor, EFCacheDependenciesProcessor>();
            services.TryAddSingleton<IMemoryCacheChangeTokenProvider, EFMemoryCacheChangeTokenProvider>();
            services.TryAddSingleton<IDbCommandInterceptorProcessor, DbCommandInterceptorProcessor>();

            configOptions(services, options);

            return services;
        }

        private static void configOptions(IServiceCollection services, Action<EFCoreSecondLevelCacheOptions> options)
        {
            var cacheOptions = new EFCoreSecondLevelCacheOptions();
            options.Invoke(cacheOptions);

            if (cacheOptions.CacheProvider == null)
            {
                services.TryAddSingleton<IEFCacheServiceProvider, EFMemoryCacheServiceProvider>();
            }
            else
            {
                services.TryAddSingleton(typeof(IEFCacheServiceProvider), cacheOptions.CacheProvider);
            }

            if (cacheOptions.CacheAllQueriesOptions != null)
            {
                services.TryAddSingleton(Options.Create(cacheOptions.CacheAllQueriesOptions));
            }
        }
    }
}