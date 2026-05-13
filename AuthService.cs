using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace llogin
{
    public static class AuthService
    {
        private static readonly string LoginUrl = "https://10.10.0.1/24online/servlet/E24onlineHTTPClient";
        private static readonly string LogoutUrl = "https://10.10.0.1/24online/servlet/E24onlineHTTPClient";
        private static readonly string ClientPageUrl = "https://10.10.0.1/24online/webpages/client.jsp";
        private static readonly string SuccessMessage = "To start surfing";
        private static readonly string[] SuccessIndicators = { "successfully logged off", "You have successfully logged off" };
        private static readonly string LoginPageIndicator = "To start surfing";

        private static readonly CookieContainer CookieJar = new CookieContainer();

        public static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                CookieContainer = CookieJar,
                UseCookies = true,
                AllowAutoRedirect = true
            };
            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            return client;
        }

        public static async Task<string?> GetLoggedInUserAsync(HttpClient client)
        {
            try
            {
                var response = await client.GetAsync(ClientPageUrl);
                string content = await response.Content.ReadAsStringAsync();
                var match = Regex.Match(content, @"name=[""']loggedinuser[""']\s+value=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return Regex.Replace(match.Groups[1].Value, @"@lpu\.com$", "", RegexOptions.IgnoreCase);
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"\nError fetching user: {ex.Message}");
                Console.ResetColor();
                return null;
            }
        }

        public static async Task<bool> InvokeLoginAsync(HttpClient client, string username, string password)
        {
            try
            {
                Console.Write($"[....] Attempting login with {username}...");
                string rawData = $"mode=191&username={Uri.EscapeDataString(username)}%40lpu.com&password={Uri.EscapeDataString(password)}";
                using var request = new HttpRequestMessage(HttpMethod.Post, LoginUrl)
                {
                    Content = new StringContent(rawData, Encoding.UTF8, "application/x-www-form-urlencoded")
                };

                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (responseContent.Contains("Wrong username/password", StringComparison.OrdinalIgnoreCase) || 
                    responseContent.Contains("Invalid Username/Password", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[FAIL] Login failed for {username}: Invalid username or password.");
                    Console.ResetColor();
                    StorageService.WriteLogEntry(username, "Login", "failed");
                    return false;
                }

                if (responseContent.Contains(SuccessMessage))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"\r[OK] Login successful for {username}.                          \n");
                    Console.ResetColor();
                    StorageService.WriteLogEntry(username, "Login", "success", LoginUrl);
                    return true;
                }

                // If not successful, provide feedback even in production
                Console.WriteLine(); // New line to move past the [....] message
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] Login failed for {username}.");
                Console.ResetColor();
                StorageService.WriteLogEntry(username, "Login", "failed");
                return false;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nError during login: {ex.Message}");
                Console.ResetColor();
                StorageService.WriteLogEntry(username, "Login", "failed");
                return false;
            }
        }

        public static async Task<bool> InvokeLogoutAsync(HttpClient client)
        {
            string? user = await GetLoggedInUserAsync(client);
            if (string.IsNullOrEmpty(user))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nUnable to fetch logged-in user. You might not be logged in.");
                Console.ResetColor();
                return false;
            }

            try
            {
                Console.Write($"[...] Logging out {user}...");
                string rawData = $"mode=193&username={Uri.EscapeDataString(user)}%40lpu.com&logout=Logout";
                using var request = new HttpRequestMessage(HttpMethod.Post, LogoutUrl)
                {
                    Content = new StringContent(rawData, Encoding.UTF8, "application/x-www-form-urlencoded")
                };

                var response = await client.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();

                bool success = content.Contains(LoginPageIndicator);
                if (!success)
                {
                    foreach (var indicator in SuccessIndicators)
                    {
                        if (content.Contains(indicator)) { success = true; break; }
                    }
                }

                if (success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"\r[OK] Logout for {user} successful.                           \n");
                    Console.ResetColor();
                    StorageService.WriteLogEntry(user, "Logout", "success", LogoutUrl);
                    return true;
                }
                
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] Logout failed for {user}.");
                Console.ResetColor();
                return false;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"\r[ERROR] Error during logout: {ex.Message}\n");
                Console.ResetColor();
                return false;
            }
        }

        public static async Task<bool> TestNetworkConnectionAsync()
        {
            try
            {
                using var client = CreateHttpClient();
                client.Timeout = TimeSpan.FromSeconds(3);
                await client.GetAsync("https://10.10.0.1/");
                return true;
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error detecting network - Unable to reach 10.10.0.1");
                Console.ResetColor();
                return false;
            }
        }
    }
}