using System;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using HarmonyLib;

namespace TokenAuthFixer;

/// <summary>
///     This is a hacky way to resolve the Token Authentication Error.
///     A proper fix would involve refactoring the EOSSDKComponent to handle token expiration and renewal.
///     I am not doing that here just because this is a BepInEx mod so it would be annoying with Harmony.
///     Epics Docs on the topic:
///     https://dev.epicgames.com/docs/epic-games-store/testing-guide#connect-interface-login-and-access-token-renewal
/// </summary>
[HarmonyPatch(typeof(EOSSDKComponent))]
public static class EOSSDKComponentPatch {
    /// <summary>
    ///     Stops the code from trying to log in multiple times simultaneously, probably unnecessary :P
    /// </summary>
    public static bool LoginInProgress;
    
    /// <summary>
    ///     This prefix patch checks if the user token whenever the main menu loads.
    ///     If it's expired it tries to log in again.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(nameof(EOSSDKComponent.Awake))]
    public static void AwakePrefix() {
        if (EOSSDKComponent.s_PlatformInterface == null) { return; } // EOS not initialized yet
        
        Result userToken = EOSSDKComponent.GetUserToken(out _); // Check the user token status
        
        Plugin.Logger.LogInfo($"User token check returned {Enum.GetName(typeof(Result), userToken)}");
        if (userToken != Result.InvalidAuth) { return; }
        
        // The login stuff is done the same way the EOSSDKComponent does it.
        LoginOptions loginOptions = new LoginOptions {
            Credentials = new Credentials {
                Token = EOSSDKComponent.ByteArrayToString(PlayFabManager.SteamTicket.Item2),
                Type = ExternalCredentialType.SteamSessionTicket
            }
        };
        
        if (LoginInProgress) {
            Plugin.Logger.LogInfo("Login already in progress, skipping re-login attempt.");
            return;
        }
        
        LoginInProgress = true;
        EOSSDKComponent.s_PlatformInterface.GetConnectInterface().Login(
            ref loginOptions, null, delegate(ref LoginCallbackInfo loginCallbackInfo) {
                Plugin.Logger.LogInfo($"Re-login attempt returned {Enum.GetName(typeof(Result), loginCallbackInfo.ResultCode)}");
                LoginInProgress = false;
            }
        );
    }
}