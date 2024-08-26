﻿namespace SpeedDate
{
    public enum OpCodes : uint
    {
        // Standard error code
        Error = 0,

        MsfStart = 32000,

        // Security
        AesKeyRequest,
        RequestPermissionLevel,

        // Rooms
        RegisterRoom,
        DestroyRoom,
        SaveRoomOptions,
        GetRoomAccess,
        ProvideRoomAccessCheck,
        ValidateRoomAccess,
        PlayerLeftRoom,

        // Spawner
        RegisterSpawner,
        SpawnRequest,
        ClientsSpawnRequest,
        SpawnRequestStatusChange,
        RegisterSpawnedProcess,
        CompleteSpawnProcess,
        KillSpawnedProcess,
        ProcessStarted,
        ProcessKilled,
        AbortSpawnRequest,
        GetSpawnFinalizationData,
        UpdateSpawnerProcessesCount,

        // Matchmaker
        FindGames,

        // Auth
        LogIn,
        LogOut,
        RegisterAccount,
        PasswordResetCodeRequest,
        RequestEmailConfirmCode,
        ConfirmEmail,
        GetLoggedInCount,
        PasswordChange,
        GetPeerAccountInfo,

        // Chat
        PickUsername,
        JoinChannel,
        LeaveChannel,
        GetCurrentChannels,
        ChatMessage,
        GetUsersInChannel,
        UserJoinedChannel,
        UserLeftChannel,
        SetDefaultChannel,

        // Lobbies
        JoinLobby,
        LeaveLobby,
        CreateLobby,
        LobbyInfo,
        SetLobbyProperties,
        SetMyLobbyProperties,
        LobbySetReady,
        LobbyStartGame,
        LobbyChatMessage,
        LobbySendChatMessage,
        JoinLobbyTeam,
        LobbyGameAccessRequest,
        LobbyIsInLobby,
        LobbyMasterChange,
        LobbyStateChange,
        LobbyStatusTextChange,
        LobbyMemberPropertySet,
        LeftLobby,
        LobbyPropertyChanged,
        LobbyMemberJoined,
        LobbyMemberLeft,
        LobbyMemberChangedTeam,
        LobbyMemberReadyStatusChange,
        LobbyMemberPropertyChanged,
        GetLobbyRoomAccess,
        GetLobbyMemberData,
        GetLobbyInfo,
        GetLobbyTypes,

        // Profiles
        ClientProfileRequest,
        ServerProfileRequest,
        UpdateServerProfile,
        UpdateClientProfile,
        
        //Echo
        Echo
    }
}
