using Microsoft.Data.Sqlite;
using OpenQA.Selenium;
using System.Text.Json;

namespace WebCrawler
{
    public class ResultSaverService
    {
        private readonly string OutputDir;
        private readonly string JsonPath;

        public ResultSaverService()
        {
            var rootDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\.."));
            OutputDir = Path.Combine(rootDir, "output");

            if (!Directory.Exists(OutputDir))
            {
                Directory.CreateDirectory(OutputDir);
            }

            JsonPath = Path.Combine(OutputDir, "results.json");
        }

        public async Task SaveScreenshotAsync(int page, IWebDriver driver)
        {
            var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
            var path = Path.Combine(OutputDir, $"page_{page}.png");
            var bytes = screenshot.AsByteArray;
            await File.WriteAllBytesAsync(path, bytes);
            await Task.CompletedTask;
        }

        public async Task SaveJsonAsync(List<ProxyDTO> entries)
        {
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(JsonPath, json);
        }

        public async Task SaveToDatabaseAsync(DateTime start, DateTime end, int pages, int lines)
        {
            var dbPath = Path.Combine(OutputDir, "crawler_results.db");

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();

            var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText =
            @"
                CREATE TABLE IF NOT EXISTS CrawlerStats (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    StartTime TEXT NOT NULL,
                    EndTime TEXT NOT NULL,
                    TotalPages INTEGER NOT NULL,
                    TotalLines INTEGER NOT NULL,
                    JsonPath TEXT NOT NULL,
                    JsonContent TEXT NOT NULL
            );
            ";
            await createTableCommand.ExecuteNonQueryAsync();

            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText =
            @"
                INSERT INTO CrawlerStats (StartTime, EndTime, TotalPages, TotalLines, JsonPath, JsonContent)
                VALUES ($start, $end, $pages, $lines, $jsonpath, $jsoncontent);
            ";
            insertCommand.Parameters.AddWithValue("$start", start.ToString("yyyy-MM-dd HH:mm:ss"));
            insertCommand.Parameters.AddWithValue("$end", end.ToString("yyyy-MM-dd HH:mm:ss"));
            insertCommand.Parameters.AddWithValue("$pages", pages);
            insertCommand.Parameters.AddWithValue("$lines", lines);
            insertCommand.Parameters.AddWithValue("$jsonpath", JsonPath);
            insertCommand.Parameters.AddWithValue("$jsoncontent", await File.ReadAllTextAsync(JsonPath));

            await insertCommand.ExecuteNonQueryAsync();

            Console.WriteLine("Dados salvos no banco de dados SQLite com sucesso.");

            await Task.CompletedTask;
        }
    }
}
