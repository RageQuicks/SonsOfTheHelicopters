using Sons.Multiplayer;
using SonsSdk;
using SonsSdk.Networking;

namespace RustHelicopter;

/// <summary>
/// "/helicopter" typed into the multiplayer chat. RedLoader routes chat commands to the host, so this works when
/// you host a co-op game.
/// </summary>
internal static class ChatCommand
{
    public static void Register() =>
        ServerCommands.RegisterCommand("helicopter", PlayerRoles.Owner, OnChat);

    private static ServerCommands.EExecutionResult OnChat(ServerCommands.CommandArgs args)
    {
        // The helicopter only exists on this PC, so only spawn for the player sitting at it.
        if (args.SteamId != SonsTools.GetSteamId())
            return ServerCommands.EExecutionResult.NoPermission;

        return RustHelicopterMod.RunCommand(args.ArgsAsString())
            ? ServerCommands.EExecutionResult.Success
            : ServerCommands.EExecutionResult.CommandFailed;
    }
}
