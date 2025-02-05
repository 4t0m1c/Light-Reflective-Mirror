using UnityEngine;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using kcp2k;
using LightReflectiveMirror;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

[RequireComponent (typeof (NetworkManager))]
[RequireComponent (typeof (AuthenticatedNetworkDiscovery))]
public class AuthenticatedConnectionManager : MonoBehaviour {

    #region Component References

    NetworkManager networkManager;
    AuthenticatedNetworkDiscovery networkDiscovery;

    #endregion

    #region Configuration

#if ODIN_INSPECTOR
    [TitleGroup ("Configuration", order: 0)]
    [BoxGroup ("Configuration/Transports", order: 0)]
#endif
    [SerializeField, Required]
    LightReflectiveMirrorTransport lrmTransport;

#if ODIN_INSPECTOR
    [BoxGroup ("Configuration/Transports", order: 0)]
#endif
    [SerializeField, Required]
    KcpTransport kcpTransport;

#if ODIN_INSPECTOR
    [HorizontalGroup ("Ports/Split", Width = 0.5f)]
    [BoxGroup ("Ports", order: 0)]
#endif
    [SerializeField] ushort lrmPort = 7777;
#if ODIN_INSPECTOR
    [HorizontalGroup ("Ports/Split", Width = 0.5f)]
    [BoxGroup ("Ports", order: 0)]
#endif
    [SerializeField] ushort kcpPort = 7778;

#if ODIN_INSPECTOR
    [BoxGroup ("Authentication", order: 0)]
    [TitleGroup ("Authentication/Settings")]
#endif
    [SerializeField]
    string groupId = "default";

#if ODIN_INSPECTOR
    [BoxGroup ("Authentication", order: 0)]
#endif
    [SerializeField]
    int authLevel = 0;

    [BoxGroup ("Server Refresh", order: 0)]
    [SerializeField] float refreshInterval = 1f; // 1 second refresh
    [BoxGroup ("Server Refresh", order: 0)]
    [SerializeField] bool autoRefreshEnabled = false;
    Coroutine refreshCoroutine;

    [BoxGroup ("Timeout", order: 0)]
    [SerializeField] float serverTimeoutDuration = 4f;
    
    #endregion

    #region Runtime State

#if ODIN_INSPECTOR
    [TitleGroup ("Network Status", order: 2)]
    [HorizontalGroup ("Network Status/Split", Width = 0.5f)]
    [BoxGroup ("Network Status/Split/Transport", order: 2)]
    [Sirenix.OdinInspector.ReadOnly, Sirenix.OdinInspector.ShowInInspector, LabelText ("Relay Connected")]
#endif
    bool isRelayConnected;

#if ODIN_INSPECTOR
    [BoxGroup ("Network Status/Split/Transport", order: 2)]
    [Sirenix.OdinInspector.ReadOnly, Sirenix.OdinInspector.ShowInInspector, LabelText ("Discovering")]
#endif
    bool isDiscovering = false;

#if ODIN_INSPECTOR
    [BoxGroup ("Network Status/Split/Transport", order: 2)]
    [Sirenix.OdinInspector.ReadOnly, Sirenix.OdinInspector.ShowInInspector, LabelText ("Active Transport")]
#endif
    string activeTransportName => currentTransport?.GetType ().Name ?? "None";

#if ODIN_INSPECTOR
    [BoxGroup ("Network Status/Split/Session", order: 2)]
    [Sirenix.OdinInspector.ReadOnly, Sirenix.OdinInspector.ShowInInspector, LabelText ("Is Host")]
#endif
    bool isHost => NetworkServer.active;

#if ODIN_INSPECTOR
    [BoxGroup ("Network Status/Split/Session", order: 2)]
    [Sirenix.OdinInspector.ReadOnly, Sirenix.OdinInspector.ShowInInspector, LabelText ("Is Client")]
#endif
    bool isClient => NetworkClient.active;

    Transport currentTransport;

    #endregion

    #region Session Discovery State


#if ODIN_INSPECTOR
    [BoxGroup ("Discovered Sessions", order: 2)]
    [TableList (ShowIndexLabels = true, AlwaysExpanded = true)]
    [Sirenix.OdinInspector.ReadOnly, Sirenix.OdinInspector.ShowInInspector]
#endif
    List<DiscoveredSession> discoveredSessions = new List<DiscoveredSession> ();

