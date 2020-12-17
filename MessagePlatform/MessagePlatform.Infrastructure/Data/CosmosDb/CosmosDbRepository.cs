using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MessagePlatform.Core.Interfaces;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;

namespace MessagePlatform.Infrastructure.Data.CosmosDb
{
    public class CosmosDbRepository<T> : IRepository<T> where T : class
    {
        private readonly DocumentClient _client;
        private readonly string _databaseId;
        private readonly string _collectionId;
        private readonly Uri _collectionUri;

        public CosmosDbRepository(DocumentClient client, string databaseId, string collectionId)
        {
            _client = client;
            _databaseId = databaseId;
            _collectionId = collectionId;
            _collectionUri = UriFactory.CreateDocumentCollectionUri(_databaseId, _collectionId);
            
            CreateDatabaseIfNotExistsAsync().Wait();
            CreateCollectionIfNotExistsAsync().Wait();
        }

        private async Task CreateDatabaseIfNotExistsAsync()
        {
            try
            {
                await _client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(_databaseId));
            }
            catch (DocumentClientException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await _client.CreateDatabaseAsync(new Database { Id = _databaseId });
            }
        }

        private async Task CreateCollectionIfNotExistsAsync()
        {
            try
            {
                await _client.ReadDocumentCollectionAsync(_collectionUri);
            }
            catch (DocumentClientException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await _client.CreateDocumentCollectionAsync(
                    UriFactory.CreateDatabaseUri(_databaseId),
                    new DocumentCollection { Id = _collectionId });
            }
        }

        public async Task<T> GetByIdAsync(string id)
        {
            try
            {
                var documentUri = UriFactory.CreateDocumentUri(_databaseId, _collectionId, id);
                var response = await _client.ReadDocumentAsync<T>(documentUri);
                return response.Document;
            }
            catch (DocumentClientException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            var query = _client.CreateDocumentQuery<T>(_collectionUri)
                .AsDocumentQuery();

            var results = new List<T>();
            while (query.HasMoreResults)
            {
                results.AddRange(await query.ExecuteNextAsync<T>());
            }

            return results;
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            var query = _client.CreateDocumentQuery<T>(_collectionUri)
                .Where(predicate)
                .AsDocumentQuery();

            var results = new List<T>();
            while (query.HasMoreResults)
            {
                results.AddRange(await query.ExecuteNextAsync<T>());
            }

            return results;
        }

        public async Task<T> AddAsync(T entity)
        {
            var response = await _client.CreateDocumentAsync(_collectionUri, entity);
            return JsonConvert.DeserializeObject<T>(response.Resource.ToString());
        }

        public async Task UpdateAsync(T entity)
        {
            var idProperty = entity.GetType().GetProperty("Id");
            if (idProperty != null)
            {
                var id = idProperty.GetValue(entity)?.ToString();
                if (!string.IsNullOrEmpty(id))
                {
                    var documentUri = UriFactory.CreateDocumentUri(_databaseId, _collectionId, id);
                    await _client.ReplaceDocumentAsync(documentUri, entity);
                }
            }
        }

        public async Task DeleteAsync(string id)
        {
            var documentUri = UriFactory.CreateDocumentUri(_databaseId, _collectionId, id);
            try
            {
                await _client.DeleteDocumentAsync(documentUri);
            }
            catch (DocumentClientException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
            }
        }

        public async Task<bool> ExistsAsync(string id)
        {
            var entity = await GetByIdAsync(id);
            return entity != null;
        }
    }
}