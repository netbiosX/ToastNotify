/*
 * ToastNotify — C# Windows Toast Notification Tool
 *
 * Sends Windows toast notifications via WinRT APIs with AUMID spoofing.
 * Accepts toast notification templates as XML files.
 *
 * Usage:
 *   ToastNotify.exe getaumid
 *   ToastNotify.exe sendtoast <aumid> <title> <body>
 *   ToastNotify.exe custom <aumid> <xml-file-path>
 *
 * Build:
 *   dotnet build -c Release
 *
 * Requirements:
 *   .NET 6.0+ with Windows 10 TFM (net6.0-windows10.0.17763.0)
 */

using System;
using System.IO;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace ToastNotify
{
    internal class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            string command = args[0].ToLowerInvariant();

            switch (command)
            {
                case "getaumid":
                    return GetAumid();

                case "sendtoast":
                    if (args.Length < 4)
                    {
                        Console.Error.WriteLine("Error: sendtoast requires <aumid> <title> <body>");
                        return 1;
                    }
                    return SendToast(args[1], args[2], args[3]);

                case "custom":
                    if (args.Length < 3)
                    {
                        Console.Error.WriteLine("Error: custom requires <aumid> <xml-file-path>");
                        return 1;
                    }
                    return SendCustom(args[1], args[2]);

                default:
                    Console.Error.WriteLine($"Unknown command: {command}");
                    PrintUsage();
                    return 1;
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine(
                "ToastNotify — Windows Toast Notification Tool\n" +
                "\n" +
                "Usage:\n" +
                "  ToastNotify.exe getaumid                          - Enumerate registered AUMIDs\n" +
                "  ToastNotify.exe sendtoast <aumid> <title> <body>  - Send a simple toast\n" +
                "  ToastNotify.exe custom <aumid> <xml-file>         - Send toast from XML file\n" +
                "\n" +
                "Examples:\n" +
                "  ToastNotify.exe getaumid\n" +
                "  ToastNotify.exe sendtoast \"MSEdge\" \"Update Available\" \"Click to install.\"\n" +
                "  ToastNotify.exe custom \"MSEdge\" toast_template.xml\n"
            );
        }

        /// <summary>
        /// Enumerate AUMIDs from the Notifications\Settings registry keys.
        /// </summary>
        static int GetAumid()
        {
            const string subKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings";

            Console.WriteLine("[Notifications\\Settings - HKCU]");
            EnumSubkeys(Registry.CurrentUser, subKeyPath);

            Console.WriteLine("[Notifications\\Settings - HKLM]");
            EnumSubkeys(Registry.LocalMachine, subKeyPath);

            return 0;
        }

        static void EnumSubkeys(RegistryKey rootKey, string subKeyPath)
        {
            try
            {
                using RegistryKey? key = rootKey.OpenSubKey(subKeyPath, false);
                if (key == null)
                {
                    Console.WriteLine("  (key not found)");
                    return;
                }

                string[] subKeyNames = key.GetSubKeyNames();
                if (subKeyNames.Length == 0)
                {
                    Console.WriteLine("  (none found)");
                    return;
                }

                foreach (string name in subKeyNames)
                {
                    Console.WriteLine($"  {name}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Error reading registry: {ex.Message}");
            }
        }

        /// <summary>
        /// Send a simple toast notification with title and body text.
        /// </summary>
        static int SendToast(string aumid, string title, string body)
        {
            string xml =
                "<toast>" +
                "<visual>" +
                "<binding template='ToastGeneric'>" +
                $"<text>{EscapeXml(title)}</text>" +
                $"<text>{EscapeXml(body)}</text>" +
                "</binding>" +
                "</visual>" +
                "</toast>";

            return SendToastXml(aumid, xml);
        }

        /// <summary>
        /// Send a custom toast notification from an XML file.
        /// </summary>
        static int SendCustom(string aumid, string xmlFilePath)
        {
            if (!File.Exists(xmlFilePath))
            {
                Console.Error.WriteLine($"Error: XML file not found: {xmlFilePath}");
                return 1;
            }

            string xmlContent;
            try
            {
                xmlContent = File.ReadAllText(xmlFilePath, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading XML file: {ex.Message}");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                Console.Error.WriteLine("Error: XML file is empty");
                return 1;
            }

            return SendToastXml(aumid, xmlContent);
        }

        /// <summary>
        /// Core method: parse XML and display a toast notification using the given AUMID.
        /// </summary>
        static int SendToastXml(string aumid, string xml)
        {
            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xml);

                var notifier = ToastNotificationManager.CreateToastNotifier(aumid);
                var toast = new ToastNotification(xmlDoc);
                notifier.Show(toast);

                Console.WriteLine("Toast sent successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error sending toast: {ex.Message}");
                if (ex.HResult != 0)
                    Console.Error.WriteLine($"HRESULT: 0x{ex.HResult:X8}");
                return 1;
            }
        }

        /// <summary>
        /// Escape special XML characters in user-provided text for the simple toast template.
        /// </summary>
        static string EscapeXml(string text)
        {
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
