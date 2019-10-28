using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows.Forms;
using CsvHelper;
using MetroFramework.Forms;
using Helloprofit_product_list.Models;
using Newtonsoft.Json.Linq;

namespace Helloprofit_product_list
{
    public partial class Form1 : MetroForm
    {
        public bool LogToUi = true;
        public bool LogToFile = true;

        private readonly string _path = Application.StartupPath;
        Random rnd = new Random();
        public HttpCaller HttpCaller = new HttpCaller();
        private string _user;
        private string _pass;
        private string _output;
        bool _isConsole = AppDomain.CurrentDomain.FriendlyName.Contains("Console");
        private int _threads;

        public Form1()
        {
            InitializeComponent();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            ServicePointManager.DefaultConnectionLimit = 65000;
            //Control.CheckForIllegalCrossThreadCalls = false;
            //this.FormBorderStyle = FormBorderStyle.FixedSingle;
            //this.MaximizeBox = false;
            // this.MinimizeBox = false;
            Directory.CreateDirectory("data");
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Utility.CreateDb();
            Utility.LoadConfig();
            Utility.InitCntrl(this);
            if (_isConsole)
            {
                await Work().ContinueWith((x) =>
                {
                    Application.Exit();
                });
            }
        }

        static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.ToString(), @"Unhandled Thread Exception");
        }
        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show((e.ExceptionObject as Exception)?.ToString(), @"Unhandled UI Exception");
        }
        #region UIFunctions
        public delegate void WriteToLogD(string s, Color c);
        public void WriteToLog(string s, Color c)
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new WriteToLogD(WriteToLog), s, c);
                    return;
                }
                if (LogToUi)
                {
                    if (DebugT.Lines.Length > 5000)
                    {
                        DebugT.Text = "";
                    }
                    DebugT.SelectionStart = DebugT.Text.Length;
                    DebugT.SelectionColor = c;
                    DebugT.AppendText(DateTime.Now.ToString(Utility.SimpleDateFormat) + " : " + s + Environment.NewLine);
                }
                Console.WriteLine(DateTime.Now.ToString(Utility.SimpleDateFormat) + @" : " + s);
                if (LogToFile)
                {
                    File.AppendAllText(_path + "/data/log.txt", DateTime.Now.ToString(Utility.SimpleDateFormat) + @" : " + s + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        public void NormalLog(string s)
        {
            WriteToLog(s, Color.Black);
        }
        public void ErrorLog(string s)
        {
            WriteToLog(s, Color.Red);
        }
        public void SuccessLog(string s)
        {
            WriteToLog(s, Color.Green);
        }
        public void CommandLog(string s)
        {
            WriteToLog(s, Color.Blue);
        }

        public delegate void SetProgressD(int x);
        public void SetProgress(int x)
        {
            if (InvokeRequired)
            {
                Invoke(new SetProgressD(SetProgress), x);
                return;
            }
            if ((x <= 100))
            {
                ProgressB.Value = x;
            }
        }
        public delegate void DisplayD(string s);
        public void Display(string s)
        {
            if (InvokeRequired)
            {
                Invoke(new DisplayD(Display), s);
                return;
            }
            displayT.Text = s;
        }

        #endregion


        public class Product
        {
            //public string Id { get; set; }
            public string Name { get; set; }
            public string Group { get; set; }
            public string Asin { get; set; }
            public string Sku { get; set; }
            public string ParentAsin { get; set; }


        }

        async Task<(List<Product> products, string error)> GetProductsPage(int page)
        {
            var resp = await HttpCaller.GetJson("https://app.helloprofit.com/merchant/products?limit=100&offset=0&order=asc&page=" + page);
            if (resp.error != null)
            {
                //ErrorLog(resp.error);
                return (null, resp.error);
            }

            try
            {
                var obj = JObject.Parse(resp.json);
                var rows = (JArray)obj["rows"];
                if (rows == null) return (null, "no prodcuts");
                Console.WriteLine(rows.Count);
                var products = new List<Product>();
                foreach (var row in rows)
                {
                    var doc = new HtmlAgilityPack.HtmlDocument();
                    var nameHtml = (string)row["name"];
                    var id = (string)row["id"];
                    var tagsHtml = (string)row["tags"];
                    var asin = (string)row["asin"];
                    var parentAsin = (string)row["parentAsin"];
                    var sku = (string)row["sku"];
                    doc.LoadHtml(nameHtml);
                    var name = doc.DocumentNode.SelectSingleNode("//a")?.InnerText;
                    doc.LoadHtml(tagsHtml);
                    var groups = doc.DocumentNode.SelectSingleNode("//span[@class='label label-default']")?.InnerText;
                    products.Add(new Product { Name = name, Asin = asin, Sku = sku, Group = groups, ParentAsin = parentAsin });
                }
                return (products, null);
            }
            catch (Exception e)
            {
                return (null, e.ToString());
            }
        }

        async Task<string> LoginToHelloProfit()
        {

            var data = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("_method", "POST"), new KeyValuePair<string, string>("username", _user), new KeyValuePair<string, string>("password", _pass) };
            Display("Logging in to HelloProfit..");
            var resp = await HttpCaller.PostFormData("https://app.helloprofit.com/login", data);
            return resp.error;
        }
        private async void startB_Click(object sender, EventArgs e)
        {
            await Work();
        }

        async Task Work()
        {
            try
            {
                var conf = File.ReadAllLines(_path + "/config.txt").ToList();
                _user = conf[0];
                _pass = conf[1];
                _output = conf[2];
                _threads=int.Parse(conf[3]);
            }
            catch (Exception ex)
            {
                ErrorLog($"failed to read config {ex}");
                return;
            }
            var loginResp = await LoginToHelloProfit();
            if (loginResp != null)
            {
                ErrorLog(loginResp);
                return;
            }
            var firstResp = await HttpCaller.GetJson("https://app.helloprofit.com/merchant/products?limit=1&offset=0&order=asc&page=" + 1);
            if (firstResp.error != null)
            {
                ErrorLog(firstResp.error);
                return;
            }
            var obj = JObject.Parse(firstResp.json);
            var total = (int)obj["total"];
            var pages = total / 100;
            if (total % 100 != 0) pages++;
            var page = 1;
            var nbr = 0;
            Console.WriteLine($@"items : {total} , pages : {pages}");
            var products = new List<Product>();
            var scrapeBlock = new TransformBlock<int, (List<Product> products, string error)>(GetProductsPage, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = _threads });
            for (var i = 1; i <= pages; i++)
            {
                await scrapeBlock.SendAsync(i);
                //break;
            }
            for (var i = 1; i <= pages; i++)
            {
                var resp = await scrapeBlock.ReceiveAsync();
                if (resp.error != null)
                {
                    ErrorLog(resp.error);
                    continue;
                }
                products.AddRange(resp.products);
                Display($"collected {products.Count} / {total}");
                SetProgress(products.Count * 100 / total);
                //break;
            }

            try
            {
                using (var writer = new StreamWriter(_output))
                using (var csv = new CsvWriter(writer))
                {
                    csv.WriteRecords(products);
                }
            }
            catch (Exception exception)
            {
                ErrorLog(exception.ToString());
            }
            Display($"Completed , collected {products.Count} / {total}");
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Utility.Config = new Dictionary<string, string>();
            Utility.SaveCntrl(this);
            Utility.SaveConfig();
        }
    }
}
