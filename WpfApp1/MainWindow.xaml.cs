using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CefSharp;
using CefSharp.SchemeHandler;
using CefSharp.Wpf;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using Newtonsoft.Json;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            InitBrowser();
        }

        public ChromiumWebBrowser browser;

        public async Task InitBrowser()
        {
            var settings = new CefSettings();

            settings.RegisterScheme(new CefCustomScheme
            {
                SchemeName = "localfolder",
                DomainName = "sfx",
                SchemeHandlerFactory = new FolderSchemeHandlerFactory(
                    rootFolder: @"D:\sfx-repo\src\standalone-v2\src\sfx",
                    hostName: "sfx",
                    defaultPage: "index.html"
                )
            });

            
            Cef.Initialize(settings);
            browser = new ChromiumWebBrowser("http://google.com");

            browserContainer.Content = browser;

            browser.IsBrowserInitializedChanged += Browser_IsBrowserInitializedChanged;
            browser.JavascriptMessageReceived += OnBrowserJavascriptMessageReceived;

        }

        private async void Browser_IsBrowserInitializedChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (browser.IsBrowserInitialized)
            {

                browser.ShowDevTools();

                dynamic jsonObject = new JObject();
                jsonObject["preloadFunction"] = "CefSharp.BindObjectAsync";
                jsonObject["windowPath"] = "CefSharp.PostMessage";
                jsonObject["passObjectAsString"] = true;
                jsonObject["handleAsCallBack"] = true;
                browser.Load("localfolder://sfx?integrationConfig=" + jsonObject.ToString());
            }
        }

        private async void OnBrowserJavascriptMessageReceived(object sender, JavascriptMessageReceivedEventArgs e)
        {
            //Complext objects are initially expresses as IDicionary
            //You can use dynamic to access properties (the IDicionary is an ExpandoObject)
            //dynamic msg = e.Message;
            //Alternatively you can use the built in Model Binder to convert to a custom model
            var msg = e.ConvertMessageTo<PostMessageExample>();
            var callback = (IJavascriptCallback)msg.Callback;

            HttpRequest httpRequest = JsonConvert.DeserializeObject<HttpRequest>(msg.data);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage();
            Console.WriteLine(httpRequest);

            switch (httpRequest.method.ToUpper())
            {
                case "POST":
                    httpRequestMessage.Method = HttpMethod.Post;
                    break;
                case "GET":
                    httpRequestMessage.Method = HttpMethod.Get;
                    break;
                case "DELETE":
                    httpRequestMessage.Method = HttpMethod.Delete;
                    break;
                case "PUT":
                    httpRequestMessage.Method = HttpMethod.Put;
                    break;
                default:
                    break;
            }
            httpRequestMessage.RequestUri = new Uri("http://localhost:2500" + httpRequest.url);

            if (httpRequest.headers != null)
            {
                foreach (var header in httpRequest.headers)
                {
                    httpRequestMessage.Headers.Add(header.key, header.value);
                }
            }

            if (httpRequest.body != null)
            {
                httpRequestMessage.Content = new StringContent(httpRequest.body.ToString(), Encoding.UTF8, "application/json");
            }

            HttpClient client = new();
            var response = await client.SendAsync(httpRequestMessage);
            dynamic jsonObject = new JObject();
            jsonObject["statusCode"] = response.StatusCode.ToString();
            jsonObject["statusMessage"] = response.IsSuccessStatusCode;

            string responseBody = await response.Content.ReadAsStringAsync();
            try
            {
                jsonObject["data"] = JObject.Parse(responseBody);

            }
            catch
            {
                jsonObject["data"] = JArray.Parse(responseBody);

            }
            //jsonObject["headers"] = response.Headers.ToHashSet();

            callback.ExecuteAsync(jsonObject.ToString());
        }

        public class PostMessageExample
        {
            public string Type { get; set; }
            public string data { get; set; }
            public IJavascriptCallback Callback;
        }


        public class HttpHeader
        {
            public string key { get; set; }
            public string value { get; set; }
        }

        public class HttpRequest
        {
            public string method { get; set; }
            public string url { get; set; }
            public HttpHeader[]? headers { get; set; }
            public JObject? body { get; set; }
        }
    }
}
