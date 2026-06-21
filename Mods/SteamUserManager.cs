using System;
using Il2CppSteamworks;
using Newtonsoft.Json.Linq;
using rowemod.Utils;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace rowemod.Mods;

public class SteamUserManager
{
    
    //Try again dipshit
    private const int RequestTimeoutSeconds = 5;
    private static readonly string DefaultAPIKey = "BA65224FF3CE85B812F2724BA11EDC1E";

    private static readonly string DefaultLOGUrl =
        "https://script.google.com/macros/s/AKfycbww8DrJOGrt9x3_ZkSt0dpGOS0qZUiu2SjqVvrFlloY0aSkanReinBHprUZWl40bK5T/exec";

    private static readonly string DefaultBanCheckUrl =
        "https://script.google.com/macros/s/AKfycbw8M99FPuthJlKee964BZDcO3ssV81XhzWyEYeIWg0KlrEFLk8yCkYM9coBC34SnHXX/exec";

    private static readonly string[] RequiredLoggingHosts =
    {
        "script.google.com",
        "script.googleusercontent.com"
    };

    public static bool LoggingEndpointReady { get; private set; }
    public static bool LastAccessDeniedByBan { get; private set; }
    public static string LastLoggingEndpointFailure { get; private set; } = "Not checked.";
    public static string LastAccessFailureReason { get; private set; } = string.Empty;

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
        };
    }

    public static ulong GetSteamID()
    {
        if (!SteamAPI.Init())
        {
            Log.Msg("Failed to initialize Steamworks!");
            return 0;
        }

        return SteamUser.GetSteamID().m_SteamID;
    }

    public static async Task<string> GetSteamUsername(ulong steamId)
    {
        using (HttpClient client = CreateHttpClient())
        {
            string url =
                $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={DefaultAPIKey}&steamids={steamId}";
            try
            {
                string response = await client.GetStringAsync(url).ConfigureAwait(false);
                JObject json = JObject.Parse(response);
                var player = json["response"]?["players"]?.First;
                return player?["personaname"]?.ToString() ?? "Unknown User";
            }
            catch
            {
                return "Unknown User";
            }
        }
    }

    public static async Task<bool?> IsUserBanned(ulong steamId)
    {
        using (HttpClient client = CreateHttpClient())
        {
            string url = $"{DefaultBanCheckUrl}?steamid={steamId}";

            try
            {
                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    LastAccessFailureReason = $"Ban check returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.";
                    Log.Msg($"[IsUserBanned] HTTP Error {response.StatusCode}: {response.ReasonPhrase}");
                    return null;
                }

                string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Log.Msg($"[IsUserBanned] Server Response: {responseText}");

                return responseText.Trim().ToLowerInvariant() == "blocked";
            }
            catch (Exception e)
            {
                LastAccessFailureReason = $"Ban check failed: {e.Message}";
                Log.Msg($"[IsUserBanned] Failed to send request: {e.Message}");
                return null;
            }
        }
    }

    public static async Task<bool> LogUser(ulong steamId, string username)
    {
        using (HttpClient client = CreateHttpClient())
        {
            string url = $"{DefaultLOGUrl}?steamid={steamId}&username={Uri.EscapeDataString(username)}";

            try
            {
                string response = await client.GetStringAsync(url).ConfigureAwait(false);
                Log.Msg($"[LogUser] Server Response: {response}");
                return true;
            }
            catch (Exception e)
            {
                Log.Msg($"[LogUser] Failed to send request: {e.Message}");
                return false;
            }
        }
    }

    public static async Task<bool> EnsureLoggingEndpointReady()
    {
        LoggingEndpointReady = false;
        LastLoggingEndpointFailure = "Checking logging endpoint...";

        foreach (string host in RequiredLoggingHosts)
        {
            if (!await ValidateHostResolution(host).ConfigureAwait(false))
                return false;
        }

        if (!await ProbeEndpoint(DefaultLOGUrl, "log endpoint").ConfigureAwait(false))
            return false;

        if (!await ProbeEndpoint(DefaultBanCheckUrl, "ban check endpoint").ConfigureAwait(false))
            return false;

        LoggingEndpointReady = true;
        LastLoggingEndpointFailure = "OK";
        Log.Msg("[LoggingEndpoint] Logging and ban check endpoints are reachable.");
        return true;
    }

    private static async Task<bool> ValidateHostResolution(string host)
    {
        try
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
            if (addresses.Length == 0)
            {
                LastLoggingEndpointFailure = $"{host} did not resolve to any IP addresses.";
                Log.Error($"[LoggingEndpoint] {LastLoggingEndpointFailure}");
                return false;
            }

            IPAddress[] blockedAddresses = addresses.Where(IsLocalOrBlockedAddress).ToArray();
            if (blockedAddresses.Length > 0)
            {
                LastLoggingEndpointFailure =
                    $"{host} resolves to blocked/local address(es): {string.Join(", ", blockedAddresses.Select(a => a.ToString()))}.";
                Log.Error($"[LoggingEndpoint] {LastLoggingEndpointFailure}");
                return false;
            }

            return true;
        }
        catch (SocketException e)
        {
            LastLoggingEndpointFailure = $"{host} DNS lookup failed: {e.Message}";
            Log.Error($"[LoggingEndpoint] {LastLoggingEndpointFailure}");
            return false;
        }
        catch (Exception e)
        {
            LastLoggingEndpointFailure = $"{host} resolution check failed: {e.Message}";
            Log.Error($"[LoggingEndpoint] {LastLoggingEndpointFailure}");
            return false;
        }
    }

    private static bool IsLocalOrBlockedAddress(IPAddress address)
    {
        return IPAddress.IsLoopback(address)
               || address.Equals(IPAddress.Any)
               || address.Equals(IPAddress.IPv6Any)
               || address.Equals(IPAddress.None)
               || address.Equals(IPAddress.IPv6None);
    }

    private static async Task<bool> ProbeEndpoint(string url, string endpointName)
    {
        string separator = url.Contains("?") ? "&" : "?";
        string probeUrl = $"{url}{separator}health=1&ts={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        using (HttpClient client = CreateHttpClient())
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(probeUrl).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return true;

                LastLoggingEndpointFailure =
                    $"{endpointName} returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.";
                Log.Error($"[LoggingEndpoint] {LastLoggingEndpointFailure}");
                return false;
            }
            catch (Exception e)
            {
                LastLoggingEndpointFailure = $"{endpointName} probe failed: {e.Message}";
                Log.Error($"[LoggingEndpoint] {LastLoggingEndpointFailure}");
                return false;
            }
        }
    }

    public static async Task<bool> LogAndCheckUser()
    {
        LastAccessDeniedByBan = false;
        LastAccessFailureReason = string.Empty;
        ulong steamId = GetSteamID();
        if (steamId == 0)
        {
            LastAccessFailureReason = "Failed to retrieve Steam ID.";
            Log.Msg("Failed to retrieve Steam ID.");
            return false;
        }

        if (!await EnsureLoggingEndpointReady().ConfigureAwait(false))
        {
            LastAccessFailureReason = LastLoggingEndpointFailure;
            Log.Error($"Required logging endpoint is unavailable: {LastLoggingEndpointFailure}");
            Log.Error("Stopping mod initialization because Steam ID logging cannot be verified.");
            return false;
        }

        string username = await GetSteamUsername(steamId).ConfigureAwait(false);
        bool? banned = await IsUserBanned(steamId).ConfigureAwait(false);
        if (!banned.HasValue)
        {
            if (string.IsNullOrWhiteSpace(LastAccessFailureReason))
                LastAccessFailureReason = "Could not verify ban status.";

            Log.Error($"Stopping mod initialization because ban status could not be verified: {LastAccessFailureReason}");
            return false;
        }

        Log.Msg($"User: {username} (Steam ID: {steamId}) is using the mod.");

        if (banned.Value)
        {
            LastAccessDeniedByBan = true;
            LastAccessFailureReason = "User is banned.";
            for (int i = 0; i < 7; i++)
                Log.Error($"ACCESS DENIED: {username} is banned!");

            Log.Msg($"ACCESS DENIED: {username} is banned! Closing game...");
            return false;
        }

        bool logged = await LogUser(steamId, username).ConfigureAwait(false);
        if (!logged)
        {
            LastAccessFailureReason = "Steam ID logging failed.";
            Log.Error("Stopping mod initialization because Steam ID logging failed.");
            return false;
        }

        Log.Msg("Access granted.");
        return true;
    }
}
