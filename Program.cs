using Microsoft.Playwright;

namespace cookies
{
    internal class Program
    {
        static SemaphoreSlim semaphore = new(1);
        public static async Task Main()
        {
            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });
            var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });

            //bool interestingPartStarted = false;

            {
                var page = await context.NewPageAsync();

                await page.RouteAsync("**/*.js*", async route =>
                {
                    //Console.WriteLine("Intercepted: " + route.Request.Url);
                    //interestingPartStarted = true;
                    // You can abort, fulfill, or continue the request
                    // For example, just continue the request
                    //await route.AbortAsync();
                    await route.ContinueAsync();
                });

                page.Request += async (_, request) =>
                {
                    /*
                    if (request.Url.Contains(".js"))
                    {
                        await semaphore.WaitAsync();
                        await Task.Delay(50);//breathing time
                        var localPage = _ as IPage;
                        var ctx = localPage.Context;
                        var y = await ctx.CookiesAsync();
                        semaphore.Release();
                    }
                    */
                };

                page.Response += async (_, response) =>
                {
                    /*
                    if (response.Url.Contains(".js"))
                    {
                        await semaphore.WaitAsync();
                        await Task.Delay(50);//breathing time
                        var localPage = _ as IPage;
                        var ctx = localPage?.Context;
                        var y = await ctx.CookiesAsync();
                       semaphore.Release();
                    }
                    */

                };

                page.Response += OnResponseV2;

                page.RequestFinished += async (_, request) =>
                {

                    if (request.Url.Contains(".js"))
                    {
                        await semaphore.WaitAsync();
                        await Task.Delay(500);//breathing time
                        var localPage = _ as IPage;
                        var ctx = localPage.Context;
                        var y = await ctx.CookiesAsync();
                        ReadCookies("Request_Finished: " + request.Url + ":\t", y);

                        semaphore.Release();
                    }

                };

                await page.GotoAsync("https://localhost:7280/sekhar");
                //await Task.Delay(2000);
                //await page.GotoAsync("https://localhost:7280/sekhar/privacy");
                //await Task.Delay(2000);

                //await page.GotoAsync("https://localhost:7280/home/index");
                //await Task.Delay(2000);

                //await page.GotoAsync("https://localhost:7280/home/privacy");
                //await Task.Delay(2000);
                //page.Response -= OnResponse1;
            }


            Console.WriteLine("Press p to print, anything else to exit");

            var input = Console.ReadLine();
            await browser.CloseAsync();

            Console.WriteLine("**header cookies**");
            foreach (var cookieKVP in CookieDictionary)
            {
                Console.WriteLine(cookieKVP.Key + ", ");
                Console.Write("\t\t");
                foreach (var l in cookieKVP.Value)
                {
                    Console.WriteLine("\t\t" + l);
                }

                Console.WriteLine("");
            }
            Console.WriteLine("**");

            Console.WriteLine("**js cookies**");
            foreach (var cookieKVP in JSCookieDictionary)
            {
                Console.WriteLine(cookieKVP.Key + ", ");
                Console.Write("\t\t");
                foreach (var l in cookieKVP.Value)
                {
                    Console.WriteLine("\t\t" + l);
                }

                Console.WriteLine("");
            }
        }

        static void PrintList(string v, IEnumerable<BrowserContextCookiesResult> newAdditions)
        {
            foreach (var cookie in newAdditions)
            {
                Console.WriteLine($"{v}:\t{cookie.Name} = {cookie.Value}; Domain: {cookie.Domain}");
            }
        }

        static void ReadCookies(string v, IReadOnlyList<BrowserContextCookiesResult> cookies)
        {
            foreach (var cookie in cookies)
            {
                Console.WriteLine($"{v}:\t{cookie.Name} = {cookie.Value}; Domain: {cookie.Domain}");
            }
        }
        private static async void OnResponse(object sender, IResponse response)
        {
            await semaphore.WaitAsync();
            var page = sender as IPage;
            var context = page.Context;

            var condition = true;

            if (condition)
            {
                Console.WriteLine($"{response.Url}");
                var initialList = await context.CookiesAsync();
                ReadCookies("Init: ", initialList);

                #region headers
                var allHeaders = await response.AllHeadersAsync();

                if (allHeaders.TryGetValue("set-cookie", out var rawSetCookie))
                {
                    Console.WriteLine($"Set-Cookie from: {response.Url}");

                    // This can include multiple cookies split by newline or comma depending on the server
                    var setCookies = rawSetCookie.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var cookie in setCookies)
                    {
                        Console.WriteLine($"  => {cookie.Trim()}");
                    }
                }
                #endregion

                await Task.Delay(3000);

                var laterList = await context.CookiesAsync();
                //ReadCookies("Later:", laterList);


                var newAdditions = laterList
                        .Where(updated => !initialList.Any(initial =>
                            initial.Name == updated.Name &&
                            initial.Domain == updated.Domain &&
                            initial.Path == updated.Path))
                        .ToList();

                PrintList("js cookies: ", newAdditions);

                Console.WriteLine(" ");

            }

            semaphore.Release();
        }

        private static Dictionary<string, List<string>> CookieDictionary = new Dictionary<string, List<string>>();
        private static Dictionary<string, List<string>> JSCookieDictionary = new Dictionary<string, List<string>>();
        private static async void OnResponseV2(object sender, IResponse response)
        {
            await semaphore.WaitAsync();
            var page = sender as IPage;
            var context = page.Context;

            var condition = true;

            if (condition)
            {
                Console.WriteLine($"{response.Url}");
                var initialList = await context.CookiesAsync();
                ReadCookies("Init: ", initialList);

                #region headers
                var allHeaders = await response.AllHeadersAsync();

                if (allHeaders.TryGetValue("set-cookie", out var rawSetCookie))
                {
                    //Console.WriteLine($"Set-Cookie from: {response.Url}");

                    // This can include multiple cookies split by newline or comma depending on the server
                    var setCookies = rawSetCookie.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var cookie in setCookies)
                    {
                        var cookiePart = cookie.Split(";");
                        var cookieName = cookiePart[0].Split('=')[0];
                        //Console.WriteLine($"  => {cookie.Trim()}");
                        if (CookieDictionary.ContainsKey(cookieName))
                        {
                            CookieDictionary[cookieName].Add(response.Url);
                        }
                        else
                        {
                            var yy = new List<string>();
                            yy.Add(response.Url);
                            CookieDictionary.Add(cookieName, yy);
                        }
                    }
                }
                #endregion

                await Task.Delay(10);

                var laterList = await context.CookiesAsync();
                //ReadCookies("Later:", laterList);

                foreach (var ll in laterList)
                {
                    var laterListCookieName = ll.Name;
                    if (CookieDictionary.ContainsKey(laterListCookieName))
                    {
                        //http cookie, so ignore
                    }
                    else
                    {
                        //mostly a javascript cookie!
                        if (!JSCookieDictionary.ContainsKey(laterListCookieName))
                        {
                            var xx = new List<string> { response.Url };
                            JSCookieDictionary.Add(laterListCookieName, xx);
                        }
                        else
                        {
                            JSCookieDictionary[laterListCookieName].Add(response.Url);
                        }

                    }
                }

                var newAdditions = laterList
                        .Where(updated => !initialList.Any(initial =>
                            initial.Name == updated.Name &&
                            initial.Domain == updated.Domain &&
                            initial.Path == updated.Path))
                        .ToList();

                PrintList("js cookies: ", newAdditions);

                Console.WriteLine(" ");

            }

            semaphore.Release();
        }
    }
}


