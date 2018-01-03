using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Media.Imaging;
using MoreLinq;
using Newtonsoft.Json.Linq;

namespace SEApiComposeImages
{
    class User
    {
        public int Id;
        public BitmapFrame Image;
        public Uri ImageUri;
        public string DisplayName;
    }

    class Program
    {
        static void Main(string[] args) => new Program().Run();

        Random random = new Random();
        const string FileName = "TopUserIds.txt";

        void Run()
        {
            var topids = GetIds(FileName);
            var topUsers = GetUsers(topids);
            int cols = 16, rows = 8;
            var shuffledImages = Shuffle(topUsers.Select(u => u.Image).Take(cols * rows));
            var result = ImageTools.CombineImages(shuffledImages, cols, rows, 128, 128, 5);
            ImageTools.SaveImageAsPng(result, $@"combined-{cols}x{rows}.png");
        }

        IEnumerable<int> GetIds(string filename)
        {
            return File.ReadLines(filename).Distinct().Select(int.Parse);
        }

        IEnumerable<User> GetUsers(IEnumerable<int> ids)
        {
            const int batchSize = 50;
            int i = -1;
            foreach (var idBatch in ids.Batch(batchSize))
            {
                var userBatch = FetchUsers(idBatch);
                foreach (var user in userBatch)
                {
                    i++;
                    var uri = user.ImageUri;
                    Console.WriteLine($"Downloading image #{i}, user={user.DisplayName}, id={user.Id}");
                    var image = DownloadImageByUri(uri);
                    if (ImageTools.IsAutomaticImage(image))
                    {
                        Console.WriteLine($"Image is auto, skipping");
                        continue;
                    }
                    user.Image = image;
                    yield return user;
                }
            }
        }

        WebClient PrepareWebClient() => new GzipHttpWebClient() { Encoding = Encoding.UTF8 };

        IEnumerable<User> FetchUsers(IEnumerable<int> ids)
        {
            var idsList = ids.ToList();
            var queryUri = BuildQuery(idsList);
            string json;
            using (var cl = PrepareWebClient())
                json = cl.DownloadString(queryUri);
            JObject total = JObject.Parse(json);
            JArray items = (JArray)total["items"];
            var users = items.Select(JsonToUser).ToDictionary(u => u.Id);
            return idsList.Select(id => users[id]);
        }

        User JsonToUser(JToken juser)
        {
            var displayName = (string)juser["display_name"];
            var userId = (int)juser["user_id"];
            var imageLink = (string)juser["profile_image"];
            return new User() { Id = userId, DisplayName = displayName, ImageUri = new Uri(imageLink) };
        }

        Uri BuildQuery(List<int> ids)
        {
            if (ids.Count > 100)
                throw new ArgumentException("Batch size too big");
            var idsCombined = string.Join(";", ids);
            var idsCombinedEncoded = WebUtility.HtmlEncode(idsCombined);
            var pattern = $"https://api.stackexchange.com/2.2/users/{idsCombinedEncoded}?page=1&pagesize={ids.Count}&site=ru.stackoverflow";
            return new Uri(pattern);
        }

        BitmapFrame DownloadImageByUri(Uri uri)
        {
            var ms = new MemoryStream();
            using (var client = PrepareWebClient())
            using (var ns = client.OpenRead(uri))
                ns.CopyTo(ms);
            ms.Position = 0;
            var frame = BitmapFrame.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            frame.Freeze();
            frame.Metadata.Freeze();
            return frame;
        }

        List<T> Shuffle<T>(IEnumerable<T> seq)
        {
            var result = new List<T>();
            foreach (var s in seq)
            {
                int j = random.Next(result.Count + 1);
                if (j == result.Count)
                {
                    result.Add(s);
                }
                else
                {
                    result.Add(result[j]);
                    result[j] = s;
                }
            }
            return result;
        }
    }
}
