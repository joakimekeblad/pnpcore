﻿using PnP.Core.Model;
using PnP.Core.Services;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace PnP.Core.QueryModel
{
    /// <summary>
    /// Class holding data model extension methods
    /// </summary>
    public static class BaseDataModelExtensions
    {
        #region Utility extension to make it easier to chain multiple async calls
        /// <summary>
        /// Chains async calls. See https://stackoverflow.com/a/52739551 for more information
        /// </summary>
        /// <typeparam name="TIn">Input task</typeparam>
        /// <typeparam name="TOut">Output task</typeparam>
        /// <param name="inputTask">Async operatation to start from</param>
        /// <param name="mapping">Async operation to run next</param>
        /// <returns>Task outcome from the ran async operation</returns>
        public static async Task<TOut> AndThen<TIn, TOut>(this Task<TIn> inputTask, Func<TIn, Task<TOut>> mapping)
        {
            if (inputTask == null)
            {
                throw new ArgumentNullException(nameof(inputTask));
            }
            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            var input = await inputTask.ConfigureAwait(false);
            return (await mapping(input).ConfigureAwait(false));
        }
        #endregion

        #region Helper methods to obtain MethodInfo in a safe way

#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable CA1801 // Review unused parameters
#pragma warning disable IDE0060 // Remove unused parameter
        private static MethodInfo GetMethodInfo<T1, T2>(Func<T1, T2> f, T1 unused1)
        {
            return f.Method;
        }

        private static MethodInfo GetMethodInfo<T1, T2, T3>(Func<T1, T2, T3> f, T1 unused1, T2 unused2)
        {
            return f.Method;
        }

        private static MethodInfo GetMethodInfo<T1, T2, T3, T4>(Func<T1, T2, T3, T4> f, T1 unused1, T2 unused2, T3 unused3)
        {
            return f.Method;
        }

        private static MethodInfo GetMethodInfo<T1, T2, T3, T4, T5>(Func<T1, T2, T3, T4, T5> f, T1 unused1, T2 unused2, T3 unused3, T4 unused4)
        {
            return f.Method;
        }

        private static MethodInfo GetMethodInfo<T1, T2, T3, T4, T5, T6>(Func<T1, T2, T3, T4, T5, T6> f, T1 unused1, T2 unused2, T3 unused3, T4 unused4, T5 unused5)
        {
            return f.Method;
        }

        private static MethodInfo GetMethodInfo<T1, T2, T3, T4, T5, T6, T7>(Func<T1, T2, T3, T4, T5, T6, T7> f, T1 unused1, T2 unused2, T3 unused3, T4 unused4, T5 unused5, T6 unused6)
        {
            return f.Method;
        }
#pragma warning restore CA1801 // Review unused parameters
#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore IDE0060 // Remove unused parameter
        #endregion

        #region Include implementation

        /// <summary>
        /// Extension method to declare the collection properties to expand while querying the REST service
        /// </summary>
        /// <typeparam name="TResult">The type of the target entity</typeparam>
        /// <param name="source">The collection of items to expand properties from</param>
        /// <param name="selector">A selector for the expandable properties</param>
        /// <returns>The resulting collection</returns>
        public static IQueryable<TResult> Include<TResult>(
            this IQueryable<TResult> source, Expression<Func<TResult, object>> selector)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (selector is null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            return source.Provider.CreateQuery<TResult>(
                Expression.Call(
                    null,
                    GetMethodInfo(Include, source, selector),
                    new Expression[] { source.Expression, Expression.Quote(selector) }
                    ));
        }

        /// <summary>
        /// Extension method to declare the collection properties to expand while querying the REST service
        /// </summary>
        /// <typeparam name="TResult">The type of the target entity</typeparam>
        /// <param name="source">The collection of items to expand properties from</param>
        /// <param name="selectors">An array of selectors for the expandable properties</param>
        /// <returns>The resulting collection</returns>
        public static IQueryable<TResult> Include<TResult>(
            this IQueryable<TResult> source, params Expression<Func<TResult, object>>[] selectors)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (selectors is null)
            {
                throw new ArgumentNullException(nameof(selectors));
            }

            IQueryable<TResult> result = source;

            foreach (var s in selectors)
            {
                result = result.Include(s);
            }

            return result;
        }

        #endregion

        #region Load implementation

        /// <summary>
        /// Extension method to declare a field/metadata property to load while executing the REST query
        /// </summary>
        /// <typeparam name="TResult">The type of the target entity</typeparam>
        /// <param name="source">The collection of items to load the field/metadata from</param>
        /// <param name="selector">A selector for a field/metadata</param>
        /// <returns>The resulting collection</returns>
        public static IQueryable<TResult> Load<TResult>(
            this IQueryable<TResult> source, Expression<Func<TResult, object>> selector)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (selector is null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            return source.Provider.CreateQuery<TResult>(
                Expression.Call(
                    null,
                    GetMethodInfo(Load, source, selector),
                    new Expression[] { source.Expression, Expression.Quote(selector) }
                    ));
        }

        /// <summary>
        /// Extension method to declare the fields/metadata properties to load while executing the REST query
        /// </summary>
        /// <typeparam name="TResult">The type of the target entity</typeparam>
        /// <param name="source">The collection of items to load fields/metadata from</param>
        /// <param name="selectors">An array of selectors for the fields/metadata</param>
        /// <returns>The resulting collection</returns>
        public static IQueryable<TResult> Load<TResult>(
            this IQueryable<TResult> source, params Expression<Func<TResult, object>>[] selectors)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (selectors is null)
            {
                throw new ArgumentNullException(nameof(selectors));
            }

            IQueryable<TResult> result = source;

            foreach (var s in selectors)
            {
                result = result.Load(s);
            }

            return result;
        }

        #endregion

        #region Internal base methods for model specific linq extension methods

        internal static async Task<T> BaseLinqGetAsync<T>(
                IQueryable<T> source,
                Expression<Func<T, bool>> predicate,
                params Expression<Func<T, object>>[] selectors)
        {
            if (!(source.Provider is IAsyncQueryProvider asyncQueryProvider))
            {
                throw new InvalidOperationException(PnPCoreResources.Exception_InvalidOperation_NotAsyncQueryableSource);
            }

            IQueryable<T> selectionTarget = PrepareExecution(source, selectors);

            return await asyncQueryProvider.ExecuteAsync<Task<T>>(
                Expression.Call(
                    null,
                    GetMethodInfo(Queryable.FirstOrDefault, selectionTarget, predicate),
                    new Expression[] { selectionTarget.Expression, Expression.Quote(predicate) }
                    )).ConfigureAwait(false);
        }

        internal static T BaseLinqGet<T>(
                IQueryable<T> source,
                Expression<Func<T, bool>> predicate,
                params Expression<Func<T, object>>[] selectors)
        {

            IQueryable<T> selectionTarget = PrepareExecution(source, selectors);

            return source.Provider.Execute<T>(
                Expression.Call(
                    null,
                    GetMethodInfo(Queryable.FirstOrDefault, selectionTarget, predicate),
                    new Expression[] { selectionTarget.Expression, Expression.Quote(predicate) }
                    ));
        }

        private static IQueryable<T> PrepareExecution<T>(IQueryable<T> source, Expression<Func<T, object>>[] selectors)
        {
            var model = EntityManager.GetEntityConcreteInstance<T>(typeof(T));
            (model as IDataModelWithContext).PnPContext = (source as IDataModelWithContext).PnPContext;

            var entityInfo = EntityManager.GetClassInfo(typeof(T), model as BaseDataModel<T>, selectors);

            IQueryable<T> selectionTarget = QueryClient.ProcessExpression(source, entityInfo, selectors);

            (source.Provider as DataModelQueryProvider<T>).EntityInfo = entityInfo;
            return selectionTarget;
        }
        #endregion

        #region Internal base methods for model specific extension methods
        internal static async Task<T> BaseGetAsync<T>(
                    IQueryable<T> source,
                    ApiCall apiCall,
                    params Expression<Func<T, object>>[] selectors)
        {
            // Instantiate a new concrete entity
            var concreteEntity = (source as IManageableCollection<T>).CreateNew();

            // Grab entity information using the provided selectors
            var entityInfo = EntityManager.GetClassInfo(concreteEntity.GetType(), (concreteEntity as BaseDataModel<T>), selectors);

            // Build the default get query but pass in our given API call as override
            var query = await new QueryClient().BuildGetAPICallAsync(concreteEntity as BaseDataModel<T>, entityInfo, apiCall).ConfigureAwait(false);

            // Trigger the get request
            await (concreteEntity as BaseDataModel<T>).RequestAsync(query.ApiCall, HttpMethod.Get).ConfigureAwait(false);

            // Add/update the result in the parent collection
            (source as IManageableCollection<T>).AddOrUpdate(concreteEntity, i => ((IDataModelWithKey)i).Key.Equals((concreteEntity as IDataModelWithKey).Key));

            return concreteEntity;
        }
        #endregion

    }
}
