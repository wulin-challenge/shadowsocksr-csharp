﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using SimpleJson;

using Shadowsocks.Model;
using Shadowsocks.Util;

namespace Shadowsocks.Controller
{
    public class UpdateChecker
    {
        private const string UpdateURL = "https://api.github.com/repos/shadowsocks/shadowsocks-windows/releases";
        private const string UserAgent = "Mozilla/5.0 (Windows NT 5.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/35.0.3319.102 Safari/537.36";

        private Configuration config;
        public bool NewVersionFound;
        public string LatestVersionNumber;
        public string LatestVersionName;
        public string LatestVersionURL;
        public string LatestVersionLocalName;
        public event EventHandler CheckUpdateCompleted;

        public const string Version = "2.5.8";

        public void CheckUpdate(Configuration config)
        {
            this.config = config;

            try
            {
                WebClient http = CreateWebClient();
                http.DownloadStringCompleted += http_DownloadStringCompleted;
                http.DownloadStringAsync(new Uri(UpdateURL));
            }
            catch (Exception ex)
            {
                Logging.LogUsefulException(ex);
            }
        }

        private void http_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                string response = e.Result;

                JsonArray result = (JsonArray)SimpleJson.SimpleJson.DeserializeObject(e.Result);

                List<Asset> asserts = new List<Asset>();
                foreach (JsonObject release in result)
                {
                    if ((bool)release["prerelease"])
                    {
                        continue;
                    }
                    foreach (JsonObject asset in (JsonArray)release["assets"])
                    {
                        Asset ass = new Asset();
                        ass.Parse(asset);
                        if (ass.IsNewVersion(Version))
                        {
                            asserts.Add(ass);
                        }
                    }
                }

                if (asserts.Count != 0)
                {
                    SortByVersions(asserts);
                    Asset asset = asserts[asserts.Count - 1];
                    NewVersionFound = true;
                    LatestVersionURL = asset.browser_download_url;
                    LatestVersionNumber = asset.version;
                    LatestVersionName = asset.name;

                    startDownload();
                }
                else if (CheckUpdateCompleted != null)
                {
                    CheckUpdateCompleted(this, new EventArgs());
                }
            }
            catch (Exception ex)
            {
                Logging.LogUsefulException(ex);
            }
        }

        private void startDownload()
        {
            try
            {
                string temppath = Utils.GetTempPath();
                LatestVersionLocalName = Path.Combine(temppath, LatestVersionName);
                WebClient http = CreateWebClient();
                http.DownloadFileCompleted += Http_DownloadFileCompleted;
                http.DownloadFileAsync(new Uri(LatestVersionURL), LatestVersionLocalName);
            }
            catch (Exception ex)
            {
                Logging.LogUsefulException(ex);
            }
        }

        private void Http_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            try
            {
                if(e.Error != null)
                {
                    Logging.LogUsefulException(e.Error);
                    return;
                }
                if (CheckUpdateCompleted != null)
                {
                    CheckUpdateCompleted(this, new EventArgs());
                }
            }
            catch (Exception ex)
            {
                Logging.LogUsefulException(ex);
            }
        }

        private WebClient CreateWebClient()
        {
            WebClient http = new WebClient();
            http.Headers.Add("User-Agent", UserAgent);
            http.Proxy = new WebProxy(IPAddress.Loopback.ToString(), config.localPort);
            return http;
        }

        private void SortByVersions(List<Asset> asserts)
        {
            asserts.Sort(new VersionComparer());
        }

        public class Asset
        {
            public bool prerelease;
            public string name;
            public string version;
            public string browser_download_url;

            public bool IsNewVersion(string currentVersion)
            {
                if (prerelease)
                {
                    return false;
                }
                if (version == null)
                {
                    return false;
                }
                return CompareVersion(version, currentVersion) > 0;
            }

            public void Parse(JsonObject asset)
            {
                name = (string)asset["name"];
                browser_download_url = (string)asset["browser_download_url"];
                version = ParseVersionFromURL(browser_download_url);
                prerelease = browser_download_url.IndexOf("prerelease") >= 0;
            }

            private static string ParseVersionFromURL(string url)
            {
                Match match = Regex.Match(url, @".*Shadowsocks-win.*?-([\d\.]+)\.\w+", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    if (match.Groups.Count == 2)
                    {
                        return match.Groups[1].Value;
                    }
                }
                return null;
            }

            public static int CompareVersion(string l, string r)
            {
                var ls = l.Split('.');
                var rs = r.Split('.');
                for (int i = 0; i < Math.Max(ls.Length, rs.Length); i++)
                {
                    int lp = (i < ls.Length) ? int.Parse(ls[i]) : 0;
                    int rp = (i < rs.Length) ? int.Parse(rs[i]) : 0;
                    if (lp != rp)
                    {
                        return lp - rp;
                    }
                }
                return 0;
            }
        }

        class VersionComparer : IComparer<Asset>
        {
            // Calls CaseInsensitiveComparer.Compare with the parameters reversed. 
            public int Compare(Asset x, Asset y)
            {
                return Asset.CompareVersion(x.version, y.version);
            }
        }

    }
}
