using HtmlAgilityPack;
using KinostarMovieParser.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;


namespace KinostarMovieParser
{
    /// <summary>
    /// Осущеставляет парсинг фильмов с сайта https://kinostar86.ru
    /// </summary>
    public class KinostarParser
    {
        private const string KINOSTAR_URL = "https://kinostar86.ru";

        /// <summary>
        /// Получить список фильмов на сегодня
        /// </summary>
        public async Task<List<Movie>> GetMovies()
        {
            return await Task.Run(() =>
            {
                List<Movie> movies = new List<Movie>();

                try
                {
                    HtmlWeb loader = new HtmlWeb();

                    HtmlDocument mainPageDoc = loader.Load(KINOSTAR_URL);

                    string[] classList = { "event rental large", "event rental small", "event premiere small", "sc-dOpmdR FIKoh event premiere large" };

                    // Вытаскиваем все фильмы за сегодня

                    HtmlNodeCollection movieNodes = new HtmlNodeCollection(mainPageDoc.DocumentNode);

                    foreach (string className in classList)
                    {
                        HtmlNodeCollection nodes = mainPageDoc.DocumentNode.SelectNodes($".//div[contains(@class, '{className}')]");
                        if (nodes != null)
                        {
                            foreach (HtmlNode node in nodes)
                            {
                                movieNodes.Add(node);
                            }
                        }
                    }

                    if (movieNodes == null || movieNodes.Count == 0) return new List<Movie>();

                    foreach (var movie in movieNodes)
                    {
                        var currentMovie = new Movie();

                        currentMovie.Id = Guid.NewGuid();

                        var movieLinkNode = movie.SelectSingleNode(".//a[@class='event-name']");
                        if (movieLinkNode == null) continue;

                        Console.WriteLine(movieLinkNode.InnerText);

                        currentMovie.Name = movieLinkNode.InnerText;

                        var movieLink = movieLinkNode.GetAttributeValue("href", string.Empty);
                        if (string.IsNullOrEmpty(movieLink)) continue;

                        HtmlDocument currentMovieDoc = loader.Load($"{KINOSTAR_URL}{movieLink}");

                        var movieInfoNodes = currentMovieDoc.DocumentNode.SelectNodes(".//*[@id=\"__next\"]/div[1]/div/div[6]");

                        foreach (var movieInfoNode in movieInfoNodes)
                        {
                            try {
                                var currentNodeTitle = movieInfoNode.SelectNodes("//*[@id=\"__next\"]/div[1]/div/div[6]/div/div[2]/div[2]/div[*]/div[1]");
                                var currentNodeInfo = movieInfoNode.SelectNodes("//*[@id=\"__next\"]/div[1]/div/div[6]/div/div[2]/div[2]/div[*]/div[2]");
                                int number = 0;
                                if (currentNodeTitle != null)
                                {
                                    foreach (var node in currentNodeTitle)
                                    {
                                        switch (node.InnerText)
                                        {
                                            case "В прокате с":
                                                currentMovie.StartDate = currentNodeInfo[number].InnerText;
                                                break;
                                            case "В прокате до":
                                                currentMovie.EndDate = currentNodeInfo[number].InnerText;
                                                break;
                                            case "Хронометраж":
                                                currentMovie.Timing = currentNodeInfo[number].InnerText;
                                                break;
                                            case "Режиссер":
                                                currentMovie.Director = currentNodeInfo[number].InnerText;
                                                break;
                                            case "В ролях":
                                                currentMovie.Actors = currentNodeInfo[number].InnerText;
                                                break;
                                        }
                                        number++;
                                    }
                                }
                            }
                            catch
                            {

                            }
                        }

                        // вытягиваем активные
                        var showNodes = currentMovieDoc.DocumentNode.SelectNodes("//div[contains(@class, 'show')]");

                        ParseShows(currentMovie, showNodes);

                        // вытягиваем неактивные сеансы
                        var disabledShowsNodes = currentMovieDoc.DocumentNode.SelectNodes("//div[contains(@class, 'show disabled')]");

                        ParseShows(currentMovie, disabledShowsNodes);                      

                        var movieDescription = currentMovieDoc.DocumentNode.SelectSingleNode("//*[@id=\"__next\"]/div[1]/div/div[6]/div/div[2]/div[3]")?.InnerText;
                        currentMovie.Description = movieDescription;
                        try
                        {
                            var moviewPosterNode = currentMovieDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'event-poster')]");
                            var moviewPosterUrl = moviewPosterNode?.SelectSingleNode(".//img")?.GetAttributeValue("src", string.Empty)?.Replace("22x32", "540x800").Replace(":blur(2)", string.Empty);
                            
                            currentMovie.PosterURL = moviewPosterUrl;
                        }
                        catch
                        {
                            
                        }
                        movies.Add(currentMovie);
                    }

                    return movies;
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Произошла ошибка при считывании информации о фильмах: {e.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return movies;
                }
            });
        }

        /// <summary>
        /// Парсинг сеансов
        /// </summary>
        private void ParseShows(Movie currentMovie, HtmlNodeCollection showNodes)
        {
            if (showNodes != null)
            {
                currentMovie.Sessions = currentMovie.Sessions ?? new List<Session>();

                string i = "";
                string j = "";

                foreach (var showNode in showNodes)
                {

                    var showTime = showNode.SelectSingleNode(".//div[contains(@class, 'show-time')]")?.InnerText;
                    var showPrice = showNode.SelectSingleNode(".//div[contains(@class, 'price')]")?.InnerText;
                    if (showTime != i || showPrice != j)
                    {
                        i = showTime;
                        j = showPrice;

                        if (string.IsNullOrEmpty(showTime) || string.IsNullOrEmpty(showPrice)) continue;

                        var time = TimeSpan.Parse(showTime);

                        // убираем все символы из строки с ценой, кроме цифр
                        var price = int.Parse(Regex.Replace(showPrice, @"[^\d]", string.Empty));

                        currentMovie.Sessions.Add(new Session()
                        {
                            StartTime = time,
                            Price = price
                        });
                    }
                }
            }
        }
    }
}
