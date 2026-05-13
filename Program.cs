using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Reflection;

namespace llogin
{
    class Program
    {
        static readonly string CurrentVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        static async Task<int> Main(string[] args)
        {
            bool help = false, logout = false, clearCreds = false, version = false, status = false;
            bool setupTask = false, removeTask = false, listUsers = false;
            string? addUser = null, addPass = null;
            string? removeUser = null;
            string? setDefaultUser = null;
            string? loginUser = null, loginPass = null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();
                if (arg == "-h" || arg == "--help") help = true;
                else if (arg == "-v" || arg == "--version") version = true;
                else if (arg == "-l" || arg == "--logout") logout = true;
                else if (arg == "-s" || arg == "--status") status = true;
                else if (arg == "-c" || arg == "--clear") clearCreds = true;
                else if (arg == "--task") setupTask = true;
                else if (arg == "--no-task") removeTask = true;
                else if (arg == "-ls" || arg == "--list") listUsers = true;
                else if (arg == "-a" || arg == "--add")
                {
                    if (i + 2 < args.Length) { addUser = args[++i]; addPass = args[++i]; }
                    else { Console.WriteLine("Error: -a requires username and password."); return 1; }
                }
                else if (arg == "-r" || arg == "--remove")
                {
                    if (i + 1 < args.Length) { removeUser = args[++i]; }
                    else { Console.WriteLine("Error: -r requires username."); return 1; }
                }
                else if (arg == "-d" || arg == "--default")
                {
                    if (i + 1 < args.Length) { setDefaultUser = args[++i]; }
                    else { Console.WriteLine("Error: -d requires username."); return 1; }
                }
                else if (arg.StartsWith("-"))
                {
                    Console.WriteLine($"Unknown option: {arg}");
                    return 1;
                }
                else if (loginUser == null) loginUser = args[i];
                else if (loginPass == null) loginPass = args[i];
            }

            if (version) { Console.WriteLine($"llogin v{CurrentVersion}"); return 0; }
            if (help) { ShowHelp(); return 0; }
            
            if (clearCreds)
            {
                if (ConfirmAction("Are you sure you want to remove ALL stored credentials?"))
                    return StorageService.RemoveAllCredentials() ? 0 : 1;
                return 0;
            }

            if (listUsers) { StorageService.ListUsers(); return 0; }
            
            if (addUser != null && addPass != null) { StorageService.AddUser(addUser, addPass); return 0; }
            
            if (removeUser != null)
            {
                if (ConfirmAction($"Are you sure you want to remove user '{removeUser}'?"))
                    StorageService.RemoveUser(removeUser);
                return 0;
            }

            if (setDefaultUser != null) { StorageService.SetDefaultUser(setDefaultUser); return 0; }

            if (setupTask) return HandleTaskSetup(true);
            if (removeTask) return HandleTaskSetup(false);

            if (!await AuthService.TestNetworkConnectionAsync()) return 1;
            using HttpClient client = AuthService.CreateHttpClient();

            if (status) return await HandleStatus(client);
            if (logout) return await AuthService.InvokeLogoutAsync(client) ? 0 : 1;

            return await HandleLogin(client, loginUser, loginPass);
        }

        static bool ConfirmAction(string message)
        {
            Console.Write($"{message} (Y/n): ");
            string? response = Console.ReadLine()?.Trim().ToLowerInvariant();
            return response != "n" || response != "no";
        }

        static async Task<int> HandleLogin(HttpClient client, string? username, string? password)
        {
            string? effectiveUsername = username;
            string? effectivePassword = password;

            if (!string.IsNullOrEmpty(effectiveUsername) && string.IsNullOrEmpty(effectivePassword))
            {
                var stored = StorageService.GetCredentials(effectiveUsername);
                if (stored != null)
                {
                    effectivePassword = stored.Password;
                }
            }

            if (string.IsNullOrEmpty(effectiveUsername))
            {
                var stored = StorageService.GetDefaultCredentials();
                if (stored != null)
                {
                    effectiveUsername = stored.Username;
                    effectivePassword = stored.Password;
                }
            }

            if (string.IsNullOrEmpty(effectiveUsername) || string.IsNullOrEmpty(effectivePassword))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                if (!string.IsNullOrEmpty(effectiveUsername))
                {
                    Console.WriteLine($"Error: Please provide password or set account in credentials.json");
                }
                else
                {
                    Console.WriteLine("Error: Specify username and password or store them in credentials.json");
                }
                Console.ResetColor();
                return 1;
            }

            return await AuthService.InvokeLoginAsync(client, effectiveUsername, effectivePassword) ? 0 : 1;
        }

        static async Task<int> HandleStatus(HttpClient client)
        {
            string? user = await AuthService.GetLoggedInUserAsync(client);
            if (!string.IsNullOrEmpty(user))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Currently logged in as: {user}");
                Console.ResetColor();
                return 0;
            }
            else
            {
                Console.WriteLine("Not currently logged in.");
                return 1;
            }
        }

        static int HandleTaskSetup(bool create)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine("Task management is currently only supported on Windows.");
                return 1;
            }

            if (create)
            {
                if (InstallService.IsAdmin())
                {
                    string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    InstallService.SetupWindowsTask(exePath);
                    return 0;
                }
                else
                {
                    InstallService.RunAsAdmin("--task");
                    return 0;
                }
            }
            else
            {
                Console.WriteLine("Task removal logic is being updated...");
                return 0;
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("LLogin - LPU Wifi Autologin");
            Console.WriteLine("================================");
            Console.WriteLine("Usage:");
            Console.WriteLine("  llogin [user] [pass]            Login with specified or stored credentials");
            Console.WriteLine("  llogin -l, --logout             Logout of the current session");
            Console.WriteLine("  llogin -s, --status             Show current connection status and user");
            Console.WriteLine("  llogin -ls, --list              List all stored users");
            Console.WriteLine("  llogin -a <user> <pass>         Add or update a user");
            Console.WriteLine("  llogin -r <user>                Remove a stored user");
            Console.WriteLine("  llogin -d <user>                Set a stored user as default");
            Console.WriteLine("  llogin -c, --clear              Remove all stored credentials");
            Console.WriteLine("  llogin --task                   Setup auto-login task (Windows only)");
            Console.WriteLine("  llogin --no-task                Remove auto-login task (Windows only)");
            Console.WriteLine("\nOptions:");
            Console.WriteLine("  -h, --help                      Show this help message");
            Console.WriteLine("  -v, --version                   Show version information");
            Console.WriteLine("\nExamples:");
            Console.WriteLine("  llogin -a 11801234 pass123      Save an account");
            Console.WriteLine("  llogin -d 11801234              Switch default account");
            Console.WriteLine("  llogin --status                 Check connection state");
        }
    }
}