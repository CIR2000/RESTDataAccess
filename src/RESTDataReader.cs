using System;
using System.Net;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using DataAccess;
using RestSharp;
using RestSharp.Extensions;

namespace DataAccess.RESTDataAccess
{
	public class RESTDataReader : DataReader
	{
		/// <summary>
		/// The RestSharp client.
		/// </summary>
		private RestClient _client;

        private Dictionary <Comparison, string> Ops = new Dictionary<Comparison, string>()
        {
 			{ Comparison.Equal, "\"{0}\"" },
			{ Comparison.NotEqual, "{{ \"$ne\": \"{0}\" }}" },
			{ Comparison.GreaterThan, "{ $gt: {0} }" },
			{ Comparison.GreaterThenOrEqual, "{ $gte: {0} }" },
			{ Comparison.LessThan, "{ $lt: {0} }"},
			{ Comparison.LessThanOrEqual, "{ $lte: {0} }" }
//            { ComparisonOperator.BeginsWith, new OperatorInfo {Operator=" LIKE ", Suffix="%"}},
//            { ComparisonOperator.Contains, new OperatorInfo {Operator=" LIKE ", Prefix="%", Suffix="%"}},
//            { ComparisonOperator.EndsWith, new OperatorInfo {Operator=" LIKE ", Prefix="%"}},
//            { ComparisonOperator.NotContains, new OperatorInfo {Operator=" NOT LIKE ", Prefix="%", Suffix="%"}},
        };

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="RESTDataAccess.RESTDataReader"/> class.
		/// </summary>
		public RESTDataReader() : this(null, null) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="RESTDataAccess.RESTDataReader"/> class.
		/// </summary>
		/// <param name="dataSourceName">Data source name.</param>
		public RESTDataReader(string dataSourceName) : this(dataSourceName, null) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="RESTDataAccess.RESTDataReader"/> class.
		/// </summary>
		/// <param name="auth">Authentication.</param>
		public RESTDataReader(Authentication auth) : this(null, auth) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="RESTDataAccess.RESTDataReader"/> class.
		/// </summary>
		/// <param name="dataSourceName">Data source name.</param>
		/// <param name="auth">Authentication.</param>
		public RESTDataReader(string dataSourceName, Authentication auth) : base (dataSourceName, auth) 
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
		/// <summary>
		/// Returns one or multiple documents from the datasource.
		/// </summary>
		/// <param name="request">A request instance.</param>
		/// <typeparam name="T">The type to be returned.</typeparam>
		public override Response<T> Get<T> (IGetRequest request)
		{ 
			var restRequest = ProcessRequestBase (request);

			if (request.Filters.Count > 0) {
				// TODO assumes T is a List. Make some type checking and raise an exception if otherwise?
				Type t = typeof(T).GetGenericArguments () [0];
				if (request.Filters.Count > 0)
					restRequest.AddParameter ("where", ParseFilters (request.Filters, t));
				if (request.Sort.Count > 0)
					restRequest.AddParameter ("sort", ParseSort (request.Sort, t));
				if (request.IfModifiedSince != null)
					restRequest.AddParameter ("If-Modified-Since", request.IfModifiedSince, ParameterType.HttpHeader);
			}

			return Execute<T> (restRequest);
		}

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
						s.Append (string.Format (@"""{0}"": {1}", GetMappedFieldName(filter.Field, typeOfT), 
						                         string.Format (Ops [filter.Comparator], filter.Value)));
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
		/// Returns an individual item from the datasource.
		/// </summary>
		/// <param name="request">A request instance.</param>
		/// <typeparam name="T">The type to be returned.</typeparam>
		public override Response<T> Get<T>(IGetRequestItem request) 
		{ 
//			return Execute<T> (ProcessRequestBase(request));
//			throw new NotImplementedException ();
			var restRequest = ProcessRequestBase(request);
			restRequest.Resource = string.Format ("/{0}/{1}/", restRequest.Resource, request.Id);

			if (request.IfNoneMatch != null)
				restRequest.AddParameter ("If-None-Match", request.IfNoneMatch, ParameterType.HttpHeader);

			return Execute<T> (restRequest);
		}

		private RestRequest ProcessRequestBase(IGetRequestBase request)
		{
			var restRequest = new RestRequest ();

			if (request.Resource != null)
				restRequest.Resource = request.Resource;
			else
				throw new ArgumentNullException ("Resource");

			if (request.Authentication != null) 
				_client.Authenticator = new HttpBasicAuthenticator (request.Authentication.UserName, request.Authentication.Password);
			else if (Authentication != null) 
				_client.Authenticator = new HttpBasicAuthenticator (Authentication.UserName, Authentication.Password);

			return restRequest;
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

			if (DataSourceName != null)
				_client.BaseUrl = DataSourceName;
			else
				throw new ArgumentNullException ("DataSourceName");

			return ProcessResponse ((RestResponse<T>)_client.Execute<T> (request));
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
			response.ResponseStatus = (DataAccess.ResponseStatus)restResponse.ResponseStatus;

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

