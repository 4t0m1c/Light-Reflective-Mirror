using System;
using UnityEngine;
using UnityEngine.UI;
using LightReflectiveMirror;
using Mirror;
using System.Collections;
using System.Collections.Generic;

public class LRMAuthorityTest : MonoBehaviour {
    [Header ("UI References")]
    public Text outputDisplay;

    [Header ("Test Configuration")]
    [SerializeField] private string testGroupId = "testGroup123";
    [SerializeField] private int testAuthorityLevel = 2;

    private LightReflectiveMirrorTransport _lrm;
    private bool _serverListUpdated = false;

    void Awake () {
        _lrm = Transport.active as LightReflectiveMirrorTransport;
        if (_lrm != null) {
            _lrm.serverListUpdated.AddListener (ServerListUpdated);
        } else {
            LogMessage ("ERROR: LRM Transport not found!");
        }
    }

    void ServerListUpdated () => _serverListUpdated = true;

    [ContextMenu ("Test Create Instructor Room")]
    void TestCreateInstructorRoom () {
        StartCoroutine (CreateRoomWithAuthority (2, "Instructor Training Room"));
    }

    [ContextMenu ("Test Create Manager Room")]
    void TestCreateManagerRoom () {
        StartCoroutine (CreateRoomWithAuthority (3, "Manager Training Room"));
    }

    [ContextMenu ("Test Create Trainee Room")]
    void TestCreateTraineeRoom () {
        StartCoroutine (CreateRoomWithAuthority (1, "Trainee Training Room"));
    }

    [ContextMenu ("Test List Rooms As Manager")]
    void TestListRoomsAsManager () {
        StartCoroutine (RequestRoomListWithAuth (3));
    }

    [ContextMenu ("Test List Rooms As Instructor")]
    void TestListRoomsAsInstructor () {
        StartCoroutine (RequestRoomListWithAuth (2));
    }

    [ContextMenu ("Test List Rooms As Trainee")]
    void TestListRoomsAsTrainee () {
        StartCoroutine (RequestRoomListWithAuth (1));
    }

    [ContextMenu ("Stop Current Host/Client")]
    void StopCurrentSession () {
        if (NetworkServer.active || NetworkClient.active) {
            NetworkManager.singleton.StopHost ();
            NetworkManager.singleton.StopClient ();
            LogMessage ("Stopped current network session");
        }
    }

    private IEnumerator CreateRoomWithAuthority (int authorityLevel, string roomName) {
        if (!_lrm.IsAuthenticated ()) {
            LogMessage ("Waiting for LRM to authenticate...");
            yield return new WaitUntil (() => _lrm.IsAuthenticated ());
        }

        LogMessage ($"Creating room with authority level {authorityLevel}...");

        _lrm.serverName = roomName;
        _lrm.extraServerData = $"Auth Level: {authorityLevel}";
        _lrm.maxServerPlayers = 10;
        _lrm.isPublicServer = true;
        _lrm.groupId = testGroupId;
        _lrm.authorityLevel = authorityLevel;

        NetworkManager.singleton.StartHost ();

        yield return new WaitUntil (() => _lrm.serverId.Length > 0);
        LogMessage ($"<color=green>Room created successfully!</color>");
        LogMessage ($"Room ID: {_lrm.serverId}");
        LogMessage ($"Authority Level: {authorityLevel}");
        LogMessage ($"Group ID: {testGroupId}");
    }

    private IEnumerator RequestRoomListWithAuth (int authorityLevel) {
        if (!_lrm.IsAuthenticated ()) {
            LogMessage ("Waiting for LRM to authenticate...");
            yield return new WaitUntil (() => _lrm.IsAuthenticated ());
        }

        LogMessage ($"\n=== Requesting room list with authority level {authorityLevel} ===");
        _serverListUpdated = false;
        _lrm.RequestServerList (testGroupId, authorityLevel);

        yield return new WaitUntil (() => _serverListUpdated);

        if (_lrm.relayServerList.Count == 0) {
            LogMessage ("No rooms found");
        } else {
            foreach (var room in _lrm.relayServerList) {
                LogMessage ($"\nRoom: {room.serverName}");
                LogMessage ($"Server ID: {room.serverId}");
                LogMessage ($"Authority Level: {room.authorityLevel}");
                LogMessage ($"Group ID: {room.groupId}");
                LogMessage ($"Players: {room.currentPlayers}/{room.maxPlayers}");
                LogMessage ($"Extra Data: {room.serverData}");
                LogMessage ("-------------------");
            }
        }
    }

    private void LogMessage (string message) {
        Debug.Log (message);
        if (outputDisplay != null) {
            outputDisplay.text += $"\n{message}";

            // Keep only the last 20 lines to prevent UI overflow
            string[] lines = outputDisplay.text.Split ('\n');
            if (lines.Length > 20) {
                outputDisplay.text = string.Join ("\n",
                    new ArraySegment<string> (lines, lines.Length - 20, 20));
            }
        }
    }

    [ContextMenu ("Clear Log Display")]
    public void ClearDisplay () {
        if (outputDisplay != null) {
            outputDisplay.text = "Log cleared";
        }
    }

    // Helper function to test joining rooms
    [ContextMenu ("Test Join Latest Room")]
    void TestJoinLatestRoom () {
        StartCoroutine (JoinLatestRoomRoutine ());
    }

    private IEnumerator JoinLatestRoomRoutine () {
        if (!_lrm.IsAuthenticated ()) {
            LogMessage ("Waiting for LRM to authenticate...");
            yield return new WaitUntil (() => _lrm.IsAuthenticated ());
        }

        _serverListUpdated = false;
        _lrm.RequestServerList (testGroupId, testAuthorityLevel);

        yield return new WaitUntil (() => _serverListUpdated);

        if (_lrm.relayServerList.Count > 0) {
            var latestRoom = _lrm.relayServerList[_lrm.relayServerList.Count - 1];
            LogMessage ($"Attempting to join room: {latestRoom.serverName}");

            NetworkManager.singleton.networkAddress = latestRoom.serverId;
            NetworkManager.singleton.StartClient ();

            yield return new WaitForSeconds (2f);

            if (NetworkClient.isConnected) {
                LogMessage ("<color=green>Successfully joined room!</color>");
            } else {
                LogMessage ("<color=red>Failed to join room</color>");
            }
        } else {
            LogMessage ("No rooms available to join");
        }
    }
}