using System.Collections;
using UnityEngine;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using LightReflectiveMirror;
using Mirror;
using System.Collections.Generic;

public class LRMDebugMonitor : MonoBehaviour {
#if ODIN_INSPECTOR
    [Required]
    [SerializeField] LightReflectiveMirrorTransport _lrm;

    void OnValidate () {
        if (_lrm == null)
            _lrm = GetComponent<LightReflectiveMirrorTransport> ();
    }

    [FoldoutGroup ("Connection Status")]
    [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
    bool IsAuthenticated => _lrm?.IsAuthenticated () ?? false;

    [FoldoutGroup ("Connection Status")]
    [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
    string ServerStatus => _lrm?.serverStatus ?? "No Transport";

    [FoldoutGroup ("Room Info")]
    [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
    bool IsServer => _lrm?.IsServer ?? false;

    [FoldoutGroup ("Room Info")]
    [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
    bool IsClient => _lrm?.IsClient ?? false;

    [FoldoutGroup ("Room Info")]
    [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
    string ServerId => _lrm?.serverId ?? "None";

    [FoldoutGroup ("Room Info")]
    [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
    string GroupId => _lrm?.groupId ?? "None";

    [FoldoutGroup ("Room Info")]
    [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
    int AuthorityLevel => _lrm?.authorityLevel ?? -1;

    [FoldoutGroup ("Available Rooms"), HideReferenceObjectPicker]
    [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly, ListDrawerSettings (ShowIndexLabels = true)]
    List<RoomDebugInfo> Rooms => GetFormattedRooms ();

    bool _serverListUpdated = false;

    List<RoomDebugInfo> GetFormattedRooms () {
        if (_lrm?.relayServerList == null) return new List<RoomDebugInfo> ();

        var formattedRooms = new List<RoomDebugInfo> ();
        foreach (var room in _lrm.relayServerList) {
            formattedRooms.Add (new RoomDebugInfo (room));
        }

        return formattedRooms;
    }

    [Button ("Refresh Server List"), GUIColor (0, 1, 0)]
    void RefreshServerList (
        [LabelText ("Group ID")] string groupId = "test",
        [LabelText ("Auth Level"), PropertyRange (0, 10)]
        int authLevel = 1,
        [LabelText ("Show Higher Auth"), ToggleLeft]
        bool showHigherAuth = false) {
        if (_lrm != null && _lrm.IsAuthenticated ()) {
            AddLog ($"Requesting server list - Group: {groupId}, Auth: {authLevel}");
            _lrm.RequestServerList (groupId, showHigherAuth ? 10 : authLevel);
            StartCoroutine (LogRoomsAfterUpdate ());
        }
    }

    IEnumerator LogRoomsAfterUpdate () {
        yield return new WaitUntil (() => _serverListUpdated);
        _serverListUpdated = false;

        if (_lrm.relayServerList.Count == 0) {
            AddLog ("No rooms found");
            yield break;
        }

        foreach (var room in _lrm.relayServerList) {
            AddLog ($"Room: {room.serverName} (Auth: {room.authorityLevel}, Group: {room.groupId})");
        }
    }

    [Button ("Join Room")]
    void JoinRoom (
        [LabelText ("Room ID")] string roomId,
        [LabelText ("Group ID")] string groupId = "test",
        [LabelText ("Auth Level"), PropertyRange (0, 10)]
        int authLevel = 1) {
        if (!_lrm.IsAuthenticated ()) return;

        _lrm.groupId = groupId;
        _lrm.authorityLevel = authLevel;
        NetworkManager.singleton.networkAddress = roomId;
        NetworkManager.singleton.StartClient ();
        AddLog ($"Attempting to join room {roomId} (Group: {groupId}, Auth: {authLevel})");
    }

    [TitleGroup ("Logs")]
    [Sirenix.OdinInspector.ShowInInspector, TextArea (5, 10)]
    string _logOutput = "";

    void OnEnable () {
        if (_lrm != null) {
            _lrm.connectedToRelay.AddListener (() => AddLog ("Connected to relay"));
            _lrm.disconnectedFromRelay.AddListener (() => AddLog ("Disconnected from relay"));
            _lrm.serverListUpdated.AddListener (() => {
                _serverListUpdated = true;
                AddLog ("Server list updated");
            });
        }
    }

    void OnDisable () {
        if (_lrm != null) {
            _lrm.connectedToRelay.RemoveAllListeners ();
            _lrm.disconnectedFromRelay.RemoveAllListeners ();
            _lrm.serverListUpdated.RemoveAllListeners ();
        }
    }

    void AddLog (string message) {
        string timestamp = System.DateTime.Now.ToString ("HH:mm:ss.fff");
        _logOutput = $"[{timestamp}] {message}\n{_logOutput}";
        if (_logOutput.Length > 2000) // Prevent the log from growing too large
            _logOutput = _logOutput.Substring (0, 2000);

        Debug.Log ($"[LRM Debug] {message}");
    }

    [TitleGroup ("Logs")]
    [Button ("Clear Logs")]
    void ClearLogs () {
        _logOutput = "";
    }

    [System.Serializable]
    public class RoomDebugInfo {
        [HorizontalGroup ("Basic"), LabelWidth (80)]
        [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
        public string Name { get; set; }

        [HorizontalGroup ("Basic"), LabelWidth (60)]
        [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
        public string ID { get; set; }

        [HorizontalGroup ("Group"), LabelWidth (80)]
        [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
        public string GroupId { get; set; }

        [HorizontalGroup ("Group"), LabelWidth (60)]
        [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
        public int Authority { get; set; }

        [HorizontalGroup ("Players"), LabelWidth (80)]
        [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
        public string Players { get; set; }

        [VerticalGroup ("ExtraInfo"), LabelWidth (60)]
        [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
        public string Extra { get; set; }

        [VerticalGroup ("ExtraInfo"), LabelWidth (60)]
        [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
        public string Version { get; set; }

        public RoomDebugInfo (Room room) {
            Name = room.serverName;
            ID = room.serverId;
            GroupId = room.groupId;
            Authority = room.authorityLevel;
            Players = $"{room.currentPlayers}/{room.maxPlayers}";
            Extra = room.serverData;
            Version = room.version;
        }
    }

    [TitleGroup ("Quick Actions")]
    [Button ("Stop Host/Client"), GUIColor (1, 0.3f, 0.3f)]
    void StopAll () {
        if (NetworkServer.active || NetworkClient.active) {
            NetworkManager.singleton.StopHost ();
            NetworkManager.singleton.StopClient ();
        }
    }

    [TitleGroup ("Quick Room Creation")]
    [Button ("Create Test Room")]
    void CreateTestRoom (
        [LabelText ("Room Name")] string roomName = "Test Room",
        [LabelText ("Group ID")] string groupId = "test",
        [LabelText ("Authority")] int authority = 1,
        [LabelText ("Max Players")] int maxPlayers = 10) {
        if (_lrm == null || !_lrm.IsAuthenticated ()) return;

        _lrm.serverName = roomName;
        _lrm.groupId = groupId;
        _lrm.authorityLevel = authority;
        _lrm.maxServerPlayers = maxPlayers;
        _lrm.isPublicServer = true;

        NetworkManager.singleton.StartHost ();
    }
#else
    [SerializeField, Mirror.ReadOnly] string OdinInspectorMissing = "Odin Inspector required for this class";
#endif
}