    Dictionary<string, DiscoveredSession> sessionLookup = new Dictionary<string, DiscoveredSession> ();

#if ODIN_INSPECTOR
    [BoxGroup ("Discovered Sessions/Local", order: 2)]
    [Sirenix.OdinInspector.ReadOnly, Sirenix.OdinInspector.ShowInInspector]
#endif
    private List<DiscoveredSession> discoveredLocalSessions = new List<DiscoveredSession> ();

#if ODIN_INSPECTOR
    [BoxGroup ("Discovered Sessions/Relay", order: 2)]
    [Sirenix.OdinInspector.ReadOnly, Sirenix.OdinInspector.ShowInInspector]
#endif
    private List<DiscoveredSession> discoveredRelaySessions = new List<DiscoveredSession> ();

#if ODIN_INSPECTOR
    [BoxGroup ("Session Statistics", order: 2)]
    [Sirenix.OdinInspector.ReadOnly, Sirenix.OdinInspector.ShowInInspector, LabelText ("Total Sessions")]
    int TotalSessions => discoveredSessions.Count;

#if ODIN_INSPECTOR
    [BoxGroup ("Session Statistics", order: 2)]
    [Sirenix.OdinInspector.ReadOnly, Sirenix.OdinInspector.ShowInInspector, LabelText ("Local Sessions")]
    int LocalSessions {
        get {
            if (discoveredSessions != null) return discoveredSessions.Count (s => s.IsLocal);
            else return 0;
        }
    }

#if ODIN_INSPECTOR
    [BoxGroup ("Session Statistics", order: 2)]
    [Sirenix.OdinInspector.ReadOnly, Sirenix.OdinInspector.ShowInInspector, LabelText ("Relay Sessions")]
    int RelaySessions => discoveredSessions.Count (s => !s.IsLocal);
#endif
#endif
#endif

    #endregion

    #region Debug Logging

#if ODIN_INSPECTOR
    [BoxGroup ("Debug Log", order: 2)]
    [TextArea (5, 10), Sirenix.OdinInspector.ReadOnly, Sirenix.OdinInspector.ShowInInspector]
#endif
    string debugLog = "";

    void LogMessage (string message, LogType type = LogType.Log) {
        string timestamp = DateTime.Now.ToString ("HH:mm:ss.fff");
        string logPrefix = type == LogType.Error ? "ERROR: " :
            type == LogType.Warning ? "WARN:  " :
            "INFO:  ";
        string logEntry = $"[{timestamp}] {logPrefix}{message}";

        debugLog = $"{logEntry}\n{debugLog}";
        if (debugLog.Length > 5000)
            debugLog = debugLog.Substring (0, 5000);

        switch (type) {
            case LogType.Warning:
                Debug.LogWarning ($"[AuthConnMgr] {message}");
                break;
            case LogType.Error:
                Debug.LogError ($"[AuthConnMgr] {message}");
                break;
            default:
                Debug.Log ($"[AuthConnMgr] {message}");
                break;
        }
    }

#if ODIN_INSPECTOR
    [BoxGroup ("Debug Log", order: 2)]
    [Button ("Clear Log")]
#endif
    void ClearLog () {
        debugLog = "";
    }

    #endregion

    #region Initialization

    void Awake () {
        InitializeComponents ();
        SetupEventListeners ();
    }

    void Update () {
        bool shouldAutoRefresh = !isHost && !isClient;

        // Enable auto-refresh if we should be and aren't currently
        if (shouldAutoRefresh && !autoRefreshEnabled) {
            StartAutoRefresh ();
        }
        // Disable auto-refresh if we shouldn't be and currently are
        else if (!shouldAutoRefresh && autoRefreshEnabled) {
            StopAutoRefresh ();
            LogMessage ("Auto-refresh stopped - Now " + (isHost ? "hosting" : "connected to host"));
        }

        // Periodically clean up stale servers (every 5 seconds)
        if (Time.time % 3f < Time.deltaTime) {
            CleanupStaleServers ();
        }
    }

    void InitializeComponents () {
        networkManager = GetComponent<NetworkManager> ();
        networkDiscovery = GetComponent<AuthenticatedNetworkDiscovery> ();

        if (networkManager == null || networkDiscovery == null) {
            LogMessage ("Required components missing!", LogType.Error);
            enabled = false;
            return;
        }

        if (lrmTransport != null) lrmTransport.serverPort = lrmPort;
        if (kcpTransport != null) kcpTransport.Port = kcpPort;

        InitializeDiscovery ();

        LogMessage ("Components initialized successfully");
    }

    void SetupEventListeners () {
        if (lrmTransport != null) {
            lrmTransport.disconnectedFromRelay.AddListener (OnRelayDisconnected);
            lrmTransport.connectedToRelay.AddListener (OnRelayConnected);
            lrmTransport.serverListUpdated.AddListener (OnRelayServersUpdated);
            LogMessage ("LRM Transport event listeners configured");
        } else {
            LogMessage ("LRM Transport not assigned!", LogType.Warning);
        }

        if (networkDiscovery != null) {
            networkDiscovery.OnAuthenticatedServerFound.AddListener (OnLocalServerDiscovered);
        }
    }

