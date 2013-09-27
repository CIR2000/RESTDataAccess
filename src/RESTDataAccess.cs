using Newtonsoft.Json;
using System;
using System.Net;
using System.Text;
using System.Reflection;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using DataAccess;
using RestSharp;
using RestSharp.Extensions;
using System.Runtime.Serialization.Json;

namespace DataAccess.RESTDataAccess
{
//  WIP
//	public class ErrReponse
//	{
//		public ErrReponse () { }
//		public string Status { get; set; }
//		public List<string> Issues { get; set; }
//	}

	public class RESTDataAccess : DataAccessBase
	{
		/// <summary>
		/// The RestSharp client.
		/// </summary>
		private RestClient _client;

        private Dictionary <Comparison, string> Ops = new Dictionary<Comparison, string>()
        {
 			{ Comparison.Equal, "{0}" },
			{ Comparison.NotEqual, "{{ \"$ne\": {0} }}" },
			{ Comparison.GreaterThan, "{{ \"$gt\": {0} }}" },
			{ Comparison.GreaterThenOrEqual, "{{ \"$gte\": {0} }}" },
			{ Comparison.LessThan, "{{ \"$lt\": {0} }}"},
			{ Comparison.LessThanOrEqual, "{{ \"$lte\": {0} }}" },
        };

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="RESTDataAccess.RESTDataReader"/> class.
		/// </summary>
		public RESTDataAccess() : this(null, null) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="RESTDataAccess.RESTDataReader"/> class.
		/// </summary>
		/// <param name="dataSourceName">Data source name.</param>
		public RESTDataAccess(string dataSourceName) : this(dataSourceName, null) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="RESTDataAccess.RESTDataReader"/> class.
		/// </summary>
		/// <param name="auth">Authentication.</param>
		public RESTDataAccess(Authentication auth) : this(null, auth) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="RESTDataAccess.RESTDataReader"/> class.
		/// </summary>
		/// <param name="dataSourceName">Data source name.</param>
		/// <param name="auth">Authentication.</param>
		public RESTDataAccess(string dataSourceName, Authentication auth) : base (dataSourceName, auth) 
		{
			// silverlight friendly way to get current version
			var assembly = Assembly.GetExecutingAssembly();
			AssemblyName assemblyName = new AssemblyName(assembly.FullName);
			var version = assemblyName.Version;

			_client = new RestClient();
			_client.UserAgent = "eve-csharp/" + version; 
            _client.AddDefaultHeader("Accept-charset", "utf-8");
	        _client.AddHandler("application/json", new JsonDeserializer());
			_client.BaseUrl = DataSourceName;
		}

		#endregion
		#region Sync
		/// <summary>
		/// Returns one or multiple documents from the datasource.
		/// </summary>
		/// <param name="request">A request instance.</param>
		/// <typeparam name="T">The type to be returned.</typeparam>
		public override Response<T> Execute<T> (IRequest request)
		{ 
			SetDataSourceName();

			var r = InitRequest<T> (request);
			switch (r.Method) {
			case Method.GET:
				break;
			case Method.POST:
				r.RequestFormat = DataFormat.Json;
				r.AddParameter("application/json; charset=utf-8", 
				               PayloadToJson (request.Payload),
				               ParameterType.RequestBody);
 				break;
			}
			return Execute<T> (r);
		}

		private Response<T> Execute<T>(RestRequest request) where T: new()
		{
			// TODO Make sure this is still needed, or how Exceptions should be handled.
			request.OnBeforeDeserialization = (resp) =>
				{
					// for individual resources when there's an error to make
					// sure that RestException props are populated
//					if (((int)resp.StatusCode) >= 400)
//					{
//						// have to read the bytes so .Content doesn't get populated
//	                    string restException = "{{ \"RestException\" : {0} }}";
//						var content = resp.RawBytes.AsString(); //get the response content
//	                    var newJson = string.Format(restException, content);
//
//	                    resp.Content = null;
//						resp.RawBytes = Encoding.UTF8.GetBytes(newJson.ToString());
//					}
				};

			SetDataSourceName ();
			return ProcessResponse ((RestResponse<T>)_client.Execute<T> (request));
		}

