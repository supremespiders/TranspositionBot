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
        private string _baseUrl;
        private string _baseUrl2 = "https://app.helloprofit.com/merchant/product_fees";
        private Dictionary<string, (double cost, string secondId)> _costs;

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
                this.WindowState = FormWindowState.Minimized;
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
            public string Id { get; set; }
            public string Id2 { get; set; }
            public string Name { get; set; }
            public string Group { get; set; }
            public string Asin { get; set; }
            public string Sku { get; set; }
            public string ParentAsin { get; set; }
            public double Cost { get; set; }


        }

        async Task<(List<Product> products, string error)> GetProductsPage(int page)
        {
            var resp = await HttpCaller.GetJson(_baseUrl + "&limit=100&offset=0&page=" + page);
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
                    products.Add(new Product { Id = id, Id2 = _costs.ContainsKey(sku) ? _costs[sku].secondId : "", Name = name, Asin = asin, Sku = sku, Group = groups, ParentAsin = parentAsin, Cost = _costs.ContainsKey(sku) ? _costs[sku].cost : 0.0 });
                }
                return (products, null);
            }
            catch (Exception e)
            {
                return (null, e.ToString());
            }
        }

        async Task<(Dictionary<string, (double cost, string secondId)> costs, string error)> GetFeesPage(int page)
        {
            var resp = await HttpCaller.GetJson(_baseUrl2 + "?limit=100&offset=0&page=" + page);
            if (resp.error != null)
                return (null, resp.error);

            try
            {
                var obj = JObject.Parse(resp.json);
                var rows = (JArray)obj["rows"];
                if (rows == null) return (null, "no prodcuts");
                Console.WriteLine(rows.Count);
                var products = new Dictionary<string, (double cost, string secondId)>();
                foreach (var row in rows)
                {
                    var doc = new HtmlAgilityPack.HtmlDocument();
                    var costHtml = (string)row["unit_cost"];
                    var sku = (string)row["sku"];
                    doc.LoadHtml(costHtml);
                    var secondId = Utility.BetweenStrings(costHtml, "product_id=", "&");
                    Console.WriteLine(secondId);
                    var pk = doc.DocumentNode.SelectSingleNode("//a").GetAttributeValue("data-pk", "");
                    Console.WriteLine(pk);
                    var cost = double.Parse(Utility.PriceRegex.Replace(doc.DocumentNode.SelectSingleNode("//a")?.InnerText ?? "0.0", ""));
                    if (!products.ContainsKey(sku))
                        products.Add(sku, (cost, pk));
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

        async Task GetCosts()
        {
            var firstResp = await HttpCaller.GetJson(_baseUrl2 + "?limit=1&offset=0&page=" + 1);
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

            _costs = new Dictionary<string, (double cost, string secondId)>();
            var tpl = new TransformBlock<int, (Dictionary<string, (double cost, string secondId)> costs, string error)>(GetFeesPage, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 20 });
            for (var i = 1; i <= pages; i++)
                tpl.Post(i);
            for (var i = 1; i <= pages; i++)
            {
                var resp = await tpl.ReceiveAsync();
                if (resp.error != null)
                {
                    ErrorLog(resp.error);
                    continue;
                }

                foreach (var respCost in resp.costs)
                {
                    if (!_costs.ContainsKey(respCost.Key))
                        _costs.Add(respCost.Key, respCost.Value);
                }
                Display($"collected fees {_costs.Count} / {total}");
                SetProgress(_costs.Count * 100 / total);
            }
        }

        async Task Work()
        {
            try
            {
                var conf = File.ReadAllLines(_path + "/config.txt").ToList();
                _user = conf[0];
                _pass = conf[1];
                _output = conf[2];
                _threads = int.Parse(conf[3]);
                _baseUrl = conf[4];
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
            await GetCosts();
            Console.WriteLine("done");
            var firstResp = await HttpCaller.GetJson(_baseUrl + "&limit=1&offset=0&page=" + 1);
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

        private void loadInputB_Click(object sender, EventArgs e)
        {
            OpenFileDialog o = new OpenFileDialog { Filter = @"csv|*.csv", InitialDirectory = _path };
            if (o.ShowDialog() == DialogResult.OK)
            {
                inputI.Text = o.FileName;
            }
        }

        private void openInputB_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(inputI.Text);
            }
            catch (Exception ex)
            {
                ErrorLog(ex.ToString());
            }
        }

        private async void UpdateTagsB_Click(object sender, EventArgs e)
        {
            try
            {
                var conf = File.ReadAllLines(_path + "/config.txt").ToList();
                _user = conf[0];
                _pass = conf[1];
                _output = conf[2];
                _threads = int.Parse(conf[3]);
                _baseUrl = conf[4];
            }
            catch (Exception ex)
            {
                ErrorLog($"failed to read config {ex}");
                return;
            }
            List<Product> products = null;
            try
            {
                using (var reader = new StreamReader(inputI.Text))
                using (var csv = new CsvReader(reader))
                {
                    products = csv.GetRecords<Product>().ToList();
                }
            }
            catch (Exception exception)
            {
                ErrorLog(exception.ToString());
                return;
            }
            var loginResp = await LoginToHelloProfit();
            if (loginResp != null)
            {
                ErrorLog(loginResp);
                return;
            }

            var updateBlock = new TransformBlock<Product, string>(UpdateTag, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 20 });

            foreach (var product in products)
                updateBlock.Post(product);
            var nbr = 0;
            var total = products.Count;
            var good = 0;
            foreach (var product in products)
            {
                var resp = await updateBlock.ReceiveAsync();
                nbr++;
                if (resp != null)
                    ErrorLog(resp);
                else
                    good++;
                Display($"tags updated {nbr} / {total} , success : {good}");
                SetProgress(nbr * 100 / total);
            }
            Display($"completed tags update  {nbr} / {total} , success : {good}");

        }

        async Task<string> UpdateTag(Product product)
        {
            Console.WriteLine(product.Id + " " + product.Asin + " " + product.Group);
            var resp = await HttpCaller.PostFormData($"https://app.helloprofit.com/merchant/products/edit/{product.Id}?merchant_id=2894", new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("_method","PUT"),
                new KeyValuePair<string, string>("id",product.Id),
                new KeyValuePair<string, string>("tags[tag_type_id]",""),
                new KeyValuePair<string, string>("tags[tag_type_id][]",product.Group),
                new KeyValuePair<string, string>("tags[model]","HelloProfit.Products"),
                new KeyValuePair<string, string>("saveTags","1"),
            });
            if (resp.error != null)
                return (resp.error);
            Console.WriteLine(resp.html);
            try
            {
                JObject obj = JObject.Parse(resp.html);
                bool success = (bool)obj.SelectToken("success");
                string msg = (string)obj.SelectToken("msg");
                if (!success)
                    return (msg);
                return null;
            }
            catch (Exception e)
            {
                return (e.ToString());
            }
        }

        async Task<string> UpdateCost(Product product)
        {
            var resp = await HttpCaller.PostFormData($"https://app.helloprofit.com/merchant/product_fees/edit?merchant_id=2894&product_id={product.Id}&region_id=1&product_cogs_id=0", new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("name","unit_cost"),
                new KeyValuePair<string, string>("value",product.Cost.ToString("#0.00")),
                new KeyValuePair<string, string>("pk",product.Id2),
            });
            if (resp.error != null)
                return (resp.error);
            Console.WriteLine(resp.html);
            try
            {
                JObject obj = JObject.Parse(resp.html);
                bool success = (bool)obj.SelectToken("success");
                string msg = (string)obj.SelectToken("msg");
                if (!success)
                    return (msg);
                return null;
            }
            catch (Exception e)
            {
                return (e.ToString());
            }
        }

        private async void updateCostsB_Click(object sender, EventArgs e)
        {
            try
            {
                var conf = File.ReadAllLines(_path + "/config.txt").ToList();
                _user = conf[0];
                _pass = conf[1];
                _output = conf[2];
                _threads = int.Parse(conf[3]);
                _baseUrl = conf[4];
            }
            catch (Exception ex)
            {
                ErrorLog($"failed to read config {ex}");
                return;
            }
            List<Product> products = null;
            try
            {
                using (var reader = new StreamReader(inputI.Text))
                using (var csv = new CsvReader(reader))
                {
                    products = csv.GetRecords<Product>().ToList();
                }
            }
            catch (Exception exception)
            {
                ErrorLog(exception.ToString());
                return;
            }
            var loginResp = await LoginToHelloProfit();
            if (loginResp != null)
            {
                ErrorLog(loginResp);
                return;
            }

            //await UpdateCost(new Product() { Id = "11363359" });

            var updateBlock = new TransformBlock<Product, string>(UpdateCost, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 20 });

            foreach (var product in products)
                updateBlock.Post(product);
            var nbr = 0;
            var total = products.Count;
            var good = 0;
            foreach (var product in products)
            {
                var resp = await updateBlock.ReceiveAsync();
                nbr++;
                if (resp != null)
                    ErrorLog(resp);
                else
                    good++;
                Display($"costs updated {nbr} / {total} , success : {good}");
                SetProgress(nbr * 100 / total);
            }
            Display($"completed costs update  {nbr} / {total} , success : {good}");
        }
    }
}
