﻿namespace Parliament.Data.Api.FixedQuery.Controllers
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Web.Http;
    using VDS.RDF;
    using VDS.RDF.Parsing.Handlers;
    using VDS.RDF.Parsing.Validation;
    using VDS.RDF.Query;
    using VDS.RDF.Storage;

    public abstract partial class BaseController : ApiController
    {
        private static readonly string sparqlEndpoint = ConfigurationManager.AppSettings["SparqlEndpoint"];
        private static readonly string subscriptionKey = ConfigurationManager.AppSettings["SubscriptionKey"];
        private static readonly string endpointUri = $"{sparqlEndpoint}?subscription-key={subscriptionKey}";
        // TODO: Extract to config or elsewhere
        protected static readonly Uri instance = new Uri("http://id.ukpds.org/");
        protected static readonly Uri schema = new Uri(instance, "schema/");

        protected static Graph ExecuteSingle(SparqlParameterizedString query)
        {
            return ExecuteSingle(query, endpointUri);
        }

        protected static Graph ExecuteSingle(SparqlParameterizedString query, string endpointUri)
        {
            var result = ExecuteList(query, endpointUri);

            if (result.IsEmpty)
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            return result;
        }

        protected static Graph ExecuteList(SparqlParameterizedString query)
        {
            return ExecuteList(query, endpointUri);
        }

        protected static Graph ExecuteList(SparqlParameterizedString query, string endpointUri)
        {
            var queryString = query.ToString();

            ValidateSparql(queryString);

            var graph = new Graph();

            graph.NamespaceMap.AddNamespace("owl", new Uri("http://www.w3.org/2002/07/owl#"));
            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("id", FixedQueryController.instance);
            graph.NamespaceMap.AddNamespace("schema", FixedQueryController.schema);

            var graphHandler = new GraphHandler(graph);

            var endpoint = new ConstructOnlyRemoteEndpoint(new Uri(endpointUri));
            using (var connector = new SparqlConnector(endpoint))
            {
                connector.SkipLocalParsing = true; // This was already done above

                connector.Query(graphHandler, null, queryString);
            }

            return graph;
        }

        protected Graph LookupInternal(string type, string property, string value)
        {
            var queryString = this.GetSparql("LookupInternal");

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("type", new Uri(BaseController.schema, type));
            query.SetUri("source", new Uri(BaseController.schema, property));
            query.SetLiteral("id", value);

            return BaseController.ExecuteSingle(query);
        }

        protected string GetSparql(string fileName)
        {
            var baseName = "Parliament.Data.Api.FixedQuery.Sparql";
            var resourceName = $"{baseName}.{fileName}.sparql";

            using (var sparqlResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                using (var reader = new StreamReader(sparqlResourceStream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static void ValidateSparql(string query)
        {
            var validator = new SparqlQueryValidator();
            var result = validator.Validate(query);

            if (!result.IsValid)
            {
                throw new SparqlInvalidException(result.Message);
            }
        }
    }
}