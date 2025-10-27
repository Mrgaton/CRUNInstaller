using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CRUNInstaller.HttpServer
{
    public static class NameValueCollectionExtensions
    {
        public static bool GetBoolean(this NameValueCollection collection, string param, bool defaultValue = default)
        {
            var result = collection[param];

            if (string.IsNullOrEmpty(result)) return defaultValue;

            char fc = char.ToLower(result.Trim()[0]);

            if (fc == 't' || fc == '1') return true;
            if (fc == 'f' || fc == '0') return false;

            return bool.TryParse(result, out bool boolean) ? boolean : defaultValue;
        }
    }

    public static class HttpListenerResponseExtensions
    {
        public static Encoding TextEncoding { get; set; } = Encoding.UTF8;

        public static async Task Return(this HttpListenerResponse res, string message, bool close = true)
        {
            byte[] data = TextEncoding.GetBytes(message);

            res.ContentType = "text/html";
            res.ContentEncoding = TextEncoding;
            res.ContentLength64 = data.LongLength;

            await res.OutputStream.WriteAsync(data, 0, data.Length);

            if(close) 
                res.OutputStream.Close();
        }
        public static async Task Return(this HttpListenerResponse res, bool close = true)
        {
            res.ContentLength64 = 0;

            res.StatusCode = 204;
            if (close) res.OutputStream.Close();
        }
    }
}