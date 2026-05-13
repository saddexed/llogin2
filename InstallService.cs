using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;

namespace llogin
{
    public static class InstallService
    {
        public static bool IsAdmin()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            return false;
        }

        public static void RunAsAdmin(string args)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule?.FileName,
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas"
            };
            try { Process.Start(processInfo); }
            catch (Exception ex) { Console.WriteLine($"Elevation failed: {ex.Message}"); }
        }

        public static async Task<int> InstallAsync(bool skipTask)
        {
            string installDir = StorageService.GetInstallDir();
            string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "llogin.exe" : "llogin";
            string targetPath = Path.Combine(installDir, exeName);
            string currentPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";

            Console.WriteLine($"Installing llogin to {installDir}...");

            try
            {
                if (!Directory.Exists(installDir)) Directory.CreateDirectory(installDir);
                if (string.Compare(currentPath, targetPath, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    File.Copy(currentPath, targetPath, true);
                }
                Console.WriteLine("Files installed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error installing files: {ex.Message}");
                return 1;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                UpdatePathWindows(installDir);
                if (!skipTask)
                {
                    if (IsAdmin()) SetupWindowsTask(targetPath);
                    else
                    {
                        Console.WriteLine("Elevation required for task scheduling. Requesting admin...");
                        RunAsAdmin("--setup-task");
                    }
                }
            }
            else
            {
                Console.WriteLine($"Add {installDir} to your PATH if not already present.");
            }

            Console.WriteLine("\nInstallation complete! Restart your terminal to use 'llogin'.");
            return 0;
        }

        private static void UpdatePathWindows(string installDir)
        {
            try
            {
                string? currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
                if (currentPath == null || !currentPath.Contains(installDir))
                {
                    string newPath = string.IsNullOrEmpty(currentPath) ? installDir : $"{currentPath};{installDir}";
                    Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);
                    Console.WriteLine("Added to User PATH.");
                }
                else { Console.WriteLine("Already in PATH."); }
            }
            catch (Exception ex) { Console.WriteLine($"Warning: Could not update PATH: {ex.Message}"); }
        }

        public static void SetupWindowsTask(string exePath)
        {
            string taskName = "llogin";
            string userName = $"{Environment.UserDomainName}\\{Environment.UserName}";
            Console.WriteLine($"Creating scheduled task '{taskName}' for user '{userName}'...");

            string xmlPath = Path.Combine(Path.GetTempPath(), $"llogin-task-{Guid.NewGuid()}.xml");
            string taskXml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.4"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <Triggers>
    <EventTrigger>
      <Enabled>true</Enabled>
      <Subscription>&lt;QueryList&gt;&lt;Query Id=""0"" Path=""Microsoft-Windows-NCSI/Operational""&gt;&lt;Select Path=""Microsoft-Windows-NCSI/Operational""&gt;*[System[Provider[@Name='Microsoft-Windows-NCSI'] and EventID=4038]]&lt;/Select&gt;&lt;/Query&gt;&lt;/QueryList&gt;</Subscription>
    </EventTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{userName}</UserId>
      <LogonType>S4U</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>StopExisting</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>true</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>true</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <DisallowStartOnRemoteAppSession>false</DisallowStartOnRemoteAppSession>
    <UseUnifiedSchedulingEngine>true</UseUnifiedSchedulingEngine>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT72H</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec><Command>""{exePath}""</Command></Exec>
  </Actions>
</Task>";

            try
            {
                File.WriteAllText(xmlPath, taskXml, System.Text.Encoding.Unicode);
                var processInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/create /tn \"{taskName}\" /xml \"{xmlPath}\" /f",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var process = Process.Start(processInfo);
                process?.WaitForExit();
                if (process?.ExitCode == 0) Console.WriteLine("Scheduled task created successfully.");
                else Console.WriteLine($"Error creating task: {process?.StandardError.ReadToEnd()}");
                if (File.Exists(xmlPath)) File.Delete(xmlPath);
            }
            catch (Exception ex) { Console.WriteLine($"Error setting up scheduled task: {ex.Message}"); }
        }
    }
}