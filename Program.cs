using System.Numerics;
using Microsoft.Playwright;

namespace cookies
{
    internal class Program
    {
        static SemaphoreSlim semaphore = new(1);
        public static async Task Main()
        {
            Console.WriteLine("Press p to print, anything else to exit");
            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });
            var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });

            //bool interestingPartStarted = false;
            #region events

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

                    if (request.Url.Contains(".js"))
                    {
                        await semaphore.WaitAsync();
                        Console.WriteLine($"{i++}\tOnRequest Initiated for: {request.Url}");
                        semaphore.Release();
                    }
                };

                /*
                page.Response += async (_, response) =>
                {
                    
                    if (response.Url.Contains(".js"))
                    {
                        await semaphore.WaitAsync();
                        await Task.Delay(50);//breathing time
                        var localPage = _ as IPage;
                        var ctx = localPage?.Context;
                        var y = await ctx.CookiesAsync();
                       semaphore.Release();
                    }
                    

                };
                */
                //page.Response += OnResponseV2;

                /*
                page.RequestFinished += async (_, request) =>
                {

                    if (request.Url.Contains(".js"))
                    {
                        await semaphore.WaitAsync();
                        await Task.Delay(500);//breathing time
                        var localPage = _ as IPage;
                        var ctx = localPage.Context;
                        var y = await ctx.CookiesAsync();
                        //ReadCookies("Request_Finished: " + request.Url + ":\t", y);

                        semaphore.Release();
                    }

                };
                */

                page.Response += OnResponse;
                page.RequestFinished += OnRequestFinished;


                //var testUrls = new List<string> { "https://localhost:7280/sekhar" , "https://localhost:7280/sekhar/privacy" };
                
                var testUrls = new List<string> { "https://localhost:7280/sekhar" };

                foreach (var url in testUrls)
                {
                    await page.GotoAsync(url);
                    await Task.Delay(3000);
                }

                await Task.Delay(5000);//final
            }
            #endregion

            await semaphore.WaitAsync();
            await browser.CloseAsync();
            semaphore.Release();

            Console.WriteLine("**header cookies**");
            foreach (var cookieKVP in Header_CookieDictionary)
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
            foreach (var cookieKVP in JS_CookieDictionary)
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
        private static async void OnResponse_xx(object sender, IResponse response)
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
        private static int i = 0;
        private static Dictionary<string, List<string>> Header_CookieDictionary = new Dictionary<string, List<string>>();
        private static Dictionary<string, List<string>> JS_CookieDictionary = new Dictionary<string, List<string>>();
        private static async void OnResponse(object sender, IResponse response)
        {
            await semaphore.WaitAsync();
            var page = sender as IPage;
            var context = page.Context;

            var excludedPaths = new List<string> { ".css" };


            var condition = !excludedPaths.Any(path => response.Url.Contains(path));


            if (condition)
            {
                Console.WriteLine($"{i++}\tOnResponse completed for: {response.Url}");
                var initialList = await context.CookiesAsync();
                //ReadCookies("Init: ", initialList);

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
                        if (Header_CookieDictionary.ContainsKey(cookieName))
                        {
                            Header_CookieDictionary[cookieName].Add(response.Url);
                        }
                        else
                        {
                            var yy = new List<string>();
                            yy.Add(response.Url);
                            Header_CookieDictionary.Add(cookieName, yy);
                        }
                        Console.WriteLine("OnResponse Header-Cookies: " + cookieName);
                    }
                }
                else
                {
                    Console.WriteLine("No Header Cookies");
                }
                #endregion

                await Task.Delay(10);

                var laterList = await context.CookiesAsync();
                //ReadCookies("Later:", laterList);

                foreach (var ll in laterList)
                {
                    var laterListCookieName = ll.Name;
                    if (Header_CookieDictionary.ContainsKey(laterListCookieName))
                    {
                        //http cookie, so ignore
                    }
                    else
                    {
                        //mostly a javascript cookie!
                        if (!JS_CookieDictionary.ContainsKey(laterListCookieName))
                        {
                            var xx = new List<string> { response.Url };
                            JS_CookieDictionary.Add(laterListCookieName, xx);
                        }
                        else
                        {
                            JS_CookieDictionary[laterListCookieName].Add(response.Url);
                        }

                    }
                }

                var newAdditions = laterList
                        .Where(updated => !initialList.Any(initial =>
                            initial.Name == updated.Name &&
                            initial.Domain == updated.Domain &&
                            initial.Path == updated.Path))
                        .ToList();

                PrintList("Probable JS Cookies: ", newAdditions);

                Console.WriteLine(" ");

            }

            semaphore.Release();
        }
        private static async void OnRequestFinished(object sender, IRequest requestFinished)
        {
            await semaphore.WaitAsync();
            var page = sender as IPage;
            var context = page.Context;

            var excludedPaths = new List<string> { ".css" };


            var condition = !excludedPaths.Any(path => requestFinished.Url.Contains(path));


            if (condition)
            {
                Console.WriteLine($"{i++}\tOnRequestFinished for: {requestFinished.Url}");
                var initialList = await context.CookiesAsync();
                //ReadCookies("Init: ", initialList);

                #region headers
                var allRequestHeaders = await requestFinished.AllHeadersAsync();
                RequestOrResponseCookieCollection(requestFinished, allRequestHeaders);

                var _response = await requestFinished.ResponseAsync();
                var allResponseHeaders = await _response.AllHeadersAsync();
                RequestOrResponseCookieCollection(requestFinished, allResponseHeaders);
                #endregion


                bool textAbleResponse = _response.Headers["content-type"].Contains("text/") || _response.Headers["content-type"].Contains("application/j");
                if (textAbleResponse)
                {

                    //dump responses to Error
                    var responseText = await _response.TextAsync();
                    System.Console.Error.WriteLine($"*******************************{requestFinished.Url} start **********************************");
                    System.Console.Error.WriteLine(responseText);
                    System.Console.Error.WriteLine($"*******************************{requestFinished.Url} end **********************************");
                }
                await Task.Delay(1000);

                var laterList = await context.CookiesAsync();
                //ReadCookies("Later:", laterList);

                foreach (var ll in laterList)
                {
                    var laterListCookieName = ll.Name;
                    if (Header_CookieDictionary.ContainsKey(laterListCookieName))
                    {
                        //http cookie, so ignore
                    }
                    else
                    {
                        //mostly a javascript cookie!
                        if (!JS_CookieDictionary.ContainsKey(laterListCookieName))
                        {
                            var xx = new List<string> { requestFinished.Url };
                            JS_CookieDictionary.Add(laterListCookieName, xx);
                        }
                        else
                        {
                            JS_CookieDictionary[laterListCookieName].Add(requestFinished.Url);
                        }

                    }
                }

                var newAdditions = laterList
                        .Where(updated => !initialList.Any(initial =>
                            initial.Name == updated.Name &&
                            initial.Domain == updated.Domain &&
                            initial.Path == updated.Path))
                        .ToList();

                PrintList("Probable JS Cookies: ", newAdditions);

                Console.WriteLine(" ");

            }

            semaphore.Release();

            static void RequestOrResponseCookieCollection(IRequest requestFinished, Dictionary<string, string> allRequestHeaders)
            {
                if (allRequestHeaders.TryGetValue("set-cookie", out var rawSetCookie))
                {
                    //Console.WriteLine($"Set-Cookie from: {response.Url}");

                    // This can include multiple cookies split by newline or comma depending on the server
                    var setCookies = rawSetCookie.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var cookie in setCookies)
                    {
                        var cookiePart = cookie.Split(";");
                        var cookieName = cookiePart[0].Split('=')[0];
                        //Console.WriteLine($"  => {cookie.Trim()}");
                        if (Header_CookieDictionary.ContainsKey(cookieName))
                        {
                            Header_CookieDictionary[cookieName].Add(requestFinished.Url);
                        }
                        else
                        {
                            var yy = new List<string>();
                            yy.Add(requestFinished.Url);
                            Header_CookieDictionary.Add(cookieName, yy);
                        }
                        Console.WriteLine("Header-Cookies: " + cookieName);
                    }
                }
                else
                {
                    //Console.WriteLine(DateTime.Now.ToLongTimeString() + "\tNo Header Cookies");
                }
            }
        }
    }
}


