//------------------------------------------------------------------------------
//
// Copyright (c) Inauro.
// All rights reserved.
//
// This code is licensed under the MIT License.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
//------------------------------------------------------------------------------

using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace Perspio.Talk.Sample.DotNet
{
    class Program
    {

        // You have to replace:
        // - clientId: The Application Id for your Perspio Talk Client App registration - Provided by Inauro
        // - tenant: Azure Tenant hosting the Perspio platform - Provided by Inauro
        // - subscriptionKey : A unique kep mapped to your Perspio Tenant
        // - clientSecret: [Optional] If on-behalf-of-user level access is not required then clientId/secret is needed

        private static string _subscriptionKey = "----";
        private static string _clientId = "---";
        private static string _clientSecret = "---";
        private static string _tenant = "----";
        private static string _instance = "https://login.microsoftonline.com/";
        private static string _perspioTalkAPIEndpoint = "https://dev-talk.perspio.io";


        private static string _apiToInvoke = "";
        private static string _apiToInvokeOnUserBehalf = "/directory/v3/user"; // this API will return your brief profile
        private static string _apiToInvokeAsDaemon = "/locations/v3/labels"; // this API will return your brief profile
                                                                             //private static string _apiToInvokeAsDaemon = "/sites/v3/"; // this API will return your brief profile



        //Set the scope for API call 
        static string[] _publicAppscopes = new string[] { "api://-----/access_as_user" }; // for clients accessinf data on-behalf-of-a user 
        static string[] _confidentialAppscopes = new string[] { "api://-----/.default" }; // for clients running as a daemon agent


        private static IPublicClientApplication _clientApp;
        private static IConfidentialClientApplication _confClientApp;

        static async Task Main()
        {
            PrepareClient(); // Instantiate

            AuthenticationResult authResult = await AuthenticateClient(); // login using Device Flow only once - subsequent run of the program will automatically acquire the token from the cache or refresh it

            var response = await InvokeAPI(authResult);
            Console.WriteLine();
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Api Response: {response.StatusCode}{Environment.NewLine} {content}");
            }
            else
            {
                Console.WriteLine($"Api Response: {response.StatusCode}{Environment.NewLine} ");
            }
            Console.ReadLine();

        }

        private static void PrepareClient()
        {
            _clientApp = PublicClientApplicationBuilder.Create(_clientId)
                     .WithAuthority($"{_instance}{_tenant}")
                     .WithDefaultRedirectUri()
                     .Build();
            TokenCacheHelper.EnableSerialization(_clientApp.UserTokenCache);

            _confClientApp = ConfidentialClientApplicationBuilder
                    .Create(_clientId)
                    .WithTenantId(_tenant)
                    .WithClientSecret(_clientSecret)
                    .Build();
            TokenCacheHelper.EnableSerialization(_confClientApp.UserTokenCache);

        }

        private static async Task<AuthenticationResult> AuthenticateClient()
        {
            AuthenticationResult authResult = null;

            IEnumerable<IAccount> accounts = await _clientApp.GetAccountsAsync();
            IAccount firstAccount = accounts.FirstOrDefault();

            try
            {
                authResult = await _clientApp.AcquireTokenSilent(_publicAppscopes, firstAccount)
                    .ExecuteAsync();
                if (authResult != null)
                {
                    Console.WriteLine($"Continue using the tokens from the cache?{Environment.NewLine} " +
                        $"Y: Login silently using the last access/refresh tokens from cache(if not expired) {Environment.NewLine} " +
                        $"N: Login and acquire new access and refresh tokens");
                    var response = Console.ReadKey();
                    if (response.Key.ToString().ToLower() == "n")
                    {
                        Console.WriteLine();
                        throw new MsalUiRequiredException("0", "Force relogin");
                    }

                }
            }
            catch (MsalUiRequiredException ex)
            {
                // A MsalUiRequiredException happened on AcquireTokenSilent. 
                // This indicates you need to call AcquireTokenWithDeviceCode to acquire a token
                System.Diagnostics.Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");

                try
                {
                    Console.WriteLine();
                    Console.WriteLine($"Choose the token acquisition option{Environment.NewLine} " +
                        $"1: On-Behalf-Of-User Flow: Web based interactive login {Environment.NewLine} " +
                        $"2: On-Behalf-Of-User Flow: Device login {Environment.NewLine} " +
                        $"3: As Daemon Agent Flow: ClientId/ClientSecret login {Environment.NewLine} ");
                    Console.Write(" ");
                    var response = Console.ReadKey();

                    if (response.Key.ToString() == "D1")
                    {
                        authResult = await _clientApp.AcquireTokenInteractive(_publicAppscopes)
                          .WithAccount(firstAccount)
                          .WithPrompt(Prompt.SelectAccount)
                          .ExecuteAsync();
                        _apiToInvoke = _apiToInvokeOnUserBehalf;
                    }
                    else if (response.Key.ToString() == "D2")
                    {
                        authResult = await _clientApp.AcquireTokenWithDeviceCode(_publicAppscopes, deviceCodeResult =>
                        {
                            Console.WriteLine(deviceCodeResult.Message);
                            return Task.FromResult(0);
                        }).ExecuteAsync();
                        _apiToInvoke = _apiToInvokeOnUserBehalf;
                    }
                    else if (response.Key.ToString() == "D3")
                    {

                        authResult = await _confClientApp.AcquireTokenForClient(_confidentialAppscopes).ExecuteAsync();
                        _apiToInvoke = _apiToInvokeAsDaemon;
                    }
                }
                catch (MsalException msalex)
                {
                    Console.WriteLine($"Error Acquiring Token Interactively:{System.Environment.NewLine}{msalex}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error Acquiring Token Silently:{System.Environment.NewLine}{ex}");
            }

            return authResult;
        }

        private static async Task<HttpResponseMessage> InvokeAPI(AuthenticationResult authResult)
        {
            if (authResult != null)
            {
                //Debug
                //Console.WriteLine($"Access Token: {authResult.AccessToken}");
                //Console.WriteLine($"View at: https://jwt.ms/?#access_token={authResult.AccessToken}");

                return await GetHttpContentWithToken($"{_perspioTalkAPIEndpoint}{_apiToInvoke}", authResult.AccessToken);
            }
            return null;

        }
        public static async Task<HttpResponseMessage> GetHttpContentWithToken(string url, string token)
        {
            var httpClient = new System.Net.Http.HttpClient();
            System.Net.Http.HttpResponseMessage response;
            var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
            response = await httpClient.SendAsync(request);
            return response;
        }
    }

}
