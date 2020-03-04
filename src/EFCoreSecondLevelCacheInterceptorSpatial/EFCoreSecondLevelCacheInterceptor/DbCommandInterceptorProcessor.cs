using System;
using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EFCoreSecondLevelCacheInterceptor
{
    /// <summary>
    /// Helps processing SecondLevelCacheInterceptor
    /// </summary>
    public interface IDbCommandInterceptorProcessor
    {
        /// <summary>
        /// Reads data from cache or cache it and then returns the result
        /// </summary>
        T ProcessExecutedCommands<T>(DbCommand command, DbContext context, T result, [CallerMemberName] string methodName = null);

        /// <summary>
        /// Adds command's data to the cache
        /// </summary>
        T ProcessExecutingCommands<T>(DbCommand command, DbContext context, T result, [CallerMemberName] string methodName = null);
    }

    /// <summary>
    /// Helps processing SecondLevelCacheInterceptor
    /// </summary>
    public class DbCommandInterceptorProcessor : IDbCommandInterceptorProcessor
    {
        private readonly IEFCacheServiceProvider _cacheService;
        private readonly IEFCacheDependenciesProcessor _cacheDependenciesProcessor;
        private readonly IEFCacheKeyProvider _cacheKeyProvider;
        private readonly IEFCachePolicyParser _cachePolicyParser;
        private readonly ILogger<DbCommandInterceptorProcessor> _logger;

        /// <summary>
        /// Helps processing SecondLevelCacheInterceptor
        /// </summary>
        public DbCommandInterceptorProcessor(
            IEFCacheServiceProvider cacheService,
            IEFCacheDependenciesProcessor cacheDependenciesProcessor,
            IEFCacheKeyProvider cacheKeyProvider,
            IEFCachePolicyParser cachePolicyParser,
            ILogger<DbCommandInterceptorProcessor> logger)
        {
            _cacheService = cacheService;
            _cacheDependenciesProcessor = cacheDependenciesProcessor;
            _cacheKeyProvider = cacheKeyProvider;
            _cachePolicyParser = cachePolicyParser;
            _logger = logger;
        }

        /// <summary>
        /// Reads data from cache or cache it and then returns the result
        /// </summary>
        public T ProcessExecutedCommands<T>(DbCommand command, DbContext context, T result, [CallerMemberName] string methodName = null)
        {
            if (result is EFTableRowsDataReader rowsReader)
            {
                _logger.LogInformation(CacheableEventId.CacheHit, $"Using the TableRows[{rowsReader.TableName}] from the cache.");
                return result;
            }

            if (_cacheDependenciesProcessor.InvalidateCacheDependencies(command, context, new EFCachePolicy()))
            {
                return result;
            }

            var cachePolicy = _cachePolicyParser.GetEFCachePolicy(command.CommandText);
            if (cachePolicy != null)
            {
                var efCacheKey = _cacheKeyProvider.GetEFCacheKey(command, context, cachePolicy);

                if (result is int data)
                {
                    _cacheService.InsertValue(efCacheKey, new EFCachedData { NonQuery = data }, cachePolicy);
                    _logger.LogInformation(CacheableEventId.QueryResultCached, $"[{data}] added to the cache[{efCacheKey}].");
                    return result;
                }

                if (result is DbDataReader dataReader)
                {
                    EFTableRows tableRows;
                    using (var dbReaderLoader = new EFDataReaderLoader(dataReader))
                    {
                        tableRows = dbReaderLoader.LoadAndClose();
                    }

                    _cacheService.InsertValue(efCacheKey, new EFCachedData { TableRows = tableRows }, cachePolicy);
                    _logger.LogInformation(CacheableEventId.QueryResultCached, $"TableRows[{tableRows.TableName}] added to the cache[{efCacheKey}].");
                    return (T)(object)new EFTableRowsDataReader(tableRows);
                }

                if (result is object)
                {
                    _cacheService.InsertValue(efCacheKey, new EFCachedData { Scalar = result }, cachePolicy);
                    _logger.LogInformation(CacheableEventId.QueryResultCached, $"[{result}] added to the cache[{efCacheKey}].");
                    return result;
                }
            }

            return result;
        }

        /// <summary>
        /// Reads command's data from the cache, if any.
        /// </summary>
        public T ProcessExecutingCommands<T>(DbCommand command, DbContext context, T result, [CallerMemberName] string methodName = null)
        {
            var cachePolicy = _cachePolicyParser.GetEFCachePolicy(command.CommandText);
            if (cachePolicy != null)
            {
                var efCacheKey = _cacheKeyProvider.GetEFCacheKey(command, context, cachePolicy);
                if (!(_cacheService.GetValue(efCacheKey, cachePolicy) is EFCachedData cacheResult))
                {
                    _logger.LogInformation($"[{efCacheKey}] was not present in the cache.");
                    return result;
                }

                if (result is InterceptionResult<DbDataReader>)
                {
                    if (cacheResult.IsNull)
                    {
                        _logger.LogInformation("Suppressed result with an empty TableRows.");
                        return (T)Convert.ChangeType(InterceptionResult<DbDataReader>.SuppressWithResult(new EFTableRowsDataReader(new EFTableRows())), typeof(T));
                    }

                    _logger.LogInformation($"Suppressed result with a TableRows[{cacheResult.TableRows.TableName}] from the cache[{efCacheKey}].");
                    return (T)Convert.ChangeType(InterceptionResult<DbDataReader>.SuppressWithResult(new EFTableRowsDataReader(cacheResult.TableRows)), typeof(T));
                }

                if (result is InterceptionResult<int>)
                {
                    int cachedResult = cacheResult.IsNull ? default : cacheResult.NonQuery;
                    _logger.LogInformation($"Suppressed result with {cachedResult} from the cache[{efCacheKey}].");
                    return (T)Convert.ChangeType(InterceptionResult<int>.SuppressWithResult(cachedResult), typeof(T));
                }

                if (result is InterceptionResult<object>)
                {
                    object cachedResult = cacheResult.IsNull ? default : cacheResult.Scalar;
                    _logger.LogInformation($"Suppressed result with {cachedResult} from the cache[{efCacheKey}].");
                    return (T)Convert.ChangeType(InterceptionResult<object>.SuppressWithResult(cachedResult), typeof(T));
                }
            }

            return result;
        }
    }
}