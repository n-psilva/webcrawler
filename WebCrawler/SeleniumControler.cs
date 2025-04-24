using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using System.Collections.Concurrent;

namespace WebCrawler
{
    public class SeleniumControler
    {
        private const string baseUrl = "https://proxyservers.pro/proxy/list/order/updated/order_dir/asc/page/";
        private readonly EdgeOptions _options = new EdgeOptions();
        private readonly ResultSaverService _resultSaverService = new ResultSaverService();
        public SeleniumControler()
        {
            _options.AddArgument("headless");
            _options.AddArgument("--disable-gpu");
        }


        public async Task StartCrawlerAsync()
        {
            int totalPages = 1;
            
            using (var driver = new EdgeDriver(_options))
            {
                driver.Navigate().GoToUrl(baseUrl.TrimEnd("/page/".ToCharArray()));
                totalPages = ExtractTotalPages(driver);
            }
            
            int maxConcurrency = 2;
            var tasks = new List<Task>();
            var results = new ConcurrentBag<ProxyDTO>();

            for (int i = 1; i <= totalPages; i++)
            {
                int currentPage = i;

                while (tasks.Count >= maxConcurrency)
                {
                    await Task.WhenAny(tasks);
                    tasks.RemoveAll(t => t.IsCompleted);
                }

                tasks.Add(Task.Run(async () =>
                {
                    using var driver = new EdgeDriver(_options);
                    string url = currentPage == 1 ? baseUrl.TrimEnd("/page/".ToCharArray()) : baseUrl + currentPage;
                    driver.Navigate().GoToUrl(url);

                    await _resultSaverService.SaveScreenshotAsync(currentPage, driver);
                    var pageResults = ExtrairDados(driver);
                    foreach (var entry in pageResults)
                        results.Add(entry);
                }));
            }

            await Task.WhenAll(tasks);
            await _resultSaverService.SaveJsonAsync(results.ToList());
            await _resultSaverService.SaveToDatabaseAsync(DateTime.Now, DateTime.Now, totalPages, results.Count);
        } 

        private int ExtractTotalPages(IWebDriver driver)
        {
            try
            {
                var pagination = driver.FindElements(By.CssSelector("ul.pagination li a"));
                return int.TryParse(pagination.LastOrDefault()?.Text, out var totalPages) ? totalPages : 1;
            }
            catch
            {
                return 1;
            }
        }
        private List<ProxyDTO> ExtrairDados(IWebDriver driver)
        {
            var rows = driver.FindElements(By.CssSelector("table.table.table-hover tbody tr"));
            var listProxy = new List<ProxyDTO>();

            foreach (var row in rows)
            {
                var cells = row.FindElements(By.TagName("td"));
                if (cells.Count >= 4)
                {
                    listProxy.Add(new ProxyDTO
                    {
                        IpAddress = cells[1].Text.Trim(),
                        Port = cells[2].Text.Trim(),
                        Country = cells[3].Text.Trim(),
                        Protocol = cells[6].Text.Trim()
                    });
                }
            }

            return listProxy;
        }
    }

}
