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
using System.Threading.Tasks;

namespace Perspio.Talk.Sample.DotNet
{
    class Program
    {
        //Set the scope for API call 
        static string[] _scopes = new string[] { "api://a916b11b-5b63-4a0d-9a7d-d70d20c0b9ac/access_as_user" };

        // You have to replace:
        // - ClientId: The Application Id for your Perspio Talk Client App registration - Provided by Inauro
        // - TenantId: Azure Tenant hosting the Perspio platform - Provided by Inauro

        private static string ClientId = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxx";
        private static string Tenant = "xxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxx";
        private static string Instance = "https://login.microsoftonline.com/";
        static string perspioTalkAPIEndpoint = "https://dev-talk.perspio.io/aad/v1/currentuser"; // this API will return your brief profile

        private static IPublicClientApplication _clientApp;

        static async Task Main()
        {
            PrepareClient(); // Instantiate

            AuthenticationResult authResult = await AuthenticateClient(); // login using Device Flow only once - subsequent run of the program will automatically acquire the token from the cache or refresh it

            var response = await InvokeAPI(authResult);
            Console.WriteLine($"Api Response: {response}");
            Console.ReadLine();
            
            //2nd API call without authenticating using deviceflow  ( reusing the token from the token cache)
            authResult = await AuthenticateClient();
            response = await InvokeAPI(authResult);
            Console.WriteLine($"Api Response: {response}");
            Console.ReadLine();

        }

    private static void PrepareClient()
    {
        _clientApp = PublicClientApplicationBuilder.Create(ClientId)
                 .WithAuthority($"{Instance}{Tenant}")
                 .WithDefaultRedirectUri()
                 .Build();
        TokenCacheHelper.EnableSerialization(_clientApp.UserTokenCache);
    }

    private static async Task<AuthenticationResult> AuthenticateClient()
    {
        AuthenticationResult authResult = null;

        IEnumerable<IAccount> accounts = await _clientApp.GetAccountsAsync();
        IAccount firstAccount = accounts.FirstOrDefault();

        try
        {
            authResult = await _clientApp.AcquireTokenSilent(_scopes, firstAccount)
                .ExecuteAsync();
        }
        catch (MsalUiRequiredException ex)
        {
            // A MsalUiRequiredException happened on AcquireTokenSilent. 
            // This indicates you need to call AcquireTokenWithDeviceCode to acquire a token
            System.Diagnostics.Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");

            try
            {
                authResult = await _clientApp.AcquireTokenWithDeviceCode(_scopes, deviceCodeResult =>
                {
                    Console.WriteLine(deviceCodeResult.Message);
                    return Task.FromResult(0);
                }).ExecuteAsync();

            }
            catch (MsalException msalex)
            {
                Console.WriteLine($"Error Acquiring Token Silently:{System.Environment.NewLine}{msalex}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error Acquiring Token Silently:{System.Environment.NewLine}{ex}");
        }
        return authResult;
    }

    private static async Task<string> InvokeAPI(AuthenticationResult authResult)
    {
        if (authResult != null)
        {
            //Debug
            //Console.WriteLine($"Access Token: {authResult.AccessToken}");
            //Console.WriteLine($"View at: https://jwt.ms/?#access_token={authResult.AccessToken}");

            return await GetHttpContentWithToken(perspioTalkAPIEndpoint, authResult.AccessToken);
        }
        return null;

    }
    public static async Task<string> GetHttpContentWithToken(string url, string token)
    {
        var httpClient = new System.Net.Http.HttpClient();
        System.Net.Http.HttpResponseMessage response;
        try
        {
            var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
            //Add the token in Authorization header
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            return ex.ToString();
        }
    }
}

}
