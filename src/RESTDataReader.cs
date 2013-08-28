using System;
using System.Net;
using System.Text;
using System.Reflection;
using DataAccess;
using RestSharp;
using RestSharp.Extensions;

namespace RESTDataAccess
{
	public class RESTDataReader : DataReader
	{
		/// <summary>
		/// The RestSharp client.
		/// </summary>
		private RestClient _client;

		/// <summary>
		/// Initializes a new instance of the <see cref="RESTDataAccess.RESTDataReader"/> class.
		/// </summary>
		public RESTDataReader() : base(null, null) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="RESTDataAccess.RESTDataReader"/> class.
		/// </summary>
		/// <param name="dataSourceName">Data source name.</param>
		public RESTDataReader(string dataSourceName) : base(dataSourceName, null) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="RESTDataAccess.RESTDataReader"/> class.
		/// </summary>
		/// <param name="auth">Authentication.</param>
		public RESTDataReader(Authentication auth) : base(null, auth) { }

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
			if (Authentication != null) 
			{
				_client.Authenticator = new HttpBasicAuthenticator (Authentication.UserName, 
				                                                    Authentication.Password);
			}

		}

		/// <summary>
		/// Returns one or multiple documents from the datasource.
		/// </summary>
		/// <param name="request">A request instance.</param>
		/// <typeparam name="T">The type to be returned.</typeparam>
		public override Response<T> Get<T> (GetRequest request)
		{ 
			RestRequest r = new RestRequest ();
			r.Resource = request.Resource;

			return Execute<T> (r);
		}

		/// <summary>
		/// Returns an individual item from the datasource.
		/// </summary>
		/// <param name="request">A request instance.</param>
		/// <typeparam name="T">The type to be returned.</typeparam>
		public override Response<T> Get<T>(GetRequestItem request) 
		{ 
			throw new NotImplementedException ();
		}

		private Response<T> Execute<T>(RestRequest request) where T: new()
		{
			// TODO Make sure this is still needed, or how Exceptions should be handled.
			request.OnBeforeDeserialization = (resp) =>
				{
					// for individual resources when there's an error to make
					// sure that RestException props are populated
					if (((int)resp.StatusCode) >= 400)
					{
						// have to read the bytes so .Content doesn't get populated
	                    string restException = "{{ \"RestException\" : {0} }}";
						var content = resp.RawBytes.AsString(); //get the response content
	                    var newJson = string.Format(restException, content);

	                    resp.Content = null;
						resp.RawBytes = Encoding.UTF8.GetBytes(newJson.ToString());
					}
				};

		  	RestResponse<T> restResponse = (RestResponse<T>)_client.Execute<T> (request);
			Response<T> response = new Response<T>();
			response.ErrorException = restResponse.ErrorException;
			response.ErrorMessage = restResponse.ErrorMessage;
			response.StatusDescription = restResponse.StatusDescription;
			response.ResponseStatus = (DataAccess.ResponseStatus)restResponse.ResponseStatus;
			response.Content = restResponse.Data;

			// TODO StatusCode enum needs better documentation and completion (how to handle the default case?)
			switch (restResponse.StatusCode) {
			case HttpStatusCode.Accepted:
				response.StatusCode = StatusCode.Accepted;
				break;
			case HttpStatusCode.Ambiguous:
				response.StatusCode = StatusCode.Ambiguous;
				break;
			default:
				response.StatusCode = StatusCode.NotAvailable;
			}

			return response;
		}
	}
}

