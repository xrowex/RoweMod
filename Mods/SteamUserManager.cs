using Il2CppSteamworks;
using Newtonsoft.Json.Linq;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods;

public class SteamUserManager
{
    private static readonly string DefaultAPIKey = "BA65224FF3CE85B812F2724BA11EDC1E";

    private static readonly string DefaultLOGUrl =
        "https://script.google.com/macros/s/AKfycbww8DrJOGrt9x3_ZkSt0dpGOS0qZUiu2SjqVvrFlloY0aSkanReinBHprUZWl40bK5T/exec";
    private static readonly string DefaultBanCheckUrl = "https://script.google.com/macros/s/AKfycbw8M99FPuthJlKee964BZDcO3ssV81XhzWyEYeIWg0KlrEFLk8yCkYM9coBC34SnHXX/exec";

    public static ulong GetSteamID()
    {
        if (!SteamAPI.Init())  // Initialize SteamAPI if not already done
        {
            Log.Msg("Failed to initialize Steamworks!");
            return 0;
        }
    
        return SteamUser.GetSteamID().m_SteamID;
    }
    
    public static async Task<string> GetSteamUsername(ulong steamId)
    {
        using (HttpClient client = new HttpClient())
        {
            string url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={DefaultAPIKey}&steamids={steamId}";
            try
            {
                string response = await client.GetStringAsync(url);
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

    public static async Task<bool> IsUserBanned(ulong steamId)
    {
        using (HttpClient client = new HttpClient())
        {
            string url = $"{DefaultBanCheckUrl}?steamid={steamId}";

            try
            {
                HttpResponseMessage response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    Log.Msg($"[IsUserBanned] HTTP Error {response.StatusCode}: {response.ReasonPhrase}");
                    return false; // If API fails, assume not banned
                }

                string responseText = await response.Content.ReadAsStringAsync();
                Log.Msg($"[IsUserBanned] Server Response: {responseText}");

                return responseText.Trim().ToLower() == "blocked";
            }
            catch (Exception e)
            {
                Log.Msg($"[IsUserBanned] Failed to send request: {e.Message}");
                return false;
            }
        }
    }


    public static async Task LogUser(ulong steamId, string username)
    {
        using (HttpClient client = new HttpClient())
        {
            string url = $"{DefaultLOGUrl}?steamid={steamId}&username={Uri.EscapeDataString(username)}"; // Ensure proper encoding
        
            try
            {
                string response = await client.GetStringAsync(url);
                Log.Msg($"[LogUser] Server Response: {response}"); // ✅ Debug log
            }
            catch (Exception e)
            {
                Log.Msg($"[LogUser] Failed to send request: {e.Message}"); // ✅ Log errors
            }
        }
    }


    public static async Task LogAndCheckUser()
    {
        ulong steamId = GetSteamID();
        if (steamId == 0)
        {
            Log.Msg("Failed to retrieve Steam ID.");
            return;
        }

        string username = await GetSteamUsername(steamId);
        bool banned = await IsUserBanned(steamId);

        Log.Msg($"User: {username} (Steam ID: {steamId}) is using the mod.");

        await LogUser(steamId, username);

        if (banned)
        {
            Log.Error($"ACCESS DENIED: {username} is banned!");
            Log.Error($"ACCESS DENIED: {username} is banned!");
            Log.Error($"ACCESS DENIED: {username} is banned!");
            Log.Error($"ACCESS DENIED: {username} is banned!");
            Log.Error($"ACCESS DENIED: {username} is banned!");
            Log.Error($"ACCESS DENIED: {username} is banned!");
            Log.Error($"ACCESS DENIED: {username} is banned!");
            Log.Msg($"ACCESS DENIED: {username} is banned! Closing game...");
            Application.Quit(); // ❌ Force quit game
            return;
        }

        Log.Msg("Access granted.");
    }
}