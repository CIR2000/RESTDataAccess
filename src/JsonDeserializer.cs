using RestSharp;
using RestSharp.Deserializers;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Globalization;
using System.Text;

namespace RESTDataAccess
{

	internal class JsonDeserializer : IDeserializer
	{
		public string RootElement { get; set; }
       	public string Namespace { get; set; }
       	public string DateFormat { get; set; }
       	public CultureInfo Culture { get; set; }
 
       	public JsonDeserializer()
       	{
        	Culture = CultureInfo.InvariantCulture;
       	}
 
       	public T Deserialize<T>(IRestResponse response) 
       	{
           	//T target = new T();
			using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(response.Content))) {
				var ser = new DataContractJsonSerializer (typeof (T));
				return (T)ser.ReadObject (ms);
			}
       }
 
   }
}
