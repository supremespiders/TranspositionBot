﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Helloprofit_product_list.Models
{
    public class HttpCaller
    {
        HttpClient _httpClient;
        readonly HttpClientHandler _httpClientHandler = new HttpClientHandler()
        {
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        public HttpCaller()
        {
            _httpClient = new HttpClient(_httpClientHandler);
        }
        public async Task<(HtmlDocument doc, string error)> GetDoc(string url, int maxAttempts = 1)
        {
            var resp = await GetHtml(url, maxAttempts);
            if (resp.error != null) return (null, resp.error);
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(resp.html);
            return (doc, null);
        }
        public async Task<(string html, string error)> GetHtml(string url, int maxAttempts = 1)
        {
            int tries = 0;
            do
            {
                try
                {
                    var response = await _httpClient.GetAsync(url);
                    string html = await response.Content.ReadAsStringAsync();
                    return (html, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    tries++;
                    if (tries == maxAttempts)
                    {
                        return (null, ex.ToString());
                    }
                    await Task.Delay(2000);
                }
            } while (true);
        } 
        public async Task<(string json, string error)> GetJson(string url, int maxAttempts = 1)
        {
            int tries = 0;
            do
            {
                try
                {
                    var httpRequestMessage = new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri(url),
                        Headers = { 
                            { "Accept", "application/json, text/javascript, */*; q=0.01" },
                            { "X-Requested-With", "XMLHttpRequest" },
                            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/72.0.3626.109 Safari/537.36" }
                        }
                    };
                    var response = await _httpClient.SendAsync(httpRequestMessage);
                    var html = await response.Content.ReadAsStringAsync();
                    return (html, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    tries++;
                    if (tries == maxAttempts)
                    {
                        return (null, ex.ToString());
                    }
                    await Task.Delay(2000);
                }
            } while (true);
        }
        public async Task<(string json, string error)> PostJson(string url, string json, int maxAttempts = 1)
        {
            int tries = 0;
            do
            {
                try
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    // content.Headers.Add("x-appeagle-authentication", Token);
                    var r = await _httpClient.PostAsync(url, content);
                    var s = await r.Content.ReadAsStringAsync();
                    return (s, null);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    tries++;
                    if (tries == maxAttempts)
                    {
                        return (null, e.ToString());
                    }
                    await Task.Delay(2000);
                }
            } while (true);

        }
        public async Task<(string html, string error)> PostFormData(string url, List<KeyValuePair<string, string>> formData, int maxAttempts = 1)
        {
            var formContent = new FormUrlEncodedContent(formData);
            int tries = 0;
            do
            {
                try
                {
                    var response = await _httpClient.PostAsync(url, formContent);
                    string html = await response.Content.ReadAsStringAsync();
                    return (html, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    tries++;
                    if (tries == maxAttempts)
                    {
                        return (null, ex.ToString());
                    }
                    await Task.Delay(2000);
                }
            } while (true);
        }
    }
}
