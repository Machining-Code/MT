using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Mt
{
    public class Agent
    {
        private Uri _baseUri;

        private async Task<XDocument> XDocumentFromResponseAsync(HttpWebResponse response)
        {
            using var reader = new StreamReader(response.GetResponseStream());
            var content = await reader.ReadToEndAsync();

            return XDocument.Parse(content);
        }

        private async Task<XDocument> RequestXDocumentAsync(Uri uri)
        {
            var request = WebRequest.Create(uri);
            var response = await request.GetResponseAsync() as HttpWebResponse;
            if (response.StatusCode != HttpStatusCode.OK)
                throw new Exception($"MTConnect Agent reported failure: {response.StatusCode} {response.StatusDescription}");

            return await XDocumentFromResponseAsync(response as HttpWebResponse);
        }

        private Uri BuildUri(Uri baseUri, params string[] paths)
        {
            if (paths == null || paths.Length == 0)
                return baseUri;

            var subPath = string.Join('/', paths.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray());
            var builder = new UriBuilder(baseUri);
            builder.Path = subPath;

            return builder.Uri;
        }

        private Uri BuildUriQuery(Uri baseUri, params (string Key, object Value)[] values)
        {
            if (values.Length == 0)
                return baseUri;

            var queryString = string.Join('&', values.Where(t => t.Item2 != null).Select(t => $"{t.Key}={t.Value}").ToArray());
            var builder = new UriBuilder(baseUri);
            builder.Query = queryString;

            return builder.Uri;
        }

        public Agent(string baseUri)
        {
            _baseUri = new Uri(baseUri);
        }

        public async Task<XDocument> ProbeAsync(string deviceName = null)
        {
            var uri = _baseUri;
            if (deviceName != null)
                uri = new Uri(_baseUri, deviceName);

            return await RequestXDocumentAsync(uri);
        }

        public async Task<XDocument> CurrentAsync(string deviceName = null, ulong? at = null, string path = null, ulong? interval = null)
        {
            var uri = _baseUri;
            uri = BuildUri(uri, deviceName, "current");
            uri = BuildUriQuery(uri, ("at", at), ("path", path), ("interval", interval));

            return await RequestXDocumentAsync(uri);
        }

        public async Task<XDocument> SampleAsync(string deviceName = null, ulong? from = null, string path = null, ulong? interval = null, ulong? count = null)
        {
            var uri = _baseUri;
            uri = BuildUri(uri, deviceName, "sample");
            uri = BuildUriQuery(uri, ("from", from), ("path", path), ("interval", interval), ("count", count));

            return await RequestXDocumentAsync(uri);
        }

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
