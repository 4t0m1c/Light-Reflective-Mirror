using System;
using System.Net;
using Mirror;
using Mirror.Discovery;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class AuthenticatedServerFoundUnityEvent : UnityEvent<AuthenticatedServerResponse> { }

public class AuthenticatedNetworkDiscovery : NetworkDiscoveryBase<AuthenticatedServerRequest, AuthenticatedServerResponse> {

    #region Configuration

    [Header ("Discovery Settings")]
    [SerializeField] private string serverName = "Local Server";
    [SerializeField] private int maxPlayers = 10;
    [SerializeField] private string uniqueServerId;

    public AuthenticatedServerFoundUnityEvent OnAuthenticatedServerFound;

    private string groupId = "default";
    private int authLevel = 0;
    private Transport discoveryTransport;

    #endregion

    #region Initialization

    private void Awake () {
        uniqueServerId = string.IsNullOrEmpty (uniqueServerId)
            ? Guid.NewGuid ().ToString ()
            : uniqueServerId;
    }

    public void Initialize (Transport discoveryTransport, string groupId, int authLevel) {
        this.discoveryTransport = discoveryTransport;
        this.groupId = groupId;
        this.authLevel = authLevel;

        Debug.Log ($"Network Discovery initialized - Transport: {discoveryTransport.GetType ().Name}");
    }

    #endregion

    #region Discovery Protocol

    protected override AuthenticatedServerRequest GetRequest () {
        return new AuthenticatedServerRequest {
            GroupId = groupId,
            AuthLevel = authLevel
        };
    }

    protected override AuthenticatedServerResponse ProcessRequest (AuthenticatedServerRequest request, IPEndPoint endpoint) {
        if (!ValidateRequest (request, endpoint)) {
            return new AuthenticatedServerResponse ();
        }

        try {
            return new AuthenticatedServerResponse {
                Uri = discoveryTransport?.ServerUri () ?? transport.ServerUri (),
                ServerId = ServerId,
                UniqueServerId = uniqueServerId,
                GroupId = groupId,
                AuthLevel = authLevel,
                ServerName = serverName,
                CurrentPlayers = NetworkServer.connections.Count,
                MaxPlayers = maxPlayers,
                IsLocal = true
            };
        } catch (Exception e) {
            Debug.LogError ($"Discovery response creation failed: {e.Message}");
            return new AuthenticatedServerResponse ();
        }
    }

    protected override void ProcessResponse (AuthenticatedServerResponse response, IPEndPoint endpoint) {
        if (!ValidateResponse (response)) {
            return;
        }

        ConfigureResponseEndpoint (ref response, endpoint);
        OnAuthenticatedServerFound?.Invoke (response);
    }

    #endregion

    #region Validation

    private bool ValidateRequest (AuthenticatedServerRequest request, IPEndPoint endpoint) {
        if (request.GroupId != groupId) {
            Debug.LogWarning ($"Discovery request from {endpoint} - Group mismatch: {request.GroupId}");
            return false;
        }

        if (request.AuthLevel < authLevel) {
            Debug.LogWarning ($"Discovery request from {endpoint} - Insufficient authority: {request.AuthLevel}");
            return false;
        }

        return true;
    }

    private bool ValidateResponse (AuthenticatedServerResponse response) {
        if (string.IsNullOrEmpty (response.GroupId) || response.GroupId != groupId) {
            Debug.LogWarning ($"Invalid response GroupId: {response.GroupId}");
            return false;
        }

        if (response.AuthLevel > authLevel) {
            Debug.LogWarning ($"Insufficient authority for response: {response.AuthLevel}");
            return false;
        }

        return true;
    }

    private void ConfigureResponseEndpoint (ref AuthenticatedServerResponse response, IPEndPoint endpoint) {
        response.EndPoint = endpoint;

        if (response.Uri != null) {
            response.Uri = new UriBuilder (response.Uri) {
                Host = response.EndPoint.Address.ToString ()
            }.Uri;
        }
    }

    #endregion

    #region Public Interface

    public void UpdateConfiguration (string newGroupId, int newAuthLevel) {
        groupId = newGroupId;
        authLevel = newAuthLevel;
        Debug.Log ($"Discovery configuration updated - Group: {groupId}, Auth: {authLevel}");
    }

    #endregion

}

// Enhanced request/response structures
public struct AuthenticatedServerRequest : NetworkMessage {
    public string GroupId;
    public int AuthLevel;
    // Add additional client authentication fields if needed
}

public struct AuthenticatedServerResponse : NetworkMessage {
    // Mirror's built-in fields
    public Uri Uri;
    public long ServerId;
    public System.Net.IPEndPoint EndPoint { get; set; }

    // Custom fields for authentication and identification
    public string UniqueServerId; // Unique identifier that can be used across local and relay scenarios
    public string GroupId;
    public int AuthLevel;
    public string ServerName;
    public int CurrentPlayers;
    public int MaxPlayers;
    public bool IsLocal;
}