		#endregion
		#region Async
		/// <summary>
		/// Asynchronously gets one or multiple documents from the datasource.
		/// <param name="request">The request instance.</param>
		/// <param name="callback">The callback function to be invoked.</param>
		/// <typeparam name="T">The type to be returned.</typeparam>
		public override void ExecuteAsync<T>(IRequest request, Action<Response<T>, IRequest> callback)
		{
			SetDataSourceName ();
			_client.ExecuteAsync<T> (InitRequest<T> (request), (response) => {
				callback(ProcessResponse((RestResponse<T>)response), request);
			});
		}
		#endregion
		#region Parsing
		/// <summary>
		/// Parses the list of filters into a Eve API-compatible 'where' statement.
		/// </summary>
		/// <returns>The Eve API-compatible 'where' part.</returns>
		/// <param name="filters">Filters.</param>
		/// <param name="typeOfT">Type of T.</param>
		protected string ParseFilters(IList<IFilter> filters, Type typeOfT)
		{
			Func<string>helper = null;

			helper = delegate() {
				var s = new StringBuilder ();
				string concat = string.Empty;

		        foreach (var f in filters)
	            {
					if (f is Filter) {
						var filter = (Filter)f;
						s.Append (concat.Length > 0 ? concat : string.Empty);
						s.Append (string.Format (@"""{0}"": {1}", GetMappedFieldName(filter.Field, typeOfT), ParseFilterValue(filter)));
					} else if (f is FiltersGroup) { 
						var fg = (FiltersGroup)f;
						if (fg.Filters.Count > 0) {
							s.Append (concat.Length > 0 ? concat : string.Empty);
							filters = fg.Filters;
							s.Append (helper());
						}
					}
					if (f.Concatenator == Concatenation.And)
						concat = ", ";
					else if (f.Concatenator == Concatenation.Or)
						concat = ", $or ";
	            }
				return s.Length > 0 ? s.ToString() : string.Empty;
			};

			var ret = helper ();
			return ret != null ? "{ " + ret + " }" : null;
		}

		/// <summary>
		/// Parses the filter value.
		/// </summary>
		/// <returns>The filter value.</returns>
		/// <param name="filter">Filter.</param>
		private string ParseFilterValue(Filter filter)
		{
			object value;

			if (filter.Value is string)
				value = string.Format ("\"{0}\"", filter.Value);
			if (filter.Value is DateTime)
				value = string.Format("\"{0}\"", ((DateTime)filter.Value).ToString ("ddd, dd MMM yyyy HH:mm:ss G\\MT"));
			else
				value = filter.Value;

			if (Ops.ContainsKey(filter.Comparator))
				return string.Format (Ops [filter.Comparator], value);
			else
				throw new NotImplementedException(string.Format("{0} comparator not supported.", filter.Comparator.ToString()));
		}

		/// <summary>
		/// Parses the sort list into a Eve API-compatible 'sort' statement.
		/// </summary>
		/// <returns>The Eve API-compatible 'sort' part.</returns>
		/// <param name="sort">The sort list.</param>
		/// <param name="typeOfT">Type of T.</param>
		protected string ParseSort(IList<Sort> sort, Type typeOfT)

		{
			var s = new StringBuilder ();

			foreach (var st in sort) {
				s.Append (string.Format (@"(""{0}"", {1})", GetMappedFieldName(st.Field, typeOfT), st.Direction == SortDirection.Ascending ? 1 : -1));
				s.Append (", ");
			}
			return s.Length > 0 ? string.Format("[{0}]", s.ToString().TrimEnd(',', ' ')) : null;
		}
		#endregion

		/// <summary>
		/// Sets the RestSharp client BaseUrl according to the DataSourceName property.
		/// </summary>
		private void SetDataSourceName()
		{
			if (DataSourceName != null)
				_client.BaseUrl = DataSourceName;
			else
				throw new ArgumentNullException ("DataSourceName");

		}

		private RestRequest InitRequest<T>(IRequest request)
		{
			var restRequest = new RestRequest ();

			if (request.Resource == null)
				throw new ArgumentNullException ("Resource");

			if (request.Authentication != null) 
				_client.Authenticator = new HttpBasicAuthenticator (request.Authentication.UserName, request.Authentication.Password);
			else if (Authentication != null) 
				_client.Authenticator = new HttpBasicAuthenticator (Authentication.UserName, Authentication.Password);

			switch (request.Method)
			{
			case Methods.Read:
				restRequest.Method = Method.GET;
				break;
			case Methods.Create:
				restRequest.Method = Method.POST;
				break;
			default:
				throw new NotImplementedException ();
			}

			if (request.DocumentId != null) {
				// single document 
				restRequest.Resource = string.Format ("/{0}/{1}/", request.Resource, request.DocumentId);
				if (request.IfNoneMatch != null)
					restRequest.AddParameter ("If-None-Match", request.IfNoneMatch, ParameterType.HttpHeader);
			} else {
				// multiple documents
				restRequest.Resource = request.Resource;
				if (request.Filters.Count > 0) {
					// TODO assumes T is a List. Make some type checking and raise an exception if otherwise?
					Type t = typeof(T).GetGenericArguments () [0];
					if (request.Filters.Count > 0)
						restRequest.AddParameter ("where", ParseFilters (request.Filters, t));
					if (request.Sort.Count > 0)
						restRequest.AddParameter ("sort", ParseSort (request.Sort, t));
				}

			}
			if (request.IfModifiedSince != null)
				restRequest.AddParameter ("If-Modified-Since", request.IfModifiedSince, ParameterType.HttpHeader);

			return restRequest;
		}

		/// <summary>
		/// Payloads to json.
		/// </summary>
		/// <returns>The json representation of the payload.</returns>
		/// <param name="payload">The POST payload.</param>
		/// <typeparam name="T">The 1st type parameter.</typeparam>
		private string PayloadToJson (object payload)
		{
			if (!(payload.GetType ().IsGenericType && payload is IEnumerable))
				throw new ArgumentException ("The payload must be a IEnumerable.");

			var json = new StringBuilder ();
			var i = 0;
			foreach (object o in ((IEnumerable)payload)) {
				var item = JsonConvert.SerializeObject (o);
				json.AppendFormat (@"""item{0}"": {1},", ++i, item);
			}
			return string.Format (@"{{{0}}}", json.ToString ().TrimEnd (','));
		}

		private Response<T> ProcessResponse<T>(RestResponse<T> restResponse)
		{
			var response = new Response<T>();
			response.ErrorException = restResponse.ErrorException;
			response.ErrorMessage = restResponse.ErrorMessage;
			response.StatusDescription = restResponse.StatusDescription;
			response.Content = restResponse.Data;

			// TODO this explicit cast is tricky as we're talking to different classes here, although they are
			// identical.
			response.ResponseStatus = (ResponseStatus)restResponse.ResponseStatus;

			// TODO StatusCode enum needs better documentation and completion (how to handle the default case?)
			switch (restResponse.StatusCode) {
			case HttpStatusCode.OK:
				response.StatusCode = StatusCode.Accepted;
				break;
			case HttpStatusCode.NotModified:
				response.StatusCode = StatusCode.NotModified;
				break;
			case HttpStatusCode.Ambiguous:
				response.StatusCode = StatusCode.Ambiguous;
				break;
			default:
				response.StatusCode = StatusCode.NotAvailable;
				break;
			}
			return response;
		}
	} 
}

