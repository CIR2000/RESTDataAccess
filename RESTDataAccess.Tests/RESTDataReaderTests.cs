using System;
using NUnit.Framework;
using DataAccess.RESTDataAccess;
using System.Collections.Generic;

namespace DataAccess.RESTDataAccess.Tests
{
	[TestFixture ()]
	public class RESTDataReaderTests
	{

		RESTDataAccess _client;

		[SetUp]
		public void Init ()
		{
			_client = new RESTDataAccess ();

		}

		[Test ()]
		public void DataSourceNameConstructor ()
		{
			_client = new RESTDataAccess ("datasource");
			Assert.AreEqual("datasource", _client.DataSourceName);
		}

		[Test ()]
		public void AuthConstructor ()
		{
			_client = new RESTDataAccess (new Authentication { UserName = "user", Password = "pw" });
			Assert.IsInstanceOfType (typeof(Authentication), _client.Authentication);
		}

		[Test ()]
		public void DataSourceNameAndAuthConstructor ()
		{
			_client = new RESTDataAccess ("datasource", new Authentication { UserName = "user", Password = "pw" });
			Assert.IsInstanceOfType (typeof(Authentication), _client.Authentication);
			Assert.AreEqual ("datasource", _client.DataSourceName);
			Assert.AreEqual ("user", _client.Authentication.UserName);
			Assert.AreEqual ("pw", _client.Authentication.Password);
		}

//		[Test ()]
//		public void ParseFilters ()
//		{
//			var fg = new FiltersGroup ();
//			fg.Filters.Add (new Filter ("field", Comparison.Equal, "hello"));
//			Assert.IsInstanceOfType (typeof(Authentication), _client.Authentication);
//			Assert.AreEqual ("datasource", _client.DataSourceName);
//			Assert.AreEqual ("user", _client.Authentication.UserName);
//			Assert.AreEqual ("pw", _client.Authentication.Password);
//		}

	}
}

