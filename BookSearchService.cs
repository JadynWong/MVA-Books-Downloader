
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace bookdown
{
    public class BookSearchService
    {
        private readonly HttpClient _client;
        private readonly ILogger _logger;

        public BookSearchService(HttpClient client, ILogger<BookSearchService> logger)
        {
            _client = client;
            _logger = logger;
        }
        public async Task<Dictionary<string, List<string>>> SearchAsync()
        {
            var dic = new Dictionary<string, List<string>>();

            var ebookHtml = await _client.GetStringAsync("https://mva.microsoft.com/ebooks");

            HtmlDocument html = new HtmlDocument();

            html.LoadHtml(ebookHtml);

            HtmlNode selectable = html.DocumentNode;

            // 利用 Selectable 查询并构造自己想要的数据对象
            var totalElements = selectable.SelectNodes(".//div[@id='eBookList']/div[@class='eBookWrapper']");
            if (totalElements == null)
            {
                return dic;
            }

            foreach (var item in totalElements)
            {

                var bookName = item.Attributes["title"]?.Value;
                using (_logger.BeginScope("book {0}", bookName))
                {

                    //_logger.LogInformation("find book <<{0}>>", bookName);
                    var links = new List<string>();
                    var linkNodes = item.SelectNodes(".//div[@class='eBookLinks']/div[@class='downloadPopup']/ul/li/input");
                    foreach (var node in linkNodes)
                    {
                        var link = node.Attributes["filepath"]?.Value;
                        if (string.IsNullOrWhiteSpace(link))
                        {
                            continue;
                        }
                        links.Add(link);
                        var type = node.Attributes["value"]?.Value;
                        _logger.LogInformation(type + ":" + link);
                    }
                    dic.Add(bookName, links);
                }
            }

            return dic;
        }

        public async Task DownloadBooks(Dictionary<string, List<string>> books)
        {
            HttpClient client = new HttpClient(new HttpClientHandler()
            {
                AllowAutoRedirect = false
            });
            foreach (var book in books)
            {
                var dirPath = Path.Combine(AppContext.BaseDirectory, GetName(book.Key));

                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }
                foreach (var item in book.Value)
                {
                    var redirect = await client.GetAsync(item, HttpCompletionOption.ResponseHeadersRead);

                    var response = await _client.GetAsync(redirect.Headers.Location, HttpCompletionOption.ResponseHeadersRead);
                    Uri uri = response.RequestMessage.RequestUri;
                    // if (!uri.IsFile)
                    // {
                    //     _logger.LogError("{0} link {1} is not file", book, item);
                    // }

                    string fileName = System.IO.Path.GetFileName(uri.LocalPath);
                    var filePath = Path.Combine(dirPath, GetName(fileName));

                    // var stream = await response.Content.ReadAsStreamAsync();

                    // using (var fileStream = File.Open(filePath, FileMode.Create))
                    // {
                    //     await stream.CopyToAsync(fileStream);

                    //     fileStream.Close();
                    // }

                    var contentLength = response.Content.Headers.ContentLength;

                    if (fileName.Equals("Microsoft_Press_ebook_Planning_SharePoint_Hybrid_MOBI.mobi"))
                    {

                    }
                    if (File.Exists(filePath))
                    {
                        using (var file = File.Open(filePath, FileMode.Open))
                        {
                            if (file.Length == contentLength)
                            {
                                Console.WriteLine("{0} 已下载", fileName);
                                continue;
                            }
                        }
                    }

                    _logger.LogInformation("{0} link {1} download complete", book.Key, item);
                    using (var progress = new ProgressBar())
                    {

                        using (Stream contentStream = await response.Content.ReadAsStreamAsync(),
                        fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var totalRead = 0L;
                            var totalReads = 0L;
                            var buffer = new byte[8192];
                            var isMoreToRead = true;

                            while (isMoreToRead)
                            {
                                if (contentLength.HasValue)
                                {
                                    progress.Report((double)totalRead / contentLength.Value);
                                }
                                var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                                if (read == 0)
                                {
                                    isMoreToRead = false;
                                }
                                else
                                {
                                    await fileStream.WriteAsync(buffer, 0, read);

                                    totalRead += read;
                                    totalReads += 1;

                                    if (totalReads % 2000 == 0)
                                    {
                                        //Console.WriteLine(string.Format("total bytes downloaded so far: {0:n0}", totalRead));
                                    }
                                }
                            }
                        }


                    }
                }

            }
        }

        private string GetName(string illegal)
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            illegal = r.Replace(illegal, "");
            return illegal;
        }
    }
}