    void InitializeDiscovery () {
        networkDiscovery.Initialize (
            discoveryTransport: kcpTransport, // Local discovery uses KCP
            groupId: groupId,
            authLevel: authLevel
        );
    }

    #endregion

    #region Relay Controls

#if ODIN_INSPECTOR
    [BoxGroup ("Relay Controls", order: 1)]
    [Button ("Connect to Relay", ButtonSizes.Large), EnableIf ("@!isRelayConnected")]
#endif
    public void ConnectToRelay () {
        if (lrmTransport == null) {
            LogMessage ("LRM Transport not assigned!", LogType.Error);
            return;
        }

        if (isRelayConnected) return;

        Transport.active = lrmTransport;

        LogMessage ("Initiating relay connection...");
        lrmTransport.ConnectToRelay ();

        Invoke (nameof (RefreshServerList), 1);
    }

#if ODIN_INSPECTOR
    [BoxGroup ("Relay Controls", order: 1)]
    [Button ("Disconnect from Relay", ButtonSizes.Large), EnableIf ("@isRelayConnected")]
#endif
    public void DisconnectFromRelay () {
        if (lrmTransport == null) {
            LogMessage ("LRM Transport not assigned!", LogType.Error);
            return;
        }

        if (isRelayConnected) {
            // Stop network activity first
            if (NetworkServer.active || NetworkClient.active) {
                networkManager.StopHost ();
                networkManager.StopClient ();
            }

            // Stop discovery
            if (networkDiscovery != null) {
                networkDiscovery.StopDiscovery ();
                isDiscovering = false;
            }

            LogMessage ("Disconnecting from relay...");
            lrmTransport.DisconnectFromRelay ();

            Invoke (nameof (RefreshServerList), 0.5f);
        }
    }

    #endregion

    #region Network Controls

#if ODIN_INSPECTOR
    [BoxGroup ("Network Controls", order: 1)]
    [Button ("Host via Relay", ButtonSizes.Large), EnableIf ("@isRelayConnected && !isHost && !isClient")]
#endif
    public void StartRelayHost () {
        LogMessage ($"Starting relay host with GroupId: {groupId}, AuthLevel: {authLevel}");

        if (!isRelayConnected) ConnectToRelay ();

        try {
            networkManager.transport = lrmTransport;
            currentTransport = lrmTransport;
            lrmTransport.groupId = groupId;
            lrmTransport.authorityLevel = authLevel;
            Transport.active = lrmTransport;

            networkManager.StartHost ();
            // Don't advertise on NetworkDiscovery for relay hosts
            LogMessage ($"Relay host started successfully using {activeTransportName}");
        } catch (Exception e) {
            LogMessage ($"Failed to start relay host: {e.Message}", LogType.Error);
        }
    }

#if ODIN_INSPECTOR
    [BoxGroup ("Network Controls", order: 1)]
    [Button ("Host Locally", ButtonSizes.Large), EnableIf ("@!isHost && !isClient")]
#endif
    public void StartLocalHost () {
        LogMessage ($"Starting local host with GroupId: {groupId}, AuthLevel: {authLevel}");

        if (isRelayConnected) {
            lrmTransport.disconnectedFromRelay.AddListener (DisconnectedFromRelay);
            DisconnectFromRelay ();

            void DisconnectedFromRelay () {
                lrmTransport.disconnectedFromRelay.RemoveListener (DisconnectedFromRelay);
                StartCoroutine (DelayedHostStart ());
                return;

                IEnumerator DelayedHostStart () {
                    yield return new WaitForSeconds (1); //Have to wait some time to allow lrm transport to shut down fully before hosting
                    StartLocalHostServer ();
                }
            }
        } else {
            StartLocalHostServer ();
        }

        return;

        void StartLocalHostServer () {
            try {
                // Double check we're fully stopped
                if (NetworkServer.active || NetworkClient.active) {
                    networkManager.StopHost ();
                    networkManager.StopClient ();
                }

                if (networkDiscovery != null) {
                    networkDiscovery.StopDiscovery ();
                    isDiscovering = false;
                }

                networkManager.transport = kcpTransport;
                currentTransport = kcpTransport;
                Transport.active = kcpTransport;

                networkManager.StartHost ();
                networkDiscovery.AdvertiseServer ();
                LogMessage ($"Local host started successfully using {activeTransportName}");
            } catch (Exception e) {
                LogMessage ($"Failed to start local host: {e.Message}", LogType.Error);
            }
        }
    }

#if ODIN_INSPECTOR
    [BoxGroup ("Network Controls", order: 1)]
    [Button ("Refresh Server List", ButtonSizes.Large)]
#endif
    public void RefreshServerList () {
        // LogMessage ("Refreshing server list");

        if (!isDiscovering) {
            // Set the appropriate transport for discovery
            networkDiscovery.transport = kcpTransport;
            LogMessage ("Starting network discovery using KCP transport");
            networkDiscovery.StartDiscovery ();
            isDiscovering = true;
        } else {
            // LogMessage ("Discovery already in progress using transport: " + networkDiscovery.transport.GetType ().Name);
        }

        // Only request relay servers if we're connected to relay
        if (isRelayConnected) {
            // LogMessage ("Requesting relay server list");
            lrmTransport.RequestServerList (groupId, authLevel);
        }
    }

