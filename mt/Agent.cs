using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Mt
{
    /// <summary>
    /// An MTConnect agent
    /// </summary>
    public class Agent
    {
        /// <summary>
        /// The base Uri for the MTConnect agent
        /// </summary>
        private Uri _baseUri;

        /// <summary>
        /// Creates an <see cref="XDocument"/> from the given <see cref="HttpWebResponse"/>
        /// </summary>
        /// <param name="response">The response</param>
        /// <returns>If the response stream is parsable as XML, an XDocument created from the response stream.</returns>
        private async Task<XDocument> XDocumentFromResponseAsync(HttpWebResponse response)
        {
            using var reader = new StreamReader(response.GetResponseStream());
            var content = await reader.ReadToEndAsync();

            return XDocument.Parse(content);
        }

        private async IAsyncEnumerable<XDocument> XDocumentsFromResponseAsync(HttpWebResponse response)
        {
            var stream = response.GetResponseStream();
            var contentType = Microsoft.Net.Http.Headers.MediaTypeHeaderValue.Parse(response.ContentType);
            var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;
            for(var streamReader = new MultipartReader(boundary, stream); ;)
            {
                var nextFrame = await streamReader.ReadNextSectionAsync();
                if (nextFrame == null)
                    yield break;

                using var frameReader = new StreamReader(nextFrame.Body);
                yield return XDocument.Parse(await frameReader.ReadToEndAsync());
            }
        }

        /// <summary>
        /// Sends a request for the given <see cref="Uri"/> and returns the response as an <see cref="XDocument"/>
        /// </summary>
        /// <param name="uri">The Uri to request</param>
        /// <returns>If the response succeeds with a status code of OK, an XDocument created from the returned XML.</returns>
        private async Task<XDocument> RequestXDocumentAsync(Uri uri)
        {
            var request = WebRequest.Create(uri);
            var response = await request.GetResponseAsync() as HttpWebResponse;
            if (response.StatusCode != HttpStatusCode.OK)
                throw new Exception($"MTConnect Agent reported failure: {response.StatusCode} {response.StatusDescription}");

            return await XDocumentFromResponseAsync(response as HttpWebResponse);
        }

        private async IAsyncEnumerable<XDocument> RequestXDocumentsAsync(Uri uri)
        {
            var request = WebRequest.Create(uri);
            var response = await request.GetResponseAsync() as HttpWebResponse;
            if (response.StatusCode != HttpStatusCode.OK)
                throw new Exception($"MTConnect Agent reported failure: {response.StatusCode} {response.StatusDescription}");

            await foreach (var doc in XDocumentsFromResponseAsync(response as HttpWebResponse))
                yield return doc;
        }

        /// <summary>
        /// Build a Uri from the base Uri and possibly-null subpath components, ignoring nulls
        /// </summary>
        /// <param name="baseUri"></param>
        /// <param name="paths"></param>
        /// <returns></returns>
        private Uri BuildUri(Uri baseUri, params string[] paths)
        {
            if (paths == null || paths.Length == 0)
                return baseUri;

            var subPath = string.Join('/', paths.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray());
            var builder = new UriBuilder(baseUri);
            builder.Path = subPath;

            return builder.Uri;
        }

        /// <summary>
        /// Build a Uri from the base Uri and tuples for the query string
        /// </summary>
        /// <param name="baseUri"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        private Uri BuildUriQuery(Uri baseUri, params (string Key, object Value)[] values)
        {
            if (values.Length == 0)
                return baseUri;

            var queryString = string.Join('&', values.Where(t => t.Item2 != null).Select(t => $"{t.Key}={t.Value}").ToArray());
            var builder = new UriBuilder(baseUri);
            builder.Query = queryString;

            return builder.Uri;
        }

        /// <summary>
        /// Agent Constructor
        /// </summary>
        /// <param name="baseUri">The base Uri of the MTConnect agent</param>
        public Agent(string baseUri)
        {
            _baseUri = new Uri(baseUri);
        }

        /// <summary>
        /// The BaseUri of the MTConnect agent.
        /// </summary>
        public Uri BaseUri => _baseUri;

        /// <summary>
        /// Send a Probe request.
        /// </summary>
        /// <param name="deviceName"></param>
        /// <returns></returns>
        public async Task<XDocument> ProbeAsync(string deviceName = null)
        {
            var uri = _baseUri;
            if (deviceName != null)
                uri = new Uri(_baseUri, deviceName);

            return await RequestXDocumentAsync(uri);
        }

        /// <summary>
        /// Send a Current request
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="at"></param>
        /// <param name="path"></param>
        /// <param name="interval"></param>
        /// <returns></returns>
        public async Task<XDocument> CurrentAsync(string deviceName = null, ulong? at = null, string path = null)
        {
            var uri = _baseUri;
            uri = BuildUri(uri, deviceName, "current");
            uri = BuildUriQuery(uri, ("at", at), ("path", path));

            return await RequestXDocumentAsync(uri);
        }

        /// <summary>
        /// Send a Current request with an interval specified.
        /// </summary>
        /// <param name="interval"></param>
        /// <param name="deviceName"></param>
        /// <param name="at"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<XDocument> CurrentAsync(ulong interval, string deviceName = null, ulong? at = null, string path = null)
        {
            var uri = _baseUri;
            uri = BuildUri(uri, deviceName, "current");
            uri = BuildUriQuery(uri, ("at", at), ("path", path), ("interval", interval));

            await foreach(var doc in RequestXDocumentsAsync(uri))
                yield return doc;
        }

        /// <summary>
        /// Send a Sample request.
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="from"></param>
        /// <param name="path"></param>
        /// <param name="interval"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public async Task<XDocument> SampleAsync(string deviceName = null, ulong? from = null, string path = null, ulong? count = null)
        {
            var uri = _baseUri;
            uri = BuildUri(uri, deviceName, "sample");
            uri = BuildUriQuery(uri, ("from", from), ("path", path), ("count", count));

            return await RequestXDocumentAsync(uri);
        }
        /// <summary>
        /// Send a Sample request with an interval specified.
        /// </summary>
        /// <param name="interval"></param>
        /// <param name="deviceName"></param>
        /// <param name="from"></param>
        /// <param name="path"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<XDocument> SampleAsync(ulong interval, string deviceName = null, ulong? from = null, string path = null, ulong? count = null)
        {
            var uri = _baseUri;
            uri = BuildUri(uri, deviceName, "sample");
            uri = BuildUriQuery(uri, ("from", from), ("path", path), ("interval", interval), ("count", count));

            await foreach (var doc in RequestXDocumentsAsync(uri))
                yield return doc;
        }

        /// <summary>
        /// Send an Asset/Assets request.
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="type"></param>
        /// <param name="removed"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public async Task<XDocument> AssetAsync(string assetId = null, string type = null, string removed = null, ulong? count = null)
        {
            var uri = _baseUri;
            if (string.IsNullOrWhiteSpace(assetId))
                uri = BuildUri(uri, "assets");
            else
                uri = BuildUri(uri, "asset", assetId);
            uri = BuildUriQuery(uri, ("type", type), ("removed", removed), ("count", count));

            return await RequestXDocumentAsync(uri);
        }
    }
}
