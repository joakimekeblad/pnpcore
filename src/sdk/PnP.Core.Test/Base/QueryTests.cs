﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using PnP.Core.Model;
using PnP.Core.Model.SharePoint;
using PnP.Core.QueryModel;
using PnP.Core.Services;
using PnP.Core.Test.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace PnP.Core.Test.Base
{


    [TestClass]
    public class QueryTests
    {
        [ClassInitialize]
        public static void TestFixtureSetup(TestContext context)
        {
            // All these tests are offline by design!
        }

        #region Helper methods

        private Tuple<TModel, EntityInfo, Expression<Func<TModelInterface, object>>[]> BuildModel<TModel, TModelInterface>(Expression<Func<TModelInterface, object>>[] expression = null, bool graphFirst = true) where TModel : new()
        {
            using (var context = TestCommon.Instance.GetContext(TestCommon.TestSite))
            {
                context.GraphFirst = graphFirst;

                var model = new TModel();
                (model as IDataModelWithContext).PnPContext = context;

                var entityInfo = EntityManager.GetClassInfo(model.GetType(), (model as BaseDataModel<TModelInterface>), expression);

                return new Tuple<TModel, EntityInfo, Expression<Func<TModelInterface, object>>[]>(model, entityInfo, expression);
            }
        }

        private async Task<List<string>> GetAPICallTestAsync<TModel, TModelInterface>(Tuple<TModel, EntityInfo, Expression<Func<TModelInterface, object>>[]> input)
        {
            List<string> requests = new List<string>();

            // Run the basic query generation
            var apiCallRequest = await new QueryClient().BuildGetAPICallAsync(input.Item1 as BaseDataModel<TModelInterface>, input.Item2, default);
            requests.Add(CleanRequestUrl((input.Item1 as IDataModelWithContext).PnPContext, apiCallRequest.ApiCall.Request));

            // Run the extra query generation (used to handle non expandable queries via a single batch)
            if (apiCallRequest.ApiCall.Type == ApiType.Graph || apiCallRequest.ApiCall.Type == ApiType.GraphBeta)
            {
                var nonExpandableRequests = await GetNonExpandableTestAsync(input);
                requests.AddRange(nonExpandableRequests);
            }

            return requests;
        }

        private async Task<List<string>> GetNonExpandableTestAsync<TModel, TModelInterface>(Tuple<TModel, EntityInfo, Expression<Func<TModelInterface, object>>[]> input)
        {
            var batch = (input.Item1 as IDataModelWithContext).PnPContext.NewBatch();

            await new QueryClient().AddGraphBatchRequestsForNonExpandableCollectionsAsync(input.Item1 as BaseDataModel<TModelInterface>, batch, input.Item2, input.Item3, null, null);

            List<string> requests = new List<string>();
            foreach (var request in batch.Requests)
            {
                requests.Add(CleanRequestUrl((input.Item1 as IDataModelWithContext).PnPContext, request.Value.ApiCall.Request));
            }

            return requests;
        }

        private async Task<List<string>> GetODataAPICallTestAsync<TModel, TModelInterface>(Tuple<TModel, EntityInfo, Expression<Func<TModelInterface, object>>[]> input, ODataQuery<TModelInterface> query)
        {
            // Instantiate the relevant collection class
            var assembly = Assembly.GetAssembly(typeof(IWeb));
            var collectionType = assembly.GetType(typeof(TModel).FullName + "Collection");
            var modelCollection = Activator.CreateInstance(collectionType, (input.Item1 as IDataModelWithContext).PnPContext, null, null);

            // Process the input expressions

            IQueryable<TModelInterface> selectionTarget = modelCollection as IQueryable<TModelInterface>;
            if (input.Item3 != null)
            {
                selectionTarget = QueryClient.ProcessExpression(selectionTarget, input.Item2, input.Item3);
            }

            // Translate the expressions to a query
            var query2 = DataModelQueryProvider<TModelInterface>.Translate(selectionTarget.Expression);

            // Unite with the provided query
            if (query.Top.HasValue)
            {
                query2.Top = query.Top;
            }

            if (query.Skip.HasValue)
            {
                query2.Skip = query.Skip;
            }

            query2.Filters.AddRange(query.Filters);

            query2.OrderBy.AddRange(query.OrderBy);

            if (query.Select.Count > 0)
            {
                foreach (var select in query.Select)
                {
                    if (!query2.Select.Contains(select))
                    {
                        query2.Select.Add(select);
                    }
                }
            }

            if (query.Expand.Count > 0)
            {
                foreach (var expand in query.Expand)
                {
                    if (!query2.Expand.Contains(expand))
                    {
                        query2.Expand.Add(expand);
                    }
                }
            }

            List<string> requests = new List<string>();

            // Build the proper ApiCall
            var apiCalls = await QueryClient.BuildODataGetQueryAsync(input.Item1, input.Item2, (input.Item1 as IDataModelWithContext).PnPContext, query2, null);

            foreach (var apiCall in apiCalls)
            {
                requests.Add(CleanRequestUrl((input.Item1 as IDataModelWithContext).PnPContext, apiCall.Request));
            }

            return requests;
        }

        private static string CleanRequestUrl(PnPContext context, string query)
        {
            query = query.Replace($"/{context.Uri.DnsSafeHost}", "/{hostname}");
            query = query.Replace($"{context.Uri.AbsolutePath}", "{serverrelativepath}");

            if (query.IndexOf("/_api/", StringComparison.InvariantCultureIgnoreCase) > -1)
            {
                return query.Substring(query.IndexOf("/_api/", StringComparison.InvariantCultureIgnoreCase) + 1);
            }

            return query;
        }

        #endregion

        #region Basic GET tests for SharePoint REST + Graph using the Web object

        [TestMethod]
        public async Task GetWebGraphFirstDefault()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>());
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "sites/{hostname}:{serverrelativepath}", true);
        }

        [TestMethod]
        public async Task GetWebDefault()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(graphFirst: false));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "_api/web", true);
        }

        [TestMethod]
        public async Task GetWebGraphFirstExpressionSingleSimpleProperty()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(new Expression<Func<IWeb, object>>[] { p => p.Title }));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "sites/{hostname}:{serverrelativepath}?$select=sharepointIds%2cdisplayName%2cid", true);
        }

        [TestMethod]
        public async Task GetWebExpressionSingleSimpleProperty()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(new Expression<Func<IWeb, object>>[] { p => p.Title }, graphFirst: false));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "_api/web?$select=Id%2cTitle", true);
        }

        [TestMethod]
        public async Task GetWebGraphFirstExpressionMultipleSimpleProperty()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(new Expression<Func<IWeb, object>>[] { p => p.Title, p => p.Description }));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "sites/{hostname}:{serverrelativepath}?$select=sharepointIds%2cdisplayName%2cdescription%2cid", true);
        }

        [TestMethod]
        public async Task GetWebExpressionMultipleSimpleProperty()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(new Expression<Func<IWeb, object>>[] { p => p.Title, p => p.Description }, graphFirst: false));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "_api/web?$select=Id%2cTitle%2cDescription", true);
        }

        [TestMethod]
        public async Task GetWebGraphFirstExpressionKeyProperty()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(new Expression<Func<IWeb, object>>[] { p => p.Id }));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "sites/{hostname}:{serverrelativepath}?$select=sharepointIds%2cid", true);
        }

        [TestMethod]
        public async Task GetWebExpressionKeyProperty()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(new Expression<Func<IWeb, object>>[] { p => p.Id }, graphFirst: false));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "_api/web?$select=Id", true);
        }

        [TestMethod]
        public async Task GetWebGraphFirstExpressionKeyPlusSimpleProperties()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(new Expression<Func<IWeb, object>>[] { p => p.Title, p => p.Description, p => p.Id }));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "sites/{hostname}:{serverrelativepath}?$select=sharepointIds%2cdisplayName%2cdescription%2cid", true);
        }

        [TestMethod]
        public async Task GetWebExpressionKeyPlusSimpleProperties()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(new Expression<Func<IWeb, object>>[] { p => p.Title, p => p.Description, p => p.Id }, graphFirst: false));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "_api/web?$select=Id%2cTitle%2cDescription", true);
        }

        [TestMethod]
        public async Task GetWebGraphFirstExpressionExpandableProperty()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(new Expression<Func<IWeb, object>>[] { p => p.Lists }));
            Assert.IsTrue(requests.Count == 2);
            Assert.AreEqual(requests[0], "sites/{hostname}:{serverrelativepath}?$select=sharepointIds%2clists%2cid", true);
            Assert.AreEqual(requests[1], "sites/{hostname}:{serverrelativepath}:/lists?$select=system,createdDateTime,description,eTag,id,lastModifiedDateTime,name,webUrl,displayName,createdBy,lastModifiedBy,parentReference,list", true);
        }

        [TestMethod]
        public async Task GetWebExpressionExpandableProperty()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(new Expression<Func<IWeb, object>>[] { p => p.Lists }, graphFirst: false));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "_api/web?$select=Id%2cLists&$expand=Lists", true);
        }

        [TestMethod]
        public async Task GetWebGraphFirstExpressionExpandablePlusSimpleProperties()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(new Expression<Func<IWeb, object>>[] { p => p.Title, p => p.Description, p => p.Lists }));
            Assert.IsTrue(requests.Count == 2);
            Assert.AreEqual(requests[0], "sites/{hostname}:{serverrelativepath}?$select=sharepointIds%2cdisplayName%2cdescription%2clists%2cid", true);
            Assert.AreEqual(requests[1], "sites/{hostname}:{serverrelativepath}:/lists?$select=system,createdDateTime,description,eTag,id,lastModifiedDateTime,name,webUrl,displayName,createdBy,lastModifiedBy,parentReference,list", true);
        }

        [TestMethod]
        public async Task GetWebExpressionExpandablePlusSimpleProperties()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(new Expression<Func<IWeb, object>>[] { p => p.Title, p => p.Description, p => p.Lists }, graphFirst: false));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "_api/web?$select=Id%2cTitle%2cDescription%2cLists&$expand=Lists", true);
        }

        [TestMethod]
        public async Task GetWebGraphFirstExpressionExpandablePlusSimplePropertiesPlusLoadPropertiesSimple()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(new Expression<Func<IWeb, object>>[]
            { p => p.Title, p => p.Description, p => p.Lists.LoadProperties(
                p=>p.Title, p=>p.TemplateType)
            }));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "_api/web?$select=Id%2cTitle%2cDescription%2cLists%2fTitle%2cLists%2fBaseTemplate%2cLists%2fId&$expand=Lists", true);
        }

        [TestMethod]
        public async Task GetWebGraphFirstExpressionExpandablePlusSimplePropertiesPlusLoadPropertiesSimplePlusKeyProperty()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(new Expression<Func<IWeb, object>>[]
            { p => p.Title, p => p.Description, p => p.Lists.LoadProperties(
                p=>p.Title, p=>p.TemplateType, p=>p.Id)
            }));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "_api/web?$select=Id%2cTitle%2cDescription%2cLists%2fTitle%2cLists%2fBaseTemplate%2cLists%2fId&$expand=Lists", true);
        }

        [TestMethod]
        public async Task GetWebGraphFirstExpressionExpandablePlusSimplePropertiesPlusLoadPropertiesRecursive()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(new Expression<Func<IWeb, object>>[]
            { p => p.Title, p => p.Description, p => p.Lists.LoadProperties(
                p => p.Title, p => p.TemplateType, p=>p.ContentTypes.LoadProperties(
                    p=>p.Name, p=>p.FieldLinks.LoadProperties(
                        p=> p.Name, p=> p.Hidden)))
            }));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "_api/web?$select=Id%2cTitle%2cDescription%2cLists%2fTitle%2cLists%2fBaseTemplate%2cLists%2fId%2cLists%2fContentTypes%2fName%2cLists%2fContentTypes%2fStringId%2cLists%2fContentTypes%2fFieldLinks%2fName%2cLists%2fContentTypes%2fFieldLinks%2fHidden%2cLists%2fContentTypes%2fFieldLinks%2fId&$expand=Lists%2cLists%2fContentTypes%2cLists%2fContentTypes%2fFieldLinks", true);
        }

        [TestMethod]
        public async Task GetWebGraphFirstExpressionExpandablePlusSimplePropertiesPlusLoadPropertiesRecursivePlusKeyProperties()
        {
            var requests = await GetAPICallTestAsync(BuildModel<Web, IWeb>(new Expression<Func<IWeb, object>>[]
            { p => p.Title, p => p.Description, p => p.Lists.LoadProperties(
                p => p.Title, p => p.TemplateType, p=>p.Id, p=>p.ContentTypes.LoadProperties(
                    p=>p.Name, p=>p.StringId, p=>p.FieldLinks.LoadProperties(
                        p=>p.Id, p=> p.Name, p=> p.Hidden)))
            }));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "_api/web?$select=Id%2cTitle%2cDescription%2cLists%2fTitle%2cLists%2fBaseTemplate%2cLists%2fId%2cLists%2fContentTypes%2fName%2cLists%2fContentTypes%2fStringId%2cLists%2fContentTypes%2fFieldLinks%2fId%2cLists%2fContentTypes%2fFieldLinks%2fName%2cLists%2fContentTypes%2fFieldLinks%2fHidden&$expand=Lists%2cLists%2fContentTypes%2cLists%2fContentTypes%2fFieldLinks", true);
        }

        #endregion

        #region Graph only tests using the Taxonomy model

        [TestMethod]
        public async Task GetTermStoreDefault()
        {
            var requests = await GetAPICallTestAsync(BuildModel<TermStore, ITermStore>());
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "termstore", true);
        }

        [TestMethod]
        public async Task GetTeamExpressionSingleSimpleProperty()
        {
            var requests = await GetAPICallTestAsync(BuildModel<TermStore, ITermStore>(new Expression<Func<ITermStore, object>>[] { p => p.DefaultLanguage }));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "termstore?$select=id%2cdefaultLanguageTag", true);
        }

        [TestMethod]
        public async Task GetTeamExpressionKeyProperty()
        {
            var requests = await GetAPICallTestAsync(BuildModel<TermStore, ITermStore>(new Expression<Func<ITermStore, object>>[] { p => p.Id }));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "termstore?$select=id", true);
        }

        [TestMethod]
        public async Task GetTeamExpressionSingleSimplePropertyPlusKeyProperty()
        {
            var requests = await GetAPICallTestAsync(BuildModel<TermStore, ITermStore>(new Expression<Func<ITermStore, object>>[] { p => p.DefaultLanguage, p => p.Id }));
            Assert.IsTrue(requests.Count == 1);
            Assert.AreEqual(requests[0], "termstore?$select=id%2cdefaultLanguageTag", true);
        }

        [TestMethod]
        public async Task GetTeamExpressionExpandable()
        {
            var requests = await GetAPICallTestAsync(BuildModel<TermStore, ITermStore>(new Expression<Func<ITermStore, object>>[] { p => p.Groups }));
            Assert.IsTrue(requests.Count == 2);
            Assert.AreEqual(requests[0], "termstore?$select=id%2cgroups", true);
            Assert.AreEqual(requests[1], "termstore/groups", true);
        }

        [TestMethod]
        public async Task GetTeamExpressionExpandableKeyProperty()
        {
            var requests = await GetAPICallTestAsync(BuildModel<TermStore, ITermStore>(new Expression<Func<ITermStore, object>>[] { p => p.Groups, p => p.Id }));
            Assert.IsTrue(requests.Count == 2);
            Assert.AreEqual(requests[0], "termstore?$select=id%2cgroups", true);
            Assert.AreEqual(requests[1], "termstore/groups", true);
        }

        [TestMethod]
        public async Task GetTeamExpressionExpandableKeyPropertyPlusLoadProperties()
        {
            var requests = await GetAPICallTestAsync(BuildModel<TermStore, ITermStore>(new Expression<Func<ITermStore, object>>[] { p => p.Id, p => p.Groups.LoadProperties(
                p=>p.Name )
            }));
            Assert.IsTrue(requests.Count == 2);
            Assert.AreEqual(requests[0], "termstore?$select=id", true);
            Assert.AreEqual(requests[1], "termstore/groups?$select=displayname,id", true);
        }

        [TestMethod]
        public async Task GetTeamExpressionExpandableKeyPropertyPlusLoadPropertiesPlusKeyProperty()
        {
            var requests = await GetAPICallTestAsync(BuildModel<TermStore, ITermStore>(new Expression<Func<ITermStore, object>>[] { p => p.Id, p => p.Groups.LoadProperties(
                p=>p.Name, p=>p.Id )
            }));
            Assert.IsTrue(requests.Count == 2);
            Assert.AreEqual(requests[0], "termstore?$select=id", true);
            Assert.AreEqual(requests[1], "termstore/groups?$select=displayname,id", true);
        }

        [TestMethod]
        [ExpectedException(typeof(ClientException))]
        public async Task GetTeamExpressionExpandableKeyPropertyPlusLoadPropertiesPlusExpandKeyProperty()
        {
            var requests = await GetAPICallTestAsync(BuildModel<TermStore, ITermStore>(new Expression<Func<ITermStore, object>>[] { p => p.Id, p => p.Groups.LoadProperties(
                p=>p.Name, p=>p.Id, p=>p.Sets )
            }));
        }
        #endregion

        #region Linq query tests

        [TestMethod]
        public async Task GetLinqListGraph()
        {
            //NOTE: $skip does not work ~ should result in exception once we've the needed metadata to check for that
            var requests = await GetODataAPICallTestAsync(BuildModel<List, IList>(), new ODataQuery<IList> { Top = 10, Skip = 5 });
            Assert.AreEqual(requests[0], "sites/{Parent.GraphId}/lists?$select=system,createdDateTime,description,eTag,id,lastModifiedDateTime,name,webUrl,displayName,createdBy,lastModifiedBy,parentReference,list&$top=10&$skip=5", true);
        }

        [TestMethod]
        public async Task GetLinqListGraphSingleSimpleProperty()
        {
            //NOTE: $skip does not work ~ should result in exception once we've the needed metadata to check for that
            var requests = await GetODataAPICallTestAsync(
                BuildModel<List, IList>(new Expression<Func<IList, object>>[] { p => p.Title }),
                new ODataQuery<IList> { Top = 10, Skip = 5 });
            Assert.AreEqual(requests[0], "sites/{Parent.GraphId}/lists?$select=id,displayName,system&$top=10&$skip=5", true);
        }

        [TestMethod]
        public async Task GetLinqListGraphMultipleSimpleProperty()
        {
            //NOTE: $skip does not work ~ should result in exception once we've the needed metadata to check for that
            var requests = await GetODataAPICallTestAsync(
                BuildModel<List, IList>(new Expression<Func<IList, object>>[] { p => p.Title, p => p.Description }),
                new ODataQuery<IList> { Top = 10, Skip = 5 });
            Assert.AreEqual(requests[0], "sites/{Parent.GraphId}/lists?$select=id,displayName,description,system&$top=10&$skip=5", true);
        }

        [TestMethod]
        public async Task GetLinqListSingleSimpleProperty()
        {
            var requests = await GetODataAPICallTestAsync(
                BuildModel<List, IList>(new Expression<Func<IList, object>>[] { p => p.ListExperience }),
                new ODataQuery<IList> { Top = 10, Skip = 5 });
            Assert.AreEqual(requests[0], "_api/web/lists?$select=Id,ListExperienceOptions&$top=10&$skip=5", true);
        }

        [TestMethod]
        public async Task GetLinqListMultipleSimpleProperty()
        {
            var requests = await GetODataAPICallTestAsync(
                BuildModel<List, IList>(new Expression<Func<IList, object>>[] { p => p.ListExperience, p => p.Description }),
                new ODataQuery<IList> { Top = 10, Skip = 5 });
            Assert.AreEqual(requests[0], "_api/web/lists?$select=id,description,listexperienceoptions&$top=10&$skip=5", true);
        }

        [TestMethod]
        public async Task GetLinqListSingleSimpleExpandProperty()
        {
            //NOTE: $skip does not work ~ should result in exception once we've the needed metadata to check for that
            var requests = await GetODataAPICallTestAsync(
                BuildModel<List, IList>(new Expression<Func<IList, object>>[] { p => p.InformationRightsManagementSettings }),
                new ODataQuery<IList> { Top = 10, Skip = 5 });
            Assert.AreEqual(requests[0], "_api/web/lists?$select=id,informationrightsmanagementsettings&$expand=informationrightsmanagementsettings&$top=10&$skip=5", true);
        }

        [TestMethod]
        public async Task GetLinqListSingleSimpleNormalAndExpandProperty()
        {
            //NOTE: $skip does not work ~ should result in exception once we've the needed metadata to check for that
            var requests = await GetODataAPICallTestAsync(
                BuildModel<List, IList>(new Expression<Func<IList, object>>[] { p => p.Title, p => p.InformationRightsManagementSettings }),
                new ODataQuery<IList> { Top = 10, Skip = 5 });
            Assert.AreEqual(requests[0], "_api/web/lists?$select=id,title,informationrightsmanagementsettings&$expand=informationrightsmanagementsettings&$top=10&$skip=5", true);
        }

        [TestMethod]
        [ExpectedException(typeof(ClientException))]
        public async Task GetLinqTermStoreSingleSimpleNormalAndExpandProperty()
        {
            // Throws exception since expand via a separate query (like for loading the Terms) is not possible
            var requests = await GetODataAPICallTestAsync(
                BuildModel<TermSet, ITermSet>(new Expression<Func<ITermSet, object>>[] { p => p.Id, p => p.Terms }),
                new ODataQuery<ITermSet> { Top = 10, Skip = 5 });
        }

        [TestMethod]
        public async Task GetLinqListPlusLoadProperties()
        {
            var requests = await GetODataAPICallTestAsync(
                BuildModel<List, IList>(new Expression<Func<IList, object>>[] { p => p.ListExperience, p => p.Fields.LoadProperties(p => p.Id, p => p.InternalName) }),
                new ODataQuery<IList> { Top = 10, Skip = 5 });
            Assert.AreEqual(requests[0], "_api/web/lists?$select=id,listexperienceoptions,fields%2fid,fields%2finternalname&$expand=fields&$top=10&$skip=5", true);
        }

        #endregion

    }
}