    public void StartAutoRefresh () {
        if (!autoRefreshEnabled) {
            autoRefreshEnabled = true;
            if (refreshCoroutine != null)
                StopCoroutine (refreshCoroutine);
            refreshCoroutine = StartCoroutine (AutoRefreshCoroutine ());
            LogMessage ("Started automatic server list refresh");
        }
    }

    public void StopAutoRefresh () {
        if (autoRefreshEnabled) {
            autoRefreshEnabled = false;
            if (refreshCoroutine != null)
                StopCoroutine (refreshCoroutine);
            refreshCoroutine = null;
            LogMessage ("Stopped automatic server list refresh");
        }
    }

    IEnumerator AutoRefreshCoroutine () {
        while (autoRefreshEnabled) {
            RefreshServerList ();
            yield return new WaitForSeconds (refreshInterval);
        }
    }

#if ODIN_INSPECTOR
    [BoxGroup ("Network Controls", order: 1)]
    [Button ("Stop All", ButtonSizes.Large), EnableIf ("@isHost || isClient")]
#endif
    public void StopAll () {
        if (NetworkServer.active || NetworkClient.active) {
            networkManager.StopHost ();
            networkManager.StopClient ();
            LogMessage ("Stopped all network activities");
        }

        if (isDiscovering) {
            networkDiscovery.StopDiscovery ();
            isDiscovering = false;
            LogMessage ("Stopped discovering");
        }
    }

    #endregion

    #region Event Handlers

    void OnRelayConnected () {
        isRelayConnected = true;
        LogMessage ("Connected to relay server");
    }

    void OnRelayDisconnected () {
        isRelayConnected = false;
        LogMessage ("Disconnected from relay server - Using local transport", LogType.Warning);
    }

    void OnLocalServerDiscovered (AuthenticatedServerResponse response) {
        var session = new DiscoveredSession {
            ServerId = response.UniqueServerId,
            ServerName = response.ServerName,
            GroupId = response.GroupId,
            AuthLevel = response.AuthLevel,
            CurrentPlayers = response.CurrentPlayers,
            MaxPlayers = response.MaxPlayers,
            IsLocal = true,
            EndPoint = response.EndPoint,
            LastSeenTime = DateTime.Now,
            IsActive = true
        };

        // Update or add the local session
        var existingIndex = discoveredLocalSessions.FindIndex (s => s.ServerId == session.ServerId);
        if (existingIndex != -1) {
            // Update existing session
            discoveredLocalSessions[existingIndex] = session;
        } else {
            // Add new session
            discoveredLocalSessions.Add (session);
        }

        UpdateMainSessionList ();
    }

    void OnRelayServersUpdated () {
        if (lrmTransport?.relayServerList == null) return;

        discoveredRelaySessions.Clear ();
        foreach (var relayRoom in lrmTransport.relayServerList) {
            var newSession = new DiscoveredSession {
                ServerId = relayRoom.serverId,
                ServerName = relayRoom.serverName,
                GroupId = relayRoom.groupId,
                AuthLevel = relayRoom.authorityLevel,
                CurrentPlayers = relayRoom.currentPlayers,
                MaxPlayers = relayRoom.maxPlayers,
                IsLocal = false,
                LastSeenTime = DateTime.Now,
                IsActive = true
            };
            discoveredRelaySessions.Add (newSession);
        }

        UpdateMainSessionList ();
    }

    void UpdateMainSessionList () {
        discoveredSessions.Clear ();
        sessionLookup.Clear ();

        // Add all local sessions
        foreach (var session in discoveredLocalSessions) {
            discoveredSessions.Add (session);
            sessionLookup[session.ServerId] = session;
        }

        // Add all relay sessions
        foreach (var session in discoveredRelaySessions) {
            discoveredSessions.Add (session);
            sessionLookup[session.ServerId] = session;
        }

        // LogMessage ($"Updated main session list - Local: {discoveredLocalSessions.Count}, Relay: {discoveredRelaySessions.Count}");
    }

    #endregion

    #region Connection Management

#if ODIN_INSPECTOR
    [TitleGroup ("Session", order: 1)]
    [BoxGroup ("Session/Session Controls", order: 1)]
    [Button ("Connect to Selected Session"), EnableIf ("@HasSelectedSession && !isClient")]
#endif
    public void ConnectToSelectedSession () {
        if (selectedSession == null) {
            LogMessage ("No session selected", LogType.Warning);
            return;
        }

        ConnectToSession (selectedSession);
    }

#if ODIN_INSPECTOR
    [TitleGroup ("Session", order: 1)]
    [BoxGroup ("Session/Session Controls", order: 1)]
    [Sirenix.OdinInspector.ShowInInspector]
    [ValueDropdown ("discoveredSessions", IsUniqueList = true)]
#endif
    DiscoveredSession selectedSession;

    bool HasSelectedSession => selectedSession != null;

    public void ConnectToSession (DiscoveredSession session) {
        if (session == null) {
            LogMessage ("Invalid session", LogType.Error);
            return;
        }

        LogMessage ($"Connecting to {(session.IsLocal ? "local" : "relay")} session: {session.ServerName}");

        try {
            // Select appropriate transport
            networkManager.transport = session.IsLocal ? kcpTransport : lrmTransport;
            Transport.active = networkManager.transport;
            currentTransport = networkManager.transport;

            if (!session.IsLocal) {
                lrmTransport.groupId = session.GroupId;
                lrmTransport.authorityLevel = authLevel;
                networkManager.networkAddress = session.ServerId;
            } else {
                networkManager.networkAddress = session.EndPoint.Address.ToString ();
            }

            networkManager.StartClient ();
            LogMessage ($"Connection attempt initiated using {currentTransport.GetType ().Name}");
        } catch (Exception e) {
            LogMessage ($"Connection failed: {e.Message}", LogType.Error);
        }
    }

    #endregion

    #region Authentication

#if ODIN_INSPECTOR
    [BoxGroup ("Authentication", order: 1)]
    [Button ("Update Authentication")]
#endif
    void ConfigureAuthentication (string newGroupId, int newAuthLevel) {
        LogMessage ($"Updating authentication - GroupId: {newGroupId}, AuthLevel: {newAuthLevel}");

        groupId = newGroupId;
        authLevel = newAuthLevel;

        networkDiscovery.UpdateConfiguration (groupId, authLevel);

        if (lrmTransport != null) {
            lrmTransport.groupId = groupId;
            lrmTransport.authorityLevel = authLevel;
        }
    }

    #endregion

    #region Session Cleanup

    void CleanupStaleServers () {
        DateTime now = DateTime.Now;

        // Clean up only local sessions (relay servers are always precise and managed separately)
        discoveredLocalSessions.RemoveAll (session =>
            !session.IsActive ||
            (now - session.LastSeenTime).TotalSeconds > serverTimeoutDuration);

        // Update the main session list
        UpdateMainSessionList ();
    }

    void OnDestroy () {
        if (lrmTransport != null) {
            lrmTransport.disconnectedFromRelay.RemoveListener (OnRelayDisconnected);
            lrmTransport.connectedToRelay.RemoveListener (OnRelayConnected);
            lrmTransport.serverListUpdated.RemoveListener (OnRelayServersUpdated);
        }

        if (networkDiscovery != null) {
            networkDiscovery.OnAuthenticatedServerFound.RemoveListener (OnLocalServerDiscovered);
        }

        StopAll ();
    }

    #endregion

}

[Serializable]
public class DiscoveredSession {
    public string ServerId;
    public string ServerName;
    public string GroupId;
    public int AuthLevel;
    public int CurrentPlayers;
    public int MaxPlayers;
    public bool IsLocal;
    public System.Net.IPEndPoint EndPoint;

    // New property to track when the server was last seen
    public DateTime LastSeenTime { get; set; }

    // New property to track if the server is considered active
    public bool IsActive { get; set; } = true;

    public string DisplayName => $"{ServerName} ({CurrentPlayers}/{MaxPlayers})" +
                                 $" [{(IsLocal ? "Local" : "Relay")}]" +
                                 $" - Group: {GroupId}, Auth: {AuthLevel}" +
                                 $" - Last Seen: {LastSeenTime:HH:mm:ss}";
}